# UserThoughts — 用户设计决策记录

> 记录本项目中用户提出的全部设计细节与架构决策。按分类整理，实时跟进。

---

## 1. 密钥体系

### 1.1 内部固定密钥对

- 非对称密钥对（RSA 2048）：**私钥加密写入 / 公钥解密读取**
- **二者均不可公开**，不对外暴露
- `GenerateVaultKeyPair()` 仅在**首次设置时调用一次**，之后原始明文**永不改变、永不重生成**
- 内部密钥对原始明文是**唯一用于加解密文件的凭证**
- `.NET RSA.Decrypt()` 需私钥参数，故 `DecryptBlob` 内部使用 `ImportRSAPrivateKey`（私钥含公钥分量）

### 1.2 密钥保护

- 用户密码 → `DeriveUserKey(password, salt)` → userKey
- userKey 用于双向加密保护密钥对密文：`auth\.gyrock`（ck=corekey）
- 改密码：只重加密 `.gyrock`，**密钥对明文不变，文件不动**

### 1.3 改密流程

> 2026-05-26

- 需同时持有**旧密码**和**新密码**
- 旧密码 → 解密 `.gyrock` → 还原密钥对
- 新密码 → 加密密钥对 → 写回 `.gyrock`
- 确认新密码生效后**才抛弃旧密码**
- `PasswordService.ChangePasswordAsync` 返回 `(oldUserKey, newUserKey)` 供调用者重加密

### 1.4 卸载策略

