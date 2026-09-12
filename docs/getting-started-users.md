# Hallzee: the teacher guide

Hallzee keeps track of classroom hall passes. Students use the terminal by the
door; you use the app to see who is out, manage your roster, and review trips.
Day-to-day use does not need internet access. Downloads and update checks do.

## Download Hallzee

Open [desktop downloads](https://github.com/dannysombrero/hallzee/releases?q=client-v&expanded=true)
and choose the newest numbered **Client** release. The release notes explain
what changed. Download the file for your computer under **Assets**:

| Your computer | Download | Install |
| --- | --- | --- |
| Windows 10/11, 64-bit Intel/AMD | `Hallzee-v[version]-Windows-win-x64.zip` | Extract the entire ZIP into a folder, then open `Hallzee.exe`. |
| Mac with Apple silicon (M1 or later) | `Hallzee-v[version]-Mac-osx-arm64.zip` | Open the ZIP, drag `Hallzee.app` to Applications, then open it. |
| Mac with an Intel processor | `Hallzee-v[version]-Mac-osx-x64.zip` | Open the ZIP, drag `Hallzee.app` to Applications, then open it. |

On a Mac, **Apple menu → About This Mac** shows the chip or processor.
You do not need Git, .NET, Arduino software, or a GitHub account.
If no numbered release is listed yet, the public download is not ready.

School computers may require IT approval to install apps or allow Bluetooth.
The current packages are not publisher-signed/notarized; ask school IT to
approve them if Windows or macOS blocks opening them. Do not turn off your
computer's security protections. Allow Hallzee to use Bluetooth when prompted.

## Connect your terminal

Have your powered terminal nearby and turn on your computer's Bluetooth.

1. With no student checked out, hold `*` and `#` together on the terminal for
   **five seconds**, then release. Its screen shows a name, unique ID, and
   six-digit pairing code.
2. In Hallzee, choose **Find Nearby Terminals** and select the matching name.
   Enter its code in Hallzee's pairing dialog. With current firmware, no
   Bluetooth passkey entry is needed; the OS may ask permission to pair.
3. Choose **Sync Now**. Hallzee sets the terminal clock and retrieves saved trips.

You can start pairing even if the terminal is asking you to set its date and
time. The same app remembers ownership and reconnects after app or terminal
restart without a code or pairing mode. If necessary, click **Reconnect** and
select the saved terminal. Hallzee labels known terminals **Currently Paired**,
**Not Paired**, or **Paired to other device**; those states are separate from
student occupancy. Bluetooth names remain stable, such as `Hallzee-2A58`.
Install the current terminal firmware as well as the client for this flow.

## Set up your classroom once

- **Settings → Teacher:** enter your name, school, and room; select
  **Save Classroom Info**.
- **Student Roster:** add student IDs and names, or import a roster using the
  import dialog. Keep leading zeros in IDs. A roster makes names appear in the
  app; students can still enter their numeric IDs at the terminal.
- **Policies & Bell Times:** use one workspace for your classroom, with class
  sections and periods inside it. Set pass capacity and your time warning,
  enter bell times, and choose **Save Workspace Settings** while connected.
- **Settings → Device:** use the pencil to name your terminal (for example,
  `Room #204`). Names may contain 1–24 characters except commas. Set the maximum
  ID length if your school needs a different limit.

Bell-time rules guide pass availability during scheduled class periods across the
desktop dashboard, Mini Window, and optionally on the terminal:

- **Pass Policy Modes:**
  - **Windows (Default):** Define behavior for the beginning (first X minutes), middle
    (instruction time), and end (last Y minutes) of class. Each window can be set
    independently to **Allow**, **Warn**, or **Lock**. Quick presets include
    *10/10 Lockout* (locks start & end, allows middle), *Start & End Only* (allows start & end,
    locks middle), and *Warning Windows* (warns during start & end).
  - **No Passes:** Completely locks pass checkout for the duration of the class period.
  - **No Rules:** Keeps passes open throughout the entire period without bell restrictions.
- **Master Toggle:** Select **Disable bell-time window rules completely** to bypass all
  window restrictions while keeping class period definitions intact.
- **Bell Transition Time:** Check **Enable bell transition time (between periods)** to
  display passing periods between classes as "Transition Time" in purple on the Mini
  Window, complete with hover tooltips showing remaining countdown and upcoming class.
- **Terminal Enforcement:** To also block checkouts at the physical terminal kiosk during
  locked windows, check **Enforce bell-time lockouts on the terminal** (off by default).
  Students already out can always check back in. Sync regularly: the terminal receives
  14 days of bell-time rules.
- **Warning Sounds:** Choose from 8 built-in synthesized alert sounds (*Chime*, *Bell*,
  *Soft alert*, *Marimba*, *Subtle Ping*, *Digital Watch*, *Gentle Knock*, *Harp Ascend*).
  Enable or mute warning audio, adjust playback volume (10–100%), and use **Preview** to
  audition the selected alert. Duration and daily-use warnings help you review
  activity; they do not automatically return students or enforce daily quotas at the terminal.

## During class

**Going out:** the student types their ID and presses `#`.
**Coming back:** they type the same ID and press `#` again.
Press `*` to clear an ID before submitting it.

Leave Hallzee open and connected for live status. Completed trips sync
automatically; **Sync Now** retrieves any missed records immediately.
If Bluetooth disconnects, the terminal still records trips. Live information
on your computer may be out of date until it reconnects and syncs.

| What you want to do | Where to go |
| --- | --- |
| See who is out and for how long | Dashboard |
| Keep pass status visible over other apps | **Mini Window** |
| Find a previous trip or filter dates/students | **Trip History Log** |
| Save a report | **Export** in the dashboard or history view |
| Check in a student who forgot | **Check In** beside their active pass; reconnect first for a terminal pass |
| Record a teacher-started pass | **Start Pass** on the dashboard; use its **Check In** button when the student returns |
| Get these instructions | **Help** in the sidebar |
| Look for a new app or terminal version | **Updates** in the sidebar |

**Teacher-started passes** are saved immediately on this computer, including
the original departure time, name/ID, period, destination, and purpose. They
survive sync, disconnects, and restarting Hallzee. Switch back to the workspace
where you started the pass to finish it. You can check it in while offline;
Hallzee saves one completed history record even if you retry. A failed save
leaves the pass available to retry.

A teacher-started pass does not reserve a slot on the physical terminal. If both
have an active pass, the teacher-started pass stays in the main card and terminal
passes appear alongside it. Each has its own **Check In** action, even when the
student IDs match.

Reports contain student information. Save and share them using your school's
approved practices. Your roster and trip history stay on this computer.
**Export Workspace** shares rules and schedules only; it is not a backup of
students, trip history, or pairing. Updating the app keeps its existing local
data, even when the new Windows ZIP is extracted into a different folder or a
Mac app is replaced. Hallzee imports roster rows into its private local database;
it does not depend on the original CSV after import.

The local database contains rosters, trip history, workspaces, policies, bell
times, and terminal details. Its location is:

- Windows: `%LOCALAPPDATA%\Hallzee\universal\hallzee-trips.db`
- Mac: `~/Library/Application Support/Hallzee/universal/hallzee-trips.db`

Do not put this database in a public or shared folder because it contains student
information. Moving Hallzee to another computer or making a complete backup is
a separate school IT task; workspace export deliberately leaves roster data out.

## Update the desktop app

1. Select **Updates** (also available in **Settings → Device**).
2. If a new desktop version is available, choose **Open desktop release** and
   download the ZIP for your computer.
3. Quit Hallzee. On Windows, extract into a new folder and open the new app.
   On Mac, replace Hallzee in Applications. Open Hallzee and let it reconnect.

The version in the top bar shows the app you are running. Checking for updates
does not install a desktop update automatically. Checks are manual; Hallzee
does not currently check on startup. If you are offline, continue using the
installed app and check later.

## Update the terminal firmware

Firmware is the software running on the terminal by the door.
[Latest firmware releases and USB setup downloads](https://github.com/dannysombrero/hallzee/releases?q=firmware-v&expanded=true)
are separate from desktop releases. Choose the newest numbered **Firmware**
release and read its notes.

For a terminal that supports Bluetooth updates:

1. Connect to the terminal. In **Settings → Device**, select **Refresh version**.
2. Check every student back in. Keep the terminal powered and the computer nearby.
3. Select **Check for software updates**, then **Download & Install firmware**
   when a compatible update is offered.
4. Leave Hallzee open until it confirms **Update complete** after reconnecting.

An update is designed to keep the terminal's name, pairing, settings, and trips.
If the result is not confirmed, reconnect and refresh its version before trying
again. A failed check does not mean you are up to date.

For an offline update, download the `.hallzee-fw` asset on another computer,
copy it over, then choose **Install firmware from file…**. Review the version
and notes before selecting **Install update**. The app checks the package's
signature and chooses the image matching your terminal.

If the app says **One-time USB setup required**, ask the person who supplied
your terminal or school IT to perform that setup. The firmware release includes
Windows and Apple-silicon Mac USB bundles with their own README and backup
instructions. This is a one-time preparation, not a normal teacher update.
Do not erase or factory-reset the terminal to solve an update problem.

## When something isn't working

| Problem | Try this |
| --- | --- |
| Terminal isn't listed | Check power and Bluetooth, move closer, and choose **Scan Again**. |
| Terminal belongs to another computer | Ask its current teacher/IT contact to release it. Pairing is not a data handoff. |
| Connection dropped | Allow the reconnect countdown to finish, then use **Reconnect** or **Find Terminal**. |
| History looks incomplete | Reconnect and select **Sync Now**. Check your history filters and selected workspace. |
| A name is missing | Check that the exact student ID exists in the selected workspace's roster. |
| Updates cannot be checked | Check your internet connection or try later. School networks may block GitHub downloads. |
| A firmware button is unavailable | Connect, refresh its version, finish syncing, and check in all active passes. |

Use **Disconnect & Unpair** only when intentionally releasing a terminal.
It requires a connection and no active passes, and the next pairing needs a
new code. It preserves trips and settings; school IT should handle reassignment
between teachers so previous classroom records are not mixed with new ones.
