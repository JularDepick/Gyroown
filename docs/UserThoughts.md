# UserThoughts — DEPRECATED

> **This file is deprecated.** All content has been restructured into `docs/UserThoughts/` directory.
> See [docs/UserThoughts/README.ai.md](UserThoughts/README.ai.md) for the new index.

---

## Migration Notice

This file's content has been split into dimension-based files under `docs/UserThoughts/`:

| File | Topic |
|------|-------|
| [01-security-encryption.md](UserThoughts/01-security-encryption.md) | Security & Encryption, Key Management |
| [02-password-system.md](UserThoughts/02-password-system.md) | Password System |
| [03-storage-architecture.md](UserThoughts/03-storage-architecture.md) | Storage Architecture |
| [04-system-constraints.md](UserThoughts/04-system-constraints.md) | System Constraints & Security Principles |
| [05-ui-design.md](UserThoughts/05-ui-design.md) | UI Design |
| [06-development-constraints.md](UserThoughts/06-development-constraints.md) | Development & Localization |
| [07-doc-maintenance.md](UserThoughts/07-doc-maintenance.md) | Documentation Maintenance |
| [README.ai.md](UserThoughts/README.ai.md) | Index, Architecture, Maintenance Rules |

**Maintenance rule**: 维护不得丢失用户表述的细节

---

## 1. 安全与加密

### 1.1 内部固定密钥对

- 非对称密钥对（RSA 2048）：**私钥加密写入 / 公钥解密读取**
- **二者均不可公开**，不对外暴露
- `GenerateVaultKeyPair()` 仅在**首次设置时调用一次**，之后原始明文**永不改变、永不重生成**
- 内部密钥对原始明文是**唯一用于加解密文件的凭证**
- `.NET RSA.Decrypt()` 需私钥参数，故 `DecryptBlob` 内部使用 `ImportRSAPrivateKey`（私钥含公钥分量）

### 1.2 混合加密方案

- **RSA 2048-bit** 非对称密钥对（PKCS#1 格式）
- **AES-256-GCM** 文件体加密（随机 key/nonce，12B nonce，16B tag）
- RSA-OAEP-SHA256 加密 AES 密钥和元数据
- Blob 格式：`[4B hdrLen][RSA-OAEP header][4B bodyLen][AES-GCM body]`

### 1.3 统一加密方法

- `EncryptBlob(data, privateKey)` — data 文件和 meta 文件统一使用
- `DecryptBlob(blob, privateKey)` — .NET 需私钥参数，私钥含公钥分量

### 1.4 分片加密

- 大文件按 `config.gyrojson` 中档位切片，每片独立 `EncryptBlob`
- 导出时按序拼接解密
- 删除时清理整个分片子目录

---

## 2. 密钥管理

### 2.1 密钥保护

- 用户密码 → `DeriveUserKey(password, salt)` → userKey
- userKey 用于双向加密保护密钥对密文：`auth\.gyrock`（ck=corekey）
- 改密码：只重加密 `.gyrock`，**密钥对明文不变，文件不动**

### 2.2 改密流程

> 2026-05-26

- 需同时持有**旧密码**和**新密码**
- 旧密码 → 解密 `.gyrock` → 还原密钥对
- 新密码 → 加密密钥对 → 写回 `.gyrock`
- 确认新密码生效后**才抛弃旧密码**
- `PasswordService.ChangePasswordAsync` 返回 `(oldUserKey, newUserKey)` 供调用者重加密

### 2.3 卸载策略

- 卸载时只有用户**明确选择「删除数据及密钥」**才能删除 `auth\` 和 `data\`

### 2.4 密钥保险（密钥恢复）

> 2026-05-26 设计，客户端已实现，云端 API 为桩

#### 动机

用户遗忘密码 → 正常途径无法解密 `.gyrock` → 所有加密文件永久失效。提供一条由应用服务商协助的恢复通道。

#### 命名

- **核心密钥** — 专门加解密文件数据的内部固定密钥对（vault key pair）
- **保险密钥对** — 每用户独立的提供商级密钥对，用于包装核心密钥的恢复备份
- **密钥保险** — 用户可见功能名称，对应文件 `auth/insurance.gyrock`

#### 流程

```
密码设置完成后弹窗询问（非前置步骤）:
  └→ 弹窗: "开启密钥保险？" (可跳过)
       ├─ 输入邮箱 → POST /request-code
       ├─ 输入验证码 → POST /verify-code → 获得 token
       └─ 后台异步 POST /upload(token, email, insPriv)
           用户可随时取消，环节错误不自动跳出
           上传完成后绿色底栏提示 3s 消失
