# Changelog

All notable changes to the Kanban Task Board app, by version. Newest first.

> Note on numbering: versions briefly reached 1.2.0 on 2026-08-14 before a deliberate decision to
> stay in 0.x while the app is still under active development; the next release after 1.2.0 was
> renumbered 0.7.0 and versioning has continued from there.

## 0.109.0 — 2026-09-21
- New: type to find a person. In the task screen, select the Who box and start typing a first name or surname; the list opens and jumps to that person. Press Space to tick them, and Enter or Esc to close the list. The Project, Priority and Who filter lists on the board also jump to a name as it is typed
- Improved: compact cards now keep the "Waiting on" line, so it stays clear why a task is not moving
- Fixed: adding a person, project, goal or flag under a name that already exists no longer creates a duplicate, which could then cause an error. The existing entry is used instead, and is made active again if it had been made inactive. Renaming an entry to a name already in use is refused
- Fixed: task files that already contain a duplicated name open and work normally; the spare entry can be removed from its Manage screen
- Fixed: double-clicking the text of a task in the Reminders list opens the task instead of showing an error

## 0.108.0 — 2026-09-21
- New: Compact buttons, in Settings. It makes the button column narrower and shorter, which suits a smaller screen: the buttons are lower, the large buttons in the lower half are brought down to the same size as the rest, and there is less padding
- The text stays the same size in compact mode, so nothing becomes harder to read. The From and To date boxes stack one above the other so that full dates still fit
- Compact buttons works alongside hiding the button column (Alt+B); use either or both

## 0.107.1 — 2026-09-21
- Fixed: when two PCs share one task file, each PC now runs its own daily check for a newer version. Previously a check made on one PC could stop the other from checking, so a PC that needed an update might never be told
- Fixed: on PCs that share a task file, the What's New screen is now shown once on each PC after it is updated, instead of reappearing when switching between PCs on different versions
- Improved: using Check for Updates in About no longer postpones the next automatic check

## 0.107.0 — 2026-09-21
- New: the button column can be hidden to give the board the full width. Click Hide at the top of the column, or press Alt+B; it shrinks to a thin strip at the side. Click the strip, or press Alt+B again, to bring it back
- While the buttons are hidden, the strip shows a note such as "3 hidden by filters" whenever filters are keeping tasks off the board
- Your choice is remembered between sessions, and every keyboard shortcut keeps working while the column is hidden
- Improved: each column heading now has a line beneath it, separating the heading from the tasks below. The heading stays in place while the tasks scroll

## 0.106.0 — 2026-09-20
- New: a Waiting On List screen for looking after the answers suggested in Waiting On. Open it from Manage list in the Waiting On prompt, from List beside the Waiting On box on the task screen, or by right-clicking the Waiting On button
- Add answers ahead of time, rename them, or delete the ones you no longer need. Each answer shows how many tasks are currently waiting on it
- Renaming an answer also rewords it on every task that uses it, so a spelling mistake can be corrected everywhere in one step
- Deleting an answer that tasks still use asks whether to clear it from those tasks too. Undo reverses any change made to tasks

## 0.105.0 — 2026-09-20
- New: Waiting On remembers your earlier answers. The Waiting On prompt lists your recent answers as soon as it opens, narrows the list as you type, and fills in the closest match ahead of the cursor
- Press Enter to save what is in the box. Keep typing to replace a suggestion, press Backspace to remove it, use the Down and Up arrows to move through the list, or click an answer to take it
- The Waiting On box on the task screen offers the same suggestions once you start typing
- Answers are remembered even after the task they belonged to is finished. To stop one being suggested, highlight it with the arrow keys and press Shift+Delete

## 0.104.2 — 2026-09-20
- Improved: in light mode, the panel behind the button column is now a soft blue-grey, so every button stands out clearly against it. Dark mode is unchanged

