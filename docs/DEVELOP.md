# Gyroown 开发文档

> 最后更新: 2026-05-27
> 版本: v0.1.1
> 仓库: https://github.com/JularDepick/Gyroown

---

## 1. 项目概述

完全离线的文件保密柜。WinUI 3 桌面应用，.NET 8。

**密钥模型**: 用户密码 → PBKDF2 派生 userKey → 解密内部非对称密钥对 → 私钥加密文件 / 公钥解密文件。内部密钥对首次生成后原始明文永不改变。

**数据落盘**: `%USERPROFILE%\.Gyroown\`

---

## 2. 架构

```
App.xaml.cs (入口 + 单实例)
  └─ 创建 MainWindow (唯一窗口)

MainWindow (单窗口承载所有视图)
  ├─ [AuthOverlay] PasswordSetupControl / UnlockControl
  ├─ [VaultContent] TitleBar + CommandBar + Sidebar + FileList + StatusBar
  └─ [SettingsPanel] 主题 + 语言 + 关于 (右侧滑入)
```

### 2.1 密钥架构

```
用户密码 (PIN/手势/自定义/图片)
  │  └── 仅用于身份认证 + 保护内部密钥
  ▼
DeriveUserKey(password, salt) → userKey     ← PasswordService 已实现
  │  └── 用 userKey 解密 auth\vault-key.enc
  ▼
内部固定密钥对 (privateKey, publicKey)        ← 桩 (GenerateVaultKeyPair)
  ├── privateKey → 加密文件 (写入保密柜)
  └── publicKey  → 解密文件 (从保密柜取出)
  二者均不可公开，首次生成后原始明文永不改变
  ▼
EncryptFileAsync(stream, privateKey)         ← 桩
DecryptFileAsync(stream, publicKey)          ← 桩

落盘结构:
%USERPROFILE%\.Gyroown\
  ├── auth\                     (Hidden, 仅后缀名)
  │   ├── .gyropw                ← 已实现 (pw=password)
  │   ├── .gyrock                ← 已实现 (ck=corekey)
  │   └── insurance.gyrock       ← 📋 设计完成
  ├── data\
  │   ├── {hashID}.gyrodt             ← 小文件 (< 阈值)
  │   └── {hashID}/c0000.gyrodt ...    ← 大文件分片 (hex 编目, 默认32MB/片)
  ├── config.gyrojson                  ← 已实现 (加密核心配置: 切片档位)
  ├── meta\
  │   ├── {hashID}.gyromt             ← 已实现 (mt=meta)
  │   └── .tree.gyrojson              ← 已实现 (加密文件夹树)
  ├── preview\
  │   └── {hashID}.gyropv             ← 已实现 (pv=preview, ≤1MB JPEG)
  ├── log\
  │   ├── error\                       ← 错误日志 (200KB 切片)
  │   │   └── error-yyyy-mm-dd-yyyy-mm-dd.txt
  │   ├── crash\                       ← 崩溃日志
  │   │   └── crash-yyyy-mm-dd-yyyy-mm-dd.txt
  │   └── run\                         ← 运行日志
  │       └── run-yyyy-mm-dd-yyyy-mm-dd.txt
  ├── key\
  │   └── portrait.*            ← 图片密码
  └── settings.gyrojson         ← 已实现 (加密用户配置: 主题/语言/强调色)
