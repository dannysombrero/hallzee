# Feature Design: Classroom Policies & Bell Schedules

## Workspace transfer

**Export Workspace** saves current edits and writes a versioned
`Hallzee Workspace` JSON package containing the workspace name, policy rule,
bell schedule templates with class sections, and date exceptions. Student
rosters, trip history, terminal assignments, and owner credentials are omitted.
**Import Workspace** validates the format/version, rule values, times, days,
and exceptions, then writes a new workspace in one SQLite transaction. Profile,
rule, schedule, and exception identifiers are regenerated. Existing workspaces
are preserved; the imported workspace is selected for review. Applying settings
to connected hardware remains the **Save Workspace Settings** action.

The mini window displays **WINDOW OPEN** in green for allowed/Warn windows,
**WINDOW CLOSED** for Lock windows or no active period, and **PASS IN USE** in
orange whenever a pass is occupied. A zero-minute window is disabled and
intersecting windows use the stricter action.

**Status:** Implemented; physical kiosk validation remains

**Target Milestone:** Delivered
**Related Issues:** #20, #21  
**Scope:** Pass capacity limits, duration warning thresholds, 10/10 lockout rules, bell schedule timetable engine, and offline enforcement boundaries.

---

## 1. Problem Statement & User Behavior

Classrooms operate under structural time constraints and safety rules:
1. **Capacity Limits:** Only a specified number of students (typically 1) may be out at any given time.
2. **Duration Limits:** If a student is out longer than an acceptable threshold (e.g. 7 minutes), the teacher needs an immediate visual indicator.
3. **Daily Frequency Caps:** Flag students who exceed daily checkout limits (e.g., more than 2 passes per day).
4. **10/10 Lockout Rule:** School policy frequently disallows hallway passes during the first 10 minutes and last 10 minutes of each class period.
5. **Bell Schedules:** Class periods change according to standard or modified (assembly/early release) daily schedules.

---

## 2. Authority & Enforcement Architecture

To guarantee reliability when the teacher's PC is asleep or disconnected, policies are bifurcated into **Hardware-Enforced (Kiosk)** and **Client-Enforced (Desktop)** rules:

| Policy Rule | Enforcement Location | Offline Behavior | Rationale |
| :--- | :--- | :--- | :--- |
| **Pass Capacity (Single Active Pass)** | **Kiosk Terminal** | Fully Enforced | Physical keypad blocks second student checkout while occupied (`ALREADY OCCUPIED`). |
| **Pass Duration Warning** | **Desktop Client** | Visual Alert on PC | Alerting is for the teacher's awareness on the desktop dashboard. |
| **Daily Pass Limit per Student** | **Desktop Client** | History Audit | Dashboard flags students who reach the teacher's daily guideline; kiosk LittleFS remains lightweight. |
| **10/10 Period Windows** | **Desktop and optional kiosk copy** | Desktop guidance; kiosk fails open outside its cache | Terminal enforcement is teacher-enabled and off by default. Enabled policies copy a resolved 14-day window cache to the kiosk. |
| **Bell Schedule Transitions** | **Desktop Client** | Dashboard and export context | Date exceptions select named templates and the matched class section/schedule is retained on the trip. Bell times never switch teacher workspaces. |

---

## 3. Data Model & Schema

```sql
-- 1. Classroom Policy Configuration (Per Profile)
CREATE TABLE IF NOT EXISTS policy_rules (
    rule_id TEXT PRIMARY KEY,
    profile_id TEXT NOT NULL,
    max_simultaneous_passes INTEGER DEFAULT 1,
    duration_warning_seconds INTEGER DEFAULT 420,  -- 7 minutes
    max_daily_passes_per_student INTEGER DEFAULT 2,
    lockout_start_minutes INTEGER DEFAULT 10,       -- First 10 mins of class
    lockout_end_minutes INTEGER DEFAULT 10,         -- Last 10 mins of class
    first_window_action TEXT NOT NULL DEFAULT 'Warn',
    last_window_action TEXT NOT NULL DEFAULT 'Warn',
    alert_sound TEXT NOT NULL DEFAULT 'Chime',
    terminal_enforcement_enabled INTEGER NOT NULL DEFAULT 0,
    FOREIGN KEY (profile_id) REFERENCES profiles(profile_id) ON DELETE CASCADE
);

-- 2. Bell Schedule Periods
CREATE TABLE IF NOT EXISTS bell_schedules (
    schedule_id TEXT PRIMARY KEY,
    profile_id TEXT NOT NULL,
    period_name TEXT NOT NULL,                      -- e.g. "Period 1"
    start_time TEXT NOT NULL,                       -- "08:30" (HH:MM 24hr)
    end_time TEXT NOT NULL,                         -- "09:25" (HH:MM 24hr)
    days_of_week TEXT NOT NULL DEFAULT '1,2,3,4,5', -- Mon-Fri
    schedule_name TEXT NOT NULL DEFAULT 'Regular',
    class_section TEXT NOT NULL DEFAULT '',
    FOREIGN KEY (profile_id) REFERENCES profiles(profile_id) ON DELETE CASCADE
);
```

