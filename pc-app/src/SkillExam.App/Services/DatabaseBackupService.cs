using System.IO;
using Microsoft.Data.Sqlite;
using SkillExam.Infrastructure.Persistence;

namespace SkillExam.App.Services;

public sealed class DatabaseBackupService(SqliteDatabase database, AppDataPaths paths)
{
    public Task<string> BackupAsync(CancellationToken cancellationToken = default) => Task.Run(async () =>
    {
        paths.EnsureCreated();
        var target = Path.Combine(paths.BackupsDirectory, $"skill-exam-{DateTimeOffset.Now:yyyyMMdd_HHmmss}.db");
        await using var source = await database.OpenConnectionAsync(cancellationToken);
        await using var destination = new SqliteConnection(new SqliteConnectionStringBuilder { DataSource = target }.ToString());
        await destination.OpenAsync(cancellationToken);
        source.BackupDatabase(destination);
        return target;
    }, cancellationToken);
}
