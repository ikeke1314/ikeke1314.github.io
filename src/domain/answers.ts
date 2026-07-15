import type { Question } from '../types';

const TRUE_VALUES = new Set(['√', '对', '正确', 'TRUE', 'T', 'YES', 'Y', '1']);
const FALSE_VALUES = new Set(['×', '错', '错误', 'FALSE', 'F', 'NO', 'N', '0']);

function clean(value: unknown): string {
  return String(value ?? '').trim().replace(/\s+/g, ' ');
}

export function normalizeMultiAnswer(value: unknown): string {
  return [...new Set(clean(value).toUpperCase().match(/[A-E]/g) ?? [])].sort().join('');
}

export function normalizeJudgeAnswer(value: unknown, question?: Pick<Question, 'options'>): string {
  let normalized = clean(value).toUpperCase();
  if (question && /^[A-E]$/.test(normalized)) {
    normalized = clean(question.options[normalized as keyof Question['options']]).toUpperCase();
  }
  if (TRUE_VALUES.has(normalized)) return '√';
  if (FALSE_VALUES.has(normalized) || normalized === 'X') return '×';
  return normalized;
}

export function normalizeAnswer(question: Pick<Question, 'type' | 'options'>, value: unknown): string {
  if (question.type === '多选题') return normalizeMultiAnswer(value);
  if (question.type === '判断题') return normalizeJudgeAnswer(value, question);
  return clean(value).toUpperCase();
}

export function isAnswerCorrect(question: Question, userAnswer: unknown): boolean {
  return normalizeAnswer(question, userAnswer) === normalizeAnswer(question, question.answer);
}
