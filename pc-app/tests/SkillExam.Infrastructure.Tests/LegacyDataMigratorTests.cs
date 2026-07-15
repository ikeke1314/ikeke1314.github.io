using System.Text.Json;
using Microsoft.Data.Sqlite;
using SkillExam.Core.Abstractions;
using SkillExam.Infrastructure.Migration;
using SkillExam.Infrastructure.Persistence;

namespace SkillExam.Infrastructure.Tests;

public sealed class LegacyDataMigratorTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), $"skill-exam-legacy-{Guid.NewGuid():N}");

    [Fact]
    public async Task GeneratedLegacyData_IsBackedUpMigratedAndIdempotentWithoutLosingDuplicateHistory()
    {
        var legacy = Path.Combine(_root, "legacy");
        Directory.CreateDirectory(legacy);
        await File.WriteAllTextAsync(Path.Combine(legacy, "config.json"), """{"last_bank_path":"D:/题库.xlsx"}""");
        await File.WriteAllTextAsync(Path.Combine(legacy, "error_questions.json"), """
            [
              {"question":"重复题","type":"单选题","options":{"A":"是","B":"否"},"correct_answer":"A","user_answer":"B","source_sheet":"测试","timestamp":"2026-07-15 10:00:00"},
              {"question":"重复题","type":"单选题","options":{"A":"是","B":"否"},"correct_answer":"A","user_answer":"C","source_sheet":"测试","timestamp":"2026-07-15 11:00:00"}
            ]
            """);
        await File.WriteAllTextAsync(Path.Combine(legacy, "practice_progress.json"), """{"一级,二级":{"单选题":{"index":3,"wrong_indices":[1]}}}""");
        var paths = new AppDataPaths(Path.Combine(_root, "data"));
        var database = new SqliteDatabase(paths.DatabasePath);
        await database.InitializeAsync();
        var migrator = new LegacyDataMigrator(database, paths, new FixedClock());

        var result = await migrator.MigrateAsync(legacy);
        Assert.Equal(2, result.ErrorAttemptsRead);
        Assert.Equal(2, result.ErrorAttemptsMigrated);
        Assert.Equal(1, result.PracticeSnapshotsMigrated);
        Assert.NotNull(result.BackupDirectory);
        Assert.Equal(3, Directory.GetFiles(result.BackupDirectory).Length);
        var errors = await new ErrorBookRepository(database).GetAllAsync();
        Assert.Equal(2, Assert.Single(errors).AttemptCount);
        Assert.Equal("D:/题库.xlsx", (await new SettingsRepository(database).GetAsync()).LastQuestionBankPath);

        var second = await migrator.MigrateAsync(legacy);
        Assert.True(second.WasAlreadyMigrated);
        Assert.Equal(2, Assert.Single(await new ErrorBookRepository(database).GetAllAsync()).AttemptCount);
    }

    [Fact]
    public async Task RealLegacyData_ReadOnlyMigrationPreservesEveryValidAttempt_WhenFilesAreAvailable()
    {
        var errorsPath = Path.Combine(TestPaths.PcAppRoot, "error_questions.json");
        if (!File.Exists(errorsPath))
        {
            return;
        }

        using var document = JsonDocument.Parse(await File.ReadAllTextAsync(errorsPath));
        var sourceCount = document.RootElement.GetArrayLength();
        var paths = new AppDataPaths(Path.Combine(_root, "real-data"));
        var database = new SqliteDatabase(paths.DatabasePath);
        await database.InitializeAsync();
        var result = await new LegacyDataMigrator(database, paths, new FixedClock()).MigrateAsync(TestPaths.PcAppRoot);

        Assert.True(sourceCount >= 189, $"真实旧错题基线低于 189：{sourceCount}");
        Assert.Equal(sourceCount, result.ErrorAttemptsRead);
        Assert.Equal(sourceCount, result.ErrorAttemptsMigrated);
        await using var connection = await database.OpenConnectionAsync();
        var command = connection.CreateCommand();
        command.CommandText = "SELECT COUNT(*) FROM error_attempts;";
        Assert.True(Convert.ToInt32(await command.ExecuteScalarAsync()) >= sourceCount);
    }

    public void Dispose()
    {
        SqliteConnection.ClearAllPools();
        if (Directory.Exists(_root))
        {
            Directory.Delete(_root, recursive: true);
        }
    }

    private sealed class FixedClock : IClock
    {
        public DateTimeOffset Now => DateTimeOffset.Parse("2026-07-15T12:00:00+08:00");
        public Task DelayAsync(TimeSpan delay, CancellationToken cancellationToken = default) => Task.CompletedTask;
    }
}
