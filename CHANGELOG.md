# Changelog

All notable changes to the Kanban Task Board app, by version. Newest first.

> Note on numbering: versions briefly reached 1.2.0 on 2026-08-14 before a deliberate decision to
> stay in 0.x while the app is still under active development; the next release after 1.2.0 was
> renumbered 0.7.0 and versioning has continued from there.

## 0.70.3 — 2026-09-02
- Widen the sidebar button panel (280px to 320px) so the Settings/Help/Report Builder/Timeline row fits its fourth button without overflowing
- Timeline: freeze the date-header row so it stays visible while scrolling through project rows, and add alternating row shading to make each project's row easier to track across the columns

## 0.70.2 — 2026-09-02
- Timeline: double-click a task block to open it in the same task dialog the board uses, with any change saved and the Timeline refreshed on close

## 0.70.1 — 2026-09-02
- Timeline: add a Day view alongside the default Week view — zoom in to see 21 daily columns (each labelled with weekday and date) instead of 12 weekly ones, then zoom back out. Prev/Next paging adjusts to match (1 week at a time in Day view, 4 weeks in Week view)

## 0.70.0 — 2026-09-02
- Add a Timeline view (Alt+L): projects down the left, weekly date columns across the top, and each task with a due date shown as a block in the week it falls due, labelled with its title, who it's assigned to, and its due date. Shows 12 weeks at a time with ◀ 4 Weeks / Today / 4 Weeks ▶ navigation; Done tasks are left out unless you check Include Done tasks

## 0.69.3 — 2026-09-02
- Report Builder: print and preview now show a "Parameters" line under the header summarizing every filter, due-date range, custom filter, sort order, group-by, and scope choice the report was built with, so a saved or printed report is self-describing

## 0.69.2 — 2026-09-02
- Report Builder: add a Portrait/Landscape page orientation option for the PDF export and preview

## 0.69.1 — 2026-09-02
- Report Builder: sort the Category level (and Group By) by the board's own column order (To Do, In Progress, On Hold, Waiting, Done) instead of alphabetically; archived rows always sort last
- Report Builder: add Today and Clear buttons beside the due-date range fields, and Clear buttons beside the archived date range fields
- Report Builder: add a "Reset all fields" button that restores every filter, date range, checkbox, and sort/group selection to its opening default

## 0.69.0 — 2026-09-02
- Report Builder: add a due-date range filter (From/To) with a checkbox to also include tasks that have no due date at all
- Report Builder: add a Custom Filters section listing your saved Alt+0-9 filter slots as checkboxes — check one or more and the report includes any task matching at least one of them, in place of the filters above
- Report Builder: add three Sort Order levels (1st/2nd/3rd — Category, Priority, Who, Due Date, Project, Goal) to control row order within each group; an option already used as Group By or an earlier sort level is grayed out to prevent picking it twice

## 0.68.0 — 2026-09-01
- Add a What's New screen that appears the first time you open a newly updated version, listing everything added across the last five updates. It can be turned off in Settings, where a Show What's New button also brings it up any time
- Add ten custom filter slots on Alt+0 to Alt+9. Set the board's filters how you like, then save the whole combination — project, priority, who, goal, flag, due date or range, and keyword — to a slot under a name of your choosing, and recall it with one keystroke. A Custom Filters button beside Clear Filters manages the slots

## 0.67.3 — 2026-09-01
- Darken the category, who, and project lettering on each row of the startup Task Reminders window, which was washed out against the light background
- Add spell checking to the sub-task fields, matching the Task and Notes fields

## 0.67.2 — 2026-09-01
- Add an Alt+T shortcut that shows just what's due today (and anything overdue), clearing every other filter first so it's the whole board's today, not today within whatever was already narrowed down

## 0.67.1 — 2026-09-01
- Extend calendar mouse-wheel month navigation to every remaining date picker: the board's due-date range filter, the Archived and Deleted lists' date ranges, Report Builder's archived date range, and the imported-tasks review grid's per-row due date

## 0.67.0 — 2026-09-01
- Add spell checking (red squiggly underlines, right-click suggestions) to the Task and Notes fields in the Add/Edit Task dialog

## 0.66.1 — 2026-09-01
- Fix calendar mouse-wheel scrolling, which didn't actually work in the previous version: the theme gives a date picker's internal calendar its own style, so the app-wide style meant to carry the handler never applied to it

