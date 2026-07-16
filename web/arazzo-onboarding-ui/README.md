# Onboarding console

A small, build-free web app — the onboarding product's operator console. It lists onboarded customers and shows
each one's journey through onboarding (created → identity-verified/blocked → provisioned → welcomed), reading the
onboarding service's `GET /accounts` and `GET /accounts/{accountId}`.

This is a **separate application** from the Arazzo workflow engine. In reality it ships independently and merely
*consumes* the engine: the `onboard-customer` workflow the control plane orchestrates drives the onboarding
service, and this console renders the resulting state. Here it is served by the onboarding host itself (same origin
as its API), so it needs no cross-origin configuration.

## Layout

- `src/onboarding-client.js` — a dependency-free ES-module client over the onboarding read API (`OnboardingClient`,
  `ProblemError`).
- `src/onboarding-console.js` — the `<onboarding-console>` custom element (Shadow DOM): the paged account list, per
  account the expandable journey (KYC verdict → provisioning → welcome), a status filter, and optional polling.
- `src/onboarding-kit.css` — self-contained `--onb-*` theme tokens (light/dark, lifecycle status colours).
- `index.html` — mounts `<onboarding-console>`.

## Running

Served at the onboarding host's root (`/`) in the Arazzo control-plane sample's Aspire composition. Open the
onboarding resource's endpoint from the dashboard.
