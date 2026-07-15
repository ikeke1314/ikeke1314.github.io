using System.Windows;
using Microsoft.Win32;
using SkillExam.App.Controls;
using SkillExam.App.Views;
using SkillExam.Core.Models;

namespace SkillExam.App.Services;

public sealed class WpfDialogService : IDialogService
{
    public Task<string?> PickQuestionBankAsync()
    {
        var dialog = new OpenFileDialog
        {
            Title = "选择技能士题库",
            Filter = "Excel 题库 (*.xlsx;*.xls)|*.xlsx;*.xls|所有文件 (*.*)|*.*",
            CheckFileExists = true,
            Multiselect = false
        };
        return Task.FromResult(dialog.ShowDialog(GetOwner()) == true ? dialog.FileName : null);
    }

    public Task<string?> PickFolderAsync(string title)
    {
        var dialog = new OpenFolderDialog { Title = title, Multiselect = false };
        return Task.FromResult(dialog.ShowDialog(GetOwner()) == true ? dialog.FolderName : null);
    }

    public Task<IReadOnlyList<string>?> SelectSheetsAsync(IReadOnlyList<SheetInfo> sheets, IReadOnlyCollection<string>? previousSelection = null)
    {
        var dialog = new SheetSelectionDialog(sheets, previousSelection) { Owner = GetOwner() };
        return Task.FromResult<IReadOnlyList<string>?>(dialog.ShowDialog() == true ? dialog.SelectedSheets : null);
    }

    public Task<bool> ConfirmAsync(string title, string message, string confirmText = "确认")
    {
        var dialog = new MessageDialogWindow(title, message, confirmText, showCancel: true) { Owner = GetOwner() };
        return Task.FromResult(dialog.ShowDialog() == true);
    }

    public Task ShowMessageAsync(string title, string message)
    {
        new MessageDialogWindow(title, message, "知道了", showCancel: false) { Owner = GetOwner() }.ShowDialog();
        return Task.CompletedTask;
    }

    public Task ShowErrorSummaryAsync(string title, string summary, IReadOnlyList<QuestionBankIssue> issues)
    {
        new ErrorSummaryDialog(title, summary, issues) { Owner = GetOwner() }.ShowDialog();
        return Task.CompletedTask;
    }

    private static Window? GetOwner() => Application.Current.MainWindow is { IsVisible: true } window ? window : null;
}
