param([string]$BaseUrl = 'https://localhost:7274')
$ErrorActionPreference = 'Stop'
if ([uri]$BaseUrl -and ([uri]$BaseUrl).Host -notin @('localhost', '127.0.0.1', '::1')) { throw 'Local URLs only.' }
$secretPath = Join-Path $env:APPDATA 'Microsoft\UserSecrets\82ee8068-3edf-4e80-82da-357ab76bfe9e\secrets.json'
$credentials = Get-Content -LiteralPath $secretPath -Raw | ConvertFrom-Json
$session = New-Object Microsoft.PowerShell.Commands.WebRequestSession
$timer = [Diagnostics.Stopwatch]::StartNew()
$form = Invoke-WebRequest "$BaseUrl/Login/Index" -WebSession $session -UseBasicParsing -TimeoutSec 90
Write-Output "LoginFormMs=$($timer.ElapsedMilliseconds)"
$token = [regex]::Match($form.Content, 'name="__RequestVerificationToken"[^>]*value="([^"]+)"').Groups[1].Value
if (-not $token) { throw 'Antiforgery token missing.' }
$timer.Restart()
try {
    $response = Invoke-WebRequest "$BaseUrl/Login/LoginUser" -Method Post -WebSession $session -UseBasicParsing -TimeoutSec 90 -Body @{
        Email = $credentials.'BootstrapAdmin:Email'
        Password = $credentials.'BootstrapAdmin:Password'
        __RequestVerificationToken = [Net.WebUtility]::HtmlDecode($token)
    }
} finally { $credentials = $null }
if ($response.BaseResponse.ResponseUri.AbsolutePath -match '/Login') { throw 'Login rejected.' }
Write-Output "LoginAndDashboardMs=$($timer.ElapsedMilliseconds); Status=$($response.StatusCode)"
foreach ($path in @('/', '/Desktop', '/Peripheral', '/UserAdmin', '/Location', '/AccessAccount')) {
    $timer.Restart()
    try {
        $page = Invoke-WebRequest "$BaseUrl$path" -WebSession $session -UseBasicParsing -TimeoutSec 90
        Write-Output "Path=$path; Status=$($page.StatusCode); Ms=$($timer.ElapsedMilliseconds); SessionValid=$($page.BaseResponse.ResponseUri.AbsolutePath -notmatch '/Login')"
    } catch {
        Write-Output "Path=$path; Ms=$($timer.ElapsedMilliseconds); Error=$($_.Exception.Message)"
    }
}
