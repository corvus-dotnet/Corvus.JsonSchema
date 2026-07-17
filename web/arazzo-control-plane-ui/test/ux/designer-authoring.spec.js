// UX suite — the workflow DESIGNER's authoring surface, one level deeper than smoke.spec.js's
// designer coverage (drag-templating, schema-editor basics, library $ref picker, JSON-tier
// problems). Here: the workspace table's create flows, autosave/rename narration, the step and
// workflow inspectors' round-trips, canvas gestures (node drag persistence, port-drawn action
// edges, keyboard delete), toolbar undo/redo, the Text tab as a true peer of the canvas, the
// Sources panel's attach/detach lifecycle (registry reuse, jsonschema attachments), and a
// positioned external-schema-reference finding that click-navigates.
//
// Every selector is grounded in demo/designer.html and src/components/*.js; the seeded document is
// demo/designer-fixture.js ("Order processing": place-order with validate-order → authorize-payment
// → await-confirmation → capture-payment + manual-review, two attached sources).
import { test, expect } from '@playwright/test';
import { watchErrors, assertClean, openDesigner } from './ux-helpers.js';

const SCHEMA_EDITOR = 'arazzo-workflow-inspector arazzo-schema-editor';

/** Scope the inspector to the workflow inputs (the START pseudo-node sits off-canvas until fit, so
 *  drive the surface's selection event — the smoke suite's established pattern), then wait for the
 *  nested schema editor to mount. */
async function selectStartNode(page) {
  await page.locator('#surface .node').first().waitFor({ state: 'attached' });
  await page.evaluate(() => {
    document.querySelector('#surface').dispatchEvent(new CustomEvent('selection-changed', {
      detail: { selection: { type: 'node', id: '#start' } }, bubbles: true, composed: true,
    }));
  });
  await expect.poll(async () => page.evaluate(() =>
    !!document.querySelector('arazzo-workflow-inspector')?.shadowRoot?.querySelector('arazzo-schema-editor'),
  )).toBe(true);
}

// ── the workspace table ───────────────────────────────────────────────────────────────────────────

test('New workflow creates a named blank working copy that opens on the empty-canvas teaching state', async ({ page }) => {
  const errors = watchErrors(page);
  await page.goto('/demo/designer.html');
  const wctable = page.locator('arazzo-workspace-table');
  await expect(wctable.getByText('Order processing')).toBeVisible(); // the seed landed; the mock is ready

  // The create flow asks for a name up front through the kit's standard dialog (no system prompt).
  await wctable.locator('button.new').click();
  const ask = wctable.locator('arazzo-input-dialog');
  await expect(ask.locator('dialog')).toBeVisible();
  await ask.locator('input.in-field').fill('inventory-sync');
  await ask.locator('button.confirm').click();

  // It opens straight into the designer: named by its title, saved, and teaching the happy path
  // (steps come from a source's operations) instead of staring blankly.
  await expect(page.locator('#surface')).toBeVisible();
  await expect(page.locator('#wc-name')).toHaveText('inventory-sync');
  await expect(page.locator('#save-status')).toHaveText('all changes saved');
  await expect(page.locator('#canvas-empty')).toBeVisible();
  await expect(page.locator('#canvas-empty')).toContainText('An empty canvas');
  await expect(page.locator('#canvas-empty-cta')).toBeVisible();

  // Validation is live from the first look: a blank document has structural findings (empty
  // sourceDescriptions/workflows), so the Problems badge lights without any button press.
  await expect(page.locator('#problems-badge')).toBeVisible({ timeout: 8000 });

  // Back in the workspace the new copy is listed.
  await page.locator('#back').click();
  await expect(wctable.getByText('inventory-sync')).toBeVisible();
  assertClean(errors);
});

