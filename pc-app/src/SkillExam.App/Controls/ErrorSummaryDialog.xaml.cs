using System.Windows;
using SkillExam.Core.Models;

namespace SkillExam.App.Controls;

public partial class ErrorSummaryDialog : Window
{
    public ErrorSummaryDialog(string title, string summary, IReadOnlyList<QuestionBankIssue> issues)
    {
        InitializeComponent();
        DialogTitle = title;
        Summary = summary;
        Issues = issues;
        DataContext = this;
    }
    public string DialogTitle { get; }
    public string Summary { get; }
    public IReadOnlyList<QuestionBankIssue> Issues { get; }
    private void Close_OnClick(object sender, RoutedEventArgs e) => Close();
}
