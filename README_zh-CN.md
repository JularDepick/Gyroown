<div align="center">

# Gyroown

[![](https://img.shields.io/badge/Copyright-Gyroown-0066AA)](./COPYRIGHT)
[![](https://img.shields.io/badge/License-AGPL--3.0--or--later-yellow)](./LICENSE)
[![](https://img.shields.io/badge/Commercial-Closed--Source_Paid-red)](./COMMERCIAL.md)

[[English]](./README.md)
[[简体中文]](./README_zh-CN.md)

</div>

---

完全离线的动态加密仓库，在保护数据的同时支持从仓库内部向外部传输数据。

**用户密码验证身份 → 内部固定非对称密钥对（私钥加密 / 公钥解密）加解密文件**，密钥对原始明文永不改变。加密数据存于 `%USERPROFILE%\.Gyroown\data\`，内部密钥密文存于 `auth\vault-key.enc`，由用户密码双向加密保护。解密内容仅存在于内存，从不落盘明文。

- **类文件管理器 UI**：多视图列表、虚拟目录树、拖入加密 / 拖出解密
- **后台驻留**：关闭主窗口不退出，系统托盘恢复，不允许多实例
- **四种解锁方式**：6 位 PIN / 九宫格手势 / 自定义密码 / 图片密码
- **国际化**：默认简体中文，支持 INI 语言包切换
- **8 套强调色** + 系统/浅色/深色主题

v0.1.0 · [GitHub](https://github.com/JularDepick/Gyroown)

所有功能已完整实现。单窗口设计：密码设置/解锁/文件管理/设置均在同一窗口内。

---

### 名字由来

- Holy fucking `Gyroown`, that's just fucking `Orange` read backwards.
- What the fuck is `Orange`?
- That's just a favorite fruit of `doro`.
- Who is `doro`?
- `Doro` is a cute anime baby with pink furry hair.

---

## 项目结构

```
Gyroown/
├── App.xaml / App.xaml.cs
├── MainWindow.xaml / .cs           # 文件管理器主窗口
├── lang/                           # 语言包 (INI)
│   ├── zh-CN.ini                   # 简体中文（默认）
│   └── en-US.ini                   # English
├── Controls/                       # 自定义 UI 控件
│   ├── VaultFileListView           # 多视图文件列表
│   ├── VaultSidebar                # 虚拟目录树
│   ├── VaultStatusBar              # 状态栏
│   └── TitleBarControl             # 自定义标题栏
├── Views/                          # 控件
│   ├── IPasswordControl            # 密码控件统一接口
│   ├── UnlockControl               # 解锁控件
│   ├── PasswordSetupControl        # 密码设置控件
│   ├── PinPasswordControl          # 6 位 PIN
│   ├── GesturePasswordControl      # 九宫格手势
│   ├── CustomPasswordControl       # 自定义密码
│   └── PicturePasswordControl      # 图片密码
├── Models/                         # 数据模型
│   ├── VaultFileItem / VaultFolder
│   ├── PasswordType / PasswordConfig
├── Services/                       # 接口 + 实现
│   ├── PasswordService             # PBKDF2 密码哈希/验证
│   ├── EncryptionService           # RSA 2048 + AES-256-GCM
│   ├── VaultService                # 加密文件仓库
│   ├── ThemeService                # 主题 + 强调色管理
│   ├── DragDropService             # 拖放协调
│   ├── Loc                         # 静态本地化辅助类
│   └── ILocalizationService        # 语言包接口
└── Gyroown.csproj                  # .NET 8 + WinUI 3

Gyroown (Package)/                  # MSIX 打包
```

### 技术栈

| 层 | 技术 |
|----|------|
| UI 框架 | **WinUI 3** (Windows App SDK 2.1) |
| 语言 | C# 12, XAML |
| 运行时 | .NET 8 |
| 加密 | RSA 2048, AES-256-GCM, PBKDF2-SHA256 |
| 打包 | MSIX |
| 最低系统 | Windows 10 1809 (build 17763) |

### 架构

- **GUI**：WinUI 3，单窗口文件管理器布局，主题色可切换
- **加密**：RSA 2048 + AES-256-GCM，`data/` + `meta/` 分目录 1:1 储存
- **密钥**：内部固定非对称密钥对，首次生成后永不改变。改密码只重加密 `auth/vault-key.enc`
- **数据**：全量落盘 `%USERPROFILE%\.Gyroown\`，解密仅内存，不碰注册表和环境变量

### 开发文档

- [开发计划](docs/plans.ai/v0.1.0-20260526.md)
- [架构与接口细节](docs/DEVELOP.md)
- [设计决策记录](docs/UserThoughts.md)
