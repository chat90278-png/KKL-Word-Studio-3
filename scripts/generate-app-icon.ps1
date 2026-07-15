param(
    [Parameter(Mandatory = $true)]
    [string]$Source,

    [Parameter(Mandatory = $true)]
    [string]$Destination
)

$ErrorActionPreference = "Stop"
Set-StrictMode -Version Latest

$encoded = (Get-Content -LiteralPath $Source -Raw).Trim()
$bytes = [Convert]::FromBase64String($encoded)

$directory = Split-Path -Parent $Destination
if (-not [string]::IsNullOrWhiteSpace($directory)) {
    [IO.Directory]::CreateDirectory($directory) | Out-Null
}

[IO.File]::WriteAllBytes($Destination, $bytes)

if ((Get-Item -LiteralPath $Destination).Length -lt 1024) {
    throw "Generated application icon is unexpectedly small."
}
