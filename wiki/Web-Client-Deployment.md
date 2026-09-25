# Hallzee Web Client Deployment Guide

This guide documents the automated deployment architecture and setup procedure for hosting the Hallzee Web Client at `https://web.hallzee.com` using Cloudflare Pages and GitHub Actions.

---

## Architecture Overview

The Hallzee Web Client (`web-client/`) is a client-side Progressive Web App (PWA) built with Vite, React 19, TypeScript, and Tailwind CSS.

- **Hosting Platform**: Cloudflare Pages
- **Production Origin**: `https://web.hallzee.com`
- **Security & Headers**: Static security headers (`web-client/public/_headers`) are natively served by Cloudflare Pages to enforce Content Security Policy (CSP) and `Permissions-Policy: bluetooth=(self)`.
- **CI/CD Pipeline**: [`.github/workflows/deploy-web-client.yml`](https://github.com/dannysombrero/hallzee/blob/main/.github/workflows/deploy-web-client.yml) automatically checks, builds, packages, and deploys the static artifact.

---

## Pipeline Strategy

### 1. Current Phase: Dev Straight to Live

To facilitate rapid development and hardware testing on your Windows PC with physical ESP32 kiosks:
- Any push to `main` (affecting `web-client/`) or manual trigger via `workflow_dispatch` executes the full verification suite (`npm run check`) and deploys directly to production at `https://web.hallzee.com`.

### 2. Future Phase: Staging → Dev → Live Pipeline

When the software matures toward multi-teacher rollout:
- **Dev Branch (`dev`)**: Commits deploy to preview URLs (`https://dev.hallzee-web-client.pages.dev` or `https://dev-web.hallzee.com`).
- **Staging Branch (`staging`)**: Pre-release verification deployed to `https://staging-web.hallzee.com`.
- **Production (`main` / Tagged Release)**: Formal signed release promoted to `https://web.hallzee.com`.

Cloudflare Pages natively handles branch deployments within the same project without duplicate infrastructure.

---

## One-Time Setup Instructions

### Step 1: Create Cloudflare Pages Project

1. Log in to the [Cloudflare Dashboard](https://dash.cloudflare.com/).
2. Navigate to **Workers & Pages** -> **Create application** -> **Pages** tab -> **Upload assets**.
3. Set the project name to:
   ```
   hallzee-web-client
   ```
4. Click **Create project**. (You do not need to upload files manually; the GitHub Action handles deployments).

### Step 2: Configure Custom Domain (`web.hallzee.com`)

1. In the `hallzee-web-client` project view, click the **Custom domains** tab.
2. Click **Set up a domain**.
3. Enter `web.hallzee.com` and click **Continue**.
4. Confirm DNS record creation (Cloudflare will automatically manage the CNAME record and SSL/TLS certificate).

### Step 3: Generate Cloudflare API Token

1. Click your profile avatar in the upper-right corner and select **My Profile**.
2. Click **API Tokens** -> **Create Token**.
3. Select **Create Custom Token** -> **Get started**.
4. Configure the token:
   - **Token name**: `hallzee-pages-deploy`
   - **Permissions**:
     - `Account` | `Cloudflare Pages` | `Edit`
   - **Account Resources**:
     - `Include` | `All accounts` (or select your specific account)
5. Click **Continue to summary** -> **Create Token**.
6. Securely copy the generated token string.

### Step 4: Locate Cloudflare Account ID

1. Return to the Cloudflare dashboard overview.
2. In the right-hand sidebar, locate **Account ID** and copy the 32-character hexadecimal string.

### Step 5: Configure GitHub Secrets

1. In the GitHub repository, navigate to **Settings** -> **Secrets and variables** -> **Actions**.
2. Under **Repository secrets**, click **New repository secret**:
   - Name: `CLOUDFLARE_API_TOKEN`
   - Value: *(Your API token from Step 3)*
3. Click **New repository secret** again:
   - Name: `CLOUDFLARE_ACCOUNT_ID`
   - Value: *(Your Account ID from Step 4)*

---

## Triggering Deployments

### Automatic Deployment
Pushing commits to `main` that include changes under `web-client/` or `.github/workflows/deploy-web-client.yml` automatically triggers the deployment action.

### Manual Deployment via GitHub Actions
1. In the GitHub repository, click the **Actions** tab.
2. Select **Deploy web client to Cloudflare** in the left sidebar.
3. Click **Run workflow**:
   - Choose branch (e.g. `main`).
   - Select environment (`production` or `preview`).
   - Optionally toggle `Dry run` to validate without pushing to Cloudflare.
4. Click **Run workflow**.

---

## Testing & Platform Verification

Whenever verifying or testing web client deployments:

- **macOS Testing**:
  - Mac testing is sufficient for local development, building the static distribution bundle, running unit tests, executing Playwright headless browser tests, and verifying the GitHub Actions workflow syntax.
- **Windows PC Testing**:
  - A Windows PC running Google Chrome or Microsoft Edge is required to verify live Web Bluetooth terminal discovery, pairing, GATT communication, and SQLite sync reconciliation with a physical ESP32 kiosk over `https://web.hallzee.com`.
  - Windows Web Bluetooth behavior over the deployed origin has not yet been verified with physical hardware.

---

## Repository Sanitization & Security Rules

In accordance with [AGENTS.md](https://github.com/dannysombrero/hallzee/blob/main/AGENTS.md):
- Never commit Cloudflare API tokens, Account IDs, or private signing keys into Git.
- All credentials must reside strictly in GitHub Repository Secrets.
- Automated repository hygiene checks must pass before merging any deployment configuration:
  ```sh
  python3 -m unittest discover -s test -p test_repo_hygiene.py
  ```

## Student join origin and room relay

The static Pages build alone does not provide the room service. There are three
origins, with separate responsibilities:

| Origin | Purpose |
| --- | --- |
| `https://web.hallzee.com` | Teacher dashboard and local classroom data |
| `https://pass.hallzee.com` | Student code-entry page and direct `/<code>` links |
| `wss://relay.hallzee.com/ws` | Ephemeral room WebSocket service |

Add **pass.hallzee.com** as another custom domain on the same
`hallzee-web-client` Pages project, following Step 2 above. Associate it in Pages
before adding its DNS record; DNS alone does not bind a Pages custom domain. Serve
the app's `index.html` for direct student routes (Pages' SPA fallback does this
when no top-level `404.html` is present). The app chooses the student landing page
from the hostname. Do not redirect that root to the teacher dashboard.

Deploy `relay/wrangler.jsonc` separately from Pages. It declares the `ROOM_DO`
Durable Object binding, the initial SQLite class migration and the relay custom
domain; the room engine itself never writes persistent storage. Review the name
and existing migration history before using this initial configuration for an
already deployed Worker. With the web bootstrap's Node available (or Node 22.23.2+
installed), the pinned command from the repository root validates without deploying:

```sh
npx --yes wrangler@4.140.0 deploy --dry-run --config relay/wrangler.jsonc
```

For the separately authorized deployment, authenticate to the intended Cloudflare
account with Wrangler and run the same command without `--dry-run`. The current
Pages GitHub Action does **not** deploy the relay or configure the pass subdomain.
Publishing the client and relay must be coordinated: both now use explicit room
status and claim/check-in acknowledgments. No live DNS or Worker changes were
performed as part of the UI implementation.

The production CSP permits `wss://relay.hallzee.com`. Local preview additionally
permits `ws://127.0.0.1:4192` for the test relay; that allowance is not in the
production `_headers` file. `VITE_RELAY_URL` and `VITE_JOIN_ORIGIN` are optional
build-time overrides for a separate environment; update its CSP consistently.
Custom join hosts use `/join` for code entry. Preview and localhost builds default
to links on their current origin; `web.hallzee.com` defaults to `pass.hallzee.com`.
Do not put credentials in either variable or URL.

Once deployed, verify code entry at the pass root, a direct lowercase room link,
the QR target, open/closed status and two-device checkout/check-in before classroom
use. A Mac suffices for this virtual-flow check; Windows is not required. Windows
physical BLE/PWA behavior remains unverified and needs its own hardware acceptance.
See [Virtual terminals and student joining](Virtual-Terminal.md).
