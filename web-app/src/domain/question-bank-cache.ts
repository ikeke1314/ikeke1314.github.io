const DATABASE_NAME = 'skill_exam_question_bank_cache';
const DATABASE_VERSION = 1;
const STORE_NAME = 'imports';
const LAST_IMPORT_KEY = 'last_import';

export interface CachedQuestionBank {
  version: 1;
  fileName: string;
  data: ArrayBuffer;
  selectedSheets: string[];
  savedAt: string;
}

function openDatabase(): Promise<IDBDatabase> {
  return new Promise((resolve, reject) => {
    if (!('indexedDB' in globalThis)) {
      reject(new Error('当前浏览器不支持 IndexedDB'));
      return;
    }
    const request = indexedDB.open(DATABASE_NAME, DATABASE_VERSION);
    request.onupgradeneeded = () => {
      if (!request.result.objectStoreNames.contains(STORE_NAME)) {
        request.result.createObjectStore(STORE_NAME);
      }
    };
    request.onsuccess = () => resolve(request.result);
    request.onerror = () => reject(request.error ?? new Error('无法打开题库缓存'));
  });
}

function waitForTransaction(transaction: IDBTransaction): Promise<void> {
  return new Promise((resolve, reject) => {
    transaction.oncomplete = () => resolve();
    transaction.onabort = () => reject(transaction.error ?? new Error('题库缓存事务已中止'));
    transaction.onerror = () => reject(transaction.error ?? new Error('题库缓存事务失败'));
  });
}

export async function saveCachedQuestionBank(
  value: Omit<CachedQuestionBank, 'version' | 'savedAt'>,
): Promise<void> {
  const database = await openDatabase();
  try {
    const transaction = database.transaction(STORE_NAME, 'readwrite');
    const completion = waitForTransaction(transaction);
    transaction.objectStore(STORE_NAME).put(
      { ...value, version: 1, savedAt: new Date().toISOString() } satisfies CachedQuestionBank,
      LAST_IMPORT_KEY,
    );
    await completion;
  } finally {
    database.close();
  }
}

export async function loadCachedQuestionBank(): Promise<CachedQuestionBank | null> {
  const database = await openDatabase();
  try {
    const transaction = database.transaction(STORE_NAME, 'readonly');
    const completion = waitForTransaction(transaction);
    const request = transaction.objectStore(STORE_NAME).get(LAST_IMPORT_KEY);
    const value = await new Promise<unknown>((resolve, reject) => {
      request.onsuccess = () => resolve(request.result);
      request.onerror = () => reject(request.error ?? new Error('无法读取题库缓存'));
    });
    await completion;
    if (!value || typeof value !== 'object') return null;
    const candidate = value as Partial<CachedQuestionBank>;
    if (
      candidate.version !== 1 ||
      typeof candidate.fileName !== 'string' ||
      Object.prototype.toString.call(candidate.data) !== '[object ArrayBuffer]' ||
      !Array.isArray(candidate.selectedSheets)
    ) return null;
    return candidate as CachedQuestionBank;
  } finally {
    database.close();
  }
}
