# Build Installer Script
# Uruchom z folderu installer/

Write-Host "========================================" -ForegroundColor Cyan
Write-Host "Building Ogur Sentinel Desktop Installer" -ForegroundColor Cyan
Write-Host "========================================" -ForegroundColor Cyan
Write-Host ""

# 1. Publish aplikacji
Write-Host "[1/3] Publishing application..." -ForegroundColor Yellow
dotnet publish ..\src\Ogur.Sentinel.Devexpress\Ogur.Sentinel.Devexpress.csproj `
  -c Release `
  -r win-x64 `
  -p:PublishSingleFile=true `
  --self-contained true

if ($LASTEXITCODE -ne 0) {
    Write-Host "❌ Publish failed!" -ForegroundColor Red
    exit 1
}

Write-Host "✅ Publish complete!" -ForegroundColor Green
Write-Host ""

# 2. Build MSI
Write-Host "[2/3] Building MSI installer..." -ForegroundColor Yellow
& "C:\Program Files\Microsoft Visual Studio\2022\Community\MSBuild\Current\Bin\MSBuild.exe" `
  Ogur.Sentinel.Installer.wixproj `
  /t:Rebuild `
  /p:Configuration=Release `
  /p:Platform=x64 `
  /p:ProductVersion=1.0.0

if ($LASTEXITCODE -ne 0) {
    Write-Host "❌ MSI build failed!" -ForegroundColor Red
    exit 1
}

Write-Host "✅ MSI build complete!" -ForegroundColor Green
Write-Host ""

# 3. Pokaż wynik
$msiPath = "bin\x64\Release\OgurSentinelSetup.msi"
if (Test-Path $msiPath) {
    $msiSize = (Get-Item $msiPath).Length / 1MB
    Write-Host "[3/3] MSI ready!" -ForegroundColor Green
    Write-Host "📦 Location: $msiPath" -ForegroundColor Cyan
    Write-Host "📏 Size: $($msiSize.ToString('0.00')) MB" -ForegroundColor Cyan
    Write-Host ""
    Write-Host "========================================" -ForegroundColor Cyan
    Write-Host "✅ Build successful!" -ForegroundColor Green
    Write-Host "========================================" -ForegroundColor Cyan
    Write-Host ""
    Write-Host "To install:" -ForegroundColor Yellow
    Write-Host "  msiexec /i `"$msiPath`"" -ForegroundColor White
} else {
    Write-Host "❌ MSI not found at expected location!" -ForegroundColor Red
    exit 1
}
