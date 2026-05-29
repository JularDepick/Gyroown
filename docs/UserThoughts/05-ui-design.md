# UI Design

> Section 7 from original UserThoughts.md

---

## 7.1 Window Strategy

- **Main window + file viewer window**: main window hosts all management functions; media viewing uses an independent window (only one at a time)
- Main window layout similar to Windows File Explorer
- Closing main window → `AppWindow.Closing` intercepts, **hides to tray** (does not exit process)
- Tray icon uses `H.NotifyIcon.WinUI`, loads `favicon.ico` first (stream read), falls back to exe embedded icon
- **Multiple main windows not allowed** (`Process.GetProcessesByName` detection)
- Default size 1600x960, minimum 800x480

### Tray Icon Behavior

> 2026-05-28

- **Left-click**: opens/restores main window (ensures uniqueness via `Process.GetProcessesByName`)
- **Right-click**: shows context menu at cursor position with "Open" and "Exit" options
- Uses `H.NotifyIcon.WinUI` 2.3.0 API: `LeftClickCommand`, `RightClickCommand`
- `TrayCommand` implements `System.Windows.Input.ICommand`
- Menu positioned via Win32 `GetCursorPos`
- `RestoreFromTray()`: `ShowWindow(Hwnd, SW_RESTORE)` + `SetForegroundWindow(Hwnd)` + `Activate()`

## 7.2 Interface Language

- **Default: Simplified Chinese**
- Switchable via **INI language packs**
- **Code files (.cs/.xaml) must not contain Chinese**: comments, variables, XML docs all in English. Translation text exists **only** in `lang/*.ini`
- **All source files encoded as UTF-8 BOM**
- Display/UI text via `Loc.Get()` with translation pre-wired, default loads Simplified Chinese translation file
- Translation files must follow UI changes in real time

## 7.3 Theme

- Theme modes: Follow system / Light / Dark (dropdown options displayed in local language)
- Default: **Follow system**
- Priority: **App setting > OS setting**
- `FrameworkElement.RequestedTheme` only affects XAML controls, not window frame
- Must explicitly set title bar colors via `AppWindow.TitleBar` API (background/foreground/hover/inactive), ensuring dark mode works fully even when OS is light
- Root Grid uses `ApplicationPageBackgroundThemeBrush` to ensure window body background follows theme
- **8 preset accent colors**: Blue, Teal, Green, Orange, Purple, Pink, Red, Graphite
- Theme and accent color are independent selections, persisted to `settings.gyrojson`

## 7.4 Preview vs View

- **Preview**: when file list is in icon mode (Icons/Tiles), thumbnails shown on file icons. Image files show thumbnails, video files show first frame. This is metadata-level display, does not decrypt original file. Preview images use **UniformToFill mode** (maintain aspect ratio, crop overflow), ensuring image pixels fill the icon control area without distortion.
- **View**: double-click or right-click to open file, decrypts and loads original media file into independent viewer window (window title is "View" not "Preview"). Supports images (zoom/pan), video (playback), audio (playback), text (syntax highlighting/encoding switch). On open, **focus goes to viewer window**, not back to main window.

## 7.5 File Manager Features

- Multiple views: Details (column headers draggable to adjust width) / Icons (loads preview thumbnails) / Tiles
- Virtual directory tree sidebar (click folder to filter file list), **sidebar right border draggable to adjust width** (160-400px, default 220px)
- Right-click context menu: Open / Export / Rename / Version History / Delete / Favorite (single select); Batch Export / Batch Delete (multi-select)
- **Favorites only in right-click menu**, no star button in list
- **All right-click menu items must have icons** (FontIcon Glyph); icon-less items are unacceptable
- Keyboard shortcuts: Delete to delete, F2 to rename, Ctrl+C/V/X for internal copy/paste/cut
- **Click blank area to deselect** (does not trigger file open)
- Column header sorting: Name/Size/Type/Date ascending/descending
- Search filter: title bar search box **right-aligned**
- File size smart units: B/KB/MB/GB, >=KB precise to 2 decimal places
- Import/export/move-in/move-out all support batch + **byte-level progress bar** (based on processed data/file size, 500ms update interval)
- Window top-left icon correctly displays app logo (`AppWindow.SetIcon`)

## 7.6 Notification System

- **Fatal warnings**: ContentDialog popup
- **Non-fatal**: bottom red/green banner (closable, clickable to view log)
- Green banner auto-dismisses after 3s

## 7.7 Folder Tree

- Encrypted storage: `meta/.tree.gyrojson` (encrypted blob, encrypted/decrypted using internal key pair)
- Create folder → generate meta entry (`IsFolder=true`) + update `.tree.gyrojson`
- Delete folder → delete all child meta/data + remove from tree
- Sidebar folder selection → sets `FilterPath` to filter file list
- Root node displays localized name ("保险柜" / "Vault")

## 7.8 Internal Clipboard (Ctrl+C/V/X)

> 2026-05-28 design, pending implementation

- App-internal use of Ctrl+C / Ctrl+V / Ctrl+X for file **copy/paste/cut**
- **Does not write to system clipboard**, does not affect operations outside the app
- **Does not change encrypted file content**, only modifies tree structure (path mapping in folder tree)
- Copy = create new entry in target folder pointing to same hashID
- Cut = remove entry from source folder, create entry in target folder
- Paste to same level = rename adding "(1)" suffix
- Stored in memory, lost on process exit

## 7.9 Settings Panel

- **Full-screen overlay page**, not a sidebar. Covers entire window, z-index above all bottom banners (error/success/progress)
- Opaque background (`SolidBackgroundFillColorBaseBrush`)
- Top title bar: back arrow + title, separator below
- Content area max width 640px centered, scrollable
- Theme dropdown options localized: Follow system / Light / Dark

### Settings Button Position

> 2026-05-28

- Settings button is in the title bar, **below the "Gyroown" text, left-aligned**
- TitleBarControl restructured to two-row layout:
  - Row 0: app icon + "Gyroown" | search box | refresh/check/advanced buttons
  - Row 1: Settings button (`HorizontalAlignment="Left"`)

## 7.10 Error Log

> 2026-05-28

- Integrity check **no longer uses popups** (ContentDialog); instead collects issue list → bottom red warning banner
- Bottom warning banner click → opens settings page → auto-scrolls to "Error Log" section
- Error log displays by entry, each with: icon (colored by type), title, hashID summary
- Error types: orphaned metadata (orange), orphaned data (orange), undecryptable (red), data directory abnormal (red)
- Click entry → Flyout shows details + action buttons:
  - Orphaned metadata/data → "Clean now" / "Ignore for now"
  - Undecryptable → "Clean now" / "Keep"
  - Data directory abnormal → info display only, no action
- Supports "Clean all" to batch-process all issues
- After cleaning, auto-refresh file list and bottom banner status
