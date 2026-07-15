using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.Data.Sqlite;
using SkillExam.Core.Abstractions;
using SkillExam.Core.Exam;
using SkillExam.Core.Models;
using SkillExam.Infrastructure.Persistence;

namespace SkillExam.Infrastructure.Migration;

public sealed class LegacyDataMigrator(SqliteDatabase database, AppDataPaths paths, IClock clock)
{
    private const string MigrationMarkerKey = "legacy_json_migration_version";
    private const string MigrationVersion = "1";
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public async Task<LegacyMigrationResult> MigrateAsync(string legacyDirectory, CancellationToken cancellationToken = default)
    {
        var directory = Path.GetFullPath(legacyDirectory);
        var configPath = Path.Combine(directory, "config.json");
        var errorsPath = Path.Combine(directory, "error_questions.json");
        var progressPath = Path.Combine(directory, "practice_progress.json");
        var existingFiles = new[] { configPath, errorsPath, progressPath }.Where(File.Exists).ToArray();
        if (existingFiles.Length == 0)
        {
            return new LegacyMigrationResult(false, 0, 0, 0, null, ["所选目录没有旧版 JSON 数据。"]);
        }

        await using (var markerConnection = await database.OpenConnectionAsync(cancellationToken))
        {
            var markerCommand = markerConnection.CreateCommand();
            markerCommand.CommandText = "SELECT value FROM settings WHERE key = $key;";
            markerCommand.Parameters.AddWithValue("$key", MigrationMarkerKey);
            if (string.Equals(await markerCommand.ExecuteScalarAsync(cancellationToken) as string, MigrationVersion, StringComparison.Ordinal))
            {
                return new LegacyMigrationResult(true, 0, 0, 0, null, []);
            }
        }

        var warnings = new List<string>();
        var legacyErrors = File.Exists(errorsPath) ? ReadErrors(errorsPath, warnings) : [];
        var legacyBankPath = File.Exists(configPath) ? ReadBankPath(configPath, warnings) : null;
        var progressDocuments = File.Exists(progressPath) ? ReadProgress(progressPath, warnings) : [];
        var backupDirectory = CreateBackup(existingFiles);

        await using var connection = await database.OpenConnectionAsync(cancellationToken);
        await using var transaction = (SqliteTransaction)await connection.BeginTransactionAsync(cancellationToken);
        var migratedAttempts = 0;
        var migratedProgress = 0;
        try
        {
            foreach (var legacyError in legacyErrors)
            {
                await ErrorBookRepository.UpsertQuestionAsync(connection, transaction, legacyError.Question, legacyError.AttemptedAt, cancellationToken);
                await ErrorBookRepository.InsertAttemptAsync(
                    connection,
                    transaction,
                    legacyError.Question.Id,
                    legacyError.UserAnswer,
                    "legacy",
                    legacyError.AttemptedAt,
                    cancellationToken);
                migratedAttempts++;
            }

            if (!string.IsNullOrWhiteSpace(legacyBankPath))
            {
                var settings = await ReadSettingsAsync(connection, transaction, cancellationToken);
                settings = settings with { LastQuestionBankPath = legacyBankPath };
                await SettingsRepository.UpsertAsync(
                    connection,
                    transaction,
                    SettingsRepository.SettingsKey,
                    JsonSerializer.Serialize(settings, JsonOptions),
                    clock.Now,
                    cancellationToken);
            }

            foreach (var progress in progressDocuments)
            {
                var command = connection.CreateCommand();
                command.Transaction = transaction;
                command.CommandText = """
                    INSERT INTO practice_sessions(
                        session_id, levels_key, categories_key, question_type, question_ids_json,
                        current_question_id, pending_retry_json, legacy_payload_json, updated_at)
                    VALUES ($id, $levels, '', $type, '[]', NULL, '[]', $payload, $updatedAt)
                    ON CONFLICT(session_id) DO NOTHING;
                    """;
                command.Parameters.AddWithValue("$id", progress.SessionId);
                command.Parameters.AddWithValue("$levels", progress.LevelsKey);
                command.Parameters.AddWithValue("$type", (int)progress.QuestionType);
                command.Parameters.AddWithValue("$payload", progress.Payload);
                command.Parameters.AddWithValue("$updatedAt", clock.Now.ToString("O"));
                migratedProgress += await command.ExecuteNonQueryAsync(cancellationToken);
            }

            await SettingsRepository.UpsertAsync(
                connection,
                transaction,
                MigrationMarkerKey,
                MigrationVersion,
                clock.Now,
                cancellationToken);
            await transaction.CommitAsync(cancellationToken);
        }
        catch
        {
            await transaction.RollbackAsync(CancellationToken.None);
            throw;
        }

        return new LegacyMigrationResult(false, legacyErrors.Count, migratedAttempts, migratedProgress, backupDirectory, warnings);
    }

