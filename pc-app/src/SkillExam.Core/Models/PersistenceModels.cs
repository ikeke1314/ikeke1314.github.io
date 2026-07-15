namespace SkillExam.Core.Models;

public sealed record ErrorAttempt(
    long Id,
    string QuestionId,
    string UserAnswer,
    DateTimeOffset AttemptedAt,
    string Mode);

public sealed record ErrorBookItem(
    Question Question,
    int AttemptCount,
    string LastUserAnswer,
    DateTimeOffset LastAttemptedAt);

public sealed record PracticeSessionSnapshot
{
    public required string SessionId { get; init; }
    public required IReadOnlyList<SkillLevel> Levels { get; init; }
    public required IReadOnlyList<QuestionCategory> Categories { get; init; }
    public required QuestionType QuestionType { get; init; }
    public required IReadOnlyList<string> QuestionIds { get; init; }
    public string? CurrentQuestionId { get; init; }
    public IReadOnlyDictionary<string, string> Answers { get; init; } = new Dictionary<string, string>();
    public IReadOnlySet<string> PendingRetryQuestionIds { get; init; } = new HashSet<string>();
    public DateTimeOffset UpdatedAt { get; init; }
}

public sealed record LegacyMigrationResult(
    bool WasAlreadyMigrated,
    int ErrorAttemptsRead,
    int ErrorAttemptsMigrated,
    int PracticeSnapshotsMigrated,
    string? BackupDirectory,
    IReadOnlyList<string> Warnings);
