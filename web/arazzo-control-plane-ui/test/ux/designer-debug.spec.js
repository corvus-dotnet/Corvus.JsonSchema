// UX suite — the designer's DEBUGGER, deeper than the smoke arcs: the run-inputs dialog's
// environment gating and readiness row, canvas repainting while scrubbing a recorded trace, the
// context pane's exchanges + criterion truth table, live breakpoint pauses, single-stepping
// (⏭ = exactly one step per press — a regression lock), run-to-here, step over with provided
// outputs, the expression console's ⏎ semantics, §18 debug-run lifecycle verbs (Stop / ✕ Clear /
// fault-only Retry), and the virtual clock riding a retryAfter.
import { test, expect } from '@playwright/test';
import { watchErrors, assertClean, openDesigner, runAgainstMocks, runInDevelopment } from './ux-helpers.js';

test('the run dialog offers only draft-run environments beside the mock target, with per-source readiness', async ({ page }) => {
  const errors = watchErrors(page);
  await openDesigner(page);
  await page.locator('#simulate').click();
  const dlg = page.locator('#run-inputs-dialog');
  await expect(dlg).toBeVisible();

  // §18: the environment select lists the mock target plus ONLY allowsDraftRuns environments —
  // of the seeded four (production, staging, uat, development) just development qualifies.
  const options = page.locator('#run-inputs-env option');
  await expect(options).toHaveCount(2);
  await expect(options.nth(0)).toHaveText('Mocks (simulated)');
  await expect(options.nth(1)).toHaveText(/Development — debug run/);
  expect(await options.evaluateAll((os) => os.map((o) => o.value))).toEqual(['', 'development']);

  // Mock target selected: the §18 hint is hidden and the readiness row carries no content.
  await expect(page.locator('#run-inputs-env-hint')).toBeHidden();
  await expect(page.locator('#run-inputs-readiness')).toBeEmpty();
  // The fault row and readiness row are hidden for the mock target — the ⚡ transient-fault
  // opt-in is meaningless against the simulator (the page's [hidden]{display:none!important}
  // reset keeps the attribute effective over the rows' inline display).
  await expect(page.locator('#run-inputs-fault')).toBeHidden();
  await expect(page.locator('#run-inputs-readiness')).toBeHidden();

  // Switching to the real environment surfaces the debug-run hint, the transient-fault opt-in,
  // and the per-source credential readiness for THAT environment (both sources bound ✓ in dev).
  await page.locator('#run-inputs-env').selectOption('development');
  await expect(page.locator('#run-inputs-env-hint')).toBeVisible();
  await expect(page.locator('#run-inputs-fault-cb')).toBeVisible();
  await expect(page.locator('#run-inputs-fault-cb')).not.toBeChecked();
  const readiness = page.locator('#run-inputs-readiness');
  await expect(readiness).toContainText('Credentials in development');
  await expect(readiness).toContainText(/payments ✓/);
  await expect(readiness).toContainText(/order-events ✓/);

  // Back to mocks: the readiness row switches with the environment (its content clears).
  await page.locator('#run-inputs-env').selectOption('');
  await expect(page.locator('#run-inputs-env-hint')).toBeHidden();
  await expect(readiness).toBeEmpty();

  // Cancel leaves no session behind.
  await dlg.locator('.ri-cancel').click();
  await expect(dlg).not.toBeVisible();
  await expect(page.locator('#debug-dock')).toBeHidden();
  assertClean(errors);
});

