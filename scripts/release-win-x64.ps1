param(
    [string]$Version = "1.0.2"
)

$ErrorActionPreference = "Stop"
Set-StrictMode -Version Latest

function Invoke-CheckedNative {
    param(
        [string]$Description,
        [scriptblock]$Command
    )

    & $Command
    if ($LASTEXITCODE -ne 0) {
        throw "$Description failed with exit code $LASTEXITCODE."
    }
}

$repositoryRoot = Split-Path -Parent $PSScriptRoot
Set-Location $repositoryRoot

$project = "src\KKL.WordStudio.UI\KKL.WordStudio.UI.csproj"
$artifactRoot = Join-Path $repositoryRoot "artifacts"
$publishDirectory = Join-Path $artifactRoot "publish-v$Version-win-x64"
$releaseExe = Join-Path $artifactRoot "KKL-Word-Studio-v$Version-win-x64.exe"
$zipPath = Join-Path $artifactRoot "KKL-Word-Studio-v$Version-win-x64.zip"
$hashPath = "$releaseExe.sha256"

New-Item -ItemType Directory -Path $artifactRoot -Force | Out-Null

# Remove legacy generic-name publish folders so an old KKL.WordStudio.exe cannot
# be copied to the desktop and resolved through Windows' stale icon cache.
Get-ChildItem -Path $artifactRoot -Directory -ErrorAction SilentlyContinue |
    Where-Object {
        $_.Name -like "KKL-Word-Studio-v*-single-exe" -or
        $_.Name -like "publish-v*-win-x64"
    } |
    Remove-Item -Recurse -Force

Get-ChildItem -Path $artifactRoot -File -Filter "KKL.WordStudio.exe" -ErrorAction SilentlyContinue |
    Remove-Item -Force

foreach ($path in @($releaseExe, $zipPath, $hashPath)) {
    if (Test-Path $path) {
        Remove-Item $path -Recurse -Force
    }
}
New-Item -ItemType Directory -Path $publishDirectory -Force | Out-Null

Invoke-CheckedNative "Release clean" { dotnet clean -c Release }
Invoke-CheckedNative "Package restore" { dotnet restore }
Invoke-CheckedNative "Release build" { dotnet build -c Release --no-restore }
Invoke-CheckedNative "Release tests" { dotnet test -c Release --no-build }
Invoke-CheckedNative "Windows single-file publish" {
    dotnet publish $project `
        -c Release `
        -r win-x64 `
        --self-contained true `
        -p:PublishProfile=win-x64-self-contained `
        -p:Version=$Version `
        -p:DebugType=None `
        -p:DebugSymbols=false `
        -o $publishDirectory
}

$publishedExe = Join-Path $publishDirectory "KKL.WordStudio.exe"
if (-not (Test-Path $publishedExe -PathType Leaf)) {
    throw "Single-file publish did not produce KKL.WordStudio.exe."
}

$unexpectedFiles = Get-ChildItem $publishDirectory -File | Where-Object { $_.FullName -ne $publishedExe }
if ($unexpectedFiles) {
    $names = ($unexpectedFiles.Name -join ", ")
    throw "Publish output is not single-file. Unexpected files: $names"
}

Copy-Item $publishedExe $releaseExe -Force
Remove-Item $publishDirectory -Recurse -Force

if (-not (Test-Path $releaseExe -PathType Leaf)) {
    throw "Versioned release executable was not created."
}

Compress-Archive -LiteralPath $releaseExe -DestinationPath $zipPath -CompressionLevel Optimal

$hash = (Get-FileHash $releaseExe -Algorithm SHA256).Hash.ToLowerInvariant()
Set-Content -Path $hashPath -Value "$hash  $(Split-Path $releaseExe -Leaf)" -Encoding ascii

Write-Host "Single executable: $releaseExe"
Write-Host "ZIP package:       $zipPath"
Write-Host "SHA-256:           $hash"