    private static IReadOnlyList<LegacyError> ReadErrors(string filePath, ICollection<string> warnings)
    {
        using var document = JsonDocument.Parse(File.ReadAllText(filePath, Encoding.UTF8));
        if (document.RootElement.ValueKind != JsonValueKind.Array)
        {
            throw new InvalidDataException("error_questions.json 的根节点必须是数组。 ");
        }

        var result = new List<LegacyError>();
        var index = 0;
        foreach (var element in document.RootElement.EnumerateArray())
        {
            index++;
            var typeText = GetString(element, "type");
            var text = GetString(element, "question");
            var answer = GetString(element, "correct_answer");
            if (!QuestionTypeExtensions.TryParseDisplayName(typeText, out var type) ||
                string.IsNullOrWhiteSpace(text) || string.IsNullOrWhiteSpace(answer))
            {
                warnings.Add($"旧错题第 {index} 条缺少题型、题目或答案，未迁移。");
                continue;
            }

            var options = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            if (element.TryGetProperty("options", out var optionsElement) && optionsElement.ValueKind == JsonValueKind.Object)
            {
                foreach (var option in optionsElement.EnumerateObject())
                {
                    options[option.Name] = option.Value.ToString();
                }
            }

            var source = GetString(element, "source_sheet");
            source = string.IsNullOrWhiteSpace(source) ? "旧版题库" : source;
            var categories = DetectCategories(source, text);
            var question = new Question
            {
                Id = QuestionIdentity.Create(type, text, options, answer, source),
                Type = type,
                Text = text,
                Options = options,
                Answer = answer,
                Levels = new HashSet<SkillLevel>(),
                Source = source,
                SourceSheet = source,
                Categories = categories
            };
            var timestampText = GetString(element, "timestamp");
            var attemptedAt = DateTimeOffset.TryParse(timestampText, out var timestamp) ? timestamp : DateTimeOffset.Now;
            result.Add(new LegacyError(question, GetString(element, "user_answer"), attemptedAt));
        }
        return result;
    }

    private static string? ReadBankPath(string filePath, ICollection<string> warnings)
    {
        try
        {
            using var document = JsonDocument.Parse(File.ReadAllText(filePath, Encoding.UTF8));
            return GetString(document.RootElement, "last_bank_path");
        }
        catch (JsonException exception)
        {
            warnings.Add($"config.json 无法解析：{exception.Message}");
            return null;
        }
    }

    private static IReadOnlyList<LegacyProgress> ReadProgress(string filePath, ICollection<string> warnings)
    {
        try
        {
            using var document = JsonDocument.Parse(File.ReadAllText(filePath, Encoding.UTF8));
            if (document.RootElement.ValueKind != JsonValueKind.Object)
            {
                warnings.Add("practice_progress.json 的根节点不是对象，已跳过。");
                return [];
            }

            var result = new List<LegacyProgress>();
            foreach (var levelGroup in document.RootElement.EnumerateObject())
            {
                if (levelGroup.Value.ValueKind != JsonValueKind.Object)
                {
                    continue;
                }
                var levels = levelGroup.Name.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries)
                    .Select(value => SkillLevelExtensions.TryParseDisplayName(value, out var level) ? level : (SkillLevel?)null)
                    .Where(level => level.HasValue)
                    .Select(level => level!.Value)
                    .ToArray();
                foreach (var typeGroup in levelGroup.Value.EnumerateObject())
                {
                    if (!QuestionTypeExtensions.TryParseDisplayName(typeGroup.Name, out var type))
                    {
                        warnings.Add($"旧练习进度包含未知题型“{typeGroup.Name}”，已跳过。");
                        continue;
                    }
                    var canonical = $"{levelGroup.Name}|{typeGroup.Name}";
                    var id = "legacy-" + Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(canonical))).ToLowerInvariant()[..24];
                    result.Add(new LegacyProgress(id, ProgressRepository.GetLevelsKey(levels), type, typeGroup.Value.GetRawText()));
                }
            }
            return result;
        }
        catch (JsonException exception)
        {
            warnings.Add($"practice_progress.json 无法解析：{exception.Message}");
            return [];
        }
    }

    private string CreateBackup(IEnumerable<string> files)
    {
        paths.EnsureCreated();
        var backupDirectory = Path.Combine(paths.BackupsDirectory, $"legacy-{clock.Now:yyyyMMdd_HHmmss_fff}");
        if (Directory.Exists(backupDirectory))
        {
            backupDirectory += "-" + Guid.NewGuid().ToString("N")[..8];
        }
        Directory.CreateDirectory(backupDirectory);
        foreach (var file in files)
        {
            File.Copy(file, Path.Combine(backupDirectory, Path.GetFileName(file)), overwrite: false);
        }
        return backupDirectory;
    }

    private static async Task<AppSettings> ReadSettingsAsync(
        SqliteConnection connection,
        SqliteTransaction transaction,
        CancellationToken cancellationToken)
    {
        var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = "SELECT value FROM settings WHERE key = $key;";
        command.Parameters.AddWithValue("$key", SettingsRepository.SettingsKey);
        var value = await command.ExecuteScalarAsync(cancellationToken) as string;
        return value is null ? new AppSettings() : JsonSerializer.Deserialize<AppSettings>(value, JsonOptions) ?? new AppSettings();
    }

    private static string GetString(JsonElement element, string propertyName) =>
        element.ValueKind == JsonValueKind.Object && element.TryGetProperty(propertyName, out var property) && property.ValueKind != JsonValueKind.Null
            ? property.ToString().Trim()
            : string.Empty;

    private static IReadOnlySet<QuestionCategory> DetectCategories(string source, string text)
    {
        var value = $"{source} {text}";
        var result = new HashSet<QuestionCategory>();
        if (value.Contains("基站", StringComparison.OrdinalIgnoreCase)) result.Add(QuestionCategory.BaseStation);
        if (value.Contains("地宝", StringComparison.OrdinalIgnoreCase)) result.Add(QuestionCategory.GroundRobot);
        if (value.Contains("窗宝", StringComparison.OrdinalIgnoreCase)) result.Add(QuestionCategory.WindowRobot);
        if (value.Contains("光学组件", StringComparison.OrdinalIgnoreCase)) result.Add(QuestionCategory.OpticalComponent);
        return result;
    }

    private sealed record LegacyError(Question Question, string UserAnswer, DateTimeOffset AttemptedAt);
    private sealed record LegacyProgress(string SessionId, string LevelsKey, QuestionType QuestionType, string Payload);
}
