using Serilog;
using SkillExam.App.Services;
using SkillExam.App.ViewModels;
using SkillExam.Core.Abstractions;
using SkillExam.Core.Exam;
using SkillExam.Core.Models;

namespace SkillExam.App.Tests;

public sealed class ViewModelInteractionTests
{
    [Fact]
    public void PracticeSetup_InheritsHomeLevelAndAllCategories()
    {
        var state = CreateBankState();
        var viewModel = new PracticeSetupViewModel(
            state,
            SkillLevel.Level2,
            defaultCategory: null,
            (_, _, _) => Task.CompletedTask,
            () => { });

        Assert.Equal([SkillLevel.Level2], viewModel.Levels.Where(item => item.IsSelected).Select(item => item.Value));
        Assert.All(viewModel.Categories, item => Assert.True(item.IsSelected));
        Assert.True(viewModel.SingleChoiceCount > 0);
    }

    [Fact]
    public async Task WrongSingleChoice_ExposesTextAndShapeStateBeyondColor()
    {
        var questions = new[] { CreateQuestion("q1", "A") };
        using var logger = new LoggerConfiguration().CreateLogger();
        var viewModel = new QuestionSessionViewModel(
            questions,
            SessionMode.Practice,
            new AnswerEvaluator(),
            new MemoryErrorBookRepository(),
            new MemoryProgressRepository(),
            new SilentSpeechService(),
            new ImmediateClock(),
            new SilentDialogService(),
            new AppSettings { AutoSpeechEnabled = false },
            logger,
            () => Task.CompletedTask,
            practiceLevels: [SkillLevel.Level1],
            practiceCategories: []);

        var wrongOption = viewModel.Options.Single(option => option.Key == "B");
        await viewModel.SelectOptionCommand.ExecuteAsync(wrongOption);

        var navigation = Assert.Single(viewModel.NavigatorItems);
        Assert.Equal(NavigationState.Current, navigation.State);
        Assert.Equal(NavigationState.Incorrect, navigation.AnswerState);
        Assert.Contains("当前题，回答错误", navigation.AccessibilityName, StringComparison.Ordinal);
        Assert.Contains("正确答案：A", viewModel.FeedbackMessage, StringComparison.Ordinal);
        Assert.True(viewModel.Options.Single(option => option.Key == "A").IsCorrect);
        Assert.True(wrongOption.IsIncorrectSelection);
        Assert.True(viewModel.CanAnswerCurrent);
    }

    [Fact]
    public async Task SubmitExam_RecordsUnansweredQuestionsAndOffersThemForReview()
    {
        var questions = new[] { CreateQuestion("unanswered", "A") };
        var repository = new MemoryErrorBookRepository();
        ExamResult? completedResult = null;
        IReadOnlyList<Question>? reviewQuestions = null;
        using var logger = new LoggerConfiguration().CreateLogger();
        var paper = new ExamPaper(
            SkillLevel.Level1,
            questions,
            new Dictionary<QuestionType, IReadOnlyDictionary<SkillLevel, int>>(),
            ExamBlueprint.MaximumScore,
            ExamBlueprint.Duration);
        var viewModel = new QuestionSessionViewModel(
            questions,
            SessionMode.Exam,
            new AnswerEvaluator(),
            repository,
            new MemoryProgressRepository(),
            new SilentSpeechService(),
            new ImmediateClock(),
            new SilentDialogService(confirm: true),
            new AppSettings { AutoSpeechEnabled = false },
            logger,
            () => Task.CompletedTask,
            (result, review) =>
            {
                completedResult = result;
                reviewQuestions = review;
                return Task.CompletedTask;
            },
            paper);

        await viewModel.SubmitExamCommand.ExecuteAsync(null);

        Assert.NotNull(completedResult);
        Assert.Equal(1, completedResult.UnansweredCount);
        Assert.Single(repository.Attempts);
        Assert.Empty(repository.Attempts[0].UserAnswer);
        Assert.Equal(questions, reviewQuestions);
    }

    private static QuestionBankState CreateBankState()
    {
        var state = new QuestionBankState();
        var questions = new[]
        {
            CreateQuestion("l2-common", "A", new HashSet<SkillLevel> { SkillLevel.Level2 }),
            CreateQuestion(
                "l2-base",
                "A",
                new HashSet<SkillLevel> { SkillLevel.Level2 },
                new HashSet<QuestionCategory> { QuestionCategory.BaseStation })
        };
        state.SetLoaded("bank.xlsx", new QuestionBankLoadResult(questions, [], ["Sheet1"], 0));
        return state;
    }

    private static Question CreateQuestion(
        string id,
        string answer,
        IReadOnlySet<SkillLevel>? levels = null,
        IReadOnlySet<QuestionCategory>? categories = null) => new()
    {
        Id = id,
        Type = QuestionType.SingleChoice,
        Text = "测试题目",
        Options = new Dictionary<string, string> { ["A"] = "正确选项", ["B"] = "错误选项" },
        Answer = answer,
        Levels = levels ?? new HashSet<SkillLevel> { SkillLevel.Level1 },
        Source = "通用",
        Categories = categories ?? new HashSet<QuestionCategory>()
    };

    private sealed class MemoryErrorBookRepository : IErrorBookRepository
    {
        public List<(Question Question, string UserAnswer)> Attempts { get; } = [];
        public Task AddAttemptAsync(Question question, string userAnswer, string mode, DateTimeOffset attemptedAt, CancellationToken cancellationToken = default)
        {
            Attempts.Add((question, userAnswer));
            return Task.CompletedTask;
        }
        public Task<IReadOnlyList<ErrorBookItem>> GetAllAsync(CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyList<ErrorBookItem>>([]);
        public Task ClearAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
    }

    private sealed class MemoryProgressRepository : IProgressRepository
    {
        public Task SaveAsync(PracticeSessionSnapshot snapshot, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task<PracticeSessionSnapshot?> GetLatestAsync(IReadOnlyCollection<SkillLevel> levels, IReadOnlyCollection<QuestionCategory> categories, QuestionType questionType, CancellationToken cancellationToken = default) => Task.FromResult<PracticeSessionSnapshot?>(null);
    }

    private sealed class SilentSpeechService : ISpeechService
    {
        public IReadOnlyList<SpeechVoice> GetVoices() => [];
        public bool IsChineseVoiceAvailable => false;
        public Task SpeakAsync(string text, string? voiceName, int rate, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public void Cancel() { }
        public void Dispose() { }
    }

    private sealed class ImmediateClock : IClock
    {
        public DateTimeOffset Now => DateTimeOffset.Parse("2026-07-15T12:00:00+08:00");
        public Task DelayAsync(TimeSpan delay, CancellationToken cancellationToken = default) => Task.CompletedTask;
    }

    private sealed class SilentDialogService(bool confirm = false) : IDialogService
    {
        public Task<string?> PickQuestionBankAsync() => Task.FromResult<string?>(null);
        public Task<string?> PickFolderAsync(string title) => Task.FromResult<string?>(null);
        public Task<IReadOnlyList<string>?> SelectSheetsAsync(IReadOnlyList<SheetInfo> sheets, IReadOnlyCollection<string>? previousSelection = null) => Task.FromResult<IReadOnlyList<string>?>(null);
        public Task<bool> ConfirmAsync(string title, string message, string confirmText = "确认") => Task.FromResult(confirm);
        public Task ShowMessageAsync(string title, string message) => Task.CompletedTask;
        public Task ShowErrorSummaryAsync(string title, string summary, IReadOnlyList<QuestionBankIssue> issues) => Task.CompletedTask;
    }
}
