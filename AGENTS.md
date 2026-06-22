# Agent开发时守则细则

- 不得修改本守则内容,除非用户明确要求维护本守则,并且维护不得丢失本守则的细节
- 本守则所在文档可能存在绑定于具体项目的信息,需要根据项目更新维护这些信息(懒维护)
- Agent的思考过程和结果输出请全程使用用户的语言,除非系统限制或用户明确指定思考/输出的语言
- 向用户确认本项目是否有缩写或简称,方便创建文件出现未明确名称时直接使用命名
- 相对路径原则:项目各处涉及项目内路径问题优先使用相对路径,避免环境依赖,保证项目迁移部署后仍正常工作
- git权限分级:[读取] git log/status/diff 可随时使用;[写入] git add/commit/push/reset/amend 需用户当次对话明确授权（如"提交"、"push"、"合并"）,授权仅限本次请求,完成后立即失效,不得跨请求复用
- git提交规范:commit标题包含版本号,body要记录功能性变化(与上一版本比较),用户未明确要求时不要动tag和release也不要push
- 维护任意md文档时,允许改写、转换说法,但是不得丢失细节,除非用户明确提出额外要求

### AGENTS.md 维护触发条件
以下情况**必须**主动更新本文件,无需用户提醒:
1. **踩坑后**:修复了不易发现的平台特性、框架陷阱、编码规范问题后,补充到对应注意事项
2. **新增全局常量/规范后**:如 `Constants.cs` 新增常量类别、新增代码规范
3. **项目结构变更后**:新增/删除/重命名了重要的源码目录或文件
4. **版本迭代时**:版本号变更涉及的同步位置、新增的本地化键同步规则
5. **技术栈变更后**:升级框架版本、新增依赖、变更构建方式
6. **用户明确要求维护本守则时**
- 项目README文档的维护应该以 `README_zh-CN.md` 为核心,最后再翻译成英文版的 `README.md`
- 项目README文档内介绍功能特性的位置不要介绍非功能性的细节
- 项目文档多语言版本维护规则:允许同一文档的不同语言版本之间通过链接相互跳转,跨文档链接要保证语言一致性(如果需要);例如 `README_zh-CN.md` 内允许通过链接跳转到不同语言版本的README,但是只链接到中文版的HELP文档(如果有)
- 应该向用户确认正在开发的项目的版本号,不允许自动迭代版本号
- 只有在用户明确重新指定新版本号后才能弃用旧版本号,新版本号要及时同步到项目源码和文档各处
- 当项目最新状态不兼容旧版本(有冲突)时,仅提醒用户注意迭代版本号,但不做版本号迭代兜底
- 工作目录下,`src/`是功能性的源码目录,其余内容则是辅助性和说明性的内容,明确目录结构,严禁混淆使用
- 必须明确开发技术栈,每当有变更技术栈的需求时需要提醒用户进行确认
- 代码内禁止使用emoji,并避免代码内的无用连续空白符
- 代码注释根据语言全部使用跨行注释,精简注释内容
- 合理组织代码保证代码结构化,避免结构混乱不利于后续开发
- 充分发挥面向对象思维,开发过程中及时封装对象
- 在模块内具有复用价值的对象和功能要提取成模板转移进独立代码文件,方便后续开发复用引入
- 不要撰写任何更新日志,避免过期内容污染项目
- 日期格式化:默认使用"yyyy-MM-dd HH:mm:ss+HH:mm"格式,除非用户明确指定使用别的格式
- 合理利用子代理(如果有)并行任务,以加快项目进程或避免已有上下文污染思考
- 项目作者[JularDepick](https://github.com/JularDepick)

---

## Gyroown 项目注意事项

### 技术栈
- C# 12 / .NET 8 / WinUI 3 (Windows App SDK 2.1)
- 源码目录: `Gyroown/` (非 `src/`)
- 解决方案: `Gyroown.slnx`

### 项目结构
```
Gyroown/                          # 解决方案根目录
├── Gyroown.slnx                  # 解决方案文件
├── AGENTS.md                     # Agent 开发守则
├── docs/                         # 文档
│   ├── DEVELOP.md                # 架构与接口详细文档
│   ├── task-queue.md             # 任务队列(版本进度)
│   ├── long-term-roadmap.md      # 长期路线图
│   ├── UserThoughts/             # 设计决策记录
│   ├── api/key-insurance.md      # 密钥保险 API 规格
│   └── release-checklist.md      # 发布检查清单
├── Gyroown/                      # 源码(非 src/)
│   ├── App.xaml / App.xaml.cs    # 入口: 单实例、路由、全局异常
│   ├── AppInfo.cs                # 版本号、应用名、支持语言
│   ├── Constants.cs              # 全局常量(路径/扩展名/加密参数/UI参数)
│   ├── MainWindow.xaml / .cs     # 主窗口: 工具栏+侧边栏+文件列表+状态栏+设置
│   ├── Models/                   # 数据模型
│   │   ├── VaultFileItem.cs      # 文件项(INotifyPropertyChanged, IconGlyph)
│   │   ├── VaultFolder.cs        # 文件夹树节点
│   │   ├── PasswordType.cs       # 密码类型枚举
│   │   ├── PasswordConfig.cs     # 密码策略配置
│   │   ├── FavoriteItem.cs       # 收藏项
│   │   ├── FileVersionRecord.cs  # 版本记录
│   │   └── SearchFilter.cs       # 高级搜索过滤器
│   ├── Services/                 # 服务层(接口 + 实现)
│   │   ├── IPasswordService / PasswordService       # PBKDF2 密码管理
│   │   ├── IEncryptionService / EncryptionService   # RSA+AES 加解密
│   │   ├── IVaultService / VaultService             # 加密文件仓库 CRUD
│   │   ├── IThemeService / ThemeService             # 主题/强调色/语言
│   │   ├── IDragDropService / DragDropService       # 拖放协调
│   │   ├── ILocalizationService / LocalizationService # INI 本地化
│   │   ├── ConfigService.cs      # 加密核心配置(切片档位)
│   │   ├── FavoritesService.cs   # 收藏夹管理
│   │   ├── VersionHistoryService.cs # 文件版本历史
│   │   ├── InsuranceService.cs   # 密钥保险 HTTP 桩(待后端 API)
│   │   ├── ImageProtection.cs    # 图片密码 XOR 加密
│   │   ├── LogService.cs         # 日志(200KB 切片)
│   │   ├── Loc.cs                # 静态本地化助手
│   │   └── JsonConfig.cs         # JsonSerializerOptions 共享配置
│   ├── Controls/                 # 自定义控件
│   │   ├── TitleBarControl       # 标题栏(搜索/筛选/设置按钮)
│   │   ├── VaultFileListView     # 文件列表(Details/Icons 双视图)
│   │   ├── VaultSidebar          # 虚拟目录树
│   │   ├── VaultStatusBar        # 状态栏(计数/路径/锁状态)
│   │   ├── FavoritesPanel        # 收藏夹面板
│   │   ├── CursorBorder.cs       # 光标样式辅助
│   │   └── Preview/              # 媒体预览
│   │       ├── FilePreviewWindow # 预览窗口(导航/多文件切换)
│   │       ├── ImagePreviewControl  # 图片缩放/平移/幻灯片
│   │       ├── TextPreviewControl   # 文本语法高亮/编码切换
│   │       └── VideoPreviewControl  # 视频/音频播放
│   ├── Views/                    # 视图控件
│   │   ├── IPasswordControl.cs   # 密码控件统一接口
│   │   ├── PasswordSetupControl  # 密码设置(含密钥保险)
│   │   ├── PasswordChangeControl # 修改密码
│   │   ├── UnlockControl         # 解锁
│   │   ├── PinPasswordControl    # 6位 PIN
│   │   ├── GesturePasswordControl # 九宫格手势
│   │   ├── CustomPasswordControl # 自定义密码
│   │   ├── PicturePasswordControl # 图片密码
│   │   └── VersionHistoryDialog  # 版本历史对话框
│   ├── lang/                     # 外部语言包(7种)
│   └── Resources/Loc/            # 嵌入式语言包(zh-CN + en-US)
└── scripts/ & tools/             # 辅助脚本和工具
```

### Constants.cs 使用规范
- 所有路径、文件扩展名、加密参数、UI 参数、阈值均在 `Constants.cs` 定义
- 新增常量必须加到 `Constants.cs`,禁止在业务代码中硬编码
- 已有常量被 10+ 个文件引用,修改时需评估影响范围

### WinUI 3 图标字体 (极易忽略)
- WinUI 3 默认图标字体是 **Segoe Fluent Icons**,不是 Segoe MDL2 Assets
- 项目使用的字形码(如 `\uE8B7` 文件夹、`\uEDE1` 导出、`\uE9A6` 适应窗口)属于 Segoe MDL2 Assets 扩展范围,在 Segoe Fluent Icons 中**不存在**
- **必须**为每个 `FontIcon` 显式指定 `FontFamily="Segoe MDL2 Assets"`,否则图标不渲染或只显示半边
- 禁止在 `App.xaml` 中定义全局 `Style TargetType="FontIcon"` — 会覆盖 WinUI 3 默认样式导致控件渲染异常(图标仅 hover 可见、只显示半边等)
- DataTemplate 内的 `FontIcon`(如 `VaultFileListView.xaml` 的文件列表)同样需要显式指定字体

### 本地化 (i18n)
- INI 文件位于 `lang/` (7种语言) 和 `Resources/Loc/` (嵌入式 zh-CN + en-US)
- 修改 INI 时**必须同步更新全部 9 个文件**: `lang/zh-CN.ini`, `zh-TW.ini`, `en-US.ini`, `en-GB.ini`, `ja-JP.ini`, `ko-KR.ini`, `fr-FR.ini` + `Resources/Loc/zh-CN.ini`, `en-US.ini`
- `AppInfo.Version` 和所有 INI 的 `[__meta__] AppVersion` 必须保持一致
- `Loc.Get("Section", "Key")` 的 Section 必须与 INI 中的 `[Section]` 段名完全匹配 — 例如 `ImportFolder` 在 `[Common]` 段则代码必须用 `Loc.Get("Common", "ImportFolder")`,不能用 `Loc.Get("MainWindow", "ImportFolder")`
- `AutomationProperties.Name` 需要在 code-behind 的 `ApplyLoc()` 方法中用 `AutomationProperties.SetName()` 设置,XAML 中的硬编码值是 fallback

### 路径处理
- Windows 上 `Path.GetDirectoryName("/myfolder")` 返回**空字符串**(不是 `"/"`),不能用它判断 Unix 风格路径的父目录
- 用 `LastIndexOf('/')` 手动截取父路径更可靠
- 所有路径比较前先 `.Replace('\\', '/')` 统一格式

### 交互事件
- WinUI 3 中 `FindItemFromSource` 用 `DataContext` 取数据项在 `x:Bind` 下可能为 null — 改用 `ListViewItem.Content` / `GridViewItem.Content`
- `OnDoubleTap` 中应设置 `e.Handled = true` 阻止事件继续传播,防止主窗口抢焦点
- `OnListTapped` 中不要在空区域取消选中 — `FindItemFromSource` 误判会导致选中后立即被取消

### 密钥安全
- 所有 `byte[]` 类型的密钥/密码用完后必须 `Array.Clear()` 清零
- 锁定保密柜时必须调用 `_vault.ClearKeys()` 清除内存中的私钥
- `EncryptionService.EncryptBlob/DecryptBlob` 中的 AES key/nonce 用 `try/finally` 包裹清零

### 构建
- Debug 构建: `dotnet build`
- Release 构建: `dotnet build -c Release`
- 编码: UTF-8 BOM
- 代码语言: 全英文(注释/变量名),中文仅限 `lang/*.ini`
- **INI 文件编码陷阱**: PowerShell 的 `Set-Content` 默认不保留 UTF-8 BOM,会导致中文/日文/韩文字符损坏。批量修改 INI 文件时必须使用 `[System.IO.File]::WriteAllText($path, $content, (New-Object System.Text.UTF8Encoding $true))` 保留 BOM

### 版本迭代检查清单
版本号变更时必须同步以下位置,遗漏任何一处会导致版本不一致:

| 位置 | 文件 | 字段 |
|------|------|------|
| C# 常量 | `Gyroown/AppInfo.cs` | `Version` |
| 外部 INI ×7 | `lang/zh-CN.ini`, `zh-TW.ini`, `en-US.ini`, `en-GB.ini`, `ja-JP.ini`, `ko-KR.ini`, `fr-FR.ini` | `[__meta__] AppVersion` |
| 嵌入 INI ×2 | `Resources/Loc/zh-CN.ini`, `Resources/Loc/en-US.ini` | `[__meta__] AppVersion` |
| 设置页面 | `MainWindow.xaml.cs` `ApplySettingsLoc()` | `VersionText.Text = AppInfo.FullVersion` (自动读取 AppInfo,无需改) |

- 新增本地化键时必须同步全部 9 个 INI 文件(7 外部 + 2 嵌入)
- 新增 `[MainWindow]` 段的键不能放到 `[Common]` 段 — `Loc.Get("MainWindow", "Key")` 只在 `[MainWindow]` 段查找
- 修改 `IVaultService` 接口时同步 `VaultService` 实现
- 修改 `IEncryptionService` / `IPasswordService` 接口时同步对应实现类
- 新增 XAML 控件时确保所有 `FontIcon` 带 `FontFamily="Segoe MDL2 Assets"`
- 新增 `FontIcon` 的字形码必须是 Segoe MDL2 Assets 范围内(`\uE7xx`-`\uExxx`),不要使用 Segoe Fluent Icons 专属码位
