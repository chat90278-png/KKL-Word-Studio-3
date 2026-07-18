$ErrorActionPreference = 'Stop'

Add-Type -AssemblyName PresentationCore
Add-Type -AssemblyName PresentationFramework

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

function New-GuidePlaceholder {
    param([Parameter(Mandatory)][string]$AssetName)

    $width = 1280
    $height = 720
    $visual = [System.Windows.Media.DrawingVisual]::new()
    $context = $visual.RenderOpen()
    try {
        $background = [System.Windows.Media.SolidColorBrush]::new([System.Windows.Media.Color]::FromRgb(245, 247, 250))
        $border = [System.Windows.Media.SolidColorBrush]::new([System.Windows.Media.Color]::FromRgb(191, 200, 214))
        $foreground = [System.Windows.Media.SolidColorBrush]::new([System.Windows.Media.Color]::FromRgb(45, 55, 72))
        $muted = [System.Windows.Media.SolidColorBrush]::new([System.Windows.Media.Color]::FromRgb(100, 116, 139))

        $context.DrawRectangle($background, $null, [System.Windows.Rect]::new(0, 0, $width, $height))
        $context.DrawRoundedRectangle(
            [System.Windows.Media.Brushes]::White,
            [System.Windows.Media.Pen]::new($border, 2),
            [System.Windows.Rect]::new(80, 90, $width - 160, $height - 180),
            24,
            24)

        $title = [System.Windows.Media.FormattedText]::new(
            'Ekran görüntüsü güncellenecek',
            [Globalization.CultureInfo]::GetCultureInfo('tr-TR'),
            [System.Windows.FlowDirection]::LeftToRight,
            [System.Windows.Media.Typeface]::new('Segoe UI Semibold'),
            42,
            $foreground,
            1.0)
        $title.TextAlignment = [System.Windows.TextAlignment]::Center
        $title.MaxTextWidth = $width - 240
        $context.DrawText($title, [System.Windows.Point]::new(120, 270))

        $detail = [System.Windows.Media.FormattedText]::new(
            $AssetName,
            [Globalization.CultureInfo]::InvariantCulture,
            [System.Windows.FlowDirection]::LeftToRight,
            [System.Windows.Media.Typeface]::new('Segoe UI'),
            24,
            $muted,
            1.0)
        $detail.TextAlignment = [System.Windows.TextAlignment]::Center
        $detail.MaxTextWidth = $width - 240
        $context.DrawText($detail, [System.Windows.Point]::new(120, 350))
    }
    finally {
        $context.Close()
    }

    $bitmap = [System.Windows.Media.Imaging.RenderTargetBitmap]::new(
        $width,
        $height,
        96,
        96,
        [System.Windows.Media.PixelFormats]::Pbgra32)
    $bitmap.Render($visual)
    $bitmap.Freeze()
    return $bitmap
}

function Read-GuideBitmap {
    param([Parameter(Mandatory)][System.IO.FileInfo]$Source)

    try {
        $text = [IO.File]::ReadAllText($Source.FullName)
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

        if ($bitmap.PixelWidth -lt 100 -or $bitmap.PixelHeight -lt 100) {
            throw "Source image is only $($bitmap.PixelWidth)x$($bitmap.PixelHeight)."
        }

        return @{ Bitmap = $bitmap; IsPlaceholder = $false; Reason = $null }
    }
    catch {
        return @{
            Bitmap = New-GuidePlaceholder -AssetName $Source.Name
            IsPlaceholder = $true
            Reason = $_.Exception.Message
        }
    }
}

try {
    $sources = @(Get-ChildItem -Path $assetsDirectory -Filter '*.jpg.base64' | Sort-Object Name)
    if ($sources.Count -eq 0) {
        throw 'No Base64 guide screenshots were found to migrate.'
    }

    foreach ($source in $sources) {
        $result = Read-GuideBitmap -Source $source
        $bitmap = $result.Bitmap
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
        if ($jpeg.Length -le 100 -or $jpeg[0] -ne 0xFF -or $jpeg[1] -ne 0xD8 -or $jpeg[2] -ne 0xFF) {
            throw "JPEG encoding failed: $($source.Name)"
        }

        Remove-Item -LiteralPath $source.FullName
        $note = if ($result.IsPlaceholder) { "placeholder: $($result.Reason)" } else { 'source preserved' }
        $messages.Add("Migrated $($source.Name) -> $([IO.Path]::GetFileName($targetPath)) ($($bitmap.PixelWidth)x$($bitmap.PixelHeight), $($jpeg.Length) bytes, $note)")
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
