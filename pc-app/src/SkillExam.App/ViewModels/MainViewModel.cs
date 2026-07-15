using System.IO;
using CommunityToolkit.Mvvm.ComponentModel;
using SkillExam.App.Services;
using SkillExam.Core.Abstractions;
using SkillExam.Core.Exam;
using SkillExam.Core.Models;
using SkillExam.Infrastructure.Migration;
using SkillExam.Infrastructure.Persistence;
using Serilog;

namespace SkillExam.App.ViewModels;

public sealed partial class MainViewModel : ObservableObject
{
    private readonly IQuestionBankReader _questionBankReader;
    private readonly IExamGenerator _examGenerator;
    private readonly IAnswerEvaluator _answerEvaluator;
    private readonly IProgressRepository _progressRepository;
    private readonly IErrorBookRepository _errorBookRepository;
    private readonly ISettingsRepository _settingsRepository;
    private readonly ISpeechService _speechService;
    private readonly IClock _clock;
    private readonly IDialogService _dialogs;
    private readonly LegacyDataMigrator _legacyMigrator;
    private readonly DatabaseBackupService _backupService;
    private readonly AppDataPaths _paths;
    private readonly ILogger _logger;
    private AppSettings _settings = new();

    public MainViewModel(
        QuestionBankState bankState,
        IQuestionBankReader questionBankReader,
        IExamGenerator examGenerator,
        IAnswerEvaluator answerEvaluator,
        IProgressRepository progressRepository,
        IErrorBookRepository errorBookRepository,
        ISettingsRepository settingsRepository,
        ISpeechService speechService,
        IClock clock,
        IDialogService dialogs,
        LegacyDataMigrator legacyMigrator,
        DatabaseBackupService backupService,
        AppDataPaths paths,
        ILogger logger)
    {
        BankState = bankState;
        _questionBankReader = questionBankReader;
        _examGenerator = examGenerator;
        _answerEvaluator = answerEvaluator;
        _progressRepository = progressRepository;
        _errorBookRepository = errorBookRepository;
        _settingsRepository = settingsRepository;
        _speechService = speechService;
        _clock = clock;
        _dialogs = dialogs;
        _legacyMigrator = legacyMigrator;
        _backupService = backupService;
        _paths = paths;
        _logger = logger;
        CurrentPage = new object();
    }

    public QuestionBankState BankState { get; }

    [ObservableProperty] private object _currentPage;
    [ObservableProperty] private bool _isBusy;
    [ObservableProperty] private string _loadingMessage = "正在准备应用…";

    public async Task InitializeAsync()
    {
        IsBusy = true;
        try
        {
            _settings = await _settingsRepository.GetAsync();
            var executableDirectory = AppContext.BaseDirectory;
            if (HasLegacyFiles(executableDirectory) &&
                await _dialogs.ConfirmAsync("发现旧版数据", "检测到程序旁存在旧版配置、错题或练习进度，是否先备份并迁移？", "迁移"))
            {
                await MigrateFromDirectoryAsync(executableDirectory);
                _settings = await _settingsRepository.GetAsync();
            }
            if (!string.IsNullOrWhiteSpace(_settings.LastQuestionBankPath))
            {
                if (File.Exists(_settings.LastQuestionBankPath))
                {
                    await LoadQuestionBankPathAsync(_settings.LastQuestionBankPath, _settings.SelectedSheets, showIssues: false);
                }
                else
                {
                    BankState.SetFailure("上次题库路径已失效，请重新选择题库");
                }
            }
            else
            {
                var bundledBankPath = Path.Combine(executableDirectory, "exam_bank", "PE 技能士题库.xlsx");
                if (File.Exists(bundledBankPath))
                {
                    await LoadQuestionBankPathAsync(bundledBankPath, selectedSheets: null, showIssues: false);
                }
            }
            ShowHome();
        }
        catch (Exception exception)
        {
            _logger.Error(exception, "应用初始化失败");
            BankState.SetFailure("初始化失败，请查看日志或重新选择题库");
            ShowHome();
            await _dialogs.ShowMessageAsync("初始化失败", "应用初始化时遇到问题。技术详情已写入日志，用户数据未被删除。");
        }
        finally
        {
            IsBusy = false;
        }
    }

    public async Task PrepareForShutdownAsync()
    {
        if (CurrentPage is QuestionSessionViewModel session)
        {
            try
            {
                await session.StopAsync();
            }
            catch (Exception exception)
            {
                _logger.Error(exception, "退出时保存答题会话失败");
            }
        }
        try
        {
            await _settingsRepository.SaveAsync(_settings);
        }
        catch (Exception exception)
        {
            _logger.Error(exception, "退出时保存设置失败");
        }
        finally
        {
            _speechService.Cancel();
        }
    }

