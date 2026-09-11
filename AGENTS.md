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

# Public repository sanitization and hardening policy

All changes committed to this public repository must be rigorously sanitized and hardened
against accidental exposure of sensitive, private, or machine-specific data:

- **Student and classroom privacy (PII)**: Never commit real student names, student IDs,
  teacher names, rosters, pass histories, attendance data, or `.csv` export files. Use strictly
  synthetic mock data (such as `docs/demo-roster.csv` and fictional test IDs) for all tests,
  fixtures, documentation, and screenshots.
- **Cryptographic secrets and private keys**: Never commit private signing keys, certificates,
  or keystores (`*.pem`, `*.key`, `*.pfx`, `*.p12`, ESP32 private keys, or `HALLZEE_FIRMWARE_SIGNING_KEY`).
  The only cryptographic file permitted in Git tracking is the public verification certificate
  `firmware/release-public-key.pem`.
- **Credentials and tokens**: Never commit API keys, authentication tokens, pairing secrets,
  session credentials, or password hashes. Redact tokens, pairing credentials, names, and keys
  from all logs, terminal outputs, PR descriptions, and conversational responses.
- **Environment and local host state**: Never commit `.env` files, `.local/`, `Library/`,
  `TestResults/`, `.DS_Store`, local SQLite databases (`hallzee-trips.db`), terminal backups,
  or scratch scripts. Keep `.gitignore` safeguards strictly intact.

## Post-request sanitization and hygiene verification

After completing code, documentation, or configuration modifications, and before
concluding every request or proposing any commit:

1. **Run repository hygiene tests**:
   Execute the automated hygiene test suite:
   ```sh
   python3 -m unittest discover -s test -p test_repo_hygiene.py
   ```
   Confirm all checks pass (no forbidden files tracked, only allowed public keys tracked,
   no private key headers present, and critical `.gitignore` rules in place).
2. **Inspect working tree state**:
   Run `git status --short` to verify no untracked secrets, scratch files, build outputs,
   or sensitive exports were created in the working tree.
3. **Review diffs for sensitive material**:
   Inspect all modified lines in `git diff` to ensure no accidental credential, token,
   or personal identifier insertion.
4. **State sanitization status plainly**:
   In every response summarizing completed work, explicitly state whether repository
   hygiene and secret sanitization checks were executed and confirmed clear.