test('New working copy… carries a catalog version in: its document projects and its registry sources resolve without re-upload', async ({ page }) => {
  const errors = watchErrors(page);
  await page.goto('/demo/designer.html');
  const wctable = page.locator('arazzo-workspace-table');
  await expect(wctable.getByText('Order processing')).toBeVisible();

  await wctable.locator('button.fromcat').click();
  const dialog = wctable.locator('.fromcat-dialog');
  await expect(dialog).toBeVisible();
  await wctable.locator('.fc-table tbody tr[data-key="adopt-pet"]').click();
  await expect(wctable.locator('.fc-name')).toHaveValue('Adopt a Pet'); // the pick names the copy
  await wctable.locator('.fc-create').click();

  // The carried document projects onto the canvas — the adopt-pet steps are real nodes.
  await expect(page.locator('#surface .node[data-id="findPet"]')).toBeVisible();
  await expect(page.locator('#surface .node[data-id="confirmAdoption"]')).toBeVisible();
  await expect(page.locator('#save-status')).toHaveText('all changes saved');

  // Its source rode over as a REGISTRY attachment: the rail resolves petstore's operation surface
  // with no re-upload (a lone source auto-expands).
  await page.locator('[data-tab="sources"]').click();
  const browser = page.locator('arazzo-operation-browser');
  await expect(browser.locator('.group-head[data-name="petstore"]')).toBeVisible();
  await expect(browser.locator('button.op', { hasText: 'listPets' })).toBeVisible();
  assertClean(errors);
});

test('autosave narrates unsaved → saved, and editing info.title on the settings page renames the working copy', async ({ page }) => {
  const errors = watchErrors(page);
  await openDesigner(page);
  await expect(page.locator('#save-status')).toHaveText('all changes saved');

  // An open starts on the settings page (empty selection = the document); info.title lives there.
  const title = page.locator('arazzo-document-inspector input.ititle');
  await expect(title).toHaveValue('Order processing');
  await title.fill('Order processing revised');

  // The title bar tracks the DOCUMENT's title live, and the autosave narration runs its arc.
  await expect(page.locator('#wc-name')).toHaveText('Order processing revised');
  await expect(page.locator('#save-status')).toHaveText('unsaved…');
  await expect(page.locator('#save-status')).toHaveText(/^saved \d/, { timeout: 5000 });

  // name ≡ the document's title: the workspace table lists the copy under its new name.
  await page.locator('#back').click();
  await expect(page.locator('arazzo-workspace-table').getByText('Order processing revised')).toBeVisible();
  assertClean(errors);
});

// ── the step inspector ────────────────────────────────────────────────────────────────────────────

test('selecting a step shows its binding and parameters; parameter, criteria, and outputs edits round-trip and the node chips track', async ({ page }) => {
  const errors = watchErrors(page);
  await openDesigner(page);
  const node = page.locator('#surface .node[data-id="validate-order"]');
  await node.click();

  // Selection auto-switches the sidebar to Inspect and scopes it to the step: the binding is the
  // step's own operationId; the declared complex parameter renders with a FIXED head (name/in from
  // the source binding, not typed in).
  await expect(page.locator('.side-tabs [data-tab="inspect"]')).toHaveClass(/active/);
  await expect(page.locator('#inspector')).toContainText('step — validate-order');
  const inspector = page.locator('arazzo-step-inspector');
  await expect(inspector.locator('.binding input.bval')).toHaveValue('validateOrder');
  await expect(inspector.locator('.prow .pfixed')).toContainText('checks');
  await expect(inspector.locator('.prow .pfixed')).toContainText('query');

  // Edit the custom parameter's value expression (orderId is undeclared → editable name + value).
  const paramRow = inspector.locator('.prow').first();
  await expect(paramRow.locator('input.pname')).toHaveValue('orderId');
  const paramValue = paramRow.locator('arazzo-expression-input');
  await expect(paramValue).toContainText('$inputs.orderId');
  await paramValue.click();
  await page.keyboard.press('ControlOrMeta+a');
  await page.keyboard.type('$inputs.customerEmail');

  // Add a success criterion — the node's ✓ chip counts it immediately.
  await inspector.locator('.crit arazzo-criteria-editor button.add').click();
  const newCriterion = inspector.locator('.crit arazzo-criteria-editor .row').nth(1);
  await newCriterion.locator('arazzo-expression-input.cond').click();
  await page.keyboard.type('$response.body#/validated == true');
  await expect(node.locator('.chips')).toContainText('✓ 2');

  // Add an output — a row is an output once it has a name; the → chip tracks.
  await inspector.locator('.outs arazzo-outputs-editor button.add').click();
  await page.keyboard.type('validatedAt'); // the add focused the new name input
  await expect(node.locator('.chips')).toContainText('→ 2');

  // Round-trip: select away and back — the inspector rebuilds from the document with every edit.
  await page.locator('#surface .node[data-id="authorize-payment"]').click();
  await expect(page.locator('#inspector')).toContainText('step — authorize-payment');
  await node.click();
  await expect(page.locator('#inspector')).toContainText('step — validate-order');
  const reopened = page.locator('arazzo-step-inspector');
  await expect(reopened.locator('.prow').first().locator('arazzo-expression-input')).toContainText('$inputs.customerEmail');
  await expect(reopened.locator('.crit arazzo-criteria-editor .row')).toHaveCount(2);
  await expect(reopened.locator('.crit arazzo-criteria-editor .row').nth(1).locator('arazzo-expression-input.cond')).toContainText('$response.body#/validated == true');
  await expect(reopened.locator('.outs arazzo-outputs-editor .orow input.oname').nth(1)).toHaveValue('validatedAt');
  assertClean(errors);
});

