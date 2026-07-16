# 技能士考试刷题系统 V3.1

Windows 10/11 x64 桌面应用，使用 .NET 8、WPF、SQLite 和 MVVM 构建。发布物为自包含便携版，无需安装 .NET 运行时，也不生成安装包。

用户操作请查看 [操作说明](./操作说明.md)，架构说明和 Android 迁移边界请查看 [开发文档](./开发文档.md)。

## 项目结构

- `src/SkillExam.Core`：抽题、答案判定、评分和练习会话。
- `src/SkillExam.Infrastructure`：Excel 题库、SQLite、旧数据迁移、语音和日志。
- `src/SkillExam.App`：WPF 界面、MVVM 和桌面交互。
- `tests`：核心逻辑、基础设施、交互和布局回归测试。
- `exam_bank`：随便携版附带的默认题库。
- `scripts/publish.ps1`：仅生成便携目录和 ZIP 的发布脚本。

## 开发与测试

在 PowerShell 7 中执行：

```powershell
dotnet restore .\SkillExam.sln
dotnet test .\SkillExam.sln --configuration Release
dotnet run --project .\src\SkillExam.App\SkillExam.App.csproj
```

## 生成便携版

```powershell
.\scripts\publish.ps1
```

脚本会先清理旧发布物、运行测试，再生成：

- `artifacts\portable\SkillExam-V3.1-win-x64`：可直接运行的便携目录。
- `artifacts\SkillExam-V3.1-win-x64-portable.zip`：便携版压缩包。

发布目录仅包含应用、默认题库、操作说明和开发文档，不生成安装程序。用户数据库、日志和备份默认保存在 `%LOCALAPPDATA%\PEGroup\SkillExam`，不会写入程序目录。
