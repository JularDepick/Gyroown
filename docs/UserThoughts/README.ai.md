# UserThoughts — Design Decision Index

> Authoritative record of all user design decisions and requirements for the Gyroown project.
> Current version: **v0.1.1**

---

## How to Use

This directory contains the complete design decision record for Gyroown, split by dimension for easy navigation. Each file is self-contained and covers one major topic area.

**For AI assistants**: Read all files in this directory to get full context before making design or implementation decisions. When the user expresses new requirements or design decisions, add them to the appropriate dimension file.

**For developers**: Each file corresponds to a logical domain. Cross-reference when changes span multiple domains.

---

## File Map

| File | Topic | Key Contents |
|------|-------|-------------|
| [01-security-encryption.md](01-security-encryption.md) | Security & Encryption | RSA/AES hybrid encryption, key pair management, key insurance, auth recovery |
| [02-password-system.md](02-password-system.md) | Password System | 4 password types (PIN/gesture/custom/picture), storage, lockout, default type |
| [03-storage-architecture.md](03-storage-architecture.md) | Storage Architecture | Directory structure, file formats, hashID, config separation, logging, auth protection |
| [04-system-constraints.md](04-system-constraints.md) | System Constraints & Security Principles | No registry, no env vars, offline-first, no plaintext on disk, user action freedom |
| [05-ui-design.md](05-ui-design.md) | UI Design | Window strategy, tray icon, language, theme, file manager, notifications, settings, error log |
| [06-development-constraints.md](06-development-constraints.md) | Development & Localization | Versioning, encoding, code language, git policy, localization architecture |
| [07-doc-maintenance.md](07-doc-maintenance.md) | Documentation Maintenance | Sync rules, file triggers, maintenance policies |

---

## Architecture Overview

```
UserThoughts/
├── README.ai.md              ← You are here (index + architecture + rules)
├── 01-security-encryption.md
├── 02-password-system.md
├── 03-storage-architecture.md
├── 04-system-constraints.md
├── 05-ui-design.md
├── 06-development-constraints.md
└── 07-doc-maintenance.md
```

### Relationship to Other Docs

| Document | Role |
|----------|------|
| `docs/UserThoughts/` (this directory) | **Authoritative** design decisions — what the user wants and why |
| `docs/DEVELOP.md` | Technical architecture — how things are implemented |
| `README.md` / `README_zh-CN.md` | Project overview — what the project is |

Priority: **UserThoughts > project progress > README**

---

## Maintenance Rules

1. **维护不得丢失用户表述的细节** — Maintenance must not lose details expressed by the user
2. Each dimension file is the authoritative source for its topic
3. New content goes in the appropriate dimension file; if no fit exists, create a new file and update this index
4. Date-stamp significant additions with `> YYYY-MM-DD` blockquotes
5. The original `docs/UserThoughts.md` is deprecated — all content lives here
6. When user expresses a new design decision, add it **immediately** to the correct file
7. Cross-reference related decisions across files with markdown links

---

## Quick Reference: Design Principles

- **Offline-first**: no network by default; key insurance is opt-in
- **User sovereignty**: user actions are unrestricted; the app does not "protect" the user from themselves
- **No plaintext persistence**: decrypted content never written to disk by the app
- **Self-contained**: all data in `%USERPROFILE%\.Gyroown\`, delete to fully clean up
- **No system pollution**: no registry, no environment variables
- **Code in English**: all source files use English only; translations in `lang/*.ini`
