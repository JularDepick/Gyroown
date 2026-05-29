# Development Constraints & Localization Architecture

> Sections 8-9 from original UserThoughts.md

---

## 8. Development Constraints

- **Version number**: defined in `AppInfo.cs` constant class, lang files synced
- **Encoding**: all `.cs` / `.xaml` / `.ini` files are **UTF-8 BOM**
- **Code language**: **Chinese forbidden in code files** (comments, variables, XML docs, string literals all in English), translation **only** in `lang/*.ini`. Avoids cross-charset garbled text
- **Supported languages**: zh-CN (default), zh-TW, en-US, en-GB, ja-JP, ko-KR, fr-FR, defined in `AppInfo.SupportedLanguages`
- **Git policy**: do not proactively use git unless user explicitly requests; authorization is per-request
- **Documentation sync**: design decisions, requirements, details from user statements must be **maintained in real time in UserThoughts.md**
- **README bilingual**: all README.md default to English, each must have a companion README_zh-CN.md (Simplified Chinese)
- **Tool directories**: `tools/` and `scripts/` are at repository root, independent of `Gyroown/` project folder, not part of main solution build. `tools/` stores binary executable tool source (needs compilation), `scripts/` stores script-type tools (run source directly)
- **Tool single-file output**: tools under `tools/` must configure `PublishSingleFile=true`, producing a single executable on publish
- **In-app tools**: tools that the Gyroown application itself needs to call go inside `Gyroown/` project folder, not in `tools/` or `scripts/`
- **Development logs**: logs generated during development (e.g. build.log) go into `docs/DevLog/`, not committed to git

---

## 9. Localization Architecture

### 9.1 Embedded Translation Resources

- zh-CN and en-US translations are hardcoded as DLL embedded resources (`EmbeddedResource`)
- Embedded resources do not depend on external files, safe for `PublishTrimming`
- Original `lang/*.ini` files are retained for compatibility, can serve as external overrides

### 9.2 Loading Priority

- Primary language loading: `lang/` .ini file > DLL embedded resource > field name fallback (`[Section.Key]`)
- Fallback language (when primary is not zh-CN): zh-CN .ini > zh-CN embedded > en-US embedded
- .ini files take priority over embedded resources (facilitates community hot-update translations)

### 9.3 Translation File Metadata

- All .ini files contain `[__meta__]` section: `LangCode` (language code), `AppVersion` (applicable version)
- Loader validates whether `AppVersion` matches `AppInfo.Version`; warns on mismatch

### 9.4 Conversion Tool

- `tools/transIniToDll/` provides .ini file to embedded resource conversion
- Community translation contributions only need to edit .ini files; the tool handles the embedding process
- `validate` command checks translation coverage
- `embed` command copies .ini to `Resources/Loc/` and prints .csproj configuration snippet
