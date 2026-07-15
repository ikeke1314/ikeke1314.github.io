using CommunityToolkit.Mvvm.ComponentModel;

namespace SkillExam.App.ViewModels;

public sealed partial class SelectionItem<T>(T value, string displayName, bool isSelected = false) : ObservableObject where T : struct
{
    public T Value { get; } = value;
    public string DisplayName { get; } = displayName;

    [ObservableProperty]
    private bool _isSelected = isSelected;
}
