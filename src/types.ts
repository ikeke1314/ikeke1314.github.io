export const QUESTION_TYPES = ['单选题', '多选题', '判断题', '简答题'] as const;
export const LEVELS = ['一级', '二级', '三级', '四级', '五级', '六级'] as const;
export const CATEGORIES = ['全部', '基站', '地宝', '窗宝', '光学组件'] as const;

export type QuestionType = (typeof QUESTION_TYPES)[number];
export type Level = (typeof LEVELS)[number];
export type Category = (typeof CATEGORIES)[number];
export type OptionKey = 'A' | 'B' | 'C' | 'D' | 'E';

export interface Question {
  id: string;
  type: QuestionType;
  question: string;
  options: Partial<Record<OptionKey, string>>;
  answer: string;
  levels: Level[];
  source: string;
  sheetName: string;
}

export interface QuestionBank {
  questions: Question[];
  levels: Level[];
  sources: string[];
}

export interface AnswerState {
  value: string;
  confirmed: boolean;
}

export interface QuestionResult {
  question: Question;
  userAnswer: string;
  isCorrect: boolean;
  points: number;
}

export interface ExamResult {
  score: number;
  maxScore: number;
  passed: boolean;
  details: QuestionResult[];
}
