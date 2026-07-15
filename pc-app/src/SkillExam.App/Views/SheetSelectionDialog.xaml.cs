using System.Collections.ObjectModel;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using SkillExam.Core.Models;

namespace SkillExam.App.Views;

public sealed partial class SheetChoice(string name, bool isSelected) : ObservableObject
{
    public string Name { get; } = name;
    [ObservableProperty] private bool _isSelected = isSelected;
}

public partial class SheetSelectionDialog : Window
{
    public SheetSelectionDialog(IReadOnlyList<SheetInfo> sheets, IReadOnlyCollection<string>? previousSelection)
    {
        InitializeComponent();
        var previous = previousSelection is { Count: > 0 }
            ? previousSelection.ToHashSet(StringComparer.OrdinalIgnoreCase)
            : null;
        Choices = new ObservableCollection<SheetChoice>(sheets.Select(sheet =>
            new SheetChoice(sheet.Name, previous?.Contains(sheet.Name) ?? sheet.IsSelectedByDefault)));
        DataContext = this;
    }

    public ObservableCollection<SheetChoice> Choices { get; }
    public IReadOnlyList<string> SelectedSheets => Choices.Where(choice => choice.IsSelected).Select(choice => choice.Name).ToArray();
    private void Confirm_OnClick(object sender, RoutedEventArgs e) => DialogResult = true;
    private void Cancel_OnClick(object sender, RoutedEventArgs e) => DialogResult = false;
}
