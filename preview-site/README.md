# Hallzee Client Preview

This browser preview is the canonical UX prototype for Hallzee's future desktop client. It uses mock services and does not connect to Bluetooth hardware or production SQLite storage.

## Structure

- `app/page.tsx` is the route entry point.
- `app/HallzeeApp.tsx` composes the provider and shell.
- `app/hallzee/HallzeeProvider.tsx` owns the mock scenario state and actions.
- `app/hallzee/types.ts` and `mockData.ts` define typed domain data and fixtures.
- `app/hallzee/components/` contains the shared shell, terminal search, and demo controls.
- `app/hallzee/pages/` contains Dashboard, Trips, Roster, Policies, Terminal, and Settings views.

## Supported prototype flows

- terminal search, selection, connection, disconnection, and manual sync;
- occupied/available demonstration state and running timer;
- recent trips plus sortable/filterable full history;
- CSV export confirmation and student roster import;
- terminal identity and maximum student-ID setting;
- planned policies, bell schedules, profiles, and automatic-sync controls;
- toast feedback, roster-name fallback, and sandbox scenario controls.

Occupied/available state is demo-only until the firmware and desktop protocol implement an explicit active-pass query/event. The current `TIME_CURSOR` sync supplies completed/reset trip records, not an in-progress checkout.

## Validation

From `preview-site/`:

```bash
npm ci
npm test
npm run lint
```

Mac testing is sufficient for this mock UI. A Windows PC and physical Hallzee terminal are still required to validate BLE discovery, GATT connection/reconnection, and real settings or trip transfer.
