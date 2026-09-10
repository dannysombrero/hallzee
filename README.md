<p align="center">
  <img src="docs/images/hallzee-logo.png" alt="Hallzee" width="88" />
</p>

<h1 align="center">Hallzee</h1>

<p align="center">
  A simple, private hall-pass system for classrooms.
</p>

<p align="center">
  <a href="docs/getting-started-users.md">Teacher guide</a> ·
  <a href="https://github.com/dannysombrero/hallzee/releases?q=client-v&expanded=true">Downloads</a> ·
  <a href="https://github.com/dannysombrero/hallzee/wiki">Documentation</a> ·
  <a href="CONTRIBUTING.md">Contribute</a> ·
  <a href="LICENSE">License</a>
</p>

<p align="center">
  <a href="LICENSE"><img src="https://img.shields.io/badge/software-AGPL--3.0--or--later-2563eb" alt="Software license: AGPL-3.0-or-later" /></a>
  <a href="models/LICENSE"><img src="https://img.shields.io/badge/hardware-CC_BY--SA_4.0-f97316" alt="Hardware license: CC BY-SA 4.0" /></a>
</p>

Hallzee pairs a small keypad terminal by the classroom door with a Windows or
Mac app. Students enter their ID when they leave and return. Teachers can see
who is out, review trip history, manage a roster, and set classroom policies.

Classroom records stay on the teacher's computer, and the terminal continues
working if Bluetooth or the internet is unavailable.

<p align="center">
  <img src="preview-site/public/hallzee-terminal.png" alt="A blue Hallzee keypad terminal mounted on a wall" width="680" />
</p>

## Start here

| I want to… | Go to… |
| --- | --- |
| Use Hallzee in my classroom | [Teacher guide](docs/getting-started-users.md) |
| Download the Windows or Mac app | [Desktop downloads](https://github.com/dannysombrero/hallzee/releases?q=client-v&expanded=true) |
| Set up or update a terminal | [Installation and firmware help](https://github.com/dannysombrero/hallzee/wiki/Testing-and-Installation) |
| Fix a problem | [Teacher troubleshooting](docs/getting-started-users.md#when-something-isnt-working) |
| Change the code or hardware | [Developer guide](docs/getting-started-developers.md) |

The current desktop packages are not publisher-signed or notarized, so a
school-managed computer may ask for IT approval when Hallzee is first opened.
The [teacher guide](docs/getting-started-users.md#download-hallzee) explains
what to download and how to open it safely.

## What Hallzee does

- Gives students a quick keypad check-out and check-in flow.
- Shows live pass status in the desktop app, including a compact mini window.
- Saves trips on the terminal during a disconnect and syncs them later.
- Keeps rosters and trip history local to the teacher's computer.
- Supports classroom schedules, pass limits, warnings, reports, and multiple
  class sections.
- Checks for desktop and terminal firmware updates from Hallzee releases.

## Visual tour

### Dashboard

<p align="center">
  <img src="docs/images/dashboard-overview.png" alt="Hallzee dashboard with current pass status, recent activity, policies, and exceeded-time insights" width="900" />
</p>

### Recent activity, roster, and policy summary

<p align="center">
  <img src="docs/images/recent-activity-and-policy.png" alt="Recent student activity beside the roster and pass policy summary" width="760" />
</p>

### Trip history log

<p align="center">
  <img src="docs/images/trip-history.png" alt="Searchable Hallzee trip history log" width="900" />
</p>

### Student roster

<p align="center">
  <img src="docs/images/student-roster.png" alt="Hallzee student roster with student IDs, names, grades, and class periods" width="900" />
</p>

### Policies and bell times

<p align="center">
  <img src="docs/images/policies-and-bell-times.png" alt="Hallzee pass policy rules and bell schedule settings" width="900" />
</p>

### Exceeded-time insights

<p align="center">
  <img src="docs/images/exceeded-time-panel.png" alt="Hallzee exceeded-time panel highlighting students with longer trips" width="540" />
</p>

### Mini Window

<p align="center">
  <img src="docs/images/mini-window.png" alt="Hallzee compact always-on-top mini window" width="540" />
</p>

## Learn more

The [Hallzee Wiki](https://github.com/dannysombrero/hallzee/wiki) is organized
by audience. Teacher instructions come first. Hardware, firmware builds,
architecture, testing, release operations, and design notes live in the
[developer documentation](https://github.com/dannysombrero/hallzee/wiki/Developer-Documentation).

Hallzee is preparing for its first stable release. Maintainers can follow the
[v1.0 release checklist](docs/release-readiness.md); teachers can use the
numbered downloads once they appear on the releases page.

## Contributing

Contributions are welcome. Read [CONTRIBUTING.md](CONTRIBUTING.md) and the
[developer guide](docs/getting-started-developers.md) before opening a pull
request. Please report security problems privately using
[SECURITY.md](SECURITY.md).

## License

Hallzee software and firmware are copyright © 2026 Hallzee Labs and licensed
under [AGPL-3.0-or-later](LICENSE). Hardware and 3D models in
[`models/`](models) use [CC BY-SA 4.0](models/LICENSE). Third-party notices are
listed in [THIRD-PARTY-NOTICES.md](THIRD-PARTY-NOTICES.md).
