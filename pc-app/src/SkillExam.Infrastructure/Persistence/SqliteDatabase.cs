using Microsoft.Data.Sqlite;

namespace SkillExam.Infrastructure.Persistence;

public interface ISchemaMigration
{
    int Version { get; }
    string Description { get; }
    Task ApplyAsync(SqliteConnection connection, SqliteTransaction transaction, CancellationToken cancellationToken);
}

public sealed class SqliteDatabase
{
    private readonly IReadOnlyList<ISchemaMigration> _migrations;

    public SqliteDatabase(string databasePath, IReadOnlyList<ISchemaMigration>? migrations = null)
    {
        DatabasePath = Path.GetFullPath(databasePath);
        Directory.CreateDirectory(Path.GetDirectoryName(DatabasePath)!);
        var builder = new SqliteConnectionStringBuilder
        {
            DataSource = DatabasePath,
            Mode = SqliteOpenMode.ReadWriteCreate,
            Cache = SqliteCacheMode.Shared,
            Pooling = true
        };
        ConnectionString = builder.ToString();
        _migrations = migrations ?? [new SchemaV1Migration()];
    }

    public string DatabasePath { get; }
    public string ConnectionString { get; }

    public async Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        await using var connection = await OpenConnectionAsync(cancellationToken);
        await using (var transaction = (SqliteTransaction)await connection.BeginTransactionAsync(cancellationToken))
        {
            var command = connection.CreateCommand();
            command.Transaction = transaction;
            command.CommandText = """
                CREATE TABLE IF NOT EXISTS schema_versions (
                    version INTEGER PRIMARY KEY,
                    description TEXT NOT NULL,
                    applied_at TEXT NOT NULL
                );
                """;
            await command.ExecuteNonQueryAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
        }

        foreach (var migration in _migrations.OrderBy(item => item.Version))
        {
            if (await IsAppliedAsync(connection, migration.Version, cancellationToken))
            {
                continue;
            }

            await using var transaction = (SqliteTransaction)await connection.BeginTransactionAsync(cancellationToken);
            try
            {
                await migration.ApplyAsync(connection, transaction, cancellationToken);
                var versionCommand = connection.CreateCommand();
                versionCommand.Transaction = transaction;
                versionCommand.CommandText = "INSERT INTO schema_versions(version, description, applied_at) VALUES ($version, $description, $appliedAt);";
                versionCommand.Parameters.AddWithValue("$version", migration.Version);
                versionCommand.Parameters.AddWithValue("$description", migration.Description);
                versionCommand.Parameters.AddWithValue("$appliedAt", DateTimeOffset.UtcNow.ToString("O"));
                await versionCommand.ExecuteNonQueryAsync(cancellationToken);
                await transaction.CommitAsync(cancellationToken);
            }
            catch
            {
                await transaction.RollbackAsync(CancellationToken.None);
                throw;
            }
        }
    }

    public async Task<SqliteConnection> OpenConnectionAsync(CancellationToken cancellationToken = default)
    {
        var connection = new SqliteConnection(ConnectionString);
        await connection.OpenAsync(cancellationToken);
        var command = connection.CreateCommand();
        command.CommandText = "PRAGMA foreign_keys = ON; PRAGMA busy_timeout = 5000;";
        await command.ExecuteNonQueryAsync(cancellationToken);
        return connection;
    }

    private static async Task<bool> IsAppliedAsync(SqliteConnection connection, int version, CancellationToken cancellationToken)
    {
        var command = connection.CreateCommand();
        command.CommandText = "SELECT EXISTS(SELECT 1 FROM schema_versions WHERE version = $version);";
        command.Parameters.AddWithValue("$version", version);
        return Convert.ToInt64(await command.ExecuteScalarAsync(cancellationToken)) == 1;
    }
}

public sealed class SchemaV1Migration : ISchemaMigration
{
    public int Version => 1;
    public string Description => "V3 初始数据结构";

    public async Task ApplyAsync(SqliteConnection connection, SqliteTransaction transaction, CancellationToken cancellationToken)
    {
        var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = """
            CREATE TABLE settings (
                key TEXT PRIMARY KEY,
                value TEXT NOT NULL,
                updated_at TEXT NOT NULL
            );

            CREATE TABLE error_questions (
                question_id TEXT PRIMARY KEY,
                question_type INTEGER NOT NULL,
                question_text TEXT NOT NULL,
                options_json TEXT NOT NULL,
                correct_answer TEXT NOT NULL,
                source TEXT NOT NULL,
                source_sheet TEXT NOT NULL,
                levels_json TEXT NOT NULL,
                categories_json TEXT NOT NULL,
                first_seen_at TEXT NOT NULL,
                last_seen_at TEXT NOT NULL
            );

            CREATE TABLE error_attempts (
                id INTEGER PRIMARY KEY AUTOINCREMENT,
                question_id TEXT NOT NULL REFERENCES error_questions(question_id) ON DELETE CASCADE,
                user_answer TEXT NOT NULL,
                attempted_at TEXT NOT NULL,
                mode TEXT NOT NULL
            );
            CREATE INDEX ix_error_attempts_question ON error_attempts(question_id, attempted_at DESC);

            CREATE TABLE practice_sessions (
                session_id TEXT PRIMARY KEY,
                levels_key TEXT NOT NULL,
                categories_key TEXT NOT NULL,
                question_type INTEGER NOT NULL,
                question_ids_json TEXT NOT NULL,
                current_question_id TEXT NULL,
                pending_retry_json TEXT NOT NULL,
                legacy_payload_json TEXT NULL,
                updated_at TEXT NOT NULL
            );
            CREATE INDEX ix_practice_lookup ON practice_sessions(levels_key, categories_key, question_type, updated_at DESC);

            CREATE TABLE practice_answers (
                session_id TEXT NOT NULL REFERENCES practice_sessions(session_id) ON DELETE CASCADE,
                question_id TEXT NOT NULL,
                answer TEXT NOT NULL,
                updated_at TEXT NOT NULL,
                PRIMARY KEY(session_id, question_id)
            );
            """;
        await command.ExecuteNonQueryAsync(cancellationToken);
    }
}
