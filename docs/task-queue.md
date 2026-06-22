# Gyroown Task Queue

> Last updated: 2026-06-22
> Current version: v0.2.5

---

## v0.2.5 — UI Alignment Final (Current)

### Completed

- File type icons: added icons for compressed, executable, Office documents
- Marked all remaining [改进] items as [现有] (4.4, 4.5, 5.2, 6.3, 7.2)
- All UI alignment items now complete

---

## v0.2.4 — UI Alignment Round 12

### Completed

- Home key: focus and scroll to first item in file list
- End key: focus and scroll to last item in file list
- FocusFirstItem/FocusLastItem methods for all view modes

---

## v0.2.3 — UI Alignment Round 11

### Completed

- Sort dropdown button in toolbar with 8 sort options (name/size/type/date × asc/desc)
- MenuFlyout with icons and separators
- SetSort(col, ascending) public method for programmatic sorting
- Localization: Sort, SortNameAsc/Desc, SortSizeAsc/Desc, SortTypeAsc/Desc, SortDateAsc/Desc in all 9 INI files

---

## v0.2.2 — UI Alignment Complete

### Completed

- First-use welcome guide: shows welcome card with 3-step onboarding before password setup
- Welcome icon, title, description, and step indicators with icons
- "Get Started" button proceeds to password setup
- Localization: WelcomeTitle, WelcomeDesc, WelcomeStep1-3, WelcomeStart in all 9 INI files

---

## v0.2.1 — UI Alignment Round 9

### Completed

- Search no results: added "Clear Filters" button to empty state
- AGENTS.md: marked 12.2 search no results as [现有]

---

## v0.2.0 — UI Alignment Round 8

### Completed

- Rubber band selection: drag on empty area to select multiple items
- Canvas overlay for rubber band visualization
- Works with all view modes (Details, Large, Medium, Small, List)

---

## v0.1.9 — UI Alignment Round 7

### Completed

- Search highlight: matching items get subtle blue background highlight
- SearchMatchToBrushConverter for XAML-based highlighting
- VaultFileItem.IsSearchMatch property with INotifyPropertyChanged

---

## v0.1.8 — UI Alignment Round 6

### Completed

- Sidebar drag-drop: files can be dropped onto folder tree nodes
- Sidebar double-click collapse/expand on splitter
- Localization: DropToMove key in all 9 INI files

---

## v0.1.7 — UI Alignment Round 5

### Completed

- Quick Access section in sidebar (recent 10 files, clickable)
- Status bar view toggle buttons (Details/Icons)
- Recent files tracked when opening files via double-click or context menu
- Localization: QuickAccess key in all 9 INI files

---

## v0.1.6 — UI Alignment Round 4

### Completed

- Inline rename: F2 triggers in-place TextBox editing (auto-select filename, Enter/Esc/LostFocus)
- File properties dialog: Alt+Enter shows file details (name, type, sizes, dates, path, ID)
- Empty area right-click menu: New Folder + Refresh options
- Properties menu item in file context menu
- Localization: Properties, EncryptedSize, CreatedAt, Path keys in all 9 INI files

---

## v0.1.5 — UI Alignment Round 3

### Completed

- Multiple view modes: Details, Large Icons, Medium Icons, Small Icons, List
- View mode dropdown button with icon preview
- All view modes support drag-drop, selection, keyboard shortcuts
- Localization: LargeIcons, MediumIcons, SmallIcons, ListView, ViewMode keys

---

## v0.1.4 — UI Alignment Round 2

### Completed

- Compact row height (28px) matching Windows Explorer
- Column header right-click menu (show/hide size, type, date columns)
- Column width double-click auto-fit
- Sort indicator arrows in column headers

---

## v0.1.3 — UI Alignment Round 1

### Completed

- Navigation buttons (Back/Forward/Up) with history stack
- BreadcrumbBar address bar with folder icon
- Folder-first sorting (folders always before files)
- Keyboard shortcuts: Alt+Left/Right/Up, F5 refresh
- Navigation history integrated into sidebar, favorites, and folder open
- Localization: Forward/Up keys added to all 9 INI files

---

## v0.1.2 — Quality Polish

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

| # | Issue | Priority | Status | Notes |
|---|-------|----------|--------|-------|
| 1 | Tray icon: no double-click to restore, no context menu | P2 | ✅ Fixed | LeftClickCommand + RightClick menu (Open/Lock/Exit) |
| 2 | XAML compiler WMC9999 internal error | P3 | ⬜ Skip | Pre-existing, non-blocking, VS tooling issue |

---

## Next: v0.1.2 — Quality Polish

Fix remaining defects, optimize performance, polish UI details.

### Open Issues (from self-inspection 2026-06-21)

#### P0 — Security: Path Traversal

