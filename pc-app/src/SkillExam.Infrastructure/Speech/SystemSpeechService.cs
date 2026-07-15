using System.Globalization;
using System.Speech.Synthesis;
using SkillExam.Core.Abstractions;
using SkillExam.Core.Models;

namespace SkillExam.Infrastructure.Speech;

public sealed class SystemSpeechService : ISpeechService
{
    private readonly SpeechSynthesizer _synthesizer = new();
    private readonly object _sync = new();
    private TaskCompletionSource? _currentCompletion;
    private Prompt? _currentPrompt;
    private bool _disposed;

    public SystemSpeechService()
    {
        _synthesizer.SpeakCompleted += OnSpeakCompleted;
    }

    public IReadOnlyList<SpeechVoice> GetVoices()
    {
        lock (_sync)
        {
            ThrowIfDisposed();
            return _synthesizer.GetInstalledVoices()
                .Where(voice => voice.Enabled)
                .Select(voice => new SpeechVoice(
                    voice.VoiceInfo.Name,
                    voice.VoiceInfo.Culture.Name,
                    voice.VoiceInfo.Culture.TwoLetterISOLanguageName.Equals("zh", StringComparison.OrdinalIgnoreCase)))
                .ToArray();
        }
    }

    public bool IsChineseVoiceAvailable => GetVoices().Any(voice => voice.IsChinese);

    public async Task SpeakAsync(string text, string? voiceName, int rate, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return;
        }

        Task completionTask;
        lock (_sync)
        {
            ThrowIfDisposed();
            CancelCore();
            SelectVoice(voiceName);
            _synthesizer.Rate = Math.Clamp(rate, -10, 10);
            _currentCompletion = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            _currentPrompt = _synthesizer.SpeakAsync(text);
            completionTask = _currentCompletion.Task;
        }

        using var registration = cancellationToken.Register(Cancel);
        await completionTask.WaitAsync(cancellationToken);
    }

    public void Cancel()
    {
        lock (_sync)
        {
            if (!_disposed)
            {
                CancelCore();
            }
        }
    }

    public void Dispose()
    {
        lock (_sync)
        {
            if (_disposed)
            {
                return;
            }
            CancelCore();
            _synthesizer.SpeakCompleted -= OnSpeakCompleted;
            _synthesizer.Dispose();
            _disposed = true;
        }
    }

    private void SelectVoice(string? requestedVoice)
    {
        var voices = _synthesizer.GetInstalledVoices().Where(voice => voice.Enabled).ToArray();
        var selected = !string.IsNullOrWhiteSpace(requestedVoice)
            ? voices.FirstOrDefault(voice => voice.VoiceInfo.Name.Equals(requestedVoice, StringComparison.OrdinalIgnoreCase))
            : null;
        selected ??= voices.FirstOrDefault(voice => voice.VoiceInfo.Culture.Name.Equals("zh-CN", StringComparison.OrdinalIgnoreCase));
        selected ??= voices.FirstOrDefault(voice => voice.VoiceInfo.Culture.TwoLetterISOLanguageName.Equals("zh", StringComparison.OrdinalIgnoreCase));
        if (selected is not null)
        {
            _synthesizer.SelectVoice(selected.VoiceInfo.Name);
        }
    }

    private void CancelCore()
    {
        _synthesizer.SpeakAsyncCancelAll();
        _currentPrompt = null;
        _currentCompletion?.TrySetCanceled();
        _currentCompletion = null;
    }

    private void OnSpeakCompleted(object? sender, SpeakCompletedEventArgs eventArgs)
    {
        lock (_sync)
        {
            if (_currentPrompt is null || eventArgs.Prompt != _currentPrompt)
            {
                return;
            }
            var completion = _currentCompletion;
            _currentPrompt = null;
            _currentCompletion = null;
            if (eventArgs.Error is not null)
            {
                completion?.TrySetException(eventArgs.Error);
            }
            else if (eventArgs.Cancelled)
            {
                completion?.TrySetCanceled();
            }
            else
            {
                completion?.TrySetResult();
            }
        }
    }

    private void ThrowIfDisposed() => ObjectDisposedException.ThrowIf(_disposed, this);
}
