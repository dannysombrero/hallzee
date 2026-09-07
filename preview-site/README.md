# Hallzee Website and Client Preview

This directory contains the Hallzee teacher-facing website and browser prototype:

- **Teacher Website (`/`)**: Product overview, hardware and desktop-client examples, waiting-list form (`/api/waitlist`), privacy notice (`/privacy`), terms (`/terms`), and self-service waiting-list removal page (`/leave-waitlist`).
- **Client Preview (`/preview`)**: Canonical UX prototype for Hallzee's desktop client. It uses mock services and simulates terminal discovery, trip synchronization, roster import, policy management, and pass occupancy without requiring Bluetooth hardware or SQLite setup.

The marketing pages use fictional student data in the client images. The terminal images are illustrative product renders; the check-in screen is rendered from the firmware display function.

## Structure

- `app/page.tsx` is the root route entry point for the teacher website.
- `app/preview/page.tsx` is the route entry point for the client preview.
- `app/HallzeeApp.tsx` composes the provider and desktop shell.
- `app/hallzee/HallzeeProvider.tsx` owns the mock scenario state, modal state, and actions.
- `app/hallzee/types.ts` and `mockData.ts` define typed domain data and fixtures.
- `app/hallzee/components/` contains the shared shell, navigation, SVG icons, modal dialogs (`TripsModal`, `RosterModal`, `PoliciesModal`, `TerminalSettingsModal`, `SettingsModal`), terminal search, and demo sandbox controls.
- `app/hallzee/pages/` contains the `DashboardPage` and standalone sub-pages.
- `app/api/waitlist/` provides endpoints for waitlist submission, removal, and CSV export.

## Supported Prototype Flows

- **Dashboard Overview**: At-a-glance terminal connection status, live pass occupancy status with real-time timer, recent completed trip activity with student avatar indicators, and pass policy summary with inner-shadow containers.
- **Trip History Log Pop-up**: Sortable and filterable table of all recorded kiosk transactions with roster matches, search by name/ID, and simulated CSV export with download tray icon.
- **Student Roster Import Pop-up**: Interactive CSV roster drop zone and active roster mapping status.
- **Policies & Bell Times Pop-up**: Classroom pass rules, overdue warning threshold, and bell period schedule.
- **Terminal Settings Pop-up**: BLE terminal connection management, terminal device name configuration, and configurable maximum student ID length (4–16 digits).
- **Application Settings Pop-up**: Background sync toggles and automatic clock alignment status.
- **Terminal Discovery & Sync**: Search for nearby kiosks, simulate BLE GATT connection, trigger manual sync with spinning progress animation, and toggle pass occupancy states.

## Getting Started

From the repository root, install dependencies and start the preview server:

```bash
npm --prefix preview-site start
```

Open <http://localhost:3000> in a browser. Press `Ctrl+C` in the terminal when finished.

For development mode with hot-reloading:

```bash
npm --prefix preview-site run dev
```

## Validation

From `preview-site/`:

```bash
npm test
npm run lint
```

## Testing Requirements

Mac testing is sufficient for verifying the UI layout, modal pop-up navigation, simulated sync flow, and export behavior in this browser prototype.

A Windows PC with Bluetooth hardware is required only for validating actual Windows BLE discovery, pairing, GATT connections, and binary protocol transfer with a physical ESP32 terminal.
