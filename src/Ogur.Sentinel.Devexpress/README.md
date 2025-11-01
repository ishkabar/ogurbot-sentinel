# Instalator MSI - Setup

## Struktura projektu

```
repo/
├── src/
│   └── Ogur.Sentinel.Devexpress/
│       └── Ogur.Sentinel.Devexpress.csproj
├── installer/
│   ├── Ogur.Sentinel.Installer.wxs      # Definicja instalatora
│   ├── Ogur.Sentinel.Installer.wixproj  # Projekt WiX
│   └── License.rtf                       # Licencja (wyświetlana w instalatorze)
└── .github/
    └── workflows/
        └── release-windows.yml
```

## Instalacja WiX Toolset (lokalnie)

### Opcja 1: Chocolatey (zalecane)
```powershell
choco install wixtoolset --version=3.14.1 -y
```

### Opcja 2: Ręczna instalacja
1. Pobierz z https://github.com/wixtoolset/wix3/releases
2. Zainstaluj `wix314.exe`
3. Dodaj do PATH: `C:\Program Files (x86)\WiX Toolset v3.14\bin`

## Budowanie MSI lokalnie

### 1. Publish aplikacji
```powershell
dotnet publish src/Ogur.Sentinel.Devexpress/Ogur.Sentinel.Devexpress.csproj `
  -c Release -r win-x64 `
  -p:PublishSingleFile=false `
  --self-contained true `
  -o out/msi-publish
```

### 2. Build MSI
```powershell
msbuild installer/Ogur.Sentinel.Installer.wixproj `
  /t:Rebuild `
  /p:Configuration=Release `
  /p:Platform=x64 `
  /p:ProductVersion=1.0.0
```

MSI będzie w: `installer/bin/x64/Release/OgurSentinelSetup.msi`

## Funkcje instalatora

### Pierwsza instalacja
- Wybór katalogu instalacji
- Tworzenie skrótów (Start Menu + Desktop)
- Rejestracja w Programs & Features

### Aktualizacja
Gdy uruchomisz nowszą wersję MSI:
- ✅ Automatycznie wykrywa zainstalowaną wersję
- ✅ Pokazuje aktualną i nową wersję
- ✅ Usuwa starą wersję przed instalacją nowej
- ✅ Zachowuje ustawienia użytkownika

### Maintenance Mode
Gdy uruchomisz tę samą lub starszą wersję MSI:
- **Modify** - zmień komponenty instalacji
- **Repair** - napraw uszkodzone pliki
- **Remove** - odinstaluj aplikację
- **Update** - zaktualizuj (jeśli dostępna nowsza wersja)

### Wykrywanie wersji
Instalator zapisuje wersję w rejestrze:
```
HKLM\Software\Ogur\Sentinel
  - Version (string)
  - InstallPath (string)
```

## Customizacja instalatora

### Zmiana nazwy produktu
W `Ogur.Sentinel.Installer.wxs`:
```xml
<?define ProductName = "Twoja Nazwa" ?>
```

### Zmiana UpgradeCode
**UWAGA:** Zmień tylko raz przed pierwszym releasem!
```xml
<?define UpgradeCode = "TWOJ-NOWY-GUID" ?>
```

Wygeneruj nowy GUID: https://www.guidgenerator.com/

### Dodanie ikony
1. Dodaj `icon.ico` do folderu `installer/`
2. Odkomentuj w `.wxs`:
```xml
<Icon Id="icon.ico" SourceFile="icon.ico"/>
<Property Id="ARPPRODUCTICON" Value="icon.ico" />
```

### Zmiana licencji
Edytuj `License.rtf` - musi być w formacie RTF!

## GitHub Actions

Workflow automatycznie:
1. Buduje 3 wersje:
   - Framework-dependent (wymaga .NET 8)
   - Self-contained (wszystko w jednym)
   - MSI installer
2. Tworzy checksumy SHA256
3. Publikuje release na GitHub

### Triggery
- **Tag**: `git tag v1.0.0 && git push --tags`
- **Manual**: GitHub Actions → workflow → "Run workflow"

### Versioning
Używa MinVer - wersja z tagów git:
- Tag `v1.2.3` → wersja `1.2.3`
- Bez tagu → `1.0.0-preview.X`

## Testowanie instalatora

### Instalacja
```powershell
msiexec /i OgurSentinelSetup.msi /l*v install.log
```

### Odinstalowanie
```powershell
msiexec /x OgurSentinelSetup.msi /l*v uninstall.log
```

### Upgrade
Po zbudowaniu nowej wersji, po prostu zainstaluj - stara zostanie automatycznie usunięta.

## Troubleshooting

### "WiX Toolset not found"
```powershell
$env:PATH += ";C:\Program Files (x86)\WiX Toolset v3.14\bin"
```

### "File not found" podczas budowania MSI
Sprawdź ścieżkę w `.wxs`:
```xml
<?define PublishDir = "..\Ogur.Sentinel.Devexpress\bin\Release\net8.0-windows\win-x64\publish" ?>
```

### MSI nie instaluje wszystkich plików
Użyj `heat.exe` do automatycznego generowania listy plików:
```powershell
heat dir out/msi-publish -cg ProductComponents -gg -sfrag -srd -dr INSTALLFOLDER -out installer/Files.wxs
```

Potem include w projekcie.

## Publiczna dystrybucja

### Code signing (opcjonalnie)
Dla produkcji warto podpisać instalator:
```powershell
signtool sign /f certificate.pfx /p password /t http://timestamp.digicert.com OgurSentinelSetup.msi
```

### Windows SmartScreen
Niepodpisane aplikacje będą miały warning. Rozwiązania:
1. Kup certyfikat code signing (~$200/rok)
2. Poczekaj aż Microsoft zbuduje reputację (tysiące instalacji)
3. Użytkownik: "More info" → "Run anyway"

## CI/CD Notes

GitHub Actions:
- ✅ Automatyczny build na tag
- ✅ 3 formaty dystrybucji
- ✅ Checksumy SHA256
- ✅ Release notes

Brakuje:
- ❌ Code signing (wymaga certyfikatu)
- ❌ Auto-update (wymaga dodatkowej infrastruktury)
