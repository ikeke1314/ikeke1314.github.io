// Main App Controller

const App = {
    loader: new ExcelLoader(),
    examManager: null,
    currentExamQuestions: [],
    currentQuestionIndex: 0,
    timerInterval: null,
    timeLeft: 45 * 60,
    mode: 'exam',
    tempWorkbook: null,

    init() {
        this.bindEvents();
        this.checkPreviousData();
    },

    bindEvents() {
        document.getElementById('btn-load-file').addEventListener('click', () => {
            document.getElementById('file-input').click();
        });

        document.getElementById('file-input').addEventListener('change', (e) => {
            const file = e.target.files[0];
            if (file) this.loadQuestionBank(file);
        });

        document.getElementById('btn-cancel-sheet').addEventListener('click', () => {
            document.getElementById('sheet-modal').classList.add('hidden');
            document.getElementById('file-input').value = '';
        });

        document.getElementById('btn-confirm-sheet').addEventListener('click', () => {
            this.confirmSheetSelection();
        });

        document.getElementById('btn-start-exam').addEventListener('click', () => {
            this.startExam();
        });

        document.getElementById('exam-level-select').addEventListener('change', (e) => {
            this.updateExamRules(e.target.value);
            Storage.saveSettings({ lastLevel: e.target.value });
        });

        document.getElementById('btn-prev-q').addEventListener('click', () => this.navQuestion(-1));
        document.getElementById('btn-next-q').addEventListener('click', () => this.navQuestion(1));
        document.getElementById('btn-submit-exam').addEventListener('click', () => this.finishExam());
        document.getElementById('btn-quit-exam').addEventListener('click', () => {
            if (confirm('确定要退出吗？进度将丢失。')) this.showScreen('home-screen');
        });

        document.getElementById('btn-home').addEventListener('click', () => this.showScreen('home-screen'));
        document.getElementById('btn-review-errors').addEventListener('click', () => {
            this.showErrorBook();
        });

        document.getElementById('btn-practice-mode').addEventListener('click', () => {
            this.initPracticeMode();
        });
        document.getElementById('btn-quit-practice').addEventListener('click', () => this.showScreen('home-screen'));

        document.getElementById('practice-category-select').addEventListener('change', (e) => {
            this.updatePracticeCounts();
            Storage.saveSettings({ lastCategory: e.target.value });
        });

        document.getElementById('exam-category-select').addEventListener('change', (e) => {
            Storage.saveSettings({ lastCategory: e.target.value });
        });

        document.querySelectorAll('.btn-tile').forEach(btn => {
            btn.addEventListener('click', () => {
                const type = btn.dataset.type;
                this.startPractice(type);
            });
        });

        document.getElementById('btn-error-book').addEventListener('click', () => {
            this.showErrorBook();
        });
        document.getElementById('btn-quit-error').addEventListener('click', () => this.showScreen('home-screen'));
        document.getElementById('btn-clear-error').addEventListener('click', () => {
            if (confirm('确定要清空错题本吗？')) {
                Storage.clearErrors();
                this.showErrorBook();
            }
        });

        document.getElementById('btn-read-question').addEventListener('click', () => {
            this.readCurrentQuestion();
        });
    },

    checkPreviousData() { },

    async loadQuestionBank(file) {
        try {
            document.getElementById('file-status').innerText = '正在读取文件...';
            const data = await file.arrayBuffer();
            const workbook = XLSX.read(data, { type: 'array' });
            this.tempWorkbook = workbook;
            this.showSheetSelection(workbook.SheetNames);
        } catch (e) {
            console.error(e);
            alert('文件读取失败，请检查格式');
            document.getElementById('file-status').innerText = '读取失败';
            document.getElementById('file-status').style.color = 'red';
        }
    },

    showSheetSelection(sheetNames) {
        const modal = document.getElementById('sheet-modal');
        const list = document.getElementById('sheet-list');
        list.innerHTML = '';
        sheetNames.forEach(name => {
            const label = document.createElement('label');
            label.className = 'checkbox-label';
            const isChecked = !name.includes('透视');
            label.innerHTML = `<input type="checkbox" value="${name}" ${isChecked ? 'checked' : ''}> <span>${name}</span>`;
            list.appendChild(label);
        });
        modal.classList.remove('hidden');
    },

    confirmSheetSelection() {
        const selectedSheets = Array.from(document.querySelectorAll('#sheet-list input:checked')).map(cb => cb.value);
        if (selectedSheets.length === 0) {
            alert('请至少选择一个工作表');
            return;
        }
        document.getElementById('sheet-modal').classList.add('hidden');
        document.getElementById('file-status').innerText = '正在解析题目...';
        try {
            this.loader.parseWorkbook(this.tempWorkbook, selectedSheets);
            document.getElementById('file-status').innerText = `已加载: ${this.loader.questions.length} 道题目`;
            document.getElementById('file-status').style.color = 'green';
            this.examManager = new ExamManager(this.loader);
            this.updateLevelSelect();
            this.updateCategorySelect();
            document.getElementById('btn-start-exam').disabled = false;
            document.getElementById('exam-level-select').disabled = false;
            document.getElementById('exam-category-select').disabled = false;
            document.getElementById('btn-practice-mode').disabled = false;
            this.tempWorkbook = null;
        } catch (e) {
            console.error(e);
            alert('解析失败');
        }
    },

    updateLevelSelect() {
        const select = document.getElementById('exam-level-select');
        select.innerHTML = '<option value="">请选择等级</option>';
        const levels = Array.from(this.loader.levels).sort();
        levels.forEach(lvl => {
            const option = document.createElement('option');
            option.value = lvl;
            option.innerText = lvl;
            select.appendChild(option);
        });
        const settings = Storage.getSettings();
        if (settings.lastLevel && this.loader.levels.has(settings.lastLevel)) {
            select.value = settings.lastLevel;
            this.updateExamRules(settings.lastLevel);
        }
    },

    updateCategorySelect() {
        // Python version line 151: Fixed categories
        const categories = ['全部', '基站', '地宝', '窗宝', '光学组件'];
        const selects = ['exam-category-select', 'practice-category-select'];
        const settings = Storage.getSettings();
        selects.forEach(id => {
            const el = document.getElementById(id);
            if (el) {
                el.innerHTML = '';
                categories.forEach(cat => {
                    const option = document.createElement('option');
                    option.value = cat;
                    option.innerText = cat;
                    el.appendChild(option);
                });
                if (settings.lastCategory && categories.includes(settings.lastCategory)) {
                    el.value = settings.lastCategory;
                }
            }
        });
    },

    // Python version line 256-295: get_selected_sources logic
    getSelectedSources() {
        const category = document.getElementById('exam-category-select').value;
        if (category === '全部') return null;
        const categories = ['基站', '地宝', '窗宝', '光学组件'];
        if (!categories.includes(category)) return null;
        if (!this.loader) return null;
        const allSources = Array.from(this.loader.sources);
        const otherCategories = categories.filter(c => c !== category);
        const filteredSources = [];
        for (const source of allSources) {
            const hasSelected = source.includes(category);
            const hasOther = otherCategories.some(other => source.includes(other));
            if (!hasOther) {
                filteredSources.push(source);
            }
        }
        return filteredSources.length > 0 ? filteredSources : null;
    },

    updateExamRules(level) {
        const rulesDiv = document.getElementById('exam-rules');
        if (!level) {
            rulesDiv.innerText = '';
            return;
        }
        const weights = this.examManager.LEVEL_WEIGHTS[level];
        if (weights) {
            const text = Object.entries(weights).map(([l, w]) => `${l} ${Math.round(w * 100)}%`).join(' + ');
            rulesDiv.innerText = `出题规则: ${text}`;
        } else {
            rulesDiv.innerText = '出题规则: 100% 当前等级';
        }
    },

    startExam() {
        const level = document.getElementById('exam-level-select').value;
        if (!level) {
            alert('请先选择等级');
            return;
        }
        const selectedSources = this.getSelectedSources();
        this.mode = 'exam';
        this.currentExamQuestions = this.examManager.startExam(level, selectedSources);
        if (this.currentExamQuestions.length === 0) {
            alert('该等级/类别下题目不足，无法开始考试');
            return;
        }
        this.currentQuestionIndex = 0;
        this.timeLeft = 45 * 60;
        this.startTimer();
        this.showScreen('exam-screen');
        document.getElementById('btn-submit-exam').style.display = 'block';
        document.getElementById('exam-timer').style.display = 'block';
        this.renderQuestion();
    },

    startPractice(type) {
        const selectedLevels = Array.from(document.querySelectorAll('#practice-level-checks input:checked')).map(cb => cb.value);
        const category = document.getElementById('practice-category-select').value;
        if (selectedLevels.length === 0) {
            alert('请至少选择一个等级');
            return;
        }
        // Apply same filtering logic as exam
        let selectedSources = null;
        if (category !== '全部') {
            const categories = ['基站', '地宝', '窗宝', '光学组件'];
            if (categories.includes(category)) {
                const allSources = Array.from(this.loader.sources);
                const otherCategories = categories.filter(c => c !== category);
                const filteredSources = [];
                for (const source of allSources) {
                    const hasOther = otherCategories.some(other => source.includes(other));
                    if (!hasOther) {
                        filteredSources.push(source);
                    }
                }
                selectedSources = filteredSources.length > 0 ? filteredSources : null;
            }
        }
        const savedProgress = Storage.load(Storage.KEYS.PRACTICE_PROGRESS);
        let useSaved = false;
        if (savedProgress && savedProgress.type === type && savedProgress.questions && savedProgress.questions.length > 0) {
            if (confirm('检测到上次未完成的练习，是否继续？')) {
                useSaved = true;
            }
        }
        if (useSaved) {
            this.currentExamQuestions = savedProgress.questions;
            this.currentQuestionIndex = savedProgress.index || 0;
            this.examManager.userAnswers = savedProgress.answers || {};
        } else {
            const qs = this.loader.getQuestions(selectedLevels, { [type]: 9999 }, selectedSources);
            if (!qs[type] || qs[type].length === 0) {
                alert('没有找到相关题目');
                return;
            }
            this.currentExamQuestions = qs[type];
            this.currentQuestionIndex = 0;
            this.examManager.userAnswers = {};
        }
        // Sync questions to examManager so submitAnswer works
        this.examManager.currentQuestions = this.currentExamQuestions;

        this.mode = 'practice';
        this.showScreen('exam-screen');
        document.getElementById('btn-submit-exam').style.display = 'none';
        document.getElementById('exam-timer').style.display = 'none';
        this.renderQuestion();
    },

    renderQuestion() {
        if (this.mode === 'practice') {
            Storage.save(Storage.KEYS.PRACTICE_PROGRESS, {
                type: this.currentExamQuestions[0].type,
                questions: this.currentExamQuestions,
                index: this.currentQuestionIndex,
                answers: this.examManager.userAnswers,
                timestamp: new Date().toISOString()
            });
        }
        const q = this.currentExamQuestions[this.currentQuestionIndex];
        if (!q) return;
        document.getElementById('q-index').innerText = `${this.currentQuestionIndex + 1}/${this.currentExamQuestions.length}`;
        document.getElementById('q-type').innerText = q.type;
        document.getElementById('q-text').innerText = q.question;
        const optionsContainer = document.getElementById('q-options');
        optionsContainer.innerHTML = '';
        const currentAns = this.examManager.userAnswers[this.currentQuestionIndex] || '';
        const isAnswered = !!currentAns;
        if (q.type === '单选题' || q.type === '多选题' || q.type === '判断题') {
            // Use actual options from question bank for all question types
            const options = q.options;
            Object.entries(options).forEach(([key, val]) => {
                const el = document.createElement('div');
                el.className = 'option-item';
                el.innerHTML = `<span class="option-label">${key}</span><span class="option-text">${val}</span>`;
                if (q.type === '多选题') {
                    if (currentAns.includes(key)) el.classList.add('selected');
                } else {
                    if (currentAns === key) el.classList.add('selected');
                }
                // Answer Feedback (Both Exam and Practice Mode)
                // For multi-choice, only show feedback after confirmation
                const isMultiConfirmed = q.type === '多选题' && this.examManager.userAnswers[this.currentQuestionIndex + '_confirmed'];
                const shouldShowFeedback = (q.type !== '多选题' && isAnswered) || isMultiConfirmed;

                if (shouldShowFeedback) {
                    const correctAnswer = q.answer || '';
                    let isCorrectOption = false;
                    let isSelectedOption = false;

                    if (q.type === '判断题') {
                        // For 判断题, compare option value (√/×) with answer
                        const optionValue = val; // val is the option text (√ or ×)
                        isCorrectOption = correctAnswer === optionValue;
                        isSelectedOption = currentAns === optionValue;
                    } else {
                        // For other types, compare option key (A/B/C/D)
                        isCorrectOption = correctAnswer.includes(key);
                        isSelectedOption = currentAns.includes(key);
                    }

                    if (isCorrectOption) {
                        el.classList.add('correct');
                    } else if (isSelectedOption && !isCorrectOption) {
                        el.classList.add('wrong');
                    }
                }
                // Click event binding logic
                // Reuse isMultiConfirmed from above

                // Determine if option should be clickable
                let shouldDisableClick = false;
                if (this.mode === 'exam') {
                    // Exam mode: disable after answering (except multi-choice before confirmation)
                    if (q.type === '多选题') {
                        shouldDisableClick = isMultiConfirmed;
                    } else {
                        shouldDisableClick = isAnswered;
                    }
                    console.log(`[DEBUG] Exam mode - type: ${q.type}, isAnswered: ${isAnswered}, shouldDisableClick: ${shouldDisableClick}`);
                } else {
                    // Practice mode: only disable multi-choice after confirmation
                    shouldDisableClick = isMultiConfirmed;
                    console.log(`[DEBUG] Practice mode - type: ${q.type}, isAnswered: ${isAnswered}, isMultiConfirmed: ${isMultiConfirmed}, shouldDisableClick: ${shouldDisableClick}`);
                }

                if (!shouldDisableClick) {
                    // Add both click and touch events for mobile compatibility
                    const handleSelect = () => {
                        console.log(`[DEBUG] Option clicked: ${key}, type: ${q.type}, mode: ${this.mode}`);
                        this.selectOption(key, q.type);
                    };
                    el.addEventListener('click', handleSelect);
                    el.addEventListener('touchend', (e) => {
                        e.preventDefault(); // Prevent double-firing with click
                        handleSelect();
                    });
                } else {
                    console.log(`[DEBUG] Click disabled for option ${key}`);
                }
                optionsContainer.appendChild(el);
            });
            // Show feedback for both exam and practice mode
            if (true) {
                if (q.type === '多选题' && !this.examManager.userAnswers[this.currentQuestionIndex + '_confirmed']) {
                    const btn = document.createElement('button');
                    btn.className = 'btn btn-primary';
                    btn.innerText = '确认答案';
                    btn.style.marginTop = '10px';
                    btn.onclick = () => {
                        const ans = this.examManager.userAnswers[this.currentQuestionIndex];
                        if (!ans || ans.length < 2) {
                            alert('请至少选择两个选项');
                            return;
                        }
                        this.examManager.userAnswers[this.currentQuestionIndex + '_confirmed'] = true;
                        this.renderQuestion();
                    };
                    optionsContainer.appendChild(btn);
                }
                const isMultiConfirmed = q.type === '多选题' && this.examManager.userAnswers[this.currentQuestionIndex + '_confirmed'];
                if ((q.type !== '多选题' && isAnswered) || isMultiConfirmed) {
                    const resultDiv = document.createElement('div');
                    resultDiv.style.marginTop = '15px';
                    resultDiv.style.padding = '10px';
                    resultDiv.style.borderRadius = '8px';
                    const isCorrect = currentAns === q.answer;
                    if (isCorrect) {
                        resultDiv.style.background = '#e6f4ea';
                        resultDiv.style.color = '#1e8e3e';
                        resultDiv.innerHTML = '<strong>√ 回答正确!</strong>';
                    } else {
                        resultDiv.style.background = '#fce8e6';
                        resultDiv.style.color = '#d93025';
                        resultDiv.innerHTML = `<strong>× 回答错误！</strong> 正确答案: ${q.answer}`;
                    }
                    optionsContainer.appendChild(resultDiv);
                }
            }
        } else {
            const textarea = document.createElement('textarea');
            textarea.className = 'form-control';
            textarea.rows = 4;
            textarea.style.width = '100%';
            textarea.value = currentAns;
            if (this.mode === 'practice') {
                const btn = document.createElement('button');
                btn.className = 'btn btn-primary';
                btn.innerText = '查看答案';
                btn.style.marginTop = '10px';
                btn.onclick = () => {
                    const ansDiv = document.createElement('div');
                    ansDiv.style.marginTop = '10px';
                    ansDiv.style.color = 'green';
                    ansDiv.innerText = `参考答案: ${q.answer}`;
                    optionsContainer.appendChild(ansDiv);
                    btn.remove();
                };
                optionsContainer.appendChild(textarea);
                optionsContainer.appendChild(btn);
            } else {
                // Exam mode: also add "查看答案" button
                textarea.addEventListener('input', (e) => {
                    this.examManager.submitAnswer(this.currentQuestionIndex, e.target.value);
                });
                optionsContainer.appendChild(textarea);

                // Add "查看答案" button for exam mode
                const btn = document.createElement('button');
                btn.className = 'btn btn-primary';
                btn.innerText = '查看答案';
                btn.style.marginTop = '10px';
                btn.onclick = () => {
                    const ansDiv = document.createElement('div');
                    ansDiv.style.marginTop = '10px';
                    ansDiv.style.padding = '10px';
                    ansDiv.style.borderRadius = '8px';
                    ansDiv.style.background = '#e6f4ea';
                    ansDiv.style.color = '#1e8e3e';
                    ansDiv.innerHTML = `<strong>参考答案:</strong><br>${q.answer}`;
                    optionsContainer.appendChild(ansDiv);
                    btn.remove();
                };
                optionsContainer.appendChild(btn);
            }
        }
    },

    selectOption(key, type) {
        console.log(`[DEBUG] selectOption called: key=${key}, type=${type}, mode=${this.mode}, questionIndex=${this.currentQuestionIndex}`);

        // Check if already answered (prevent re-answering in exam mode)
        if (this.mode === 'exam') {
            if (type !== '多选题' && this.examManager.userAnswers[this.currentQuestionIndex]) {
                console.log('[DEBUG] Exam mode: already answered, returning');
                return; // Already answered, cannot modify
            }
            if (type === '多选题' && this.examManager.userAnswers[this.currentQuestionIndex + '_confirmed']) {
                console.log('[DEBUG] Exam mode: multi-choice confirmed, returning');
                return; // Multi-choice confirmed, cannot modify
            }
        }

        let currentAns = this.examManager.userAnswers[this.currentQuestionIndex] || '';
        const q = this.currentExamQuestions[this.currentQuestionIndex];

        if (type === '单选题' || type === '判断题') {
            // For 判断题, convert option key to option value for comparison
            let answerToSubmit = key;
            let answerToCompare = key;

            if (type === '判断题' && q.options && q.options[key]) {
                // Submit the option value (√ or ×) instead of the key (A or B)
                answerToSubmit = q.options[key];
                answerToCompare = q.options[key];
                console.log(`[DEBUG] 判断题: key=${key} → value=${answerToSubmit}`);
            }

            // Submit answer
            console.log(`[DEBUG] Submitting answer: ${answerToSubmit}`);
            this.examManager.submitAnswer(this.currentQuestionIndex, answerToSubmit);

            // Immediately re-render to show feedback
            console.log('[DEBUG] Re-rendering question to show feedback');
            this.renderQuestion();

            // Auto-jump logic: only for CORRECT answers
            const isCorrect = answerToCompare === q.answer;
            console.log(`[DEBUG] Answer check: submitted=${answerToCompare}, correct=${q.answer}, isCorrect=${isCorrect}`);

            if (isCorrect) {
                // 答对:0.5秒后自动跳转下一题
                setTimeout(() => {
                    if (this.currentQuestionIndex < this.currentExamQuestions.length - 1) {
                        this.navQuestion(1);
                    }
                }, 500);
            }
            // 答错:不自动跳转,停留在当前题让用户查看反馈
        } else if (type === '多选题') {
            // Toggle selection for multi-choice
            let ansArr = currentAns ? currentAns.split('').sort() : [];
            if (ansArr.includes(key)) {
                ansArr = ansArr.filter(k => k !== key);
            } else {
                ansArr.push(key);
            }
            this.examManager.submitAnswer(this.currentQuestionIndex, ansArr.sort().join(''));
            this.renderQuestion();
        }
    },

    navQuestion(delta) {
        const newIndex = this.currentQuestionIndex + delta;
        if (newIndex >= 0 && newIndex < this.currentExamQuestions.length) {
            this.currentQuestionIndex = newIndex;
            this.renderQuestion();
        }
    },

    startTimer() {
        clearInterval(this.timerInterval);
        const timerEl = document.getElementById('exam-timer');
        timerEl.style.color = '';
        this.timerInterval = setInterval(() => {
            this.timeLeft--;
            const m = Math.floor(this.timeLeft / 60).toString().padStart(2, '0');
            const s = (this.timeLeft % 60).toString().padStart(2, '0');
            timerEl.innerText = `${m}:${s}`;
            if (this.timeLeft < 5 * 60) {
                timerEl.style.color = 'red';
            }
            if (this.timeLeft <= 0) {
                this.finishExam();
            }
        }, 1000);
    },

    finishExam() {
        console.log('[finishExam] 考试结束,开始计算成绩');
        console.log('[finishExam] 答题情况:', this.examManager.userAnswers);

        clearInterval(this.timerInterval);
        const result = this.examManager.calculateResult();

        console.log('[finishExam] 成绩计算完成:', {
            totalScore: result.totalScore,
            maxScore: result.maxScore,
            passed: result.passed,
            errorCount: result.details.filter(d => !d.isCorrect).length
        });

        document.getElementById('result-score').innerText = result.totalScore;
        document.getElementById('result-status').innerText = result.passed ? '恭喜通过' : '未通过';
        document.getElementById('result-status').style.color = result.passed ? 'green' : 'red';
        const usedTime = (45 * 60) - this.timeLeft;
        const um = Math.floor(usedTime / 60).toString().padStart(2, '0');
        const us = (usedTime % 60).toString().padStart(2, '0');
        document.getElementById('result-time').innerText = `${um}:${us}`;
        const errorCount = result.details.filter(d => !d.isCorrect).length;
        document.getElementById('result-errors').innerText = errorCount;

        console.log('[finishExam] 显示结果页面,错题数量:', errorCount);
        this.showScreen('result-screen');
    },

    readCurrentQuestion() {
        const q = this.currentExamQuestions[this.currentQuestionIndex];
        if (!q) return;
        window.speechSynthesis.cancel();
        const text = `题目：${q.question}。${this.getOptionsText(q)}`;
        const utterance = new SpeechSynthesisUtterance(text);
        utterance.lang = 'zh-CN';
        window.speechSynthesis.speak(utterance);
    },

    getOptionsText(q) {
        if (q.type === '简答题') return '';
        if (q.type === '判断题') return 'A 正确, B 错误';
        let text = '';
        Object.entries(q.options).forEach(([k, v]) => {
            text += `选项 ${k}, ${v}。`;
        });
        return text;
    },

    showScreen(screenId) {
        document.querySelectorAll('.screen').forEach(s => s.classList.remove('active'));
        document.getElementById(screenId).classList.add('active');
    },

    initPracticeMode() {
        this.showScreen('practice-screen');
        const container = document.getElementById('practice-level-checks');
        container.innerHTML = '';
        Array.from(this.loader.levels).sort().forEach(lvl => {
            const label = document.createElement('label');
            label.className = 'checkbox-label';
            label.innerHTML = `<input type="checkbox" value="${lvl}" checked> <span>${lvl}</span>`;
            container.appendChild(label);
        });
        this.updatePracticeCounts();
        container.querySelectorAll('input').forEach(cb => {
            cb.addEventListener('change', () => this.updatePracticeCounts());
        });
    },

    updatePracticeCounts() {
        const selectedLevels = Array.from(document.querySelectorAll('#practice-level-checks input:checked')).map(cb => cb.value);
        const category = document.getElementById('practice-category-select').value;
        let selectedSources = null;
        if (category !== '全部') {
            const categories = ['基站', '地宝', '窗宝', '光学组件'];
            if (categories.includes(category)) {
                const allSources = Array.from(this.loader.sources);
                const otherCategories = categories.filter(c => c !== category);
                const filteredSources = [];
                for (const source of allSources) {
                    const hasOther = otherCategories.some(other => source.includes(other));
                    if (!hasOther) {
                        filteredSources.push(source);
                    }
                }
                selectedSources = filteredSources.length > 0 ? filteredSources : null;
            }
        }
        const types = ['单选题', '多选题', '判断题', '简答题'];
        const typeMap = { '单选题': 'single', '多选题': 'multi', '判断题': 'judge', '简答题': 'short' };
        types.forEach(t => {
            const qs = this.loader.getQuestions(selectedLevels, { [t]: 9999 }, selectedSources);
            const count = qs[t] ? qs[t].length : 0;
            document.getElementById(`count-${typeMap[t]}`).innerText = count;
        });
    },

    showErrorBook() {
        const errors = Storage.getErrors();
        this.showScreen('error-screen');
        const list = document.getElementById('error-list');
        list.innerHTML = '';
        if (errors.length === 0) {
            list.innerHTML = '<p style="text-align:center;color:#999;margin-top:20px;">暂无错题记录</p>';
            return;
        }
        errors.reverse().forEach(err => {
            const card = document.createElement('div');
            card.className = 'error-card';
            let optionsHtml = '';
            if (err.options) {
                optionsHtml = Object.entries(err.options).map(([k, v]) => `<div>${k}. ${v}</div>`).join('');
            }
            card.innerHTML = `
                <div class="meta">
                    <span>${err.type} | ${err.source_sheet}</span>
                    <span>${err.timestamp.split('T')[0]}</span>
                </div>
                <div class="q-text">${err.question}</div>
                <div class="options" style="font-size:0.9rem;color:#666;margin-bottom:10px;">${optionsHtml}</div>
                <div class="ans-row">你的答案: <span class="wrong-ans">${err.userAnswer || '未作答'}</span></div>
                <div class="ans-row">正确答案: <span class="correct-ans">${err.answer}</span></div>
            `;
            list.appendChild(card);
        });
    }
};

window.addEventListener('DOMContentLoaded', () => {
    App.init();
});
