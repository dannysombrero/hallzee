# Hallzee Web Client Deployment Guide

This guide documents the automated deployment architecture and setup procedure for hosting the Hallzee Web Client at `https://web.hallzee.com` using Cloudflare Pages and GitHub Actions.

---

## Architecture Overview

The Hallzee Web Client (`web-client/`) is a client-side Progressive Web App (PWA) built with Vite, React 19, TypeScript, and Tailwind CSS.

- **Hosting Platform**: Cloudflare Pages
- **Production Origin**: `https://web.hallzee.com`
- **Security & Headers**: Static security headers (`web-client/public/_headers`) are natively served by Cloudflare Pages to enforce Content Security Policy (CSP) and `Permissions-Policy: bluetooth=(self)`.
- **CI/CD Pipeline**: [`.github/workflows/deploy-web-client.yml`](../.github/workflows/deploy-web-client.yml) automatically checks, builds, packages, and deploys the static artifact.

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

In accordance with [AGENTS.md](../AGENTS.md):
- Never commit Cloudflare API tokens, Account IDs, or private signing keys into Git.
- All credentials must reside strictly in GitHub Repository Secrets.
- Automated repository hygiene checks must pass before merging any deployment configuration:
  ```sh
  python3 -m unittest discover -s test -p test_repo_hygiene.py
  ```
