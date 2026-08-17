# Kanban Task Board

WPF (.NET, `net10.0-windows`) desktop kanban app, SQLite-backed via `Microsoft.Data.Sqlite`. Personal project, developed from two computers with this folder synced via OneDrive and pushed to GitHub (`origin` = `JeremyHillier/kanban-program`).

## Working across two computers

- **Git via GitHub is the source of truth, not OneDrive.** OneDrive also raw-syncs the `.git` folder since the whole project sits inside OneDrive — that's a secondary convenience, not the sync mechanism. `git pull` at the start of a session. Don't leave uncommitted work sitting around between sessions on either machine.
- **Auto-push**: the user wants every commit pushed to GitHub immediately after it's made, without asking first — standing instruction, not a one-off. Push right after each commit on both machines.
- If you ever see a stuck `index.lock`, weird "bad object", or other git corruption, it's almost certainly OneDrive's file-level sync colliding with git's own writes to `.git` (e.g. both machines touching it near-simultaneously). Recover via `git fsck` / re-clone from GitHub if needed; don't try to hand-fix `.git` internals.
- This file is the persistent shared brief between sessions/machines — Claude Code loads it automatically at the start of every session in this repo. Keep it updated with anything a fresh session on either computer would need to not re-derive from scratch. Session history and Claude's memory do **not** sync between machines — only files (including this one) do.
- Commit messages here are doing real work as a paper trail (e.g. `Fix the real cause of the quick-edit freeze` → `Fix the actual cause...`) — keep them descriptive; `git log` is the fastest way for a session on the other machine to re-orient.

## Build

`dotnet build` from the project root. No special setup beyond the SDK.

## Architecture

Standard MVVM:
- `Models/` — plain POCOs (`CardItem`, `Project`, `Goal`, `Flag`, `Person`, `SubTaskItem`, `CardAttachment`, `ArchivedCardInfo`/`DeletedCardInfo`, `ImportedTaskRow`, `ReportRow`).
- `ViewModels/` — `ObservableObject`/`RelayCommand` base, one VM per model, plus `MainViewModel` (large — settings passthrough, filters/sort, card CRUD, per-managed-list CRUD, lifecycle transitions).
- `Views/` — one Window per feature (XAML + code-behind). `MainWindow.xaml.cs` is code-behind-heavy (drag/drop, quick-edit popups, keyboard shortcuts). Dashboard and Report/Print logic live mostly in Views' code-behind rather than dedicated ViewModels — an inconsistency with the rest of the app, not a bug.
- `Services/` — `DatabaseService` (SQLite, schema migration via ad hoc `MigrateColumn` calls, no formal migrations table), `AppConfig` (bootstrap JSON: DB path, pending cleanup path), `AppChannel` (`#if TEST_CHANNEL` selects Production/Test data folder + mutex), `ReportService` (PDFsharp export, reads Segoe UI straight from `%Windows%\Fonts` — fragile if that font isn't installed there), `ImportService` (ClosedXML Excel template + import).
- `Theming/ThemeManager.cs`, `Converters/`.

### Key conventions

- **Managed lists** (Project/Goal/Person/Flag): each own table with `Id, Name, SortOrder, IsActive`. Soft-toggle via `SetXActive`; hard `DeleteX` only allowed from UI when nothing references it. Cards reference by FK (`ProjectId`/`GoalId`/`WhoId`/`CardFlags` join table). The four CRUD blocks in `MainViewModel` are near-identical copy-paste — a refactor candidate if a fifth list ever gets added.
- **Task soft-delete/archive**: `Cards.IsArchived` and `Cards.IsDeleted` are independent flags. Every lifecycle transition writes to `CardHistory`, which also backs the archived/deleted list's "when" display via `MAX(Timestamp)` — there's no dedicated timestamp column for that.
- **Settings**: in-app preferences live in the SQLite `Settings` key/value table (`GetSetting`/`SetSetting`), read once into `MainViewModel` at `Load()`. `AppConfig`'s JSON file is only for bootstrap info needed before the DB is even open.
- **`Who` legacy column**: `Cards.Who` (free text) still exists alongside `WhoId` (FK to `People`), kept only for the one-time `BackfillPeopleFromLegacyWho` migration. Don't write to `Who` in new code.
- **Recurrence**: `SpawnNextOccurrence` creates the next task when a recurring one completes — check this if adding new completion-adjacent behavior.

## Known rough edges

- Dashboard / Report / Print bypass MVVM (logic in code-behind) — inconsistent with the rest of the app, not urgent to fix but don't copy the pattern forward.
- No DB migration versioning — fine at current scale, but there's no rollback story if a migration ever needs undoing.
- PDF export's font resolver isn't bundling fonts — could break on a machine without Segoe UI at the expected path.