test('invalid JSON inputs keep the run dialog open until they parse', async ({ page }) => {
  const errors = watchErrors(page);
  await openDesigner(page);
  // order-with-compensation declares NO inputs schema, so the dialog serves the raw-JSON tier —
  // the one place a syntactically invalid value is possible (typed fields assemble best-effort).
  await page.locator('#workflow').selectOption('order-with-compensation');
  await page.locator('#simulate').click();
  const dlg = page.locator('#run-inputs-dialog');
  await expect(dlg).toBeVisible();
  const editor = page.locator('#run-inputs-editor');
  await expect(editor.locator('.empty')).toContainText('No typed schema available');

  // Unparseable JSON: ▶ Run must NOT start a session — the dialog stays open, the dock stays shut.
  await editor.locator('textarea').fill('{ this is not json');
  await dlg.locator('.ri-run').click();
  await expect(dlg).toBeVisible();
  await expect(page.locator('#debug-dock')).toBeHidden();

  // Once it parses, the same button runs (the sub-workflow bubbles its message wait → suspended).
  await editor.locator('textarea').fill('{}');
  await dlg.locator('.ri-run').click();
  await expect(dlg).not.toBeVisible();
  await expect(page.locator('#debug-dock')).toBeVisible();
  await expect(page.locator('#debugtray .chip')).toContainText(/suspended/);
  assertClean(errors);
});

test('a mock simulate lands the trace in the tray and scrubbing repaints the canvas overlay', async ({ page }) => {
  const errors = watchErrors(page);
  await openDesigner(page);
  await runAgainstMocks(page);

  // The trace settles at the AsyncAPI receive: two executed rows, cursor at the end.
  const tray = page.locator('#debugtray');
  await expect(tray.locator('.chip')).toContainText(/suspended/);
  await expect(tray.locator('.steps .step')).toHaveCount(2);
  await expect(tray.locator('.bar span.muted')).toHaveText('2/2');

  // At the end both executed steps carry done ticks; the walked edges are lit; nothing pulses.
  const surface = page.locator('#surface');
  await expect(surface.locator('svg.debugging').first()).toBeVisible();
  await expect(surface.locator('.node[data-id="validate-order"].st-done-success')).toBeVisible();
  await expect(surface.locator('.node[data-id="authorize-payment"].st-done-success')).toBeVisible();
  await expect(surface.locator('.edge.lit')).toHaveCount(3); // seq:#start · seq:validate-order · seq:authorize-payment
  await expect(surface.locator('.node.st-active')).toHaveCount(0);

  // Scrub BACK one step: the frame repaints — only validate-order is done, its edge trail shrinks,
  // and the step about to run (authorize-payment) pulses with the active halo.
  await tray.locator('.prev').click();
  await expect(tray.locator('.bar span.muted')).toHaveText('1/2');
  await expect(tray.locator('.scrub')).toHaveValue('1');
  await expect(surface.locator('.node[data-id="validate-order"].st-done-success')).toBeVisible();
  await expect(surface.locator('.node[data-id="authorize-payment"].st-active')).toBeVisible();
  await expect(surface.locator('.node[data-id="authorize-payment"].st-done-success')).toHaveCount(0);
  await expect(surface.locator('.edge.lit')).toHaveCount(2);
  // (Playwright reports SVG <g> wrappers as "hidden" — assert the lit class by count, not visibility.)
  await expect(surface.locator('.edge.lit[data-id="seq:validate-order"]')).toHaveCount(1);

  // A trace-row click moves the cursor to "after that step": the row highlights, the canvas follows.
  await tray.locator('.steps .step[data-index="1"]').click();
  await expect(tray.locator('.bar span.muted')).toHaveText('2/2');
  await expect(tray.locator('.steps .step[data-index="1"]')).toHaveClass(/\bat\b/);
  await expect(surface.locator('.node[data-id="authorize-payment"].st-done-success')).toBeVisible();
  await tray.locator('.steps .step[data-index="0"]').click();
  await expect(tray.locator('.steps .step[data-index="0"]')).toHaveClass(/\bat\b/);
  await expect(surface.locator('.node[data-id="authorize-payment"].st-active')).toBeVisible();
  assertClean(errors);
});

