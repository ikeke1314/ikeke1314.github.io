# GitHub Pages 部署指南

> 📘 将您的技能士考试系统部署到GitHub Pages,让任何人都能在线访问

## 🎯 部署概述

**GitHub Pages** 是 GitHub 提供的免费静态网站托管服务,非常适合部署纯前端项目。

**访问地址**: `https://你的用户名.github.io/仓库名`

**费用**: 完全免费 ✅

## 📋 前置要求

- ✅ GitHub账号([注册地址](https://github.com/signup))
- ✅ Git已安装([下载地址](https://git-scm.com/downloads))
- ✅ 项目代码已准备好

## 🚀 方法一:通过Git命令行部署(推荐)

### 步骤1:创建GitHub仓库

1. 登录 [GitHub](https://github.com)
2. 点击右上角 **"+"** → **"New repository"**
3. 填写仓库信息:
   - **Repository name**: `skill-exam` (或其他名称)
   - **Description**: 技能士理论考核模拟系统
   - **Public** 或 **Private** (都可以)
   - ❌ **不要**勾选 "Add a README file"
4. 点击 **"Create repository"**

### 步骤2:初始化本地仓库

打开命令行(PowerShell或CMD),进入项目目录:

```powershell
# 进入项目文件夹
cd C:\Users\<user>\Desktop\google\web-app

# 初始化Git仓库
git init

# 添加所有文件
git add .

# 提交代码
git commit -m "Initial commit: 技能士考试系统v2.0"
```

### 步骤3:推送到GitHub

```powershell
# 添加远程仓库(替换YOUR_USERNAME为您的GitHub用户名)
git remote add origin https://github.com/YOUR_USERNAME/skill-exam.git

# 推送代码
git branch -M main
git push -u origin main
```

**提示**: 如果提示输入用户名密码,需要使用**Personal Access Token**:

1. GitHub → Settings → Developer settings → Personal access tokens → Generate new token
2. 勾选 `repo` 权限
3. 复制生成的token
4. 推送时用token作为密码

### 步骤4:启用GitHub Pages

1. 打开您的GitHub仓库页面
2. 点击 **Settings**(设置)
3. 左侧菜单找到 **Pages**
4. **Source** 选择:
   - Branch: `main`
   - Folder: `/ (root)`
5. 点击 **Save**

⏱️ **等待1-2分钟**,刷新页面会看到:

```
✅ Your site is published at https://YOUR_USERNAME.github.io/skill-exam/
```

### 步骤5:访问您的网站

在浏览器打开: `https://YOUR_USERNAME.github.io/skill-exam/`

🎉 **部署完成!**

## 🌐 方法二:通过GitHub网页直接上传

适合不熟悉Git命令的用户

### 步骤1:创建仓库

同上方法一的步骤1

### 步骤2:上传文件

1. 进入新创建的仓库
2. 点击 **"uploading an existing file"** 或 **"Add file"** → **"Upload files"**
3. 将`web-app`文件夹下的所有文件拖入上传区
   - `index.html`
   - `css/` 文件夹
   - `js/` 文件夹  
   - `README.md` 等
4. 填写提交信息: `Initial commit`
5. 点击 **"Commit changes"**

### 步骤3:启用GitHub Pages

同上方法一的步骤4

## 🔄 更新网站

### 使用Git更新

```powershell
# 修改代码后
git add .
git commit -m "更新说明"
git push
```

### 网页直接更新

1. 点击要修改的文件
2. 点击铅笔图标✏️编辑
3. 修改后点击 **"Commit changes"**

**⏱️ 等待1-2分钟生效**

## 📱 绑定自定义域名(可选)

### 步骤1:购买域名

在域名服务商购买域名(如:Namesilo、Cloudflare、阿里云)

### 步骤2:添加DNS记录

在域名管理面板添加CNAME记录:

| 类型 | 名称 | 值 |
|------|------|---|
| CNAME | www | YOUR_USERNAME.github.io |
| CNAME | @ | YOUR_USERNAME.github.io |

### 步骤3:在GitHub配置

1. GitHub仓库 → Settings → Pages
2. **Custom domain** 填入: `yourdomain.com`
3. 勾选 **Enforce HTTPS**
4. 保存

**⏱️ 等待DNS生效(15分钟-24小时)**

## ⚙️ 高级配置

### 使用子目录部署

如果仓库名不是`用户名.github.io`,访问路径会包含仓库名:

```
https://用户名.github.io/仓库名/
```

**修改方式**:

1. 仓库改名为 `用户名.github.io`
2. 访问: `https://用户名.github.io/`

### 启用HTTPS

默认已启用,如未启用:

Settings → Pages → 勾选 **Enforce HTTPS**

### 404页面

创建 `404.html`:

```html
<!DOCTYPE html>
<html>
<head>
    <title>404 - 页面未找到</title>
    <meta http-equiv="refresh" content="0;url=/">
</head>
<body>
    <p>正在跳转...</p>
</body>
</html>
```

## 🐛 常见问题

### Q1: 推送时提示403错误

**A:** 需要使用Personal Access Token代替密码
- Settings → Developer settings → Personal access tokens
- 生成token并勾选`repo`权限
- 推送时用token作为密码

### Q2: 网站404错误

**A:** 检查:
1. GitHub Pages是否已启用
2. 源分支是否选择正确(main或gh-pages)
3. `index.html`是否在根目录

### Q3: 修改后没有更新

**A:**
1. 强制刷新浏览器: `Ctrl + F5`
2. 清除浏览器缓存
3. 等待1-2分钟让GitHub Pages重新构建

### Q4: CSS/JS文件404

**A:** 检查文件路径:
- 确保使用**相对路径**: `css/style.css`
- 而非绝对路径: `/css/style.css`

### Q5: 如何删除网站

**A:**
1. Settings → Pages → Source选择None
2. 或直接删除整个仓库

## 📊 访问统计(可选)

### 添加Google Analytics

1. 注册 [Google Analytics](https://analytics.google.com/)
2. 获取跟踪ID
3. 在`index.html`的`<head>`中添加:

```html
<!-- Google Analytics -->
<script async src="https://www.googletagmanager.com/gtag/js?id=G-XXXXXXXXXX"></script>
<script>
  window.dataLayer = window.dataLayer || [];
  function gtag(){dataLayer.push(arguments);}
  gtag('js', new Date());
  gtag('config', 'G-XXXXXXXXXX');
</script>
```

## 🔗 相关资源

- 📖 [GitHub Pages官方文档](https://docs.github.com/pages)
- 🎓 [Git教程](https://git-scm.com/book/zh/v2)
- 💡 [Markdown语法](https://www.markdownguide.org/)

## 📞 获取帮助

- GitHub Pages问题: [GitHub Community](https://github.community/)
- Git使用问题: [Stack Overflow](https://stackoverflow.com/questions/tagged/git)
- 项目问题: [提交Issue](https://github.com/YOUR_USERNAME/skill-exam/issues)

---

**祝您部署顺利!** 🚀

如有任何问题,欢迎提交Issue或联系开发者。
