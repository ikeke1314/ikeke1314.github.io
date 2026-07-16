using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Serilog;
using SkillExam.App.Services;
using SkillExam.Core.Abstractions;
using SkillExam.Core.Exam;
using SkillExam.Core.Models;
using SkillExam.Core.Practice;

namespace SkillExam.App.ViewModels;

public enum SessionMode
{
    Exam,
    Practice,
    ErrorReview
}

public enum NavigationState
{
    Unanswered,
    Current,
    Correct,
    Incorrect
}

public sealed partial class QuestionNavigationItem(int index, Question question) : ObservableObject
{
    public int Index { get; } = index;
    public int Number => Index + 1;
    public string TypeDisplay => Question.Type.ToDisplayName();
    public Question Question { get; } = question;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(State))]
    [NotifyPropertyChangedFor(nameof(StatusDisplay))]
    [NotifyPropertyChangedFor(nameof(AccessibilityName))]
    private NavigationState _answerState;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(State))]
    [NotifyPropertyChangedFor(nameof(StatusDisplay))]
    [NotifyPropertyChangedFor(nameof(AccessibilityName))]
    private bool _isCurrent;

    public NavigationState State => IsCurrent ? NavigationState.Current : AnswerState;
    public string StatusDisplay => AnswerState switch
    {
        NavigationState.Correct => IsCurrent ? "当前题，回答正确" : "回答正确",
        NavigationState.Incorrect => IsCurrent ? "当前题，回答错误" : "回答错误",
        _ => IsCurrent ? "当前题，未作答" : "未作答"
    };
    public string AccessibilityName => $"第 {Number} 题，{TypeDisplay}，{StatusDisplay}";
}

public sealed record QuestionNavigationGroup(
    string TypeDisplay,
    IReadOnlyList<QuestionNavigationItem> Items);

public sealed partial class QuestionOptionViewModel(string key, string text) : ObservableObject
{
    public string Key { get; } = key;
    public string Text { get; } = text;
    [ObservableProperty] private bool _isSelected;
    [ObservableProperty] private bool _isCorrect;
    [ObservableProperty] private bool _isIncorrectSelection;
}

public sealed partial class QuestionSessionViewModel : ObservableObject
{
    private readonly IAnswerEvaluator _answerEvaluator;
    private readonly IErrorBookRepository _errorBookRepository;
    private readonly IProgressRepository _progressRepository;
    private readonly ISpeechService _speechService;
    private readonly IClock _clock;
    private readonly IDialogService _dialogs;
    private readonly AppSettings _settings;
    private readonly ILogger _logger;
    private readonly Func<ExamResult, IReadOnlyList<Question>, Task>? _examFinished;
    private readonly Func<Task> _exit;
    private readonly ExamPaper? _paper;
    private readonly PracticeSession? _practiceSession;
    private readonly IReadOnlyList<SkillLevel> _practiceLevels;
    private readonly IReadOnlyList<QuestionCategory> _practiceCategories;
    private readonly Dictionary<string, string> _answers = [];
    private readonly Dictionary<string, AnswerStatus> _answerStatuses = [];
    private readonly HashSet<string> _lockedQuestionIds = [];
    private CancellationTokenSource? _timerCancellation;
    private CancellationTokenSource? _autoNextCancellation;
    private DateTimeOffset _startedAt;
    private bool _finished;

