# System Constraints & Security Principles

> Sections 5-6 from original UserThoughts.md

---

## 5. System Constraints

- **No registry touches**: does not use `Microsoft.Win32.Registry`
- **No environment variables**: only `GetFolderPath` to read system paths
- **Self-contained data**: all persistent data is in `%USERPROFILE%\.Gyroown\`; deleting it completely cleans up
- **Fully offline**: no network by default; key insurance is an optional online feature

---

## 6. Security Principles

- **Program never proactively writes plaintext to disk**: the application itself does not write decrypted content to temp files/cache directories
- **User actions are unrestricted**: drag in/out, import/export, move in/out are all user-initiated actions; the application does not "safeguard" security on behalf of the user
- Decrypted content vanishes when process dies
- Temp file secure erasure: random data overwrite → Flush → delete
- **High-risk operation lock**: delete/move-out/move-in/change-password/lock use `_busy` mutex; closing window is forbidden during these operations
- Minimize is unaffected; closing window is not allowed during encryption/decryption
