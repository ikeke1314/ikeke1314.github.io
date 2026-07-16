namespace SkillExam.Core.Models;

public sealed record AppSettings
{
    public const int DefaultAutoNextDelayMilliseconds = 1000;
    public const int MinimumAutoNextDelayMilliseconds = 0;
    public const int MaximumAutoNextDelayMilliseconds = 3000;
    public const int CurrentAutoNextDelaySettingsVersion = 1;

    public string? LastQuestionBankPath { get; init; }
    public IReadOnlyList<string> SelectedSheets { get; init; } = [];
    public bool AutoSpeechEnabled { get; init; } = true;
    public string? VoiceName { get; init; }
    public int SpeechRate { get; init; }
    public int AutoNextDelayMilliseconds { get; init; } = DefaultAutoNextDelayMilliseconds;
    public int AutoNextDelaySettingsVersion { get; init; } = CurrentAutoNextDelaySettingsVersion;
    public bool ReduceMotion { get; init; }

    public static int NormalizeAutoNextDelayMilliseconds(int value) =>
        Math.Clamp(value, MinimumAutoNextDelayMilliseconds, MaximumAutoNextDelayMilliseconds);
}

public sealed record SpeechVoice(string Name, string Culture, bool IsChinese);
