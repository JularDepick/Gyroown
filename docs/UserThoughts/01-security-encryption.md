# Security & Encryption

> Sections 1-2 from original UserThoughts.md

---

## 1. Security & Encryption

### 1.1 Internal Fixed Key Pair

- Asymmetric key pair (RSA 2048): **private key encrypts writes / public key decrypts reads**
- **Neither is public** — both are kept internal, never exposed
- `GenerateVaultKeyPair()` is called **only once at first setup**; the raw plaintext **never changes, never regenerates**
- The internal key pair plaintext is the **sole credential for file encryption/decryption**
- .NET `RSA.Decrypt()` requires the private key parameter, so `DecryptBlob` internally uses `ImportRSAPrivateKey` (private key contains public key components)

### 1.2 Hybrid Encryption Scheme

- **RSA 2048-bit** asymmetric key pair (PKCS#1 format)
- **AES-256-GCM** file body encryption (random key/nonce, 12B nonce, 16B tag)
- RSA-OAEP-SHA256 encrypts AES key and metadata
- Blob format: `[4B hdrLen][RSA-OAEP header][4B bodyLen][AES-GCM body]`

### 1.3 Unified Encryption Methods

- `EncryptBlob(data, privateKey)` — used for both data files and meta files
- `DecryptBlob(blob, privateKey)` — .NET requires private key parameter; private key contains public key components

### 1.4 Chunked Encryption

- Large files are split by the tier defined in `config.gyrojson`; each chunk is independently `EncryptBlob`-ed
- Export: sequential concatenation + decryption
- Deletion: cleans the entire chunk subdirectory

---

## 2. Key Management

### 2.1 Key Protection

- User password → `DeriveUserKey(password, salt)` → userKey
- userKey encrypts/protects the key pair ciphertext: `auth\.gyrock` (ck=corekey)
- Password change: only re-encrypts `.gyrock`; **key pair plaintext unchanged, files untouched**

### 2.2 Password Change Flow

> 2026-05-26

- Requires both **old password** and **new password**
- Old password → decrypt `.gyrock` → restore key pair
- New password → encrypt key pair → write back `.gyrock`
- Old password is discarded **only after confirming new password works**
- `PasswordService.ChangePasswordAsync` returns `(oldUserKey, newUserKey)` for caller to re-encrypt

### 2.3 Uninstall Strategy

- On uninstall, `auth\` and `data\` are deleted **only if the user explicitly chooses "delete data and keys"**

### 2.4 Key Insurance (Key Recovery)

> 2026-05-26 design, client implemented, cloud API is a stub

#### Motivation

User forgets password → normal path cannot decrypt `.gyrock` → all encrypted files permanently inaccessible. Provides a recovery channel assisted by the application service provider.

#### Naming

- **Core Key** — the internal fixed key pair specifically for encrypting/decrypting file data (vault key pair)
- **Insurance Key Pair** — per-user provider-level key pair, used to wrap the core key recovery backup
- **Key Insurance** — user-facing feature name, corresponding file `auth/insurance.gyrock`

#### Flow

```
After password setup completes, a popup asks (not a prerequisite):
  └→ Popup: "Enable key insurance?" (skippable)
       ├─ Enter email → POST /request-code
       ├─ Enter verification code → POST /verify-code → receive token
       └─ Background async POST /upload(token, email, insPriv)
           User can cancel at any time; errors in any step do not auto-exit
           After upload completes, green bottom banner shows for 3s then disappears
```

#### Security

| Threat | Mitigation |
|--------|------------|
| Provider private key leak | Per-user insurance key pair, private key stored in HSM |
| Identity impersonation | Email + SMS dual verification, strong binding |
| Provider peeking at core key | Server briefly holds it during recovery — UI explicitly informs user of this trade-off |
| Online dependency | Disabled by default, user must actively enable. Fully offline when disabled |

#### API Endpoints (Stub)

| Endpoint | Description |
|----------|-------------|
| `POST /insurance/request-code` | Send verification code to email |
| `POST /insurance/verify-code` | Verification code → return identity token |
| `POST /insurance/upload` | Background async upload (fire-and-forget) |

### 2.5 auth/ Missing Recovery Flow

> 2026-05-27

On startup, check by priority:

1. **auth directory does not exist** → first-use scenario: auto-create auth directory, enter password setup flow (non-fatal)
2. **`.gyrock` (core key) missing** →
   - If key insurance is enabled → remind user to contact service provider to initiate cloud recovery
   - If key insurance is not enabled → fatal warning (data unrecoverable), prompt to delete `~/.Gyroown/` and start over
3. **`.gyropw` (password file) missing** → enter new password setup flow (key pair still exists, just re-encrypt)

> 2026-05-28 addition: first startup (auth directory does not exist) no longer crashes; instead creates auth directory and enters normal setup flow. auth directory creation is deferred to `Loaded` event, wrapped in try/catch.
