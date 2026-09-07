# Feature Design: Student Roster Import & ID Enrichment

**Status:** Implemented

**Target Milestone:** Delivered

**Related Issues:** #17  
**Scope:** Roster CSV parser, column mapping, teacher-workspace storage, display enrichment, and student-ID fallback.

---

## 1. Problem Statement & User Behavior

The CSV preview, column auto-detection/mapping, profile-scoped upsert, import
reporting, and local roster enrichment described below are implemented in the
shared SQLite core and Universal client. Workbook files remain intentionally
out of scope: export them to CSV before import.

The storage schema still calls a teacher workspace a `profile`. Students are
scoped to that workspace, while `roster_enrollments` allows one student to
belong to multiple class sections. Bell periods select and record a class
section without switching teacher workspaces.

When students use the Hallzee kiosk, they enter only their numeric student ID (e.g. `10482`). In the raw sync database, records only contain this number. Teachers need immediate visual recognition of student names on their dashboard and in historical trip logs (e.g., displaying `Alex Rivera (Period 2)` instead of just `10482`).

Teachers will import class rosters directly from standard Student Information System (SIS) CSV exports (PowerSchool, Infinite Campus, Canvas, Google Classroom).
Apple Numbers and Excel workbooks must first be exported as `.csv` files; roster
import intentionally reads CSV text rather than spreadsheet-workbook internals.

### User Flow & Two-Phase Import Architecture
1. Teacher opens **Roster Management** from the desktop sidebar or settings.
2. Clicks **Import Roster (CSV)** and selects a CSV file.
   - **Dialog Concurrency & Yielding:** The client yields to the UI message loop (`Task.Yield()`) before invoking the native file dialog to ensure clean pointer capture release on Windows, guards against re-entrant multi-clicks, and yields (`Task.Delay(100)`) after dialog return so the native COM window handle detaches cleanly.
   - **Shared Read Streams:** The file stream is opened with `FileShare.ReadWrite` to allow seamless import even if the CSV file is actively open in Microsoft Excel or another viewer on Windows.
3. **Phase 1: Preview & Column Auto-Detection (`AnalyzeRosterCsv`)**:
   - The engine offloads CSV reading and tokenization to a background task (`Task.Run`), keeping the desktop UI completely responsive even on large district rosters (thousands of rows).
   - Extracts sample preview rows (e.g. first 3–5 rows) and total row count.
   - Auto-detects suggested column mappings for `Student ID`, `First Name`, `Last Name`, `Full Name`, `Grade`, and `Period` using SIS synonyms (PowerSchool, Infinite Campus, Skyward, Google Classroom, Canvas).
   - Displays a preview table showing sample values under each header so the teacher can verify the suggested mapping.
4. **Phase 2: Confirmed Import (`ImportRosterWithMapping`)**:
   - Teacher reviews the sample data, adjusts any column dropdowns if needed, and confirms the import.
   - The engine validates rows, splits names if full name format is selected (`"Last, First"` or `"First Last"`), and batch upserts valid students into `roster_students` scoped to the profile.
   - Surfaces an import report summary with total rows, imported count, skipped count, and row-level error diagnostics.
5. The dashboard and trip tables immediately enrich matching student IDs with full names and periods.


---

## 2. Data Model & Storage Changes

Rosters are stored in the local SQLite database scoped to a teacher workspace
(currently named `profile` in the schema):

```sql
CREATE TABLE IF NOT EXISTS roster_students (
    student_id TEXT NOT NULL,
    profile_id TEXT NOT NULL,
    first_name TEXT NOT NULL,
    last_name TEXT NOT NULL,
    grade TEXT,
    class_period TEXT,
    created_at TEXT NOT NULL DEFAULT (datetime('now')),
    updated_at TEXT NOT NULL DEFAULT (datetime('now')),
    PRIMARY KEY (student_id, profile_id),
    FOREIGN KEY (profile_id) REFERENCES profiles(profile_id) ON DELETE CASCADE
);

CREATE INDEX IF NOT EXISTS idx_roster_name ON roster_students(last_name, first_name);

CREATE TABLE IF NOT EXISTS roster_enrollments (
    profile_id TEXT NOT NULL,
    student_id TEXT NOT NULL,
    class_section TEXT NOT NULL,
    PRIMARY KEY (profile_id, student_id, class_section)
);
```

### Display Enrichment Query (Joined View)
```sql
SELECT 
    t.trip_id,
    t.student_id,
    r.first_name,
    r.last_name,
    r.class_period,
    t.trip_date,
    t.time_out,
    t.time_in,
    t.duration_seconds,
    t.status
FROM trips t
LEFT JOIN roster_students r 
    ON t.student_id = r.student_id 
    AND r.profile_id = $activeProfileId
ORDER BY t.trip_id DESC;
```

---

## 3. Fallback & Safe Degradation Rules

- **Unmatched Student ID:** If a student ID is recorded on the kiosk that does not exist in the active profile's roster, the UI MUST safely fall back to displaying the raw Student ID formatted with a hash prefix (e.g., `#10482`).
- **No Active Profile:** If no profile is selected, raw student IDs are displayed.
- **Malformed CSV Rows:** Parser skips invalid rows with non-numeric IDs and surfaces a summary toast (e.g., `"Imported 28 students; 2 malformed rows skipped"`).

---

## 4. Privacy & Bluetooth Boundaries

- **Zero Over-The-Air Transmission:** Roster records and student names are strictly stored on the local desktop PC.
- **No Terminal Knowledge:** The physical ESP32 kiosk never receives names, periods, or roster data over BLE.

---

## 5. Verification & Testing

| Verification Step | macOS Sufficient? | Windows Required? | Hardware Needed? |
| :--- | :--- | :--- | :--- |
| CSV parsing & column auto-detection unit tests | **Yes** | No | No |
| SQLite insert/upsert & profile scoping tests | **Yes** | No | No |
| Join query performance (<10ms for 10,000 trips) | **Yes** | No | No |
| Desktop UI import dialog & fallback rendering | **Yes** | No | No |
| Windows native COM file picker & concurrent file read | No | **Yes** | No |
