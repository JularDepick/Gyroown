# transIniToDll

将 `.ini` 翻译文件转换为 Gyroown 的 DLL 嵌入式资源。

这是一个**社区扩展工具** — 核心项目通过 `.csproj` 中的 `<EmbeddedResource>` 自动构建 zh-CN 和 en-US 为嵌入式资源。此工具供希望添加其他语言为嵌入式资源的贡献者使用。

## 使用方法

```bash
dotnet run --project tools/transIniToDll -- <command> [args]
```

### 命令

| 命令 | 说明 |
|------|------|
| `embed <lang-code> [path]` | 复制 `.ini` 到 `Resources/Loc/`，验证并打印 .csproj 配置片段 |
| `validate <path]` | 对照 zh-CN 基准检查 `.ini` 文件覆盖率 |
| `report` | 显示哪些语言已嵌入，哪些仅有 .ini 文件 |

### 示例

```bash
# 将日语嵌入为 DLL 资源
dotnet run --project tools/transIniToDll -- embed ja-JP lang/ja-JP.ini

# 验证法语翻译覆盖率
dotnet run --project tools/transIniToDll -- validate lang/fr-FR.ini

# 显示所有语言状态
dotnet run --project tools/transIniToDll -- report
```

### 社区工作流

1. Fork 仓库
2. 复制 `lang/en-US.ini` 到 `lang/<新语言>.ini`
3. 翻译所有值（保留 `[__meta__]` 头部，填写正确的 `LangCode` 和 `AppVersion`）
4. 运行 `validate` 检查覆盖率
5. 提交 PR（附带新的 `.ini` 文件）
6. 维护者运行 `embed <lang-code>` 将其添加为嵌入式资源（如需要）

## 说明

- `embed` 命令将 `.ini` 复制到 `Resources/Loc/` 并打印需添加到 `.csproj` 的 `<EmbeddedResource>` 配置行
- 嵌入后需重新构建主项目：`dotnet build Gyroown/Gyroown.csproj`
- 所有 `.ini` 文件必须包含 `[__meta__]` 段，其中声明 `LangCode` 和 `AppVersion`
