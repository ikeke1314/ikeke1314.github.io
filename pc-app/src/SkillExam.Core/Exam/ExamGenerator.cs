using SkillExam.Core.Abstractions;
using SkillExam.Core.Models;

namespace SkillExam.Core.Exam;

public sealed class ExamGenerator : IExamGenerator
{
    private static readonly IReadOnlyDictionary<SkillLevel, IReadOnlyDictionary<SkillLevel, decimal>> LevelWeights =
        new Dictionary<SkillLevel, IReadOnlyDictionary<SkillLevel, decimal>>
        {
            [SkillLevel.Level1] = Weights((SkillLevel.Level1, 0.8m), (SkillLevel.Level2, 0.2m)),
            [SkillLevel.Level2] = Weights((SkillLevel.Level1, 0.1m), (SkillLevel.Level2, 0.7m), (SkillLevel.Level3, 0.2m)),
            [SkillLevel.Level3] = Weights((SkillLevel.Level2, 0.1m), (SkillLevel.Level3, 0.7m), (SkillLevel.Level4, 0.2m)),
            [SkillLevel.Level4] = Weights((SkillLevel.Level3, 0.1m), (SkillLevel.Level4, 0.7m), (SkillLevel.Level5, 0.2m)),
            [SkillLevel.Level5] = Weights((SkillLevel.Level4, 0.1m), (SkillLevel.Level5, 0.7m), (SkillLevel.Level6, 0.2m)),
            [SkillLevel.Level6] = Weights((SkillLevel.Level5, 0.1m), (SkillLevel.Level6, 0.9m))
        };

    public IReadOnlyDictionary<SkillLevel, decimal> GetWeights(SkillLevel targetLevel) =>
        LevelWeights.TryGetValue(targetLevel, out var weights)
            ? weights
            : throw new ArgumentOutOfRangeException(nameof(targetLevel));

    public IReadOnlyDictionary<QuestionType, IReadOnlyDictionary<SkillLevel, int>> GetDistribution(SkillLevel targetLevel)
    {
        var weights = GetWeights(targetLevel);
        return ExamBlueprint.QuestionCounts.ToDictionary(
            pair => pair.Key,
            pair => Allocate(pair.Value, targetLevel, weights));
    }

    public ExamGenerationResult Generate(IReadOnlyList<Question> questionBank, ExamRequest request)
    {
        ArgumentNullException.ThrowIfNull(questionBank);
        var random = request.Seed is int seed ? new Random(seed) : new Random();
        var filtered = QuestionFilter.Apply(questionBank, categories: request.Categories, sources: request.Sources);
        var distribution = GetDistribution(request.TargetLevel);
        var selected = new List<Question>(ExamBlueprint.TotalQuestions);
        var shortages = new List<QuestionShortage>();

        foreach (var type in Enum.GetValues<QuestionType>())
        {
            var candidates = filtered.Where(question => question.Type == type).ToArray();
            var assignment = AssignUniqueQuestions(candidates, distribution[type], random);
            selected.AddRange(assignment.Selected);
            shortages.AddRange(assignment.Shortages.Select(shortage =>
                new QuestionShortage(type, shortage.Level, shortage.Required, shortage.Available)));
        }

        if (shortages.Count > 0)
        {
            return new ExamGenerationResult(null, shortages);
        }

        // 保持题型分组，组内随机，便于左侧导航按题型展示。
        var ordered = Enum.GetValues<QuestionType>()
            .SelectMany(type => Shuffle(selected.Where(question => question.Type == type).ToArray(), random))
            .ToArray();
        var paper = new ExamPaper(
            request.TargetLevel,
            ordered,
            distribution,
            ExamBlueprint.MaximumScore,
            ExamBlueprint.Duration);
        return new ExamGenerationResult(paper, []);
    }

    private static AssignmentResult AssignUniqueQuestions(
        IReadOnlyList<Question> questions,
        IReadOnlyDictionary<SkillLevel, int> required,
        Random random)
    {
        var slots = required.SelectMany(pair => Enumerable.Repeat(pair.Key, pair.Value))
            .OrderBy(level => questions.Count(question => question.Levels.Contains(level)))
            .ToArray();
        var slotCandidates = slots.Select(level => Shuffle(
                questions.Select((question, index) => (question, index))
                    .Where(pair => pair.question.Levels.Contains(level))
                    .Select(pair => pair.index)
                    .ToArray(), random))
            .ToArray();
        var slotToQuestion = Enumerable.Repeat(-1, slots.Length).ToArray();
        var questionToSlot = Enumerable.Repeat(-1, questions.Count).ToArray();

        bool TryAssign(int slot, HashSet<int> visitedQuestions, HashSet<int> visitedSlots)
        {
            if (!visitedSlots.Add(slot))
            {
                return false;
            }

            foreach (var questionIndex in slotCandidates[slot])
            {
                if (!visitedQuestions.Add(questionIndex))
                {
                    continue;
                }

                var previousSlot = questionToSlot[questionIndex];
                if (previousSlot < 0 || TryAssign(previousSlot, visitedQuestions, visitedSlots))
                {
                    slotToQuestion[slot] = questionIndex;
                    questionToSlot[questionIndex] = slot;
                    return true;
                }
            }

            return false;
        }

        for (var slot = 0; slot < slots.Length; slot++)
        {
            TryAssign(slot, [], []);
        }

        var shortages = required.Select(pair =>
        {
            var matched = slots.Select((level, index) => (level, index))
                .Count(pair2 => pair2.level == pair.Key && slotToQuestion[pair2.index] >= 0);
            var rawAvailable = questions.Count(question => question.Levels.Contains(pair.Key));
            return new AllocationShortage(pair.Key, pair.Value, Math.Min(rawAvailable, matched));
        }).Where(shortage => shortage.Available < shortage.Required).ToArray();

        var selected = slotToQuestion.Where(index => index >= 0).Select(index => questions[index]).DistinctBy(question => question.Id).ToArray();
        return new AssignmentResult(selected, shortages);
    }

    private static IReadOnlyDictionary<SkillLevel, int> Allocate(
        int total,
        SkillLevel targetLevel,
        IReadOnlyDictionary<SkillLevel, decimal> weights)
    {
        var result = weights.ToDictionary(pair => pair.Key, pair => (int)Math.Floor(total * pair.Value));
        result[targetLevel] += total - result.Values.Sum();
        return result;
    }

    private static IReadOnlyDictionary<SkillLevel, decimal> Weights(params (SkillLevel Level, decimal Weight)[] values) =>
        values.ToDictionary(pair => pair.Level, pair => pair.Weight);

    private static T[] Shuffle<T>(T[] values, Random random)
    {
        for (var index = values.Length - 1; index > 0; index--)
        {
            var target = random.Next(index + 1);
            (values[index], values[target]) = (values[target], values[index]);
        }
        return values;
    }

    private sealed record AllocationShortage(SkillLevel Level, int Required, int Available);
    private sealed record AssignmentResult(IReadOnlyList<Question> Selected, IReadOnlyList<AllocationShortage> Shortages);
}