    public QuestionSessionViewModel(
        IReadOnlyList<Question> questions,
        SessionMode mode,
        IAnswerEvaluator answerEvaluator,
        IErrorBookRepository errorBookRepository,
        IProgressRepository progressRepository,
        ISpeechService speechService,
        IClock clock,
        IDialogService dialogs,
        AppSettings settings,
        ILogger logger,
        Func<Task> exit,
        Func<ExamResult, IReadOnlyList<Question>, Task>? examFinished = null,
        ExamPaper? paper = null,
        PracticeSessionSnapshot? practiceSnapshot = null,
        IReadOnlyList<SkillLevel>? practiceLevels = null,
        IReadOnlyList<QuestionCategory>? practiceCategories = null)
    {
        if (questions.Count == 0)
        {
            throw new ArgumentException("答题会话不能为空。", nameof(questions));
        }
        if (mode == SessionMode.Exam && paper is null)
        {
            throw new ArgumentException("考试模式必须提供试卷。", nameof(paper));
        }

        Questions = questions;
        Mode = mode;
        _answerEvaluator = answerEvaluator;
        _errorBookRepository = errorBookRepository;
        _progressRepository = progressRepository;
        _speechService = speechService;
        _clock = clock;
        _dialogs = dialogs;
        _settings = settings;
        _logger = logger;
        _exit = exit;
        _examFinished = examFinished;
        _paper = paper;
        _practiceLevels = practiceLevels ?? [];
        _practiceCategories = practiceCategories ?? [];
        NavigatorItems = new ObservableCollection<QuestionNavigationItem>(
            questions.Select((question, index) => new QuestionNavigationItem(index, question)));
        NavigatorGroups = Enum.GetValues<QuestionType>()
            .Select(type => new QuestionNavigationGroup(
                type.ToDisplayName(),
                NavigatorItems.Where(item => item.Question.Type == type).ToArray()))
            .Where(group => group.Items.Count > 0)
            .ToArray();
        if (mode == SessionMode.Practice)
        {
            _practiceSession = new PracticeSession(questions, answerEvaluator, practiceSnapshot);
            CurrentIndex = _practiceSession.CurrentIndex;
            foreach (var answer in _practiceSession.Answers)
            {
                _answers[answer.Key] = answer.Value;
                var question = questions.FirstOrDefault(item => item.Id == answer.Key);
                if (question is not null)
                {
                    _answerStatuses[answer.Key] = _practiceSession.PendingRetryQuestionIds.Contains(answer.Key)
                        ? AnswerStatus.Incorrect
                        : answerEvaluator.Evaluate(question, answer.Value).Status;
                    if (_answerStatuses[answer.Key] == AnswerStatus.Correct)
                    {
                        _lockedQuestionIds.Add(answer.Key);
                    }
                }
            }
        }
        RemainingTime = paper?.Duration ?? TimeSpan.Zero;
        LoadCurrentQuestion();
    }

    public IReadOnlyList<Question> Questions { get; }
    public SessionMode Mode { get; }
    public ObservableCollection<QuestionNavigationItem> NavigatorItems { get; }
    public IReadOnlyList<QuestionNavigationGroup> NavigatorGroups { get; }
    public ObservableCollection<QuestionOptionViewModel> Options { get; } = [];
    public bool IsExam => Mode == SessionMode.Exam;
    public bool IsPractice => Mode == SessionMode.Practice;
    public bool IsErrorReview => Mode == SessionMode.ErrorReview;
    public bool IsShortAnswer => CurrentQuestion.Type == QuestionType.ShortAnswer;
    public bool IsMultipleChoice => CurrentQuestion.Type == QuestionType.MultipleChoice;
    public bool ShowsConfirmAnswer => IsMultipleChoice;
    public bool ShowsReferenceAnswer => IsShortAnswer;
    public bool CanAnswerCurrent => !_lockedQuestionIds.Contains(CurrentQuestion.Id);
    public bool CanMovePrevious => CurrentIndex > 0;
    public bool CanMoveNext => CurrentIndex < Questions.Count - 1;
    public string ProgressText => $"{CurrentIndex + 1} / {Questions.Count}";
    public string QuestionNumberText => $"{CurrentIndex + 1}. {CurrentQuestion.Text}";
    public string TypeDisplay => CurrentQuestion.Type.ToDisplayName();
    public string SourceDisplay => string.IsNullOrWhiteSpace(CurrentQuestion.Source) ? "来源未标注" : CurrentQuestion.Source;
    public string LevelDisplay => CurrentQuestion.Levels.Count == 0
        ? "等级未标注"
        : string.Join("、", CurrentQuestion.Levels.Order().Select(level => level.ToDisplayName()));
    public string ModeDisplay => Mode switch
    {
        SessionMode.Exam => "模拟考试",
        SessionMode.Practice => "专项练习",
        SessionMode.ErrorReview => "错题复习",
        _ => string.Empty
    };
    public string ExitButtonText => Mode == SessionMode.ErrorReview ? "退出错题本" : "退出练习";

