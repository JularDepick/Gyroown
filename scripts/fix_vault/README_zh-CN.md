# fix_vault

用于 `VaultService.cs` 的一次性代码补丁脚本 — 将 `ImportItemAsync` 和 `ExportItemAsync` 替换为流式版本，使用常量内存（~1MB 缓冲区）而非将整个文件加载到 RAM。

## 文件

| 文件 | 语言 | 说明 |
|------|------|------|
| `fix_vault.ps1` | PowerShell | 编排脚本 — 调用 `_fix_vault.ps1` 进行正则清理 |
| `_fix_vault.ps1` | PowerShell | 基于正则的重复方法存根移除 |
| `_fix_vault.mjs` | Node.js | 完整替换脚本（PS1 方案的替代方案） |

## 用途

这些脚本在开发期间用于将 `VaultService.cs` 从内存中处理文件迁移为流式分片存储。变更已应用到代码库中 — 这些脚本作为历史参考保留。

## 变更内容

- `ImportItemAsync`：以 1MB 缓冲区将源数据流式写入临时文件，内联计算 SHA256，然后从磁盘加密
- `ExportItemAsync`：通过 `FileStream` 使用 `SequentialScan` 提示从磁盘读取加密分片
- 新增 `StoreChunkedStream`：流式分片存储，从临时文件读取，独立加密每个分片