test("the context pane shows a step's sent request and its criterion truth table", async ({ page }) => {
  const errors = watchErrors(page);
  await openDesigner(page);

  // Stage REAL inputs through the typed form so the sent request is deterministic.
  await page.locator('#simulate').click();
  const dlg = page.locator('#run-inputs-dialog');
  await expect(dlg).toBeVisible();
  await dlg.locator('#run-inputs-editor input[type="text"]').fill('o-42');
  await dlg.locator('#run-inputs-editor input[type="number"]').fill('19.5');
  await dlg.locator('.ri-run').click();
  await expect(page.locator('#debug-dock')).toBeVisible();

  const tray = page.locator('#debugtray');
  await expect(tray.locator('.chip')).toContainText(/suspended/);

  // The cursor rests after authorize-payment: the exchange shows the method/path AS SENT, the
  // response status, and the request body with the staged inputs resolved into its expressions.
  const ctx = tray.locator('.ctx');
  await expect(ctx).toContainText('exchanges — as sent');
  await expect(ctx).toContainText('POST /payments/authorize');
  await expect(ctx).toContainText('201');
  await expect(ctx).toContainText('"orderId":"o-42"');
  await expect(ctx).toContainText('"amount":19.5');

  // The criterion truth table renders each condition with its judged verdict.
  await expect(ctx).toContainText('success criteria (truth table)');
  await expect(ctx).toContainText('$statusCode == 201');
  await expect(ctx).toContainText('$[?@.status == "authorized"]');
  await expect(ctx.locator('td.ok', { hasText: '✓' }).first()).toBeVisible();

  // And the step's extracted outputs under their runtime-expression name.
  await expect(ctx).toContainText('outputs — $steps.authorize-payment.outputs');
  await expect(ctx).toContainText('authorizationId');

  // Selecting an earlier frame swaps the context: validate-order's exchange carries its resolved
  // deepObject query parameter, and its own truth table.
  await tray.locator('.steps .step[data-index="0"]').click();
  await expect(ctx).toContainText('after step 1: validate-order');
  await expect(ctx).toContainText('/orders/validate?checks=');
  await expect(ctx).toContainText('$statusCode == 200');
  assertClean(errors);
});

test('a canvas breakpoint pauses ▶ before the step, live; clearing it lets the run flow on', async ({ page }) => {
  const errors = watchErrors(page);
  await openDesigner(page);

  // Toggle the breakpoint dot on authorize-payment — the node adorns bp-on.
  const surface = page.locator('#surface');
  await surface.locator('.node[data-id="authorize-payment"] .bp').click();
  await expect(surface.locator('.node.bp-on')).toHaveCount(1);
  await expect(surface.locator('.node[data-id="authorize-payment"].bp-on')).toBeVisible();

  // ▶ Run: the trace pauses BEFORE the breakpointed step — live, no scrubbing — with the halo
  // pulsing on it and only the step already executed listed in the tray.
  await runAgainstMocks(page);
  const tray = page.locator('#debugtray');
  await expect(tray.locator('.chip')).toContainText('⏸ paused before authorize-payment');
  await expect(tray.locator('.steps .step')).toHaveCount(1);
  await expect(surface.locator('.node[data-id="authorize-payment"].st-active')).toBeVisible();
  await expect(surface.locator('.node[data-id="validate-order"].st-done-success')).toBeVisible();

  // Clear the breakpoint (toggle off) and press ▶ again: the session replays with no stops and
  // settles at the workflow's natural wait instead.
  await surface.locator('.node[data-id="authorize-payment"] .bp').click();
  await expect(surface.locator('.node.bp-on')).toHaveCount(0);
  await page.locator('#simulate').click(); // a live session replays directly — no dialog
  await expect(tray.locator('.chip')).toContainText(/suspended/);
  await expect(tray.locator('.steps .step')).toHaveCount(2);
  assertClean(errors);
});

