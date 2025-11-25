class ExcelLoader {
    constructor() {
        this.questions = [];
        this.levels = new Set();
        this.sources = new Set();
    }

    async loadFile(file) {
        return new Promise((resolve, reject) => {
            const reader = new FileReader();
            reader.onload = (e) => {
                try {
                    const data = new Uint8Array(e.target.result);
                    const workbook = XLSX.read(data, { type: 'array' });
                    this.parseWorkbook(workbook);
                    resolve(this.questions);
                } catch (err) {
                    reject(err);
                }
            };
            reader.onerror = reject;
            reader.readAsArrayBuffer(file);
        });
    }

    extractCategory(sheetName) {
        // Extract category keywords from sheet name
        const categoryMap = {
            '基站': '基站',
            '地宝': '地宝',
            '窗宝': '窗宝',
            '光学组件': '光学组件',
            '普朗克': '基站', // 普朗克 is related to 基站
        };

        // Check if sheet name contains any category keyword
        for (const [keyword, category] of Object.entries(categoryMap)) {
            if (sheetName.includes(keyword)) {
                return category;
            }
        }

        // If no keyword found, return the original sheet name
        return sheetName;
    }

    parseWorkbook(workbook, selectedSheets = null) {
        this.questions = [];
        this.levels = new Set();
        this.sources = new Set();

        const sheetsToProcess = selectedSheets || workbook.SheetNames;

        sheetsToProcess.forEach(sheetName => {
            if (sheetName.includes('透视')) return;

            const sheet = workbook.Sheets[sheetName];
            const jsonData = XLSX.utils.sheet_to_json(sheet, { range: 3 });

            jsonData.forEach(row => {
                const cleanRow = {};
                Object.keys(row).forEach(k => cleanRow[k.trim()] = row[k]);

                if (!cleanRow['题目'] || !cleanRow['答案']) return;

                const qType = cleanRow['考题类型']?.trim();
                const questionText = cleanRow['题目']?.trim();
                const answer = cleanRow['答案']?.trim();

                if (!qType || !questionText || !answer) return;

                const options = {};
                ['A', 'B', 'C', 'D', 'E'].forEach(opt => {
                    const key = `选项${opt}`;
                    if (cleanRow[key]) {
                        options[opt] = String(cleanRow[key]).trim();
                    }
                });

                const qLevels = [];
                ['一级', '二级', '三级', '四级', '五级', '六级'].forEach(lvl => {
                    if (cleanRow[lvl]) {
                        qLevels.push(lvl);
                        this.levels.add(lvl);
                    }
                });

                let source = sheetName;
                if (cleanRow['来源']) {
                    source = String(cleanRow['来源']).trim();
                } else {
                    // Extract category from sheet name
                    source = this.extractCategory(sheetName);
                }

                // Debug logging
                if (!this.sources.has(source)) {
                    console.log(`New source found: "${source}" from sheet: "${sheetName}"`);
                }
                this.sources.add(source);

                this.questions.push({
                    type: qType,
                    question: questionText,
                    options: options,
                    answer: answer,
                    levels: qLevels,
                    source_sheet: source
                });
            });
        });

        console.log(`Total questions loaded: ${this.questions.length}`);
        console.log(`Available sources:`, Array.from(this.sources));
    }

    getQuestions(levels, typeCounts, selectedSources = null) {
        const targetLevels = Array.isArray(levels) ? levels : [levels];

        let levelQs = this.questions.filter(q =>
            q.levels.some(l => targetLevels.includes(l))
        );

        if (selectedSources && selectedSources.length > 0) {
            levelQs = levelQs.filter(q => selectedSources.includes(q.source_sheet));
        }

        const result = {};

        for (const [type, count] of Object.entries(typeCounts)) {
            const typeQs = levelQs.filter(q => q.type === type);

            if (typeQs.length < count) {
                // If asking for 9999 (all), or just not enough, return all
                result[type] = this.shuffle(typeQs);
            } else {
                result[type] = this.shuffle(typeQs).slice(0, count);
            }
        }

        return result;
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