```

---

## 3. 服务接口

### 3.1 IPasswordService — 已实现

> 文件: `Services/IPasswordService.cs` (接口), `Services/PasswordService.cs` (实现)

```csharp
public interface IPasswordService
{
    Task SetupAsync(object credential);
    Task<PasswordValidationResult> ValidateAsync(object credential);
    bool IsPasswordSet { get; }
    Task<(byte[] OldUserKey, byte[] NewUserKey)> ChangePasswordAsync(
        object oldCredential, object newCredential);
    void Lock();
    bool IsLocked { get; }
    int AutoLockTimeout { get; set; }
    string? GetPasswordType();
    event EventHandler? Unlocked;
    event EventHandler? Locked;
}
```

**实现细节**:
- PBKDF2-SHA256，100,000 迭代，32B salt，32B hash
- `password.dat` JSON 结构: `{ type, salt, hash, iterations }`
- 安全比对: `CryptographicOperations.FixedTimeEquals`
- userKey 派生: `Rfc2898DeriveBytes.Pbkdf2(credBytes, salt, iterations, SHA256, 32)`

### 3.2 IEncryptionService — 已实现

> ⚠️ 2026-05-26 修复：`.NET RSA.Decrypt()` 需要私钥参数，`DecryptBlob` 改用私钥。旧 `data/` 和 `meta/` 需清理。

> 文件: `Services/IEncryptionService.cs` (接口), `Services/EncryptionService.cs` (实现)

**实现细节**:
- RSA 2048-bit 密钥对（PKCS#1 格式）
- AES-256-GCM 文件加密（随机 key/nonce，12B nonce，16B tag）
- RSA-OAEP-SHA256 加密 AES 密钥和文件元数据到文件头
- 文件格式: `[4B magic "GYRO"][4B ver][4B len][RSA header][4B len][AES-GCM body]`
- vault-key.enc 格式: `[12B nonce][AES-GCM cipher][16B tag]` (userKey 加密的密钥对)

```csharp
public interface IEncryptionService
{
    byte[] DeriveUserKey(string password, byte[] salt);
    (byte[] PrivateKey, byte[] PublicKey) GenerateVaultKeyPair();
    byte[] EncryptVaultKeyPair((byte[] PrivateKey, byte[] PublicKey) keyPair, byte[] userKey);
    (byte[] PrivateKey, byte[] PublicKey) DecryptVaultKeyPair(byte[] encrypted, byte[] userKey);
    Task EncryptFileAsync(Stream inStream, Stream outStream, byte[] privateKey, ...);
    Task DecryptFileAsync(Stream inStream, Stream outStream, byte[] publicKey, ...);
}
```

### 3.3 IVaultService — 已实现

> 文件: `Services/IVaultService.cs` (接口 + 实现)

**实现细节**:
- 原始文件 → `data\{guid}`（无后缀，纯 AES-GCM 密文）
- 元数据 → `meta\{guid}.meta`（AES加密JSON: 文件名/大小/RSA加密的AES密钥）
- 文件列表通过扫描 `meta\` 目录构建，无需索引文件
- 安全删除（覆写随机数据后 `File.Delete()`）
- MIME 类型自动推断

```csharp
public interface IVaultService
{
    bool IsInitialized { get; }
    void Initialize(byte[] privateKey, byte[] publicKey);
    string VaultPath { get; }
    IReadOnlyList<VaultFileItem> ListItems(string virtualPath = "/");
    VaultFolder GetFolderTree();
    Task<VaultFileItem> ImportItemAsync(Stream data, string name, ...);
    Task ExportItemAsync(string itemId, Stream outStream, ...);
    void DeleteItem(string itemId);
    void MoveItem(string itemId, string newVirtualPath);
    void RenameItem(string itemId, string newName);
    void CreateFolder(string virtualPath);
    void DeleteFolder(string virtualPath);
    IReadOnlyList<FileVersionRecord> GetVersionHistory(string fileId);
    Task RestoreFileVersionAsync(string fileId, int versionNumber, ...);
    FileVersionRecord? SaveCurrentVersion(string fileId, string description = "");
}
```

### 3.4 IThemeService — 已实现

> 文件: `Services/IThemeService.cs` (接口), `Services/ThemeService.cs` (实现)

**实现细节**:
- 持久化到 `~/.Gyroown/settings.gyrojson`（加密，vault key pair）
- `Initialize(vaultKey)` — 解锁后加载加密设置
- `SetTheme()` / `SetAccentColor()` / `SetLanguage()` — 保存并触发 ThemeChanged 事件
- 8 种强调色预设 (Blue, Teal, Green, Orange, Purple, Pink, Red, Graphite)
- 3 种主题: Default (跟随系统), Light, Dark

```csharp
public interface IThemeService
{
    AppTheme CurrentTheme { get; }
    string AccentColor { get; }
    string Language { get; }
    void Initialize(byte[] vaultKey);
    void SetTheme(AppTheme theme);
    void SetAccentColor(string hexColor);
    void SetLanguage(string lang);
    IReadOnlyList<AppTheme> GetAvailableThemes();
    event EventHandler? ThemeChanged;
}
```

### 3.5 IDragDropService — 已实现

> 文件: `Services/IDragDropService.cs` (接口), `Services/DragDropService.cs` (实现)

**实现细节**:
- 拖入 (`HandleDropInAsync`): 遍历文件路径，调用 `VaultService.ImportItemAsync()` 加密导入
- 拖出: 由 `VaultFileListView.OnDragStart` 直接处理，通过 `DecryptToFile` 回调解密到临时目录后提供 `StorageItems`

```csharp
public interface IDragDropService
{
    Task HandleDropInAsync(IReadOnlyList<string> filePaths, ...);
    Task<IReadOnlyList<string>> HandleDragOutAsync(IReadOnlyList<string> itemIds, ...);
}
```

### 3.6 ILocalizationService — 已实现

> 文件: `Services/ILocalizationService.cs` (接口), `Services/LocalizationService.cs` (实现)

```csharp
public interface ILocalizationService
{
    string Get(string section, string key);
    string CurrentLanguage { get; }
    void SetLanguage(string langCode);
    IReadOnlyList<string> GetAvailableLanguages();
    event EventHandler? LanguageChanged;
}
```

**实现细节**: INI 文件解析 + 嵌入式资源回退。加载优先级：`lang/` .ini 文件 > DLL 嵌入式资源 > 字段名降级。zh-CN 和 en-US 硬编码为 DLL 嵌入式资源（`Resources/Loc/`）。所有 .ini 文件含 `[__meta__]` 段（LangCode + AppVersion）。7 种语言，16 个 section，默认 `zh-CN`。

### 3.7 FavoritesService — 已实现

> 文件: `Services/FavoritesService.cs`

**实现细节**:
- 管理收藏夹项目，持久化到 `~/.Gyroown/favorites.json`（明文 JSON）
- 支持分组管理、拖拽排序、孤立项清理
- 事件驱动：`FavoritesChanged` 事件通知 UI 更新

**主要方法**:
- `Load()` / `Save()` — 加载/保存收藏夹
- `Add()` / `Remove()` / `Toggle()` — 添加/删除/切换收藏
- `GetAll()` / `GetGrouped()` / `GetGroups()` — 获取收藏列表
- `Reorder()` — 拖拽排序
- `FindOrphans()` / `RemoveOrphans()` — 孤立项处理

### 3.8 VersionHistoryService — 已实现

> 文件: `Services/VersionHistoryService.cs`

**实现细节**:
- 文件版本历史管理，目录结构：`{vaultRoot}/versions/{fileHashId}/`
- 每个版本存储为：`v{versionNumber}.gyroverdata`（加密数据）+ `v{versionNumber}.gyrovermeta`（加密元数据）
- 支持版本保存、恢复、清理、安全删除
- 最大保留版本数可配置（默认 10）

**主要方法**:
- `SaveVersion()` / `SaveVersionFromFile()` — 保存新版本
- `ListVersions()` — 列出所有版本
- `RestoreVersion()` — 恢复指定版本
- `GetVersionRecord()` — 获取版本元数据
- `CleanVersions()` / `DeleteAllVersions()` — 清理版本历史

### 3.9 SearchFilter — 已实现

> 文件: `Models/SearchFilter.cs`

**实现细节**:
- 高级搜索过滤器模型
- 支持文件名、文件类型、大小范围、日期范围过滤
- 用于 `VaultFileListView` 的过滤功能

---

## 4. 数据模型

> 目录: `Models/`

| 类 | 文件 | 字段 |
|----|------|------|
| `VaultFileItem` | `VaultFileItem.cs` | Id, Name, VirtualPath, EncryptedSize, OriginalSize, ContentType, CreatedAt, ModifiedAt, IsFolder |
| `VaultFolder` | `VaultFolder.cs` | Name, VirtualPath, SubFolders, Files |
| `PasswordType` | `PasswordType.cs` | enum: Pin, Gesture, Custom, Picture |
| `PasswordConfig` | `PasswordConfig.cs` | Type, PinMinLength(6), GestureMinPoints(4), CustomMinLength(6), CustomMaxLength(32), PictureMinPoints(3), LockoutThreshold(5), LockoutDurationSec(30), PictureToleranceRatio(0.05), AllowedChars |

---

## 5. 目录结构

```
Gyroown/
├── App.xaml / App.xaml.cs          # Entry point: single instance, routing
├── AppInfo.cs                      # Version, name, supported languages constants
├── MainWindow.xaml / .cs           # File manager main window
├── lang/                           # 语言包 (7 种, 外部覆盖)
│   ├── zh-CN.ini                   # 简体中文 (默认)
│   ├── zh-TW.ini                   # 繁体中文
│   ├── en-US.ini                   # English (US)
│   ├── en-GB.ini                   # English (UK)
│   ├── ja-JP.ini                   # 日本語
│   ├── ko-KR.ini                   # 한국어
│   └── fr-FR.ini                   # Francais
├── Resources/Loc/                  # 嵌入式翻译 (DLL 内置)
│   ├── zh-CN.ini                   # 简体中文 (嵌入式)
│   └── en-US.ini                   # English (US) (嵌入式)
├── Controls/                       # 自定义控件
│   ├── TitleBarControl             # 标题栏 (拖拽区 + 窗口按钮)
│   ├── VaultFileListView           # 文件列表 (Details/Icons/Tiles)
│   ├── VaultSidebar                # 虚拟目录树
│   ├── VaultStatusBar              # 状态栏
│   └── FavoritesPanel              # ★ 收藏夹面板
├── Views/                          # 窗口/子视图
│   ├── IPasswordControl.cs         # 密码控件统一接口
│   ├── PinPasswordControl          # 6位 PIN
│   ├── GesturePasswordControl      # 九宫格手势
│   ├── CustomPasswordControl       # 自定义密码
│   ├── PicturePasswordControl      # 图片密码
│   ├── UnlockWindow                # 解锁窗口
│   ├── PasswordSetupWindow         # 密码设置 (二次确认)
│   ├── SettingsWindow              # 设置
│   └── VersionHistoryDialog        # ★ 版本历史对话框
├── Models/                         # 数据模型
│   ├── VaultFileItem.cs
│   ├── VaultFolder.cs
│   ├── PasswordType.cs
│   ├── PasswordConfig.cs
│   ├── FavoriteItem.cs             # ★ 收藏夹项目
│   ├── FileVersionRecord.cs        # ★ 版本记录
│   └── SearchFilter.cs             # ★ 搜索过滤器
├── Services/                       # 接口 + 实现
│   ├── IPasswordService / PasswordService    # ★ PBKDF2
│   ├── IEncryptionService / EncryptionService # ★ RSA + AES-GCM
│   ├── IVaultService / VaultService          # ★ 加密文件仓库
│   ├── IThemeService / ThemeService          # ★ 主题 + 强调色
│   ├── IDragDropService / DragDropService    # ★ 拖放协调
│   ├── ILocalizationService / LocalizationService   # INI 本地化
│   ├── ConfigService                # ★ 加密核心配置 (切片档位)
│   ├── InsuranceService             # 📋 密钥保险 HTTP 桩 (接线完成, 待后端 API)
│   ├── JsonConfig / Loc             # 序列化 + 静态本地化
│   ├── LogService                   # 日志系统
│   ├── FavoritesService             # ★ 收藏夹管理
│   └── VersionHistoryService        # ★ 文件版本历史
└── Gyroown.csproj                  # .NET 8 + WinUI 3

