import { expect, test } from '@playwright/test';
import { resolve } from 'node:path';
import * as XLSX from 'xlsx';

const realWorkbook = resolve(process.cwd(), '../pc-app/exam_bank/PE 技能士题库.xlsx');

async function expectNoHorizontalOverflow(page: import('@playwright/test').Page) {
  const overflow = await page.evaluate(() => ({
    documentWidth: document.documentElement.scrollWidth,
    viewportWidth: document.documentElement.clientWidth,
  }));
  expect(overflow.documentWidth).toBeLessThanOrEqual(overflow.viewportWidth);
}

test('真实 Excel：导入、答题、交卷和错题本完整流程', async ({ page }) => {
  const pageErrors: Error[] = [];
  page.on('pageerror', (error) => pageErrors.push(error));
  await page.goto('/');
  await expectNoHorizontalOverflow(page);

  await page.getByLabel('选择 Excel 题库').setInputFiles(realWorkbook);
  await expect(page.getByRole('dialog', { name: /选择要加载的题库/ })).toBeVisible();
  await expect(page.getByRole('checkbox', { name: 'PE技能士理论考核题库（总库）' })).toBeChecked();
  await page.getByRole('button', { name: '确定加载' }).click();
  await expect(page.getByRole('status')).toContainText('已加载 1291 道题');

  await page.getByLabel('考试等级').selectOption('四级');
  await page.getByLabel('考试类别').selectOption('全部');
  await page.getByRole('button', { name: '开始模拟考试' }).click();
  await expect(page.getByText(/1 \/ 63/)).toBeVisible();
  await expectNoHorizontalOverflow(page);

  const firstOption = page.getByRole('group', { name: '答题选项' }).getByRole('button').first();
  await firstOption.click();
  await expect(page.getByText(/回答正确|回答错误/)).toBeVisible();
  await page.getByRole('button', { name: '交卷' }).click();
  await expect(page.getByRole('heading', { name: '考试结束' })).toBeVisible();
  await expect(page.locator('.score')).toContainText('/ 100');
  await expectNoHorizontalOverflow(page);

  await page.getByRole('button', { name: '查看错题' }).click();
  await expect(page.getByRole('heading', { name: '错题本' })).toBeVisible();
  await expect(page.locator('.error-item').first()).toBeVisible();
  await expectNoHorizontalOverflow(page);
  expect(pageErrors).toEqual([]);
});

test('手动切题取消自动跳题，退出考试清理计时器', async ({ page }, testInfo) => {
  test.skip(testInfo.project.name !== 'windows-chromium-1440x900', '定时器回归只需在桌面 Chromium 验证一次');
  await page.addInitScript(() => {
    const tracked = new Set<number>();
    const originalSetInterval = window.setInterval.bind(window);
    const originalClearInterval = window.clearInterval.bind(window);
    Object.defineProperty(window, '__examIntervals', { value: tracked });
    window.setInterval = ((handler: TimerHandler, timeout?: number, ...args: unknown[]) => {
      const id = originalSetInterval(handler, timeout, ...args);
      if (timeout === 1000) tracked.add(id);
      return id;
    }) as typeof window.setInterval;
    window.clearInterval = ((id?: number) => {
      if (id !== undefined) tracked.delete(id);
      originalClearInterval(id);
    }) as typeof window.clearInterval;
  });

  const headers = ['序号', '随机码', '固定码', '考题类型', '题目', '选项A', '选项B', '选项C', '选项D', '选项E', '答案', '来源', '一级', '二级', '三级', '四级', '五级', '六级'];
  const rows: unknown[][] = [['说明'], [], [], headers];
  for (let index = 1; index <= 5; index += 1) {
    rows.push([index, '', '', '单选题', `定时器题目 ${index}`, '正确', '错误', '', '', '', 'A', '基站测试', '√', '', '', '', '', '']);
  }
  const workbook = XLSX.utils.book_new();
  XLSX.utils.book_append_sheet(workbook, XLSX.utils.aoa_to_sheet(rows), '小题库');
  const body = XLSX.write(workbook, { type: 'buffer', bookType: 'xlsx' });

  await page.goto('/');
  await page.getByLabel('选择 Excel 题库').setInputFiles({ name: 'mini.xlsx', mimeType: 'application/vnd.openxmlformats-officedocument.spreadsheetml.sheet', buffer: body });
  await page.getByRole('button', { name: '确定加载' }).click();
  await page.getByLabel('考试等级').selectOption('一级');
  await page.getByRole('button', { name: '开始模拟考试' }).click();
  await expect(page.getByText('1 / 5')).toBeVisible();
  await expect.poll(() => page.evaluate(() => (Reflect.get(window, '__examIntervals') as Set<number>).size)).toBe(1);

  await page.getByRole('button', { name: /A.*正确/ }).click();
  await page.getByRole('button', { name: '下一题' }).click();
  await page.waitForTimeout(1400);
  await expect(page.getByText('2 / 5')).toBeVisible();

  page.once('dialog', (dialog) => dialog.accept());
  await page.getByRole('button', { name: '‹ 退出' }).click();
  await expect(page.getByRole('heading', { name: '技能士理论考核' })).toBeVisible();
  await expect.poll(() => page.evaluate(() => (Reflect.get(window, '__examIntervals') as Set<number>).size)).toBe(0);
});
