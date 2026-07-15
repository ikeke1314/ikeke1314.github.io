import type { Question, QuestionType } from '../types';

interface QuestionIdentity {
  type: QuestionType;
  question: string;
  options?: Record<string, unknown> | Partial<Question['options']>;
  answer: string;
}

function normalizeIdentityText(value: unknown): string {
  return String(value ?? '').trim().replace(/\s+/g, ' ');
}

function hash32(value: string, seed: number): string {
  let hash = seed >>> 0;
  for (let index = 0; index < value.length; index += 1) {
    hash ^= value.charCodeAt(index);
    hash = Math.imul(hash, 0x01000193) >>> 0;
  }
  return hash.toString(16).padStart(8, '0');
}

/** ID 只取题目固有内容，来源或等级调整不会让已有错题失联。 */
export function createQuestionId(identity: QuestionIdentity): string {
  const optionText = Object.entries(identity.options ?? {})
    .sort(([left], [right]) => left.localeCompare(right))
    .map(([key, value]) => `${key}:${normalizeIdentityText(value)}`)
    .join('|');
  const canonical = [
    identity.type,
    normalizeIdentityText(identity.question),
    optionText,
    normalizeIdentityText(identity.answer),
  ].join('\u001f');
  return `q_${hash32(canonical, 0x811c9dc5)}${hash32(canonical, 0x9e3779b9)}`;
}
