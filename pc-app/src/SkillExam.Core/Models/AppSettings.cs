namespace SkillExam.Core.Models;

public sealed record AppSettings
{
    public string? LastQuestionBankPath { get; init; }
    public IReadOnlyList<string> SelectedSheets { get; init; } = [];
    public bool AutoSpeechEnabled { get; init; } = true;
    public string? VoiceName { get; init; }
    public int SpeechRate { get; init; }
    public bool ReduceMotion { get; init; }
}

public sealed record SpeechVoice(string Name, string Culture, bool IsChinese);
