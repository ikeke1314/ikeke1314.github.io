using SkillExam.Core.Models;

namespace SkillExam.Core.Exam;

public sealed record ExamRequest(
    SkillLevel TargetLevel,
    IReadOnlyCollection<QuestionCategory>? Categories = null,
    IReadOnlyCollection<string>? Sources = null,
    int? Seed = null);

public sealed record QuestionShortage(
    QuestionType QuestionType,
    SkillLevel Level,
    int Required,
    int Available)
{
    public int Missing => Math.Max(0, Required - Available);
}

public sealed record ExamPaper(
    SkillLevel TargetLevel,
    IReadOnlyList<Question> Questions,
    IReadOnlyDictionary<QuestionType, IReadOnlyDictionary<SkillLevel, int>> Distribution,
    int MaximumScore,
    TimeSpan Duration);

public sealed record ExamGenerationResult(ExamPaper? Paper, IReadOnlyList<QuestionShortage> Shortages)
{
    public bool Success => Paper is not null && Shortages.Count == 0;
}

public enum AnswerStatus
{
    Unanswered,
    Correct,
    Incorrect
}

public sealed record AnswerEvaluation(AnswerStatus Status, string NormalizedUserAnswer, string NormalizedCorrectAnswer)
{
    public bool IsCorrect => Status == AnswerStatus.Correct;
}

public sealed record ExamQuestionResult(Question Question, string UserAnswer, AnswerStatus Status, int AwardedPoints, int MaximumPoints);

public sealed record ExamResult(
    int Score,
    int MaximumScore,
    bool Passed,
    int CorrectCount,
    int IncorrectCount,
    int UnansweredCount,
    TimeSpan Elapsed,
    IReadOnlyList<ExamQuestionResult> Questions);

public static class ExamBlueprint
{
    public const int PassingScore = 80;
    public static readonly TimeSpan Duration = TimeSpan.FromMinutes(45);

    public static IReadOnlyDictionary<QuestionType, int> QuestionCounts { get; } = new Dictionary<QuestionType, int>
    {
        [QuestionType.SingleChoice] = 40,
        [QuestionType.MultipleChoice] = 10,
        [QuestionType.TrueFalse] = 10,
        [QuestionType.ShortAnswer] = 3
    };

    public static IReadOnlyDictionary<QuestionType, int> Points { get; } = new Dictionary<QuestionType, int>
    {
        [QuestionType.SingleChoice] = 1,
        [QuestionType.MultipleChoice] = 2,
        [QuestionType.TrueFalse] = 1,
        [QuestionType.ShortAnswer] = 10
    };

    public static int TotalQuestions => QuestionCounts.Values.Sum();
    public static int MaximumScore => QuestionCounts.Sum(pair => pair.Value * Points[pair.Key]);
}
