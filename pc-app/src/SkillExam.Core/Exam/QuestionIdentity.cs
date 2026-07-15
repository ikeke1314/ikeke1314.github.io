using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using SkillExam.Core.Models;

namespace SkillExam.Core.Exam;

public static partial class QuestionIdentity
{
    public static string Create(
        QuestionType type,
        string text,
        IReadOnlyDictionary<string, string> options,
        string answer,
        string source)
    {
        var normalizedOptions = string.Join("|", options
            .OrderBy(pair => pair.Key, StringComparer.OrdinalIgnoreCase)
            .Select(pair => $"{Normalize(pair.Key).ToUpperInvariant()}={Normalize(pair.Value)}"));
        var canonical = string.Join("\n", type, Normalize(text), normalizedOptions, Normalize(answer).ToUpperInvariant(), Normalize(source));
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(canonical))).ToLowerInvariant();
    }

    public static string Normalize(string? value) => WhitespaceRegex().Replace(value?.Trim() ?? string.Empty, " ");

    [GeneratedRegex(@"\s+")]
    private static partial Regex WhitespaceRegex();
}
