# Project documentation policy

Keep the project documentation current as part of every change. When a change
affects installation, setup, testing, operation, hardware, or platform support,
update the relevant page under `docs/` and the matching GitHub Wiki page in the
same branch. Link it from the README when it helps a new contributor find it.

Favor a one-command bootstrap from a clean computer. Documentation must not
assume that Git, .NET, Arduino CLI, board packages, or libraries are already
installed. If an action cannot be reduced to one or two commands, add or update
a project script that performs the setup automatically.

Whenever asking for human testing, state all of the following plainly:

- whether testing on a Mac is sufficient;
- whether a Windows PC is required;
- the exact Windows-specific capability being tested, if one exists; and
- when Windows behavior has not yet been verified.

Use `docs/testing-and-installation.md` as the main contributor guide. Keep it
short, task-focused, and accurate to the currently supported workflows.
