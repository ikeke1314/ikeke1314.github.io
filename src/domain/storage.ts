import type { AnswerState, Category, Level, Question, QuestionType } from '../types';
import { CATEGORIES, LEVELS, QUESTION_TYPES } from '../types';
import { createQuestionId } from './question-id';

export const STORAGE_VERSION = 2;
export const STORAGE_KEYS = {
  errorBook: 'skill_exam_error_book',
  practiceProgress: 'skill_exam_practice_progress',
  settings: 'skill_exam_settings',
} as const;

interface Versioned<T> {
  version: typeof STORAGE_VERSION;
  data: T;
}

export interface Settings {
  lastLevel: Level | '';
  lastCategory: Category;
}

export interface ErrorBookEntry {
  id: string;
  type: QuestionType;
  question: string;
  options: Question['options'];
  answer: string;
  levels: Level[];
  source: string;
  sheetName: string;
  userAnswer: string;
  timestamp: string;
}

export interface PracticeProgress {
  type: QuestionType;
  levels: Level[];
  category: Category;
  questionIds: string[];
  index: number;
  answers: Record<string, AnswerState>;
  timestamp: string;
}

export interface StorageLike {
  getItem(key: string): string | null;
  setItem(key: string, value: string): void;
  removeItem(key: string): void;
}

const DEFAULT_SETTINGS: Settings = { lastLevel: '', lastCategory: '全部' };

function storageOrDefault(storage?: StorageLike): StorageLike {
  return storage ?? window.localStorage;
}

function parseRaw(storage: StorageLike, key: string): unknown {
  try {
    const value = storage.getItem(key);
    return value ? JSON.parse(value) : null;
  } catch {
    return null;
  }
}

function isVersioned<T>(value: unknown): value is Versioned<T> {
  return Boolean(value && typeof value === 'object' && (value as Versioned<T>).version === STORAGE_VERSION && 'data' in value);
}

function saveVersioned<T>(storage: StorageLike, key: string, data: T): boolean {
  try {
    storage.setItem(key, JSON.stringify({ version: STORAGE_VERSION, data } satisfies Versioned<T>));
    return true;
  } catch {
    return false;
  }
}

function legacyQuestion(value: unknown): Question | null {
  if (!value || typeof value !== 'object') return null;
  const item = value as Record<string, unknown>;
  const type = String(item.type ?? '') as QuestionType;
  const question = String(item.question ?? '').trim();
  const answer = String(item.answer ?? '').trim();
  if (!QUESTION_TYPES.includes(type) || !question || !answer) return null;
  const options = (item.options && typeof item.options === 'object' ? item.options : {}) as Question['options'];
  const levels = Array.isArray(item.levels)
    ? item.levels.filter((level): level is Level => LEVELS.includes(level as Level))
    : [];
  const source = String(item.source ?? item.source_sheet ?? '未知来源');
  const sheetName = String(item.sheetName ?? item.source_sheet ?? source);
  return {
    id: createQuestionId({ type, question, options, answer }),
    type,
    question,
    options,
    answer,
    levels,
    source,
    sheetName,
  };
}

export function getSettings(storage?: StorageLike): Settings {
  const target = storageOrDefault(storage);
  const raw = parseRaw(target, STORAGE_KEYS.settings);
  const candidate = isVersioned<unknown>(raw) ? raw.data : raw;
  const values = candidate && typeof candidate === 'object' ? (candidate as Record<string, unknown>) : {};
  const lastLevel = LEVELS.includes(values.lastLevel as Level) ? (values.lastLevel as Level) : '';
  const lastCategory = CATEGORIES.includes(values.lastCategory as Category)
    ? (values.lastCategory as Category)
    : '全部';
  const settings: Settings = { lastLevel, lastCategory };
  saveVersioned(target, STORAGE_KEYS.settings, settings);
  return settings;
}

export function saveSettings(settings: Settings, storage?: StorageLike): boolean {
  return saveVersioned(storageOrDefault(storage), STORAGE_KEYS.settings, settings);
}

export function getErrorBook(storage?: StorageLike): ErrorBookEntry[] {
  const target = storageOrDefault(storage);
  const raw = parseRaw(target, STORAGE_KEYS.errorBook);
  const candidate = isVersioned<unknown>(raw) ? raw.data : raw;
  const migrated = (Array.isArray(candidate) ? candidate : [])
    .map((item) => {
      const question = legacyQuestion(item);
      if (!question) return null;
      const legacy = item as Record<string, unknown>;
      return {
        ...question,
        userAnswer: String(legacy.userAnswer ?? ''),
        timestamp: String(legacy.timestamp ?? new Date(0).toISOString()),
      } satisfies ErrorBookEntry;
    })
    .filter((entry): entry is ErrorBookEntry => entry !== null)
    .filter((entry, index, entries) => entries.findIndex((candidate) => candidate.id === entry.id) === index);
  saveVersioned(target, STORAGE_KEYS.errorBook, migrated);
  return migrated;
}

