# Key Insurance API 规范

> 版本: 2.0 | 基础 URL: `https://api.gyroown.example/v1`
> 全部使用 HTTPS。请求/响应格式: JSON。

---

## 流程

```
客户端                            服务端
  │                               │
  ├─ POST /request-code ─────────►│  向邮箱发送验证码
  │◄──── { success, message } ────┤
  │                               │
  │  [用户输入验证码]              │
  │                               │
  ├─ POST /verify-code ──────────►│  验证码校验，返回身份令牌
  │◄──── { success, token } ──────┤
  │                               │
  ├─ POST /upload (后台) ─────────►│  上传加密的保险密钥
  │◄──── { success } ─────────────┤
```

用户可在任意步骤取消。错误不会自动退出流程。

---

## 1. 请求验证码

```
POST /insurance/request-code
```

### 请求
```json
{ "email": "user@example.com" }
```

### 响应 (200)
```json
{ "success": true, "message": "验证码已发送" }
```

### 错误
| 400 | 邮箱无效 | 429 | 请求过于频繁 |

---

## 2. 验证验证码

```
POST /insurance/verify-code
```

### 请求
```json
{ "email": "user@example.com", "code": "123456" }
```

### 响应 (200)
```json
{ "success": true, "message": "验证通过", "data": { "token": "eyJhbG..." } }
```

### 错误
| 400 | 验证码错误 | 410 | 验证码已过期 |

---

## 3. 上传（后台执行）

```
POST /insurance/upload
```

### 请求
```json
{
  "email": "user@example.com",
  "token": "eyJhbG...",
  "insurance_private_key": "<base64>"
}
```

### 响应 (200)
```json
{ "success": true }
```

后台上传，不阻塞用户操作。服务端将加密的私钥与已验证的邮箱关联存储。
