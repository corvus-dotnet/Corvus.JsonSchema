// Captioned UX walkthrough clips for docs/control-plane/ux-review.md §8 — driven against the in-browser demo mock.
// Each test is ONE user goal, shown end to end (every option exercised), at a deliberate pace.
import { test } from '@playwright/test';

// The caption banner is a top-layer POPOVER: a native modal <dialog> renders in the browser's top layer, above any
// normal-DOM z-index, so a plain div caption is painted *under* dialogs. A popover is also in the top layer, and
// re-showing it on every step re-promotes it above whatever dialog is currently open — so it stays readable, and it
// sits at the top of the viewport, clear of centred dialog bodies.
async function caption(page, text, holdMs = 3200) {
  await page.evaluate((t) => {
    let el = document.getElementById('__cap');
    if (!el) {
      el = document.createElement('div');
      el.id = '__cap';
      el.setAttribute('popover', 'manual');
      el.style.cssText = [
        'position:fixed', 'top:22px', 'left:50%', 'transform:translateX(-50%)', 'inset:auto', 'margin:0',
        'background:#0d1014', 'color:#fff',
        'font:600 20px/1.5 system-ui,-apple-system,Segoe UI,Roboto,sans-serif',
        'padding:14px 26px', 'border:1px solid rgba(255,255,255,0.16)', 'border-radius:12px',
        'max-width:84vw', 'text-align:center', 'box-shadow:0 12px 44px rgba(0,0,0,0.6)', 'pointer-events:none',
      ].join(';');
      document.body.appendChild(el);
    }
    el.textContent = t;
    // Re-promote into the top layer so it paints above any modal <dialog> opened since the last caption.
    try { el.hidePopover(); } catch { /* not shown yet */ }
    try { el.showPopover(); } catch { /* ignore */ }
  }, text);
  await page.waitForTimeout(holdMs);
}

// A settle pause so each action's effect is on screen before the next step.
const beat = (page, ms = 1300) => page.waitForTimeout(ms);

// ── Goal: approve an access request from the approver inbox ──────────────────────────────────────
test('clip-1-approve-access-request', async ({ page }) => {
  await page.goto('/demo/index.html');
  await page.locator('arazzo-control-plane').waitFor();
  await caption(page, 'Goal — approve an access request', 3000);

  await page.locator('#tab-access').click();
  await beat(page);
  await page.locator('arazzo-access-requests .tab-queue').click();
  await page.locator('arazzo-access-requests .act[data-action="approve"]').first().waitFor();
  await beat(page);
  await caption(page, 'The approver queue opens on Pending — across every workflow you administer', 3800);
  await caption(page, 'Each row shows the workflow, who asked, and the scopes requested', 3600);
  await caption(page, 'Every pending row offers Approve · Make eligible · Deny — we’ll approve this one', 4000);

  await page.locator('arazzo-access-requests .act[data-action="approve"]').first().click();
  await page.locator('arazzo-access-requests .decision-dialog').waitFor();
  await beat(page);
  await caption(page, 'The decision dialog — add an optional note for the audit trail', 3800);
  await page.locator('arazzo-access-requests .decision-dialog .reason-in').fill('On-call retry — approved for the incident window.');
  await beat(page);
  await caption(page, 'Confirm the approval', 2800);
  await page.locator('arazzo-access-requests .decision-dialog .ok').click();
  await beat(page, 1600);
  await caption(page, 'Approved — a time-boxed grant is written, and the queue clears to inbox-zero', 4400);
  await beat(page, 800);
});

