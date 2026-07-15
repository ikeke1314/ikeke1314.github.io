import { readFileSync } from 'node:fs';
import { resolve } from 'node:path';
import * as XLSX from 'xlsx';
import { describe, expect, it } from 'vitest';
import { parseWorkbook, readWorkbook } from '../src/domain/parser';
import { createWorkbookFixture } from './fixtures/workbook';

describe('题库解析', () => {
  it('从第 4 行读取表头、解析四种题型和六级标记', async () => {
    const workbook = createWorkbookFixture();
    const bank = await parseWorkbook(workbook, ['正常题库']);
    expect(bank.questions).toHaveLength(4);
    expect(bank.questions.map((question) => question.type)).toEqual(['单选题', '判断题', '多选题', '简答题']);
    expect(bank.levels).toEqual(['一级', '二级', '三级', '四级']);
    expect(bank.questions[1].question).toContain('<img');
    expect(bank.questions[0].id).toMatch(/^q_[0-9a-f]{16}$/);
  });

  it('可从 ArrayBuffer 读取 xlsx 工作簿', async () => {
    const bytes = XLSX.write(createWorkbookFixture(), { type: 'array', bookType: 'xlsx' }) as ArrayBuffer;
    await expect(readWorkbook(bytes)).resolves.toMatchObject({ sheetNames: ['正常题库', '数据透视表'] });
  });

  it('能解析提供的真实题库', async () => {
    const path = resolve(process.cwd(), '../pc-app/exam_bank/PE 技能士题库.xlsx');
    const workbook = XLSX.read(readFileSync(path), { type: 'buffer' });
    const bank = await parseWorkbook(workbook);
    expect(bank.questions).toHaveLength(1291);
    expect(bank.levels).toEqual(['一级', '二级', '三级', '四级', '五级', '六级']);
    expect(new Set(bank.questions.map((question) => question.type))).toEqual(new Set(['单选题', '多选题', '判断题', '简答题']));
  });
});