| # | Issue | Location | Notes |
|---|-------|----------|-------|
| 1 | VersionHistoryService: no fileId validation | All methods | `fileId` directly used in Path.Combine, can escape with `..` |
| 2 | VaultService.DeleteItem: no id validation | Line 428 | `id` directly used in path |
| 3 | VaultService.DeleteFolder: no path validation | Line 500 | `virtualPath` used without `..` check |
| 4 | VaultService.CleanOrphans/CleanUndecryptable: no id validation | Lines 187, 204 | `id` directly used in path |

#### P1 — Security: Key Material Not Zeroed

| # | Issue | Location | Notes |
|---|-------|----------|-------|
| 5 | EncryptionService.EncryptBlob: aesKey/aesNonce not cleared | Lines 31-32 | Temporary AES key remains in memory |
| 6 | EncryptionService.DecryptBlob: aesKey/aesNonce not cleared | Lines 68-69 | Same |
| 7 | PasswordService.SetupAsync: credBytes not cleared | Line 43 | Password bytes in memory |
| 8 | PasswordService.ValidateAsync: stdCredBytes not cleared | Line 94 | Password bytes in memory |
| 9 | VaultService._priv not cleared on Lock | Field `_priv` | RSA private key persists after lock |

#### P2 — Code Quality: Empty Catch Blocks

| # | Location | Context | Fix |
|---|----------|---------|-----|
| 10 | MainWindow.xaml.cs:55 | SetIcon | Add LogService.Debug |
| 11 | MainWindow.xaml.cs:416 | InitAuthFlow fallback | Add LogService.Warn |
| 12 | MainWindow.xaml.cs:758 | TitleBar color | Add LogService.Debug |
| 13 | MainWindow.xaml.cs:1188 | PreviewWindow close | Add LogService.Debug |
| 14 | VaultFileListView.xaml.cs:95 | Preview load | Add LogService.Debug |
| 15 | VaultFileListView.xaml.cs:469 | Drag-out decrypt | Add LogService.Warn + user notification |
| 16 | VaultFileListView.xaml.cs:498-503 | Temp cleanup | Add LogService.Debug |
| 17 | TitleBarControl.xaml.cs:80 | LoadHistory | Add LogService.Debug |
| 18 | TitleBarControl.xaml.cs:93 | SaveHistory | Add LogService.Debug |
| 19 | VaultService.cs:340 | Old chunk dir delete | Add LogService.Debug |
| 20 | VaultService.cs:452 | Preview delete | Add LogService.Debug |
| 21 | ImageProtection.cs:40 | Image delete | Add LogService.Debug |
| 22 | LocalizationService.cs:89,104 | INI parse | Add LogService.Debug |
| 23 | VaultService.cs:183 | CanDecrypt | Add LogService.Debug |
| 24 | VersionHistoryService.cs:169 | GetVersionRecord | Add LogService.Debug |

#### P3 — Compile Warning

| # | Issue | Location | Fix |
|---|-------|----------|-----|
| 25 | CS0067 CanExecuteChanged never raised | MainWindow.xaml.cs:395 | Add `#pragma warning disable` |

#### P4 — Localization: Hardcoded AutomationProperties

| # | File | Count | Fix |
|---|------|-------|-----|
| 26 | MainWindow.xaml | ~9 | Add Loc keys to INI, use x:Uid or code-behind |
| 27 | TitleBarControl.xaml | ~6 | Same |
| 28 | VaultFileListView.xaml | ~2 | Same |
| 29 | FavoritesPanel.xaml | ~1 | Same |
| 30 | FilePreviewWindow.xaml | ~4 | Same |
| 31 | ImagePreviewControl.xaml | ~6 | Same |
| 32 | VersionHistoryDialog.xaml | ~1 | Same |

#### P5 — Tray Icon (Already Fixed)

| # | Issue | Status | Notes |
|---|-------|--------|-------|
| 33 | Tray icon restore | ✅ Done | LeftClickCommand implemented, RightClick menu with Open/Lock/Exit |
| 34 | XAML WMC9999 | ⬜ Skip | Pre-existing, non-blocking, likely VS tooling issue |

#### P6 — Resource Management

| # | Issue | Location | Fix |
|---|-------|----------|-----|
| 35 | MemoryStream not disposed | MainWindow:1204, VaultFileListView:88, FilePreviewWindow:166,208 | Add `using` |

### Execution Order

1. P0: Path traversal validation (security critical)
2. P1: Key material zeroing (security)
3. P2: Empty catch blocks → add logging (debuggability)
4. P3: CS0067 warning (code cleanliness)
5. P4: Hardcoded strings → localization (i18n completeness)
6. P5: Update task-queue issue status
7. P6: Resource management (cleanup)

---

## Later: v0.1.3+ Feature Enhancement

See `long-term-roadmap.md` for version plan and `UserThoughts.md` for design decisions.

---

*This document tracks task status. For architecture and implementation details, see `DEVELOP.md`. For design decisions, see `UserThoughts.md`.*