// ── canvas gestures ───────────────────────────────────────────────────────────────────────────────

test('a dragged node position rides designerState: it survives leaving and re-opening the working copy', async ({ page }) => {
  const errors = watchErrors(page);
  await openDesigner(page);
  const node = page.locator('#surface .node[data-id="validate-order"]');
  await expect(node).toBeVisible();

  const before = await page.evaluate(() => ({ ...document.getElementById('surface').positions['validate-order'] }));
  const box = await node.boundingBox();
  await page.mouse.move(box.x + box.width / 2, box.y + box.height / 2);
  await page.mouse.down();
  await page.mouse.move(box.x + box.width / 2 + 90, box.y + box.height / 2 + 60, { steps: 8 });
  await page.mouse.up();

  const moved = await page.evaluate(() => ({ ...document.getElementById('surface').positions['validate-order'] }));
  expect(moved).not.toEqual(before);

  // The move is a model edit like any other: it dirties the copy and autosaves.
  await expect(page.locator('#save-status')).toHaveText(/^saved \d/, { timeout: 5000 });

  // Leave and re-open: the pinned position comes back from the stored designerState.
  await page.locator('#back').click();
  await page.locator('arazzo-workspace-table').getByText('Order processing').click();
  await expect(page.locator('#surface')).toBeVisible();
  await expect(page.locator('#surface .node[data-id="validate-order"]')).toBeVisible();
  const reopened = await page.evaluate(() => ({ ...document.getElementById('surface').positions['validate-order'] }));
  expect(reopened).toEqual(moved);
  assertClean(errors);
});

test('drawing from a success port authors a goto action edge (✓ always), the inspector edits it, and Delete removes it', async ({ page }) => {
  const errors = watchErrors(page);
  await openDesigner(page);
  await expect(page.locator('#surface .node[data-id="validate-order"]')).toBeVisible();

  // The seeded failure edge already carries the kind glyph — edge kind is never colour-only.
  const declined = page.locator('#surface .edge[data-id="goto:authorize-payment:manual-review-on-decline:failure"]');
  await expect(declined.locator('.elabel')).toContainText('✗');
  await expect(declined.locator('.elabel')).toContainText('$statusCode == 402');

  // Zoom to 1:1 anchored just above validate-order so the (small) port circles are easy targets.
  await page.evaluate(() => {
    const surface = document.getElementById('surface');
    const a = surface.positions['validate-order'];
    surface.view = { k: 1, tx: -(a.x - 80), ty: -(a.y - 60) };
  });

  // Drag from the success port onto the authorize-payment card.
  const port = page.locator('#surface .node[data-id="validate-order"] .port-success');
  const target = page.locator('#surface .node[data-id="authorize-payment"] rect.card');
  const pbox = await port.boundingBox();
  const tbox = await target.boundingBox();
  await page.mouse.move(pbox.x + pbox.width / 2, pbox.y + pbox.height / 2);
  await page.mouse.down();
  await page.mouse.move(tbox.x + tbox.width / 2, tbox.y + tbox.height / 2, { steps: 10 });
  await page.mouse.up();

  // An unconditional goto action lands in the document and projects as a selected action edge whose
  // label says so: the ✓ kind glyph plus the clickable "always" (no silent missing conditions).
  const edge = page.locator('#surface .edge[data-id="goto:validate-order:goto-authorize-payment:success"]');
  await expect(edge).toBeVisible();
  await expect(edge.locator('.elabel')).toContainText('✓ always');
  await expect(edge).toHaveClass(/selected/);

  // Inspecting the edge opens the action editor on that onSuccess entry, goto target and all.
  await page.evaluate(() => {
    document.querySelector('#surface').dispatchEvent(new CustomEvent('selection-changed', {
      detail: { selection: { type: 'edge', id: 'goto:validate-order:goto-authorize-payment:success' } },
      bubbles: true, composed: true,
    }));
  });
  await expect(page.locator('#inspector')).toContainText('validate-order · onSuccess[0]');
  await expect(page.locator('#inspector arazzo-action-editor select.step')).toHaveValue('step:authorize-payment');

  // Delete removes the ACTION (its onSuccess entry) — the canvas selection still holds the edge.
  await page.locator('#surface svg').press('Delete');
  await expect(page.locator('#surface .edge[data-id="goto:validate-order:goto-authorize-payment:success"]')).toHaveCount(0);
  await expect(page.locator('#selection')).toHaveText('(document)'); // deletion lands back on the settings page
  assertClean(errors);
});