    [ObservableProperty] private int _currentIndex;
    [ObservableProperty] private Question _currentQuestion = null!;
    [ObservableProperty] private string _answerText = string.Empty;
    [ObservableProperty] private string _feedbackMessage = string.Empty;
    [ObservableProperty] private AnswerStatus _feedbackStatus = AnswerStatus.Unanswered;
    [ObservableProperty] private string _referenceAnswer = string.Empty;
    [ObservableProperty] private bool _isReferenceVisible;
    [ObservableProperty] private TimeSpan _remainingTime;
    [ObservableProperty] private bool _isBusy;

    public string RemainingTimeText => $"{(int)RemainingTime.TotalMinutes:00}:{RemainingTime.Seconds:00}";
    public bool HasFeedback => !string.IsNullOrWhiteSpace(FeedbackMessage);

    partial void OnRemainingTimeChanged(TimeSpan value) => OnPropertyChanged(nameof(RemainingTimeText));
    partial void OnFeedbackMessageChanged(string value) => OnPropertyChanged(nameof(HasFeedback));

    public Task StartAsync()
    {
        _startedAt = _clock.Now;
        if (IsExam)
        {
            _timerCancellation = new CancellationTokenSource();
            _ = RunTimerAsync(_timerCancellation.Token);
        }
        StartSpeechIfEnabled();
        return Task.CompletedTask;
    }

    [RelayCommand]
    private async Task SelectOptionAsync(QuestionOptionViewModel option)
    {
        if (!CanAnswerCurrent)
        {
            return;
        }

        if (IsMultipleChoice)
        {
            return;
        }

        foreach (var item in Options)
        {
            item.IsSelected = item == option;
        }
        await EvaluateCurrentAsync(option.Key);
    }

    [RelayCommand]
    private Task ConfirmAnswerAsync()
    {
        var answer = string.Concat(Options.Where(option => option.IsSelected).Select(option => option.Key));
        return EvaluateCurrentAsync(answer);
    }

    [RelayCommand]
    private Task RevealAnswerAsync()
    {
        IsReferenceVisible = true;
        ReferenceAnswer = CurrentQuestion.Answer;
        return EvaluateCurrentAsync(AnswerText);
    }

    [RelayCommand]
    private Task NavigateToAsync(QuestionNavigationItem item) => MoveToAsync(item.Index, isManual: true);

    [RelayCommand]
    private Task PreviousAsync() => MoveToAsync(CurrentIndex - 1, isManual: true);

    [RelayCommand]
    private Task NextAsync() => MoveToAsync(CurrentIndex + 1, isManual: true);

    [RelayCommand]
    private async Task ReadQuestionAsync()
    {
        try
        {
            await SpeakCurrentAsync();
        }
        catch (OperationCanceledException)
        {
            // 用户再次读题或切题时取消上一段语音属于正常流程。
        }
        catch (Exception exception)
        {
            _logger.Error(exception, "手动朗读题目失败，题目 {QuestionId}", CurrentQuestion.Id);
            await _dialogs.ShowMessageAsync("读题失败", "系统语音暂时不可用，请检查 Windows 语音设置后重试。");
        }
    }

    [RelayCommand]
    private async Task SubmitExamAsync()
    {
        if (!IsExam || _finished)
        {
            return;
        }
        if (await _dialogs.ConfirmAsync("交卷确认", "交卷后不能继续修改答案，确定现在交卷吗？", "交卷"))
        {
            await CompleteExamAsync(autoSubmitted: false);
        }
    }

    [RelayCommand]
    private async Task ExitAsync()
    {
        if (_finished)
        {
            return;
        }
        if (IsExam && !await _dialogs.ConfirmAsync("退出考试", "退出将按当前答案交卷，确定继续吗？", "退出并交卷"))
        {
            return;
        }
        if (IsExam)
        {
            await CompleteExamAsync(autoSubmitted: false);
            return;
        }
        await StopAsync();
        await _exit();
    }