## 0.104.0 — 2026-09-20
- New: update notices. When a newer version is released, the app tells you what is new in it and offers a button that opens the download page. Nothing is downloaded or installed unless you choose to
- The check runs at most once a day when the app starts, and sends only the app's name and version number. It can be turned off in Settings, and About has a Check for Updates button for checking at any time
- Remind Me Later postpones the notice to the next day; Skip This Version hides it for that version only
- New: a Licence Agreement and a Privacy Note, both available from the About screen. The installer now shows the Licence Agreement before installing
- The Privacy Note confirms how the app has always worked: your tasks, notes and attachments are stored only on your own PC, with no account, no cloud storage and no usage tracking
- About now shows the support email address

## 0.103.0 — 2026-09-20
- New: shared tasks. A task can now be assigned to more than one person. Open the Who list on the task screen and tick everyone involved; the first person ticked is the lead, and "make lead" changes that at any time
- Cards list everyone assigned to a task, lead first. Clicking the Assigned line, or Assign To in the right-click menu, adds or removes one person without affecting the others
- The Who filter shows a shared task when any of its people is selected, and Sort by Who orders tasks by their lead
- Email This Task is addressed to everyone on the task who has an email address
- Reports grouped by Who list a shared task under each of its people, with a note naming the others it appears under. The Dashboard's Who chart counts shared tasks the same way
- Excel import accepts several names in the Who cell, separated by semicolons (for example: Sam Lee; Priya Patel). The task file attached to a task email lists everyone in the same format
- With several tasks selected, Assign To adds a person to all of them or removes that person from all of them, and Unassigned clears everyone
- Duplicated tasks, the next occurrence of a recurring task, and task templates all carry the full list of people. Undo covers every assignment change
- Existing tasks are unchanged: each keeps its current person, who becomes the lead
- Improved: a long list of assigned people now wraps onto a second line on the card instead of being cut off

