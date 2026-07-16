using System.IO;
using Serilog;
using SkillExam.App.Services;
using SkillExam.App.ViewModels;
using SkillExam.Core.Abstractions;
using SkillExam.Core.Exam;
using SkillExam.Core.Models;
using SkillExam.Infrastructure.Persistence;

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
    public async Task MultipleChoice_ConfirmsAllSelectedOptionsTogether()
    {
        var questions = new[] { CreateQuestion("multiple", "AB", type: QuestionType.MultipleChoice) };
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

        viewModel.Options.Single(option => option.Key == "A").IsSelected = true;
        viewModel.Options.Single(option => option.Key == "B").IsSelected = true;
        await viewModel.ConfirmAnswerCommand.ExecuteAsync(null);

        Assert.Equal(AnswerStatus.Correct, viewModel.FeedbackStatus);
        Assert.Equal("回答正确", viewModel.FeedbackMessage);
        Assert.All(viewModel.Options, option => Assert.True(option.IsSelected));
        Assert.False(viewModel.CanAnswerCurrent);
    }

    [Fact]
    public async Task TrueFalse_SelectsAndEvaluatesImmediately()
    {
        var questions = new[] { CreateQuestion("true-false", "A", type: QuestionType.TrueFalse) };
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

        await viewModel.SelectOptionCommand.ExecuteAsync(viewModel.Options.Single(option => option.Key == "A"));

        Assert.Equal(AnswerStatus.Correct, viewModel.FeedbackStatus);
        Assert.True(viewModel.Options.Single(option => option.Key == "A").IsCorrect);
        Assert.False(viewModel.CanAnswerCurrent);
    }

    [Fact]
    public async Task ShortAnswer_RevealsReferenceAndEvaluatesEnteredText()
    {
        var questions = new[] { CreateQuestion("short-answer", "标准答案", type: QuestionType.ShortAnswer) };
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

        viewModel.AnswerText = "标准答案";
        await viewModel.RevealAnswerCommand.ExecuteAsync(null);

        Assert.True(viewModel.IsReferenceVisible);
        Assert.Equal("标准答案", viewModel.ReferenceAnswer);
        Assert.Equal(AnswerStatus.Correct, viewModel.FeedbackStatus);
        Assert.False(viewModel.CanAnswerCurrent);
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

    [Theory]
    [InlineData(0)]
    [InlineData(2500)]
    public async Task CorrectAnswer_UsesConfiguredAutoNextDelay(int configuredDelayMilliseconds)
    {
        var questions = new[] { CreateQuestion("q1", "A"), CreateQuestion("q2", "A") };
        var clock = new ControlledClock();
        using var logger = new LoggerConfiguration().CreateLogger();
        var viewModel = new QuestionSessionViewModel(
            questions,
            SessionMode.Practice,
            new AnswerEvaluator(),
            new MemoryErrorBookRepository(),
            new MemoryProgressRepository(),
            new SilentSpeechService(),
            clock,
            new SilentDialogService(),
            new AppSettings { AutoSpeechEnabled = false, AutoNextDelayMilliseconds = configuredDelayMilliseconds },
            logger,
            () => Task.CompletedTask,
            practiceLevels: [SkillLevel.Level1],
            practiceCategories: []);

        var correctOption = viewModel.Options.Single(option => option.Key == "A");
        await viewModel.SelectOptionCommand.ExecuteAsync(correctOption);
        var requestedDelay = await clock.DelayRequested.Task.WaitAsync(TimeSpan.FromSeconds(1));

        Assert.Equal(TimeSpan.FromMilliseconds(configuredDelayMilliseconds), requestedDelay);
        Assert.Equal(0, viewModel.CurrentIndex);

        clock.ReleaseDelay();
        await WaitUntilAsync(() => viewModel.CurrentIndex == 1);
    }

    [Fact]
    public async Task SettingsSave_PersistsAutoNextDelay()
    {
        var repository = new MemorySettingsRepository();
        var paths = new AppDataPaths(Path.Combine(Path.GetTempPath(), $"skill-exam-settings-{Guid.NewGuid():N}"));
        var database = new SqliteDatabase(paths.DatabasePath);
        AppSettings? savedSettings = null;
        var navigatedBack = false;
        using var logger = new LoggerConfiguration().CreateLogger();
        var viewModel = new SettingsViewModel(
            new AppSettings(),
            repository,
            new SilentSpeechService(),
            new DatabaseBackupService(database, paths),
            paths,
            new SilentDialogService(),
            logger,
            settings =>
            {
                savedSettings = settings;
                return Task.CompletedTask;
            },
            () => navigatedBack = true);

        Assert.Equal(1.0, viewModel.AutoNextDelaySeconds);

        viewModel.AutoNextDelaySeconds = 2.4;
        await viewModel.SaveCommand.ExecuteAsync(null);

        Assert.Equal(2400, repository.SavedSettings?.AutoNextDelayMilliseconds);
        Assert.Equal(repository.SavedSettings, savedSettings);
        Assert.True(navigatedBack);
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
        IReadOnlySet<QuestionCategory>? categories = null,
        QuestionType type = QuestionType.SingleChoice) => new()
    {
        Id = id,
        Type = type,
        Text = "测试题目",
        Options = type == QuestionType.ShortAnswer
            ? new Dictionary<string, string>()
            : new Dictionary<string, string> { ["A"] = "选项 A", ["B"] = "选项 B" },
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

    private sealed class MemorySettingsRepository : ISettingsRepository
    {
        public AppSettings? SavedSettings { get; private set; }

        public Task<AppSettings> GetAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult(SavedSettings ?? new AppSettings());

        public Task SaveAsync(AppSettings settings, CancellationToken cancellationToken = default)
        {
            SavedSettings = settings;
            return Task.CompletedTask;
        }
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

    private sealed class ControlledClock : IClock
    {
        private readonly TaskCompletionSource _delayCompletion = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public DateTimeOffset Now => DateTimeOffset.Parse("2026-07-15T12:00:00+08:00");
        public TaskCompletionSource<TimeSpan> DelayRequested { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public async Task DelayAsync(TimeSpan delay, CancellationToken cancellationToken = default)
        {
            DelayRequested.TrySetResult(delay);
            await _delayCompletion.Task.WaitAsync(cancellationToken);
        }

        public void ReleaseDelay() => _delayCompletion.TrySetResult();
    }

    private static async Task WaitUntilAsync(Func<bool> condition)
    {
        using var cancellation = new CancellationTokenSource(TimeSpan.FromSeconds(1));
        while (!condition())
        {
            await Task.Delay(10, cancellation.Token);
        }
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
