using System.Collections.ObjectModel;
using System.ComponentModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SkillExam.App.Services;
using SkillExam.Core.Exam;
using SkillExam.Core.Models;

namespace SkillExam.App.ViewModels;

public sealed partial class PracticeSetupViewModel : ObservableObject
{
    private readonly QuestionBankState _bankState;
    private readonly Func<IReadOnlyList<SkillLevel>, IReadOnlyList<QuestionCategory>, QuestionType, Task> _startPractice;
    private readonly Action _back;

    public PracticeSetupViewModel(
        QuestionBankState bankState,
        SkillLevel defaultLevel,
        QuestionCategory? defaultCategory,
        Func<IReadOnlyList<SkillLevel>, IReadOnlyList<QuestionCategory>, QuestionType, Task> startPractice,
        Action back)
    {
        _bankState = bankState;
        _startPractice = startPractice;
        _back = back;
        Levels = new ObservableCollection<SelectionItem<SkillLevel>>(
            SkillLevelExtensions.All.Select(level => new SelectionItem<SkillLevel>(level, level.ToDisplayName(), level == defaultLevel)));
        Categories = new ObservableCollection<SelectionItem<QuestionCategory>>(
            QuestionCategoryExtensions.All.Select(category => new SelectionItem<QuestionCategory>(
                category,
                category.ToDisplayName(),
                defaultCategory is null || category == defaultCategory)));
        foreach (var item in Levels.Cast<object>().Concat(Categories))
        {
            ((INotifyPropertyChanged)item).PropertyChanged += (_, _) => UpdateCounts();
        }
        UpdateCounts();
    }

    public ObservableCollection<SelectionItem<SkillLevel>> Levels { get; }
    public ObservableCollection<SelectionItem<QuestionCategory>> Categories { get; }

    [ObservableProperty] private int _singleChoiceCount;
    [ObservableProperty] private int _multipleChoiceCount;
    [ObservableProperty] private int _trueFalseCount;
    [ObservableProperty] private int _shortAnswerCount;

    [RelayCommand]
    private Task StartPracticeAsync(QuestionType questionType)
    {
        var levels = Levels.Where(item => item.IsSelected).Select(item => item.Value).ToArray();
        var categories = Categories.Where(item => item.IsSelected).Select(item => item.Value).ToArray();
        return _startPractice(levels, categories, questionType);
    }

    [RelayCommand]
    private void Back() => _back();

    [RelayCommand]
    private void RefreshCounts() => UpdateCounts();

    private void UpdateCounts()
    {
        var levels = Levels.Where(item => item.IsSelected).Select(item => item.Value).ToArray();
        var categories = Categories.Where(item => item.IsSelected).Select(item => item.Value).ToArray();
        var questions = levels.Length == 0 ? [] : QuestionFilter.Apply(_bankState.Questions, levels, categories);
        SingleChoiceCount = questions.Count(question => question.Type == QuestionType.SingleChoice);
        MultipleChoiceCount = questions.Count(question => question.Type == QuestionType.MultipleChoice);
        TrueFalseCount = questions.Count(question => question.Type == QuestionType.TrueFalse);
        ShortAnswerCount = questions.Count(question => question.Type == QuestionType.ShortAnswer);
    }
}
