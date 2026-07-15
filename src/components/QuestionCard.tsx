import { isAnswerCorrect, normalizeAnswer, normalizeMultiAnswer } from '../domain/answers';
import type { AnswerState, OptionKey, Question } from '../types';

interface QuestionCardProps {
  question: Question;
  answer?: AnswerState;
  onAnswer: (value: string, confirmed: boolean) => void;
  canRetry?: boolean;
}

function optionIsCorrect(question: Question, key: OptionKey): boolean {
  if (question.type === '判断题') return isAnswerCorrect(question, key);
  if (question.type === '多选题') return normalizeMultiAnswer(question.answer).includes(key);
  return normalizeAnswer(question, question.answer) === key;
}

export function QuestionCard({ question, answer = { value: '', confirmed: false }, onAnswer, canRetry = false }: QuestionCardProps) {
  const isChoice = question.type !== '简答题';
  const isMulti = question.type === '多选题';
  const confirmed = answer.confirmed;
  const locked = confirmed && !canRetry;

  const chooseOption = (key: OptionKey) => {
    if (locked) return;
    if (isMulti) {
      const selected = normalizeMultiAnswer(answer.value);
      const next = selected.includes(key)
        ? selected.replace(key, '')
        : normalizeMultiAnswer(`${selected}${key}`);
      onAnswer(next, false);
      return;
    }
    onAnswer(key, true);
  };

  return (
    <article className="question-card" aria-live="polite">
      <div className="question-meta">
        <span className="badge">{question.type}</span>
        <span>{question.source}</span>
        <span>{question.levels.join('、') || '未标等级'}</span>
      </div>
      <h2 className="question-text">{question.question}</h2>

      {isChoice ? (
        <div className="options" role="group" aria-label="答题选项">
          {Object.entries(question.options).map(([rawKey, text]) => {
            const key = rawKey as OptionKey;
            const selected = isMulti ? answer.value.includes(key) : answer.value === key;
            const correct = confirmed && optionIsCorrect(question, key);
            const wrong = confirmed && selected && !correct;
            return (
              <button
                className={`option${selected ? ' selected' : ''}${correct ? ' correct' : ''}${wrong ? ' wrong' : ''}`}
                type="button"
                key={key}
                aria-pressed={selected}
                disabled={locked}
                onClick={() => chooseOption(key)}
              >
                <span className="option-key">{key}</span>
                <span>{text}</span>
              </button>
            );
          })}
          {isMulti && !locked && (
            <button
              className="button button-primary confirm-answer"
              type="button"
              disabled={normalizeMultiAnswer(answer.value).length < 2}
              onClick={() => onAnswer(answer.value, true)}
            >
              确认答案
            </button>
          )}
        </div>
      ) : (
        <div className="short-answer">
          <label htmlFor={`short-${question.id}`}>请输入答案</label>
          <textarea
            id={`short-${question.id}`}
            rows={5}
            value={answer.value}
            disabled={locked}
            onChange={(event) => onAnswer(event.target.value, false)}
          />
          {!locked && (
            <button
              className="button button-primary"
              type="button"
              disabled={!answer.value.trim()}
              onClick={() => onAnswer(answer.value, true)}
            >
              提交答案
            </button>
          )}
        </div>
      )}

      {confirmed && (
        <div className={`feedback ${isAnswerCorrect(question, answer.value) ? 'feedback-correct' : 'feedback-wrong'}`}>
          <strong>{isAnswerCorrect(question, answer.value) ? '回答正确' : '回答错误'}</strong>
          {!isAnswerCorrect(question, answer.value) && <span>正确答案：{question.answer}</span>}
        </div>
      )}
    </article>
  );
}
