// Captioned UX walkthrough clips for docs/control-plane/ux-review.md §8 — driven against the in-browser demo mock.
// Each test is ONE user goal, shown end to end at a deliberate pace.
//
// Timing model (the point of step()): the caption appears, leads the UI change by only a few hundred ms, THEN the
// action fires, THEN it holds so the caption and the resulting UI are read together. The hold is AFTER the action, not
// before — so there is never a long dead wait between a caption and the change it describes. Pure narration / result
// comments use caption() directly (no action to lead).
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
      // Centre horizontally at the top via auto inline margins on a fit-content fixed box — NOT translateX, and never
      // `inset:auto` after top/left (the inset shorthand would reset them, dropping the banner to the corner).
      el.style.cssText = [
        'position:fixed', 'top:22px', 'bottom:auto', 'left:0', 'right:0', 'margin:0 auto',
        'width:fit-content', 'max-width:84vw',
        'background:#0d1014', 'color:#fff',
        'font:600 20px/1.5 system-ui,-apple-system,Segoe UI,Roboto,sans-serif',
        'padding:14px 26px', 'border:1px solid rgba(255,255,255,0.16)', 'border-radius:12px',
        'text-align:center', 'box-shadow:0 12px 44px rgba(0,0,0,0.6)', 'pointer-events:none',
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

// Show the caption, lead the UI change by only `lead` ms, run the action, then hold so the caption and its result are
// on screen together. This is the synced rhythm: caption-just-before-change, never a long pre-action wait.
async function step(page, text, action, { lead = 200, hold = 2600 } = {}) {
  await caption(page, text, lead);
  if (action) await action();
  await page.waitForTimeout(hold);
}

// Scroll a field to the centre of its (possibly overflow:auto) dialog BEFORE its caption, so the viewer sees the field
// the caption describes while it is being described — not only when the action finally auto-scrolls to it.
async function reveal(page, selector) {
  await page.locator(selector).first().evaluate((el) => el.scrollIntoView({ block: 'center', inline: 'nearest' }));
  await page.waitForTimeout(250);
}

// Click Resume… on the open run detail and wait for the resume dialog.
async function openResume(page) {
  await page.locator('arazzo-run-detail').getByRole('button', { name: /resume/i }).click();
  await page.locator('arazzo-resume-dialog dialog').waitFor();
}
const confirmResume = (page) => page.locator('arazzo-resume-dialog').getByRole('button', { name: 'Resume', exact: true }).click();

// ── Goal: approve an access request from the approver inbox ──────────────────────────────────────
test('clip-1-approve-access-request', async ({ page }) => {
  await page.goto('/demo/index.html');
  await page.locator('arazzo-control-plane').waitFor();
  await caption(page, 'Goal — approve a colleague’s request for access to a workflow', 3000);

  await step(page, 'Approvals live under Access — your inbox spans every workflow you administer', async () => {
    await page.locator('#tab-access').click();
    await page.locator('arazzo-access-requests .tab-queue').click();
    await page.locator('arazzo-access-requests .act[data-action="approve"]').first().waitFor();
  }, { hold: 1200 });
  await caption(page, 'The queue opens on Pending — each row is one person asking for scopes on one workflow', 4200);
  await caption(page, 'A row shows who asked, the workflow, and exactly which scopes they want', 3800);

  await step(page, 'We’ll approve this one — granting the requested scopes for a bounded window', async () => {
    await page.locator('arazzo-access-requests .act[data-action="approve"]').first().click();
    await page.locator('arazzo-access-requests .decision-dialog').waitFor();
  }, { hold: 1200 });
  await step(page, 'The decision dialog — add a note for the audit trail so the “why” is recorded', async () => {
    await page.locator('arazzo-access-requests .decision-dialog .reason-in').fill('On-call retry — approved for the incident window.');
  }, { hold: 2400 });
  await step(page, 'Confirm the approval', async () => {
    await page.locator('arazzo-access-requests .decision-dialog .ok').click();
  }, { lead: 350, hold: 1400 });
  await caption(page, 'Approved — a time-boxed grant is written, and the queue clears to inbox-zero', 4400);
});

// ── Goal: recover faulted runs — a realistic case for EACH of the four resume modes, on four different runs ───────
test('clip-2-recover-faulted-run', async ({ page }) => {
  await page.goto('/demo/index.html');
  await page.locator('arazzo-runs-table tbody tr[data-id]').first().waitFor();
  await caption(page, 'Goal — recover faulted runs: a realistic case for each of the four resume modes', 3800);

  await step(page, 'Filter to Faulted — several runs failed, each for a different reason; we’ll recover one per mode', async () => {
    await page.locator('arazzo-control-plane .status-chip[data-status="Faulted"]').click();
    await page.locator('arazzo-runs-table tbody tr[data-id="run-7f3a9c21"]').waitFor();
  }, { hold: 2800 });

  // ── 1 / 4 — Retry: a transient upstream blip; nothing in the run is wrong ──
  await step(page, 'Run 1 — adopt-pet faulted at reservePayment: a 502 from the payments API', async () => {
    await page.locator('arazzo-runs-table tbody tr[data-id="run-7f3a9c21"]').click();
    await page.locator('arazzo-run-detail [part="fault"]').waitFor();
  }, { hold: 2600 });
  await caption(page, 'A 502 is a transient upstream blip — nothing in the run is wrong, so simply run the step again', 4400);
  await step(page, 'Open Resume and keep the default — Retry faulted step', async () => {
    await openResume(page);
    await page.locator('arazzo-resume-dialog input[name="mode"][value="RetryFaultedStep"]').check();
  }, { hold: 2600 });
  await step(page, 'Resume', () => confirmResume(page), { lead: 350, hold: 1600 });
  await caption(page, 'Recovered by Retry — the run is Running again and drops out of the faulted queue', 4200);

  // ── 2 / 4 — Rewind: redo upstream work whose result caused the downstream fault ──
  await step(page, 'Run 2 — onboard-customer faulted at provisionResources: a hard quota in region eu-west-1', async () => {
    await page.locator('arazzo-runs-table tbody tr[data-id="run-dd44ee55"]').click();
    await page.locator('arazzo-run-detail [part="fault"]').waitFor();
  }, { hold: 2800 });
  await caption(page, 'Retrying can’t help — the region was fixed upstream at createAccount, so we must redo that step', 4600);
  await step(page, 'Choose Rewind — go back to an earlier step and re-run forward from there', async () => {
    await openResume(page);
    await page.locator('arazzo-resume-dialog input[name="mode"][value="Rewind"]').check();
    await page.locator('arazzo-resume-dialog .rewind-picker select').waitFor();
  }, { hold: 2000 });
  await reveal(page, 'arazzo-resume-dialog .rewind-picker select');
  await step(page, 'Pick step 0, createAccount — re-create the account in a region with capacity', async () => {
    await page.locator('arazzo-resume-dialog .rewind-picker select').selectOption('0');
  }, { hold: 2600 });
  await step(page, 'Resume', () => confirmResume(page), { lead: 350, hold: 1600 });
  await caption(page, 'Recovered by Rewind — the cursor is back at createAccount and the run is Running', 4200);

  // ── 3 / 4 — Skip with recorded outputs: supply a result produced out of band ──
  await step(page, 'Run 3 — onboard-customer faulted at verifyIdentity: the KYC provider couldn’t read the document', async () => {
    await page.locator('arazzo-runs-table tbody tr[data-id="run-aa11bb22"]').click();
    await page.locator('arazzo-run-detail [part="fault"]').waitFor();
  }, { hold: 2800 });
  await caption(page, 'It can’t pass automatically — but compliance verified the customer by hand, so skip it and record that result', 5000);
  await step(page, 'Choose Skip — advance past the step, supplying the outputs it would have produced', async () => {
    await openResume(page);
    await page.locator('arazzo-resume-dialog input[name="mode"][value="Skip"]').check();
  }, { hold: 2200 });
  await reveal(page, 'arazzo-resume-dialog input.record-outputs');
  await step(page, 'Record outputs — a form typed from verifyIdentity’s own output schema appears', async () => {
    await page.locator('arazzo-resume-dialog input.record-outputs').check();
    await page.locator('arazzo-resume-dialog arazzo-value-editor.skip-builder').waitFor();
  }, { hold: 2600 });
  await reveal(page, 'arazzo-resume-dialog arazzo-value-editor.skip-builder');
  await step(page, 'Mark it verified, by a knowledge-based check, for the named applicant', async () => {
    await page.locator('arazzo-resume-dialog arazzo-value-editor.skip-builder input[type="checkbox"]').first().check();
    await page.locator('arazzo-resume-dialog arazzo-value-editor.skip-builder select').first().selectOption({ label: 'knowledge-based' });
    await page.locator('arazzo-resume-dialog arazzo-value-editor.skip-builder fieldset input[type="text"]').first().fill('Grace Hopper');
  }, { hold: 3200 });
  await step(page, 'Resume', () => confirmResume(page), { lead: 350, hold: 1800 });
  await caption(page, 'Recovered by Skip — the outputs are validated against the schema, then the run advances past the step', 4600);

  // ── 4 / 4 — State-patch: fix a bad value in the run context, then retry ──
  await step(page, 'Run 4 — adopt-pet faulted at submitAdoption: adopter.email was missing from the shelter record', async () => {
    await page.locator('arazzo-runs-table tbody tr[data-id="run-b2c3d4e5"]').click();
    await page.locator('arazzo-run-detail [part="fault"]').waitFor();
  }, { hold: 2800 });
  await caption(page, 'The run context itself is wrong — we have the correct email, so patch it in, then retry', 4600);
  await step(page, 'Choose State patch — an RFC 6902 JSON Patch over the run context (inputs + step outputs)', async () => {
    await openResume(page);
    await page.locator('arazzo-resume-dialog input[name="mode"][value="StatePatch"]').check();
    await page.locator('arazzo-resume-dialog #patch').waitFor();
  }, { hold: 2200 });
  await reveal(page, 'arazzo-resume-dialog #patch');
  await step(page, 'Add the missing adopter email to the run’s inputs', async () => {
    await page.locator('arazzo-resume-dialog #patch').fill('[\n  { "op": "add", "path": "/inputs/adopter/email", "value": "ada@example.com" }\n]');
  }, { hold: 3200 });
  await step(page, 'Resume', () => confirmResume(page), { lead: 350, hold: 1600 });
  await caption(page, 'Recovered by State patch — the context is corrected and the step retried. Four runs, four modes.', 5000);
});

// ── Goal: register a connection — rooted in the workflow's declared source, with its auth derived ─────────────────
const CD = 'arazzo-catalog-detail';                       // the catalog detail (master-detail pane)
const CDLG = 'arazzo-catalog-detail arazzo-credential-dialog'; // its embedded credential dialog
test('clip-3-register-connection', async ({ page }) => {
  await page.goto('/demo/index.html');
  await caption(page, 'Goal — give a workflow’s source the credential it needs to call its API', 3600);

  // Credentials are rooted in the source, which is declared by the workflow — so we start from the workflow.
  await step(page, 'Open the workflow in the Catalog — the source (and its auth) are declared there, not guessed', async () => {
    await page.locator('#tab-catalog').click();
    await page.locator('arazzo-catalog-table tbody tr[data-key="adopt-pet"]').click();
    await page.locator(`${CD} [part="sources"]`).waitFor();
  }, { hold: 1600 });
  await caption(page, 'The Sources section lists what this workflow calls — each from its own OpenAPI / AsyncAPI document', 4600);

  await reveal(page, `${CD} .src[data-name="petstore"]`);
  await caption(page, 'petstore already has a production credential — credentials are reusable, so you’d normally just reuse it', 5000);
  await caption(page, 'Here we’ll add the staging environment. Set up credential opens a dialog already rooted in this source', 5000);

  await step(page, 'Set up a credential for petstore', async () => {
    await page.locator(`${CD} .src[data-name="petstore"] .setup-cred`).click();
    await page.locator(`${CDLG} dialog`).waitFor();
  }, { hold: 1600 });

  // ── Identity: source + auth are NOT typed — they come from the source document ──
  await reveal(page, `${CDLG} #sourceName`);
  await caption(page, 'Source name is fixed to petstore — it must match the workflow’s sourceDescriptions, so it isn’t typed', 4800);
  await reveal(page, `${CDLG} #authKind`);
  await caption(page, 'Auth kind is DERIVED from petstore’s security scheme — apiKey, with its header config — not guessed', 5200);
  await reveal(page, `${CDLG} #environment`);
  await step(page, 'You supply only what the document can’t know — first, the environment: staging', async () => {
    await page.locator(`${CDLG} #environment`).fill('staging');
  }, { hold: 2400 });

  // ── Secret reference (guided Key Vault, walked through part by part) ──
  await caption(page, 'And the secret reference — a Key Vault pointer keyvault://vault/secret#version', 4400);
  await reveal(page, `${CDLG} .refs [data-key="host"]`);
  await step(page, 'Vault — the Key Vault that holds the secret', async () => {
    await page.locator(`${CDLG} .refs [data-key="host"]`).first().fill('petstore-kv');
  }, { hold: 2000 });
  await reveal(page, `${CDLG} .refs [data-key="name"]`);
  await step(page, 'Secret — its name inside that vault', async () => {
    await page.locator(`${CDLG} .refs [data-key="name"]`).first().fill('api-key');
  }, { hold: 2000 });
  await reveal(page, `${CDLG} .refs .refpreview`);
  await caption(page, 'The pointer composes to keyvault://petstore-kv/api-key — and that pointer is all the control plane stores', 5000);

  // ── The out-of-band step the dialog spells out: grant the runner identity read on the secret (§13.5) ──
  await reveal(page, `${CDLG} .refs .refaccess`);
  await caption(page, 'Storing the pointer isn’t enough — the runner reads the secret at run time as its OWN identity', 4800);
  await caption(page, 'So grant that identity get on petstore-kv/api-key (e.g. the “Key Vault Secrets User” role)', 4800);
  await caption(page, 'Writing the secret is a separate, write-capable identity (CI/IaC); the control plane never touches the store', 5200);

  // ── Usage (who may use it) ──
  await reveal(page, `${CDLG} .usage-grantee`);
  await step(page, 'Usage — whose runs may use this connection, resolved to a real identity (not free text)', async () => {
    await page.locator(`${CDLG} .usage-grantee .q`).fill('payments');
    await page.locator(`${CDLG} .usage-grantee ul.results li[data-index]`).first().waitFor();
  }, { hold: 1400 });
  await step(page, 'Pick the Payments team — matched to its exact directory identity', async () => {
    await page.locator(`${CDLG} .usage-grantee ul.results li[data-index]`).first().click();
  }, { hold: 2400 });

  // ── Create ──
  await reveal(page, `${CDLG} .confirm`);
  await step(page, 'Everything here is a reference or metadata — safe to store; create the connection', async () => {
    await page.locator(`${CDLG} .confirm`).click();
  }, { lead: 350, hold: 1800 });
  await reveal(page, `${CD} .src[data-name="petstore"] .src-binds`);
  await caption(page, 'Registered — petstore now has a staging binding alongside production, ready for the workflow’s runs', 5200);
});
