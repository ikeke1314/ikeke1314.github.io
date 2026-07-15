import { CATEGORIES, type Category, type Question } from '../types';

const CATEGORY_KEYWORDS: Record<Exclude<Category, '全部'>, string[]> = {
  基站: ['基站', '普朗克'],
  地宝: ['地宝'],
  窗宝: ['窗宝'],
  光学组件: ['光学组件'],
};

export function isCategory(value: string): value is Category {
  return CATEGORIES.includes(value as Category);
}

export function matchesCategory(question: Pick<Question, 'source' | 'sheetName'>, category: Category): boolean {
  if (category === '全部') return true;
  const haystack = `${question.source} ${question.sheetName}`;
  return CATEGORY_KEYWORDS[category].some((keyword) => haystack.includes(keyword));
}

export function filterByCategory(questions: Question[], category: Category): Question[] {
  return questions.filter((question) => matchesCategory(question, category));
}