    public async Task StopAsync()
    {
        CancelTimer();
        CancelAutoNext();
        _speechService.Cancel();
        try
        {
            await PersistPracticeSafelyAsync();
        }
        finally
        {
            _finished = true;
        }
    }

    private async Task EvaluateCurrentAsync(string? userAnswer)
    {
        if (_finished || !CanAnswerCurrent)
        {
            return;
        }
        var evaluation = _answerEvaluator.Evaluate(CurrentQuestion, userAnswer);
        if (evaluation.Status == AnswerStatus.Unanswered)
        {
            FeedbackStatus = AnswerStatus.Unanswered;
            FeedbackMessage = "请先作答。";
            return;
        }

        _answers[CurrentQuestion.Id] = userAnswer ?? string.Empty;
        _answerStatuses[CurrentQuestion.Id] = evaluation.Status;
        if (Mode == SessionMode.Practice)
        {
            _practiceSession!.Submit(userAnswer);
        }
        if (IsExam || evaluation.IsCorrect)
        {
            _lockedQuestionIds.Add(CurrentQuestion.Id);
        }

        FeedbackStatus = evaluation.Status;
        FeedbackMessage = evaluation.IsCorrect
            ? "回答正确"
            : Mode == SessionMode.Practice || Mode == SessionMode.ErrorReview
                ? $"回答错误，正确答案：{CurrentQuestion.Answer}。请重新作答"
                : $"回答错误，正确答案：{CurrentQuestion.Answer}";
        UpdateOptionFeedback(userAnswer, evaluation.Status);
        if (!evaluation.IsCorrect)
        {
            try
            {
                await _errorBookRepository.AddAttemptAsync(
                    CurrentQuestion,
                    userAnswer ?? string.Empty,
                    Mode.ToString().ToLowerInvariant(),
                    _clock.Now);
            }
            catch (Exception exception)
            {
                _logger.Error(exception, "保存错题失败，题目 {QuestionId}", CurrentQuestion.Id);
                FeedbackMessage += "；本次错题记录保存失败";
            }
        }
        UpdateNavigatorStates();
        await PersistPracticeSafelyAsync();
        OnPropertyChanged(nameof(CanAnswerCurrent));

        if (evaluation.IsCorrect)
        {
            ScheduleAutoNext(CurrentQuestion.Id, CurrentIndex);
        }
    }

    private async Task MoveToAsync(int targetIndex, bool isManual)
    {
        if (_finished || targetIndex < 0 || targetIndex >= Questions.Count || targetIndex == CurrentIndex)
        {
            return;
        }
        if (isManual)
        {
            CancelAutoNext();
        }
        _speechService.Cancel();
        CurrentIndex = targetIndex;
        _practiceSession?.MoveTo(targetIndex);
        LoadCurrentQuestion();
        await PersistPracticeSafelyAsync();
        StartSpeechIfEnabled();
    }

    private void LoadCurrentQuestion()
    {
        CurrentQuestion = Questions[CurrentIndex];
        Options.Clear();
        foreach (var option in CurrentQuestion.Options.OrderBy(pair => pair.Key, StringComparer.OrdinalIgnoreCase))
        {
            Options.Add(new QuestionOptionViewModel(option.Key, option.Value));
        }
        _answers.TryGetValue(CurrentQuestion.Id, out var existingAnswer);
        AnswerText = existingAnswer ?? string.Empty;
        foreach (var option in Options)
        {
            option.IsSelected = AnswerEvaluator.Normalize(CurrentQuestion.Type, existingAnswer).Contains(option.Key, StringComparison.OrdinalIgnoreCase);
        }
        IsReferenceVisible = false;
        ReferenceAnswer = string.Empty;
        if (_answerStatuses.TryGetValue(CurrentQuestion.Id, out var status))
        {
            FeedbackStatus = status;
            FeedbackMessage = status == AnswerStatus.Correct ? "回答正确" : "回答错误";
            UpdateOptionFeedback(existingAnswer, status);
        }
        else
        {
            FeedbackStatus = AnswerStatus.Unanswered;
            FeedbackMessage = string.Empty;
            UpdateOptionFeedback(null, AnswerStatus.Unanswered);
        }
        UpdateNavigatorStates();
        RaiseQuestionProperties();
    }

