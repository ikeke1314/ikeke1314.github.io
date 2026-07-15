using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Serilog;
using SkillExam.App.Services;
using SkillExam.Core.Abstractions;
using SkillExam.Core.Models;

namespace SkillExam.App.ViewModels;

public sealed record QuestionTypeFilter(string Name, QuestionType? Type);

public sealed partial class ErrorBookItemViewModel(ErrorBookItem item) : ObservableObject
{
    public ErrorBookItem Item { get; } = item;
    public Question Question => Item.Question;
    public string TypeDisplay => Question.Type.ToDisplayName();
    public string AttemptText => $"累计答错 {Item.AttemptCount} 次 · 最近 {Item.LastAttemptedAt:yyyy-MM-dd HH:mm}";
}

public sealed partial class ErrorBookViewModel : ObservableObject
{
    private readonly IErrorBookRepository _repository;
    private readonly IDialogService _dialogs;
    private readonly ILogger _logger;
    private readonly Func<IReadOnlyList<Question>, Task> _review;
    private readonly Action _back;
    private IReadOnlyList<ErrorBookItem> _allItems = [];

    public ErrorBookViewModel(
        IErrorBookRepository repository,
        IDialogService dialogs,
        ILogger logger,
        Func<IReadOnlyList<Question>, Task> review,
        Action back)
    {
        _repository = repository;
        _dialogs = dialogs;
        _logger = logger;
        _review = review;
        _back = back;
        Filters = [
            new QuestionTypeFilter("全部题型", null),
            .. Enum.GetValues<QuestionType>().Select(type => new QuestionTypeFilter(type.ToDisplayName(), type))
        ];
        SelectedFilter = Filters[0];
    }

    public ObservableCollection<ErrorBookItemViewModel> Items { get; } = [];
    public IReadOnlyList<QuestionTypeFilter> Filters { get; }
    public bool IsEmpty => Items.Count == 0;
    public int TotalQuestions => _allItems.Count;
    public int TotalAttempts => _allItems.Sum(item => item.AttemptCount);

    [ObservableProperty] private QuestionTypeFilter _selectedFilter;
    [ObservableProperty] private bool _isLoading;

    partial void OnSelectedFilterChanged(QuestionTypeFilter value) => ApplyFilter();

    public async Task LoadAsync()
    {
        IsLoading = true;
        try
        {
            _allItems = await _repository.GetAllAsync();
            ApplyFilter();
        }
        catch (Exception exception)
        {
            _logger.Error(exception, "加载错题本失败");
            _allItems = [];
            ApplyFilter();
            await _dialogs.ShowMessageAsync("错题本加载失败", "无法读取错题数据，技术详情已写入日志。");
        }
        finally
        {
            IsLoading = false;
        }
    }

    [RelayCommand]
    private Task ReviewAllAsync() => _review(Items.Select(item => item.Question).ToArray());

    [RelayCommand]
    private Task ReviewOneAsync(ErrorBookItemViewModel item) => _review([item.Question]);

    [RelayCommand]
    private async Task ClearAsync()
    {
        if (!await _dialogs.ConfirmAsync("清空错题本", "此操作会清空聚合错题及历史作答记录，确定继续吗？", "清空"))
        {
            return;
        }
        try
        {
            await _repository.ClearAsync();
            _allItems = [];
            ApplyFilter();
        }
        catch (Exception exception)
        {
            _logger.Error(exception, "清空错题本失败");
            await _dialogs.ShowMessageAsync("清空失败", "错题本未被清空，请查看日志后重试。");
        }
    }

    [RelayCommand]
    private void Back() => _back();

    private void ApplyFilter()
    {
        var filtered = SelectedFilter.Type is null
            ? _allItems
            : _allItems.Where(item => item.Question.Type == SelectedFilter.Type).ToArray();
        Items.Clear();
        foreach (var item in filtered)
        {
            Items.Add(new ErrorBookItemViewModel(item));
        }
        OnPropertyChanged(nameof(IsEmpty));
        OnPropertyChanged(nameof(TotalQuestions));
        OnPropertyChanged(nameof(TotalAttempts));
    }
}
