param(
    [switch]$Generate,
    [decimal]$MaxEtb = 1,
    [string]$Voice = 'ti-berhane'
)
$ErrorActionPreference = 'Stop'
$previousKey = $env:ADDIS_API_KEY
$secureKey = $null
try {
    if ([string]::IsNullOrWhiteSpace($env:ADDIS_API_KEY)) {
        $secureKey = Read-Host 'Klistra in Addis AI-nyckeln (visas inte och sparas inte)' -AsSecureString
        $credential = [System.Net.NetworkCredential]::new('', $secureKey)
        $env:ADDIS_API_KEY = $credential.Password.Trim()
        $credential = $null
    }
    if ($env:ADDIS_API_KEY.Length -lt 20 -or $env:ADDIS_API_KEY -match '[\s\p{Cc}]') {
        Write-Host 'Hela API-nyckeln verkar inte ha kommit med. Prova Shift+Insert eller högerklick för att klistra in. Inget API-anrop har gjorts.' -ForegroundColor Yellow
        return
    }
    $mode = '--estimate'
    if ($Generate) { $mode = '--generate' }
    $limit = $MaxEtb.ToString([Globalization.CultureInfo]::InvariantCulture)
    & dotnet run --project (Join-Path $PSScriptRoot 'AiAudioTrial.csproj') -- $mode "--voice=$Voice" "--max-etb=$limit"
    if ($LASTEXITCODE -ne 0) { Write-Host 'Testet stoppades. Läs den konkreta felorsaken ovan; nyckeln ska inte skickas till chatten.' -ForegroundColor Yellow }
}
finally {
    $env:ADDIS_API_KEY = $previousKey
    if ($null -ne $secureKey) { $secureKey.Dispose() }
}