## 0.66.0 — 2026-09-01
- Scroll the mouse wheel over an open calendar to move a month at a time (up for earlier, down for later)
- Remove the four screenshots from the Help screen
- Widen the gap between the Recurring task checkbox and its pattern dropdown, and give the dropdown a minimum width so the longer options aren't cramped

## 0.65.2 — 2026-08-30
- Darken each dialog's bottom-right copyright line, which was faded to near-invisible against a light background

## 0.65.1 — 2026-08-30
- Show a subtle copyright line in the bottom-right corner of every dialog. The main board, splash screen, and About dialog are left alone, since each already displays it as part of its own design

## 0.65.0 — 2026-08-30
- Replace the Help screen's About section with a proper About dialog, opened by a new About button beside Close: app identity and icon, version, company and copyright, what the app does, this installation's channel/database/settings paths and runtime, acknowledgements, plus Copy details and a Website button linking to hillierconsulting.ca
- Add an optional Website field to each task in the Add/Edit Task dialog, with an Open button that launches the link in your default browser (a link typed without http:// still works); the link is remembered with the task and carried onto a recurring task's next occurrence
- Cards can now be dragged up and down within their column at any time — no mode to switch on first. Doing so hand-arranges that column and unhighlights every sort button; clicking any sort button takes over again. The Manual Sort button added in 0.64.0 is gone, since dragging now does its job on its own

## 0.64.0 — 2026-08-30
- Add manual card sorting: a new Manual Sort button turns off auto-sorting so cards can be dragged up/down within a column into any order, which is then remembered; clicking any other sort button (Project/Due Date/Who/Priority) overrides Manual Sort and turns it back off

## 0.63.9 — 2026-08-30
- Add an About section to the Help screen: app name, version, copyright, and an editable Website field with an Open button that launches it in your default browser

## 0.63.8 — 2026-08-30
- Add/Edit Task dialog: the Recurring task checkbox now sits centered on the same row as the Due Date input instead of floating up near the labels above it, and Force edit upon completion moved from spanning the full row to sitting directly under the Goal dropdown

## 0.63.7 — 2026-08-30
- Fix Report Builder text overlapping: a Title or Notes with an embedded line break (both fields allow multi-line input) was rendering as one TextBlock spanning two physical lines in a space budgeted for one, overlapping whatever came after it — each line is now measured and placed on its own
- Wrap long sub-task titles in the report, which previously ran off the page edge unwrapped like the row Title used to

## 0.63.6 — 2026-08-30
- Dim and disable a card's quick-move button for the column it's already in, since clicking it there was a no-op

## 0.63.5 — 2026-08-30
- Rework the Add/Edit Task dialog's field layout: Priority, Category, and Who now sit on one row, and Due Date, Recurring, and Goal are grouped together on the next row

## 0.63.4 — 2026-08-30
- Fix Report Builder: a long task title now wraps across multiple lines instead of running off the edge of the page/preview, matching how the meta line and notes already wrapped

## 0.63.3 — 2026-08-28
- Rename "Task details" to "Task" in the Add/Edit Task dialog, and cap it at 255 characters

## 0.63.2 — 2026-08-28
- Fix a flickering sub-task drag indicator: DragLeave was firing spuriously whenever the mouse crossed over a row's textbox/checkbox/delete button, toggling the drop-position line hidden and shown as it tracked across the row

## 0.63.1 — 2026-08-28
- Show an insertion-line indicator at the exact spot a dragged sub-task would land, tracking the mouse as you drag it to reorder

## 0.63.0 — 2026-08-28
- Let deleting a recurring task optionally keep the series going: the board's delete button now offers a three-way choice for eligible recurring cards — delete and spawn the next occurrence, delete and end the series, or cancel — instead of always ending it

## 0.62.0 — 2026-08-28
- Add sub-task drag-reordering via a drag handle on each row; checking a sub-task off now also auto-sorts completed sub-tasks to the bottom
- Expand the Notes box from 50px to 110px tall

## 0.61.2 — 2026-08-26
- Fix the Add/Edit Task dialog's Cancel/Add Task buttons becoming unreachable with a long sub-task list: the scrollable form area now has its own height cap, so it gets its own scrollbar instead of just growing the whole window past the screen's bottom edge
- Tighten sub-task row spacing (less padding per row, smaller remove button) so more fit on screen at once

## 0.61.1 — 2026-08-25
- The Edit Task dialog's Email button now reacts live to the Who selection instead of only reflecting the card's already-saved assignee — picking someone with an email on file shows the button immediately, no save/reopen needed

## 0.61.0 — 2026-08-25
- Add emailing a task card via Outlook: people can now have an email address on file (Manage Who), and any card assigned to someone with one gets an Email quick-action — both directly on the card and in the Add/Edit Task dialog — that opens a pre-filled Outlook compose window for review before sending

## 0.60.0 — 2026-08-24
- Fix Help screen text wrapping: a bullet's second line now aligns under the first word of its text instead of under the bullet character
- Add four screenshots to the Help screen (main board, Add/Edit Task dialog, Report Builder, Dashboard) so key screens can be seen at a glance alongside their descriptions
- Add MSIX packaging for Microsoft Store submission (build tooling only — not yet installed/run in packaged form, since Store submission still needs a signing cert or dev-mode sideload)
- Add a README.md documenting the repo's folder structure, for browsing on GitHub or locally

## 0.59.5 — 2026-08-22
- Help screen accuracy pass (audited the whole thing against the current code, no behavior changes): documented the Archived/Deleted lists' Clear Dates button; fixed the Excel import template's required column — it's labeled "Title", not "Task Details" (the review grid afterward does say "Task Details", which was already correct); corrected the Dashboard's chart list to the actual four charts (Status Distribution and Priority Mix are one combined chart, not two) and noted its extra "In Done" summary tile

## 0.59.4 — 2026-08-22
- Internal refactor, no functional change: consolidated the four near-identical ContextMenu-building blocks in MainWindow.xaml.cs (the card's Flags/Priority/Who/Project quick-edit popups) into one shared generic helper

## 0.59.3 — 2026-08-22
- Internal refactor, no functional change: split the 1149-line DatabaseService.cs into 11 focused partial-class files by entity (Schema, Settings, Columns, Cards, SubTasks, Attachments, Flags, Projects, People, Goals), same treatment as MainViewModel.cs in the last update

## 0.59.2 — 2026-08-22
- Internal refactor, no functional change: split the 1339-line MainViewModel.cs into 11 focused partial-class files by concern (Settings, Dashboard, Sorting, Filters, Cards, Attachments, Recurring, ArchiveDelete, ManagedLists, Import) for easier navigation and maintenance going forward

## 0.59.1 — 2026-08-22
- Add a Clear Dates button next to the Archived and Deleted task lists' From/To date range, to reset the filter back to showing everything

## 0.59.0 — 2026-08-22
- Add permanent delete to the Archived and Deleted task lists: right-click a task for Permanently Delete, with a confirmation prompt — this actually erases it (and any attachments still stored with it), unlike the regular Delete on the board
- Add a From/To date range filter to both the Archived and Deleted task lists, filtering by when each task was archived or deleted

## 0.58.1 — 2026-08-22
- Extend the previous fix to existing data: recurring tasks that were already completed (in Done or Archived) before that update are now retroactively marked as having already spawned their next occurrence, so reactivating one of them and marking it Done again won't spawn a duplicate either — closes the gap where only completions from that point forward were protected

## 0.58.0 — 2026-08-22
- Fix a duplicate-task bug: a recurring task that's completed, archived, then reactivated and marked Done again no longer spawns a second copy of its next occurrence — each task now only ever spawns its successor once

## 0.57.1 — 2026-08-22
- Fix dialogs/lists jumping slightly when hovering a button, text field, date picker, dropdown, or card: hover now only changes the border's color, never its thickness (the thicker border's space is reserved permanently instead), so nothing around it shifts

## 0.57.0 — 2026-08-22
- '+ File...' now also copies the chosen file into the task's Attachments folder (instead of linking to its original location), matching drag-and-drop — so it moves with the task into Done/Archived/Deleted and is unaffected by later changes to the original file
- Renamed the "Linked Files Default Path" setting to "Attach File Default Path" and rewrote its Settings/Help wording to describe copying, replacing a stale note left over from before file attachments were fully built out

## 0.56.0 — 2026-08-22
- Dragging a file onto the attachments area (or directly onto a card) now copies it into the Attachments folder right away, instead of just linking to its original location — so it's fully owned by the task from the start and actually moves into Done/Archived/Deleted when the task's status changes, matching what already happens for pasted screenshots

## 0.55.1 — 2026-08-22
- Fix buttons app-wide (including every button in the Add/Edit Task dialog) so hover actually highlights with a visible accent-colored border, not just a barely-noticeable opacity dim

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
