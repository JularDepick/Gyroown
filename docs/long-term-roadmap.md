# Gyroown Long-Term Roadmap

> Last updated: 2026-05-27
> Status: Active

---

## Overview

| Version | Theme | Core Goal | Status |
|---------|-------|-----------|--------|
| v0.1.1 | GUI skeleton + core | Complete UI + encryption + localization | Current |
| v0.1.2 | Quality polish | Fix defects, optimize experience | Planned |
| v0.1.3 | Feature enhancement | Video preview, search, batch ops | Planned |
| v0.2.0 | Security hardening | Key backup, audit logs, tamper detection | Planned |
| v0.3.0 | Performance | Large file streaming, memory optimization | Planned |
| v0.4.0 | User experience | Animations, shortcuts, drag-drop, notifications | Planned |
| v0.5.0 | Extensions | Cloud sync, multi-device, plugin system | Planned |
| v0.6.0 | Internationalization | Multi-language, localization tools | Planned |
| v0.7.0 | Enterprise | Multi-user, permissions, audit | Planned |
| v0.8.0 | Release prep | Docs, tests, packaging, distribution | Planned |
| v1.0.0 | Stable release | Production ready | Planned |

---

## Version Details

### v0.1.1 — GUI Skeleton + Core (Current)

Complete application shell with encryption, password system, file management, localization, and all core features. See `task-queue.md` for detailed task history.

### v0.1.2 — Quality Polish

Fix remaining defects, optimize performance for large files, improve error handling, and polish UI details.

### v0.1.3 — Feature Enhancement

Video preview generation, advanced file search (type/size/date filters), batch operation progress, file preview enhancements (PDF/Office), version history management, favorites system.

### v0.2.0 — Security Hardening

Key backup and recovery, security audit logging, tamper detection, password strength detection, two-factor authentication (TOTP), enhanced secure erase.

### v0.3.0 — Performance

Large file streaming (avoid OOM), memory optimization (<200MB), concurrent encryption, startup optimization, database indexing, compression before encryption.

### v0.4.0 — User Experience

Transition animations, keyboard shortcuts (Ctrl+C/V/X/Z), enhanced drag-drop, system notifications, right-click menu enhancements, file tags.

### v0.5.0 — Extensions

Cloud sync (OneDrive/Google Drive), multi-device support, plugin system, REST API, CLI tool, auto-update.

### v0.6.0 — Internationalization

Translation completion, localization tooling, date/time formatting, number formatting, RTL support, font bundling.

### v0.7.0 — Enterprise

Multi-user support, permission management, audit log export, policy management, LDAP/AD integration, compliance reports.

### v0.8.0 — Release Prep

Documentation, test coverage (80%+), packaging (MSIX/MSI), auto-update, community building, legal compliance.

### v1.0.0 — Stable Release

Production release, user feedback collection, issue tracking, continuous improvement.

---

## Dependency Graph

```
v0.1.1 (current)
  │
  ▼
v0.1.2 (quality)
  │
  ▼
v0.1.3 (features)
  │
  ├─→ v0.2.0 (security)
  │     │
  │     ▼
  │   v0.3.0 (performance)
  │
  └─→ v0.4.0 (UX)
        │
        ▼
      v0.5.0 (extensions)
        │
        ├─→ v0.6.0 (i18n)
        │
        └─→ v0.7.0 (enterprise)
              │
              ▼
            v0.8.0 (release prep)
              │
              ▼
            v1.0.0 (stable)
```

---

## Resource Estimates

| Version | Duration | Key Dependencies |
|---------|----------|-----------------|
| v0.1.2 | 1-2 weeks | — |
| v0.1.3 | 2-3 weeks | — |
| v0.2.0 | 2-3 weeks | Backend API (key insurance) |
| v0.3.0 | 2-3 weeks | — |
| v0.4.0 | 2-3 weeks | — |
| v0.5.0 | 3-4 weeks | Cloud SDK integration |
| v0.6.0 | 2-3 weeks | — |
| v0.7.0 | 3-4 weeks | Enterprise requirements |
| v0.8.0 | 2-3 weeks | — |
| v1.0.0 | 1-2 weeks | — |

**Total**: ~20-30 weeks (5-8 months)

---

*For detailed task lists and status, see `task-queue.md`.*
