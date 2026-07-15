import { render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { describe, expect, it, vi } from 'vitest';
import { QuestionCard } from '../src/components/QuestionCard';
import { SheetSelectionModal } from '../src/components/SheetSelectionModal';
import type { Question } from '../src/types';

describe('关键组件', () => {
  it('Sheet 多选默认状态由父状态控制并可切换', async () => {
    const onChange = vi.fn();
    render(<SheetSelectionModal sheetNames={['题库', '透视统计']} selectedSheets={['题库']} onChange={onChange} onCancel={vi.fn()} onConfirm={vi.fn()} />);
    expect(screen.getByRole('checkbox', { name: '题库' })).toBeChecked();
    expect(screen.getByRole('checkbox', { name: '透视统计' })).not.toBeChecked();
    await userEvent.click(screen.getByRole('checkbox', { name: '透视统计' }));
    expect(onChange).toHaveBeenCalledWith(['题库', '透视统计']);
  });

  it('题目文本不按 HTML 执行，选项可通过键盘确认', async () => {
    const question: Question = {
      id: 'xss', type: '单选题', question: '<img src=x onerror=alert(1)>', options: { A: '<b>安全文本</b>', B: '其他' }, answer: 'A', levels: ['一级'], source: '<script>来源</script>', sheetName: 'Sheet',
    };
    const onAnswer = vi.fn();
    const { container } = render(<QuestionCard question={question} onAnswer={onAnswer} />);
    expect(container.querySelector('img')).toBeNull();
    expect(container.querySelector('b')).toBeNull();
    const option = screen.getByRole('button', { name: /A.*安全文本/ });
    option.focus();
    await userEvent.keyboard('{Enter}');
    expect(onAnswer).toHaveBeenCalledWith('A', true);
  });
});
