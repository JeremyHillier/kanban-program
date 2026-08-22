# Changelog

All notable changes to the Kanban Task Board app, by version. Newest first.

> Note on numbering: versions briefly reached 1.2.0 on 2026-08-14 before a deliberate decision to
> stay in 0.x while the app is still under active development; the next release after 1.2.0 was
> renumbered 0.7.0 and versioning has continued from there.

## 0.55.0 — 2026-08-19
- Date pickers now highlight on hover too (the sidebar's From/To range and the Add/Edit Task dialog's Due Date), via a central template fix that covers every DatePicker in the app
- Added a reusable hover style for text fields and applied it to the Add/Edit Task dialog's Task Details, Notes, and sub-task title fields, alongside the sidebar's Keyword box from the last release
- Confirmed every button and dropdown in the Add/Edit Task dialog already had hover feedback from prior fixes — no gaps found there

## 0.54.0 — 2026-08-19
- Keyword search now also matches task Notes, not just title/project/who
- Filter dropdowns and the Keyword box now highlight on hover too, matching the button/card treatment from the last release

## 0.53.0 — 2026-08-19
- Buttons and cards now highlight on mouse hover, app-wide (several button styles had no hover feedback at all)
- Attachments (both linked files and pasted screenshots) now auto-organize into Done/Archived/Deleted subfolders of the Attachments folder as a task's status changes, and move back if it returns to the board; a file still shared with another task is left in place

