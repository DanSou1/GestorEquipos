param([ValidateRange(1, 100)][int]$Cycles = 20)
$ErrorActionPreference = 'Stop'
# Local-only probe. Credentials remain in memory and are never included in output.
$base = 'http://localhost:5249'
$config = Get-Content (Join-Path $PSScriptRoot '..\appsettings.json') -Raw | ConvertFrom-Json
$connectionSettings = New-Object System.Data.SqlClient.SqlConnectionStringBuilder($config.ConnectionStrings.DefaultConnection)
$connectionSettings['Connect Timeout'] = 5
$connectionSettings['Pooling'] = $false
$sqlTimes = @()
foreach ($attempt in 1..5) {
    $connection = New-Object System.Data.SqlClient.SqlConnection($connectionSettings.ConnectionString)
    $sqlTimer = [Diagnostics.Stopwatch]::StartNew()
    try {
        $connection.Open()
        $command = $connection.CreateCommand()
        $command.CommandTimeout = 5
        $command.CommandText = 'SELECT COUNT(*) FROM dbo.Desktop'
        $null = $command.ExecuteScalar()
        $sqlTimes += $sqlTimer.ElapsedMilliseconds
    } finally { $connection.Dispose() }
}
Write-Output "SqlOpenAndQueryMs=$($sqlTimes -join ',')"
$secretPath = Join-Path $env:APPDATA 'Microsoft\UserSecrets\82ee8068-3edf-4e80-82da-357ab76bfe9e\secrets.json'
$secrets = Get-Content -LiteralPath $secretPath -Raw | ConvertFrom-Json
$session = New-Object Microsoft.PowerShell.Commands.WebRequestSession
$timer = [Diagnostics.Stopwatch]::StartNew()
$login = Invoke-WebRequest "$base/Login/Index" -WebSession $session -UseBasicParsing -TimeoutSec 90
$token = [regex]::Match($login.Content, 'name="__RequestVerificationToken"[^>]*value="([^"]+)"').Groups[1].Value
if (-not $token) { throw 'Login antiforgery token missing.' }
$timer.Restart()
$response = Invoke-WebRequest "$base/Login/LoginUser" -Method Post -WebSession $session -UseBasicParsing -TimeoutSec 90 -Body @{
    Email = $secrets.'BootstrapAdmin:Email'
    Password = $secrets.'BootstrapAdmin:Password'
    __RequestVerificationToken = [Net.WebUtility]::HtmlDecode($token)
}
$loginMs = $timer.ElapsedMilliseconds
$secrets = $null
if ($response.BaseResponse.ResponseUri.AbsolutePath -match '/Login') { throw 'Configured account could not log in.' }
Write-Output "LoginAndDashboardMs=$loginMs"
$desktopPage = Invoke-WebRequest "$base/Desktop" -WebSession $session -UseBasicParsing -TimeoutSec 90
$detailLink = [regex]::Match($desktopPage.Content, 'href="(/Desktop/Details/\d+)"').Groups[1].Value
if (-not $detailLink) { throw 'No desktop available for detail/PDF verification.' }
$desktopId = ($detailLink -split '/')[-1]
$paths = @('/', '/Desktop', '/Peripheral', '/UserAdmin', '/Remote', '/Location', '/DesktopType', '/PeripheralType', '/AccessAccount', '/MaintenanceType', $detailLink)
$peripheralPage = Invoke-WebRequest "$base/Peripheral" -WebSession $session -UseBasicParsing -TimeoutSec 90
$peripheralLink = [regex]::Match($peripheralPage.Content, 'href="(/Peripheral/Details/\d+)"').Groups[1].Value
if ($peripheralLink) { $paths += $peripheralLink }
$results = @()
foreach ($cycle in 0..$Cycles) {
    foreach ($path in $paths) {
        $timer.Restart()
        $page = Invoke-WebRequest "$base$path" -WebSession $session -UseBasicParsing -TimeoutSec 90
        $elapsed = $timer.ElapsedMilliseconds
        if ($page.StatusCode -ne 200 -or $page.BaseResponse.ResponseUri.AbsolutePath -match '/Login') {
            throw "Unexpected response/session loss at $path"
        }
        if ($cycle -gt 0) { $results += [pscustomobject]@{Path=$path;Ms=$elapsed} }
    }
    if ($cycle -eq 0) { Write-Output 'Warmup complete' }
}
$timer.Restart()
$pdf = Invoke-WebRequest "$base/Desktop/DownloadPdf/$desktopId" -WebSession $session -UseBasicParsing -TimeoutSec 90
if ($pdf.Headers['Content-Type'] -notmatch 'application/pdf') { throw 'PDF response invalid.' }
Write-Output "PdfMs=$($timer.ElapsedMilliseconds)"
$results | Group-Object Path | ForEach-Object {
    $stats = $_.Group | Measure-Object Ms -Minimum -Maximum -Average
    [pscustomobject]@{Path=$_.Name;Requests=$_.Count;MinMs=$stats.Minimum;MaxMs=$stats.Maximum;AverageMs=[math]::Round($stats.Average,1)}
} | Format-Table -AutoSize
$slow = @($results | Where-Object Ms -ge 2000).Count
Write-Output "Requests=$($results.Count); RequestsOver2Seconds=$slow; SessionLosses=0"
if ($slow -gt 0) { throw 'Warm navigation exceeded the 2-second target.' }
