# Key Insurance API Specification

> Version: 2.0 | Base URL: `https://api.gyroown.example/v1`  
> All HTTPS. Request/response: JSON.

---

## Flow

```
Client                          Server
  │                               │
  ├─ POST /request-code ─────────►│  Send verification code to email
  │◄──── { success, message } ────┤
  │                               │
  │  [User enters code]           │
  │                               │
  ├─ POST /verify-code ──────────►│  Validate code, return identity token
  │◄──── { success, token } ──────┤
  │                               │
  ├─ POST /upload (bg) ──────────►│  Upload encrypted insurance key
  │◄──── { success } ─────────────┤
```

User can cancel at any step. Errors do NOT automatically exit the flow.

---

## 1. Request Code

```
POST /insurance/request-code
```

### Request
```json
{ "email": "user@example.com" }
```

### Response (200)
```json
{ "success": true, "message": "Verification code sent" }
```

### Errors
| 400 | Invalid email | 429 | Too many requests |

---

## 2. Verify Code

```
POST /insurance/verify-code
```

### Request
```json
{ "email": "user@example.com", "code": "123456" }
```

### Response (200)
```json
{ "success": true, "message": "Verified", "data": { "token": "eyJhbG..." } }
```

### Errors
| 400 | Invalid code | 410 | Code expired |

---

## 3. Upload (background)

```
POST /insurance/upload
```

### Request
```json
{
  "email": "user@example.com",
  "token": "eyJhbG...",
  "insurance_private_key": "<base64>"
}
```

### Response (200)
```json
{ "success": true }
```

Fire-and-forget. Server stores the encrypted private key linked to the verified email.
