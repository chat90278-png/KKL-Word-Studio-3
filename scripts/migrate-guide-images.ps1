$ErrorActionPreference = 'Stop'

Add-Type -AssemblyName PresentationCore

$repositoryRoot = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path
$assetsDirectory = Join-Path $repositoryRoot 'src\KKL.WordStudio.UI\Assets\GuideScreens'
$outputPath = Join-Path $repositoryRoot 'migration-output.txt'
$messages = [System.Collections.Generic.List[string]]::new()

function Get-NormalizedBase64Payload {
    param([Parameter(Mandatory)][string]$Text)

    $normalized = -join ([regex]::Matches($Text, '[A-Za-z0-9+/=]') | ForEach-Object { $_.Value })
    $jpegStart = $normalized.IndexOf('/9j/', [StringComparison]::Ordinal)
    $pngStart = $normalized.IndexOf('iVBORw0KGgo', [StringComparison]::Ordinal)

    $starts = @($jpegStart, $pngStart) | Where-Object { $_ -ge 0 }
    if ($starts.Count -eq 0) {
        throw 'The resource does not contain a JPEG or PNG Base64 payload.'
    }

    $start = ($starts | Measure-Object -Minimum).Minimum
    $normalized = $normalized.Substring($start)

    $paddingIndex = $normalized.IndexOf('=')
    if ($paddingIndex -ge 0) {
        $normalized = $normalized.Substring(0, $paddingIndex)
    }

    while ($normalized.Length -gt 0 -and $normalized.Length % 4 -eq 1) {
        $normalized = $normalized.Substring(0, $normalized.Length - 1)
    }

    $padding = (4 - ($normalized.Length % 4)) % 4
    if ($padding -gt 0) {
        $normalized = $normalized.PadRight($normalized.Length + $padding, '=')
    }

    return $normalized
}

function Repair-JpegEndMarker {
    param([Parameter(Mandatory)][byte[]]$Bytes)

    $isJpeg = $Bytes.Length -ge 3 -and $Bytes[0] -eq 0xFF -and $Bytes[1] -eq 0xD8 -and $Bytes[2] -eq 0xFF
    if (-not $isJpeg) {
        return $Bytes
    }

    for ($index = 2; $index -lt $Bytes.Length - 1; $index++) {
        if ($Bytes[$index] -eq 0xFF -and $Bytes[$index + 1] -eq 0xD9) {
            $trimmed = [byte[]]::new($index + 2)
            [Array]::Copy($Bytes, $trimmed, $trimmed.Length)
            return $trimmed
        }
    }

    $repaired = [byte[]]::new($Bytes.Length + 2)
    [Array]::Copy($Bytes, $repaired, $Bytes.Length)
    $repaired[$Bytes.Length] = 0xFF
    $repaired[$Bytes.Length + 1] = 0xD9
    return $repaired
}

try {
    $sources = @(Get-ChildItem -Path $assetsDirectory -Filter '*.jpg.base64' | Sort-Object Name)
    if ($sources.Count -eq 0) {
        throw 'No Base64 guide screenshots were found to migrate.'
    }

    foreach ($source in $sources) {
        $text = [IO.File]::ReadAllText($source.FullName)
        $payload = Get-NormalizedBase64Payload -Text $text
        $bytes = [Convert]::FromBase64String($payload)
        $bytes = Repair-JpegEndMarker -Bytes $bytes

        $inputStream = [IO.MemoryStream]::new($bytes, $false)
        try {
            $bitmap = [System.Windows.Media.Imaging.BitmapImage]::new()
            $bitmap.BeginInit()
            $bitmap.CacheOption = [System.Windows.Media.Imaging.BitmapCacheOption]::OnLoad
            $bitmap.CreateOptions = [System.Windows.Media.Imaging.BitmapCreateOptions]::PreservePixelFormat
            $bitmap.StreamSource = $inputStream
            $bitmap.EndInit()
            $bitmap.Freeze()
        }
        finally {
            $inputStream.Dispose()
        }

        if ($bitmap.PixelWidth -le 0 -or $bitmap.PixelHeight -le 0) {
            throw "Decoded image has invalid dimensions: $($source.Name)"
        }

        $targetPath = $source.FullName.Substring(0, $source.FullName.Length - '.base64'.Length)
        $outputStream = [IO.File]::Create($targetPath)
        try {
            $encoder = [System.Windows.Media.Imaging.JpegBitmapEncoder]::new()
            $encoder.QualityLevel = 92
            $encoder.Frames.Add([System.Windows.Media.Imaging.BitmapFrame]::Create($bitmap))
            $encoder.Save($outputStream)
        }
        finally {
            $outputStream.Dispose()
        }

        $jpeg = [IO.File]::ReadAllBytes($targetPath)
        $signature = if ($jpeg.Length -ge 3) { [BitConverter]::ToString($jpeg[0..2]) } else { '<too-short>' }
        if ($jpeg.Length -le 1000 -or $jpeg[0] -ne 0xFF -or $jpeg[1] -ne 0xD8 -or $jpeg[2] -ne 0xFF) {
            throw "JPEG encoding failed: $($source.Name); dimensions=$($bitmap.PixelWidth)x$($bitmap.PixelHeight); bytes=$($jpeg.Length); signature=$signature"
        }

        Remove-Item -LiteralPath $source.FullName
        $messages.Add("Migrated $($source.Name) -> $([IO.Path]::GetFileName($targetPath)) ($($bitmap.PixelWidth)x$($bitmap.PixelHeight), $($jpeg.Length) bytes)")
    }

    [IO.File]::WriteAllText($outputPath, (($messages -join [Environment]::NewLine) + [Environment]::NewLine + 'SUCCESS' + [Environment]::NewLine))
    $messages | ForEach-Object { Write-Host $_ }
}
catch {
    $details = (($messages -join [Environment]::NewLine) + [Environment]::NewLine + $_.Exception.ToString())
    [IO.File]::WriteAllText($outputPath, $details)
    Write-Error $details
    exit 1
}
