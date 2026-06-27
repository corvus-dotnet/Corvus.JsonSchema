// Captioned UX walkthrough clips for docs/control-plane/ux-review.md §8 — driven against the in-browser demo mock.
// Each test is one short clip; caption(...) injects an on-screen banner per step so the recorded video is captioned.
import { test } from '@playwright/test';

// Inject / update a fixed caption banner in the light DOM (it overlays the shadow-DOM components), then hold it on
// screen so it reads in the video. A short fade-in keeps cuts from feeling abrupt.
async function caption(page, text, holdMs = 1700) {
  await page.evaluate((t) => {
    let el = document.getElementById('__cap');
    if (!el) {
      el = document.createElement('div');
      el.id = '__cap';
      el.style.cssText = [
        'position:fixed', 'left:50%', 'bottom:30px', 'transform:translateX(-50%)', 'z-index:2147483647',
        'background:rgba(13,16,20,0.93)', 'color:#fff', 'font:600 18px/1.45 system-ui,-apple-system,Segoe UI,sans-serif',
        'padding:13px 22px', 'border-radius:11px', 'max-width:78vw', 'text-align:center',
        'box-shadow:0 8px 30px rgba(0,0,0,.45)', 'transition:opacity .25s', 'pointer-events:none',
      ].join(';');
      document.body.appendChild(el);
    }
    el.style.opacity = '0';
    el.textContent = t;
    requestAnimationFrame(() => { el.style.opacity = '1'; });
  }, text);
  await page.waitForTimeout(holdMs);
}

// A small settle pause so each click's effect is on screen before the next caption.
const beat = (page, ms = 650) => page.waitForTimeout(ms);

test('clip-1-approver-inbox', async ({ page }) => {
  await page.goto('/demo/index.html');
  await page.locator('arazzo-control-plane').waitFor();
  await caption(page, 'Arazzo control plane — the operator console', 1900);

  await page.locator('#tab-access').click();
  await beat(page);
  await caption(page, 'Access — approvals, scopes and grants');

  // The access-requests panel opens on "My requests"; switch to the approver queue (the inbox).
  await page.locator('arazzo-access-requests .tab-queue').click();
  await beat(page);
  await caption(page, 'The approver queue is an inbox — everything you can act on', 2000);
  await caption(page, 'Pending requests across every workflow you administer — no workflow to pick first', 2200);

  // Approve the pending request in place.
  await page.locator('arazzo-access-requests .act[data-action="approve"]').first().click();
  await beat(page);
  await caption(page, 'Approve, make eligible, or deny — in place, with an optional note', 2100);
  await page.locator('arazzo-access-requests .decision-dialog .reason-in').fill('On-call retry — approved for the incident window.');
  await beat(page);
  await page.locator('arazzo-access-requests .decision-dialog .ok').click();
  await beat(page, 900);
  await caption(page, '…and the queue clears to inbox-zero', 2400);
});

test('clip-2-diagnose-recover-fault', async ({ page }) => {
  await page.goto('/demo/index.html');
  await page.locator('arazzo-runs-table tbody tr[data-id]').first().waitFor();
  await caption(page, 'Runs — health at a glance, colour-coded by status', 2000);

  // Open the adopt-pet run faulted mid-flow (rich fault + earlier steps to rewind to).
  await page.locator('arazzo-runs-table tbody tr[data-id="run-b2c3d4e5"]').click();
  await page.locator('arazzo-run-detail [part="fault"]').waitFor();
  await beat(page);
  await caption(page, 'Open a faulted run — the step, the attempt, and the error', 2200);

  await page.locator('arazzo-run-detail').getByRole('button', { name: /resume/i }).click();
  await page.locator('arazzo-resume-dialog dialog').waitFor();
  await beat(page);
  await caption(page, 'Resume — four recovery modes', 1700);

  // Rewind: the step picker lists the workflow's real steps (from the catalog).
  await page.locator('arazzo-resume-dialog input[name="mode"][value="Rewind"]').check();
  await beat(page);
  await caption(page, 'Rewind to an earlier step — picked by name, from the catalog', 2100);

  // Skip + record outputs: a schema-typed builder, not free-form JSON.
  await page.locator('arazzo-resume-dialog input[name="mode"][value="Skip"]').check();
  await page.locator('arazzo-resume-dialog input.record-outputs').check();
  await beat(page);
  await caption(page, 'Skip past it, recording outputs — validated against the step’s schema', 2300);

  // Back to the common case and recover.
  await page.locator('arazzo-resume-dialog input[name="mode"][value="RetryFaultedStep"]').check();
  await beat(page);
  await caption(page, 'Or just retry the faulted step — the common case', 1900);
  await page.locator('arazzo-resume-dialog').getByRole('button', { name: 'Resume', exact: true }).click();
  await beat(page, 900);
  await caption(page, 'Recovered — back in flight', 2200);
});

test('clip-3-register-connection', async ({ page }) => {
  await page.goto('/demo/index.html');
  await page.locator('#tab-credentials').click();
  await page.locator('arazzo-credentials-table').waitFor();
  await caption(page, 'Sources — the credentials a workflow’s runs use', 2000);

  await page.locator('arazzo-credentials-table').getByRole('button', { name: /new credential/i }).click();
  await page.locator('arazzo-credential-dialog dialog').waitFor();
  await beat(page);
  await caption(page, 'New credential — references and metadata only, never a secret', 2400);

  await page.locator('arazzo-credential-dialog #sourceName').fill('petstore');
  await page.locator('arazzo-credential-dialog #environment').fill('production');
  await page.locator('arazzo-credential-dialog #authKind').fill('apiKey');
  await page.locator('arazzo-credential-dialog #authKind').press('Tab');
  await beat(page);
  await caption(page, 'The auth kind sets which secret is needed', 1900);

  // Point the secret at a store — a reference, not the secret itself.
  await page.locator('arazzo-credential-dialog .refs .scheme').first().selectOption('raw');
  await beat(page);
  await page.locator('arazzo-credential-dialog .refs .rawref').first().fill('keyvault://petstore-kv/api-key#a1b2c3');
  await beat(page);
  await caption(page, 'You point at your secret store: keyvault://petstore-kv/api-key', 2500);
  await caption(page, 'The control plane stores the reference; it never reads the secret', 2400);
});
