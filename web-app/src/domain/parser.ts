import type { WorkBook } from 'xlsx';
import { LEVELS, QUESTION_TYPES, type Level, type OptionKey, type Question, type QuestionBank, type QuestionType } from '../types';
import { createQuestionId } from './question-id';

export interface WorkbookPreview {
  workbook: WorkBook;
  sheetNames: string[];
}

function asText(value: unknown): string {
  return String(value ?? '').trim();
}

function extractSheetCategory(sheetName: string): string {
  if (sheetName.includes('普朗克')) return '基站';
  return ['基站', '地宝', '窗宝', '光学组件'].find((category) => sheetName.includes(category)) ?? sheetName;
}

export async function readWorkbook(data: ArrayBuffer): Promise<WorkbookPreview> {
  const XLSX = await import('xlsx');
  const workbook = XLSX.read(data, { type: 'array' });
  return { workbook, sheetNames: [...workbook.SheetNames] };
}

export async function readWorkbookFile(file: File): Promise<WorkbookPreview> {
  return readWorkbook(await file.arrayBuffer());
}

export async function parseWorkbook(workbook: WorkBook, selectedSheets = workbook.SheetNames): Promise<QuestionBank> {
  const XLSX = await import('xlsx');
  const questions: Question[] = [];

  selectedSheets.forEach((sheetName) => {
    const sheet = workbook.Sheets[sheetName];
    if (!sheet) return;
    const rows = XLSX.utils.sheet_to_json<Record<string, unknown>>(sheet, { range: 3, defval: '' });
    rows.forEach((rawRow) => {
      const row = Object.fromEntries(Object.entries(rawRow).map(([key, value]) => [key.trim(), value]));
      const type = asText(row['考题类型']) as QuestionType;
      const questionText = asText(row['题目']);
      const answer = asText(row['答案']);
      if (!QUESTION_TYPES.includes(type) || !questionText || !answer) return;

      const options: Question['options'] = {};
      (['A', 'B', 'C', 'D', 'E'] as OptionKey[]).forEach((key) => {
        const value = asText(row[`选项${key}`]);
        if (value) options[key] = value;
      });
      const levels = LEVELS.filter((level) => Boolean(row[level])) as Level[];
      const source = asText(row['来源']) || extractSheetCategory(sheetName);
      const identity = { type, question: questionText, options, answer };
      questions.push({
        id: createQuestionId(identity),
        ...identity,
        levels,
        source,
        sheetName,
      });
    });
  });

  return {
    questions,
    levels: LEVELS.filter((level) => questions.some((question) => question.levels.includes(level))),
    sources: [...new Set(questions.map((question) => question.source))].sort((left, right) => left.localeCompare(right, 'zh-CN')),
  };
}
