# Hallzee Technical Architecture Document

**Status:** Approved Reference  
**Scope:** ESP32 Firmware, Bluetooth Protocol, Native Desktop Application, SQLite Storage, and Data Boundaries  
**Target Architecture:** Multi-Layer Local-First Native Client & Dedicated BLE Hardware Kiosk

---

## 1. System Overview & Topology

Hallzee is structured into three primary architectural tiers:
1. **Embedded Physical Terminal (Kiosk):** An ESP32 microcontroller with a color TFT display and 3x4 matrix keypad placed at the classroom exit door. The default hardware profile uses an ST7735 display; an alternate ILI9341 profile supports 240×320 modules.
2. **Bluetooth Low Energy (BLE) Synchronization Transport:** A connectionless/paired GATT service facilitating encrypted/authenticated local transfer between the kiosk and desktop.
3. **Local Desktop Client (Teacher Workstation):** A native .NET 8 / Avalonia client backed by an embedded SQLite database, responsible for roster mapping, live pass oversight, historical search, and data export.

```mermaid
flowchart TB
    subgraph Hardware_Kiosk ["ESP32 Physical Kiosk"]
        Keypad["4x4 Keypad"] --> KeypadCtrl["KeypadController"]
        KeypadCtrl --> Sketch["bathroom-signin.ino"]
        Sketch --> TermCtrl["TerminalController"]
        TermCtrl --> FlashStorage["TripStorage\n(LittleFS Append Log + Preferences)"]
        Sketch --> Display["TerminalDisplay (ST7735)"]
        Sketch --> Clock["ClockService (RTC / System Clock)"]
        BluetoothSync["BluetoothSync (GATT Server)"] <--> FlashStorage
        BluetoothSync <--> Sketch
    end

    subgraph BLE_Transport ["Bluetooth Low Energy (GATT)"]
        BluetoothSync <===|"Chunked UTF-8 Packets (20-byte ATT MTU)\nService: 005924a2-c6e5-4340-9bb8-22d9dd37a283"===> BleManager["BluetoothConnectionManager\n(WinRT BLE GATT Client)"]
    end

    subgraph Desktop_Client ["Teacher Workstation (.NET / Avalonia)"]
        BleManager <--> SyncSession["SyncSession & KioskProtocol"]
        SyncSession <--> AppServices["Application Services\n(ActivePass, Roster, Policy, Settings)"]
        AppServices <--> ViewModels["ViewModels & State Stores"]
        ViewModels <--> UIViews["Avalonia XAML Views\n(Dashboard, Trips, Roster, Policies)"]
        AppServices <--> SQLiteRepo["TripSqliteRepository\n(Local SQLite with WAL)"]
        SQLiteRepo --> SQLiteDB[("hallzee.db\n(trips, profiles, rosters, policies)")]
        SQLiteRepo --> CSVExport["CSV File Exporter"]
    end

    subgraph Future_Expansion ["Future Cloud Boundary (Optional)"]
        Desktop_Client -.->|"Opt-In Aggregate Sync"| CloudPortal["School-Wide Admin Portal"]
    end
```

---

## 2. State Ownership Decision Matrix

To prevent data drift, duplicated configuration systems, and split-brain sync errors, every entity in the Hallzee ecosystem has an explicit authority boundary:

