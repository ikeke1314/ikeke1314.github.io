using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SkillExam.App.Services;
using SkillExam.Core.Abstractions;
using SkillExam.Core.Models;

namespace SkillExam.App.ViewModels;

public sealed record HomeCategoryOption(string DisplayName, QuestionCategory? Value);

public sealed partial class HomeViewModel : ObservableObject
{
    private readonly IExamGenerator _examGenerator;
    private readonly Func<Task> _loadBank;
    private readonly Func<SkillLevel, IReadOnlyList<QuestionCategory>, Task> _startExam;
    private readonly Func<SkillLevel, QuestionCategory?, Task> _showPractice;
    private readonly Func<Task> _showErrors;
    private readonly Action _showSettings;
    private readonly Func<Task> _migrateLegacy;

    public HomeViewModel(
        QuestionBankState bankState,
        IExamGenerator examGenerator,
        Func<Task> loadBank,
        Func<SkillLevel, IReadOnlyList<QuestionCategory>, Task> startExam,
        Func<SkillLevel, QuestionCategory?, Task> showPractice,
        Func<Task> showErrors,
        Action showSettings,
        Func<Task> migrateLegacy)
    {
        BankState = bankState;
        _examGenerator = examGenerator;
        _loadBank = loadBank;
        _startExam = startExam;
        _showPractice = showPractice;
        _showErrors = showErrors;
        _showSettings = showSettings;
        _migrateLegacy = migrateLegacy;
        CategoryOptions = [
            new HomeCategoryOption("全部", null),
            .. QuestionCategoryExtensions.All.Select(category => new HomeCategoryOption(category.ToDisplayName(), category))
        ];
        SelectedCategory = CategoryOptions[0];
        BankState.PropertyChanged += (_, _) => StartExamCommand.NotifyCanExecuteChanged();
    }

    public QuestionBankState BankState { get; }
    public IReadOnlyList<SkillLevel> Levels => SkillLevelExtensions.All;
    public IReadOnlyList<HomeCategoryOption> CategoryOptions { get; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(RuleText))]
    private SkillLevel _selectedLevel = SkillLevel.Level1;

    [ObservableProperty]
    private HomeCategoryOption _selectedCategory;

    public string RuleText => string.Join(" · ", _examGenerator.GetWeights(SelectedLevel)
        .Select(pair => $"{pair.Key.ToDisplayName()} {pair.Value:P0}"));

    [RelayCommand]
    private Task LoadBankAsync() => _loadBank();

    private bool CanStartExam() => BankState.IsLoaded;

    [RelayCommand(CanExecute = nameof(CanStartExam))]
    private Task StartExamAsync() => _startExam(
        SelectedLevel,
        SelectedCategory.Value is QuestionCategory category ? [category] : []);

    [RelayCommand]
    private Task ShowPracticeAsync() => _showPractice(SelectedLevel, SelectedCategory.Value);

    [RelayCommand]
    private Task ShowErrorsAsync() => _showErrors();

    [RelayCommand]
    private void ShowSettings() => _showSettings();

    [RelayCommand]
    private Task MigrateLegacyAsync() => _migrateLegacy();
}
