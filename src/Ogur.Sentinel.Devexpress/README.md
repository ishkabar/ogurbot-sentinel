# Ogur Sentinel Desktop

A Windows desktop application for tracking and displaying respawn timers with real-time synchronization.

## System Requirements

- Windows 10 or Windows 11 (64-bit)
- .NET 8.0 Runtime (for portable version) or included in installer
- Minimum display resolution: 1920x1080
- Internet connection for timer synchronization

## Installation

### MSI Installer (Recommended)

1. Download `OgurSentinelSetup.msi` from the latest release
2. Double-click the installer file
3. Follow the installation wizard
4. Launch from Start Menu or Desktop shortcut

The installer will:
- Install the application to Program Files
- Create Start Menu shortcuts
- Create Desktop shortcut
- Register for easy uninstallation

### Portable Version

**Framework-dependent** (requires .NET 8.0 Runtime):
1. Download `Ogur.Sentinel.Devexpress-win-x64-*.zip`
2. Extract to any folder
3. Run `Ogur.Sentinel.Devexpress.exe`

**Self-contained** (no additional requirements):
1. Download `Ogur.Sentinel.Devexpress-selfcontained-*.zip`
2. Extract to any folder
3. Run `Ogur.Sentinel.Devexpress.exe`

## First Time Setup

1. Launch the application
2. Enter your username and password
3. Click Login
4. Configure your preferences in Settings (gear icon)

## Features

### Real-time Timer Display

Monitor multiple respawn timers with color-coded warnings:
- **Green border** - Safe time remaining
- **Orange border** - Warning threshold reached
- **Red border** - Critical time remaining

### Settings Configuration

Access settings via the gear icon in the header bar:

**Sync Interval** (5-300 seconds)
- Controls how often the application checks for timer updates
- Default: 30 seconds
- Lower values = more frequent updates, higher server load

**Time Offset** (-180 to +180 seconds)
- Adjusts the displayed time relative to server time
- Negative values show less time remaining
- Example: -30 means timer displays 30 seconds less than actual
- Default: 0 seconds

**Warning Thresholds**
- **Red warning** (0-60 minutes) - When timer shows critical state
- **Orange warning** (0-120 minutes) - When timer shows warning state
- Orange threshold must be greater than red threshold

### Window Controls

**Pin on top** - Keeps the application window above all other windows
**Minimize** - Minimize to taskbar
**Close** - Exit application

**Drag to move** - Click and drag anywhere in the window to reposition it

### Authentication

**Logout** - Available via the door icon in the header bar
**Automatic login** - Application remembers your credentials for convenience

## Usage

### Normal Operation

1. After login, timers automatically sync at the configured interval
2. Timers display countdown in MM:SS format
3. Next respawn time is shown below each timer
4. Status bar at bottom shows sync status and errors

### Changing Settings

1. Click the gear icon
2. Adjust any settings as needed
3. Click Save
4. Restart the application for changes to take effect

### Uninstallation

**MSI Installer version:**
1. Open Windows Settings
2. Go to Apps > Installed apps
3. Find "Ogur Sentinel Desktop"
4. Click Uninstall

**Portable version:**
- Simply delete the application folder

## Configuration Files

Settings are stored in:
```
%APPDATA%\Ogur.Sentinel.Desktop\settings.json
```

To reset to defaults, delete this file and restart the application.

## Troubleshooting

### Application won't start

- Verify you have .NET 8.0 Runtime installed (portable version only)
- Try running as Administrator
- Check Windows Event Viewer for error details

### Cannot connect to server

- Verify your internet connection
- Check that the server is online
- Ensure firewall is not blocking the application
- Try logging out and back in

### Timers not updating

- Check your Sync Interval setting
- Verify status bar shows "Connected" or recent sync time
- Ensure you have an active internet connection
- Try clicking Logout and logging back in

### Login fails

- Verify username and password are correct
- Check that your account is active
- Ensure the server is accessible
- Contact your administrator if issues persist

### Display issues

- Recommended display scaling: 100%
- Minimum window size: 450x850 pixels
- Try adjusting Windows display scaling settings
- Restart the application after changing display settings

### Installer problems

**"A newer version is already installed"**
- Uninstall the current version first
- Then run the installer again

**Installation fails**
- Run installer as Administrator
- Ensure Windows Installer service is running
- Check that you have sufficient disk space

**Cannot uninstall**
- Use Windows Settings > Apps to remove
- If that fails, run installer again and choose Repair, then Uninstall

## Updates

### MSI Installer

When a new version is released:
1. Download the new installer
2. Run it - it will automatically remove the old version
3. Complete the installation

### Portable Version

1. Download the new version
2. Extract to a new folder or replace existing files
3. Your settings will be preserved

## Support

For issues, questions, or feedback:
- Check this README for common solutions
- Review the Troubleshooting section
- Contact your system administrator
- Report bugs via the issue tracker

## Security

- Credentials are stored securely on your local machine
- All communication with the server uses encrypted connections
- Authentication tokens expire after a period of inactivity
- No sensitive data is logged or transmitted to third parties

## Version Information

Check the About section in the application or the installer properties to see the current version number.

## Legal

Copyright (c) 2025 Ogur. All rights reserved.

This software is provided "as is" without warranty of any kind. See the license agreement for full terms and conditions.