test('⏭ single-step advances exactly one step per press (twice)', async ({ page }) => {
  const errors = watchErrors(page);
  await openDesigner(page);

  // Pause before the FIRST step, so every ⏭ from here is a one-step budget replay.
  await page.locator('#surface .node[data-id="validate-order"] .bp').click();
  await runAgainstMocks(page);
  const tray = page.locator('#debugtray');
  await expect(tray.locator('.chip')).toContainText('⏸ paused before validate-order');
  await expect(tray.locator('.steps .step')).toHaveCount(0);
  await expect(tray.locator('.bar span.muted')).toHaveText('0/0');

  // First press: exactly ONE step executes (this regressed once — ▶-semantics leaked into ⏭).
  await page.locator('#step').click();
  await expect(tray.locator('.steps .step')).toHaveCount(1);
  await expect(tray.locator('.bar span.muted')).toHaveText('1/1');
  await expect(tray.locator('.chip')).toContainText('⏸ paused (budget) before authorize-payment');

  // Second press: exactly one more.
  await page.locator('#step').click();
  await expect(tray.locator('.steps .step')).toHaveCount(2);
  await expect(tray.locator('.bar span.muted')).toHaveText('2/2');
  await expect(tray.locator('.chip')).toContainText('⏸ paused (budget) before await-confirmation');
  assertClean(errors);
});

test('double-clicking a node runs to before it (run-to-here)', async ({ page }) => {
  const errors = watchErrors(page);
  await openDesigner(page);

  // No session yet: run-to-here IS the session starter — the double tap composes
  // until.beforeStepId and the replay pauses at the threshold of the activated node.
  const node = page.locator('#surface .node[data-id="authorize-payment"]');
  await node.click(); // settle the selection (the side panel opening shifts layout) before the second tap
  await node.dblclick();

  await expect(page.locator('#debug-dock')).toBeVisible();
  const tray = page.locator('#debugtray');
  await expect(tray.locator('.chip')).toContainText('⏸ paused before authorize-payment');
  await expect(tray.locator('.steps .step')).toHaveCount(1);
  await expect(page.locator('#surface .node[data-id="authorize-payment"].st-active')).toBeVisible();
  assertClean(errors);
});

test('step over provides outputs in the context pane and the replay marks the step skipped', async ({ page }) => {
  const errors = watchErrors(page);
  await openDesigner(page);
  await runAgainstMocks(page);
  const tray = page.locator('#debugtray');
  await expect(tray.locator('.chip')).toContainText(/suspended/);

  // The context pane offers step over on the frame's step (authorize-payment at the cursor); the
  // inline editor opens TYPED by the step's declared outputs (the working-copy schemas endpoint
  // maps each declared output name to its schema, so authorize-payment gets one field:
  // authorizationId). The document declares no shape for that output, so the field itself is a
  // per-output JSON value editor — seeded with the step's OBSERVED output (the mock's
  // schema-shaped body skeleton, the empty string).
  await tray.locator('.override').click();
  const form = tray.locator('.ovr-form');
  await expect(form).toBeVisible();
  await expect(form.locator('.why')).toContainText("typed by authorize-payment's declared outputs");
  const field = form.locator('.ovr-editor .field', { hasText: 'authorizationId' });
  await expect(field).toBeVisible();
  const valueIn = field.locator('textarea');
  await expect(valueIn).toHaveValue('""'); // seeded from the observed outputs

  // Provide outputs and replay: the step must NOT execute this time.
  await valueIn.fill('"auth-override-1"');
  await form.locator('.ovr-apply').click();

  // The replay lands (suspended at the wait again) with authorize-payment SKIPPED: the row wears
  // the ⏭ marker and the context pane shows the provided outputs on the record.
  await expect(tray.locator('.steps .step')).toHaveCount(2);
  const skippedRow = tray.locator('.steps .step[data-index="1"]');
  await expect(skippedRow).toContainText('authorize-payment');
  await expect(skippedRow.locator('[title="stepped over — outputs provided"]')).toBeVisible();
  const ctx = tray.locator('.ctx');
  await expect(ctx).toContainText('after step 2: authorize-payment (stepped over — outputs provided)');
  await expect(ctx).toContainText('auth-override-1');
  // No exchange happened for a stepped-over step.
  await expect(ctx).not.toContainText('exchanges — as sent');
  assertClean(errors);
});

