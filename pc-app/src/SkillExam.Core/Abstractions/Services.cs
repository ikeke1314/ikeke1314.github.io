using SkillExam.Core.Exam;
using SkillExam.Core.Models;

namespace SkillExam.Core.Abstractions;

public interface IQuestionBankReader
{
    Task<IReadOnlyList<SheetInfo>> GetSheetsAsync(string filePath, CancellationToken cancellationToken = default);
    Task<QuestionBankLoadResult> ReadAsync(
        string filePath,
        IReadOnlyCollection<string>? selectedSheets = null,
        CancellationToken cancellationToken = default);
}

public interface IExamGenerator
{
    IReadOnlyDictionary<SkillLevel, decimal> GetWeights(SkillLevel targetLevel);
    IReadOnlyDictionary<QuestionType, IReadOnlyDictionary<SkillLevel, int>> GetDistribution(SkillLevel targetLevel);
    ExamGenerationResult Generate(IReadOnlyList<Question> questionBank, ExamRequest request);
}

public interface IAnswerEvaluator
{
    AnswerEvaluation Evaluate(Question question, string? userAnswer);
}

public interface IProgressRepository
{
    Task SaveAsync(PracticeSessionSnapshot snapshot, CancellationToken cancellationToken = default);
    Task<PracticeSessionSnapshot?> GetLatestAsync(
        IReadOnlyCollection<SkillLevel> levels,
        IReadOnlyCollection<QuestionCategory> categories,
        QuestionType questionType,
        CancellationToken cancellationToken = default);
}

public interface IErrorBookRepository
{
    Task AddAttemptAsync(Question question, string userAnswer, string mode, DateTimeOffset attemptedAt, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<ErrorBookItem>> GetAllAsync(CancellationToken cancellationToken = default);
    Task ClearAsync(CancellationToken cancellationToken = default);
}

public interface ISettingsRepository
{
    Task<AppSettings> GetAsync(CancellationToken cancellationToken = default);
    Task SaveAsync(AppSettings settings, CancellationToken cancellationToken = default);
}

public interface ISpeechService : IDisposable
{
    IReadOnlyList<SpeechVoice> GetVoices();
    bool IsChineseVoiceAvailable { get; }
    Task SpeakAsync(string text, string? voiceName, int rate, CancellationToken cancellationToken = default);
    void Cancel();
}

public interface IClock
{
    DateTimeOffset Now { get; }
    Task DelayAsync(TimeSpan delay, CancellationToken cancellationToken = default);
}
