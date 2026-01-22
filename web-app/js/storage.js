const Storage = {
    KEYS: {
        ERROR_BOOK: 'skill_exam_error_book',
        PRACTICE_PROGRESS: 'skill_exam_practice_progress',
        SETTINGS: 'skill_exam_settings'
    },

    save(key, data) {
        try {
            localStorage.setItem(key, JSON.stringify(data));
        } catch (e) {
            console.error('Storage save failed:', e);
            alert('存储空间不足，无法保存进度');
        }
    },

    load(key, defaultValue = null) {
        try {
            const item = localStorage.getItem(key);
            return item ? JSON.parse(item) : defaultValue;
        } catch (e) {
            console.error('Storage load failed:', e);
            return defaultValue;
        }
    },

    // Error Book Specifics
    addError(question, userAnswer) {
        const errors = this.load(this.KEYS.ERROR_BOOK, []);
        console.log('[Storage.addError] 当前错题本数量:', errors.length);

        // Check if already exists (by question text)
        const exists = errors.some(e => e.question === question.question);
        if (!exists) {
            const newError = {
                ...question,
                userAnswer,
                timestamp: new Date().toISOString()
            };
            errors.push(newError);
            this.save(this.KEYS.ERROR_BOOK, errors);
            console.log('[Storage.addError] 新增错题:', {
                question: question.question.substring(0, 30) + '...',
                userAnswer,
                type: question.type,
                totalErrors: errors.length
            });
        } else {
            console.log('[Storage.addError] 错题已存在,跳过:', question.question.substring(0, 30) + '...');
        }
    },

    getErrors() {
        return this.load(this.KEYS.ERROR_BOOK, []);
    },

    clearErrors() {
        this.save(this.KEYS.ERROR_BOOK, []);
    },

    // Settings / Preferences
    saveSettings(settings) {
        const current = this.load(this.KEYS.SETTINGS, {});
        const newSettings = { ...current, ...settings };
        this.save(this.KEYS.SETTINGS, newSettings);
    },

    getSettings() {
        return this.load(this.KEYS.SETTINGS, {});
    }
};
