# Feature Design: Classroom Policies & Bell Schedules

**Status:** Approved Design Spec  
**Target Milestone:** Phase 6 (Add Policy & Automation Features)  
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
| **Daily Pass Limit per Student** | **Desktop Client** | History Audit | Requires historical SQLite records; kiosk LittleFS remains lightweight. |
| **10/10 Period Lockout Warning** | **Desktop Client** | Dashboard Alert | Desktop flags checkouts that violate period lockout boundaries based on RTC. |
| **Bell Schedule Transitions** | **Desktop Client** | Profile Switching | Desktop tracks active period and automatically associates trips with that period. |

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
    FOREIGN KEY (profile_id) REFERENCES profiles(profile_id) ON DELETE CASCADE
);
```

---

## 4. Policy Engine Evaluation Logic

```csharp
public sealed class PolicyEngine : IPolicyEngine {
    public PolicyEvaluationResult EvaluateCheckout(string studentId, DateTime checkoutTime, PolicyRule rule, List<Trip> todayTrips) {
        var studentTodayCount = todayTrips.Count(t => t.StudentId == studentId && t.TripDate == checkoutTime.Date);
        var isDailyExceeded = studentTodayCount >= rule.MaxDailyPassesPerStudent;
        
        var currentPeriod = GetActivePeriod(checkoutTime);
        var isLockout = false;
        if (currentPeriod != null) {
            var startLockout = currentPeriod.StartTime.AddMinutes(rule.LockoutStartMinutes);
            var endLockout = currentPeriod.EndTime.AddMinutes(-rule.LockoutEndMinutes);
            isLockout = checkoutTime < startLockout || checkoutTime > endLockout;
        }

        return new PolicyEvaluationResult {
            AllowCheckout = true, // Informational warnings on desktop
            DailyLimitWarning = isDailyExceeded,
            LockoutPeriodWarning = isLockout
        };
    }
}
```

---

## 5. Verification & Testing Matrix

| Capability | macOS Testing | Windows Testing | Hardware Required |
| :--- | :--- | :--- | :--- |
| Policy evaluation & 10/10 calculation unit tests | **Sufficient** | Optional | No |
| Bell schedule time-window matching tests | **Sufficient** | Optional | No |
| Desktop UI policy alerts & warnings | **Sufficient** | Optional | No |
| Physical kiosk single-pass rejection test | **Not Usable** | **Required** | **Yes (ESP32 Kiosk)** |