    private void ShowHome()
    {
        CurrentPage = new HomeViewModel(
            BankState,
            _examGenerator,
            LoadQuestionBankAsync,
            StartExamAsync,
            ShowPracticeAsync,
            ShowErrorBookAsync,
            ShowSettings,
            MigrateLegacyAsync);
    }

    private async Task LoadQuestionBankAsync()
    {
        var path = await _dialogs.PickQuestionBankAsync();
        if (path is null)
        {
            return;
        }
        IsBusy = true;
        LoadingMessage = "正在读取 Sheet 列表…";
        try
        {
            var sheets = await _questionBankReader.GetSheetsAsync(path);
            var selected = await _dialogs.SelectSheetsAsync(sheets, _settings.SelectedSheets);
            if (selected is null || selected.Count == 0)
            {
                return;
            }
            await LoadQuestionBankPathAsync(path, selected, showIssues: true);
        }
        catch (Exception exception)
        {
            _logger.Error(exception, "题库加载失败，文件 {FileName}", Path.GetFileName(path));
            BankState.SetFailure("题库加载失败");
            await _dialogs.ShowMessageAsync("题库加载失败", "无法读取该 Excel 文件。请确认文件未损坏、格式为 .xlsx/.xls，并查看日志摘要。");
        }
        finally
        {
            IsBusy = false;
        }
    }

    private async Task LoadQuestionBankPathAsync(string path, IReadOnlyCollection<string>? selectedSheets, bool showIssues)
    {
        LoadingMessage = "正在异步解析题库…";
        var result = await _questionBankReader.ReadAsync(path, selectedSheets);
        if (!result.HasUsableQuestions)
        {
            BankState.SetFailure("题库没有可用题目");
            await _dialogs.ShowErrorSummaryAsync("题库不可用", "没有导入任何有效题目。", result.Issues);
            return;
        }
        BankState.SetLoaded(path, result);
        _settings = _settings with { LastQuestionBankPath = path, SelectedSheets = result.LoadedSheets };
        await _settingsRepository.SaveAsync(_settings);
        if (showIssues && result.Issues.Count > 0)
        {
            await _dialogs.ShowErrorSummaryAsync(
                "题库已加载（含警告）",
                $"成功导入 {result.Questions.Count} 题，跳过 {result.InvalidRowCount} 行。",
                result.Issues);
        }
    }

    private async Task StartExamAsync(SkillLevel level, IReadOnlyList<QuestionCategory> categories)
    {
        try
        {
            var generated = _examGenerator.Generate(BankState.Questions, new ExamRequest(level, categories, Seed: null));
            if (!generated.Success)
            {
                var issues = generated.Shortages.Select(shortage => new QuestionBankIssue(
                    shortage.Level.ToDisplayName(),
                    null,
                    shortage.QuestionType.ToDisplayName(),
                    $"需要 {shortage.Required} 题，可分配 {shortage.Available} 题，缺少 {shortage.Missing} 题。"))
                    .ToArray();
                await _dialogs.ShowErrorSummaryAsync("无法生成完整试卷", "题量不足时不会生成低于 100 分的试卷。", issues);
                return;
            }
            var paper = generated.Paper!;
            var session = CreateSession(
                paper.Questions,
                SessionMode.Exam,
                paper: paper,
                examFinished: ShowResultAsync);
            CurrentPage = session;
            await session.StartAsync();
        }
        catch (Exception exception)
        {
            _logger.Error(exception, "开始模拟考试失败，等级 {Level}", level);
            await _dialogs.ShowMessageAsync("无法开始考试", "试卷生成或会话初始化失败，技术详情已写入日志。");
        }
    }

    private async Task ShowPracticeAsync(SkillLevel defaultLevel, QuestionCategory? defaultCategory)
    {
        if (!BankState.IsLoaded)
        {
            await _dialogs.ShowMessageAsync("尚未加载题库", "请先在首页加载 Excel 题库。");
            return;
        }
        CurrentPage = new PracticeSetupViewModel(
            BankState,
            defaultLevel,
            defaultCategory,
            StartPracticeAsync,
            ShowHome);
    }

    private async Task StartPracticeAsync(
        IReadOnlyList<SkillLevel> levels,
        IReadOnlyList<QuestionCategory> categories,
        QuestionType type)
    {
        try
        {
            if (levels.Count == 0)
            {
                await _dialogs.ShowMessageAsync("请选择等级", "专项练习至少需要选择一个等级。");
                return;
            }
            var questions = QuestionFilter.Apply(BankState.Questions, levels, categories, type: type);
            if (questions.Count == 0)
            {
                await _dialogs.ShowMessageAsync("没有可练习题目", "当前等级、类别和题型组合没有可用题目。");
                return;
            }
            var snapshot = await _progressRepository.GetLatestAsync(levels, categories, type);
            var ordered = OrderFromSnapshot(questions, snapshot);
            var session = CreateSession(
                ordered,
                SessionMode.Practice,
                practiceSnapshot: snapshot,
                practiceLevels: levels,
                practiceCategories: categories);
            CurrentPage = session;
            await session.StartAsync();
        }
        catch (Exception exception)
        {
            _logger.Error(exception, "开始专项练习失败，题型 {QuestionType}", type);
            await _dialogs.ShowMessageAsync("无法开始练习", "读取练习进度或初始化会话失败，技术详情已写入日志。");
        }
    }

