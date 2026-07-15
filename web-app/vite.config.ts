import { defineConfig } from 'vitest/config';
import react from '@vitejs/plugin-react';

export default defineConfig({
  // 相对资源地址同时适用于用户根站点和任意 GitHub Pages 仓库子路径。
  base: './',
  plugins: [react()],
  test: {
    environment: 'jsdom',
    setupFiles: './tests/setup.ts',
    css: true,
    include: ['tests/**/*.test.{ts,tsx}'],
  },
});