## 0.52.0 — 2026-08-19
- Esc now clears every filter on the main board, from any focus state (previously nothing was wired up, so some controls' own native Escape handling made it look like it only cleared some filters)
- Add Alt+H shortcut to open Help

## 0.51.5 — 2026-08-19
- Fix due-date popup freeze for real: release stuck mouse capture

## 0.51.4 — 2026-08-19
- Fix due-date popup freeze when picking a date from the calendar

## 0.51.3 — 2026-08-19
- Widen the sidebar and drop the "Other" section header

## 0.51.2 — 2026-08-19
- Stability pass: close every remaining instance of the popup-close/collection-mutation deadlock

## 0.51.1 — 2026-08-19
- Fix remaining board freeze: due-date popup closing synchronously

## 0.51.0 — 2026-08-19
- Fix board freeze; sort reminders by due date then priority; add due-date range filter; dialog wording ("Save & Close")

## 0.50.1 — 2026-08-18
- Brighten card Project name; include overdue tasks in the "Within a Week" filter

## 0.50.0 — 2026-08-18
- Support multi-key sorting on the board with Ctrl+click

## 0.49.0 — 2026-08-18
- Let a card's Project be changed directly from the board

## 0.48.2 — 2026-08-18
- Hide the quick-add-flag button on compact cards

## 0.48.1 — 2026-08-18
- Fix Outlook drag-drop attachments: DV_E_FORMATETC on classic Outlook

## 0.48.0 — 2026-08-18
- Add drag-and-drop attachments from Outlook and Explorer

## 0.47.3 — 2026-08-18
- Fix quick-action button padding and align colors to column status

## 0.47.2 — 2026-08-18
- Formatting polish: quick buttons, Close button, footer, Tomorrow wrap, gaps

## 0.47.1 — 2026-08-18
- Dedup the four managed-list CRUD blocks (Project/Person/Goal/Flag)

## 0.47.0 — 2026-08-18
- Add Who/Priority/Category to reminder dialog; round buttons app-wide

## 0.46.1 — 2026-08-18
- Fix filters not applying at startup; add crash resilience and logging

## 0.46.0 — 2026-08-18
- Live-refresh reminder rows, two-column Add Task layout, Today date shortcuts

## 0.45.1 — 2026-08-18
- Fix dark-mode contrast for ComboBox and DatePicker chrome app-wide

## 0.45.0 — 2026-08-18
- Report Builder: archived-only scope + date range + sub-task completion counts; reflow Settings; fix dark-mode selected dropdown item

## 0.44.0 — 2026-08-17
- Rename columns, remember last view, archived tasks + counts in reports, dark-mode dropdown fix

## 0.43.0 — 2026-08-17
- Add mark-done checkbox to reminders, Alt+R shortcut, keep list open on double-click

## 0.42.0 — 2026-08-17
- Add due-date reminder pop-up on startup, plus a manual Reminders button

## 0.41.1 — 2026-08-17
- Alternate row banding for Archived Tasks, tighter row spacing, Enter-to-submit on several dialogs

## 0.41.0 — 2026-08-17
- Fix washed-out dashboard chart colors, add stacked charts

## 0.40.0 — 2026-08-17
- Add per-card "Force edit upon completion" toggle

## 0.39.2 — 2026-08-17
- Make dark-mode column header text pure white

## 0.39.1 — 2026-08-17
- Darken column colors in dark mode, add shortcut hints to tooltips

## 0.39.0 — 2026-08-17
- Add Alt+ keyboard shortcuts and Esc-to-close on dialogs

## 0.38.0 — 2026-08-17
- UI polish: tighter Manage rows, readable confirmations, Low priority

## 0.37.2 — 2026-08-17
- Fix the actual cause of the whole-app freeze on quick-edit

## 0.37.1 — 2026-08-17
- Fix the real cause of the quick-edit freeze

## 0.37.0 — 2026-08-17
- Fix quick-edit freeze; add due date quick-edit on cards

## 0.36.0 — 2026-08-17
- Reposition flag button, add inline priority/assignee edit on cards

## 0.35.0 — 2026-08-17
- Add Confirm Archive setting; report preview is now a real print preview

## 0.34.0 — 2026-08-17
- Focus Project on task dialog, add recurrence options, highlight active sort

## 0.33.0 — 2026-08-16
- Make task column width configurable in Settings

## 0.32.1 — 2026-08-16
- Widen task columns by 20%

## 0.32.0 — 2026-08-16
- Reorder task dialog, add flag quick-add, alphabetical sorting, and polish

## 0.31.0 — 2026-08-16
- Add keyboard shortcuts for new task, report builder, and quit

## 0.30.0 — 2026-08-16
- Highlight overdue tasks, require Project field, add Unassigned filters

## 0.29.2 — 2026-08-16
- Fix Close button center alignment

## 0.29.1 — 2026-08-16
- Sidebar layout fixes

## 0.29.0 — 2026-08-16
- Convert Who to a managed list; improve card layout

## 0.28.0 — 2026-08-16
- Add Windows installer with Production/Test channel separation

## 0.27.0 — 2026-08-16
- Add task attachments: linked files and pasted screenshots

## 0.26.0 — 2026-08-16
- Auto-clean up old database file after changing storage location

## 0.25.0 — 2026-08-16
- Add Active/Inactive toggle for Projects, Goals, and Flags

## 0.24.0 — 2026-08-16
- Add By Project and By Who charts to Dashboard

## 0.23.0 — 2026-08-16
- Add Dashboard: header stats strip + hand-rolled chart window

## 0.22.2 — 2026-08-16
- Remove Due filter dropdown, keep quick-select buttons only

## 0.22.1 — 2026-08-16
- Include overdue tasks in the "Today" due filter

## 0.22.0 — 2026-08-16
- Add Sort by Priority, Due quick-filters, rename Compact Cards, reactivate on double-click

## 0.21.0 — 2026-08-16
- Use Default Import Path for template save; add Linked Files Default Path

## 0.20.1 — 2026-08-16
- Suppress Excel alert on new Project/Goal values; left-align Due Date column

## 0.20.0 — 2026-08-16
- Allow new Project/Goal on Excel template; enter dates MM/DD/YYYY, show DD-MMM-YYYY

## 0.19.2 — 2026-08-16
- Fix Excel template dropdown validation shifted one column left

## 0.19.1 — 2026-08-16
- Constrain Excel template fields to dropdowns; widen imported-tasks columns

## 0.19.0 — 2026-08-16
- Add Excel task import, imported-task review, and slightly darker background

## 0.18.0 — 2026-08-15
- Remember window size and position between sessions

## 0.17.0 — 2026-08-15
- Add double-click column header to create a task pre-assigned to that column

## 0.16.1 — 2026-08-15
- Fix: window no longer auto-expands when a column overflows

## 0.16.0 — 2026-08-15
- Add Report Builder: customizable report dialog, on-screen preview, and PDF export

## 0.15.0 — 2026-08-15
- Add full-screen/confirm-delete/note-on-complete settings; center sidebar buttons

## 0.14.0 — 2026-08-15
- Add default export/import paths in Settings and a Help dialog

## 0.13.0 — 2026-08-15
- Make Project/Goal clear non-destructive, add Notes field, tighten dialog

## 0.12.0 — 2026-08-15
- Tighten Task dialog layout, add inline delete for Project/Goal, add card size toggle

## 0.11.0 — 2026-08-15
- Add optional sub-task checklists to tasks

## 0.10.3 — 2026-08-15
- Give inline +New buttons more breathing room; rename Add Task to New Task

## 0.10.2 — 2026-08-15
- Restyle inline +New buttons as small, consistently-aligned "+" icons

## 0.10.1 — 2026-08-15
- Fix cropped prompt dialog buttons and put Sort & Add on one row

## 0.10.0 — 2026-08-15
- Polish task dialog, sidebar buttons, card styling, and splash logo

## 0.9.0 — 2026-08-15
- Add task flags, soft-delete with reactivation, and splash settings

## 0.8.0 — 2026-08-15
- Version bump (no functional change)

## 0.7.0 — 2026-08-15
- Add top banner, unified sidebar, Settings dialog, splash screen, and rework versioning (renumbered down from 1.2.0 — see note above)

## 1.2.0 — 2026-08-14
- Add quick-action buttons, last-updated timestamps, and dark mode

## 1.1.0 — 2026-08-14
- Add double-click-to-edit and Priority/Due Date/Who fields to tasks

## 1.0.0 — 2026-08-13
- Relayout controls, add archived-tasks viewer, card history log, and app versioning
