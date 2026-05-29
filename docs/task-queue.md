# Gyroown Task Queue

> Last updated: 2026-05-27
> Current version: v0.1.1

---

## v0.1.1 — GUI Skeleton + Core (Current)

All core features complete. See `DEVELOP.md` section 6 for full implementation status.

### Completed

- Application shell (single instance, tray, startup routing)
- Password system (PBKDF2, 4 types, confirmation, lockout, salt)
- Encryption core (RSA 2048 + AES-256-GCM, key pair, file encrypt/decrypt)
- File vault (CRUD, encrypted index, secure delete, virtual folder tree)
- Localization (7 languages, runtime switch, embedded fallback)
- Drag-drop (batch import/export, secure cleanup)
- Theme switching (system/light/dark + accent colors, encrypted persistence)
- Settings panel (theme + accent + language + password + about)
- In-app viewer (image zoom/pan, text syntax highlight, video/audio playback)
- Move in/out (clipboard import/export)
- Thumbnail preview (JPEG <=1MB encrypted)
- Log system (error/crash/run subdirs, 200KB slicing)
- Error notifications (red/green banner, clickable log link)
- Chunked storage (auto-slice, hex numbering, configurable tiers)
- Configurable chunk size (2-64MB, 6 tiers, encrypted config)
- Picture password image picker (FileOpenPicker)
- Sidebar folder filtering (FilterPath + .tree.gyrojson persistence)
- Right-click export (ExportRequested -> FileSavePicker)
- Window behavior (close-to-tray, native buttons, 800x480 min)
- Security principles (re-verify on restore, secure erase, no registry/env)
- hashID spec (SHA256 first 32 hex chars, lowercase)
- High-risk operation lock (delete/export/import/change-password/lock)
- Password details (confirmation, 5x lockout 30s, auto-verify, PIN backspace)
- auth protection (Hidden attribute, suffix-only filenames)
- Keyboard shortcuts (Ctrl+I/E/N/L/F/A, Enter, Backspace)
- Settings panel animation (Storyboard, 250ms, CubicEase)
- File list performance (ContainerContentChanging lazy load, preview cache)
- Banner animation (slide-in/fade-out, 200ms, CubicEase)
- Progress bar animation (DoubleAnimation, 300ms, CubicEase)
- Search enhancement (search history, empty state hint)
- Global exception handling (App.UnhandledException + LogService)
- Disk space check (pre-import AvailableFreeSpace)
- Log levels (Debug/Info/Warn/Error + MinLevel)
- File type icons (ContentType-based icon glyphs)
- Video preview generation (Shell thumbnail, auto-generate for video files)
- Advanced search filter (file type, size range, date range)
- Batch operations (Ctrl/Shift multi-select, progress dialog)
- File preview enhancement (zoom/pan, syntax highlighting)
- File version history (VersionHistoryService, rollback, secure delete)
- Favorites (FavoritesService, drag sort, group management)
- Key insurance client (InsuranceService, HTTP stub, awaiting backend API)
- Embedded DLL localization (zh-CN + en-US as EmbeddedResource)
- INI metadata headers ([__meta__] LangCode + AppVersion)
- transIniToDll community tool (tools/transIniToDll/)

### Open Issues

| # | Issue | Priority | Notes |
|---|-------|----------|-------|
| 1 | Tray icon: no double-click to restore, no context menu | P2 | Basic close-to-tray works |
| 2 | XAML compiler WMC9999 internal error | P3 | Pre-existing, non-blocking |

---

## Next: v0.1.2 — Quality Polish

Fix remaining defects, optimize performance, polish UI details.

---

## Later: v0.1.3+ Feature Enhancement

See `long-term-roadmap.md` for version plan and `UserThoughts.md` for design decisions.

---

*This document tracks task status. For architecture and implementation details, see `DEVELOP.md`. For design decisions, see `UserThoughts.md`.*