// ── Goal: recover a faulted run (all four resume modes) ──────────────────────────────────────────
test('clip-2-recover-faulted-run', async ({ page }) => {
  await page.goto('/demo/index.html');
  await page.locator('arazzo-runs-table tbody tr[data-id]').first().waitFor();
  await caption(page, 'Goal — recover a faulted run', 3000);

  await page.locator('arazzo-runs-table tbody tr[data-id="run-b2c3d4e5"]').click();
  await page.locator('arazzo-run-detail [part="fault"]').waitFor();
  await beat(page);
  await caption(page, 'Open the faulted run — the step that failed, the attempt, and the error', 4000);

  await page.locator('arazzo-run-detail').getByRole('button', { name: /resume/i }).click();
  await page.locator('arazzo-resume-dialog dialog').waitFor();
  await beat(page);
  await caption(page, 'Resume offers four recovery modes — here is each', 3400);

  await page.locator('arazzo-resume-dialog input[name="mode"][value="RetryFaultedStep"]').check();
  await beat(page);
  await caption(page, '1 / 4 — Retry the faulted step (the common case)', 3600);

  await page.locator('arazzo-resume-dialog input[name="mode"][value="Rewind"]').check();
  await page.locator('arazzo-resume-dialog .rewind-picker select').waitFor();
  await beat(page);
  await caption(page, '2 / 4 — Rewind to an earlier step, chosen by name from the catalog', 4000);

  await page.locator('arazzo-resume-dialog input[name="mode"][value="Skip"]').check();
  await page.locator('arazzo-resume-dialog input.record-outputs').check();
  await page.locator('arazzo-resume-dialog arazzo-value-editor.skip-builder').waitFor();
  await beat(page);
  await caption(page, '3 / 4 — Skip past it, recording outputs — a form typed from the step’s schema', 4200);

  await page.locator('arazzo-resume-dialog input[name="mode"][value="StatePatch"]').check();
  await page.locator('arazzo-resume-dialog #patch').waitFor();
  await beat(page);
  await page.locator('arazzo-resume-dialog #patch').fill('[\n  { "op": "replace", "path": "/inputs/adopter/email", "value": "ada@example.com" }\n]');
  await beat(page);
  await caption(page, '4 / 4 — State-patch the run context (RFC 6902 JSON Patch), then retry', 4200);

  await page.locator('arazzo-resume-dialog input[name="mode"][value="RetryFaultedStep"]').check();
  await beat(page);
  await caption(page, 'We’ll take the common case — retry the faulted step', 3400);
  await page.locator('arazzo-resume-dialog').getByRole('button', { name: 'Resume', exact: true }).click();
  await beat(page, 1600);
  await caption(page, 'Recovered — the run is back in flight', 3800);
  await beat(page, 800);
});

// ── Goal: register a source connection (a secret reference, never a secret) ───────────────────────
test('clip-3-register-connection', async ({ page }) => {
  await page.goto('/demo/index.html');
  await page.locator('#tab-credentials').click();
  await page.locator('arazzo-credentials-table').waitFor();
  await caption(page, 'Goal — register a source connection', 3000);

  await page.locator('arazzo-credentials-table').getByRole('button', { name: /new credential/i }).click();
  await page.locator('arazzo-credential-dialog dialog').waitFor();
  await beat(page);
  await caption(page, 'New credential — references and non-secret metadata only, never a secret', 4000);

  await page.locator('arazzo-credential-dialog #sourceName').fill('petstore');
  await beat(page, 700);
  await page.locator('arazzo-credential-dialog #environment').fill('production');
  await beat(page, 700);
  await page.locator('arazzo-credential-dialog #authKind').fill('apiKey');
  await page.locator('arazzo-credential-dialog #authKind').press('Tab');
  await page.locator('arazzo-credential-dialog .refs .scheme').first().waitFor();
  await beat(page);
  await caption(page, 'Name it and pick the auth kind — that decides which secret is needed', 4000);

  await page.locator('arazzo-credential-dialog .refs .scheme').first().selectOption('raw');
  await beat(page);
  await page.locator('arazzo-credential-dialog .refs .rawref').first().fill('keyvault://petstore-kv/api-key#a1b2c3');
  await beat(page);
  await caption(page, 'Point at the secret in your store — the control plane stores only the reference', 4200);

  await page.locator('arazzo-credential-dialog .usage-grantee .q').fill('payments');
  await page.locator('arazzo-credential-dialog .usage-grantee ul.results li[role="option"]').first().waitFor();
  await beat(page);
  await caption(page, 'Choose whose runs may use it — a real team, resolved to its exact identity', 4200);
  await page.locator('arazzo-credential-dialog .usage-grantee ul.results li[role="option"]').first().click();
  await beat(page);

  await caption(page, 'Create the connection', 2800);
  await page.locator('arazzo-credential-dialog .confirm').click();
  await beat(page, 1600);
  await caption(page, 'Registered — it now appears in the Sources list', 4000);
  await beat(page, 800);
});