```

#### 安全性

| 威胁 | 缓解措施 |
|------|----------|
| 提供商私钥泄露 | 每用户独立保险密钥对，私钥存 HSM |
| 身份冒充 | 邮箱 + 短信双重验证，强绑定 |
| 提供商窥视核心密钥 | 恢复过程中服务端短暂持有——UI 明确告知用户此权衡 |
| 在线依赖 | 默认关闭，用户主动开启。关闭时完全离线 |

#### API 端点（桩）

| 端点 | 说明 |
|------|------|
| `POST /insurance/request-code` | 发验证码到邮箱 |
| `POST /insurance/verify-code` | 验证码→返回身份 token |
| `POST /insurance/upload` | 后台异步上传（fire-and-forget） |

### 2.5 auth/缺失恢复流程

> 2026-05-27

启动时按优先级检查：

1. **auth 目录不存在** → 首次使用场景：自动创建 auth 目录，进入密码设置流程（非崩溃性）
2. **`.gyrock`（核心密钥）丢失** →
   - 若已开启密钥保险 → 提醒用户联系服务商发起云端恢复
   - 若未开启密钥保险 → 崩溃性警告（数据不可恢复），提示删除 `~/.Gyroown/` 重新开始
3. **`.gyropw`（密码文件）丢失** → 进入设置新密码流程（密钥对仍在，重新加密即可）

> 2026-05-28 补充：首次启动（auth 目录不存在）不再崩溃，改为创建 auth 目录并进入正常设置流程。auth 目录创建延迟到 `Loaded` 事件，包裹 try/catch。

---

## 3. 密码系统

### 3.1 四种密码类型

| 类型 | 标识符 | 最小长度 | 说明 |
|------|--------|----------|------|
| 6 位数字 PIN | `pin` | 6 位 | 6 个单字符 PasswordBox，自动跳转，Backspace 回退 |
| 九宫格手势 | `gesture` | 4 点 | 3×3 Canvas + Polyline 连线，不可重复访问 |
| 自定义密码 | `custom` | 6-32 位 | `[a-zA-Z0-9]` + 可见标点，达到长度自动验证（无需 Enter） |
| 图片密码 | `picture` | 3 点 | FileOpenPicker 选图 → 按序点击坐标 → 欧氏距离验证 |

### 3.2 图片密码细节

- 图片以 **Uniform 模式**显示（保持宽高比，居中）
- 坐标记录为 `(x_ratio, y_ratio)` 相对于图片宽高的**比例**
- 每个已点击点显示带**序号的圆形标记**（蓝色圆 + 白色序号）
- 半径阈值：图片短边的 **5%**（开发者可调参数）
- 验证：按序点击，正确 → 绿色序号圆推进；错误 → 全部变红闪烁 500ms → 重置
- 需先设置一个**备用密码**（A/B/C 之一），用于失效恢复

### 3.3 图片密码持久化

> 2026-05-28

- 用户选择的图片**加密存储**到 `auth/image.pwimg`，解锁时自动加载显示
- 加密方案：XOR + 随机 32 字节密钥（`auth/.imgkey`）+ 16 字节随机 salt
- 密钥**独立于 vault key 和用户密码**（因为图片需在认证前显示）
- 不是安全敏感数据（仅视觉辅助），轻量加密即可，避免明文存储
- 改密时若切换到非图片类型 → 删除 `image.pwimg`；切换到图片类型 → 触发重新选图
- 图片文件丢失时显示占位提示，仍可点击空白区域（坐标验证不受影响）

### 3.4 密码设置

- **二次输入验证**：第一步输入 → 第二步确认 → 比对一致才生效
- 可返回上一步修改
- Enter 键触发"下一步"/"设置"，Esc 返回

### 3.5 密码存储

- PBKDF2-SHA256，**100,000 迭代**
- 32 字节随机 salt，32 字节 hash
- 恒定时间比较（`CryptographicOperations.FixedTimeEquals`）
- 存储格式：JSON `{ type, salt, hash, iterations }` → `auth\.gyropw`

### 3.6 锁定策略

- 连续 **5 次**错误 → 锁定 **30 秒**，倒计时显示

### 3.7 解锁界面交互

- 检测到键盘输入**可见字符**时，自动聚焦到密码输入框（`CharacterReceived` 事件）
- 聚焦调用 `IPasswordControl.FocusInput()`，由各密码控件自行实现

---

## 4. 存储架构

### 4.1 路径约定

> **`~` 即为用户文件夹目录**（`%USERPROFILE%`，即 `C:\Users\<username>`）。全文所有 `~` 均指此目录。

### 4.2 目录结构

```
%USERPROFILE%\.Gyroown\
├── auth\                         (Hidden 属性)
│   ├── .gyropw                   # 密码哈希 (pw=password, 仅后缀)
│   ├── .gyrock                   # 内部密钥对密文 (ck=corekey, 仅后缀)
│   ├── .imgkey                   # 图片密码图片加密密钥 (32B random)
│   ├── image.pwimg               # 图片密码用户选择的图片 (XOR 加密)
│   └── insurance.gyrock          # 密钥保险备份
├── data\
│   ├── {hashID}.gyrodt           # 小文件 (dt=data)
│   └── {hashID}\                  # 大文件分片目录
│       ├── c0000.gyrodt           # 4位十六进制编号, 前导零
│       └── c0001.gyrodt
├── meta\
│   ├── {hashID}.gyromt           # 元数据 (mt=meta)
│   └── .tree.gyrojson            # 加密文件夹树
├── preview\
│   └── {hashID}.gyropv           # 加密缩略图 (pv=preview)
├── versions\                      # 版本历史
│   └── {hashID}\
│       ├── v1.gyroverdata
│       ├── v1.gyrovermeta
│       └── ...
├── log\
│   ├── error\                     # 错误日志 (200KB 切片)
│   │   └── error-{ymd}-{ymd}.txt
│   ├── crash\                     # 崩溃日志
│   │   └── crash-{ymd}-{ymd}.txt
│   └── run\                       # 运行日志
│       └── run-{ymd}-{ymd}.txt
├── favorites.gyrojson             # 加密收藏夹
├── search-history.gyrojson        # 加密搜索历史
├── settings.gyrojson              # 加密用户配置 (主题/语言/强调色)
└── config.gyrojson                # 加密核心配置 (切片档位/最大版本数/自动锁定超时)
```

### 4.3 加密数据文件

- **一个原始文件对应一个加密数据文件**
- 后缀 `.gyrodt`（dt=data），使用内部固定密钥对进行加密
- 格式：`[4B hdrLen][RSA-OAEP header{ aesKey, aesNonce, len }][4B bodyLen][AES-GCM body]`
- 分片存储：> 阈值时按 `data/{hashID}/c{xxxx}.gyrodt` 切片，4 位十六进制小写编号

### 4.4 元数据文件

- **一个元数据文件对应一个加密数据文件**（1:1 映射，同 hashID）
- 后缀 `.gyromt`（mt=meta），使用内部固定密钥对加密
- 包含：文件名、原始大小、MIME 类型、虚拟路径、创建/修改时间、ChunkCount/ChunkSize、PreviewId
- 文件列表通过扫描 `*.gyromt` 构建，无需索引文件
- **`meta/` 和 `data/` 是强绑定的**：必须同时存在或同时不存在

### 4.5 hashID

- 算法: `SHA256(原始内容)` → 十六进制 → 取前 32 位 → **全小写**
- `ListItems` 扫描 `*.gyromt` 通配，不假定 ID 长度 — 前后版本兼容
- 分片编号 `c{i:x4}` 同样小写

### 4.6 配置分离

| 文件 | 格式 | 内容 | 编辑方式 |
|------|------|------|----------|
| `settings.gyrojson` | 加密 JSON（vault key） | 用户个性化：主题/语言/强调色 | 应用 UI |
| `config.gyrojson` | 加密 JSON（vault key） | 程序维护：切片档位/最大版本数/自动锁定超时 | **仅**应用 UI |
| `favorites.gyrojson` | 加密 JSON（vault key） | 收藏夹数据 | 应用 UI |
| `search-history.gyrojson` | 加密 JSON（vault key） | 搜索历史（最多 10 条） | 应用 UI |

- `.gyrojson` = 加密 JSON 文件，加解密方式与数据文件全局统一
- 所有 `.gyrojson` 文件使用 vault key 加密，解密后需重新解锁才能读取
- 配置修改时异步写入磁盘（`_ = SaveAsync()`），不阻塞 UI
- 切片档位 1-6: 2/4/8/16/32/64 MB，默认 5 (32MB)。切换到 6 档弹硬件性能风险警告。仅影响后续导入。

### 4.7 日志系统

- 子目录分类：`log/error/`（错误）、`log/crash/`（崩溃）、`log/run/`（运行）
- 文件格式：`{prefix}-{start:yyyy-MM-dd}-{end:yyyy-MM-dd}.txt`
- 自动分片：**200KB** 阈值，保持交界处单条日志消息完整性
- 默认级别：Info（可通过 `LogService.MinLevel` 调整）
- 崩溃日志：`LogService.Crash(context, exception)` 记录完整堆栈

### 4.8 auth 权限保护

- auth 目录及文件设 Windows **Hidden 属性**，文件资源管理器默认不可见
- 文件仅后缀名（`.gyropw` / `.gyrock`），无主名称
- `VaultService.ProtectAuthDir()` 在密码创建/改密后自动调用

---

## 5. 系统约束

- **不碰注册表**：不使用 `Microsoft.Win32.Registry`
- **不设环境变量**：仅 `GetFolderPath` 读取系统路径
- **数据自包含**：所有持久化数据均在 `%USERPROFILE%\.Gyroown\`，删除即完全清除
- **完全离线**：默认不联网，密钥保险为可选在线功能

---

## 6. 安全原则

- **程序不主动落盘明文**：应用自身不将解密内容写入临时文件/缓存目录
- **用户操作不受限制**：拖入/拖出、导入/导出、移入/移出均为用户主动行为，应用不替用户兜底安全
- 解密内容进程死后消失
- 临时文件安全擦除：随机数据覆写 → Flush → 删除
- **高危操作锁**：删除/移出/移入/改密/锁定加 `_busy` 互斥，期间禁止关窗
- 最小化不受影响，加解密期间不允许关闭窗口

---

## 7. UI 设计

### 7.1 窗口策略

- **主窗口 + 文件查看窗口**：主窗口承载所有管理功能，媒体查看使用独立窗口（同一时间仅一个）
- 主窗口类 Windows 文件资源管理器布局
- 关闭主窗口 → `AppWindow.Closing` 拦截，**隐藏到托盘**（不退出进程）
- 托盘图标使用 `H.NotifyIcon.WinUI`，优先加载 `favicon.ico`（流式读取），回退到 exe 内嵌图标
- **不允许多个主窗口**（`Process.GetProcessesByName` 检测）
- 默认尺寸 1600×960，最小 800×480

### 7.2 界面语言

- **默认使用简体中文**
- 允许通过 **INI 语言包**切换
- **代码文件（.cs/.xaml）禁止包含中文**：注释、变量、XML doc 一律英文。翻译文本仅存在于 `lang/*.ini`
- **所有源文件编码为 UTF-8 BOM**
- 展示/UI 文本通过 `Loc.Get()` 预留翻译，默认挂载简体中文翻译文件
- 翻译文件须实时跟进 UI 变更

### 7.3 主题

- 主题模式：跟随系统 / 浅色 / 深色（下拉选项本地化显示）
- 默认值：**跟随系统**
- 优先级：**应用内设置 > 操作系统设置**
- `FrameworkElement.RequestedTheme` 只影响 XAML 控件，不影响窗口框架
- 需要通过 `AppWindow.TitleBar` API 显式设置标题栏颜色（背景/前景/悬停/非活跃），确保深色模式在 OS 为浅色时也能完全生效
- 根 Grid 使用 `ApplicationPageBackgroundThemeBrush` 确保窗口体背景跟随主题
- **8 套预设强调色**：Blue, Teal, Green, Orange, Purple, Pink, Red, Graphite
- 主题和强调色独立选择，持久化到 `settings.gyrojson`

### 7.4 预览 vs 查看

- **预览（Preview）**：文件列表处于图标模式（Icons/Tiles）时，在文件图标上显示的缩略图。图片文件显示缩略图，视频文件显示第一帧。这是元数据级别的展示，不解密原始文件。预览图片采用**UniformToFill 模式**（保持宽高比，裁剪溢出部分），确保图标控件范围内都有图片像素且不变形。
- **查看（View）**：双击或右键打开文件时，解密并加载原始媒体文件到独立查看窗口（窗口标题为"查看"而非"预览"）。支持图片（缩放/平移）、视频（播放）、音频（播放）、文本（语法高亮/编码切换）。打开时**聚焦到查看窗口**，不应回到主窗口。

### 7.5 文件管理器功能

- 多视图：Details（列头可拖动调整列宽）/ Icons（加载预览缩略图）/ Tiles
- 虚拟目录树侧边栏（点击文件夹筛选文件列表），**侧边栏右边界可拖动调整宽度**（160-400px，默认 220px）
- 右键上下文菜单：打开 / 导出 / 重命名 / 版本历史 / 删除 / 收藏（单选）；批量导出 / 批量删除（多选）
- **收藏仅在右键菜单中操作**，不在列表中显示星标按钮
- **所有右键菜单项必须有图标**（FontIcon Glyph），无图标项不可接受
- 键盘快捷键：Delete 删除、F2 重命名、Ctrl+C/V/X 内部复制/粘贴/剪切
- **点击空白处释放选中**（不触发打开文件）
- 列头排序：Name/Size/Type/Date 升降序
- 搜索过滤：标题栏搜索框**右对齐**
- 文件大小智能单位：B/KB/MB/GB，≥KB 精确到小数点后 2 位
- 导入/导出/移入/移出均支持批量 + **字节级进度条**（基于已处理数据量/文件大小，500ms 更新间隔）
- 窗口左上角图标正确显示应用 logo（`AppWindow.SetIcon`）

### 7.6 通知系统

- **崩溃性警告**：ContentDialog 弹窗
- **非崩溃性**：底部红色/绿色底栏（可关闭，可点击查看日志）
- 绿色底栏 3s 自动消失

### 7.7 文件夹树

- 加密存储：`meta/.tree.gyrojson`（加密 blob，使用内部密钥对加解密）
- 创建文件夹 → 生成 meta 条目（`IsFolder=true`）+ 更新 `.tree.gyrojson`
- 删除文件夹 → 删除所有子项 meta/data + 从树中移除
- 侧边栏选择文件夹 → 设置 `FilterPath` 筛选文件列表
- 根节点显示为本地化名称（"保险柜" / "Vault"）

### 7.8 内部剪贴板（Ctrl+C/V/X）

> 2026-05-28 设计，待实现

- 应用内使用 Ctrl+C / Ctrl+V / Ctrl+X 进行文件**复制/粘贴/剪切**
- **不写入系统剪贴板**，不影响应用外操作
- **不改变加密文件内容**，只修改 tree 结构（即文件夹树中的路径映射）
- 复制 = 在目标文件夹创建指向同一 hashID 的新条目
- 剪切 = 从源文件夹移除条目，在目标文件夹创建条目
- 粘贴到同级 = 重命名添加 "(1)" 后缀
- 存储在内存中，进程退出即丢失

### 7.9 设置面板

- **全屏覆盖页**，不是侧边栏。覆盖整个窗口，z-index 高于所有底部栏（错误/成功/进度）
- 背景不透明（`SolidBackgroundFillColorBaseBrush`）
- 顶部标题栏：返回箭头 + 标题，下方分隔线
- 内容区最大宽度 640px 居中，可滚动
- 主题下拉选项本地化：跟随系统 / 浅色 / 深色

### 7.10 错误日志

> 2026-05-28

- 完整性检查**不再使用弹窗**（ContentDialog），改为收集问题列表 → 底部红色警告栏
- 底部警告栏点击 → 打开设置页 → 自动滚动到「错误日志」区域
- 错误日志按条目显示，每条包含：图标（按类型着色）、标题、hashID 摘要
- 错误类型：孤立元数据（橙色）、孤立数据（橙色）、无法解密（红色）、数据目录异常（红色）
- 点击条目 → Flyout 弹出详情 + 操作按钮：
  - 孤立元数据/数据 → 「立即清除」/「暂不处理」
  - 无法解密 → 「立即清除」/「保留」
  - 数据目录异常 → 仅展示信息，无操作
- 支持「全部清除」一键处理所有问题
- 清除后自动刷新文件列表和底部栏状态

---

## 8. 开发约束

- **版本号**：定义在 `AppInfo.cs` 常量类中，lang 文件同步
- **编码**：所有 `.cs` / `.xaml` / `.ini` 文件为 **UTF-8 BOM**
- **代码语言**：代码文件中**禁止中文**（注释、变量、XML doc、字符串字面量一律英文），翻译**仅**存在于 `lang/*.ini`。避免跨字符集乱码
- **支持语言**：zh-CN（默认）、zh-TW、en-US、en-GB、ja-JP、ko-KR、fr-FR，定义在 `AppInfo.SupportedLanguages`
- **Git 策略**：用户未明确要求时不得主动使用 git，授权为单次授权
- **文档同步**：用户发言中的设计决策、需求、细节须**实时维护进 UserThoughts.md**
- **README 双语**：所有 README.md 默认英文，每个都需要配套一个 README_zh-CN.md（简体中文）
- **工具目录**：`tools/` 和 `scripts/` 位于仓库根目录，独立于 `Gyroown/` 项目文件夹，不参与主解决方案构建。`tools/` 存放二进制可执行工具源码（需编译），`scripts/` 存放脚本型工具（直接运行源码）
- **工具单文件输出**：`tools/` 下的工具必须配置 `PublishSingleFile=true`，发布后生成单个可执行文件
- **应用内工具**：Gyroown 应用自身需要调用的工具放在 `Gyroown/` 项目文件夹内，不放 `tools/` 或 `scripts/`
- **开发日志**：开发时产生的日志（如 build.log）放入 `docs/DevLog/`，不提交到 git

---

## 9. 本地化架构

### 9.1 嵌入式翻译资源

- zh-CN 和 en-US 翻译硬编码为 DLL 嵌入式资源（`EmbeddedResource`）
- 嵌入式资源不依赖外部文件，`PublishTrimming` 安全
- 原有 `lang/*.ini` 文件兼容保留，可作为外部覆盖

### 9.2 加载优先级

- 主语言加载：`lang/` .ini 文件 > DLL 嵌入式资源 > 字段名降级（`[Section.Key]`）
- 回退语言（当主语言非 zh-CN 时）：zh-CN .ini > zh-CN 嵌入式 > en-US 嵌入式
- .ini 文件优先于嵌入式资源（便于社区热更新翻译）

### 9.3 翻译文件元数据

- 所有 .ini 文件包含 `[__meta__]` 段：`LangCode`（语言代号）、`AppVersion`（适用版本）
- 加载器校验 `AppVersion` 是否匹配 `AppInfo.Version`，不匹配时警告

### 9.4 转换工具

- `tools/transIniToDll/` 提供 .ini 文件转嵌入式资源的功能
- 社区贡献翻译只需编辑 .ini 文件，工具负责嵌入流程
- `validate` 命令检查翻译覆盖率
- `embed` 命令复制 .ini 到 `Resources/Loc/` 并打印 .csproj 配置片段

---

## 10. 文档维护

以下文件需随项目变更**实时同步更新**，不得滞后：

| 文件 | 内容 | 触发条件 |
|------|------|----------|
| `AppInfo.cs` | 版本号、应用名等常量 | 版本变更 |
| `README.md` | 英文项目说明 | 功能/架构/状态变更 |
| `README_zh-CN.md` | 简体中文项目说明 | 同上 |
| `docs/DEVELOP.md` | 架构图、接口签名、实现状态、命名规范 | 接口/模型/存储架构变更 |
| `docs/UserThoughts.md` | 设计决策记录 | 任何新决策或需求，**用户发言实时跟进** |
| `docs/api/key-insurance.md` | 密钥保险 API 规范 | API 端点/参数变更 |
| `lang/zh-CN.ini` | 简体中文翻译 | 新增/修改 UI 文本 |
| `lang/en-US.ini` | English translation | 同上 |
| `.gitignore` | 忽略规则 | 新增需排除的文件类型 |
