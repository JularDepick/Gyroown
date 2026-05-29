# Password System

> Section 3 from original UserThoughts.md

---

## 3.1 Four Password Types

| Type | Identifier | Min Length | Description |
|------|-----------|------------|-------------|
| 6-digit PIN | `pin` | 6 digits | 6 single-character PasswordBoxes, auto-advance, Backspace to go back |
| Grid gesture | `gesture` | 4 points | 3x3 Canvas + Polyline lines, no revisiting |
| Custom password | `custom` | 6-32 chars | `[a-zA-Z0-9]` + visible punctuation, auto-verify on reaching max length (no Enter needed) |
| Picture password | `picture` | 3 points | FileOpenPicker to select image → tap coordinates in order → Euclidean distance verification |

## 3.2 Picture Password Details

- Image displayed in **Uniform mode** (maintain aspect ratio, centered)
- Coordinates recorded as `(x_ratio, y_ratio)` relative to image width/height **ratio**
- Each tapped point shows a **numbered circular marker** (blue circle + white number)
- Radius threshold: **5% of image short edge** (developer-adjustable parameter)
- Verification: tap in order, correct → green numbered circle advances; incorrect → all flash red for 500ms → reset
- Must first set a **backup password** (one of A/B/C) for failure recovery

## 3.3 Picture Password Persistence

> 2026-05-28

- User-selected image is **encrypted and stored** at `auth/image.pwimg`; auto-loaded and displayed on unlock
- Encryption: XOR + random 32-byte key (`auth/.imgkey`) + 16-byte random salt
- Key is **independent of vault key and user password** (because image must be displayed before authentication)
- Not security-sensitive data (visual aid only), lightweight encryption is sufficient, avoids plaintext storage
- On password change: if switching away from picture type → delete `image.pwimg`; if switching to picture type → trigger re-selection
- If image file is missing, show placeholder hint; tapping blank area still works (coordinate verification unaffected)

## 3.4 Password Setup

- **Two-input verification**: first input → second confirmation → compare for match before applying
- Can go back to previous step to modify
- Enter key triggers "next"/"set", Esc goes back

## 3.5 Password Storage

- PBKDF2-SHA256, **100,000 iterations**
- 32-byte random salt, 32-byte hash
- Constant-time comparison (`CryptographicOperations.FixedTimeEquals`)
- Storage format: JSON `{ type, salt, hash, iterations }` → `auth\.gyropw`

## 3.6 Lockout Policy

- **5** consecutive errors → lock for **30 seconds**, countdown displayed

## 3.7 Unlock Screen Interaction

- Detecting keyboard input of **visible characters** auto-focuses the password input field (`CharacterReceived` event)
- Focus calls `IPasswordControl.FocusInput()`, implemented by each password control

## 3.8 Default Password Type

> 2026-05-28

- Password setup and password change both **default to PIN type** (not custom)
- Radio button `OptPin` has `IsChecked="True"`
- Code-behind `_type` / `_newPwType` initialized to `"pin"`