test('deleting a step removes it with its action edges, and the toolbar undo/redo unwind and replay it', async ({ page }) => {
  const errors = watchErrors(page);
  await openDesigner(page);
  const nodes = page.locator('#surface .node');
  await expect(nodes).toHaveCount(7); // 5 steps + start/end pseudo-nodes
  await expect(page.locator('#undo')).toBeDisabled(); // a load is not an edit
  await expect(page.locator('#redo')).toBeDisabled();

  // authorize-payment carries two failure action edges (the 402 goto + the shared escalate ref).
  const outgoing = page.locator('#surface .edge[data-id^="goto:authorize-payment:"]');
  await expect(outgoing).toHaveCount(2);

  await page.locator('#surface .node[data-id="authorize-payment"]').click();
  await page.locator('#surface svg').press('Delete');
  await expect(nodes).toHaveCount(6);
  await expect(outgoing).toHaveCount(0);

  // Undo restores the step and its edges; redo replays the deletion; the buttons track the stack.
  await expect(page.locator('#undo')).toBeEnabled();
  await page.locator('#undo').click();
  await expect(nodes).toHaveCount(7);
  await expect(outgoing).toHaveCount(2);
  await expect(page.locator('#undo')).toBeDisabled();
  await expect(page.locator('#redo')).toBeEnabled();
  await page.locator('#redo').click();
  await expect(nodes).toHaveCount(6);
  await expect(outgoing).toHaveCount(0);
  await expect(page.locator('#redo')).toBeDisabled();
  await expect(page.locator('#undo')).toBeEnabled();
  assertClean(errors);
});

// ── the workflow inspector ────────────────────────────────────────────────────────────────────────

test('the defaults card opens the whole-workflow editor and summary/description edits round-trip', async ({ page }) => {
  const errors = watchErrors(page);
  await openDesigner(page);

  // place-order declares workflow-level failureActions, so the inherited-defaults card renders.
  await page.locator('#surface .defaults').click();
  await expect(page.locator('#inspector')).toContainText('workflow — place-order');
  const inspector = page.locator('arazzo-workflow-inspector');
  await expect(inspector.locator('input.summary')).toHaveValue('The happy path: validate, authorize, await confirmation, capture.');

  await inspector.locator('input.summary').fill('The amended happy path.');
  await inspector.locator('input.wdesc').fill('Now with tightened checks.');

  // Round-trip: select a step, then the defaults card again — the editor rebuilds from the model.
  await page.locator('#surface .node[data-id="validate-order"]').click();
  await expect(page.locator('#inspector')).toContainText('step — validate-order');
  await page.locator('#surface .defaults').click();
  await expect(page.locator('#inspector')).toContainText('workflow — place-order');
  await expect(page.locator('arazzo-workflow-inspector input.summary')).toHaveValue('The amended happy path.');
  await expect(page.locator('arazzo-workflow-inspector input.wdesc')).toHaveValue('Now with tightened checks.');
  assertClean(errors);
});

