import { describe, expect, it } from 'vitest';
import { EXAM_COUNTS, generateExam, getWeightedCounts, scoreExam } from '../src/domain/exam';
import type { Level, Question, QuestionType } from '../src/types';

function makeQuestions(): Question[] {
  const questions: Question[] = [];
  const types = Object.keys(EXAM_COUNTS) as QuestionType[];
  const levels: Level[] = ['三级', '四级', '五级'];
  types.forEach((type) => {
    levels.forEach((level) => {
      for (let index = 0; index < 60; index += 1) {
        questions.push({
          id: `${type}-${level}-${index}`,
          type,
          question: `${type}-${level}-${index}`,
          options: { A: '正确', B: '错误' },
          answer: 'A',
          levels: [level],
          source: index % 2 ? '5-光学组件功能介绍' : '2-基站（普朗克）功能简介',
          sheetName: '总库',
        });
      }
    });
  });
  return questions;
}

describe('组卷和计分', () => {
  it('按现有四级权重计算每题型数量', () => {
    expect(getWeightedCounts('四级', 40)).toEqual({ 三级: 4, 四级: 28, 五级: 8 });
    expect(getWeightedCounts('四级', 3)).toEqual({ 三级: 0, 四级: 3, 五级: 0 });
  });

  it('生成完整题量、去重并执行明确类别过滤', () => {
    const exam = generateExam(makeQuestions(), '四级', '基站', () => 0.42);
    expect(exam).toHaveLength(63);
    expect(new Set(exam.map((question) => question.id))).toHaveLength(63);
    expect(exam.every((question) => question.source.includes('基站') || question.source.includes('普朗克'))).toBe(true);
  });

  it('题量不足时显示实际满分并按实际满分 80% 判定', () => {
    const questions = makeQuestions().slice(0, 2);
    const full = scoreExam(questions, Object.fromEntries(questions.map((question) => [question.id, 'A'])));
    expect(full.maxScore).toBe(2);
    expect(full.score).toBe(2);
    expect(full.passed).toBe(true);
  });
});
