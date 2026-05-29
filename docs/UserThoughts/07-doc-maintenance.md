# Documentation Maintenance

> Section 10 from original UserThoughts.md

---

The following files must be **synced in real time** with project changes, no lag allowed:

| File | Content | Trigger |
|------|---------|---------|
| `AppInfo.cs` | Version number, app name constants | Version change |
| `README.md` | English project description | Feature/architecture/status change |
| `README_zh-CN.md` | Simplified Chinese project description | Same as above |
| `docs/DEVELOP.md` | Architecture diagram, interface signatures, implementation status, naming conventions | Interface/model/storage architecture change |
| `docs/UserThoughts/` (this directory) | Design decision records | Any new decision or requirement, **real-time follow-up on user statements** |
| `docs/api/key-insurance.md` | Key insurance API specification | API endpoint/parameter change |
| `lang/zh-CN.ini` | Simplified Chinese translation | New/modified UI text |
| `lang/en-US.ini` | English translation | Same as above |
| `.gitignore` | Ignore rules | New file types to exclude |

## Maintenance Rules

1. **维护不得丢失用户表述的细节** — Maintenance must not lose details expressed by the user
2. Each dimension file (`01-*.md` through `07-*.md`) is the authoritative source for its topic
3. When adding new content, place it in the appropriate dimension file; if no fit exists, create a new file and update `README.ai.md`
4. Date-stamp significant additions with `> YYYY-MM-DD` blockquotes
5. The original `docs/UserThoughts.md` is deprecated; all content lives here now
