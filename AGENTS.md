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
- 前端网页项目不要使用浏览器原生弹窗提醒,而是使用自定义飘窗提醒
- 前端网页项目不要使用浏览器原生弹窗进行二次确认,而是在原按钮上执行"替换为确认按钮-3s内点击确认-超时回归初始状态"流程
- 前端网页项目默认隐藏浏览器侧边滚动条(如果有),然后告知用户(允许用户回退该操作)
- 前端网页项目的表格、select控件默认文本水平居中
- 日期格式化:默认使用"yyyy-MM-dd HH:mm:ss+HH:mm"格式,除非用户明确指定使用别的格式
- 合理利用子代理(如果有)并行任务,以加快项目进程或避免已有上下文污染思考
- 项目作者[JularDepick](https://github.com/JularDepick)
- 前后端项目请在后端代码注释头、每一个前端页面底部标注作者信息,并在控制前端页面的代码里定义宏或常量方便开发者动态替换前端页面作者信息

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

---

## UI 对齐 Windows 文件资源管理器 开发细则

> 目标:将 Gyroown 的文件管理 UI 逐项对齐到 Windows 11 文件资源管理器的交互细节和视觉规范。
> 每个条目标注 [现有] 表示已实现、[缺失] 表示待开发、[改进] 表示需优化。

### 1. 导航区域 (TitleBar 区)

#### 1.1 导航按钮 [现有]
- 在标题栏左侧添加 **返回 / 前进 / 上级** 三个导航按钮
- 返回按钮 Glyph: `\uE72B` (Back), 前进: `\uE72A` (Forward), 上级: `\uE74A` (Up)
- 返回/前进维护导航历史栈 `_navHistory` (List\<string\>) + `_navIndex` (int)
- 上级按钮等同于现有 Backspace 功能 (`GoToParent`)
- 按钮在无法操作时(无历史/已在根目录)应 Disabled 而非隐藏
- 快捷键: Alt+Left 返回, Alt+Right 前进, Alt+Up 上级

#### 1.2 面包屑地址栏 [现有]
- 使用 WinUI 3 `BreadcrumbBar` 控件替换现有路径显示
- 位置: 导航按钮右侧,搜索框左侧
- 显示当前路径的每一级文件夹名,用 chevron `\uE76C` 分隔
- 点击面包屑中任意节点可直接跳转到该层级
- 最后一个节点(当前文件夹)高亮显示,点击不触发导航
- 路径过长时自动折叠左侧节点,显示省略号 `\uE712` + Flyout 展开
- 双击面包屑空白区域切换为可编辑文本框模式(输入路径直接跳转)
- 文本框模式按 Enter 确认跳转,按 Esc 恢复面包屑模式
- 根目录显示为保险柜名称(Vault 图标 + 本地化名称)

#### 1.3 搜索框 [现有]
- 位置保持在标题栏右侧
- 改进: 搜索时在文件列表区域实时高亮匹配文字(不是过滤,是高亮)
- 改进: 搜索框获得焦点时显示最近搜索历史(已有),但需增加清除单条历史的按钮
- 改进: 搜索无结果时显示更友好的空状态(图标+文字+清除筛选按钮)

### 2. 工具栏 (CommandBar 区)

#### 2.1 按钮布局 [现有,改进]
- 现有按钮: 新建文件夹、导入、导入文件夹、导出、剪切入、剪切出、删除、锁定
- 改进: 按钮分组更清晰,参考资源管理器的分组逻辑:
  - **新建组**: 新建文件夹
  - **剪贴板组**: 剪切入 / 剪切出 (对应资源管理器的剪切/粘贴位置)
  - **导入导出组**: 导入 / 导入文件夹 / 导出
  - **操作组**: 删除
  - **安全组**: 锁定
- 每组之间用 `AppBarSeparator` 分隔(现有)

#### 2.2 视图切换按钮 [现有]
- 现有: Details / Icons 两个 ToggleButton 在文件列表顶部
- 改进: 移到工具栏右侧(与资源管理器一致),使用下拉菜单:
  - 详细信息 (现有 `\uE8A1`)
  - 大图标 (现有 `\uE8A9`)
  - 中图标 [新增] (`\uE8A9` 缩小)
  - 小图标 [新增] (`\uE8A9` 更小)
  - 列表 [新增] (`\uE8A1` 紧凑)
- 使用 `DropDownButton` + `MenuFlyout` 实现
- 记住用户选择到加密配置(ConfigService)

#### 2.3 排序按钮 [现有]
- 在工具栏添加排序下拉按钮,图标 `\uE8CB` (Sort)
- 菜单项:
  - 名称 (升序/降序)
  - 大小 (升序/降序)
  - 类型 (升序/降序)
  - 修改日期 (升序/降序)
  - 创建日期 [新增] (升序/降序)
- 当前排序项旁边显示勾选标记
- 与列头点击排序同步状态

#### 2.4 筛选按钮 [现有,改进]
- 现有: 高级搜索按钮(Flyout 含类型/大小/日期筛选)
- 改进: 当有活跃筛选时,按钮旁显示筛选数量 badge
- 改进: 筛选 Flyout 增加"清除所有筛选"按钮(现有)并增加当前筛选条件的标签展示

### 3. 侧边栏 (NavigationPane 区)

#### 3.1 整体结构 [现有,改进]
- 现有: 收藏夹面板 + 分隔线 + 文件夹树
- 改进: 参考资源管理器的侧边栏结构,增加以下区域:

#### 3.2 快速访问区域 [现有]
- 在文件夹树上方增加"快速访问"区域
- 显示最近打开的文件(最近 10 个),按打开时间倒序
- 使用 `\uE730` (Clock) 图标
- 点击直接打开文件预览
- 数据持久化到加密配置

#### 3.3 收藏夹面板 [现有,改进]
- 保持现有功能: 收藏列表、分组、拖拽排序
- 改进: 收藏项双击展开/折叠分组(而非单击)
- 改进: 空收藏夹时显示引导文字"拖拽文件到此处添加收藏"

#### 3.4 文件夹树 [现有,改进]
- 现有: TreeView 显示虚拟文件夹层级
- 改进: 节点前的展开/折叠 chevron 改为 `\uE76C` (右箭头) / `\uE76D` (下箭头)
- 改进: 文件夹节点显示子文件夹数量(灰色小字)
- 改进: 右键文件夹节点显示上下文菜单(新建子文件夹、重命名、删除)
- 改进: 拖拽文件到文件夹节点可移动文件到该文件夹
- 改进: 选中文件夹时,文件列表区域同步显示该文件夹内容(现有)

#### 3.5 侧边栏宽度调节 [现有]
- 现有: 拖拽分隔条调节宽度(180-400px)
- 改进: 双击分隔条折叠/展开侧边栏
- 改进: 折叠时只显示图标(收藏夹图标 + 根文件夹图标)
- 改进: 折叠状态下点击图标临时展开(Overlay 模式)

### 4. 文件列表区 (Details View)

#### 4.1 列头 [现有]
- 现有列: 名称、大小、类型、修改日期
- 新增列: 创建日期(默认隐藏)
- 改进: 右键列头显示"选择列"菜单,可显示/隐藏各列
- 改进: 列头排序箭头改为 `\uE70C` (上) / `\uE70D` (下),更小更精致
- 改进: 列头 hover 时显示浅色背景高亮
- 改进: 列头点击排序时添加短暂的动画过渡

#### 4.2 列宽调节 [现有]
- 现有: 拖拽列分隔线调节宽度(最小 40px)
- 改进: 双击列分隔线自动调整列宽以适应内容(autofit)
- 改进: 分隔线 hover 时显示蓝色高亮指示条(2px 宽)
- 改进: 拖拽时显示蓝色指示线(与资源管理器一致)

#### 4.3 行样式 [现有]
- 行高度: 28px(与资源管理器一致)
- 行 hover 背景: 使用 `ControlFillColorSecondaryBrush` 作为 hover 高亮
- 选中行背景: 使用 `AccentFillColorDefaultBrush` 的 20% 透明度
- 选中行文字: 选中状态下文字保持原色(不是白色)
- 交替行背景: 不使用(与资源管理器一致)
- 文件夹始终排在文件前面(分组显示),无论排序方式

#### 4.4 图标 [现有]
- 文件/文件夹图标大小: 16px(详细信息视图),现有正确
- 图标与文件名间距: 8px(现有正确)
- 改进: 为更多文件类型添加专用图标:
  - 压缩文件 (`\uE8B5` Archive)
  - 可执行文件 (`\uE7EF` App)
  - Office 文档 (`\uE8A5` Document)
  - 代码文件 (`\uE8F4` Code)
  - 字体文件 (`\uE8D2` Font)

#### 4.5 文件名列 [现有]
- 文件名过长时使用省略号(现有 `TextTrimming="CharacterEllipsis"`)
- 改进: 省略号出现在中间而非末尾(与资源管理器一致): `TextTrimming="CharacterEllipsis"` + `TextAlignment="Left"`
- 改进: 文件名列支持水平滚动查看完整名称

### 5. 文件列表区 (Icons / Grid View)

#### 5.1 图标大小 [现有]
- 大图标: 96×96px,间距 16px [新增]
- 中图标: 48×48px,间距 12px [新增]
- 小图标: 32×32px,间距 8px [新增]
- 现有 Icons View: 60×60px(介于中/小之间),需调整

#### 5.2 图标布局 [现有]
- 改进: 图标下方文件名最多显示 2 行,超出用省略号
- 改进: 文件名区域固定宽度(100px),文本居中
- 改进: 选中项显示半透明蓝色边框(不是纯背景色)
- 改进: hover 项显示浅色背景圆角矩形

#### 5.3 列表视图 [新增]
- 新增"列表"视图模式: 小图标 + 单行文件名,水平排列
- 适用于快速浏览大量文件

### 6. 选择行为

#### 6.1 鼠标选择 [现有]
- 单击选中(现有)
- Ctrl+单击切换选中(现有)
- Shift+单击范围选择(现有)
- 改进: 在空白区域按住左键拖拽显示**橡皮筋选择框**(rubber band selection)
- 改进: 橡皮筋框使用半透明蓝色填充 + 蓝色边框

#### 6.2 键盘选择 [现有,改进]
- 方向键移动焦点(现有)
- Ctrl+A 全选(现有)
- 改进: Home/End 跳转到首/末项
- 改进: Page Up/Page Down 翻页
- 改进: 输入字母跳转到以该字母开头的文件(type-ahead)

#### 6.3 选中状态视觉 [现有]
- 改进: 选中项数量 > 1 时,状态栏显示"已选择 N 项"和总大小(现有显示数量,需增加大小)
- 改进: 单个文件选中时状态栏显示该文件的完整路径

### 7. 交互行为

#### 7.1 双击打开 [现有,改进]
- 双击文件夹进入(现有)
- 双击文件打开预览(现有)
- 改进: 双击文件夹时添加短暂的进入动画(淡入)

#### 7.2 单击交互 [现有]
- 改进: 单击已选中项不取消选中(现有行为可能取消,需修复)
- 改进: 单击文件名区域才触发选中,单击行空白区域不触发

#### 7.3 右键菜单 [现有]
- 现有: 收藏、打开、导出、重命名、版本历史、删除
- 改进: 菜单项添加快捷键提示(如 "删除\tDel")
- 改进: 添加"属性"菜单项(Alt+Enter),显示文件详细信息对话框
- 改进: 添加"打开方式"子菜单(仅限文件)
- 改进: 多选时右键菜单显示批量操作(现有批量导出/删除,需增加批量重命名)
- 改进: 空白区域右键显示不同菜单(新建文件夹、粘贴、刷新、属性)

#### 7.4 内联重命名 [现有]
- 现有: F2 触发重命名对话框(ContentDialog)
- 改进: 改为内联编辑模式(与资源管理器一致):
  - F2 或慢速双击触发
  - 文件名变为可编辑 TextBox
  - 自动选中文件名(不含扩展名)
  - Enter 确认,Esc 取消
  - 失焦自动确认
  - 编辑框使用系统字体,与显示字体一致

#### 7.5 拖放 [现有,改进]
- 拖出解密(现有)
- 拖入加密(现有)
- 改进: 拖拽时显示文件缩略图作为拖拽光标(多文件显示数量 badge)
- 改进: 拖拽到侧边栏文件夹节点时高亮目标文件夹
- 改进: 拖拽过程中显示半透明预览(ghost image)

### 8. 状态栏

#### 8.1 布局 [现有,改进]
- 左侧: 项目计数 + 选中信息(现有)
- 中间: 当前路径(现有)
- 右侧: 锁定状态(现有)
- 改进: 左侧增加选中文件总大小显示(如 "已选择 3 项 (1.2 MB)")
- 改进: 中间路径改为可点击的面包屑(与标题栏联动)

#### 8.2 视图切换按钮 [现有]
- 在状态栏最右侧添加视图切换小图标按钮(与资源管理器一致):
  - 详细信息视图按钮
  - 图标视图按钮
- 与工具栏的视图切换同步

### 9. 键盘快捷键对齐

#### 9.1 已实现 [现有]
| 快捷键 | 功能 | 状态 |
|--------|------|------|
| Ctrl+I | 导入 | 现有 |
| Ctrl+E | 导出 | 现有 |
| Ctrl+N | 新建文件夹 | 现有 |
| Ctrl+L | 锁定 | 现有 |
| Ctrl+F | 聚焦搜索 | 现有 |
| Ctrl+A | 全选 | 现有 |
| Enter | 打开选中项 | 现有 |
| Backspace | 返回上级 | 现有 |
| Del | 删除 | 现有 |
| F2 | 重命名 | 现有 |
| Alt+Left | 导航返回 | 现有 |
| Alt+Right | 导航前进 | 现有 |
| Alt+Up | 导航到上级 | 现有 |
| F5 | 刷新列表 | 现有 |

#### 9.2 待实现 [现有]
| 快捷键 | 功能 | 优先级 |
|--------|------|--------|
| Alt+Enter | 文件属性 | P2 |
| Ctrl+Shift+N | 新建文件夹 | P2 |
| Home | 跳转到首项 | P2 |
| End | 跳转到末项 | P2 |
| Page Up | 向上翻页 | P2 |
| Page Down | 向下翻页 | P2 |

### 10. 视觉细节对齐

#### 10.1 间距与边距
- 工具栏按钮间距: 2px(现有正确)
- 文件列表内边距: Padding="8,0"(现有正确)
- 侧边栏内边距: 12px(现有正确)
- 状态栏高度: 28px(现有正确)
- 列头高度: 32px(现有约 28px,需调整)

#### 10.2 颜色
- 使用 `ThemeResource` 系统刷子,不硬编码颜色(现有正确)
- 选中项: `AccentFillColorDefaultBrush` 20% 透明度
- Hover 项: `ControlFillColorSecondaryBrush`
- 分隔线: `CardStrokeColorDefaultBrush`(现有正确)
- 次要文字: `TextFillColorSecondaryBrush`(现有正确)
- 三级文字: `TextFillColorTertiaryBrush`(现有正确)

#### 10.3 字体
- 文件名: 14px, Regular(现有正确)
- 列头: 12px, SemiBold(现有正确)
- 状态栏: 12px, Regular(现有正确)
- 路径面包屑: 12px, Regular

#### 10.4 圆角
- 卡片/面板: CornerRadius="8"(现有正确)
- 按钮: CornerRadius="4"(现有正确)
- 文件列表项: 无圆角(与资源管理器一致)

#### 10.5 动画
- 页面进入: `EntranceThemeTransition`(默认)
- 列表刷新: `AddDeleteThemeTransition`(默认)
- 侧边栏折叠/展开: 250ms `CubicEase`(现有设置动画已有)
- 面包屑导航: 无动画(与资源管理器一致)

### 11. 文件属性对话框 [现有]

#### 11.1 功能
- 显示选中文件的详细属性信息
- 字段: 名称、类型、大小(原始大小 + 加密大小)、创建时间、修改时间、所在路径、内容类型
- 只读模式,不允许编辑
- 使用 `ContentDialog` 实现,标题为文件名

#### 11.2 触发方式
- 右键菜单 → 属性
- Alt+Enter 快捷键

### 12. 空状态与引导 [现有,改进]

#### 12.1 空文件夹 [现有,改进]
- 现有: 显示文件夹图标 + 提示文字
- 改进: 增加"导入文件"按钮,点击直接触发导入

#### 12.2 搜索无结果 [现有]
- 现有: 显示搜索图标 + 提示文字
- 改进: 增加"清除筛选"按钮

#### 12.3 首次使用 [现有]
- 保险柜为空且未设置密码时,显示引导卡片
- 引导内容: 欢迎文字 + 快速入门步骤(设置密码 → 导入文件 → 开始使用)

### 13. 开发优先级排序

| 优先级 | 功能 | 复杂度 | 影响范围 |
|--------|------|--------|----------|
| P0 | 导航按钮(返回/前进/上级) | 低 | MainWindow |
| P0 | 面包屑地址栏 | 中 | TitleBarControl |
| P0 | 文件夹优先排序(文件夹排在文件前) | 低 | VaultFileListView |
| P1 | 行样式对齐(高度/hover/选中色) | 低 | VaultFileListView |
| P1 | 列头右键菜单(选择列) | 中 | VaultFileListView |
| P1 | 列宽双击自适应 | 低 | VaultFileListView |
| P1 | 更多视图模式(中图标/小图标/列表) | 中 | VaultFileListView |
| P1 | 内联重命名 | 高 | VaultFileListView + 新控件 |
| P1 | 新增键盘快捷键(Alt+方向键/F5) | 低 | MainWindow |
| P2 | 快速访问区域 | 中 | VaultSidebar |
| P2 | 文件属性对话框 | 低 | 新 Dialog |
| P2 | 橡皮筋选择 | 高 | VaultFileListView |
| P2 | 空白区域右键菜单 | 低 | VaultFileListView |
| P2 | 拖拽到侧边栏文件夹 | 中 | VaultSidebar |
| P3 | 状态栏视图切换按钮 | 低 | VaultStatusBar |
| P3 | 侧边栏双击折叠/展开 | 低 | MainWindow |
| P3 | 搜索高亮匹配文字 | 高 | VaultFileListView |
| P3 | 首次使用引导 | 中 | MainWindow |

### 14. 实现约束

- 所有新增 UI 控件必须遵循本守则的图标字体规范(FontFamily="Segoe MDL2 Assets")
- 所有新增文本必须通过 `Loc.Get()` 本地化,并同步全部 9 个 INI 文件
- 新增常量(如图标大小、行高、动画时长)必须放入 `Constants.cs`
- 新增 XAML 控件放在 `Controls/` 目录下,遵循现有命名规范
- 不引入第三方 UI 库,仅使用 WinUI 3 原生控件
- 每个功能点独立开发、独立测试,避免大面积重构

---

## 全自动推进流程 (UI 对齐 Windows 资源管理器)

> 当用户开启本流程后,Agent 按以下阶段循环执行,直到 AGENTS.md 第 13 节所有条目均为 [现有]。
> 每轮循环产出一个小版本 commit,不 push,不 tag。

### 流程总览

```
┌─────────────┐
│ 1. 审计阶段  │ ← 读取 AGENTS.md 第 13 节,识别下一个优先级批次
└──────┬──────┘
       ▼
┌─────────────┐
│ 2. 确认阶段  │ ← 向用户汇报本轮计划,等待确认(或用户已授权自动执行)
└──────┬──────┘
       ▼
┌─────────────┐
│ 3. 开发阶段  │ ← 按计划逐项实现,每项完成后更新 AGENTS.md 状态
└──────┬──────┘
       ▼
┌─────────────┐
│ 4. 验证阶段  │ ← dotnet build (Debug + Release),检查编译警告/错误
└──────┬──────┘
       ▼
┌─────────────┐
│ 5. 修复阶段  │ ← 修复发现的所有问题,重复步骤 4 直到编译干净
└──────┬──────┘
       ▼
┌─────────────┐
│ 6. 提交阶段  │ ← 版本号 +1 (patch),同步版本到源码/INI/文档,commit
└──────┬──────┘
       ▼
  回到步骤 1
```

### 阶段 1: 审计

1. 读取 AGENTS.md 第 13 节(开发优先级排序)
2. 找到优先级最高且状态为 `[缺失]` 或 `[改进]` 的条目
3. 如果同优先级有多个条目,按复杂度从低到高排序
4. 每轮选取 **3-5 个条目** 作为本轮开发目标
5. 向用户汇报:本轮目标列表 + 预计影响的文件

### 阶段 2: 确认

- 向用户展示本轮计划,格式:
  ```
  本轮开发目标 (v0.1.x):
  [P0] 导航按钮 — 影响 MainWindow.xaml/.cs, TitleBarControl.xaml/.cs
  [P0] 面包屑地址栏 — 影响 TitleBarControl.xaml/.cs
  [P0] 文件夹优先排序 — 影响 VaultFileListView.xaml.cs
  是否确认开始?
  ```
- 用户确认后进入阶段 3
- 如果用户明确授权"自动执行",则跳过确认直接进入阶段 3

### 阶段 3: 开发

- 按计划逐项实现,每项遵循:
  1. 先读取相关文件,理解现有代码结构
  2. 实现功能(XAML + code-behind)
  3. 同步本地化(9 个 INI 文件)
  4. 新增常量放入 Constants.cs
  5. 确保所有 FontIcon 带 `FontFamily="Segoe MDL2 Assets"`
  6. 完成后将 AGENTS.md 对应条目从 `[缺失]`/`[改进]` 改为 `[现有]`
- 开发过程中如发现 AGENTS.md 未列出的 UI 细节问题,直接修复并补充到 AGENTS.md

### 阶段 4: 验证

1. `dotnet build` — Debug 构建,确认零错误
2. `dotnet build -c Release` — Release 构建,确认零错误
3. 检查编译输出中的 CS0067/CS0618 等警告,有则修复
4. 检查新增文件是否包含 BOM、编码正确

### 阶段 5: 修复

- 如果阶段 4 发现问题:
  1. 分析错误/警告原因
  2. 修复代码
  3. 重新执行阶段 4
  4. 循环直到编译完全干净(零错误、零新增警告)
- 修复完成后回到阶段 4 重新验证

### 阶段 6: 提交

1. 版本号: 当前 patch 版本 +1 (如 0.1.2 → 0.1.3)
2. 同步版本号到以下位置:
   - `AppInfo.cs` → `Version`
   - 9 个 INI 文件 → `[__meta__] AppVersion`
3. 更新 `docs/task-queue.md`: 将本轮完成的条目移入 "Completed" 列表
4. `git add` 所有变更文件(不含 `.ustht/` 等非项目文件)
5. `git commit` — 标题格式: `v0.1.x: <一句话概括本轮变更>`
   - Body 列出本轮完成的功能点
6. 不 push,不 tag,不创建 release
7. 回到阶段 1 重新开始

### 流程终止条件

以下情况暂停流程并通知用户:
- AGENTS.md 第 13 节所有条目均为 `[现有]` → UI 对齐完成
- 编译错误无法自行修复(需要用户决策)
- 开发过程中发现需要变更技术栈或引入新依赖
- 用户主动要求暂停/停止

### 版本号规划

| 轮次 | 版本号 | 预计内容 |
|------|--------|----------|
| 第 1 轮 | v0.1.3 | P0: 导航按钮、面包屑、文件夹优先排序 |
| 第 2 轮 | v0.1.4 | P1: 行样式、列头菜单、列宽自适应、快捷键 |
| 第 3 轮 | v0.1.5 | P1: 视图模式、内联重命名 |
| 第 4 轮 | v0.1.6 | P2: 快速访问、属性对话框、右键菜单增强 |
| 第 5 轮 | v0.1.7 | P2: 橡皮筋选择、拖拽到侧边栏 |
| 第 6 轮 | v0.1.8 | P3: 状态栏视图切换、侧边栏折叠、搜索高亮、首次引导 |
| 后续 | v0.2.x+ | 根据实际进度调整 |
