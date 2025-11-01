# Generate simple installer images using System.Drawing
# Uruchom z folderu installer/

Add-Type -AssemblyName System.Drawing

Write-Host "Generating installer images..." -ForegroundColor Cyan

# Banner (493 x 58)
$banner = New-Object System.Drawing.Bitmap(493, 58)
$graphics = [System.Drawing.Graphics]::FromImage($banner)
$brush = New-Object System.Drawing.Drawing2D.LinearGradientBrush(
    [System.Drawing.Point]::new(0, 0),
    [System.Drawing.Point]::new(493, 58),
    [System.Drawing.Color]::FromArgb(0, 120, 212),
    [System.Drawing.Color]::FromArgb(0, 80, 150)
)
$graphics.FillRectangle($brush, 0, 0, 493, 58)

$font = New-Object System.Drawing.Font("Arial", 16, [System.Drawing.FontStyle]::Bold)
$textBrush = [System.Drawing.Brushes]::White
$graphics.DrawString("Ogur Sentinel Desktop", $font, $textBrush, 10, 15)

$banner.Save("Banner.bmp", [System.Drawing.Imaging.ImageFormat]::Bmp)
$graphics.Dispose()
$banner.Dispose()

Write-Host "Banner.bmp created (493x58)" -ForegroundColor Green

# Dialog (493 x 312)
$dialog = New-Object System.Drawing.Bitmap(493, 312)
$graphics = [System.Drawing.Graphics]::FromImage($dialog)
$brush = New-Object System.Drawing.Drawing2D.LinearGradientBrush(
    [System.Drawing.Point]::new(0, 0),
    [System.Drawing.Point]::new(0, 312),
    [System.Drawing.Color]::FromArgb(240, 240, 240),
    [System.Drawing.Color]::FromArgb(255, 255, 255)
)
$graphics.FillRectangle($brush, 0, 0, 493, 312)
$dialog.Save("Dialog.bmp", [System.Drawing.Imaging.ImageFormat]::Bmp)
$graphics.Dispose()
$dialog.Dispose()

Write-Host "Dialog.bmp created (493x312)" -ForegroundColor Green
Write-Host ""
Write-Host "Images ready! Now run build-installer.ps1" -ForegroundColor Yellow