test('the inputs schema editor authors a property of every primitive type and a oneOf combiner (§15-8, beyond smoke)', async ({ page }) => {
  const errors = watchErrors(page);
  await openDesigner(page);
  await selectStartNode(page);

  const editor = page.locator(SCHEMA_EDITOR);
  const addProperty = editor.locator('button.add', { hasText: 'Add property' });
  const rows = editor.locator('.node.child'); // the root object's property rows (all scalar until the combiner lands)
  await expect(rows).toHaveCount(3); // orderId · amount · customerEmail

  // Each add focuses+selects the fresh name input, so type-then-Tab renames it in place; the
  // rename rebuilds the form, so wait for the named row before touching its type select.
  const addNamed = async (name, index) => {
    await addProperty.click();
    await page.keyboard.type(name);
    await page.keyboard.press('Tab');
    await expect(rows.nth(index).locator('input.name')).toHaveValue(name);
  };

  await addNamed('age', 3);
  await rows.nth(3).locator('select.type').selectOption('integer');
  await addNamed('score', 4);
  await rows.nth(4).locator('select.type').selectOption('number');
  await addNamed('active', 5);
  await rows.nth(5).locator('select.type').selectOption('boolean');
  await addNamed('nickname', 6); // stays the default string

  // A combiner is a first-class row: switching the type to one-of grows variant cards.
  await addNamed('contact', 7);
  await rows.nth(7).locator('select.type').selectOption('oneOf');
  const combiner = rows.nth(7);
  await expect(combiner.locator('.variant')).toHaveCount(1);
  await combiner.locator('button.add', { hasText: 'Add variant' }).click();
  await expect(combiner.locator('.variant')).toHaveCount(2);

  // The canvas start node counts the authored inputs live.
  await expect(page.locator('#surface .node[data-id="#start"]')).toContainText('⇥ 8 inputs');

  // The JSON tier shows exactly what the form authored — the document IS the model.
  await editor.locator('.t-json').click();
  const schema = JSON.parse(await editor.locator('textarea.json').inputValue());
  expect(schema.properties.age.type).toBe('integer');
  expect(schema.properties.score.type).toBe('number');
  expect(schema.properties.active.type).toBe('boolean');
  expect(schema.properties.nickname.type).toBe('string');
  expect(Array.isArray(schema.properties.contact.oneOf)).toBe(true);
  expect(schema.properties.contact.oneOf).toHaveLength(2);
  assertClean(errors);
});

// ── the Text tab ──────────────────────────────────────────────────────────────────────────────────

test('the text tab mirrors a canvas edit, and a typed text edit re-projects the canvas (round-trip)', async ({ page }) => {
  const errors = watchErrors(page);
  await openDesigner(page);

  // Canvas → text: edit a step description through the inspector…
  await page.locator('#surface .node[data-id="validate-order"]').click();
  await page.locator('arazzo-step-inspector input.desc').fill('Tightened stock check.');

  // …and the text tab's buffer (the model's deterministic JSON) carries it.
  await page.locator('#tab-text').click();
  await expect(page.locator('#text .cm-editor')).toBeVisible(); // CM6 mounted (not the fallback)
  await expect.poll(async () => page.evaluate(() => document.getElementById('text').value)).toContain('Tightened stock check.');

  // Text → canvas: revealStep selects the step's `"stepId": …` text (the canvas→text sync seam),
  // so typing over the selection is a genuine editor edit that renames the step.
  await page.evaluate(() => document.getElementById('text').revealStep('capture-payment'));
  await page.keyboard.type('"stepId": "capture-payment-2"');

  // The debounced text-changed reduces to model ops: the canvas re-projects with the renamed node
  // and the edit rides the same undo stack as any other.
  await page.locator('#tab-design').click();
  await expect(page.locator('#surface .node[data-id="capture-payment-2"]')).toBeVisible();
  await expect(page.locator('#surface .node[data-id="capture-payment"]')).toHaveCount(0);
  await expect(page.locator('#undo')).toBeEnabled();
  assertClean(errors);
});

test('an invalid JSON text edit is guarded: the parse problem shows, the model survives, and re-entry restores the buffer', async ({ page }) => {
  const errors = watchErrors(page);
  await openDesigner(page);
  await page.locator('#tab-text').click();
  await expect(page.locator('#text .cm-editor')).toBeVisible();

  // Replace a step's `"stepId": …` text with garbage — the buffer no longer parses.
  await page.evaluate(() => document.getElementById('text').revealStep('validate-order'));
  await page.keyboard.type('!!!');

  // The problem is narrated in place (parse-gated, last-valid-wins) — nothing is destroyed.
  await expect(page.locator('#text .err')).toBeVisible();
  await expect(page.locator('#text .err')).not.toBeEmpty();
  await expect(page.locator('#text .ted')).toHaveClass(/invalid/);

  // The model never took the broken edit: the canvas still shows every step.
  await page.locator('#tab-design').click();
  await expect(page.locator('#surface .node[data-id="validate-order"]')).toBeVisible();
  await expect(page.locator('#surface .node')).toHaveCount(7);

  // Re-entering the text tab refreshes the buffer from the model — valid JSON, problem cleared.
  await page.locator('#tab-text').click();
  await expect(page.locator('#text .err')).toBeHidden();
  await expect.poll(async () => page.evaluate(() => document.getElementById('text').value)).toContain('"stepId": "validate-order"');
  assertClean(errors);
});

