import { createContext, useContext, useMemo, useReducer, type Dispatch, type ReactNode } from 'react';
import type { WorkBook } from 'xlsx';
import type { AnswerState, Category, ExamResult, Level, Question, QuestionBank, QuestionType } from '../types';
import { getSettings, migrateAll, type Settings } from '../domain/storage';

export type Screen = 'home' | 'practice' | 'session' | 'result' | 'errors';
export type SessionMode = 'exam' | 'practice';

export interface AppState {
  screen: Screen;
  bank: QuestionBank | null;
  workbook: WorkBook | null;
  sheetNames: string[];
  selectedSheets: string[];
  fileStatus: string;
  fileError: string;
  settings: Settings;
  practiceLevels: Level[];
  practiceCategory: Category;
  mode: SessionMode;
  questions: Question[];
  currentIndex: number;
  answers: Record<string, AnswerState>;
  timeLeft: number;
  result: ExamResult | null;
  usedSeconds: number;
  practiceType: QuestionType | null;
  storageWarning: string;
}

type Action =
  | { type: 'WORKBOOK_READY'; workbook: WorkBook; sheetNames: string[] }
  | { type: 'CLOSE_WORKBOOK' }
  | { type: 'SET_SHEETS'; sheets: string[] }
  | { type: 'BANK_READY'; bank: QuestionBank }
  | { type: 'FILE_ERROR'; message: string }
  | { type: 'SET_SCREEN'; screen: Screen }
  | { type: 'SET_SETTINGS'; settings: Settings }
  | { type: 'SET_PRACTICE_FILTERS'; levels?: Level[]; category?: Category }
  | { type: 'START_SESSION'; mode: SessionMode; questions: Question[]; answers?: Record<string, AnswerState>; index?: number; practiceType?: QuestionType }
  | { type: 'SET_ANSWER'; questionId: string; answer: AnswerState }
  | { type: 'SET_INDEX'; index: number }
  | { type: 'TICK' }
  | { type: 'SHOW_RESULT'; result: ExamResult; usedSeconds: number }
  | { type: 'STORAGE_WARNING'; message: string };

function initialState(): AppState {
  migrateAll();
  const settings = getSettings();
  return {
    screen: 'home',
    bank: null,
    workbook: null,
    sheetNames: [],
    selectedSheets: [],
    fileStatus: '未加载题库',
    fileError: '',
    settings,
    practiceLevels: [],
    practiceCategory: settings.lastCategory,
    mode: 'exam',
    questions: [],
    currentIndex: 0,
    answers: {},
    timeLeft: 45 * 60,
    result: null,
    usedSeconds: 0,
    practiceType: null,
    storageWarning: '',
  };
}

function reducer(state: AppState, action: Action): AppState {
  switch (action.type) {
    case 'WORKBOOK_READY':
      return {
        ...state,
        workbook: action.workbook,
        sheetNames: action.sheetNames,
        selectedSheets: action.sheetNames.filter((name) => !name.includes('透视')),
        fileStatus: '请选择要导入的 Sheet',
        fileError: '',
      };
    case 'CLOSE_WORKBOOK':
      return { ...state, workbook: null, sheetNames: [], selectedSheets: [], fileStatus: state.bank ? `已加载 ${state.bank.questions.length} 道题` : '未加载题库', fileError: '' };
    case 'SET_SHEETS':
      return { ...state, selectedSheets: action.sheets };
    case 'BANK_READY':
      return {
        ...state,
        bank: action.bank,
        workbook: null,
        sheetNames: [],
        selectedSheets: [],
        practiceLevels: action.bank.levels,
        fileStatus: `已加载 ${action.bank.questions.length} 道题`,
        fileError: '',
      };
    case 'FILE_ERROR':
      return { ...state, workbook: null, sheetNames: [], fileStatus: '读取失败', fileError: action.message };
    case 'SET_SCREEN':
      return { ...state, screen: action.screen };
    case 'SET_SETTINGS':
      return { ...state, settings: action.settings };
    case 'SET_PRACTICE_FILTERS':
      return {
        ...state,
        practiceLevels: action.levels ?? state.practiceLevels,
        practiceCategory: action.category ?? state.practiceCategory,
      };
    case 'START_SESSION':
      return {
        ...state,
        screen: 'session',
        mode: action.mode,
        questions: action.questions,
        answers: action.answers ?? {},
        currentIndex: action.index ?? 0,
        timeLeft: 45 * 60,
        result: null,
        usedSeconds: 0,
        practiceType: action.practiceType ?? null,
      };
    case 'SET_ANSWER':
      return { ...state, answers: { ...state.answers, [action.questionId]: action.answer } };
    case 'SET_INDEX':
      return { ...state, currentIndex: action.index };
    case 'TICK':
      return { ...state, timeLeft: Math.max(0, state.timeLeft - 1) };
    case 'SHOW_RESULT':
      return { ...state, screen: 'result', result: action.result, usedSeconds: action.usedSeconds };
    case 'STORAGE_WARNING':
      return { ...state, storageWarning: action.message };
    default:
      return state;
  }
}

const AppStateContext = createContext<AppState | null>(null);
const AppDispatchContext = createContext<Dispatch<Action> | null>(null);

export function AppProvider({ children }: { children: ReactNode }) {
  const [state, dispatch] = useReducer(reducer, undefined, initialState);
  const stateValue = useMemo(() => state, [state]);
  return (
    <AppStateContext.Provider value={stateValue}>
      <AppDispatchContext.Provider value={dispatch}>{children}</AppDispatchContext.Provider>
    </AppStateContext.Provider>
  );
}

export function useAppState(): AppState {
  const state = useContext(AppStateContext);
  if (!state) throw new Error('useAppState 必须在 AppProvider 中使用');
  return state;
}

export function useAppDispatch(): Dispatch<Action> {
  const dispatch = useContext(AppDispatchContext);
  if (!dispatch) throw new Error('useAppDispatch 必须在 AppProvider 中使用');
  return dispatch;
}