    private void RaiseQuestionProperties()
    {
        OnPropertyChanged(nameof(IsShortAnswer));
        OnPropertyChanged(nameof(IsMultipleChoice));
        OnPropertyChanged(nameof(ShowsConfirmAnswer));
        OnPropertyChanged(nameof(ShowsReferenceAnswer));
        OnPropertyChanged(nameof(CanAnswerCurrent));
        OnPropertyChanged(nameof(CanMovePrevious));
        OnPropertyChanged(nameof(CanMoveNext));
        OnPropertyChanged(nameof(ProgressText));
        OnPropertyChanged(nameof(QuestionNumberText));
        OnPropertyChanged(nameof(TypeDisplay));
        OnPropertyChanged(nameof(SourceDisplay));
        OnPropertyChanged(nameof(LevelDisplay));
    }

    private void UpdateNavigatorStates()
    {
        foreach (var item in NavigatorItems)
        {
            item.AnswerState = _answerStatuses.TryGetValue(item.Question.Id, out var status)
                ? status == AnswerStatus.Correct ? NavigationState.Correct : NavigationState.Incorrect
                : NavigationState.Unanswered;
            item.IsCurrent = item.Index == CurrentIndex;
        }
    }

    private void ScheduleAutoNext(string questionId, int sourceIndex)
    {
        CancelAutoNext();
        if (sourceIndex >= Questions.Count - 1)
        {
            return;
        }
        _autoNextCancellation = new CancellationTokenSource();
        _ = AutoMoveNextAsync(questionId, sourceIndex, _autoNextCancellation.Token);
    }

    private async Task AutoMoveNextAsync(string questionId, int sourceIndex, CancellationToken cancellationToken)
    {
        try
        {
            var delayMilliseconds = AppSettings.NormalizeAutoNextDelayMilliseconds(
                _settings.AutoNextDelayMilliseconds);
            await _clock.DelayAsync(TimeSpan.FromMilliseconds(delayMilliseconds), cancellationToken);
            if (!_finished && CurrentIndex == sourceIndex && CurrentQuestion.Id == questionId &&
                _answerStatuses.GetValueOrDefault(questionId) == AnswerStatus.Correct)
            {
                await MoveToAsync(sourceIndex + 1, isManual: false);
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            // 用户手动切题或新答案到达时，旧跳题任务按设计取消。
        }
        catch (Exception exception)
        {
            _logger.Error(exception, "自动跳题失败，题目 {QuestionId}", questionId);
            FeedbackMessage = "答案已记录，但自动跳题失败，请手动点击下一题。";
        }
    }

    private async Task RunTimerAsync(CancellationToken cancellationToken)
    {
        try
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                await _clock.DelayAsync(TimeSpan.FromSeconds(1), cancellationToken);
                var elapsed = _clock.Now - _startedAt;
                RemainingTime = elapsed >= _paper!.Duration ? TimeSpan.Zero : _paper.Duration - elapsed;
                if (RemainingTime <= TimeSpan.Zero)
                {
                    await CompleteExamAsync(autoSubmitted: true);
                    return;
                }
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            // 正常交卷会取消计时器。
        }
        catch (Exception exception)
        {
            _logger.Error(exception, "考试倒计时任务失败");
            await _dialogs.ShowMessageAsync("倒计时异常", "倒计时已停止，请尽快手动交卷；当前答案不会丢失。");
        }
    }