test('the expression console evaluates against the frame at the scrub cursor on ⏎', async ({ page }) => {
  const errors = watchErrors(page);
  await openDesigner(page);

  // ⏎ with no session explains itself rather than failing silently.
  const expr = page.locator('#expr');
  const result = page.locator('#expr-result');
  // The console lives in the dock's session rail — visible only once the dock opens; evaluate the
  // no-session case through a session-less trace? No: the dock is hidden pre-run, so start the
  // session first and use cursor positions for the frame semantics.
  await runAgainstMocks(page);
  const tray = page.locator('#debugtray');
  await expect(tray.locator('.chip')).toContainText(/suspended/);

  // At the end of the trace (2/2) the step's outputs resolve — the mock's schema-shaped body
  // gives authorizationId its skeleton value "".
  await expr.click();
  await page.keyboard.press('ControlOrMeta+a');
  await page.keyboard.type('$steps.authorize-payment.outputs.authorizationId');
  await page.keyboard.press('Escape'); // close any completion popup so ⏎ commits
  await page.keyboard.press('Enter');
  await expect(result).toBeVisible();
  await expect(result.locator('div').first()).toContainText('$steps.authorize-payment.outputs.authorizationId @ 2/2 = ""');

  // Scrub BACK before authorize-payment ran: the SAME expression now misses — the console
  // evaluates against the frame at the cursor, not the final state.
  await tray.locator('.prev').click();
  await expect(tray.locator('.bar span.muted')).toHaveText('1/2');
  await expr.click();
  await page.keyboard.press('Escape');
  await page.keyboard.press('Enter');
  await expect(result.locator('div').first()).toContainText(/@ 1\/2 — /);
  assertClean(errors);
});

test('§18 Stop cancels the debug run and detaches the dock', async ({ page }) => {
  const errors = watchErrors(page);
  await openDesigner(page);
  await runInDevelopment(page);
  await expect(page.locator('#save-status')).toHaveText(/debug run suspended in development/i, { timeout: 5000 });

  // A healthy pause offers no fault remediation: ↻ Retry and ⚕ Remediate stay hidden.
  await expect(page.locator('#retry-faulted')).toBeHidden();
  await expect(page.locator('#remediate')).toBeHidden();
  await expect(page.locator('#stop')).toBeVisible();

  // ■ Stop cancels the durable run and detaches: the dock closes and the session controls retire.
  await page.locator('#stop').click();
  await expect(page.locator('#debug-dock')).toBeHidden();
  await expect(page.locator('#stop')).toBeHidden();

  // Detached for real: the next ▶ starts a FRESH session (the inputs dialog fronts it again).
  await page.locator('#simulate').click();
  await expect(page.locator('#run-inputs-dialog')).toBeVisible();
  await page.locator('#run-inputs-dialog .ri-cancel').click();
  await expect(page.locator('#debug-dock')).toBeHidden();
  assertClean(errors);
});

test('§18 tray ✕ Clear purges the debug run and empties the dock', async ({ page }) => {
  const errors = watchErrors(page);
  await openDesigner(page);
  await runInDevelopment(page);
  await expect(page.locator('#save-status')).toHaveText(/debug run suspended in development/i, { timeout: 5000 });

  // ✕ Clear purges (deleteDebugRun — the run, its trace, and its draft die server-side; a re-read
  // would 404): the dock clears entirely and the toolbar's session controls retire with it.
  await page.locator('#debugtray .clear').click();
  await expect(page.locator('#debug-dock')).toBeHidden();
  await expect(page.locator('#stop')).toBeHidden();
  await expect(page.locator('#retry-faulted')).toBeHidden();

  // The status line still narrates the last observed state (the purge is silent by design here);
  // a fresh ▶ fronts the inputs dialog — nothing lingers from the purged session.
  await page.locator('#simulate').click();
  await expect(page.locator('#run-inputs-dialog')).toBeVisible();
  await page.locator('#run-inputs-dialog .ri-cancel').click();
  assertClean(errors);
});

