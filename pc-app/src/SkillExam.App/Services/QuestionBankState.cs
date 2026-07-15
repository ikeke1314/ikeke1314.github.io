using CommunityToolkit.Mvvm.ComponentModel;
using SkillExam.Core.Models;

namespace SkillExam.App.Services;

public sealed partial class QuestionBankState : ObservableObject
{
    [ObservableProperty]
    private IReadOnlyList<Question> _questions = [];

    [ObservableProperty]
    private QuestionBankStatistics? _statistics;

    [ObservableProperty]
    private string? _filePath;

    [ObservableProperty]
    private IReadOnlyList<string> _loadedSheets = [];

    [ObservableProperty]
    private string _status = "尚未加载题库";

    public bool IsLoaded => Questions.Count > 0;
    public string LoadedSheetsText => LoadedSheets.Count == 0
        ? "Sheet：未加载"
        : $"Sheet：{string.Join("、", LoadedSheets)}";

    public void SetLoaded(string filePath, QuestionBankLoadResult result)
    {
        FilePath = filePath;
        Questions = result.Questions;
        Statistics = QuestionBankStatistics.From(result.Questions);
        LoadedSheets = result.LoadedSheets;
        Status = $"已加载 {result.Questions.Count} 题 · {result.LoadedSheets.Count} 个 Sheet";
        OnPropertyChanged(nameof(IsLoaded));
        OnPropertyChanged(nameof(LoadedSheetsText));
    }

    public void SetFailure(string status)
    {
        Questions = [];
        Statistics = null;
        LoadedSheets = [];
        Status = status;
        OnPropertyChanged(nameof(IsLoaded));
        OnPropertyChanged(nameof(LoadedSheetsText));
    }
}
