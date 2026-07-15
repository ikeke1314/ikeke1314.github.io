using System.Windows;
using System.Windows.Threading;
using Microsoft.Extensions.DependencyInjection;
using Serilog;
using SkillExam.App.Services;
using SkillExam.App.ViewModels;
using SkillExam.Core.Abstractions;
using SkillExam.Core.Exam;
using SkillExam.Infrastructure.Excel;
using SkillExam.Infrastructure.Logging;
using SkillExam.Infrastructure.Migration;
using SkillExam.Infrastructure.Persistence;
using SkillExam.Infrastructure.Speech;

namespace SkillExam.App;

public partial class App : Application
{
    private ServiceProvider? _services;
    private Serilog.ILogger? _logger;

    protected override async void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        var paths = new AppDataPaths();
        paths.EnsureCreated();
        _logger = LoggingConfigurator.CreateLogger(paths);
        DispatcherUnhandledException += OnDispatcherUnhandledException;
        AppDomain.CurrentDomain.UnhandledException += OnUnhandledException;

        try
        {
            var database = new SqliteDatabase(paths.DatabasePath);
            await database.InitializeAsync();
            var services = new ServiceCollection();
            services.AddSingleton(paths);
            services.AddSingleton(database);
            services.AddSingleton(_logger);
            services.AddSingleton<QuestionBankState>();
            services.AddSingleton<IQuestionBankReader, ExcelQuestionBankReader>();
            services.AddSingleton<IExamGenerator, ExamGenerator>();
            services.AddSingleton<IAnswerEvaluator, AnswerEvaluator>();
            services.AddSingleton<IProgressRepository, ProgressRepository>();
            services.AddSingleton<IErrorBookRepository, ErrorBookRepository>();
            services.AddSingleton<ISettingsRepository, SettingsRepository>();
            services.AddSingleton<ISpeechService, SystemSpeechService>();
            services.AddSingleton<IClock, SystemClock>();
            services.AddSingleton<IDialogService, WpfDialogService>();
            services.AddSingleton<DatabaseBackupService>();
            services.AddSingleton(provider => new LegacyDataMigrator(
                provider.GetRequiredService<SqliteDatabase>(),
                provider.GetRequiredService<AppDataPaths>(),
                provider.GetRequiredService<IClock>()));
            services.AddSingleton<MainViewModel>();
            _services = services.BuildServiceProvider();

            var window = new MainWindow { DataContext = _services.GetRequiredService<MainViewModel>() };
            MainWindow = window;
            ShutdownMode = ShutdownMode.OnMainWindowClose;
            window.Show();
            await ((MainViewModel)window.DataContext).InitializeAsync();
        }
        catch (Exception exception)
        {
            _logger.Error(exception, "应用启动失败");
            MessageBox.Show(
                "应用启动失败，用户数据未被删除。请查看 LocalAppData 下的日志。",
                "技能士考试刷题系统",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
            Shutdown(-1);
        }
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _services?.Dispose();
        (_logger as IDisposable)?.Dispose();
        base.OnExit(e);
    }

    private void OnDispatcherUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
    {
        _logger?.Error(e.Exception, "UI 线程发生未处理异常");
        e.Handled = true;
        MessageBox.Show("操作未能完成，技术详情已写入日志。", "发生错误", MessageBoxButton.OK, MessageBoxImage.Error);
    }

    private void OnUnhandledException(object? sender, UnhandledExceptionEventArgs e)
    {
        _logger?.Fatal(e.ExceptionObject as Exception, "进程发生未处理异常，正在终止：{IsTerminating}", e.IsTerminating);
    }
}
