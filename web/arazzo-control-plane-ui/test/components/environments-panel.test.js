// Tier 3 — <arazzo-environments> mounted in a real browser against the in-memory mock.
import { ArazzoControlPlaneClient } from '../../src/arazzo-client.js';
import { createMockControlPlane } from '../../demo/mock-api.js';
import '../../src/components/environments-panel.js';
import { ok, equal, nextEvent, waitFor, mount } from './helpers.js';

const FULL = 'environments:read environments:write availability:read';

function panelWithMock(attrs = { scopes: FULL }) {
  const mock = createMockControlPlane({ latencyMs: 0 });
  const el = document.createElement('arazzo-environments');
  for (const [k, v] of Object.entries(attrs)) el.setAttribute(k, v);
  el.client = new ArazzoControlPlaneClient({ baseUrl: 'https://mock/arazzo/v1', fetch: mock.fetch });
  return el;
}

const rows = (el) => el.shadowRoot.querySelectorAll('.erow');
const detail = (el) => el.shadowRoot.querySelector('.detail-pane');

describe('<arazzo-environments>', () => {
  let el;
  afterEach(() => el?.remove());

  it('lists the seeded environments, ordered by name', async () => {
    el = panelWithMock();
    mount(el);
    await nextEvent(el, 'loaded');
    equal(rows(el).length, 4, 'development + production + staging + uat');
    ok(el.shadowRoot.textContent.includes('Production'), 'shows the display name');
    ok(el.shadowRoot.textContent.includes('production'), 'shows the name');
  });

  it('pages the environment list with Prev/Next over the keyset cursor', async () => {
    el = panelWithMock({ scopes: FULL, 'page-size': '1' });
    mount(el);
    await nextEvent(el, 'loaded');
    equal(rows(el).length, 1, 'page 1 holds one environment');
    const next = el.shadowRoot.querySelector('.next');
    ok(next && !next.disabled, 'Next is enabled when a page follows');
    ok(el.shadowRoot.querySelector('.prev').disabled, 'Prev is disabled on page 1');

    const page2 = nextEvent(el, 'loaded');
    next.click();
    await page2;
    equal(rows(el).length, 1, 'page 2 holds the next environment');
    ok(!el.shadowRoot.querySelector('.next').disabled, 'Next still enabled — uat follows');

    const page3 = nextEvent(el, 'loaded');
    el.shadowRoot.querySelector('.next').click();
    await page3;
    equal(rows(el).length, 1, 'page 3 holds the next environment');
    ok(!el.shadowRoot.querySelector('.next').disabled, 'Next still enabled — a fourth follows');

    const page4 = nextEvent(el, 'loaded');
    el.shadowRoot.querySelector('.next').click();
    await page4;
    ok(el.shadowRoot.querySelector('.next').disabled, 'Next disabled on the last page');

    for (let back = 0; back < 3; back++) {
      const landed = nextEvent(el, 'loaded');
      el.shadowRoot.querySelector('.prev').click();
      await landed;
    }
    ok(el.shadowRoot.querySelector('.prev').disabled, 'Prev disabled again back on page 1');
  });

  it('selecting an environment shows its detail, administrators sub-panel, and availability', async () => {
    el = panelWithMock();
    mount(el);
    await nextEvent(el, 'loaded');
    const selected = nextEvent(el, 'environment-selected');
    el.shadowRoot.querySelector('.erow[data-name="production"]').click();
    await selected;
    ok(detail(el).querySelector('.dtitle').textContent.includes('Production'), 'detail header');
    // The administrators sub-panel renders in environment mode and lists the env admins.
    const admins = detail(el).querySelector('arazzo-administrators-panel');
    ok(admins, 'embeds the administrators panel');
    equal(admins.getAttribute('environment'), 'production', 'in environment mode');
    await waitFor(() => admins.shadowRoot.querySelectorAll('.arow').length === 2);
    // Availability lists the seeded versions made available in production.
    await waitFor(() => detail(el).querySelectorAll('.avail-row').length >= 1);
    ok(detail(el).textContent.includes('adopt-pet'), 'shows an available workflow');
  });

  it('creates an environment via the dialog, selecting it and emitting environment-created', async () => {
    el = panelWithMock();
    mount(el);
    await nextEvent(el, 'loaded');
    el.shadowRoot.querySelector('.new').click();
    ok(el.shadowRoot.querySelector('dialog[open]'), 'the create dialog opens');
    const name = el.shadowRoot.querySelector('.f-name');
    name.value = 'qa';
    name.dispatchEvent(new Event('input'));
    const created = nextEvent(el, 'environment-created');
    el.shadowRoot.querySelector('.confirm').click();
    const e = await created;
    equal(e.detail.environment.name, 'qa', 'the created environment');
    await waitFor(() => [...rows(el)].some((r) => r.dataset.name === 'qa'));
    await waitFor(() => detail(el).querySelector('.dtitle'));
    ok(detail(el).textContent.includes('qa'), 'opens on the new environment');
  });

  it('shows management tags editable in the detail and sets them on create', async () => {
    el = panelWithMock();
    mount(el);
    await nextEvent(el, 'loaded');
    // Production is seeded with a management tag → pre-filled in the editable detail input (writable).
    el.shadowRoot.querySelector('.erow[data-name="production"]').click();
    await nextEvent(el, 'environment-selected');
    await waitFor(() => detail(el).querySelector('.d-mgmt-editor')?.tags?.length);
    const ed = detail(el).querySelector('.d-mgmt-editor');
    equal(ed.tags[0].key, 'team', 'seeded management tag key pre-filled in the editor');
    equal(ed.tags[0].value, 'platform', 'seeded management tag value pre-filled');

    // Create a new environment with a management tag via the dialog input.
    el.shadowRoot.querySelector('.new').click();
    const name = el.shadowRoot.querySelector('.f-name');
    name.value = 'qa'; name.dispatchEvent(new Event('input'));
    const mtags = el.shadowRoot.querySelector('.f-mgmt-editor');
    mtags.tags = [{ key: 'team', value: 'qa' }];
    const created = nextEvent(el, 'environment-created');
    el.shadowRoot.querySelector('.confirm').click();
    const e = await created;
    equal(e.detail.environment.managementTags?.[0]?.key, 'team', 'management tag key persisted');
    equal(e.detail.environment.managementTags?.[0]?.value, 'qa', 'management tag value persisted');
  });

  it('creates an environment requiring evidence and toggles the flag in the detail (§4.6)', async () => {
    el = panelWithMock();
    mount(el);
    await nextEvent(el, 'loaded');
    // Create with the promotion-readiness requirement checked.
    el.shadowRoot.querySelector('.new').click();
    const name = el.shadowRoot.querySelector('.f-name');
    name.value = 'prod-eu'; name.dispatchEvent(new Event('input'));
    const cb = el.shadowRoot.querySelector('.f-requireEvidence');
    cb.checked = true; cb.dispatchEvent(new Event('change'));
    const created = nextEvent(el, 'environment-created');
    el.shadowRoot.querySelector('.confirm').click();
    const e = await created;
    equal(e.detail.environment.requireEvidence, true, 'the flag persisted on create');

    // The detail shows it checked; unchecking + Save clears the requirement.
    await waitFor(() => detail(el).querySelector('.d-requireEvidence'));
    ok(detail(el).querySelector('.d-requireEvidence').checked, 'detail checkbox reflects the flag');
    detail(el).querySelector('.d-requireEvidence').checked = false;
    const changed = nextEvent(el, 'environment-changed');
    detail(el).querySelector('.d-save').click();
    const ch = await changed;
    equal(ch.detail.environment.requireEvidence, false, 'unchecking clears the requirement');
  });

  it('re-tags management tags on update and the change is durable', async () => {
    el = panelWithMock();
    mount(el);
    await nextEvent(el, 'loaded');
    el.shadowRoot.querySelector('.erow[data-name="production"]').click();
    await nextEvent(el, 'environment-selected');
    await waitFor(() => detail(el).querySelector('.d-mgmt-editor')?.tags?.length);
    // Re-tag: replace the seeded team=platform with team=payments via the editor, then Save.
    detail(el).querySelector('.d-mgmt-editor').tags = [{ key: 'team', value: 'payments' }];
    const changed = nextEvent(el, 'environment-changed');
    detail(el).querySelector('.d-save').click();
    const e = await changed;
    equal(e.detail.environment.managementTags?.[0]?.value, 'payments', 're-tag persisted');
    // Durable on refetch: switch away and back; the editor shows the new tag.
    el.shadowRoot.querySelector('.erow[data-name="staging"]').click();
    await nextEvent(el, 'environment-selected');
    el.shadowRoot.querySelector('.erow[data-name="production"]').click();
    await nextEvent(el, 'environment-selected');
    await waitFor(() => detail(el).querySelector('.d-mgmt-editor')?.tags?.length);
    equal(detail(el).querySelector('.d-mgmt-editor').tags[0].value, 'payments', 're-tag durable on refetch');
  });

  it('saves edited metadata and emits environment-changed', async () => {
    el = panelWithMock();
    mount(el);
    await nextEvent(el, 'loaded');
    el.shadowRoot.querySelector('.erow[data-name="staging"]').click();
    await nextEvent(el, 'environment-selected');
    detail(el).querySelector('.d-displayName').value = 'Staging (EU)';
    const changed = nextEvent(el, 'environment-changed');
    detail(el).querySelector('.d-save').click();
    const e = await changed;
    equal(e.detail.environment.displayName, 'Staging (EU)', 'persisted the edit');
  });

  it('deletes an environment after confirmation and emits environment-deleted', async () => {
    el = panelWithMock();
    mount(el);
    await nextEvent(el, 'loaded');
    // A throwaway environment to delete.
    el.shadowRoot.querySelector('.new').click();
    const name = el.shadowRoot.querySelector('.f-name');
    name.value = 'scratch';
    name.dispatchEvent(new Event('input'));
    const created = nextEvent(el, 'environment-created');
    el.shadowRoot.querySelector('.confirm').click();
    await created;
    // submit closed the create dialog and selected 'scratch'; its detail (with Delete) is now shown.
    await waitFor(() => detail(el).querySelector('.d-delete'));
    const deleted = nextEvent(el, 'environment-deleted');
    detail(el).querySelector('.d-delete').click();
    // Drive the themed confirm dialog appended to the shadow root.
    const okBtn = await waitFor(() => el.shadowRoot.querySelector('dialog.arazzo-confirm .ok'));
    okBtn.click();
    const e = await deleted;
    equal(e.detail.name, 'scratch', 'removed the environment');
    await waitFor(() => ![...rows(el)].some((r) => r.dataset.name === 'scratch'));
  });

  // ADR 0068: the environment stores only the override it authored, so the panel reads the budget resource and shows
  // each limit three ways: what was authored, what a run started here now is held to, and what the deployment allows.
  async function openEnvironment(name, attrs) {
    el = panelWithMock(attrs);
    mount(el);
    await nextEvent(el, 'loaded');
    const selected = nextEvent(el, 'environment-selected');
    el.shadowRoot.querySelector(`.erow[data-name="${name}"]`).click();
    await selected;
    return waitFor(() => detail(el).querySelector('.budget'));
  }

  const cell = (col, limit) => detail(el).querySelector(`.budget [data-col="${col}"][data-limit="${limit}"]`).textContent;
  const overrideInput = (limit) => detail(el).querySelector(`.budget input[data-limit="${limit}"]`);
  const type = (input, value) => { input.value = value; input.dispatchEvent(new Event('input')); };

  it('shows each budget limit as the override, the budget in effect, and the ceiling', async () => {
    await openEnvironment('production');
    equal(overrideInput('maxSteps').value, '120', 'the authored override seeds the editor');
    equal(overrideInput('wallClockSeconds').value, '', 'a limit the environment leaves out is empty');
    equal(cell('effective', 'maxSteps'), '120', 'the override tightens the ceiling');
    equal(cell('ceiling', 'maxSteps'), '250', 'the deployment allows more');
    equal(cell('effective', 'wallClockSeconds'), '43200s (12h)', 'a limit left out is the ceilings');
    equal(cell('effective', 'stepTimeoutSeconds'), '30s');
    equal(cell('ceiling', 'maxResponseBytes'), '16,777,216 (16 MiB)');
    equal(detail(el).querySelectorAll('.budget input').length, 6, 'one input per limit');
  });

  it('saves a changed override and shows the budget the server then resolves', async () => {
    await openEnvironment('production');
    type(overrideInput('maxSteps'), '40');
    type(overrideInput('wallClockSeconds'), '600');
    const changed = nextEvent(el, 'environment-changed');
    detail(el).querySelector('.d-save').click();
    await changed;
    await waitFor(() => cell('effective', 'maxSteps') === '40');
    equal(cell('effective', 'wallClockSeconds'), '600s (10m)');
    equal(overrideInput('stepTimeoutSeconds').value, '30', 'the limits not touched are kept: the override is replaced whole');
    const stored = await el.client.getEnvironmentExecutionBudget('production');
    equal(JSON.stringify(stored.override), JSON.stringify({ maxSteps: 40, wallClockSeconds: 600, stepTimeoutSeconds: 30 }));
  });

  it('a save that does not touch the budget does not send it', async () => {
    await openEnvironment('production');
    let sent;
    const update = el.client.updateEnvironment.bind(el.client);
    el.client.updateEnvironment = (name, patch) => { sent = patch; return update(name, patch); };
    el.buildClient = () => el.client;
    const changed = nextEvent(el, 'environment-changed');
    detail(el).querySelector('.d-save').click();
    await changed;
    ok(!('executionBudget' in sent), 'absent leaves the stored override unchanged');
  });

  it('says a limit over the ceiling beside its input and does not save', async () => {
    await openEnvironment('production');
    let saves = 0;
    const update = el.client.updateEnvironment.bind(el.client);
    el.client.updateEnvironment = (name, patch) => { saves++; return update(name, patch); };
    el.buildClient = () => el.client;
    type(overrideInput('maxSteps'), '251');
    detail(el).querySelector('.d-save').click();
    const message = await waitFor(() => detail(el).querySelector('.berr[data-limit="maxSteps"]'));
    ok(message.textContent.includes('ceiling of 250'), 'names the ceiling');
    equal(overrideInput('maxSteps').getAttribute('aria-invalid'), 'true');
    equal(overrideInput('maxSteps').value, '251', 'what was typed survives the repaint');
    equal(saves, 0, 'nothing was sent');

    // Editing the limit clears its message without a repaint.
    type(overrideInput('maxSteps'), '25');
    ok(!detail(el).querySelector('.berr'), 'the message clears as it is edited');
  });

  it('emptying every limit removes the override, and the environment takes the ceiling', async () => {
    await openEnvironment('production');
    for (const input of detail(el).querySelectorAll('.budget input')) type(input, '');
    const changed = nextEvent(el, 'environment-changed');
    detail(el).querySelector('.d-save').click();
    await changed;
    await waitFor(() => cell('effective', 'maxSteps') === '250');
    equal((await el.client.getEnvironmentExecutionBudget('production')).override, undefined);
  });

  it('shows the budget read-only without environments:write', async () => {
    await openEnvironment('production', { scopes: 'environments:read' });
    equal(detail(el).querySelectorAll('.budget input').length, 0, 'no editor');
    equal(detail(el).querySelector('.budget [data-col="override"]').textContent, '120');
    equal(cell('effective', 'maxSteps'), '120');
  });

  it('authors a budget on create, and the draft-run posture reaches the server', async () => {
    el = panelWithMock();
    mount(el);
    await nextEvent(el, 'loaded');
    el.shadowRoot.querySelector('.new').click();
    type(el.shadowRoot.querySelector('.f-name'), 'qa');
    const drafts = el.shadowRoot.querySelector('.f-allowsDraftRuns');
    drafts.checked = true;
    drafts.dispatchEvent(new Event('change'));
    type(el.shadowRoot.querySelector('.budget-create input[data-limit="maxSteps"]'), '15');
    const created = nextEvent(el, 'environment-created');
    el.shadowRoot.querySelector('.confirm').click();
    const e = await created;
    equal(e.detail.environment.allowsDraftRuns, true, 'the client used to drop this on create');
    equal(JSON.stringify((await el.client.getEnvironmentExecutionBudget('qa')).override), JSON.stringify({ maxSteps: 15 }));
  });

  it('hides the mutating controls without environments:write', async () => {
    el = panelWithMock({ scopes: 'environments:read availability:read' });
    mount(el);
    await nextEvent(el, 'loaded');
    ok(el.shadowRoot.querySelector('.new').hidden, 'New environment is hidden');
    el.shadowRoot.querySelector('.erow[data-name="production"]').click();
    await nextEvent(el, 'environment-selected');
    ok(!detail(el).querySelector('.d-save'), 'no Save control');
    ok(!detail(el).querySelector('.d-delete'), 'no Delete control');
    ok(!detail(el).querySelector('.d-mgmt-editor'), 'management tags editor not shown without write scope');
    ok(detail(el).textContent.includes('team=platform'), 'management tags shown read-only');
  });
});
describe('<arazzo-environments> deployment-internal tags', () => {
  let el;
  afterEach(() => el?.remove());

  // Staging is seeded with a deployment-internal sys:group tag (as a really-deployed environment
  // is). The editor must show only the USER-owned labels, and a metadata save must succeed by NOT
  // echoing the internal tag back (the server rejects the reserved prefix — the live-only 400 the
  // live suite caught) while the server preserves it.
  it('hides sys:* tags from the editor and a save neither echoes nor drops them', async () => {
    el = panelWithMock();
    mount(el);
    await nextEvent(el, 'loaded');
    el.shadowRoot.querySelector('.erow[data-name="staging"]').click();
    await nextEvent(el, 'environment-selected');
    await waitFor(() => detail(el).querySelector('.d-mgmt-editor'));
    const ed = detail(el).querySelector('.d-mgmt-editor');
    ok(!ed.tags.some((t) => String(t.key).startsWith('sys:')), 'the internal tag is not offered for editing');

    // Save the metadata unchanged: with the internal tag filtered the round trip succeeds…
    const changed = nextEvent(el, 'environment-changed');
    detail(el).querySelector('.d-save').click();
    const e = await changed;
    ok(!detail(el).querySelector('.error-banner'), 'the save round trip is clean');
    // …and the server-side environment KEPT its internal tag (preserved, not dropped by the replace).
    ok(e.detail.environment.managementTags.some((t) => t.key === 'sys:group'), 'the internal tag survives the save');
  });
});
