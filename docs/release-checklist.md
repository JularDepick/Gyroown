# Gyroown v0.1.1 Release Checklist

> Last updated: 2026-05-27
> Status: Development

---

## 1. Build Verification

- [x] `dotnet build` passes (0 errors, pre-existing WMC9999 non-blocking)
- [ ] `dotnet publish` produces runnable output
- [ ] No runtime crashes on clean launch

## 2. Feature Completeness

### Core
- [x] App startup flow (password setup/unlock)
- [x] File encrypted import
- [x] File decrypted export
- [x] File delete (secure erase)
- [x] Folder create/delete
- [x] Virtual folder tree navigation

### Password System
- [x] PIN password (6-digit)
- [x] Gesture password (3x3 grid)
- [x] Custom password (6-32 chars)
- [x] Picture password (click coordinates)
- [x] Password change (all types)
- [x] Error lockout (5 attempts / 30s)
- [x] Unlock UI auto-detects stored password type

### UI Features
- [x] File list 3-view switch (Details/Icons/Tiles)
- [x] Theme switch (System/Light/Dark)
- [x] Accent color (8 presets)
- [x] Language switch (7 languages)
- [x] Search with filters and history
- [x] Tray minimize (close-to-tray)
- [x] Single instance detection
- [x] Keyboard shortcuts
- [x] Settings panel with animation

### Drag-Drop
- [x] Drag-in encrypted storage
- [x] Move-in / Move-out (clipboard)

### Data Features
- [x] Chunked storage (configurable 2-64MB)
- [x] Thumbnail preview (image/video)
- [x] File version history
- [x] Favorites system
- [x] Advanced search (type/size/date filters)
- [x] Batch operations with progress

### Localization
- [x] 7 language INI packs
- [x] Embedded DLL resources (zh-CN, en-US)
- [x] Runtime switch with fallback
- [x] transIniToDll community tool

### Security
- [x] PBKDF2-SHA256 password hashing
- [x] RSA 2048 + AES-256-GCM encryption
- [x] Secure erase (random overwrite)
- [x] auth directory hidden
- [x] FixedTimeEquals anti-timing
- [x] High-risk operation mutex

## 3. Known Issues

| # | Issue | Priority | Status |
|---|-------|----------|--------|
| 1 | Tray: no double-click restore / context menu | P2 | Open |
| 2 | XAML compiler WMC9999 | P3 | Pre-existing |

## 4. Performance Checks

- [ ] Large file import (>100MB) does not freeze UI
- [ ] File list 1000+ items scrolls smoothly
- [ ] Memory usage reasonable (<200MB idle)

## 5. Security Checks

- [x] Password hash storage (PBKDF2 100K iterations)
- [x] Key pair not stored in plaintext on disk
- [x] Secure erase (random data overwrite)
- [x] auth directory Hidden attribute
- [x] FixedTimeEquals constant-time comparison

## 6. Release Artifacts

- [ ] `dotnet publish` generates distributable package
- [ ] Contains favicon.ico / favicon.png
- [ ] Contains language packs (zh-CN.ini / en-US.ini embedded)
- [ ] Version confirmed (v0.1.1)

---

*Update this checklist as testing progresses. For task status, see `task-queue.md`.*
