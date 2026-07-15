using System.Text.Json;
using Microsoft.Data.Sqlite;
using SkillExam.Core.Abstractions;
using SkillExam.Core.Models;

namespace SkillExam.Infrastructure.Persistence;

public sealed class ErrorBookRepository(SqliteDatabase database) : IErrorBookRepository
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public async Task AddAttemptAsync(
        Question question,
        string userAnswer,
        string mode,
        DateTimeOffset attemptedAt,
        CancellationToken cancellationToken = default)
    {
        await using var connection = await database.OpenConnectionAsync(cancellationToken);
        await using var transaction = (SqliteTransaction)await connection.BeginTransactionAsync(cancellationToken);
        try
        {
            await UpsertQuestionAsync(connection, transaction, question, attemptedAt, cancellationToken);
            await InsertAttemptAsync(connection, transaction, question.Id, userAnswer, mode, attemptedAt, cancellationToken);
            await transaction.CommitAsync(cancellationToken);
        }
        catch
        {
            await transaction.RollbackAsync(CancellationToken.None);
            throw;
        }
    }

    public async Task<IReadOnlyList<ErrorBookItem>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        await using var connection = await database.OpenConnectionAsync(cancellationToken);
        var command = connection.CreateCommand();
        command.CommandText = """
            SELECT q.question_id, q.question_type, q.question_text, q.options_json, q.correct_answer,
                   q.source, q.source_sheet, q.levels_json, q.categories_json,
                   COUNT(a.id),
                   (SELECT user_answer FROM error_attempts x WHERE x.question_id = q.question_id ORDER BY x.attempted_at DESC, x.id DESC LIMIT 1),
                   MAX(a.attempted_at)
            FROM error_questions q
            JOIN error_attempts a ON a.question_id = q.question_id
            GROUP BY q.question_id
            ORDER BY MAX(a.attempted_at) DESC;
            """;
        var result = new List<ErrorBookItem>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            var question = new Question
            {
                Id = reader.GetString(0),
                Type = (QuestionType)reader.GetInt32(1),
                Text = reader.GetString(2),
                Options = JsonSerializer.Deserialize<Dictionary<string, string>>(reader.GetString(3), JsonOptions) ?? [],
                Answer = reader.GetString(4),
                Source = reader.GetString(5),
                SourceSheet = reader.GetString(6),
                Levels = JsonSerializer.Deserialize<HashSet<SkillLevel>>(reader.GetString(7), JsonOptions) ?? [],
                Categories = JsonSerializer.Deserialize<HashSet<QuestionCategory>>(reader.GetString(8), JsonOptions) ?? []
            };
            result.Add(new ErrorBookItem(
                question,
                reader.GetInt32(9),
                reader.IsDBNull(10) ? string.Empty : reader.GetString(10),
                DateTimeOffset.Parse(reader.GetString(11))));
        }
        return result;
    }

    public async Task ClearAsync(CancellationToken cancellationToken = default)
    {
        await using var connection = await database.OpenConnectionAsync(cancellationToken);
        var command = connection.CreateCommand();
        command.CommandText = "DELETE FROM error_questions;";
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    internal static async Task UpsertQuestionAsync(
        SqliteConnection connection,
        SqliteTransaction transaction,
        Question question,
        DateTimeOffset seenAt,
        CancellationToken cancellationToken)
    {
        var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = """
            INSERT INTO error_questions(
                question_id, question_type, question_text, options_json, correct_answer, source, source_sheet,
                levels_json, categories_json, first_seen_at, last_seen_at)
            VALUES ($id, $type, $text, $options, $answer, $source, $sheet, $levels, $categories, $seenAt, $seenAt)
            ON CONFLICT(question_id) DO UPDATE SET
                question_type = excluded.question_type,
                question_text = excluded.question_text,
                options_json = excluded.options_json,
                correct_answer = excluded.correct_answer,
                source = excluded.source,
                source_sheet = excluded.source_sheet,
                levels_json = excluded.levels_json,
                categories_json = excluded.categories_json,
                last_seen_at = excluded.last_seen_at;
            """;
        command.Parameters.AddWithValue("$id", question.Id);
        command.Parameters.AddWithValue("$type", (int)question.Type);
        command.Parameters.AddWithValue("$text", question.Text);
        command.Parameters.AddWithValue("$options", JsonSerializer.Serialize(question.Options, JsonOptions));
        command.Parameters.AddWithValue("$answer", question.Answer);
        command.Parameters.AddWithValue("$source", question.Source);
        command.Parameters.AddWithValue("$sheet", question.SourceSheet);
        command.Parameters.AddWithValue("$levels", JsonSerializer.Serialize(question.Levels, JsonOptions));
        command.Parameters.AddWithValue("$categories", JsonSerializer.Serialize(question.Categories, JsonOptions));
        command.Parameters.AddWithValue("$seenAt", seenAt.ToString("O"));
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    internal static async Task InsertAttemptAsync(
        SqliteConnection connection,
        SqliteTransaction transaction,
        string questionId,
        string userAnswer,
        string mode,
        DateTimeOffset attemptedAt,
        CancellationToken cancellationToken)
    {
        var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = """
            INSERT INTO error_attempts(question_id, user_answer, attempted_at, mode)
            VALUES ($questionId, $userAnswer, $attemptedAt, $mode);
            """;
        command.Parameters.AddWithValue("$questionId", questionId);
        command.Parameters.AddWithValue("$userAnswer", userAnswer);
        command.Parameters.AddWithValue("$attemptedAt", attemptedAt.ToString("O"));
        command.Parameters.AddWithValue("$mode", mode);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }
}
