class ExamManager {
    constructor(questionBank) {
        this.bank = questionBank;
        this.currentQuestions = [];
        this.userAnswers = {};
        this.examConfig = {
            '单选题': 40,
            '多选题': 10,
            '判断题': 10,
            '简答题': 3
        };
        this.scores = {
            '单选题': 1,
            '多选题': 2,
            '判断题': 1,
            '简答题': 10
        };

        this.LEVEL_WEIGHTS = {
            '一级': { '一级': 0.8, '二级': 0.2 },
            '二级': { '一级': 0.1, '二级': 0.7, '三级': 0.2 },
            '三级': { '二级': 0.1, '三级': 0.7, '四级': 0.2 },
            '四级': { '三级': 0.1, '四级': 0.7, '五级': 0.2 },
            '五级': { '四级': 0.1, '五级': 0.7, '六级': 0.2 },
            '六级': { '五级': 0.1, '六级': 0.9 }
        };
    }

    startExam(targetLevel, selectedSources = null) {
        this.userAnswers = {};
        const levelWeights = this.LEVEL_WEIGHTS[targetLevel] || { [targetLevel]: 1.0 };

        let finalQuestions = [];
        const seenQuestions = new Set();

        for (const [qType, totalCount] of Object.entries(this.examConfig)) {
            let typeQuestions = [];
            const countsPerLevel = {};
            let currentTotal = 0;

            const sortedLevels = Object.keys(levelWeights).sort();

            // Calculate counts
            sortedLevels.forEach(lvl => {
                const w = levelWeights[lvl];
                const count = Math.floor(totalCount * w);
                countsPerLevel[lvl] = count;
                currentTotal += count;
            });

            // Distribute remainder
            const remainder = totalCount - currentTotal;
            if (remainder > 0) {
                if (countsPerLevel[targetLevel] !== undefined) {
                    countsPerLevel[targetLevel] += remainder;
                } else {
                    countsPerLevel[sortedLevels[0]] += remainder;
                }
            }

            // Fetch questions
            for (const [lvl, count] of Object.entries(countsPerLevel)) {
                if (count <= 0) continue;

                // Get all available questions for this level and type
                const tempConfig = { [qType]: 9999 };
                const qsDict = this.bank.getQuestions(lvl, tempConfig, selectedSources);

                if (qsDict[qType] && qsDict[qType].length > 0) {
                    const availableQs = qsDict[qType];
                    const uniqueAvailable = availableQs.filter(q => !seenQuestions.has(q.question));

                    let selected = [];
                    if (uniqueAvailable.length >= count) {
                        selected = this.shuffle(uniqueAvailable).slice(0, count);
                    } else {
                        selected = uniqueAvailable;
                    }

                    selected.forEach(q => seenQuestions.add(q.question));
                    typeQuestions.push(...selected);
                }
            }

            typeQuestions = this.shuffle(typeQuestions);
            finalQuestions.push(...typeQuestions);
        }

        this.currentQuestions = finalQuestions;
        return this.currentQuestions;
    }

    submitAnswer(index, answer) {
        if (index >= 0 && index < this.currentQuestions.length) {
            this.userAnswers[index] = answer;
        }
    }

    calculateResult() {
        let totalScore = 0;
        let maxScore = 0;
        const results = [];

        console.log('[calculateResult] 开始计算成绩,总题数:', this.currentQuestions.length);

        this.currentQuestions.forEach((q, i) => {
            const userAns = (this.userAnswers[i] || "").trim();
            const correctAns = (q.answer || "").trim();
            const points = this.scores[q.type] || 0;

            let isCorrect = false;
            
            // 判断题特殊处理:将选项键转换为选项值进行比较
            if (q.type === '判断题') {
                // 如果题库答案是选项键(A/B),转换为选项值(√/×)
                let normalizedCorrectAns = correctAns;
                if (q.options && (correctAns === 'A' || correctAns === 'B')) {
                    normalizedCorrectAns = q.options[correctAns];
                    console.log(`[判断题] 题目${i+1}: 答案从选项键"${correctAns}"转换为选项值"${normalizedCorrectAns}"`);
                }
                isCorrect = userAns === normalizedCorrectAns;
                console.log(`[判断题] 题目${i+1}: 用户答案="${userAns}", 正确答案="${normalizedCorrectAns}", 是否正确=${isCorrect}`);
            } else if (q.type === '简答题') {
                // Strict match for now as per Python code
                isCorrect = userAns === correctAns;
                console.log(`[简答题] 题目${i+1}: 用户答案="${userAns}", 正确答案="${correctAns}", 是否正确=${isCorrect}`);
            } else {
                isCorrect = userAns === correctAns;
                console.log(`[${q.type}] 题目${i+1}: 用户答案="${userAns}", 正确答案="${correctAns}", 是否正确=${isCorrect}`);
            }

            if (isCorrect) {
                totalScore += points;
            } else {
                // 只有真正答错的才加入错题本
                console.log(`[错题记录] 题目${i+1}答错,加入错题本:`, {
                    question: q.question.substring(0, 30) + '...',
                    userAnswer: userAns,
                    correctAnswer: correctAns,
                    type: q.type
                });
                Storage.addError(q, userAns);
            }

            maxScore += points;

            results.push({
                index: i,
                question: q,
                userAnswer: userAns,
                isCorrect: isCorrect,
                points: isCorrect ? points : 0
            });
        });

        console.log('[calculateResult] 计算完成: 总分=', totalScore, '/', maxScore, ', 错误数=', results.filter(r => !r.isCorrect).length);

        return {
            totalScore,
            maxScore,
            passed: totalScore >= 80,
            details: results
        };
    }

    shuffle(array) {
        const newArr = [...array];
        for (let i = newArr.length - 1; i > 0; i--) {
            const j = Math.floor(Math.random() * (i + 1));
            [newArr[i], newArr[j]] = [newArr[j], newArr[i]];
        }
        return newArr;
    }
}