## 0.102.0 — 2026-09-20
- Timeline: the window now remembers whether you left it in Day view or Week view
- The navy behind the button column and the header is now lighter in light mode and deeper in dark mode
- New safety check: the task file now records which version of the app has used it. If an older copy of the app opens a task file that a newer copy has been using (a second PC that hasn't been updated, say), it warns you before anything can be changed, and offers to close. It only warns when the newer version really does store something the older one doesn't know about

## 0.101.0 — 2026-09-20
- Timeline: the window now opens at the size you last left it, including full screen
- PDF reports no longer depend on a Windows font. If the usual one (Segoe UI) can't be read on a PC, the report is made in Lato, a free font now carried inside the app, instead of failing. Nothing changes on a PC where reports already worked
- About now credits the Lato font

## 0.100.1 — 2026-09-19
- The Template list now always shows at the top of a new task. With no templates yet, it says how to make one, and its Manage button is always there
- Right-click the New Task button to start a task straight from a template, or to open Manage Templates
- New shortcut: Alt+M opens Manage Templates from the main screen
- Timeline: hovering over a task now shows its due time, if it has one, and what it is waiting on

## 0.100.0 — 2026-09-19
- New: Quick Add from any program. While the app is running, press Ctrl+Alt+N anywhere to open a small New Task box, type the task and press Enter. The box stays open for more; Esc closes it
- Quick Add codes, each as its own word: !high !medium !low for priority, @sam for who, and /today /tomorrow /fri /+3 /10-15 for the due date. The line under the box shows how it was understood
- Quick Add remembers the project you used last. It can be switched off in Settings, which also tells you if another program already uses the key
- New: task templates. Fill in a task and click Save as Template (or right-click a task on the board), then pick it from the Template list at the top of a new task
- Templates keep the project, priority, who, goal, flags, sub-tasks, notes, website and recurring settings. Dates are kept as so many days from today
- Manage, beside the Template list, renames and deletes templates
- The board confirms a Quick Add with a short note, and Undo takes it back

## 0.99.0 — 2026-09-19
- New: Waiting On. When a task is held up, say who or what by (for example "Sam's quote") in the new Waiting On box on the task screen
- The card shows it in a highlighted line. Click the line to change it, or save it empty to clear it
- New Waiting On button beside No Due Date: shows every task that is waiting, across all columns, with a count
- Right-click a task, or a selection of tasks, and choose Waiting On to set or clear it without opening the task
- Moving a task into the Waiting column asks what it is waiting on (once, and only if it doesn't already say)
- Finishing a task clears what it was waiting on
- The Keyword filter searches it, and it is included in Copy as Text, task emails, reports, and Excel import and export. The import template has a new Waiting On column at the end; Waiting For or Blocked By work as headings too
- Undo covers it

## 0.98.1 — 2026-09-19
- Timeline: a task's box now sits at its start date, on the left, with the arrow running right to its due date. A task that started before the dates showing gets a dashed box in the first column
- The Today, Tomorrow and Within a Week buttons also show tasks that start on those days, even if they aren't due for a while. Tomorrow and Within a Week show them even while Hide Future is on
- Task screen: Start and Due are together on one line, with Recurring and the alert Time on the line below, all inside one box

## 0.98.0 — 2026-09-19
- Timeline: a task with a start date now has an arrow running along its row from the start date to its box at the due date, so you can see how long each task has and what overlaps. A dot marks the start
- A task that started before the dates showing has its arrow come in from the left edge
- A task due after the dates showing is drawn as a dashed box where it starts, says when it is due, and its arrow runs off the right-hand side
- Tasks that overlap are stacked on separate lines within their project so arrows never cross a box
- A task with a start date but no due date now appears on the Timeline, at its start date
- The printed Timeline shows the same arrows
- Works in week view and day view, and in the light and dark themes

## 0.97.0 — 2026-09-19
- New: a Start date on tasks, for work that can't begin until a certain day. It sits beside the due time on the task screen and is optional
- While the start date is still ahead, the card says when it starts
- New Hide Future button (beside Undo): keeps tasks off the board until their start date arrives, so you only see what you can work on now. While it is on, the button is outlined and shows how many tasks are hidden
- Hide Future is remembered between sessions, and Clear Filters leaves it alone. Finished tasks are never hidden by it
- The start date can't be after the due date: the task screen says so, and moving a due date earlier on the card pulls the start date back with it
- Recurring tasks keep the same gap between start and due on each new occurrence
- The start date is included in Copy as Text, task emails, reports, the Timeline (shown as "Oct 3 to Oct 20"), and Excel import and export. The import template has a new Start Date column at the end, and your own spreadsheet can head it Start or Not Before
- Undo covers the start date too

## 0.96.0 — 2026-09-19
- New: Undo. Press Ctrl+Z, or click the new Undo button beside New Task, to take back the last thing you did to a task
- It covers adding, editing, moving, dragging into a new order, duplicating, deleting, Archive Done, importing, and the quick changes on a card. A change to several selected tasks is undone in one go
- Hover over the Undo button to see what it will take back. A short note at the foot of the board confirms what was undone
- Undoing the completion of a recurring task also removes the next occurrence it created, and attachment files move back with the task
- The last 30 actions are remembered until you close the app. There is no redo
- Not covered: the Project, Goal, Flag and Who lists, Settings, adding or removing attachments, and the View Archived and View Deleted screens
- The delete prompts now say you can take a delete back with Undo

## 0.95.0 — 2026-09-19
- Right-click works on a whole selection: pick several tasks (Ctrl+click or Shift+click), right-click any of them, and the menu acts on all of them at once: Move All To, Priority, Assign To, Project, Add Flag, Duplicate All, and Delete
- Copy All as Text copies every selected task in full, and Copy Titles copies just the titles, one per line
- Deleting several tasks at once always asks first. If any of them repeat, it asks once whether to keep their series going
- A tick in the menu means every selected task already has that value
- Right-clicking a task outside the selection clears the selection and opens the usual one-task menu
- The one-task right-click menu also has Project now

## 0.94.0 — 2026-09-19
- Softer colours on the left-hand panel: the buttons and filter labels are toned down to be easier on the eyes, and the navy background (also behind the top banner) is a little lighter

## 0.93.0 — 2026-09-19
- Settings has a Cancel button: it puts every setting back to how it was when you opened Settings (Esc and the X do the same), and asks first if you changed anything. Save & Close keeps your changes
- Importing from Excel now works with your own spreadsheets: the headings can be on any row and in any column, and the task column can be headed Title, Task, or Task Details. Before, nothing imported unless the first column was headed Title
- Click the coloured Project, Who, Goal, or Flag label beside its filter to open that Manage screen
- The square buttons on the main screen have slightly rounded corners, matching the Goal, Flag and Keyword labels

## 0.92.0 — 2026-09-17
- Resize the filter lists: drag the small bar under the Project list, or under Priority and Who, to make them taller or shorter. Priority and Who resize together
- Double-click a bar to put its list back to the standard height. The app remembers the heights you choose

## 0.91.0 — 2026-09-17
- New Report a Problem button in Help and About: it opens an email to support with the app's error log attached, so you can tell us what went wrong. Nothing is sent until you click Send
- When the app hits an unexpected error, it now offers to open that problem report for you
- For your safety, a task's Website field now accepts only web links and email addresses. Links to files, folders or programs are refused, including in task files shared by someone else
- An email address in the Website field now opens a new email
- The Project filter list is now the same height as Priority and Who, and the Goal and Flag filters match the Keyword box

## 0.90.0 — 2026-09-17
- Move several tasks at once: Ctrl+click (or Shift+click for a range) to pick them, then drag any one of them to another column, or to a new spot in their own column
- Selected tasks are highlighted, the number picked shows at the top of the board, and Esc clears the selection
- The Project filter list is twice as tall, and Priority and Who now sit side by side, each twice as tall as before, so you can see more of each without scrolling

## 0.89.0 — 2026-09-17
- Much faster on big boards: the board now only draws the tasks you can see, so with thousands of tasks it opens in under a second instead of 20+ seconds and uses a fraction of the memory
- Filtering, sorting and resizing the window stay quick however many tasks you have
- Fixed a long one-time freeze on big boards the first time another window (such as the Timeline) was opened
- Dragging tasks to reorder them works as before, including while a filter is on

## 0.88.1 — 2026-09-17
- The Archived Tasks list, and reports that include archived tasks, open in a fraction of a second on large boards — with 6,000 archived tasks they previously took about 16 seconds

## 0.88.0 — 2026-09-16
- Right-click a task on the board for a menu of everything you can do with it: Edit, Copy as Text, Copy Title, Duplicate, Move To, Priority, Assign To, Add Flag, Open Website, Email Task, and Delete
- Copy as Text puts the whole task on the clipboard — status, project, priority, due date, who it's assigned to, goal, flags, notes, and sub-tasks — ready to paste anywhere
- Duplicate makes a copy of a task in the same column with all its details and its sub-tasks unticked (attached files aren't copied)

## 0.87.0 — 2026-09-16
- The Edit Task dialog now shows when the task was completed and when it was last updated, beside the ? button at the bottom
- The Help screen has a What's New button, next to About, for seeing what changed in recent updates

## 0.86.0 — 2026-09-15
- Tasks now record when they were completed. Cards in Done show "Completed" with the date and time instead of "Updated", and editing a finished task no longer changes it
- The Archived list shows when each task was completed as well as when it was archived
- Report Builder shows the completion date on finished tasks and can sort by Completed Date
- Tasks you'd already finished get their completion time filled in from their history

## 0.85.0 — 2026-09-15
- Task Due Now alerts have a Snooze button: be reminded again in 15 minutes, 1 hour, 2 hours, 4 hours, or 1 day
- Fixed the Close button (and Esc) doing nothing on a Task Due Now alert

## 0.84.1 — 2026-09-14
- The Imported Tasks review window now wraps long task descriptions instead of stretching past the edge of the screen, so the Imported checkboxes are always visible
- Fixed Save Changes in Imported Tasks clearing each task's due time, website link, and "force edit upon completion" setting
- Fixed editing a task from the Timeline clearing its due time

## 0.84.0 — 2026-09-14
- Email This Task now works without classic Outlook. With the new Outlook, another email app, or no Outlook at all, the email opens in your default email app, and a folder with the task's Excel file and attachments opens beside it so you can drag them in
- Task emails now include the due time, not just the date
- Help has new topics for emailing tasks, keeping separate task files, and backups, and now covers Timeline's Day view, colours and printing, and Report Builder's saved views, sorting and page orientation

## 0.83.2 — 2026-09-14
- AM and PM buttons beside the due Time. The highlighted one shows how the time will be saved; click to switch
- A time typed without AM or PM (e.g. 2:30) now follows the button you clicked, or if you haven't clicked one, is read as a working hour (7 to 11 AM, 12 to 6 PM) instead of always being AM
- The Time box also accepts 2:30p, 7a, and p.m.

## 0.83.1 — 2026-09-14
- The Time field in Add/Edit Task now shows a grey "e.g. 2:30 PM" hint while it's empty, like the date picker's "Select a date"

## 0.83.0 — 2026-09-13
- Tasks can now have an optional due Time alongside the Due Date (typed like 2:30 PM, 2pm, or 14:30), shown on the card
- While the app is running, a Task Due Now alert pops up with a sound when a task's due time arrives, even if the board is minimised. Check the task off to mark it Done, or double-click to open it
- Each task alerts once for its time; moving the time later makes it alert again. Recurring tasks carry their time forward
- New Settings option to turn the due-time alerts off

## 0.82.1 — 2026-09-13
- Rebalanced Settings: moved Backups to the left column (alongside Behavior) so both columns run about the same length instead of the right column towering over the left
- Fixed Help buttons to scroll their topic to the top of the Help screen instead of just barely into view at the bottom

## 0.82.0 — 2026-09-13
- Timeline task boxes are now colour-coded by priority (High/Medium/Normal/Low), both on screen and when printed, using the same colours as the priority badge on the board
- Add/Edit Task, Report Builder, Timeline, and Settings each now have a Help (?) button that opens Help scrolled straight to that topic

## 0.81.0 — 2026-09-13
- The "delete a recurring task" prompt now shows plain, clearly labeled buttons (Delete This Occurrence, Keep Series Going / Delete This Occurrence, End Series / Cancel) instead of a generic Yes/No/Cancel dialog whose meaning wasn't obvious
- Settings now has an About button at the bottom, next to Save & Close
- Fixed a stray blue drag-and-drop insertion line sometimes staying visible on the board after finishing a drag

## 0.80.0 — 2026-09-09
- The app now automatically backs up your task file each time it closes, keeping the most recent 20 by default. New Settings > Backups section: turn it off, change how many to keep, back up on demand, or open the backups folder directly

## 0.79.0 — 2026-09-09
- Settings > Data Storage can now open or switch to a different task file entirely - like separate company files in accounting software, each is a fully independent board with its own tasks, columns, and settings. Open Existing File... or New File... to pick one, or quick-switch from a remembered list of recent files. Switching restarts the app; nothing about the file you're leaving is touched or deleted

## 0.78.0 — 2026-09-09
- New Semi-Monthly recurring option, for tasks that repeat twice a month. It holds two fixed dates rather than repeating every 15 days, so it doesn't drift through the calendar the way Bi-Weekly does: a task due on the 3rd repeats on the 3rd and the 18th, one due on the 20th repeats on the 5th and the 20th
- Due dates from the 13th to the 15th and from the 28th to the 31st settle onto the 15th and the last day of the month — the conventional twice-monthly pairing, and the only one that holds its dates through February. Dates from the 1st to the 12th keep their own pair permanently

## 0.77.0 — 2026-09-08
- Email This Task now also attaches a one-row Excel file with the task's fields, so a recipient who runs Kanban Task Board can pull it straight into their own board via Import Tasks instead of retyping it

## 0.76.1 — 2026-09-07
- The Today / Tomorrow / Within a Week / No Due Date buttons are no longer cumulative: each now clears every other filter first — including a saved Alt+0-9 filter that was applied — so you always get that due date across the whole board rather than within whatever was already narrowed down. Alt+T already worked this way and is unchanged

## 0.76.0 — 2026-09-05
- Pressing Escape (or Cancel, or the window's X) after entering or changing anything in the Add/Edit Task dialog now asks to confirm before discarding it, instead of closing silently. Closing an untouched dialog, or one you've just saved, still closes straight away
- The same confirmation now guards the imported-tasks review grid, where Close previously discarded any edits made since the last Save Changes, and the small "add a new project/goal/flag/person" name prompt

## 0.75.0 — 2026-09-04
- Task Details/Notes spell check now accepts both Canadian and American spellings (colour/color, centre/center, travelled/traveled, etc.) instead of flagging one dialect as misspelled

## 0.74.0 — 2026-09-04
- Task Details/Notes spell check now uses Canadian English (was US English)
- Timeline print now draws light vertical lines dividing the date/week columns

## 0.73.0 — 2026-09-03
- Email This Task now signs off with Outlook's own default signature when one is configured (captured by displaying the compose window before setting the body, then splicing our content in ahead of it), falling back to a simple signature built from new Name/Title/Email/Phone fields under Settings > Your Details when Outlook has none. Any of the four fields can be left blank

## 0.72.0 — 2026-09-03
- Add saved report views to Report Builder: name and save the full set of fields (columns, filters, sort/group, scope, orientation, Notes/Sub-tasks toggles) and reload it later from a dropdown next to Load/Save View/Delete View
- Report Builder: the Done column is now unchecked by default when opening the window
- Report Builder: the Parameters summary moves into the header band, right-aligned and level with "Generated...", in a larger font, instead of a separate line below it
- Hover the sidebar's Custom Filters button to preview every saved Alt+0-9 slot (name and summary) without opening the manage dialog

## 0.71.5 — 2026-09-02
- Internal review and refactor, no functional change: saving a task's flags, sub-tasks, and attachments now writes each set in a single transaction with one reused statement instead of committing every row separately, which was the main cost of saving a task
- Filtering the board no longer rebuilds the selected Project/Priority/Who lists once per card, so typing in the Keyword box no longer allocates thousands of throwaway lists per keystroke
- Editing a card no longer rewrites the sort order of every card on the board when nothing actually moved
- Removed the repeated twelve-argument save call and the repeated post-change refresh from every card operation, so a newly added field can no longer be missed on one path

## 0.71.4 — 2026-09-02
- Give the From/To due-date range the same colour-filled label boxes as the rest of the filters, moving their labels off their own line and reclaiming another row
- Colour the Clear Filters (orange) and Custom Filters (indigo) buttons instead of leaving them plain grey

## 0.71.3 — 2026-09-02
- Tighten the sidebar's filter block: each filter's label now sits in a colour-filled box beside its control instead of on its own line above it, reclaiming six rows of vertical space. Project, Priority, and Who are rotated into a narrow spine alongside their lists; Goal, Flag, and Keyword stay horizontal

## 0.71.2 — 2026-09-02
- Widen the sidebar buttons (66px to 72px, sidebar 344px) so Manage Projects and Reminders stop looking squished
- Remove the "Sort & Add", "Filter", and "Due" section labels from the sidebar — self-evident from the buttons themselves — and tighten the spacing now that they're gone
- Drop the "(Ctrl/Shift-click for multiple)" wording from the board's Project/Priority/Who filter labels and tooltips
- Move the version number and copyright line from the bottom of the sidebar to the top-right of the header, under the Test Build badge

## 0.71.1 — 2026-09-02
- Move the Due quick-filter buttons and date range to the top of the sidebar Filter section, above Project
- Move Settings and Help into the same row as Close, and Reminders and dark/light mode toggle into the same row as Report Builder and Timeline
- Shrink Clear Filters and Custom Filters to half width, side by side, instead of stacked full-width

## 0.71.0 — 2026-09-02
- Align the four Manage buttons (Projects/Goals/Flags/Who) into a single row
- Add printing to the Timeline: a Print button opens the same preview/print window Report Builder uses, rendering the currently visible range and zoom level as a paginated landscape document
- Project, Priority, and Who filters — on both the board and Report Builder — are now multi-select: Ctrl-click or Shift-click to pick more than one, matching any of the selected values. Saved Alt+0-9 custom filter slots and the remembered last-session filters upgrade automatically; older single-value slots keep working

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
