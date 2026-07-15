using System.Text.Json;
using Microsoft.Data.Sqlite;
using SkillExam.Core.Abstractions;
using SkillExam.Core.Models;

namespace SkillExam.Infrastructure.Persistence;

public sealed class ProgressRepository(SqliteDatabase database) : IProgressRepository
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public async Task SaveAsync(PracticeSessionSnapshot snapshot, CancellationToken cancellationToken = default)
    {
        await using var connection = await database.OpenConnectionAsync(cancellationToken);
        await using var transaction = (SqliteTransaction)await connection.BeginTransactionAsync(cancellationToken);
        try
        {
            var command = connection.CreateCommand();
            command.Transaction = transaction;
            command.CommandText = """
                INSERT INTO practice_sessions(
                    session_id, levels_key, categories_key, question_type, question_ids_json,
                    current_question_id, pending_retry_json, legacy_payload_json, updated_at)
                VALUES ($id, $levels, $categories, $type, $questionIds, $currentId, $pending, NULL, $updatedAt)
                ON CONFLICT(session_id) DO UPDATE SET
                    levels_key = excluded.levels_key,
                    categories_key = excluded.categories_key,
                    question_type = excluded.question_type,
                    question_ids_json = excluded.question_ids_json,
                    current_question_id = excluded.current_question_id,
                    pending_retry_json = excluded.pending_retry_json,
                    updated_at = excluded.updated_at;
                """;
            AddSessionParameters(command, snapshot);
            await command.ExecuteNonQueryAsync(cancellationToken);

            var deleteAnswers = connection.CreateCommand();
            deleteAnswers.Transaction = transaction;
            deleteAnswers.CommandText = "DELETE FROM practice_answers WHERE session_id = $id;";
            deleteAnswers.Parameters.AddWithValue("$id", snapshot.SessionId);
            await deleteAnswers.ExecuteNonQueryAsync(cancellationToken);

            foreach (var answer in snapshot.Answers)
            {
                var answerCommand = connection.CreateCommand();
                answerCommand.Transaction = transaction;
                answerCommand.CommandText = """
                    INSERT INTO practice_answers(session_id, question_id, answer, updated_at)
                    VALUES ($sessionId, $questionId, $answer, $updatedAt);
                    """;
                answerCommand.Parameters.AddWithValue("$sessionId", snapshot.SessionId);
                answerCommand.Parameters.AddWithValue("$questionId", answer.Key);
                answerCommand.Parameters.AddWithValue("$answer", answer.Value);
                answerCommand.Parameters.AddWithValue("$updatedAt", snapshot.UpdatedAt.ToString("O"));
                await answerCommand.ExecuteNonQueryAsync(cancellationToken);
            }
            await transaction.CommitAsync(cancellationToken);
        }
        catch
        {
            await transaction.RollbackAsync(CancellationToken.None);
            throw;
        }
    }

    public async Task<PracticeSessionSnapshot?> GetLatestAsync(
        IReadOnlyCollection<SkillLevel> levels,
        IReadOnlyCollection<QuestionCategory> categories,
        QuestionType questionType,
        CancellationToken cancellationToken = default)
    {
        await using var connection = await database.OpenConnectionAsync(cancellationToken);
        var command = connection.CreateCommand();
        command.CommandText = """
            SELECT session_id, question_ids_json, current_question_id, pending_retry_json, updated_at
            FROM practice_sessions
            WHERE levels_key = $levels AND categories_key = $categories AND question_type = $type
                  AND legacy_payload_json IS NULL
            ORDER BY updated_at DESC LIMIT 1;
            """;
        command.Parameters.AddWithValue("$levels", GetLevelsKey(levels));
        command.Parameters.AddWithValue("$categories", GetCategoriesKey(categories));
        command.Parameters.AddWithValue("$type", (int)questionType);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        if (!await reader.ReadAsync(cancellationToken))
        {
            return null;
        }

        var sessionId = reader.GetString(0);
        var questionIdsJson = reader.GetString(1);
        var currentQuestionId = reader.IsDBNull(2) ? null : reader.GetString(2);
        var pendingRetryJson = reader.GetString(3);
        var updatedAt = DateTimeOffset.Parse(reader.GetString(4));
        var answers = new Dictionary<string, string>();
        await reader.DisposeAsync();
        var answerCommand = connection.CreateCommand();
        answerCommand.CommandText = "SELECT question_id, answer FROM practice_answers WHERE session_id = $id;";
        answerCommand.Parameters.AddWithValue("$id", sessionId);
        await using var answerReader = await answerCommand.ExecuteReaderAsync(cancellationToken);
        while (await answerReader.ReadAsync(cancellationToken))
        {
            answers[answerReader.GetString(0)] = answerReader.GetString(1);
        }

        return new PracticeSessionSnapshot
        {
            SessionId = sessionId,
            Levels = levels.Order().ToArray(),
            Categories = categories.Order().ToArray(),
            QuestionType = questionType,
            QuestionIds = JsonSerializer.Deserialize<string[]>(questionIdsJson, JsonOptions) ?? [],
            CurrentQuestionId = currentQuestionId,
            PendingRetryQuestionIds = JsonSerializer.Deserialize<HashSet<string>>(pendingRetryJson, JsonOptions) ?? [],
            Answers = answers,
            UpdatedAt = updatedAt
        };
    }

    private static void AddSessionParameters(SqliteCommand command, PracticeSessionSnapshot snapshot)
    {
        command.Parameters.AddWithValue("$id", snapshot.SessionId);
        command.Parameters.AddWithValue("$levels", GetLevelsKey(snapshot.Levels));
        command.Parameters.AddWithValue("$categories", GetCategoriesKey(snapshot.Categories));
        command.Parameters.AddWithValue("$type", (int)snapshot.QuestionType);
        command.Parameters.AddWithValue("$questionIds", JsonSerializer.Serialize(snapshot.QuestionIds, JsonOptions));
        command.Parameters.AddWithValue("$currentId", (object?)snapshot.CurrentQuestionId ?? DBNull.Value);
        command.Parameters.AddWithValue("$pending", JsonSerializer.Serialize(snapshot.PendingRetryQuestionIds, JsonOptions));
        command.Parameters.AddWithValue("$updatedAt", snapshot.UpdatedAt.ToString("O"));
    }

    internal static string GetLevelsKey(IEnumerable<SkillLevel> levels) => string.Join(",", levels.Distinct().Order().Select(level => (int)level));
    internal static string GetCategoriesKey(IEnumerable<QuestionCategory> categories) => string.Join(",", categories.Distinct().Order().Select(category => (int)category));
}
