#!/usr/bin/env bash
set -euo pipefail

# Replaces one local Hallzee workspace with fictional data for README images.
demo_database="${1:-$HOME/Library/Application Support/Hallzee/universal/hallzee-trips.db}"
demo_roster="$(cd "$(dirname "$0")/.." && pwd)/docs/demo-roster.csv"

if [[ ! -f "$demo_database" || ! -f "$demo_roster" ]]; then
  echo "Hallzee database or demo roster was not found." >&2
  exit 1
fi

roster_sql=""
trip_sql=""
student_ids=()
while IFS=, read -r student_id student_name _class_name _period; do
  [[ "$student_id" == "student_id" ]] && continue
  first_name="${student_name%% *}"
  last_name="${student_name#* }"
  grade=$((10 + (${#student_ids[@]} % 3)))
  student_ids+=("$student_id")
  roster_sql+="INSERT INTO roster_students (student_id, profile_id, first_name, last_name, grade, class_period, created_at, updated_at) VALUES ('$student_id','default','$first_name','$last_name','$grade','Chemistry AP',datetime('now'),datetime('now'));"$'\n'
done < "$demo_roster"

for trip_number in $(seq 1 50); do
  student_index=$(( (trip_number - 1) % ${#student_ids[@]} ))
  student_id="${student_ids[$student_index]}"
  # Noah Carter has two over-threshold trips; two other students have one each.
  case "$trip_number" in
    2) duration=512 ;;
    15) duration=545 ;;
    31) duration=703 ;;
    49) student_id="${student_ids[1]}"; duration=626 ;;
    *) duration=$((210 + ((trip_number * 37) % 170))) ;;
  esac
  day=$((10 - ((trip_number - 1) / 8)))
  minute=$((2 + ((trip_number * 7) % 48)))
  time_out=$(printf '09:%02d:00' "$minute")
  time_in=$(printf '09:%02d:00' "$((minute + 4))")
  trip_sql+="INSERT INTO trips (terminal_id, trip_id, student_id, trip_date, time_out, time_in, duration_seconds, status, synced_at, schedule_name, class_section, profile_id) VALUES ('DEMO-ROOM-204',$((1000 + trip_number)),'$student_id','2026-09-$(printf '%02d' "$day")','$time_out','$time_in','$duration','COMPLETED',datetime('now'),'Regular','Chemistry AP','default');"$'\n'
done

sqlite3 "$demo_database" <<SQL
PRAGMA foreign_keys = ON;
BEGIN IMMEDIATE;
DELETE FROM desktop_passes;
DELETE FROM trips;
DELETE FROM roster_enrollments;
DELETE FROM roster_students;
DELETE FROM bell_schedules;
DELETE FROM policy_rules;
DELETE FROM sync_state;
INSERT INTO profiles (profile_id, name, is_active, created_at, updated_at)
VALUES ('default', 'Room 204 • Chemistry AP', 1, datetime('now'), datetime('now'))
ON CONFLICT(profile_id) DO UPDATE SET name = excluded.name, is_active = 1, updated_at = datetime('now');
UPDATE profiles SET is_active = CASE WHEN profile_id = 'default' THEN 1 ELSE 0 END;
INSERT INTO policy_rules (rule_id, profile_id, max_simultaneous_passes, duration_warning_seconds, max_daily_passes_per_student, lockout_start_minutes, lockout_end_minutes, first_window_action, last_window_action, alert_sound, terminal_enforcement_enabled)
VALUES ('demo-policy', 'default', 1, 480, 2, 10, 10, 'Warn', 'Warn', 'Chime', 0);
INSERT INTO bell_schedules (schedule_id, profile_id, period_name, start_time, end_time, days_of_week, schedule_name, class_section) VALUES
  ('demo-period-1', 'default', 'Period 1', '08:00', '08:50', '1,2,3,4,5', 'Regular', 'Chemistry AP'),
  ('demo-period-2', 'default', 'Period 2', '08:55', '09:45', '1,2,3,4,5', 'Regular', 'Chemistry AP'),
  ('demo-period-3', 'default', 'Period 3', '09:15', '10:05', '1,2,3,4,5', 'Regular', 'Chemistry AP'),
  ('demo-period-4', 'default', 'Period 4', '10:10', '11:00', '1,2,3,4,5', 'Regular', 'Chemistry AP');
$roster_sql
INSERT INTO roster_enrollments (profile_id, student_id, class_section)
SELECT profile_id, student_id, class_period FROM roster_students WHERE profile_id = 'default';
$trip_sql
INSERT INTO sync_state (profile_id, last_successful_sync_at) VALUES ('default', datetime('now', '-2 minutes'));
COMMIT;
SQL

echo "Demo workspace seeded: $demo_database"
