// Shared plumbing for the LIVE UX suite (test/live/*.spec.js) — the same kit components as the mock
// suite, but served by the sample app (samples/arazzo Aspire composition): real control plane, real
// stores, Keycloak sign-in through the BFF (§16.3), a real runner executing real runs.
//
// The rules of the live suite differ from the mock suite in three ways, and every test must honour
// them:
//   1. State is REAL and SHARED. There is no fresh-mock-per-goto: anything a test creates it must
//      clean up (or create under a unique name via uniq()), and assertions must be RELATIVE
//      (contains / gained-one / status-changed), never seed-exact counts.
//   2. Identity is real. There is no persona dropdown — personas are actual Keycloak users
//      (LIVE_USERS), each sign-in a real OIDC round trip; a second identity needs a second browser
//      context (its own cookie jar).
//   3. The pre-login 401 bounce is expected noise: the shell's panels fire their first fetches
//      before the redirect lands on the challenge. Start console watching AFTER signIn.
import { expect } from '@playwright/test';

/** The Keycloak users the realm import seeds (arazzo-users-1.json). */
export const LIVE_USERS = {
  admin: { username: 'arazzo-admin', password: 'admin' }, // arazzo-admins group — the bootstrapped administrator (§16.2)
  alice: { username: 'alice', password: 'alice' },        // payments group
  oscar: { username: 'oscar', password: 'oscar' },        // observers group (read-only)
  erin: { username: 'erin', password: 'erin' },           // env-admins group
  wanda: { username: 'wanda', password: 'wanda' },        // reconcile-owners group
};

/**
 * Land the page's context on the app shell as the given user. A fresh context is bounced to the
 * Keycloak challenge (the shell's authFetch redirects on the first 401); an already-authenticated
 * context skips it. Returns once the primary tab bar is interactive.
 */
export async function signIn(page, user = LIVE_USERS.admin) {
  await page.goto('/');
  const kcUser = page.locator('#username');
  const runsTab = page.getByRole('tab', { name: 'Runs' });
  await Promise.race([
    kcUser.waitFor({ timeout: 30_000 }).catch(() => {}),
    runsTab.waitFor({ timeout: 30_000 }).catch(() => {}),
  ]);
  if (await kcUser.count()) {
    await kcUser.fill(user.username);
    await page.locator('#password').fill(user.password);
    await page.locator('#kc-login, input[name="login"]').first().click();
  }
  await expect(runsTab).toBeVisible({ timeout: 30_000 });
}

/**
 * Collect console errors from NOW on (call after signIn — the pre-auth 401 bounce is expected).
 * assertLiveClean() fails the test if any arrived.
 */
export function watchLiveErrors(page) {
  const errors = [];
  page.on('console', (message) => { if (message.type() === 'error') errors.push(message.text()); });
  page.on('pageerror', (error) => errors.push(String(error)));
  return errors;
}

export function assertLiveClean(errors) {
  expect(errors, `console errors on the live app:\n${errors.join('\n')}`).toEqual([]);
}

/**
 * Locate a PRIMARY tab. Addressed by aria-controls, not text: badge counts extend the accessible
 * name ("Approvals 2"), and Playwright's shadow piercing makes any text/class match collide with
 * the kit panels' own internal tab bars ("My requests" is a role=tab inside <arazzo-access-requests>).
 */
export function liveTab(page, name) {
  return page.locator(`[role="tab"][aria-controls="view-${name.toLowerCase()}"]`);
}

export async function openLiveTab(page, name) {
  await liveTab(page, name).click();
}

/** Select a sub-tab inside a grouped view (Runners / Security / Approvals / Requests) by its view id. */
export async function openLiveSubTab(page, subViewId) {
  await page.locator(`[role="tab"][aria-controls="${subViewId}"]`).click();
}

/** A collision-proof name for anything a test creates on the real backend. */
export function uniq(prefix) {
  return `${prefix}-${Date.now().toString(36)}${Math.floor(Math.random() * 1e4).toString(36)}`;
}
