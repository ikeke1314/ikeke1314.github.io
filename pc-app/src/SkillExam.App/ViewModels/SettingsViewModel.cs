using System.Collections.ObjectModel;
using System.Diagnostics;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Serilog;
using SkillExam.App.Services;
using SkillExam.Core.Abstractions;
using SkillExam.Core.Models;
using SkillExam.Infrastructure.Persistence;

namespace SkillExam.App.ViewModels;

public sealed partial class SettingsViewModel : ObservableObject
{
    private readonly ISettingsRepository _repository;
    private readonly ISpeechService _speechService;
    private readonly DatabaseBackupService _backupService;
    private readonly AppDataPaths _paths;
    private readonly IDialogService _dialogs;
    private readonly ILogger _logger;
    private readonly Func<AppSettings, Task> _saved;
    private readonly Action _back;
    private readonly AppSettings _original;

    public SettingsViewModel(
        AppSettings settings,
        ISettingsRepository repository,
        ISpeechService speechService,
        DatabaseBackupService backupService,
        AppDataPaths paths,
        IDialogService dialogs,
        ILogger logger,
        Func<AppSettings, Task> saved,
        Action back)
    {
        _original = settings;
        _repository = repository;
        _speechService = speechService;
        _backupService = backupService;
        _paths = paths;
        _dialogs = dialogs;
        _logger = logger;
        _saved = saved;
        _back = back;
        AutoSpeechEnabled = settings.AutoSpeechEnabled;
        VoiceName = settings.VoiceName;
        SpeechRate = settings.SpeechRate;
        AutoNextDelaySeconds = AppSettings.NormalizeAutoNextDelayMilliseconds(settings.AutoNextDelayMilliseconds) / 1000d;
        ReduceMotion = settings.ReduceMotion;
        Voices = new ObservableCollection<SpeechVoice>(_speechService.GetVoices());
        if (string.IsNullOrWhiteSpace(VoiceName))
        {
            VoiceName = Voices.FirstOrDefault(voice => voice.IsChinese)?.Name ?? Voices.FirstOrDefault()?.Name;
        }
    }

    public ObservableCollection<SpeechVoice> Voices { get; }
    public string DataDirectory => _paths.RootDirectory;
    public string VoiceWarning => _speechService.IsChineseVoiceAvailable
        ? "已优先选择中文语音。"
        : "未检测到中文语音，将使用系统默认语音。";
    public string AutoNextDelayDisplay => $"{AutoNextDelaySeconds:0.0} 秒";

    [ObservableProperty] private bool _autoSpeechEnabled;
    [ObservableProperty] private string? _voiceName;
    [ObservableProperty] private int _speechRate;
    [ObservableProperty] private double _autoNextDelaySeconds;
    [ObservableProperty] private bool _reduceMotion;
    [ObservableProperty] private bool _isBusy;

    partial void OnAutoNextDelaySecondsChanged(double value) => OnPropertyChanged(nameof(AutoNextDelayDisplay));

    [RelayCommand]
    private async Task SaveAsync()
    {
        try
        {
            var settings = _original with
            {
                AutoSpeechEnabled = AutoSpeechEnabled,
                VoiceName = VoiceName,
                SpeechRate = Math.Clamp(SpeechRate, -10, 10),
                AutoNextDelayMilliseconds = AppSettings.NormalizeAutoNextDelayMilliseconds(
                    (int)Math.Round(AutoNextDelaySeconds * 1000, MidpointRounding.AwayFromZero)),
                ReduceMotion = ReduceMotion
            };
            await _repository.SaveAsync(settings);
            await _saved(settings);
            _back();
        }
        catch (Exception exception)
        {
            _logger.Error(exception, "保存设置失败");
            await _dialogs.ShowMessageAsync("保存失败", "设置未能保存，请查看日志后重试。");
        }
    }

    [RelayCommand]
    private void OpenDataDirectory()
    {
        _paths.EnsureCreated();
        Process.Start(new ProcessStartInfo("explorer.exe", _paths.RootDirectory) { UseShellExecute = true });
    }

    [RelayCommand]
    private async Task BackupAsync()
    {
        IsBusy = true;
        try
        {
            var path = await _backupService.BackupAsync();
            await _dialogs.ShowMessageAsync("备份完成", $"数据库已备份到：\n{path}");
        }
        catch (Exception exception)
        {
            _logger.Error(exception, "备份数据库失败");
            await _dialogs.ShowMessageAsync("备份失败", "数据库未能备份，请查看日志后重试。");
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private async Task ResetAsync()
    {
        if (!await _dialogs.ConfirmAsync("恢复默认设置", "将恢复自动读题、默认语速、跳题延迟和动画设置，题库路径与用户数据不会删除。", "恢复"))
        {
            return;
        }
        var defaults = new AppSettings
        {
            LastQuestionBankPath = _original.LastQuestionBankPath,
            SelectedSheets = _original.SelectedSheets
        };
        AutoSpeechEnabled = defaults.AutoSpeechEnabled;
        VoiceName = Voices.FirstOrDefault(voice => voice.IsChinese)?.Name ?? Voices.FirstOrDefault()?.Name;
        SpeechRate = defaults.SpeechRate;
        AutoNextDelaySeconds = defaults.AutoNextDelayMilliseconds / 1000d;
        ReduceMotion = defaults.ReduceMotion;
    }

    [RelayCommand]
    private void Back() => _back();
}
