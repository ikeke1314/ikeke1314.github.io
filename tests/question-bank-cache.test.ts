import { IDBFactory } from 'fake-indexeddb';
import { beforeEach, describe, expect, it } from 'vitest';
import { loadCachedQuestionBank, saveCachedQuestionBank } from '../src/domain/question-bank-cache';

describe('题库 IndexedDB 缓存', () => {
  beforeEach(() => {
    Object.defineProperty(globalThis, 'indexedDB', { configurable: true, value: new IDBFactory() });
  });

  it('保存并恢复题库文件和 Sheet 选择', async () => {
    const data = new Uint8Array([1, 2, 3, 4]).buffer;
    await saveCachedQuestionBank({ fileName: '题库.xlsx', data, selectedSheets: ['题库', '判断题'] });
    const cached = await loadCachedQuestionBank();
    expect(cached).toMatchObject({ version: 1, fileName: '题库.xlsx', selectedSheets: ['题库', '判断题'] });
    expect(cached).not.toBeNull();
    expect([...new Uint8Array(cached!.data)]).toEqual([1, 2, 3, 4]);
  });
});
