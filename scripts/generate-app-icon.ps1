param(
    [Parameter(Mandatory = $true)]
    [string]$Source,

    [Parameter(Mandatory = $true)]
    [string]$Destination
)

$ErrorActionPreference = "Stop"
Set-StrictMode -Version Latest
Add-Type -AssemblyName System.Drawing

$encoded = (Get-Content -LiteralPath $Source -Raw).Trim()
$sourceIcon = [Convert]::FromBase64String($encoded)

if ($sourceIcon.Length -lt 22 -or
    $sourceIcon[0] -ne 0 -or
    $sourceIcon[1] -ne 0 -or
    $sourceIcon[2] -ne 1 -or
    $sourceIcon[3] -ne 0) {
    throw "The application icon source is not a valid Windows ICO file."
}

$sourceImageCount = [BitConverter]::ToUInt16($sourceIcon, 4)
if ($sourceImageCount -lt 1) {
    throw "The application icon source contains no images."
}

$firstEntryOffset = 6
$sourceImageLength = [BitConverter]::ToUInt32($sourceIcon, $firstEntryOffset + 8)
$sourceImageOffset = [BitConverter]::ToUInt32($sourceIcon, $firstEntryOffset + 12)

if ($sourceImageOffset + $sourceImageLength -gt $sourceIcon.Length) {
    throw "The application icon source contains an invalid image entry."
}

$sourcePng = New-Object byte[] ([int]$sourceImageLength)
[Array]::Copy($sourceIcon, [int]$sourceImageOffset, $sourcePng, 0, [int]$sourceImageLength)

$pngSignature = [byte[]](137, 80, 78, 71, 13, 10, 26, 10)
for ($index = 0; $index -lt $pngSignature.Length; $index++) {
    if ($sourcePng[$index] -ne $pngSignature[$index]) {
        throw "The application icon source image must be a PNG frame."
    }
}

$directory = Split-Path -Parent $Destination
if (-not [string]::IsNullOrWhiteSpace($directory)) {
    [IO.Directory]::CreateDirectory($directory) | Out-Null
}

$sizes = @(16, 20, 24, 32, 40, 48, 64, 96, 128, 256)
$frames = @()
$sourceStream = [IO.MemoryStream]::new([byte[]]$sourcePng)
$sourceImage = $null

try {
    $sourceImage = [Drawing.Image]::FromStream($sourceStream, $true, $true)

    foreach ($size in $sizes) {
        if ($size -eq 256) {
            $frameBytes = [byte[]]$sourcePng.Clone()
        }
        else {
            $bitmap = [Drawing.Bitmap]::new(
                $size,
                $size,
                [Drawing.Imaging.PixelFormat]::Format32bppArgb)
            $graphics = [Drawing.Graphics]::FromImage($bitmap)
            $frameStream = [IO.MemoryStream]::new()

            try {
                $graphics.Clear([Drawing.Color]::Transparent)
                $graphics.CompositingMode = [Drawing.Drawing2D.CompositingMode]::SourceCopy
                $graphics.CompositingQuality = [Drawing.Drawing2D.CompositingQuality]::HighQuality
                $graphics.InterpolationMode = [Drawing.Drawing2D.InterpolationMode]::HighQualityBicubic
                $graphics.SmoothingMode = [Drawing.Drawing2D.SmoothingMode]::HighQuality
                $graphics.PixelOffsetMode = [Drawing.Drawing2D.PixelOffsetMode]::HighQuality

                $destinationRectangle = [Drawing.Rectangle]::new(0, 0, $size, $size)
                $graphics.DrawImage(
                    $sourceImage,
                    $destinationRectangle,
                    0,
                    0,
                    $sourceImage.Width,
                    $sourceImage.Height,
                    [Drawing.GraphicsUnit]::Pixel)

                $bitmap.Save($frameStream, [Drawing.Imaging.ImageFormat]::Png)
                $frameBytes = $frameStream.ToArray()
            }
            finally {
                $frameStream.Dispose()
                $graphics.Dispose()
                $bitmap.Dispose()
            }
        }

        $frames += [pscustomobject]@{
            Size = $size
            Bytes = [byte[]]$frameBytes
        }
    }
}
finally {
    if ($null -ne $sourceImage) {
        $sourceImage.Dispose()
    }
    $sourceStream.Dispose()
}

$fileStream = [IO.File]::Create($Destination)
$writer = [IO.BinaryWriter]::new($fileStream)

try {
    $writer.Write([UInt16]0)
    $writer.Write([UInt16]1)
    $writer.Write([UInt16]$frames.Count)

    $imageOffset = 6 + ($frames.Count * 16)

    foreach ($frame in $frames) {
        $dimension = if ($frame.Size -eq 256) { [byte]0 } else { [byte]$frame.Size }

        $writer.Write($dimension)
        $writer.Write($dimension)
        $writer.Write([byte]0)
        $writer.Write([byte]0)
        $writer.Write([UInt16]1)
        $writer.Write([UInt16]32)
        $writer.Write([UInt32]$frame.Bytes.Length)
        $writer.Write([UInt32]$imageOffset)

        $imageOffset += $frame.Bytes.Length
    }

    foreach ($frame in $frames) {
        $writer.Write([byte[]]$frame.Bytes)
    }
}
finally {
    $writer.Dispose()
    $fileStream.Dispose()
}

$output = [IO.File]::ReadAllBytes($Destination)
$outputCount = [BitConverter]::ToUInt16($output, 4)
if ($outputCount -ne $sizes.Count) {
    throw "Generated application icon does not contain the expected frame count."
}

$seenOffsets = New-Object 'System.Collections.Generic.HashSet[UInt32]'
for ($index = 0; $index -lt $sizes.Count; $index++) {
    $entryOffset = 6 + ($index * 16)
    $storedDimension = $output[$entryOffset]
    $actualDimension = if ($storedDimension -eq 0) { 256 } else { [int]$storedDimension }
    $frameLength = [BitConverter]::ToUInt32($output, $entryOffset + 8)
    $frameOffset = [BitConverter]::ToUInt32($output, $entryOffset + 12)

    if ($actualDimension -ne $sizes[$index]) {
        throw "Generated application icon contains an unexpected frame size."
    }
    if (-not $seenOffsets.Add($frameOffset)) {
        throw "Generated application icon contains duplicate frame offsets."
    }
    if ($frameOffset + $frameLength -gt $output.Length) {
        throw "Generated application icon contains an invalid frame entry."
    }

    for ($signatureIndex = 0; $signatureIndex -lt $pngSignature.Length; $signatureIndex++) {
        if ($output[$frameOffset + $signatureIndex] -ne $pngSignature[$signatureIndex]) {
            throw "Generated application icon contains a non-PNG frame."
        }
    }
}

if ($output.Length -lt 10000) {
    throw "Generated multi-resolution application icon is unexpectedly small."
}
