# Feature Design: Student Roster Import & ID Enrichment

**Status:** Approved Design Spec  
**Target Milestone:** Phase 5 (Planned Data Foundations)  
**Related Issues:** #17  
**Scope:** Roster CSV parser, column mapping, profile-scoped database storage, display enrichment, and student-ID fallback.

---

## 1. Problem Statement & User Behavior

When students use the Hallzee kiosk, they enter only their numeric student ID (e.g. `10482`). In the raw sync database, records only contain this number. Teachers need immediate visual recognition of student names on their dashboard and in historical trip logs (e.g., displaying `Alex Rivera (Period 2)` instead of just `10482`).

Teachers will import class rosters directly from standard Student Information System (SIS) CSV exports (PowerSchool, Infinite Campus, Canvas, Google Classroom).

### User Flow
1. Teacher opens **Roster Management** from the desktop sidebar or settings.
2. Clicks **Import Roster (CSV)** and selects a CSV file.
3. The client previews the parsed records, auto-detects column headers, and allows manual column mapping:
   - Student ID (Required)
   - First Name / Last Name (or Full Name) (Required)
   - Class Period / Section (Optional)
   - Grade Level (Optional)
4. Teacher confirms import for the selected **Classroom Profile**.
5. The local SQLite database inserts or updates student records for that profile.
6. The dashboard and trip tables immediately enrich matching student IDs with full names and periods.

---

## 2. Data Model & Storage Changes

Rosters are stored in the local SQLite database scoped to specific classroom profiles:

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
