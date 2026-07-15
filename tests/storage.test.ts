import { describe, expect, it } from 'vitest';
import { STORAGE_KEYS, STORAGE_VERSION, getErrorBook, getPracticeProgress, getSettings, type StorageLike } from '../src/domain/storage';

class MemoryStorage implements StorageLike {
  private values = new Map<string, string>();
  getItem(key: string) { return this.values.get(key) ?? null; }
  setItem(key: string, value: string) { this.values.set(key, value); }
  removeItem(key: string) { this.values.delete(key); }
}

const legacyQuestion = {
  type: '判断题', question: '旧版判断题', options: { A: '√', B: '×' }, answer: 'A', levels: ['一级'], source_sheet: '基站来源',
};

describe('LocalStorage 迁移', () => {
  it('兼容三个旧键并写回 v2 信封格式', () => {
    const storage = new MemoryStorage();
    storage.setItem(STORAGE_KEYS.settings, JSON.stringify({ lastLevel: '三级', lastCategory: '基站' }));
    storage.setItem(STORAGE_KEYS.errorBook, JSON.stringify([{ ...legacyQuestion, userAnswer: 'B', timestamp: '2025-01-01T00:00:00.000Z' }]));
    storage.setItem(STORAGE_KEYS.practiceProgress, JSON.stringify({ type: '判断题', questions: [legacyQuestion], index: 0, answers: { 0: 'B' } }));

    expect(getSettings(storage)).toEqual({ lastLevel: '三级', lastCategory: '基站' });
    const errors = getErrorBook(storage);
    const progress = getPracticeProgress(storage);
    expect(errors).toHaveLength(1);
    expect(progress?.questionIds).toEqual([errors[0].id]);
    expect(progress?.answers[errors[0].id]).toEqual({ value: 'B', confirmed: true });
    expect(JSON.parse(storage.getItem(STORAGE_KEYS.errorBook) ?? '{}').version).toBe(STORAGE_VERSION);
    expect(JSON.parse(storage.getItem(STORAGE_KEYS.practiceProgress) ?? '{}').data.questions).toBeUndefined();
  });

  it('损坏的 v2 设置和练习进度不会让应用崩溃', () => {
    const storage = new MemoryStorage();
    storage.setItem(STORAGE_KEYS.settings, JSON.stringify({ version: STORAGE_VERSION, data: { lastLevel: '七级', lastCategory: '未知' } }));
    storage.setItem(STORAGE_KEYS.practiceProgress, JSON.stringify({ version: STORAGE_VERSION, data: { type: '未知题型', questionIds: ['bad'] } }));
    expect(getSettings(storage)).toEqual({ lastLevel: '', lastCategory: '全部' });
    expect(getPracticeProgress(storage)).toBeNull();
  });
});
