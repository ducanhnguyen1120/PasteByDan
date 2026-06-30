# Paste by Dan — CLAUDE.md

## Stack
C# WPF, .NET Framework 4.8, Newtonsoft.Json

## Build & Run
```
dotnet build PasteByDan/PasteByDan.csproj
```

For release + installer:
```
dotnet build PasteByDan/PasteByDan.csproj -c Release
& "C:\Users\Admin\AppData\Local\Programs\Inno Setup 6\ISCC.exe" installer.iss
# Output: installer/PasteByDan-Setup.exe
```

## Architecture
- `MainWindow.xaml.cs` — WndProc hook, clipboard listener, LL keyboard hook, tray, multi-monitor positioning
- `Services/Win32.cs` — all P/Invoke declarations
- `Services/ClipboardService.cs` — monitor + write with chw suppression
- `Services/PasteService.cs` — SendInput Ctrl+V (VK_RCONTROL to bypass PowerToys remap)
- `Services/StorageService.cs` — JSON persistence to %APPDATA%\PasteByDan\store.json
- `ViewModels/MainViewModel.cs` — INotifyPropertyChanged, collections
- `SettingsWindow.xaml` — settings panel with hotkey recorder

## Key technical notes
- chw suppression: ExcludeClipboardContentFromMonitorProcessing set FIRST in same OpenClipboard session
- WM_CLIPBOARDUPDATE (0x031D) for clipboard monitoring — no polling
- Hotkey: WH_KEYBOARD_LL (low-level hook) — distinguishes LCtrl/RCtrl/LShift/RShift/LAlt/RAlt/LWin/RWin
- HotkeyModVKs uses [JsonProperty(ObjectCreationHandling = ObjectCreationHandling.Replace)] to prevent Newtonsoft appending defaults on load
- prevHwnd tracked via SetWinEventHook(EVENT_SYSTEM_FOREGROUND) — works even when opened from tray
- Multi-monitor: MonitorFromWindow(_prevHwnd) + GetMonitorInfo → window appears on active monitor
- VK_RCONTROL (0xA3) + KEYEVENTF_EXTENDEDKEY for paste — bypasses PowerToys Left Ctrl remap
- WM_MOUSEHWHEEL (0x020E) handled in WndProc for horizontal scroll tilt wheel
- Window: frameless, transparent, full-width bottom of screen, 27% height
- Cards: horizontal scroll, 170px wide, single-click=copy, double-click=paste
- Hover actions (pin/delete) use Grid overlay — no effect on card layout

## Wishlist
- [ ] File clipboard support: `CF_HDROP` → capture file paths, re-put on clipboard when pasting
