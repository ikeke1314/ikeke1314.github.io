# V2.3 到 V3.0 桌面界面对照

本清单以 `main_code/ui_build.py`、`技术文档-05-UI界面.md` 和实际运行的 V2.3 为基准。V3.0 只更新控件视觉，不改变主要层级、位置和操作顺序。

## 实机取证

2026-07-15 使用旧版可执行文件的隔离副本完成截图，未写入仓库中的真实配置、错题和练习进度：

- `legacy-home.png`：首页
- `legacy-practice.png`：专项练习
- `legacy-question.png`：答题页
- `legacy-result.png`：结果页
- `legacy-error-review.png`：错题复习状态

截图保存在本次任务的 Codex visualizations 目录，不作为源代码或发布资源提交。

## 控件与位置映射

| 旧版控件 | V3.0 控件 | V3.0 位置与约束 |
| --- | --- | --- |
| 首页标题 `QLabel` | `TextBlock` | 页面顶部居中，仅显示 `V3.0` |
| 加载题库 `QPushButton` | WPF `Button` | 标题下方居中，Tab 顺序第 1 |
| 题库状态 | 紧凑 `TextBlock` 区域 | 加载按钮下方，显示题数、Sheet 和路径，不使用状态卡片 |
| 等级 `QComboBox` | WPF `ComboBox` | 同一水平选择区左侧，Tab 顺序第 2 |
| 类别 `QComboBox` | WPF `ComboBox` | 同一水平选择区右侧，Tab 顺序第 3 |
| 出题规则 `QLabel` | 次要 `TextBlock` | 选择区正下方，保持单行优先 |
| 首页三个主按钮 | 四个同宽 WPF `Button` | 开始考试、专项练习、错题本、设置纵向居中排列 |
| 练习等级滚动列表 | `Border` + `ScrollViewer` + `CheckBox` | 左侧固定尺寸列表 |
| 练习类别滚动列表 | `Border` + `ScrollViewer` + `CheckBox` | 右侧与等级列表等宽等高 |
| 刷新数量按钮 | WPF `Button` | 两个列表下方居中 |
| 四个练习题型按钮 | 四个 WPF `Button` | 同宽纵向排列并直接显示可用题数 |
| 左侧题号导航 | `QuestionNavigator` | 默认展开，宽 300px，独立滚动，按题型分组并用紧凑网格展示 |
| 进度和倒计时 | `ExamHeader` | 答题区顶部同一水平行，包含模式、进度、题型和倒计时 |
| 题目 `QLabel` | 换行 `TextBlock` | 右侧主区域顶部，来源和等级紧邻显示 |
| 读题按钮 | WPF `Button` | 题目和来源下方左侧 |
| 单选/判断按钮 | 语义化 `RadioButton` | 保留圆点语义，不使用大卡片选项 |
| 多选按钮 | 语义化 `CheckBox` | 保留复选框语义，不使用大卡片选项 |
| 反馈标签 | `FeedbackBanner` | 选项下方轻量反馈条，颜色之外同时显示符号和文字 |
| 上一题/提交/下一题 | WPF 底部按钮行 | 顺序保持上一题、确认/查看、下一题 |
| 交卷/退出 | WPF 底部右侧按钮 | 考试显示交卷，练习或错题复习显示退出当前模式 |
| 结果页标签和分数 | 居中 `StackPanel` | 保持单一居中结果结构，不使用数据分析网格 |

## 可访问性状态

题号导航同时使用填充色、边框、状态圆点和 `AutomationProperties.Name`：未答为灰色，当前题为蓝色，正确为绿色，错误为红色。键盘焦点另有高对比度外框，不能只靠颜色判断状态。
