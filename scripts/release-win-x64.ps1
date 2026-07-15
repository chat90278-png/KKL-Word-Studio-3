param(
    [string]$Version = "1.0.0"
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
$publishDirectory = Join-Path $artifactRoot "KKL-Word-Studio-v$Version-win-x64"
$zipPath = "$publishDirectory.zip"

if (Test-Path $publishDirectory) {
    Remove-Item $publishDirectory -Recurse -Force
}
if (Test-Path $zipPath) {
    Remove-Item $zipPath -Force
}
New-Item -ItemType Directory -Path $publishDirectory -Force | Out-Null

Invoke-CheckedNative "Release clean" { dotnet clean -c Release }
Invoke-CheckedNative "Package restore" { dotnet restore }
Invoke-CheckedNative "Release build" { dotnet build -c Release --no-restore }
Invoke-CheckedNative "Release tests" { dotnet test -c Release --no-build }
Invoke-CheckedNative "Windows publish" {
    dotnet publish $project `
        -c Release `
        -r win-x64 `
        --self-contained true `
        -p:PublishProfile=win-x64-self-contained `
        -p:Version=$Version `
        -o $publishDirectory
}

$commit = (& git rev-parse HEAD).Trim()
if ($LASTEXITCODE -ne 0) {
    throw "Unable to resolve the release commit."
}

Set-Content -Path (Join-Path $publishDirectory "BUILD-COMMIT.txt") -Value $commit -Encoding utf8
Copy-Item "RELEASE_NOTES.md" (Join-Path $publishDirectory "RELEASE_NOTES.md") -Force
Compress-Archive -Path (Join-Path $publishDirectory "*") -DestinationPath $zipPath -CompressionLevel Optimal

Write-Host "Release package created: $zipPath"
Write-Host "Commit: $commit"
