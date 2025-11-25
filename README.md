# 技能士理论考核模拟系统 (Web版)

> 🎯 一个支持移动端的在线考试练习系统,支持Excel题库导入、模拟考试、专项练习和错题本功能

[![License: MIT](https://img.shields.io/badge/License-MIT-blue.svg)](LICENSE)
[![Version](https://img.shields.io/badge/version-2.0-green.svg)]()

## ✨ 功能特点

- 📱 **移动端优先** - 完美支持iOS和Android设备
- 📚 **Excel题库导入** - 支持自定义题库,灵活方便  
- 🎯 **多种练习模式** - 模拟考试、专项练习、错题回顾
- 🔄 **智能出题** - 加权抽题算法,科学组卷
- 📝 **四种题型** - 单选题、多选题、判断题、简答题
- 💾 **本地存储** - 错题自动保存,随时回顾
- 🔊 **语音读题** - 支持题目朗读功能
- ⏱️ **考试计时** - 45分钟倒计时,真实模拟

## 🚀 快速开始

### 在线使用

直接访问: [https://ikeke1314.github.io/web-app](https://ikeke1314.github.io/web-app)

### 本地运行

1. **克隆项目**
```bash
git clone https://github.com/your-username/web-app.git
cd web-app
```

2. **启动应用**

直接用浏览器打开 `index.html` 即可,无需安装任何依赖!

或使用本地服务器:
```bash
# 使用 Python
python -m http.server 8000

# 使用 Node.js
npx serve
```

3. **准备题库**

将Excel题库文件放入任意位置,通过界面加载即可

## 📖 使用说明

### 题库格式

Excel文件需包含以下列:

| 列名 | 说明 | 必填 |
|------|------|------|
| 考题类型 | 单选题/多选题/判断题/简答题 | ✅ |
| 题目 | 题目内容 | ✅ |
| 选项A-E | 选项内容(判断题为√/×) | 根据题型 |
| 答案 | 正确答案 | ✅ |
| 一级-六级 | 等级标记(任意非空值) | 至少一个 |
| 来源 | 题目来源/分类 | ❌ |

**示例:**

| 考题类型 | 题目 | 选项A | 选项B | 答案 | 一级 | 二级 |
|---------|------|-------|-------|------|------|------|
| 单选题 | 以下哪个是... | 选项A | 选项B | A | ✓ |  |
| 判断题 | 某某说法正确吗 | √ | × | √ |  | ✓ |

### 基本操作

1. **加载题库** - 点击"加载自定义题库",选择Excel文件
2. **选择模式** - 模拟考试或专项练习
3. **开始答题** - 选择答案后自动反馈
4. **查看错题** - 考试后查看错题本,针对性复习

详细说明请参考 [使用说明.md](使用说明.md)

## 🏗️ 项目结构

```
web-app/
├── index.html          # 主页面
├── css/
│   └── style.css       # 样式文件
├── js/
│   ├── app.js          # 应用主逻辑
│   ├── exam_core.js    # 考试核心逻辑
│   ├── excel_loader.js # Excel解析
│   └── storage.js      # 本地存储
├── README.md           # 项目说明
└── 使用说明.md         # 详细使用文档
```

## 🔧 技术栈

- **纯前端** - HTML5 + CSS3 + Vanilla JavaScript
- **Excel解析** - [SheetJS (xlsx)](https://github.com/SheetJS/sheetjs)
- **本地存储** - LocalStorage API
- **语音合成** - Web Speech API

## 📱 浏览器支持

- ✅ Chrome/Edge (推荐)
- ✅ Safari (iOS)
- ✅ Firefox
- ⚠️ 旧版IE不支持

## 🤝 贡献

欢迎提交Issue和Pull Request!

## 📄 开源协议

[MIT License](LICENSE)

## 🔗 相关链接

- [使用说明文档](使用说明.md)
- [GitHub Pages部署指南](DEPLOY.md)
- [PC版本](../pc-app)

## 📮 联系方式

如有问题或建议,请提交Issue或联系开发者

---

⭐ 如果这个项目对您有帮助,请给个Star支持一下!