tools/                              # Binary tools (source → compile → run)
└── transIniToDll/              # .ini → DLL embedded resource converter (community tool)
    ├── transIniToDll.csproj
    ├── Program.cs
    └── README.md

scripts/                            # Script tools (run source directly, no build)
└── fix_vault/                  # One-time VaultService.cs streaming migration scripts
    ├── fix_vault.ps1
    ├── _fix_vault.ps1
    ├── _fix_vault.mjs
    └── README.md

docs/
├── DEVELOP.md                      # This document
├── UserThoughts.md                 # Design decisions
├── CHANGELOG.md                    # Version changelog
├── long-term-roadmap.md            # Long-term roadmap
├── release-checklist.md            # Release checklist
├── task-queue.md                   # Task queue management
├── DevLog/                         # Development logs (not committed)
│   └── build.log
├── api/
│   └── key-insurance.md            # Key insurance API spec
└── outdated/                       # Completed historical plans
```

---

## 6. 实现状态

| 模块 | 状态 | 说明 |
|------|------|------|
| 应用外壳 | ✅ 完成 | 单实例, 托盘(H.NotifyIcon), 启动路由 |
| 主窗口布局 | ✅ 完成 | TitleBar + CommandBar + Sidebar + FileList + StatusBar |
| 密码系统 | ✅ 完成 | PBKDF2, 4种密码类型, 二次确认, 错误锁定, salt管理 |
| 加密核心 | ✅ 完成 | RSA 2048 + AES-256-GCM, 密钥对生成/加解密, 文件加解密 |
| 文件仓库 | ✅ 完成 | VaultService: CRUD, 加密索引, 安全删除, 虚拟目录树 |
| 本地化 | ✅ 完成 | 7 种语言 INI 包, 运行时切换, fallback |
| 拖放 | ✅ 完成 | DragDropService: 批量导入/导出 + 安全清理 |
| 主题切换 | ✅ 完成 | ThemeService: 系统/浅色/深色 + 持久化 settings.gyrojson (加密) |
| 设置面板 | ✅ 完成 | 主窗口内嵌侧面板: 主题 + 强调色 + 语言 + 密码 + 关于 |
| 应用内查看器 | ✅ 完成 | 图片(预览) / 文本(只读) / 视频(桩) |
| 移入/移出 | ✅ 完成 | 剪切导入/导出, 不拖放 |
| 缩略预览 | ✅ 完成 | preview/ 目录, 图片 JPEG ≤1MB 加密 |
| 日志系统 | ✅ 完成 | log/error/, log/crash/, log/run/ 子目录, 200KB 切片, 消息完整性 |
| 错误通知 | ✅ 完成 | 红色底栏 + 可关闭 + 点击查看日志 |
| 通用分片存储 | ✅ 完成 | 大文件自动切片, hex 编号子目录, ConfigService 管理档位 |
| 可配置切片 | ✅ 完成 | 2-64MB 6 档位, config.gyrojson 加密存储, 仅影响后续导入 |
| 密钥保险 | 📋 接线完成 | 客户端实现, HTTP 桩 (待后端 API), 文档 docs/api/key-insurance.md |
| 图片密码选图 | ✅ 完成 | FileOpenPicker 集成 |
| 侧边栏文件夹筛选 | ✅ 完成 | FilterPath + .tree.gyrojson 持久化, 点击设置 CurrentPath |
| 右键导出 | ✅ 完成 | ExportRequested → FileSavePicker |
| 窗口行为 | ✅ 完成 | AppWindow.Closing 拦截关闭→托盘, 原生按钮, 800×480 |
| 安全原则 | ✅ 完成 | 关闭窗口后重验证, 安全擦除, 不碰注册表/环境变量 |
| hashID 规范 | ✅ 完成 | SHA256 前32位全小写, 前后兼容 |
| 高危操作锁 | ✅ 完成 | 删除/移出/移入/改密/锁定 加 _busy 互斥 |
| 密码细节 | ✅ 完成 | 二次确认, 5次锁定30s, 自动验证(无需Enter), PIN退格回退 |
| auth 保护 | ✅ 完成 | 仅后缀名(.gyropw/.gyrock), Hidden 属性, 目录隐匿 |
| 键盘快捷键 | ✅ 完成 | Ctrl+I/E/N/L/F/A, Enter, Backspace |
| 设置面板动画 | ✅ 完成 | Storyboard + DoubleAnimation, 250ms, CubicEase |
| 文件列表性能 | ✅ 完成 | ContainerContentChanging 懒加载, 预览缓存 |
| Banner 动画 | ✅ 完成 | 滑入/淡出动画, 200ms, CubicEase |
| 进度条动画 | ✅ 完成 | DoubleAnimation, 300ms, CubicEase |
| 搜索增强 | ✅ 完成 | 搜索历史, 空状态提示 |
| 全局异常处理 | ✅ 完成 | App.UnhandledException + LogService |
| 磁盘空间检查 | ✅ 完成 | 导入前检查 AvailableFreeSpace |
| 日志级别 | ✅ 完成 | Debug/Info/Warn/Error + MinLevel |
| 文件类型图标 | ✅ 完成 | 根据 ContentType 显示不同图标 |
| 视频预览生成 | ✅ 完成 | Shell 缩略图方案, 视频文件自动生成预览图 |
| 文件搜索增强 | ✅ 完成 | 高级搜索过滤条件 (文件类型、大小、日期) |
| 批量操作优化 | ✅ 完成 | Ctrl/Shift 多选、进度对话框 |
| 文件预览增强 | ✅ 完成 | 缩放/平移/语法高亮 |
| 文件版本管理 | ✅ 完成 | VersionHistoryService: 历史版本、回滚、安全删除 |
| 收藏夹功能 | ✅ 完成 | FavoritesService: 快速访问、拖拽排序、分组管理 |

---

---

## 7. 命名规范

### 7.1 文件后缀

| 目录 | 后缀 | 含义 | 主名称 |
|------|------|------|--------|
| `auth/` | `.gyropw` | pw=password | 无（仅后缀） |
| | `.gyrock` | ck=corekey | 无（仅后缀） |
| | `insurance.gyrock` | 密钥恢复 | 无 |
| `data/` | `.gyrodt` | dt=data | `{hashID}` (SHA256 前 32 位, 小写) |
| | `/c{xxxx}.gyrodt` | 分片 (4 位 hex, 小写) | `{hashID}` |
| `meta/` | `.gyromt` | mt=meta | `{hashID}` |
| `preview/` | `.gyropv` | pv=preview | `{hashID}` |
| `log/` | `.txt` | 日志 | `error-{start}-{end}` / `run-{start}-{end}` |
| `versions/{hashID}/` | `.gyroverdata` | 版本加密数据 | `v{n}` |
| | `.gyrovermeta` | 版本加密元数据 | `v{n}` |
| 根 | `.json` | 明文配置 | `settings`, `favorites` |
| 根 | `.gyrojson` | 加密配置 (vault key pair) | `config` |

### 7.2 目录结构

```
%USERPROFILE%\.Gyroown\
├── auth\                     (Hidden)
│   ├── .gyropw
│   ├── .gyrock
│   └── insurance.gyrock
├── data\
│   ├── {hashID}.gyrodt       ← 小文件
│   └── {hashID}\              ← 大文件分片
│       ├── c0000.gyrodt
│       └── c0001.gyrodt
├── meta\
│   └── {hashID}.gyromt
│   └── .tree.gyrojson
├── preview\
│   └── {hashID}.gyropv
├── log\
│   ├── error-{yyyy-mm-dd}-{yyyy-mm-dd}.txt
│   └── run-{yyyy-mm-dd}-{yyyy-mm-dd}.txt
├── versions\                    ← 版本历史
│   └── {hashID}\
│       ├── v1.gyroverdata
│       ├── v1.gyrovermeta
│       └── ...
├── favorites.json               ← 明文收藏夹
├── settings.json
└── config.gyrojson
```

### 7.3 hashID

- 算法: `SHA256(原始内容)` → 十六进制 → 取前 32 位 → **全小写**
- `ListItems` 扫描 `*.gyromt` 通配，不假定 ID 长度（前后兼容）

---

## 8. 构建

```bash
# 还原依赖
dotnet restore

# 构建
dotnet build

# 运行 (需 Windows 10 1809+)
dotnet run
```

**编码**: 所有源文件 UTF-8 BOM。**代码语言**: 全部英文（注释/文档/变量名），中文仅限 `lang/*.ini`。**依赖**:
- .NET 8.0
- Microsoft.WindowsAppSDK 2.1.3
- H.NotifyIcon.WinUI 2.3.0
- Windows 10 1809 (build 17763) 或更高
