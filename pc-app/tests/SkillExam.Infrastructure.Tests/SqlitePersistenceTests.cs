using Microsoft.Data.Sqlite;
using SkillExam.Core.Exam;
using SkillExam.Core.Models;
using SkillExam.Infrastructure.Persistence;

namespace SkillExam.Infrastructure.Tests;

public sealed class SqlitePersistenceTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), $"skill-exam-db-{Guid.NewGuid():N}");

    [Fact]
    public async Task SchemaMigration_IsVersionedAndIdempotent()
    {
        var database = CreateDatabase();
        await database.InitializeAsync();
        await database.InitializeAsync();

        await using var connection = await database.OpenConnectionAsync();
        var command = connection.CreateCommand();
        command.CommandText = "SELECT COUNT(*) FROM schema_versions WHERE version = 1;";
        Assert.Equal(1L, Convert.ToInt64(await command.ExecuteScalarAsync()));
        foreach (var table in new[] { "settings", "error_questions", "error_attempts", "practice_sessions", "practice_answers" })
        {
            var tableCommand = connection.CreateCommand();
            tableCommand.CommandText = "SELECT COUNT(*) FROM sqlite_master WHERE type='table' AND name=$name;";
            tableCommand.Parameters.AddWithValue("$name", table);
            Assert.Equal(1L, Convert.ToInt64(await tableCommand.ExecuteScalarAsync()));
        }
    }

    [Fact]
    public async Task FailedMigration_RollsBackSchemaAndVersion()
    {
        var database = new SqliteDatabase(Path.Combine(_root, "rollback.db"), [new FailingMigration()]);
        await Assert.ThrowsAsync<InvalidOperationException>(() => database.InitializeAsync());

        await using var connection = await database.OpenConnectionAsync();
        var command = connection.CreateCommand();
        command.CommandText = "SELECT COUNT(*) FROM sqlite_master WHERE type='table' AND name='should_rollback';";
        Assert.Equal(0L, Convert.ToInt64(await command.ExecuteScalarAsync()));
        command.CommandText = "SELECT COUNT(*) FROM schema_versions WHERE version=2;";
        Assert.Equal(0L, Convert.ToInt64(await command.ExecuteScalarAsync()));
    }

    [Fact]
    public async Task SettingsProgressAndErrors_RoundTripWithStableIdsAndHistoryAggregation()
    {
        var database = CreateDatabase();
        await database.InitializeAsync();
        var settingsRepository = new SettingsRepository(database);
        var progressRepository = new ProgressRepository(database);
        var errorRepository = new ErrorBookRepository(database);
        var settings = new AppSettings { LastQuestionBankPath = @"D:\bank.xlsx", AutoSpeechEnabled = false, SpeechRate = 2 };
        await settingsRepository.SaveAsync(settings);
        var restoredSettings = await settingsRepository.GetAsync();
        Assert.Equal(settings.LastQuestionBankPath, restoredSettings.LastQuestionBankPath);
        Assert.Equal(settings.AutoSpeechEnabled, restoredSettings.AutoSpeechEnabled);
        Assert.Equal(settings.SpeechRate, restoredSettings.SpeechRate);

        var question = CreateQuestion("稳定题目");
        var snapshot = new PracticeSessionSnapshot
        {
            SessionId = "practice-1",
            Levels = [SkillLevel.Level1, SkillLevel.Level2],
            Categories = [QuestionCategory.BaseStation],
            QuestionType = QuestionType.SingleChoice,
            QuestionIds = [question.Id],
            CurrentQuestionId = question.Id,
            Answers = new Dictionary<string, string> { [question.Id] = "B" },
            PendingRetryQuestionIds = new HashSet<string> { question.Id },
            UpdatedAt = DateTimeOffset.Parse("2026-07-15T01:00:00Z")
        };
        await progressRepository.SaveAsync(snapshot);
        var restored = await progressRepository.GetLatestAsync(snapshot.Levels, snapshot.Categories, snapshot.QuestionType);
        Assert.NotNull(restored);
        Assert.Equal(question.Id, restored.CurrentQuestionId);
        Assert.Equal("B", restored.Answers[question.Id]);

        await errorRepository.AddAttemptAsync(question, "B", "exam", DateTimeOffset.Parse("2026-07-15T02:00:00Z"));
        await errorRepository.AddAttemptAsync(question, "C", "practice", DateTimeOffset.Parse("2026-07-15T03:00:00Z"));
        var errors = await errorRepository.GetAllAsync();
        var item = Assert.Single(errors);
        Assert.Equal(2, item.AttemptCount);
        Assert.Equal("C", item.LastUserAnswer);
        await errorRepository.ClearAsync();
        Assert.Empty(await errorRepository.GetAllAsync());
    }

    public void Dispose()
    {
        SqliteConnection.ClearAllPools();
        if (Directory.Exists(_root))
        {
            Directory.Delete(_root, recursive: true);
        }
    }

    private SqliteDatabase CreateDatabase() => new(Path.Combine(_root, "skill-exam.db"));

    internal static Question CreateQuestion(string text)
    {
        var options = new Dictionary<string, string> { ["A"] = "正确", ["B"] = "错误" };
        return new Question
        {
            Id = QuestionIdentity.Create(QuestionType.SingleChoice, text, options, "A", "测试"),
            Type = QuestionType.SingleChoice,
            Text = text,
            Options = options,
            Answer = "A",
            Levels = new HashSet<SkillLevel> { SkillLevel.Level1 },
            Source = "测试",
            SourceSheet = "Sheet1",
            Categories = new HashSet<QuestionCategory>()
        };
    }

    private sealed class FailingMigration : ISchemaMigration
    {
        public int Version => 2;
        public string Description => "故意失败";

        public async Task ApplyAsync(SqliteConnection connection, SqliteTransaction transaction, CancellationToken cancellationToken)
        {
            var command = connection.CreateCommand();
            command.Transaction = transaction;
            command.CommandText = "CREATE TABLE should_rollback(id INTEGER PRIMARY KEY);";
            await command.ExecuteNonQueryAsync(cancellationToken);
            throw new InvalidOperationException("测试回滚");
        }
    }
}
