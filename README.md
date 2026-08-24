# Kanban Task Board

A Windows desktop kanban board app — WPF (.NET, `net10.0-windows`), SQLite-backed. Personal project, developed across two computers with this folder synced via OneDrive and pushed to GitHub (`origin` = [`JeremyHillier/kanban-program`](https://github.com/JeremyHillier/kanban-program)).

See [`CHANGELOG.md`](CHANGELOG.md) for version history and [`CLAUDE.md`](CLAUDE.md) for the deeper architecture/conventions notes used by Claude Code sessions working on this repo.

## Folder structure

### Source code

| Folder | What's in it |
|---|---|
| `Views/` | The UI itself — every window/dialog as an `.xaml` (layout) + `.xaml.cs` (code-behind) pair: the main board, Add/Edit Task, Settings, Report Builder, Dashboard, Archived/Deleted lists, and so on. |
| `ViewModels/` | The app's state and logic that the UI binds to (MVVM). `MainViewModel.*.cs` is split into focused files by concern — Cards, Filters, Settings, Sorting, Recurring, Attachments, ArchiveDelete, ManagedLists, Import, Dashboard. |
| `Services/` | Everything that isn't UI: `DatabaseService.*.cs` (all the SQLite access, split by entity — Cards, Projects, People, Goals, Flags, Attachments, SubTasks, Settings, Columns, Schema), `AppConfig.cs` (where the database file lives), `AppChannel.cs` (Test vs Production identity). |
| `Models/` | Plain data classes (`CardItem`, `Project`, `Flag`, etc.) — the shape of what's actually read from and written to the database. |
| `Converters/` | Small WPF value converters that translate data for display (e.g. a priority string into a color). |
| `Theming/` | Light/dark mode logic. |
| `Assets/` | The app icon and the Help screen's screenshots. |
| `App.xaml` / `App.xaml.cs` | Application startup and global styles/resources. |
| `MainWindow.xaml` / `MainWindow.xaml.cs` | The main board window itself. |
| `KanbanApp.csproj` | The project file — dependencies and the version number. |

If you just want to browse the app's logic, `Views/`, `ViewModels/`, and `Services/` are the three that matter — everything else below is data shape, build output, or tooling.

### Installer

| Folder | What's in it |
|---|---|
| `installer/` | The Inno Setup script (`KanbanTaskBoard.iss`), the build script (`build-installers.ps1`), wizard images, and `Output/` (the built `.exe` installers — not tracked in git). |

### Build output (auto-generated, safe to ignore)

| Folder | What's in it |
|---|---|
| `bin/` | Compiled binaries from `dotnet build`. |
| `obj/` | Intermediate build files. |
| `publish/` | Self-contained build output that feeds the installer. |

### Tooling (not app code)

| Folder | What's in it |
|---|---|
| `.git/` | The git repository itself. |
| `.claude/` | Claude Code project settings for this repo. |
| `.gitignore` | Tells git which of the above to ignore (`bin/`, `obj/`, `publish/`, `installer/Output/`). |

## Build

```
dotnet build
```

from the project root. No special setup beyond the .NET SDK. Defaults to the **Test** channel (a disposable data folder) unless built with `-p:AppChannel=Production` — see `CLAUDE.md` for details.
