# 技能士理论考核模拟系统（Web）

React + TypeScript + Vite 实现的纯静态刷题程序。浏览器本地解析 Excel，不上传题库、不需要后端，可部署到 GitHub Pages 的根域名或仓库子路径。

## 功能

- 导入 `.xlsx` / `.xls`，多选 Sheet，默认排除名称含“透视”的 Sheet
- 一级至六级、基站/地宝/窗宝/光学组件筛选
- 单选、多选、判断、简答四种题型
- 按原有等级权重生成 45 分钟模拟考试
- 实时答案反馈、答对自动下一题、答错停留、手动交卷和超时交卷
- 专项练习题数统计与中断恢复
- 成绩页和版本化错题本
- 手动读题，无自动语音
- 360px 起的响应式布局、键盘操作、safe-area 与 `100dvh`

详细对应关系见 [功能对照清单.md](功能对照清单.md)，操作说明见 [使用说明.md](使用说明.md)。

## 本地开发

要求 Node.js 20.19+ 或 22.12+（当前项目已在 Node.js 24 验证）。

```powershell
cd "D:\Git code\答题系统\web-app"
npm ci
npm run dev
```

开发服务默认地址由 Vite 输出。页面启动后手动导入题库；程序不会把整份题库写入 LocalStorage。

## 验证

```powershell
npm run typecheck
npm run test
npm run build
npm run test:assets
npx playwright install chromium webkit
npm run test:e2e
```

`npm run check` 会依次运行全部验证。Playwright 使用真实题库 `../pc-app/exam_bank/PE 技能士题库.xlsx`，覆盖 1440×900 Windows Chromium、390×844 iPhone WebKit、412×915 Android Chromium。

## 目录

```text
web-app/
├─ src/
│  ├─ components/        React 组件
│  ├─ domain/            无 DOM 依赖的解析、组卷、评分、迁移模块
│  ├─ state/             useReducer + Context 状态
│  ├─ App.tsx
│  └─ styles.css
├─ tests/
│  ├─ e2e/               Playwright 真实题库流程
│  └─ *.test.*           Vitest / React Testing Library
├─ index.html
├─ vite.config.ts
└─ playwright.config.ts
```

SheetJS 固定为 npm 锁定依赖 `xlsx@0.20.3`（官方 tarball），不再使用运行时 CDN；解析代码只在用户选择 Excel 后延迟加载。

## 旧版回滚

重构前版本仍完整保留在 Git 历史的 `master` 分支与提交 `c6b1654`。需要临时查看旧版时可新建回滚分支：

```powershell
git switch -c rollback/web-app-v2 c6b1654
```

不要在有未提交改动时直接切换。当前重构分支是 `codex/web-app-refactor`。

## 部署

构建后静态文件位于 `dist/`，HTML 中的 JS/CSS 使用 `./assets/...` 相对路径。仓库内置 GitHub Actions Pages 工作流，推送到 `main` 后自动验证、构建和部署，详见 [DEPLOY.md](DEPLOY.md)。
