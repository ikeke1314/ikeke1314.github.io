import '@testing-library/jest-dom/vitest';
import { cleanup } from '@testing-library/react';
import { afterEach, vi } from 'vitest';

afterEach(() => cleanup());

Object.defineProperty(window, 'speechSynthesis', {
  configurable: true,
  value: { cancel: vi.fn(), speak: vi.fn() },
});
