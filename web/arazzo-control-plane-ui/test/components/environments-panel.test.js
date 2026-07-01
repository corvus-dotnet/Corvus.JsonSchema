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
    equal(rows(el).length, 2, 'production + staging');
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
    ok(el.shadowRoot.querySelector('.next').disabled, 'Next disabled on the last page');

    const back = nextEvent(el, 'loaded');
    el.shadowRoot.querySelector('.prev').click();
    await back;
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

  it('hides the mutating controls without environments:write', async () => {
    el = panelWithMock({ scopes: 'environments:read availability:read' });
    mount(el);
    await nextEvent(el, 'loaded');
    ok(el.shadowRoot.querySelector('.new').hidden, 'New environment is hidden');
    el.shadowRoot.querySelector('.erow[data-name="production"]').click();
    await nextEvent(el, 'environment-selected');
    ok(!detail(el).querySelector('.d-save'), 'no Save control');
    ok(!detail(el).querySelector('.d-delete'), 'no Delete control');
  });
});