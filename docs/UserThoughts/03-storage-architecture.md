# Storage Architecture

> Section 4 from original UserThoughts.md

---

## 4.1 Path Convention

> **`~` means the user profile directory** (`%USERPROFILE%`, i.e. `C:\Users\<username>`). All `~` in this document refers to this directory.

## 4.2 Directory Structure

```
%USERPROFILE%\.Gyroown\
├── auth\                         (Hidden attribute)
│   ├── .gyropw                   # Password hash (pw=password, suffix only)
│   ├── .gyrock                   # Internal key pair ciphertext (ck=corekey, suffix only)
│   ├── .imgkey                   # Picture password image encryption key (32B random)
│   ├── image.pwimg               # Picture password user-selected image (XOR encrypted)
│   └── insurance.gyrock          # Key insurance backup
├── data\
│   ├── {hashID}.gyrodt           # Small files (dt=data)
│   └── {hashID}\                  # Large file chunk directory
│       ├── c0000.gyrodt           # 4-digit hex numbering, zero-padded
│       └── c0001.gyrodt
├── meta\
│   ├── {hashID}.gyromt           # Metadata (mt=meta)
│   └── .tree.gyrojson            # Encrypted folder tree
├── preview\
│   └── {hashID}.gyropv           # Encrypted thumbnail (pv=preview)
├── versions\                      # Version history
│   └── {hashID}\
│       ├── v1.gyroverdata
│       ├── v1.gyrovermeta
│       └── ...
├── log\
│   ├── error\                     # Error logs (200KB slicing)
│   │   └── error-{ymd}-{ymd}.txt
│   ├── crash\                     # Crash logs
│   │   └── crash-{ymd}-{ymd}.txt
│   └── run\                       # Run logs
│       └── run-{ymd}-{ymd}.txt
├── favorites.gyrojson             # Encrypted favorites
├── search-history.gyrojson        # Encrypted search history
├── settings.gyrojson              # Encrypted user settings (theme/language/accent color)
└── config.gyrojson                # Encrypted core config (chunk tier/max versions/auto-lock timeout)
```

## 4.3 Encrypted Data Files

- **One original file corresponds to one encrypted data file**
- Suffix `.gyrodt` (dt=data), encrypted using internal fixed key pair
- Format: `[4B hdrLen][RSA-OAEP header{ aesKey, aesNonce, len }][4B bodyLen][AES-GCM body]`
- Chunked storage: when > threshold, split into `data/{hashID}/c{xxxx}.gyrodt`, 4-digit hex lowercase numbering

## 4.4 Metadata Files

- **One metadata file corresponds to one encrypted data file** (1:1 mapping, same hashID)
- Suffix `.gyromt` (mt=meta), encrypted using internal fixed key pair
- Contains: file name, original size, MIME type, virtual path, creation/modification time, ChunkCount/ChunkSize, PreviewId
- File list is built by scanning `*.gyromt` — no index file needed
- **`meta/` and `data/` are strongly bound**: both must exist or both must not exist

## 4.5 hashID

- Algorithm: `SHA256(original content)` → hex → take first 32 characters → **all lowercase**
- `ListItems` scans `*.gyromt` wildcard, does not assume ID length — forward/backward version compatible
- Chunk numbering `c{i:x4}` also lowercase

## 4.6 Configuration Separation

| File | Format | Content | Edit Method |
|------|--------|---------|-------------|
| `settings.gyrojson` | Encrypted JSON (vault key) | User preferences: theme/language/accent color | App UI |
| `config.gyrojson` | Encrypted JSON (vault key) | Program maintenance: chunk tier/max versions/auto-lock timeout | **App UI only** |
| `favorites.gyrojson` | Encrypted JSON (vault key) | Favorites data | App UI |
| `search-history.gyrojson` | Encrypted JSON (vault key) | Search history (max 10 entries) | App UI |

- `.gyrojson` = encrypted JSON file, encryption/decryption method unified with data files globally
- All `.gyrojson` files encrypted with vault key; decryption requires re-unlock to read
- Config changes written to disk asynchronously (`_ = SaveAsync()`), non-blocking for UI
- Chunk tiers 1-6: 2/4/8/16/32/64 MB, default 5 (32MB). Switching to tier 6 shows hardware performance risk warning. Only affects subsequent imports.

## 4.7 Logging System

- Subdirectory classification: `log/error/` (errors), `log/crash/` (crashes), `log/run/` (runtime)
- File format: `{prefix}-{start:yyyy-MM-dd}-{end:yyyy-MM-dd}.txt`
- Auto-slicing: **200KB** threshold, maintaining single log message integrity at boundaries
- Default level: Info (adjustable via `LogService.MinLevel`)
- Crash logs: `LogService.Crash(context, exception)` records full stack trace

## 4.8 Auth Permission Protection

- auth directory and files set Windows **Hidden attribute**, not visible in File Explorer by default
- Files have suffix-only names (`.gyropw` / `.gyrock`), no primary name
- `VaultService.ProtectAuthDir()` is automatically called after password creation/password change
