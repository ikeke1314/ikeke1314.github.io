# 技能士考试刷题系统 V3.0

V3.0 是 Windows 10/11 x64 桌面版，运行时为 .NET 8 + WPF。旧版 Python/PyQt6 文件仍保留在 `pc-app`，用于功能核对和迁移，不是新版运行依赖。

## 架构

- `src/SkillExam.Core`：抽题、筛选、答案标准化、评分和练习会话，不依赖 WPF、SQLite、ExcelDataReader 或语音组件。
- `src/SkillExam.Infrastructure`：Excel 读取、SQLite、旧 JSON 迁移、System.Speech 和 Serilog。
- `src/SkillExam.App`：WPF UI、MVVM、依赖注入和桌面交互。
- `tests`：Core、Infrastructure、真实题库和桌面布局回归测试。

用户数据库与日志默认位于：

```text
%LOCALAPPDATA%\PEGroup\SkillExam
```

首次启动时会自动加载程序目录下的 `exam_bank\PE 技能士题库.xlsx`。若程序目录包含旧版 `config.json`、`error_questions.json` 或 `practice_progress.json`，应用会先询问是否备份并迁移；迁移不修改或删除旧 JSON，且同一数据库只执行一次。

## 开发与测试

```powershell
dotnet restore .\SkillExam.sln
dotnet test .\SkillExam.sln --configuration Release
dotnet run --project .\src\SkillExam.App\SkillExam.App.csproj
```

## 发布

安装 Inno Setup 6 后，在 PowerShell 7 中执行：

```powershell
.\scripts\publish.ps1
```

脚本会先运行测试，再生成：

- `artifacts\portable\SkillExam-V3.0-win-x64`：自包含便携目录
- `artifacts\SkillExam-V3.0-win-x64-portable.zip`：便携版压缩包
- `artifacts\installer\SkillExam-V3.0-win-x64-setup.exe`：当前用户安装包

构建产物、数据库、日志与真实用户 JSON 均已由 `.gitignore` 排除，不应提交。