// ── the Sources panel ─────────────────────────────────────────────────────────────────────────────

test('the acquisition dialog attaches a REGISTERED source by name (no re-upload) and its operations render for dragging', async ({ page }) => {
  const errors = watchErrors(page);
  await openDesigner(page);
  await page.locator('[data-tab="sources"]').click();
  const browser = page.locator('arazzo-operation-browser');
  await browser.locator('button.add').click();

  // Registry is the dialog's leading mode; picking a source names the attachment after it.
  const dialog = page.locator('#acqdialog');
  await expect(dialog.locator('dialog')).toBeVisible();
  await expect(dialog.locator('select.registry-in')).toContainText('petstore'); // the §7.6 registry loaded
  await dialog.locator('select.registry-in').selectOption('petstore');
  await expect(dialog.locator('.name-in')).toHaveValue('petstore');
  await dialog.locator('button.attach').click();
  await expect(dialog.locator('dialog')).not.toBeVisible();
  await expect(page.locator('#save-status')).toHaveText('source attached');

  // The rail lists the new group; expanding renders its draggable operation rows.
  const head = browser.locator('.group-head[data-name="petstore"]');
  await expect(head).toBeVisible();
  await expect(head.locator('.type')).toHaveText('openapi');
  await head.click();
  const listPets = browser.locator('button.op', { hasText: 'listPets' });
  await expect(listPets).toBeVisible();
  await expect(listPets).toHaveAttribute('draggable', 'true');
  assertClean(errors);
});

test('detaching a source that steps bind is confirm-gated with the consequence spelled out, and the toast restores it', async ({ page }) => {
  const errors = watchErrors(page);
  await openDesigner(page);
  await page.locator('[data-tab="sources"]').click();
  const browser = page.locator('arazzo-operation-browser');
  const payments = browser.locator('.group-head[data-name="payments"]');
  await expect(payments).toBeVisible();

  // The confirm names what breaks (steps bound to its operations lose their surface) AND what stays
  // (the document's sourceDescriptions declaration — a detach never edits the document).
  await payments.locator('button.detach').click();
  const ask = browser.locator('arazzo-input-dialog');
  await expect(ask.locator('dialog')).toBeVisible();
  await expect(ask.locator('.message')).toContainText('Steps bound to its operations lose their surface');
  await expect(ask.locator('.message')).toContainText('the sourceDescriptions declaration stays in the document');

  // Cancelling keeps the attachment.
  await ask.locator('button.cancel').click();
  await expect(payments).toBeVisible();

  // Confirming detaches — and the toast's Restore is the undo story (attachments cannot ride the
  // document's undo stack), re-attaching the stashed document verbatim.
  await payments.locator('button.detach').click();
  await expect(ask.locator('dialog')).toBeVisible();
  await ask.locator('button.confirm').click();
  await expect(browser.locator('.group-head[data-name="payments"]')).toHaveCount(0);
  await expect(page.locator('#save-status')).toHaveText('source detached');
  const toast = page.locator('#toast');
  await expect(toast).toBeVisible();
  await expect(toast).toContainText("Source 'payments' detached");
  await toast.locator('button', { hasText: 'Restore' }).click();
  await expect(browser.locator('.group-head[data-name="payments"]')).toBeVisible();
  await expect(page.locator('#save-status')).toHaveText('source restored');
  assertClean(errors);
});

