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
        if (value is null)
        {
            return new AppSettings();
        }

        var settings = JsonSerializer.Deserialize<AppSettings>(value, JsonOptions) ?? new AppSettings();
        using var document = JsonDocument.Parse(value);
        if (!document.RootElement.TryGetProperty("autoNextDelaySettingsVersion", out _))
        {
            // V3.1 短期版本的默认值是 1.5 秒；只迁移该默认值，保留用户主动选择的其他延迟。
            var migratedDelay = settings.AutoNextDelayMilliseconds == 1500
                ? AppSettings.DefaultAutoNextDelayMilliseconds
                : AppSettings.NormalizeAutoNextDelayMilliseconds(settings.AutoNextDelayMilliseconds);
            return settings with
            {
                AutoNextDelayMilliseconds = migratedDelay,
                AutoNextDelaySettingsVersion = AppSettings.CurrentAutoNextDelaySettingsVersion
            };
        }

        return settings with
        {
            AutoNextDelayMilliseconds = AppSettings.NormalizeAutoNextDelayMilliseconds(settings.AutoNextDelayMilliseconds),
            AutoNextDelaySettingsVersion = AppSettings.CurrentAutoNextDelaySettingsVersion
        };
    }

    public async Task SaveAsync(AppSettings settings, CancellationToken cancellationToken = default)
    {
        settings = settings with
        {
            AutoNextDelayMilliseconds = AppSettings.NormalizeAutoNextDelayMilliseconds(settings.AutoNextDelayMilliseconds),
            AutoNextDelaySettingsVersion = AppSettings.CurrentAutoNextDelaySettingsVersion
        };
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