| Data or Behavior | Desktop Client | Teacher Workspace | Terminal Kiosk | Future Cloud | Authoritative Source |
| :--- | :--- | :--- | :--- | :--- | :--- |
| **Trip History** | Yes (Local SQLite store) | Referenced | Local source (LittleFS) | Possible (read-only audit) | **Terminal** creates; **Client SQLite** is permanent historical authority. |
| **Student Roster** | Yes (Local DB) | Yes (teacher workspace with class-section enrollments) | No (Never sent over BLE) | Possible (SIS integration) | **Desktop Client teacher workspace** is authoritative. |
| **Student-ID Length Limit** | Display & Configuration | Optional default preset | Authoritative (Preferences) | Possible (School policy default) | **Terminal Hardware** is authoritative during checkout. |
| **Terminal Name** | Remembered in UI | Association | Authoritative (Preferences) | Possible (Asset registry) | **Terminal Hardware** stores its own name in non-volatile flash. |
| **Auto-Sync Preferences** | Yes (Local client config) | Yes (Teacher-workspace setting) | No | No | **Desktop Client** owns sync timing. |
| **Maximum Students Out** | Display & UI warning | Yes (Teacher-workspace setting) | Yes (1–8 active passes) | Possible | **Terminal** enforces physically; **Teacher workspace** configures. |
| **Bell Schedule & Periods** | Yes (Local DB) | Yes (Teacher-workspace setting) | Optional cache | Possible (School schedule sync) | **Desktop Client** is authoritative. |
| **Live Active Pass** | UI Display & Timer | Ephemeral cache | Authoritative (Preferences) | Possible (Live status widget) | **Terminal Hardware** owns the current active checkout. |

---

## 3. Desktop Client Architecture

The desktop application is built with modern C# / .NET 8 using the MVVM (Model-View-ViewModel) pattern, with presentation in Avalonia UI and a prototype reference in React/Vite.

```text
receiver/
  universal/                  # Avalonia Desktop Presentation
    ViewModels/               # Reactive ViewModels
      DashboardViewModel.cs
      TripsViewModel.cs
      RosterViewModel.cs
      PoliciesViewModel.cs
      TerminalViewModel.cs
      SettingsViewModel.cs
    Views/                    # XAML Views & Dialogs
      DashboardView.axaml
      TripsView.axaml
      RosterView.axaml
  windows/
    BathroomSync.Core/        # Core Domain & Infrastructure
      Contracts/              # Service Interfaces
        ITripRepository.cs
        ITerminalConnectionPort.cs
        IRosterRepository.cs
        IPolicyEngine.cs
        ISettingsService.cs
      Storage/                # SQLite & Migration Engine
        TripSqliteRepository.cs
        DatabaseMigrator.cs
        Migrations/
      Protocol/               # BLE Transport & Session Codec
        SyncSession.cs
        KioskSettingsProtocol.cs
        ActivePassProtocol.cs
      Domain/                 # Strongly-typed models
        Trip.cs
        Student.cs
        Profile.cs
        PolicyRule.cs
```

### Layer Responsibilities
- **Views (XAML):** Declarative UI rendering, responsive layout, dark/light themes, user input bindings.
- **ViewModels:** Screen state coordination, command handling, formatting (e.g. converting elapsed seconds to `4m 12s`), toast notification dispatching.
- **Service Contracts:** Pure business interfaces decoupled from hardware or storage implementations.
- **Storage Layer:** Thread-safe SQLite repository operating with Write-Ahead Logging (WAL) and synchronous transactions.
- **Protocol & Bluetooth Layer:** Handles WinRT BLE GATT device discovery, notification subscriptions, fragment assembly, and command serialization.

---

## 4. Firmware Architecture (ESP32)

The firmware composition root is `firmware/terminal/bathroom-signin.ino`. It instantiates modular hardware controllers and runs a cooperative, non-blocking main loop.

### Core Firmware Modules
1. **`TerminalController`:**
   - Manages the active-pass state machine (`AVAILABLE` vs. `OCCUPIED`).
   - Restores in-flight passes from ESP32 Preferences upon power-on.
   - Handles checkout, checkin, student ID validation, and manual admin reset (`* + #` gesture).
2. **`TripStorage`:**
   - Maintains an append-only transaction log in LittleFS flash memory.
   - Assigns monotonically increasing integer `trip_id` values.
   - Streams unsynced or post-cursor records across BLE.
3. **`BluetoothSync`:**
   - Implements the BLE GATT server, advertising the service UUID `005924a2-c6e5-4340-9bb8-22d9dd37a283`.
   - Manages 20-byte ATT fragmentation, UTF-8 newline buffering, and cursor-based synchronization.
4. **`ClockService`:**
   - Tracks wall-clock time using ESP32 system time.
   - Synchronizes time with the desktop client upon BLE handshake.