test('a jsonschema attachment (#94) lists distinctly in the rail and feeds the external-$ref type picker', async ({ page }) => {
  const errors = watchErrors(page);
  await openDesigner(page);
  await page.locator('[data-tab="sources"]').click();
  const browser = page.locator('arazzo-operation-browser');
  await browser.locator('button.add').click();

  // Upload a JSON Schema document: no openapi/asyncapi/arazzo marker — the dialog sniffs its shape.
  const dialog = page.locator('#acqdialog');
  await dialog.locator('[data-mode="upload"]').click();
  const schemaDoc = {
    $id: 'https://example.com/schemas/customer-shapes',
    $schema: 'https://json-schema.org/draft/2020-12/schema',
    $defs: { Customer: { type: 'object', properties: { id: { type: 'string' }, city: { type: 'string' } } } },
  };
  await dialog.locator('.file-in').setInputFiles({
    name: 'customer-shapes.json', mimeType: 'application/json', buffer: Buffer.from(JSON.stringify(schemaDoc)),
  });
  await expect(dialog.locator('.upload-preview')).toContainText('jsonschema');
  await expect(dialog.locator('.name-in')).toHaveValue('customer-shapes');
  await dialog.locator('button.attach').click();

  // Listed DISTINCTLY from operation sources: typed jsonschema, no operation surface.
  const head = browser.locator('.group-head[data-name="customer-shapes"]');
  await expect(head).toBeVisible();
  await expect(head.locator('.type')).toHaveText('jsonschema');
  await expect(head.locator('.count')).toHaveText('0 ops');

  // The inputs schema editor's type menu now offers its $defs under "External schemas", referenced
  // by the document's declared root $id (the preferred form).
  await selectStartNode(page);
  await expect.poll(async () => page.evaluate(() => {
    const se = document.querySelector('arazzo-workflow-inspector')?.shadowRoot?.querySelector('arazzo-schema-editor');
    const sel = se?.shadowRoot?.querySelector('select.type');
    return sel ? [...sel.querySelectorAll('optgroup[label="External schemas"] option')].map((o) => o.value) : [];
  }), { timeout: 8000 }).toContain('xref:https://example.com/schemas/customer-shapes#/$defs/Customer');
  assertClean(errors);
});

// ── Problems ──────────────────────────────────────────────────────────────────────────────────────

test('a dangling schemas/<name> $ref surfaces a positioned external-schema error that click-navigates to the inputs editor', async ({ page }) => {
  const errors = watchErrors(page);
  await openDesigner(page);
  await selectStartNode(page);

  // Author an inputs schema referencing an external schema document that is NOT attached — the
  // JSON tier commits the moment it parses (schema-changed → autosave → validate).
  const editor = page.locator(SCHEMA_EDITOR);
  await editor.locator('.t-json').click();
  await editor.locator('textarea.json').fill('{"$ref": "schemas/missing#/$defs/Customer"}');

  // The mirror of the server's pass-4 walk finds it: an ERROR, positioned at the workflow's inputs.
  const badge = page.locator('#problems-badge');
  await expect.poll(async () => Number((await badge.textContent().catch(() => '0')) || '0'), { timeout: 8000 }).toBeGreaterThan(0);
  await page.locator('[data-tab="problems"]').click();
  const finding = page.locator('#problems button', { hasText: 'external schema' });
  await expect(finding).toBeVisible();
  await expect(finding).toContainText('/workflows/0/inputs');
  await expect(finding).toContainText("'missing'");

  // Clicking the finding puts the designer ON it: the inputs editor opens (START anchor) showing
  // the unresolvable reference row.
  await finding.click();
  await expect(page.locator('.side-tabs [data-tab="inspect"]')).toHaveClass(/active/);
  await expect(page.locator('#panel-inspect')).toBeVisible();
  const refRow = page.locator(`${SCHEMA_EDITOR} .ghost`);
  await expect(refRow).toContainText('external schema');
  await expect(refRow).toContainText('schemas/missing#/$defs/Customer');
  await expect(refRow).toContainText('not attached');
  assertClean(errors);
});

test('the canvas key stays bounded on a narrow viewport and dismisses on any outside interaction', async ({ page }) => {
  const errors = watchErrors(page);
  await page.setViewportSize({ width: 390, height: 844 }); // phone-sized: the card must not own the canvas
  await openDesigner(page);

  await page.locator('#legend-btn').click();
  const legend = page.locator('#canvas-legend');
  await expect(legend).toBeVisible();
  const box = await legend.boundingBox();
  expect(box.width).toBeLessThanOrEqual(390 - 56 + 1); // never edge-to-edge
  const canvas = await page.locator('#surface').boundingBox();
  expect(box.height).toBeLessThanOrEqual(canvas.height * 0.5 + 1); // never half the canvas tall

  // Tapping the canvas (any outside interaction) dismisses; so does Escape.
  await page.locator('#surface').click({ position: { x: 30, y: 30 } });
  await expect(legend).toBeHidden();
  await page.locator('#legend-btn').click();
  await expect(legend).toBeVisible();
  await page.keyboard.press('Escape');
  await expect(legend).toBeHidden();
  assertClean(errors);
});