test('§18 Retry and Remediate appear only while the debug run is faulted', async ({ page }) => {
  const errors = watchErrors(page);
  await openDesigner(page);
  await runInDevelopment(page, { transientFault: true });

  // The transient fault lands: the toolbar offers BOTH remediation verbs, and the tray's context
  // names the transport-level fault (no status code — connection refused, not an HTTP failure).
  await expect(page.locator('#save-status')).toHaveText(/debug run faulted in development/i, { timeout: 5000 });
  await expect(page.locator('#retry-faulted')).toBeVisible();
  await expect(page.locator('#remediate')).toBeVisible();
  const ctx = page.locator('#debugtray .ctx');
  await expect(ctx).toContainText('fault');
  await expect(ctx).toContainText(/Connection refused/);
  await expect(page.locator('#debugtray .chip')).toContainText(/faulted/);

  // Retry recovers (the endpoint came back): the run is no longer faulted, so the fault-only
  // verbs retire — they are gated on the run's status, not latched by history.
  await page.locator('#retry-faulted').click();
  await expect(page.locator('#save-status')).toHaveText(/debug run paused in development/i, { timeout: 5000 });
  await expect(page.locator('#retry-faulted')).toBeHidden();
  await expect(page.locator('#remediate')).toBeHidden();
  assertClean(errors);
});

test('the virtual clock advances past a retryAfter and the tray narrates it', async ({ page }) => {
  const errors = watchErrors(page);
  await openDesigner(page);

  // No interactive clock-advance control exists in mock mode — the tray renders the trace's
  // recorded advances. Reach one through the ONLY UI path that yields a retry timer: author a
  // scenario whose authorize mock answers 429 then 201, so retry-throttled (retryAfter: 5)
  // fires and the simulator's virtual clock jumps past it.
  await page.locator('aside .side-tabs [data-tab="scenarios"]').click();
  const panel = page.locator('#scpanel');
  await panel.locator('.sc [data-edit="happy-path"]').click();
  const editor = panel.locator('arazzo-scenario-editor');
  await expect(editor).toBeVisible();
  await editor.locator('.toggle:not([disabled])').click(); // the JSON tier, one toggle away
  await editor.locator('textarea.json').fill(JSON.stringify({
    name: 'happy-path',
    description: 'Authorize throttled once, then confirmed and captured.',
    inputs: { orderId: 'o-1001', amount: 42.5 },
    mocks: [
      { source: 'payments', operationId: 'validateOrder', responses: [{ status: 200, body: { validated: true } }] },
      { source: 'payments', operationId: 'authorizePayment', responses: [{ status: 429 }, { status: 201, body: { status: 'authorized', authorizationId: 'auth-77' } }] },
      { source: 'payments', operationId: 'capturePayment', responses: [{ status: 200, body: { receiptId: 'rcpt-9' } }] },
    ],
    triggers: [{ channel: '/channels/orderConfirmations', payload: { confirmed: true, at: '2026-07-01T09:30:00Z' } }],
    expect: { outcome: 'completed' },
  }, null, 2));
  await editor.locator('.save').click();

  // Run it and open its trace in the debug tray.
  await panel.locator('.sc [data-run="happy-path"]').click();
  await expect(panel.locator('.sc', { hasText: 'happy-path' })).toContainText(/passed/i);
  await panel.locator('.sc [data-trace="happy-path"]').first().click();
  await expect(page.locator('#debug-dock')).toBeVisible();

  const tray = page.locator('#debugtray');
  await expect(tray.locator('.chip')).toContainText(/completed/);
  // The throttled attempt and its retry are BOTH recorded; the retry row wears the attempt badge.
  await expect(tray.locator('.steps .step', { hasText: 'authorize-payment' })).toHaveCount(2);
  await expect(tray.locator('.steps .step', { hasText: '↻1' })).toHaveCount(1);
  // And the virtual clock section narrates the jump past retryAfter (5s from the simulated epoch).
  const ctx = tray.locator('.ctx');
  await expect(ctx).toContainText('virtual clock');
  await expect(ctx).toContainText('2020-01-01T00:00:05.000Z (timer due)');
  await expect(ctx).toContainText('timer due');
  assertClean(errors);
});