5. **`TerminalDisplay`:**
   - Manages TFT graphics rendering through `DisplayPort`. The default ST7735
     profile uses the original 160×128 landscape canvas; the alternate ILI9341
     profile maps that shared UI into a 320×240 landscape panel by default and
     supports all four controller rotations.
   - Decoupled from business logic; accepts view models and draw commands.
6. **`KeypadController`:**
   - Implements debounce, long-press detection, and keystroke buffering for 4x4 matrix keypads.

---

## 5. Bluetooth Protocol & State Machines

### 5.1 BLE GATT Transport Specification
- **Service UUID:** `005924a2-c6e5-4340-9bb8-22d9dd37a283`
- **RX Characteristic (Client -> Terminal Writes):** `e80f9559-49eb-47bc-af04-8e92e98ced56`
- **TX Characteristic (Terminal -> Client Notifications):** `44a359f3-9215-4189-a3cb-e7ce18ad40d6`

All protocol communication consists of newline-terminated (`\n`) UTF-8 text strings, chunked into 20-byte payloads to guarantee compatibility with default BLE ATT MTU constraints.

### 5.2 Synchronization Protocol Flow

```text
Desktop Client                               ESP32 Kiosk Terminal
      |                                              |
      |--- Enable TX Notifications ----------------->|
      |--- HELLO,1 \n ------------------------------>|
      |<-- HALLZEE_READY,1 \n -----------------------|
      |--- GET_SETTINGS \n ------------------------->|
      |<-- SETTINGS,MAX_ID_LENGTH,10 \n -------------|
      |--- GET_ACTIVE_PASS \n ---------------------->|
      |<-- ACTIVE_PASS,12345,1725200000 \n ----------|  (or ACTIVE_PASS,NONE)
      |--- TIME_CURSOR,2026-09-01,16:45:00,104 \n -->|
      |<-- TIME_ACK,OK \n ---------------------------|
      |<-- SYNC_BEGIN,2 \n --------------------------|
      |<-- TRIP,105,12345,2026-09-01,16:30,16:35,300,COMPLETED \n
      |--- ACK,105 \n (Committed to SQLite) -------->|
      |<-- TRIP,106,67890,2026-09-01,16:36,16:40,240,COMPLETED \n
      |--- ACK,106 \n (Committed to SQLite) -------->|
      |<-- SYNC_END \n ------------------------------|
```

### 5.3 Exclusive Channel Operations
To prevent race conditions, the desktop client's communication coordinator strictly serializes commands. Only one operation may occupy the channel at a time:
1. `SyncSession` (Clock update + `TIME_CURSOR` stream)
2. `SettingsQuery` / `SettingsUpdate` (`GET_SETTINGS`, `SET,MAX_ID_LENGTH,<val>`, `SET,TERMINAL_NAME,<name>`)
3. `ActivePassQuery` (`GET_ACTIVE_PASS`)
4. `FullRecovery` (`SYNC_ALL`)

---

## 6. SQLite Database Schema & Versioned Migrations

The desktop client maintains a local SQLite database named `hallzee.db` in `%LOCALAPPDATA%/Hallzee/` (Windows) or `~/Library/Application Support/Hallzee/` (macOS).

### 6.1 Schema Version Table
```sql
CREATE TABLE IF NOT EXISTS schema_migrations (
    version INTEGER PRIMARY KEY,
    applied_at TEXT NOT NULL,
    description TEXT NOT NULL
);
```

### 6.2 Relational Tables (v2 Unified Schema)