export function addError(question: Question, userAnswer: string, storage?: StorageLike): boolean {
  const target = storageOrDefault(storage);
  const entries = getErrorBook(target);
  const existingIndex = entries.findIndex((entry) => entry.id === question.id);
  const nextEntry: ErrorBookEntry = { ...question, userAnswer, timestamp: new Date().toISOString() };
  if (existingIndex >= 0) entries[existingIndex] = nextEntry;
  else entries.push(nextEntry);
  return saveVersioned(target, STORAGE_KEYS.errorBook, entries);
}

export function clearErrorBook(storage?: StorageLike): boolean {
  return saveVersioned(storageOrDefault(storage), STORAGE_KEYS.errorBook, [] as ErrorBookEntry[]);
}

export function getPracticeProgress(storage?: StorageLike): PracticeProgress | null {
  const target = storageOrDefault(storage);
  const raw = parseRaw(target, STORAGE_KEYS.practiceProgress);
  if (isVersioned<unknown>(raw)) {
    if (raw.data === null) return null;
    if (!raw.data || typeof raw.data !== 'object') {
      saveVersioned(target, STORAGE_KEYS.practiceProgress, null);
      return null;
    }
    const value = raw.data as Record<string, unknown>;
    const type = String(value.type ?? '') as QuestionType;
    const questionIds = Array.isArray(value.questionIds)
      ? value.questionIds.filter((id): id is string => typeof id === 'string' && id.startsWith('q_'))
      : [];
    if (!QUESTION_TYPES.includes(type) || questionIds.length === 0) {
      saveVersioned(target, STORAGE_KEYS.practiceProgress, null);
      return null;
    }
    const levels = Array.isArray(value.levels)
      ? value.levels.filter((level): level is Level => LEVELS.includes(level as Level))
      : [];
    const category = CATEGORIES.includes(value.category as Category) ? (value.category as Category) : '全部';
    const rawAnswers = value.answers && typeof value.answers === 'object' ? (value.answers as Record<string, unknown>) : {};
    const answers: Record<string, AnswerState> = {};
    questionIds.forEach((id) => {
      const answer = rawAnswers[id];
      if (answer && typeof answer === 'object') {
        const item = answer as Record<string, unknown>;
        answers[id] = { value: String(item.value ?? ''), confirmed: Boolean(item.confirmed) };
      }
    });
    const progress: PracticeProgress = {
      type,
      levels,
      category,
      questionIds,
      index: Math.max(0, Math.min(Number(value.index) || 0, questionIds.length - 1)),
      answers,
      timestamp: String(value.timestamp ?? new Date(0).toISOString()),
    };
    saveVersioned(target, STORAGE_KEYS.practiceProgress, progress);
    return progress;
  }
  if (!raw || typeof raw !== 'object') {
    saveVersioned(target, STORAGE_KEYS.practiceProgress, null);
    return null;
  }
  const legacy = raw as Record<string, unknown>;
  const type = String(legacy.type ?? '') as QuestionType;
  const questions = Array.isArray(legacy.questions)
    ? legacy.questions.map(legacyQuestion).filter((question): question is Question => question !== null)
    : [];
  if (!QUESTION_TYPES.includes(type) || questions.length === 0) {
    saveVersioned(target, STORAGE_KEYS.practiceProgress, null);
    return null;
  }
  const legacyAnswers = legacy.answers && typeof legacy.answers === 'object' ? (legacy.answers as Record<string, unknown>) : {};
  const answers: Record<string, AnswerState> = {};
  questions.forEach((question, index) => {
    const value = String(legacyAnswers[index] ?? '');
    const confirmed = Boolean(legacyAnswers[`${index}_confirmed`]) || (question.type !== '多选题' && value !== '');
    if (value) answers[question.id] = { value, confirmed };
  });
  const migrated: PracticeProgress = {
    type,
    levels: [...new Set(questions.flatMap((question) => question.levels))],
    category: '全部',
    questionIds: questions.map((question) => question.id),
    index: Math.max(0, Number(legacy.index) || 0),
    answers,
    timestamp: String(legacy.timestamp ?? new Date(0).toISOString()),
  };
  saveVersioned(target, STORAGE_KEYS.practiceProgress, migrated);
  return migrated;
}

export function savePracticeProgress(progress: PracticeProgress | null, storage?: StorageLike): boolean {
  return saveVersioned(storageOrDefault(storage), STORAGE_KEYS.practiceProgress, progress);
}

export function migrateAll(storage?: StorageLike): void {
  getSettings(storage);
  getErrorBook(storage);
  getPracticeProgress(storage);
}
