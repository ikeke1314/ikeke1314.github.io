# GitHub Pages 部署

项目使用 `.github/workflows/pages.yml` 自动构建和发布，无需提交 `dist/`。Vite 的 `base` 是 `./`，因此构建产物同时兼容用户根站点和仓库子路径。

## 自动发布

推送到 `main` 后，GitHub Actions 会依次执行：

1. `npm ci`
2. `npm run typecheck`
3. `npm run test`
4. `npm run build`
5. `npm run test:assets`
6. 上传 `dist/` 并发布 GitHub Pages

在仓库 **Settings → Pages** 中，Source 应设置为 **GitHub Actions**。部署进度可在仓库 **Actions** 页面查看。

## 本地发布前验收

```powershell
cd "D:\Git code\答题系统\web-app"
npm ci
npx playwright install chromium webkit
npm run check
```

其中 `test:assets` 会验证入口和延迟加载的 CSS/JS 使用相对路径，并确认文件真实存在，从而防止 Pages 资源 404。

## 本地预览生产构建

```powershell
npm run build
npx vite preview
```

部分浏览器会限制 `file://` 下的 ES Module，因此不要直接双击 `dist/index.html`。

## 数据说明

Excel 只在浏览器本地解析，不上传到 GitHub 或服务器。部署站点不包含 `pc-app` 题库，用户需要自行选择 Excel 文件。