```sql
-- 1. Completed & Logged Trips
CREATE TABLE IF NOT EXISTS trips (
    trip_id INTEGER PRIMARY KEY,
    student_id TEXT NOT NULL,
    trip_date TEXT NOT NULL,
    time_out TEXT NOT NULL,
    time_in TEXT NOT NULL,
    duration_seconds INTEGER NOT NULL,
    status TEXT NOT NULL,               -- 'COMPLETED' | 'MANUAL_RESET'
    synced_at TEXT NOT NULL DEFAULT (datetime('now')),
    terminal_id TEXT DEFAULT 'DEFAULT'
);
CREATE INDEX IF NOT EXISTS idx_trips_date ON trips(trip_date);
CREATE INDEX IF NOT EXISTS idx_trips_student ON trips(student_id);

-- 2. Teacher Workspaces (stored in the legacy `profiles` table)
CREATE TABLE IF NOT EXISTS profiles (
    profile_id TEXT PRIMARY KEY,
    name TEXT NOT NULL,
    is_active INTEGER NOT NULL DEFAULT 0,
    created_at TEXT NOT NULL,
    updated_at TEXT NOT NULL
);

-- 3. Student Roster (Scoped per Profile)
CREATE TABLE IF NOT EXISTS roster_students (
    student_id TEXT NOT NULL,
    profile_id TEXT NOT NULL,
    first_name TEXT NOT NULL,
    last_name TEXT NOT NULL,
    grade TEXT,
    class_period TEXT,
    PRIMARY KEY (student_id, profile_id),
    FOREIGN KEY (profile_id) REFERENCES profiles(profile_id) ON DELETE CASCADE
);
CREATE INDEX IF NOT EXISTS idx_roster_name ON roster_students(last_name, first_name);

-- 4. Terminal Associations & Config
CREATE TABLE IF NOT EXISTS terminals (
    terminal_id TEXT PRIMARY KEY,
    custom_name TEXT NOT NULL,
    ble_address TEXT,
    last_seen_at TEXT,
    max_id_length INTEGER DEFAULT 10
);

-- 5. Classroom Policies
CREATE TABLE IF NOT EXISTS policy_rules (
    rule_id TEXT PRIMARY KEY,
    profile_id TEXT NOT NULL,
    max_simultaneous_passes INTEGER DEFAULT 1,
    duration_warning_seconds INTEGER DEFAULT 420,
    max_daily_passes_per_student INTEGER DEFAULT 3,
    lockout_start_minutes INTEGER DEFAULT 10,
    lockout_end_minutes INTEGER DEFAULT 10,
    FOREIGN KEY (profile_id) REFERENCES profiles(profile_id) ON DELETE CASCADE
);
```

---

## 7. Security, Privacy & Compliance Boundaries

1. **FERPA Compliance:** Student names, grades, and profile information are stored exclusively within the local SQLite database. No roster data is transmitted over the air.
2. **Bluetooth Hygiene:** Bluetooth advertising packets contain only the device name (`Hallzee`) and standard 128-bit service UUIDs. No telemetry or student IDs are broadcast in advertising payloads.
3. **Local Encryption & Permissions:** Database files are stored with standard user-level file permissions. On multi-user school computers, data is isolated per Windows user profile.
4. **Local Diagnostics:** Diagnostics and crash reports stay on the teacher's workstation and are never uploaded automatically. Student names may appear when useful for local classroom support, but roster data must never be transmitted to the terminal or an external service.

---

## 8. Verification & Platform Testing Matrix

In compliance with the project documentation policy (`AGENTS.md`), the testing requirements for all architectural subsystems are declared below:

| Architectural Component | macOS Testing | Windows Testing | Hardware Required | Notes |
| :--- | :--- | :--- | :--- | :--- |
| **React Prototype UI (`preview-site`)** | Sufficient | Optional | No | Full preview & mock testing available in browser. |
| **C# Domain Logic & ViewModels** | Sufficient | Optional | No | Fully tested via `dotnet test` on .NET 8 (macOS & Linux). |
| **SQLite Schema & Migrations** | Sufficient | Optional | No | Validated via cross-platform SQLite unit tests. |
| **WinRT Bluetooth Discovery & GATT** | **Not Usable** | **Required** | **Yes (BLE PC)** | Windows-specific WinRT BLE stack requires a Windows 10/11 host. |
| **Physical ESP32 Kiosk Firmware** | **Not Usable** | **Required** | **Yes (ESP32 Kiosk)** | End-to-end BLE pairing, chunking, and flash sync requires physical hardware. |
