# fix_vault

One-time code patching scripts for `VaultService.cs` — replaced `ImportItemAsync` and `ExportItemAsync` with streaming versions that use constant memory (~1MB buffer) instead of loading entire files into RAM.

## Files

| File | Language | Description |
|------|----------|-------------|
| `fix_vault.ps1` | PowerShell | Orchestrator — calls `_fix_vault.ps1` for the regex cleanup |
| `_fix_vault.ps1` | PowerShell | Regex-based removal of duplicate method stubs |
| `_fix_vault.mjs` | Node.js | Full replacement script (alternative to the PS1 approach) |

## Purpose

These scripts were used during development to migrate `VaultService.cs` from in-memory file processing to streaming chunked storage. The changes have already been applied to the codebase — these scripts are kept as historical reference.

## What Changed

- `ImportItemAsync`: Streams source to temp file with 1MB buffer, computes SHA256 inline, then encrypts from disk
- `ExportItemAsync`: Reads encrypted chunks from disk via `FileStream` with `SequentialScan` hint
- Added `StoreChunkedStream`: Streaming chunked storage that reads from temp file, encrypts each chunk independently
