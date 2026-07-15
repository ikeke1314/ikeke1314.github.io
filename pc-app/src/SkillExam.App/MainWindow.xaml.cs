using System.ComponentModel;
using SkillExam.App.ViewModels;
using Wpf.Ui.Controls;

namespace SkillExam.App;

public partial class MainWindow : FluentWindow
{
    private bool _shutdownPrepared;

    public MainWindow()
    {
        InitializeComponent();
        Closing += OnClosingAsync;
    }

    private async void OnClosingAsync(object? sender, CancelEventArgs eventArgs)
    {
        if (_shutdownPrepared || DataContext is not MainViewModel viewModel)
        {
            return;
        }
        eventArgs.Cancel = true;
        IsEnabled = false;
        try
        {
            await viewModel.PrepareForShutdownAsync();
        }
        finally
        {
            _shutdownPrepared = true;
            // Closing 事件内同步再次调用 Close 会触发 WPF 的重入保护。
            // 投递到调度器，确保原始关闭事件已经返回后再真正关闭窗口。
            await Dispatcher.InvokeAsync(Close, System.Windows.Threading.DispatcherPriority.ContextIdle);
        }
    }
}