- 卸载时只有用户**明确选择「删除数据及密钥」**才能删除 `auth\` 和 `data\`

### 1.5 密钥保险（密钥恢复）

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

---

## 2. 存储架构

### 2.0 路径约定

> **`~` 即为用户文件夹目录**（`%USERPROFILE%`，即 `C:\Users\<username>`）。全文所有 `~` 均指此目录。

### 2.1 根目录

```
%USERPROFILE%\.Gyroown\
├── auth\                         (Hidden 属性)
│   ├── .gyropw                   # 密码哈希 (pw=password, 仅后缀)
│   ├── .gyrock                   # 内部密钥对密文 (ck=corekey, 仅后缀)
│   └── insurance.gyrock          # 密钥保险备份
├── data\
│   ├── {hashID}.gyrodt           # 小文件 (dt=data)
│   └── {hashID}\                  # 大文件分片目录
│       ├── c0000.gyrodt           # 4位十六进制编号, 前导零
│       └── c0001.gyrodt
├── meta\
│   └── {hashID}.gyromt           # 元数据 (mt=meta)
├── preview\
│   └── {hashID}.gyropv           # 加密缩略图 (pv=preview)
├── log\
│   ├── error-{ymd}-{ymd}.txt     # 错误日志 100KB 切片
│   └── run-{ymd}-{ymd}.txt       # 运行日志
├── settings.json                 # 明文 UI 配置
└── config.gyrojson               # 加密核心配置 (vault key pair)
```

### 2.2 加密数据文件

- **一个原始文件对应一个加密数据文件**
- 后缀 `.gyrodt`（dt=data），使用内部固定密钥对进行加密
- 格式：`[4B hdrLen][RSA-OAEP header{ aesKey, aesNonce, len }][4B bodyLen][AES-GCM body]`
- 分片存储：> 阈值时按 `data/{hashID}/c{xxxx}.gyrodt` 切片，4 位十六进制小写编号

### 2.3 元数据文件

- **一个元数据文件对应一个加密数据文件**（1:1 映射，同 hashID）
- 后缀 `.gyromt`（mt=meta），使用内部固定密钥对加密
- 包含：文件名、原始大小、MIME 类型、虚拟路径、创建/修改时间、ChunkCount/ChunkSize、PreviewId
- 文件列表通过扫描 `*.gyromt` 构建，无需索引文件
- **`meta/` 和 `data/` 是强绑定的**：必须同时存在或同时不存在

### 2.4 hashID

- 算法: `SHA256(原始内容)` → 十六进制 → 取前 32 位 → **全小写**
- `ListItems` 扫描 `*.gyromt` 通配，不假定 ID 长度 — 前后版本兼容
- 分片编号 `c{i:x4}` 同样小写

### 2.5 配置分离

| 文件 | 格式 | 内容 | 编辑方式 |
|------|------|------|----------|
| `settings.json` | 明文 JSON | 主题/语言/强调色 | 应用 UI |
| `config.gyrojson` | 加密 blob（vault key pair） | 切片档位等核心参数 | **仅**应用 UI |

切片档位 1-6: 2/4/8/16/32/64 MB，默认 5 (32MB)。切换到 6 档弹硬件性能风险警告。仅影响后续导入。

### 2.6 auth 权限保护

- auth 目录及文件设 Windows **Hidden 属性**，文件资源管理器默认不可见
- 文件仅后缀名（`.gyropw` / `.gyrock`），无主名称
- `VaultService.ProtectAuthDir()` 在密码创建/改密后自动调用

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

### 3.3 密码设置

- **二次输入验证**：第一步输入 → 第二步确认 → 比对一致才生效
- 可返回上一步修改
- Enter 键触发"下一步"/"设置"，Esc 返回

### 3.4 密码存储

- PBKDF2-SHA256，**100,000 迭代**
- 32 字节随机 salt，32 字节 hash
- 恒定时间比较（`CryptographicOperations.FixedTimeEquals`）
- 存储格式：JSON `{ type, salt, hash, iterations }` → `auth\.gyropw`

### 3.5 锁定策略

- 连续 **5 次**错误 → 锁定 **30 秒**，倒计时显示

---

## 4. 加密算法

### 4.1 混合加密方案

- **RSA 2048-bit** 非对称密钥对（PKCS#1 格式）
- **AES-256-GCM** 文件体加密（随机 key/nonce，12B nonce，16B tag）
- RSA-OAEP-SHA256 加密 AES 密钥和元数据
- Blob 格式：`[4B hdrLen][RSA-OAEP header][4B bodyLen][AES-GCM body]`

### 4.2 统一加密方法

- `EncryptBlob(data, privateKey)` — data 文件和 meta 文件统一使用
- `DecryptBlob(blob, privateKey)` — .NET 需私钥参数，私钥含公钥分量

### 4.3 分片加密

- 大文件按 `config.gyrojson` 中档位切片，每片独立 `EncryptBlob`
- 导出时按序拼接解密
- 删除时清理整个分片子目录

---

## 5. 系统约束

- **不碰注册表**：不使用 `Microsoft.Win32.Registry`
- **不设环境变量**：仅 `GetFolderPath` 读取系统路径
- **数据自包含**：所有持久化数据均在 `%USERPROFILE%\.Gyroown\`，删除即完全清除

## 6. 安全原则

- 解密内容**仅存在于安全运行内存**，**从不主动落盘明文**（缓存/临时目录均禁止）
- 仅用户主动选择「导出」「移出」时，明文写入用户指定的目标位置
- **拖出已启用**：拖出时解密到临时文件，拖动完成后自动清理。安全性由关闭窗口后重新验证密码保障
- 解密内容进程死后消失
- 临时文件安全擦除：随机数据覆写 → Flush → 删除
- **高危操作锁**：删除/移出/移入/改密/锁定加 `_busy` 互斥，期间禁止关窗
- 最小化不受影响，加解密期间不允许关闭窗口

---

## 7. UI 设计

### 7.1 窗口策略

- **所有 UI 均在主窗口内**，不弹出新窗口
- 主窗口类 Windows 文件资源管理器布局
- 关闭主窗口 → `AppWindow.Closing` 拦截，**隐藏到托盘**（不退出进程）
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

- 主题模式：跟随系统 / 浅色 / 深色（`FrameworkElement.RequestedTheme`）
- **8 套预设强调色**：Blue, Teal, Green, Orange, Purple, Pink, Red, Graphite
- 主题和强调色独立选择，持久化到 `settings.json`

### 7.4 文件管理器功能

- 多视图：Details（列头可拖动调整列宽）/ Icons（加载预览缩略图）/ Tiles
- 虚拟目录树侧边栏（点击文件夹筛选文件列表）
- 右键上下文菜单：导出 / 重命名 / 删除
- 键盘快捷键：Delete 删除、F2 重命名
- 列头排序：Name/Size/Type/Date 升降序
- 搜索过滤：标题栏搜索框实时过滤
- 文件大小智能单位：B/KB/MB/GB，≥KB 精确到小数点后 2 位
- 导入/导出/移入/移出均支持批量 + 进度条

### 7.5 通知系统

- **崩溃性警告**：ContentDialog 弹窗
- **非崩溃性**：底部红色/绿色底栏（可关闭，可点击查看日志）
- 绿色底栏 3s 自动消失

---

## 8. 文档维护

以下文件需随项目变更**实时同步更新**，不得滞后：

| 文件 | 内容 | 触发条件 |
|------|------|----------|
| `README.md` | 英文项目说明 | 功能/架构/状态变更 |
| `README_zh-CN.md` | 简体中文项目说明 | 同上 |
| `docs/DEVELOP.md` | 架构图、接口签名、实现状态、命名规范 | 接口/模型/存储架构变更 |
| `docs/UserThoughts.md` | 设计决策记录 | 任何新决策或需求 |
| `docs/api/key-insurance.md` | 密钥保险 API 规范 | API 端点/参数变更 |
| `lang/zh-CN.ini` | 简体中文翻译 | 新增/修改 UI 文本 |
| `lang/en-US.ini` | English translation | 同上 |
| `.gitignore` | 忽略规则 | 新增需排除的文件类型 |
| 全部 `.cs`/`.xaml`/`.ini` | UTF-8 BOM 编码 | PowerShell 操作后需重设 |
