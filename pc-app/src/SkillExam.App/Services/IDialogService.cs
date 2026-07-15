using SkillExam.Core.Models;

namespace SkillExam.App.Services;

public interface IDialogService
{
    Task<string?> PickQuestionBankAsync();
    Task<string?> PickFolderAsync(string title);
    Task<IReadOnlyList<string>?> SelectSheetsAsync(IReadOnlyList<SheetInfo> sheets, IReadOnlyCollection<string>? previousSelection = null);
    Task<bool> ConfirmAsync(string title, string message, string confirmText = "确认");
    Task ShowMessageAsync(string title, string message);
    Task ShowErrorSummaryAsync(string title, string summary, IReadOnlyList<QuestionBankIssue> issues);
}