---

## 4. Policy Engine Evaluation Logic

`PolicyScheduleService` first applies a date exception. A no-school exception
resolves no period; another exception selects its named template. Otherwise it
uses each template period's weekday membership (for example, `Regular` on
Monday/Tuesday/Thursday/Friday and `Wednesday` on Wednesday). Within the matched period,
first/last windows independently evaluate `Allow`, `Warn`, or `Lock`, with
`Lock` winning if the windows overlap. The dashboard separately groups today's
trips by student and flags counts at or above the saved daily guideline.

---

## 5. Verification & Testing Matrix

| Capability | macOS Testing | Windows Testing | Hardware Required |
| :--- | :--- | :--- | :--- |
| Policy evaluation & 10/10 calculation unit tests | **Sufficient** | Optional | No |
| Bell schedule time-window matching tests | **Sufficient** | Optional | No |
| Desktop UI policy alerts & warnings | **Sufficient** | Optional | No |
| Physical kiosk single-pass rejection test | **Not Usable** | **Required** | **Yes (ESP32 Kiosk)** |

---

## Implemented bell-aware terminal policies and period tracking

The client stores and edits policies, named bell templates, period-to-class
section mappings, and date-specific overrides. It evaluates the selected
first/last-window action, records matched schedule/class context on new trips,
and includes that context in enriched CSV exports.

### Teacher workspace decision

A profile represents a teacher workspace, not an individual class. A teacher
normally has one workspace for a room; schedules identify the active class
section inside it. Therefore this feature must not implement automatic profile
switching. `roster_enrollments` stores one student's membership in multiple
class sections beneath the existing teacher-workspace storage.

### Teacher schedule setup

- Teachers create named schedule templates, such as `Regular`,
  `Wednesday`, `Block A`, and `Block B`.
- Each template contains ordered periods with a name, class section, start time,
  and end time.
- Teachers assign regular periods to weekdays and add
  date-specific exceptions for assemblies, early-release days, testing, or
  other one-off schedules. A Wednesday schedule therefore does not need to
  match Monday, Tuesday, Thursday, or Friday.
- The client shows the active schedule and period so the teacher can
  confirm what the terminal will use before the school day begins.

### First/last-ten-minute policy

For every period, teachers choose an action for the first and
last protected windows, with independently configurable window lengths. The
terminal-enforcement switch is **off by default**. When it is off, these values
remain desktop guidance and do not restrict kiosk checkout.

| Option | Terminal behavior |
| --- | --- |
| Allow | Accept checkouts normally. |
| Warning | Accept the checkout and show a visual warning. |
| Lock | Refuse new checkouts until the protected time window ends. |

The Policies/Bell Times screen offers a bundled desktop sound library with a
preview control, plus `No sound`. Current kiosk hardware has no speaker, so its
warning is visual.

When a teacher enables terminal enforcement, the desktop resolves and sends a
14-day dated window cache. The ESP32 commits the staged cache atomically,
persists it, evaluates it against its clock, and explains visual warning and
lockout results. A date outside the cache fails open.

### Period metadata and export

When a checkout occurs during a matched period, the client records the schedule
template and class section active at checkout. CSV export includes
`schedule_name` and `class_section`; blank values remain
valid for unscheduled time, missing bell data, or legacy trips. This makes it
possible to report the period in which a student left without making roster
period data mandatory.

### Remaining physical verification

| Capability | macOS Testing | Windows Testing | Hardware Required |
| :--- | :--- | :--- | :--- |
| Schedule-template, day assignment, and exception matching | **Sufficient** | Optional | No |
| First/last-window evaluation and period metadata export | **Sufficient** | Optional | No |
| Policy editor and sound preview | **Sufficient** | Optional | No |
| Offline ESP32 visual warning and lockout enforcement | **Sufficient for firmware compile/unit tests; physical screen not yet verified** | Optional | **Yes (ESP32 Kiosk)** |
