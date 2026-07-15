using System.Windows;

namespace SkillExam.App.Views;

public partial class MessageDialogWindow : Window
{
    public MessageDialogWindow(string title, string message, string confirmText, bool showCancel)
    {
        InitializeComponent();
        DialogTitle = title;
        Message = message;
        ConfirmButton.Content = confirmText;
        CancelButton.Visibility = showCancel ? Visibility.Visible : Visibility.Collapsed;
        DataContext = this;
    }

    public string DialogTitle { get; }
    public string Message { get; }
    private void ConfirmButton_OnClick(object sender, RoutedEventArgs e) => DialogResult = true;
    private void CancelButton_OnClick(object sender, RoutedEventArgs e) => DialogResult = false;
}
