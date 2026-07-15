import { useCallback, useEffect, useMemo, useRef, type ChangeEvent } from 'react';
import { CATEGORIES, LEVELS, QUESTION_TYPES, type Category, type Level, type Question, type QuestionType } from './types';
import { SheetSelectionModal } from './components/SheetSelectionModal';
import { QuestionCard } from './components/QuestionCard';
import { filterByCategory } from './domain/categories';
import { isAnswerCorrect } from './domain/answers';
import { EXAM_COUNTS, LEVEL_WEIGHTS, generateExam, scoreExam, shuffle } from './domain/exam';
import { parseWorkbook, readWorkbook } from './domain/parser';
import { loadCachedQuestionBank, saveCachedQuestionBank } from './domain/question-bank-cache';
import {
  addError,
  clearErrorBook,
  getErrorBook,
  getPracticeProgress,
  savePracticeProgress,
  saveSettings,
} from './domain/storage';
import { useAppDispatch, useAppState } from './state/AppContext';

function formatTime(seconds: number): string {
  const minutes = Math.floor(seconds / 60).toString().padStart(2, '0');
  const remainder = (seconds % 60).toString().padStart(2, '0');
  return `${minutes}:${remainder}`;
}

function Header({ title, onBack }: { title: string; onBack?: () => void }) {
  return (
    <header className="page-header">
      {onBack ? <button className="header-button" type="button" onClick={onBack}>‹ 返回</button> : <span />}
      <h1>{title}</h1>
      <span />
    </header>
  );
}