    private async Task ShowErrorBookAsync()
    {
        var viewModel = new ErrorBookViewModel(_errorBookRepository, _dialogs, _logger, StartErrorReviewAsync, ShowHome);
        CurrentPage = viewModel;
        await viewModel.LoadAsync();
    }

    private async Task StartErrorReviewAsync(IReadOnlyList<Question> questions)
    {
        if (questions.Count == 0)
        {
            await _dialogs.ShowMessageAsync("没有错题", "当前筛选下没有可复习的错题。");
            return;
        }
        var session = CreateSession(questions, SessionMode.ErrorReview);
        CurrentPage = session;
        await session.StartAsync();
    }

    private void ShowSettings()
    {
        CurrentPage = new SettingsViewModel(
            _settings,
            _settingsRepository,
            _speechService,
            _backupService,
            _paths,
            _dialogs,
            _logger,
            settings =>
            {
                _settings = settings;
                return Task.CompletedTask;
            },
            ShowHome);
    }

    private Task ShowResultAsync(ExamResult result, IReadOnlyList<Question> incorrectQuestions)
    {
        CurrentPage = new ResultViewModel(result, incorrectQuestions, ShowHome, StartErrorReviewAsync);
        return Task.CompletedTask;
    }

    private QuestionSessionViewModel CreateSession(
        IReadOnlyList<Question> questions,
        SessionMode mode,
        ExamPaper? paper = null,
        Func<ExamResult, IReadOnlyList<Question>, Task>? examFinished = null,
        PracticeSessionSnapshot? practiceSnapshot = null,
        IReadOnlyList<SkillLevel>? practiceLevels = null,
        IReadOnlyList<QuestionCategory>? practiceCategories = null) => new(
            questions,
            mode,
            _answerEvaluator,
            _errorBookRepository,
            _progressRepository,
            _speechService,
            _clock,
            _dialogs,
            _settings,
            _logger,
            () =>
            {
                ShowHome();
                return Task.CompletedTask;
            },
            examFinished,
            paper,
            practiceSnapshot,
            practiceLevels,
            practiceCategories);

    private async Task MigrateLegacyAsync()
    {
        var directory = await _dialogs.PickFolderAsync("选择包含旧版 JSON 的目录");
        if (directory is not null)
        {
            await MigrateFromDirectoryAsync(directory);
            _settings = await _settingsRepository.GetAsync();
        }
    }

    private async Task MigrateFromDirectoryAsync(string directory)
    {
        IsBusy = true;
        LoadingMessage = "正在备份并迁移旧版数据…";
        try
        {
            var result = await _legacyMigrator.MigrateAsync(directory);
            var message = result.WasAlreadyMigrated
                ? "该数据库已完成旧版数据迁移，无需重复导入。"
                : $"迁移错题作答 {result.ErrorAttemptsMigrated}/{result.ErrorAttemptsRead} 条，练习进度 {result.PracticeSnapshotsMigrated} 组。\n备份目录：{result.BackupDirectory ?? "未创建"}";
            if (result.Warnings.Count > 0)
            {
                message += $"\n警告：{string.Join("；", result.Warnings)}";
            }
            await _dialogs.ShowMessageAsync("旧版数据迁移", message);
        }
        catch (Exception exception)
        {
            _logger.Error(exception, "旧版数据迁移失败");
            await _dialogs.ShowMessageAsync("迁移失败", "迁移事务已回滚，旧 JSON 未被修改或删除；迁移前备份如已创建也会保留。");
        }
        finally
        {
            IsBusy = false;
        }
    }

    private static IReadOnlyList<Question> OrderFromSnapshot(
        IReadOnlyList<Question> questions,
        PracticeSessionSnapshot? snapshot)
    {
        if (snapshot is null)
        {
            return questions.OrderBy(question => question.Id, StringComparer.Ordinal).ToArray();
        }
        var byId = questions.ToDictionary(question => question.Id);
        var restored = snapshot.QuestionIds.Where(byId.ContainsKey).Select(id => byId[id]).ToList();
        restored.AddRange(questions.Where(question => !snapshot.QuestionIds.Contains(question.Id)).OrderBy(question => question.Id));
        return restored;
    }

    private static bool HasLegacyFiles(string directory) =>
        new[] { "config.json", "error_questions.json", "practice_progress.json" }
            .Any(name => File.Exists(Path.Combine(directory, name)));
}
