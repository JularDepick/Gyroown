# transIniToDll

Convert `.ini` translation files to DLL embedded resources for Gyroown.

This is a **community extension tool** — the core project builds zh-CN and en-US as embedded resources automatically via `<EmbeddedResource>` in the `.csproj`. This tool is for contributors who want to add additional languages as embedded resources.

## Usage

```bash
dotnet run --project tools/transIniToDll -- <command> [args]
```

### Commands

| Command | Description |
|---------|-------------|
| `embed <lang-code> [path]` | Copy `.ini` to `Resources/Loc/` for embedding into the DLL |
| `validate <path>` | Validate a `.ini` file against the zh-CN baseline (coverage check) |
| `report` | Show which languages are embedded vs .ini-only |

### Examples

```bash
# Embed Japanese as a DLL resource
dotnet run --project tools/transIniToDll -- embed ja-JP lang/ja-JP.ini

# Validate French translation coverage
dotnet run --project tools/transIniToDll -- validate lang/fr-FR.ini

# Show all language statuses
dotnet run --project tools/transIniToDll -- report
```

### Community Workflow

1. Fork the repository
2. Copy `lang/en-US.ini` to `lang/<new-lang>.ini`
3. Translate all values (keep `[__meta__]` header with correct `LangCode` and `AppVersion`)
4. Run `validate` to check coverage
5. Submit a PR with the new `.ini` file
6. Maintainer runs `embed <lang-code>` to add it as an embedded resource if desired

## Notes

- The `embed` command copies the `.ini` to `Resources/Loc/` and prints the `<EmbeddedResource>` line to add to `.csproj`
- After embedding, rebuild the main project: `dotnet build Gyroown/Gyroown.csproj`
- All `.ini` files must include a `[__meta__]` section with `LangCode` and `AppVersion`
