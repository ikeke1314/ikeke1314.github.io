import { describe, expect, it } from 'vitest';
import { isAnswerCorrect, normalizeJudgeAnswer, normalizeMultiAnswer } from '../src/domain/answers';
import type { Question } from '../src/types';

const judgeQuestion: Question = {
  id: 'judge', type: '判断题', question: '判断', options: { A: '√', B: '×' }, answer: 'B', levels: ['一级'], source: '来源', sheetName: 'Sheet1',
};

describe('答案标准化与评分', () => {
  it('多选答案去重、排序并忽略分隔符', () => {
    expect(normalizeMultiAnswer('c, A A')).toBe('AC');
  });

  it('判断题的 A/B、√/× 和文字答案等价', () => {
    expect(normalizeJudgeAnswer('A', judgeQuestion)).toBe('√');
    expect(normalizeJudgeAnswer('错误')).toBe('×');
    expect(isAnswerCorrect(judgeQuestion, '×')).toBe(true);
    expect(isAnswerCorrect(judgeQuestion, 'B')).toBe(true);
  });
});
