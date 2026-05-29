<div align="center">

# Gyroown

[![](https://img.shields.io/badge/Copyright-Gyroown-0066AA)](./COPYRIGHT)
[![](https://img.shields.io/badge/License-AGPL--3.0--or--later-yellow)](./LICENSE)
[![](https://img.shields.io/badge/Commercial-Closed--Source_Paid-red)](./COMMERCIAL.md)

[[English]](./README.md)
[[简体中文]](./README_zh-CN.md)

</div>

---

A fully local, offline dynamic encrypted repository that safeguards your data while enabling you to transmit data externally from within it to any destination.

**User password authenticates identity → internal fixed asymmetric key pair (private key encrypts / public key decrypts) secures files**. The key pair plaintext never changes after initial generation. Encrypted data stored at `%USERPROFILE%\.Gyroown\data\`, vault key ciphertext at `auth\vault-key.enc` protected by user password. Decrypted content exists only in RAM — never written to disk.

- **File Explorer-like UI**: multi-view file list, virtual directory tree, drag-in to encrypt / drag-out to decrypt
- **Background resident**: closing the window hides to system tray, not exit. Single instance only.
- **Four unlock methods**: 6-digit PIN / 3×3 gesture pattern / custom password / picture password
- **i18n**: default Simplified Chinese, INI language pack switching
- **8 accent presets** + system/light/dark themes

v0.1.0 · [GitHub](https://github.com/JularDepick/Gyroown)

All features fully implemented. Single-window design: setup / unlock / file management / settings all in one window.

---

### Name

- Holy fucking `Gyroown`, that's just fucking `Orange` read backwards.
- What the fuck is `Orange`?
- That's just a favorite fruit of `doro`.
- Who is `doro`?
- `Doro` is a cute anime baby with pink furry hair.

---

## Project Structure

```
Gyroown/
├── App.xaml / App.xaml.cs
├── MainWindow.xaml / .cs           # Main window
├── lang/                           # Language packs (INI)
│   ├── zh-CN.ini                   # Simplified Chinese (default)
│   └── en-US.ini                   # English
├── Controls/                       # Custom controls
│   ├── VaultFileListView           # Multi-view file list
│   ├── VaultSidebar                # Virtual directory tree
│   ├── VaultStatusBar              # Status bar
│   └── TitleBarControl             # Custom title bar
├── Views/                          # View controls
│   ├── IPasswordControl            # Password control interface
│   ├── UnlockControl               # Unlock view
│   ├── PasswordSetupControl        # Password setup view
│   ├── PinPasswordControl          # 6-digit PIN
│   ├── GesturePasswordControl      # Gesture pattern
│   ├── CustomPasswordControl       # Custom password
│   └── PicturePasswordControl      # Picture password
├── Models/                         # Data models
│   ├── VaultFileItem / VaultFolder
│   ├── PasswordType / PasswordConfig
├── Services/                       # Interfaces + implementations
│   ├── PasswordService             # PBKDF2 password hashing
│   ├── EncryptionService           # RSA 2048 + AES-256-GCM
│   ├── VaultService                # Encrypted file storage
│   ├── ThemeService                # Theme + accent management
│   ├── DragDropService             # Drag-drop coordination
│   ├── Loc                         # Static localization helper
│   └── ILocalizationService        # Language pack interface
└── Gyroown.csproj                  # .NET 8 + WinUI 3
```

### Tech Stack

| Layer | Technology |
|-------|-----------|
| UI Framework | **WinUI 3** (Windows App SDK 2.1) |
| Language | C# 12, XAML |
| Runtime | .NET 8 |
| Crypto | RSA 2048, AES-256-GCM, PBKDF2-SHA256 |
| Packaging | Unpackaged (standalone .exe) |
| Min OS | Windows 10 1809 (build 17763) |

### Architecture

- **GUI**: WinUI 3, single-window File Explorer layout, theme switching
- **Encryption**: RSA 2048 + AES-256-GCM, `data/` + `meta/` 1:1 per file
- **Keys**: Internal fixed asymmetric key pair, plaintext never changes. Password change only re-encrypts `auth/vault-key.enc`
- **Data**: Fully stored in `%USERPROFILE%\.Gyroown\`, decryption in RAM only, no registry/environment variable usage

### Documentation

- [Development Plan](docs/plans.ai/v0.1.0-20260526.md)
- [Architecture & Interface Details](docs/DEVELOP.md)
- [Design Decision Log](docs/UserThoughts.md)
