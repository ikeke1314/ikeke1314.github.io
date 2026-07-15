import { filterByCategory } from './categories';
import { isAnswerCorrect } from './answers';
import type { Category, ExamResult, Level, Question, QuestionType } from '../types';

export const EXAM_COUNTS: Record<QuestionType, number> = {
  单选题: 40,
  多选题: 10,
  判断题: 10,
  简答题: 3,
};

export const QUESTION_SCORES: Record<QuestionType, number> = {
  单选题: 1,
  多选题: 2,
  判断题: 1,
  简答题: 10,
};

export const LEVEL_WEIGHTS: Record<Level, Partial<Record<Level, number>>> = {
  一级: { 一级: 0.8, 二级: 0.2 },
  二级: { 一级: 0.1, 二级: 0.7, 三级: 0.2 },
  三级: { 二级: 0.1, 三级: 0.7, 四级: 0.2 },
  四级: { 三级: 0.1, 四级: 0.7, 五级: 0.2 },
  五级: { 四级: 0.1, 五级: 0.7, 六级: 0.2 },
  六级: { 五级: 0.1, 六级: 0.9 },
};

export type RandomSource = () => number;

export function shuffle<T>(items: readonly T[], random: RandomSource = Math.random): T[] {
  const result = [...items];
  for (let index = result.length - 1; index > 0; index -= 1) {
    const target = Math.floor(random() * (index + 1));
    [result[index], result[target]] = [result[target], result[index]];
  }
  return result;
}

export function getWeightedCounts(level: Level, total: number): Partial<Record<Level, number>> {
  const weights = LEVEL_WEIGHTS[level];
  const counts: Partial<Record<Level, number>> = {};
  let assigned = 0;
  Object.entries(weights).forEach(([weightedLevel, weight]) => {
    const count = Math.floor(total * weight);
    counts[weightedLevel as Level] = count;
    assigned += count;
  });
  counts[level] = (counts[level] ?? 0) + total - assigned;
  return counts;
}

export function generateExam(
  questionBank: Question[],
  level: Level,
  category: Category,
  random: RandomSource = Math.random,
): Question[] {
  const categoryPool = filterByCategory(questionBank, category);
  const weightedLevels = Object.keys(LEVEL_WEIGHTS[level]) as Level[];
  const selectedIds = new Set<string>();
  const exam: Question[] = [];

  (Object.entries(EXAM_COUNTS) as [QuestionType, number][]).forEach(([type, total]) => {
    const typeSelection: Question[] = [];
    const targets = getWeightedCounts(level, total);
    weightedLevels.forEach((weightedLevel) => {
      const candidates = categoryPool.filter(
        (question) => question.type === type && question.levels.includes(weightedLevel) && !selectedIds.has(question.id),
      );
      shuffle(candidates, random)
        .slice(0, targets[weightedLevel] ?? 0)
        .forEach((question) => {
          selectedIds.add(question.id);
          typeSelection.push(question);
        });
    });

    // 某个等级库存不足时，先从同一权重等级池补足，仍保持题型和去重约束。
    const shortage = total - typeSelection.length;
    if (shortage > 0) {
      const fallback = categoryPool.filter(
        (question) =>
          question.type === type &&
          question.levels.some((candidateLevel) => weightedLevels.includes(candidateLevel)) &&
          !selectedIds.has(question.id),
      );
      shuffle(fallback, random)
        .slice(0, shortage)
        .forEach((question) => {
          selectedIds.add(question.id);
          typeSelection.push(question);
        });
    }
    exam.push(...shuffle(typeSelection, random));
  });
  return exam;
}

export function scoreExam(questions: Question[], answers: Record<string, string>): ExamResult {
  const details = questions.map((question) => {
    const isCorrect = isAnswerCorrect(question, answers[question.id] ?? '');
    return {
      question,
      userAnswer: answers[question.id] ?? '',
      isCorrect,
      points: isCorrect ? QUESTION_SCORES[question.type] : 0,
    };
  });
  const score = details.reduce((sum, detail) => sum + detail.points, 0);
  const maxScore = questions.reduce((sum, question) => sum + QUESTION_SCORES[question.type], 0);
  return { score, maxScore, passed: maxScore > 0 && score >= maxScore * 0.8, details };
}
