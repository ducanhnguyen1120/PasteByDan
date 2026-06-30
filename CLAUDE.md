# Paste by Dan — CLAUDE.md

## Stack
C# WPF, .NET Framework 4.8, Newtonsoft.Json

## Build & Run
```
dotnet build PasteByDan/PasteByDan.csproj
dotnet run --project PasteByDan/PasteByDan.csproj
```

For release:
```
dotnet publish -c Release -r win-x64 --project PasteByDan/PasteByDan.csproj
```

## Architecture
- `MainWindow.xaml.cs` — WndProc hook, clipboard listener, hotkey, tray
- `Services/Win32.cs` — all P/Invoke declarations
- `Services/ClipboardService.cs` — monitor + write with chw suppression
- `Services/PasteService.cs` — ForceForeground + SendInput
- `Services/StorageService.cs` — JSON persistence to %APPDATA%\PasteByDan\store.json
- `ViewModels/MainViewModel.cs` — INotifyPropertyChanged, collections
- `InputDialog.xaml` — rename/create collection dialog
- `SettingsWindow.xaml` — settings panel

## Key technical notes
- chw suppression: ExcludeClipboardContentFromMonitorProcessing in same OpenClipboard session
- WM_CLIPBOARDUPDATE (0x031D) for clipboard monitoring — no polling
- WM_HOTKEY (0x0312) for global hotkey Ctrl+Shift+V
- prevHwnd captured via GetForegroundWindow() BEFORE showing window
- AttachThreadInput required for SetForegroundWindow to work across processes
- Window: frameless, transparent, full-width bottom of screen, 27% height
- Cards: horizontal scroll, 170px wide, single-click=copy, double-click=paste
- Right-click context menu: Copy, Paste, Pin, Move to Collection, Delete