export default function App() {
  const state = useAppState();
  const dispatch = useAppDispatch();
  const restoreStartedRef = useRef(false);

  const cancelPendingActions = useCallback(() => {
    window.speechSynthesis?.cancel();
  }, []);

  useEffect(() => cancelPendingActions, [cancelPendingActions]);

  useEffect(() => {
    if (restoreStartedRef.current) return;
    restoreStartedRef.current = true;
    void (async () => {
      try {
        const cached = await loadCachedQuestionBank();
        if (!cached) return;
        const preview = await readWorkbook(cached.data);
        const selectedSheets = cached.selectedSheets.filter((sheet) => preview.sheetNames.includes(sheet));
        if (selectedSheets.length === 0) return;
        const bank = await parseWorkbook(preview.workbook, selectedSheets);
        if (bank.questions.length > 0) dispatch({ type: 'BANK_READY', bank, restored: true });
      } catch {
        // 缓存不可用不应阻止用户重新手动导入题库。
      }
    })();
  }, [dispatch]);

  const finishExam = useCallback(() => {
    if (state.mode !== 'exam' || state.questions.length === 0) return;
    cancelPendingActions();
    const values = Object.fromEntries(Object.entries(state.answers).map(([id, answer]) => [id, answer.value]));
    const result = scoreExam(state.questions, values);
    result.details.filter((detail) => !detail.isCorrect).forEach((detail) => {
      addError(detail.question, detail.userAnswer);
    });
    dispatch({ type: 'SHOW_RESULT', result, usedSeconds: 45 * 60 - state.timeLeft });
  }, [cancelPendingActions, dispatch, state.answers, state.mode, state.questions, state.timeLeft]);

  useEffect(() => {
    if (state.screen !== 'session' || state.mode !== 'exam') return undefined;
    const timer = window.setInterval(() => dispatch({ type: 'TICK' }), 1000);
    return () => window.clearInterval(timer);
  }, [dispatch, state.mode, state.screen]);

  useEffect(() => {
    if (state.screen === 'session' && state.mode === 'exam' && state.timeLeft === 0) finishExam();
  }, [finishExam, state.mode, state.screen, state.timeLeft]);

  useEffect(() => {
    if (state.screen !== 'session' || state.mode !== 'practice' || !state.practiceType || state.questions.length === 0) return;
    const saved = savePracticeProgress({
      type: state.practiceType,
      levels: state.practiceLevels,
      category: state.practiceCategory,
      questionIds: state.questions.map((question) => question.id),
      index: state.currentIndex,
      answers: state.answers,
      timestamp: new Date().toISOString(),
    });
    if (!saved) dispatch({ type: 'STORAGE_WARNING', message: '浏览器存储空间不足，练习进度未能保存。' });
  }, [dispatch, state.answers, state.currentIndex, state.mode, state.practiceCategory, state.practiceLevels, state.practiceType, state.questions, state.screen]);

  const updateSettings = (lastLevel: Level | '' = state.settings.lastLevel, lastCategory: Category = state.settings.lastCategory) => {
    const settings = { lastLevel, lastCategory };
    saveSettings(settings);
    dispatch({ type: 'SET_SETTINGS', settings });
  };

  const handleFile = async (event: ChangeEvent<HTMLInputElement>) => {
    const file = event.target.files?.[0];
    event.target.value = '';
    if (!file) return;
    try {
      const data = await file.arrayBuffer();
      const preview = await readWorkbook(data);
      dispatch({ type: 'WORKBOOK_READY', workbook: preview.workbook, sheetNames: preview.sheetNames, data, fileName: file.name });
    } catch {
      dispatch({ type: 'FILE_ERROR', message: '题库读取失败，请确认文件是有效的 .xlsx 或 .xls。' });
    }
  };

  const confirmSheets = async () => {
    if (!state.workbook || state.selectedSheets.length === 0) {
      window.alert('请至少选择一个 Sheet');
      return;
    }
    try {
      const bank = await parseWorkbook(state.workbook, state.selectedSheets);
      if (bank.questions.length === 0) throw new Error('题库没有有效题目');
      if (state.workbookData) {
        try {
          await saveCachedQuestionBank({
            fileName: state.workbookFileName,
            data: state.workbookData,
            selectedSheets: state.selectedSheets,
          });
        } catch {
          dispatch({ type: 'STORAGE_WARNING', message: '题库已导入，但浏览器未能缓存文件；刷新后需要重新选择。' });
        }
      }
      dispatch({ type: 'BANK_READY', bank });
    } catch {
      dispatch({ type: 'FILE_ERROR', message: '没有解析到有效题目，请检查表头和数据格式。' });
    }
  };

  const startExam = () => {
    if (!state.bank || !state.settings.lastLevel) {
      window.alert('请先导入题库并选择等级');
      return;
    }
    const questions = generateExam(state.bank.questions, state.settings.lastLevel, state.settings.lastCategory);
    if (questions.length === 0) {
      window.alert('当前等级和类别没有可用题目');
      return;
    }
    dispatch({ type: 'START_SESSION', mode: 'exam', questions });
  };

  const practiceCounts = useMemo(() => {
    if (!state.bank) return Object.fromEntries(QUESTION_TYPES.map((type) => [type, 0])) as Record<QuestionType, number>;
    const questions = filterByCategory(state.bank.questions, state.practiceCategory).filter((question) =>
      question.levels.some((level) => state.practiceLevels.includes(level)),
    );
    return Object.fromEntries(QUESTION_TYPES.map((type) => [type, questions.filter((question) => question.type === type).length])) as Record<QuestionType, number>;
  }, [state.bank, state.practiceCategory, state.practiceLevels]);

  const startPractice = (type: QuestionType) => {
    if (!state.bank || state.practiceLevels.length === 0) return;
    const progress = getPracticeProgress();
    if (progress?.type === type) {
      const byId = new Map(state.bank.questions.map((question) => [question.id, question]));
      const resumedQuestions = progress.questionIds.map((id) => byId.get(id)).filter((question): question is Question => Boolean(question));
      if (resumedQuestions.length === progress.questionIds.length && window.confirm('检测到上次未完成的练习，是否继续？')) {
        dispatch({
          type: 'SET_PRACTICE_FILTERS',
          levels: progress.levels.length ? progress.levels : state.practiceLevels,
          category: progress.category,
        });
        dispatch({
          type: 'START_SESSION',
          mode: 'practice',
          questions: resumedQuestions,
          answers: progress.answers,
          index: Math.min(progress.index, resumedQuestions.length - 1),
          practiceType: type,
        });
        return;
      }
    }
    const questions = shuffle(filterByCategory(state.bank.questions, state.practiceCategory).filter(
      (question) => question.type === type && question.levels.some((level) => state.practiceLevels.includes(level)),
    ));
    if (questions.length === 0) {
      window.alert('没有找到相关题目');
      return;
    }
    dispatch({ type: 'START_SESSION', mode: 'practice', questions, practiceType: type });
  };

  const goToQuestion = (index: number) => {
    if (index < 0 || index >= state.questions.length) return;
    cancelPendingActions();
    dispatch({ type: 'SET_INDEX', index });
  };

  const handleAnswer = (value: string, confirmed: boolean) => {
    const question = state.questions[state.currentIndex];
    if (!question) return;
    dispatch({ type: 'SET_ANSWER', questionId: question.id, answer: { value, confirmed } });
    if (!confirmed) return;
    const correct = isAnswerCorrect(question, value);
    if (!correct && !addError(question, value)) {
      dispatch({ type: 'STORAGE_WARNING', message: '浏览器存储空间不足，错题未能保存。' });
    }
    if (correct && state.currentIndex < state.questions.length - 1) {
      cancelPendingActions();
      dispatch({ type: 'SET_INDEX', index: state.currentIndex + 1 });
    }
  };

  const quitSession = () => {
    if (state.mode === 'exam' && !window.confirm('确定退出考试？本次考试答案不会保留。')) return;
    cancelPendingActions();
    dispatch({ type: 'SET_SCREEN', screen: state.mode === 'practice' ? 'practice' : 'home' });
  };

  const readQuestion = () => {
    const question = state.questions[state.currentIndex];
    if (!question || !('speechSynthesis' in window)) return;
    window.speechSynthesis.cancel();
    const options = Object.entries(question.options).map(([key, value]) => `选项 ${key}，${value}`).join('。');
    const utterance = new SpeechSynthesisUtterance(`题目：${question.question}。${options}`);
    utterance.lang = 'zh-CN';
    window.speechSynthesis.speak(utterance);
  };

  const currentQuestion = state.questions[state.currentIndex];

  const errors = state.screen === 'errors' ? getErrorBook() : [];

  return (
    <main className="app-shell">
      {state.storageWarning && <div className="global-warning" role="alert">{state.storageWarning}</div>}

      {state.screen === 'home' && (
        <div className="page home-page">
          <header className="hero">
            <p className="eyebrow">PE SKILL EXAM</p>
            <h1>技能士理论考核</h1>
            <p>模拟考试、专项练习与错题复盘</p>
          </header>
          <div className="dashboard-grid">
            <section className="card bank-card">
              <div><span className="section-number">01</span><h2>导入题库</h2></div>
              <p className="muted">支持 .xlsx / .xls，可多选 Sheet。</p>
              <label className="button button-ghost file-button">
                选择 Excel 文件
                <input aria-label="选择 Excel 题库" type="file" accept=".xlsx,.xls" onChange={handleFile} />
              </label>
              <p className={state.fileError ? 'status error-text' : 'status'} role="status">{state.fileError || state.fileStatus}</p>
            </section>

            <section className="card exam-card">
              <div><span className="section-number">02</span><h2>模拟考试</h2></div>
              <div className="form-grid">
                <label>等级
                  <select
                    aria-label="考试等级"
                    disabled={!state.bank}
                    value={state.settings.lastLevel}
                    onChange={(event) => updateSettings(event.target.value as Level | '')}
                  >
                    <option value="">请选择等级</option>
                    {(state.bank?.levels ?? LEVELS).map((level) => <option key={level}>{level}</option>)}
                  </select>
                </label>
                <label>类别
                  <select
                    aria-label="考试类别"
                    disabled={!state.bank}
                    value={state.settings.lastCategory}
                    onChange={(event) => updateSettings(state.settings.lastLevel, event.target.value as Category)}
                  >
                    {CATEGORIES.map((category) => <option key={category}>{category}</option>)}
                  </select>
                </label>
              </div>
              <p className="rule-summary">
                {state.settings.lastLevel
                  ? `权重：${Object.entries(LEVEL_WEIGHTS[state.settings.lastLevel]).map(([level, weight]) => `${level} ${Math.round(weight * 100)}%`).join(' · ')}`
                  : '选择等级后显示组卷权重'}
              </p>
              <p className="muted">{Object.entries(EXAM_COUNTS).map(([type, count]) => `${type} ${count}`).join(' · ')} · 45 分钟</p>
              <button className="button button-primary" type="button" disabled={!state.bank || !state.settings.lastLevel} onClick={startExam}>开始模拟考试</button>
            </section>

            <section className="card action-card">
              <div><span className="section-number">03</span><h2>专项练习</h2></div>
              <p className="muted">按等级、类别和题型集中训练，自动保存中断进度。</p>
              <button className="button button-secondary" type="button" disabled={!state.bank} onClick={() => dispatch({ type: 'SET_SCREEN', screen: 'practice' })}>选择专项</button>
            </section>

            <section className="card action-card">
              <div><span className="section-number">04</span><h2>错题本</h2></div>
              <p className="muted">按稳定题目 ID 去重，重新答错会更新最近记录。</p>
              <button className="button button-ghost" type="button" onClick={() => dispatch({ type: 'SET_SCREEN', screen: 'errors' })}>查看错题</button>
            </section>
          </div>
        </div>
      )}

      {state.screen === 'practice' && (
        <div className="page">
          <Header title="专项练习" onBack={() => dispatch({ type: 'SET_SCREEN', screen: 'home' })} />
          <div className="practice-layout">
            <section className="card filter-panel">
              <h2>选择范围</h2>
              <fieldset>
                <legend>等级（可多选）</legend>
                <div className="chip-list">
                  {(state.bank?.levels ?? []).map((level) => (
                    <label className="check-chip" key={level}>
                      <input
                        type="checkbox"
                        checked={state.practiceLevels.includes(level)}
                        onChange={() => dispatch({
                          type: 'SET_PRACTICE_FILTERS',
                          levels: state.practiceLevels.includes(level)
                            ? state.practiceLevels.filter((candidate) => candidate !== level)
                            : [...state.practiceLevels, level],
                        })}
                      />
                      <span>{level}</span>
                    </label>
                  ))}
                </div>
              </fieldset>
              <label>类别
                <select
                  aria-label="练习类别"
                  value={state.practiceCategory}
                  onChange={(event) => {
                    const category = event.target.value as Category;
                    dispatch({ type: 'SET_PRACTICE_FILTERS', category });
                    updateSettings(state.settings.lastLevel, category);
                  }}
                >
                  {CATEGORIES.map((category) => <option key={category}>{category}</option>)}
                </select>
              </label>
            </section>
            <section className="type-grid" aria-label="题型统计">
              {QUESTION_TYPES.map((type) => (
                <button className="type-card" type="button" key={type} disabled={practiceCounts[type] === 0} onClick={() => startPractice(type)}>
                  <span>{type}</span><strong>{practiceCounts[type]}</strong><small>道可练习</small>
                </button>
              ))}
            </section>
          </div>
        </div>
      )}

      {state.screen === 'session' && currentQuestion && (
        <div className="session-page">
          <header className="session-header">
            <button className="header-button" type="button" onClick={quitSession}>‹ 退出</button>
            <div className="session-progress">
              <strong>{state.currentIndex + 1} / {state.questions.length}</strong>
              {state.mode === 'exam' && <span className={state.timeLeft < 300 ? 'timer danger' : 'timer'}>{formatTime(state.timeLeft)}</span>}
            </div>
            {state.mode === 'exam' ? <button className="header-button submit" type="button" onClick={finishExam}>交卷</button> : <span />}
          </header>
          <div className="session-content">
            <div className="question-actions">
              <button className="read-button" type="button" onClick={readQuestion}>🔊 读题</button>
            </div>
            <QuestionCard
              question={currentQuestion}
              answer={state.answers[currentQuestion.id]}
              onAnswer={handleAnswer}
              canRetry={state.mode === 'practice' && Boolean(state.answers[currentQuestion.id]?.confirmed) && !isAnswerCorrect(currentQuestion, state.answers[currentQuestion.id]?.value)}
            />
          </div>
          <footer className="session-footer">
            <button className="button button-ghost" type="button" disabled={state.currentIndex === 0} onClick={() => goToQuestion(state.currentIndex - 1)}>上一题</button>
            <button className="button button-primary" type="button" disabled={state.currentIndex === state.questions.length - 1} onClick={() => goToQuestion(state.currentIndex + 1)}>下一题</button>
          </footer>
        </div>
      )}

      {state.screen === 'result' && state.result && (
        <div className="page result-page">
          <section className="result-card">
            <p className="eyebrow">EXAM COMPLETE</p>
            <h1>考试结束</h1>
            <div className="score"><strong>{state.result.score}</strong><span>/ {state.result.maxScore}</span></div>
            <p className={state.result.passed ? 'pass' : 'fail'}>{state.result.passed ? '考核通过' : '未通过'}</p>
            <div className="result-stats">
              <div><span>用时</span><strong>{formatTime(state.usedSeconds)}</strong></div>
              <div><span>错题</span><strong>{state.result.details.filter((detail) => !detail.isCorrect).length}</strong></div>
              <div><span>正确率</span><strong>{Math.round((state.result.details.filter((detail) => detail.isCorrect).length / state.result.details.length) * 100)}%</strong></div>
            </div>
            <div className="result-actions">
              <button className="button button-primary" type="button" onClick={() => dispatch({ type: 'SET_SCREEN', screen: 'home' })}>返回首页</button>
              <button className="button button-ghost" type="button" onClick={() => dispatch({ type: 'SET_SCREEN', screen: 'errors' })}>查看错题</button>
            </div>
          </section>
        </div>
      )}

      {state.screen === 'errors' && (
        <div className="page">
          <Header title="错题本" onBack={() => dispatch({ type: 'SET_SCREEN', screen: state.result ? 'result' : 'home' })} />
          <section className="error-summary card">
            <div><span>总错题</span><strong>{errors.length}</strong></div>
            {QUESTION_TYPES.map((type) => <div key={type}><span>{type}</span><strong>{errors.filter((entry) => entry.type === type).length}</strong></div>)}
            <button className="button danger-button" type="button" disabled={errors.length === 0} onClick={() => {
              if (window.confirm('确定清空错题本？')) {
                clearErrorBook();
                dispatch({ type: 'SET_SCREEN', screen: 'home' });
                dispatch({ type: 'SET_SCREEN', screen: 'errors' });
              }
            }}>清空错题</button>
          </section>
          <section className="error-list">
            {errors.length === 0 && <div className="empty-state">暂无错题记录</div>}
            {[...errors].reverse().map((entry) => (
              <article className="error-item" key={entry.id}>
                <div className="question-meta"><span className="badge">{entry.type}</span><span>{entry.source}</span><time>{entry.timestamp.slice(0, 10)}</time></div>
                <h2>{entry.question}</h2>
                {Object.entries(entry.options).length > 0 && <ul>{Object.entries(entry.options).map(([key, value]) => <li key={key}><strong>{key}</strong> {value}</li>)}</ul>}
                <p className="wrong-answer">你的答案：{entry.userAnswer || '未作答'}</p>
                <p className="correct-answer">正确答案：{entry.answer}</p>
              </article>
            ))}
          </section>
        </div>
      )}

      <SheetSelectionModal
        sheetNames={state.sheetNames}
        selectedSheets={state.selectedSheets}
        onChange={(sheets) => dispatch({ type: 'SET_SHEETS', sheets })}
        onCancel={() => dispatch({ type: 'CLOSE_WORKBOOK' })}
        onConfirm={confirmSheets}
      />
    </main>
  );
}
