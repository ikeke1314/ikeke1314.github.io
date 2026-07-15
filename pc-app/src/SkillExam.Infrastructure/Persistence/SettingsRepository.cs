using System.Text.Json;
using Microsoft.Data.Sqlite;
using SkillExam.Core.Abstractions;
using SkillExam.Core.Models;

namespace SkillExam.Infrastructure.Persistence;

public sealed class SettingsRepository(SqliteDatabase database) : ISettingsRepository
{
    internal const string SettingsKey = "app_settings";
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public async Task<AppSettings> GetAsync(CancellationToken cancellationToken = default)
    {
        await using var connection = await database.OpenConnectionAsync(cancellationToken);
        var command = connection.CreateCommand();
        command.CommandText = "SELECT value FROM settings WHERE key = $key;";
        command.Parameters.AddWithValue("$key", SettingsKey);
        var value = await command.ExecuteScalarAsync(cancellationToken) as string;
        return value is null ? new AppSettings() : JsonSerializer.Deserialize<AppSettings>(value, JsonOptions) ?? new AppSettings();
    }

    public async Task SaveAsync(AppSettings settings, CancellationToken cancellationToken = default)
    {
        await using var connection = await database.OpenConnectionAsync(cancellationToken);
        await UpsertAsync(connection, null, SettingsKey, JsonSerializer.Serialize(settings, JsonOptions), DateTimeOffset.UtcNow, cancellationToken);
    }

    internal static async Task UpsertAsync(
        SqliteConnection connection,
        SqliteTransaction? transaction,
        string key,
        string value,
        DateTimeOffset updatedAt,
        CancellationToken cancellationToken)
    {
        var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = """
            INSERT INTO settings(key, value, updated_at) VALUES ($key, $value, $updatedAt)
            ON CONFLICT(key) DO UPDATE SET value = excluded.value, updated_at = excluded.updated_at;
            """;
        command.Parameters.AddWithValue("$key", key);
        command.Parameters.AddWithValue("$value", value);
        command.Parameters.AddWithValue("$updatedAt", updatedAt.ToString("O"));
        await command.ExecuteNonQueryAsync(cancellationToken);
    }
}