    private async Task CompleteExamAsync(bool autoSubmitted)
    {
        if (_finished)
        {
            return;
        }
        _finished = true;
        CancelTimer();
        CancelAutoNext();
        _speechService.Cancel();
        var elapsed = _clock.Now - _startedAt;
        if (elapsed < TimeSpan.Zero) elapsed = TimeSpan.Zero;
        if (elapsed > _paper!.Duration) elapsed = _paper.Duration;
        var result = new ExamScorer(_answerEvaluator).Score(_paper, _answers, elapsed);
        foreach (var unanswered in result.Questions.Where(item => item.Status == AnswerStatus.Unanswered))
        {
            try
            {
                await _errorBookRepository.AddAttemptAsync(
                    unanswered.Question,
                    string.Empty,
                    "exam",
                    _clock.Now);
            }
            catch (Exception exception)
            {
                _logger.Error(exception, "记录未答题失败，题目 {QuestionId}", unanswered.Question.Id);
            }
        }
        if (autoSubmitted)
        {
            await _dialogs.ShowMessageAsync("考试时间到", "45 分钟倒计时已结束，系统已自动交卷。");
        }
        var incorrectQuestions = result.Questions
            .Where(item => item.Status != AnswerStatus.Correct)
            .Select(item => item.Question)
            .ToArray();
        if (_examFinished is not null)
        {
            await _examFinished(result, incorrectQuestions);
        }
    }

    private async Task PersistPracticeAsync()
    {
        if (_practiceSession is null)
        {
            return;
        }
        var snapshot = _practiceSession.CreateSnapshot(
            $"practice-{string.Join('-', _practiceLevels.Order().Select(level => (int)level))}-{string.Join('-', _practiceCategories.Order().Select(category => (int)category))}-{(int)CurrentQuestion.Type}",
            _practiceLevels,
            _practiceCategories,
            _clock.Now);
        await _progressRepository.SaveAsync(snapshot);
    }

    private async Task PersistPracticeSafelyAsync()
    {
        try
        {
            await PersistPracticeAsync();
        }
        catch (Exception exception)
        {
            _logger.Error(exception, "保存专项练习进度失败");
            FeedbackMessage = string.IsNullOrWhiteSpace(FeedbackMessage)
                ? "练习进度保存失败，请稍后重试。"
                : $"{FeedbackMessage}；练习进度保存失败";
        }
    }

    private void StartSpeechIfEnabled()
    {
        if (_settings.AutoSpeechEnabled)
        {
            _ = SpeakCurrentSafelyAsync();
        }
    }

    private Task SpeakCurrentAsync()
    {
        _speechService.Cancel();
        var text = CurrentQuestion.Text;
        if (CurrentQuestion.Options.Count > 0)
        {
            text += "。" + string.Join("。", CurrentQuestion.Options.OrderBy(pair => pair.Key).Select(pair => $"选项{pair.Key}，{pair.Value}"));
        }
        return _speechService.SpeakAsync(text, _settings.VoiceName, _settings.SpeechRate);
    }

    private async Task SpeakCurrentSafelyAsync()
    {
        try
        {
            await SpeakCurrentAsync();
        }
        catch (OperationCanceledException)
        {
            // 切题时取消上一段语音属于正常流程。
        }
        catch (Exception exception)
        {
            _logger.Warning(exception, "自动朗读失败，题目 {QuestionId}", CurrentQuestion.Id);
        }
    }

    private void UpdateOptionFeedback(string? userAnswer, AnswerStatus status)
    {
        foreach (var option in Options)
        {
            option.IsCorrect = status != AnswerStatus.Unanswered && IsCorrectOption(option.Key);
            option.IsIncorrectSelection = status == AnswerStatus.Incorrect &&
                                          option.IsSelected &&
                                          !option.IsCorrect;
        }
    }

    private bool IsCorrectOption(string optionKey)
    {
        var normalizedCorrect = AnswerEvaluator.Normalize(CurrentQuestion.Type, CurrentQuestion.Answer);
        if (CurrentQuestion.Type == QuestionType.TrueFalse)
        {
            return string.Equals(
                AnswerEvaluator.Normalize(CurrentQuestion.Type, optionKey),
                normalizedCorrect,
                StringComparison.OrdinalIgnoreCase);
        }
        return normalizedCorrect.Contains(optionKey, StringComparison.OrdinalIgnoreCase);
    }

    private void CancelTimer()
    {
        _timerCancellation?.Cancel();
        _timerCancellation?.Dispose();
        _timerCancellation = null;
    }

    private void CancelAutoNext()
    {
        _autoNextCancellation?.Cancel();
        _autoNextCancellation?.Dispose();
        _autoNextCancellation = null;
    }
}
