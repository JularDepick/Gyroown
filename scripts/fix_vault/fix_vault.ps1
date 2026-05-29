$content = Get-Content 'C:\Users\liwenfang\GitHub\JularDepick\Gyroown\Gyroown\Services\VaultService.cs' -Raw -Encoding UTF8
$pattern = '(?s)public async Task ExportItemAsync .*?return new VaultFileItem.*?\n    \}\n\n    public async Task ExportItemAsync\(string itemId, Stream outStream,'
$replacement = 'public async Task ExportItemAsync(string itemId, Stream outStream,'
$content = [regex]::Replace($content, $pattern, $replacement)
Set-Content 'C:\Users\liwenfang\GitHub\JularDepick\Gyroown\Gyroown\Services\VaultService.cs' $content -NoNewline -Encoding UTF8
