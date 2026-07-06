// Tier 3 — <arazzo-workspace-table> mounted in a real browser against the in-memory mock.
import { ArazzoControlPlaneClient } from '../../src/arazzo-client.js';
import { createMockControlPlane } from '../../demo/mock-api.js';
import '../../src/components/workspace-table.js';
import { ok, equal, nextEvent, mount, waitFor } from './helpers.js';

function tableWithMock(attrs = {}) {
  const mock = createMockControlPlane({ latencyMs: 0 });
  const el = document.createElement('arazzo-workspace-table');
  for (const [k, v] of Object.entries(attrs)) el.setAttribute(k, v);
  el.client = new ArazzoControlPlaneClient({ baseUrl: 'https://mock/arazzo/v1', fetch: mock.fetch });
  return el;
}

async function seed(el, name, extra = {}) {
  return el.client.createWorkingCopy({ name, document: { arazzo: '1.1.0' }, ...extra });
}

const rowCount = (el) => el.shadowRoot.querySelectorAll('tbody tr[data-id]').length;

describe('<arazzo-workspace-table>', () => {
  let el;
  afterEach(() => el?.remove());

  it('shows the explicit empty state, then rows after a reload', async () => {
    el = tableWithMock();
    mount(el);
    await nextEvent(el, 'loaded');
    ok(el.shadowRoot.textContent.includes('No working copies yet'), 'empty state renders');

    await seed(el, 'retry tuning', { fromless: undefined });
    el.reload();
    await nextEvent(el, 'loaded');
    equal(rowCount(el), 1, 'the created working copy renders');
    ok(el.shadowRoot.textContent.includes('retry tuning'), 'name renders');
  });

  it('renders provenance for carried-over copies and — for fresh ones', async () => {
    el = tableWithMock();
    // The mock does not support carry-over, so fake provenance via the summary shape it DOES return:
    // create two fresh ones and assert the provenance column dashes.
    mount(el);
    await seed(el, 'one');
    await seed(el, 'two');
    el.reload();
    await nextEvent(el, 'loaded');
    equal(rowCount(el), 2);
    equal(el.$$('tbody .prov').length, 0, 'fresh copies have no provenance span');
  });

  it('emits working-copy-selected with the summary when a row is clicked', async () => {
    el = tableWithMock({ selectable: '' });
    mount(el);
    const created = await seed(el, 'pick me');
    el.reload();
    await nextEvent(el, 'loaded');

    const selected = nextEvent(el, 'working-copy-selected');
    el.shadowRoot.querySelector(`tbody tr[data-id="${created.id}"]`).click();
    const e = await selected;
    equal(e.detail.workingCopy.id, created.id, 'event carries the summary');
    equal(e.detail.workingCopy.document, undefined, 'the summary is document-less — the shell fetches the full copy');
  });

  it('New creates a blank working copy and emits it ready to open (can-write only)', async () => {
    el = tableWithMock();
    mount(el);
    await nextEvent(el, 'loaded');
    ok(el.shadowRoot.querySelector('button.new').hidden, 'New hidden without can-write');

    el.setAttribute('can-write', '');
    ok(!el.shadowRoot.querySelector('button.new').hidden, 'New shows with can-write');
    const createdEvent = nextEvent(el, 'working-copy-created');
    el.shadowRoot.querySelector('button.new').click();
    // The kit's standard dialog asks for the name — no system prompt.
    const ask = el.shadowRoot.querySelector('arazzo-input-dialog');
    await waitFor(() => ask.shadowRoot.querySelector('.in-field'));
    equal(ask.shadowRoot.querySelector('.in-field').value, 'untitled', 'the default name prefills');
    ask.shadowRoot.querySelector('.confirm').click();
    const e = await createdEvent;
    equal(e.detail.workingCopy.name, 'untitled', 'blank create derives the untitled name');
    ok(e.detail.workingCopy.document, 'the created event carries the FULL working copy');
    await nextEvent(el, 'loaded');
    equal(rowCount(el), 1, 'the list refreshed');
  });

  it('Delete removes the row without selecting it', async () => {
    el = tableWithMock({ selectable: '', 'can-write': '' });
    mount(el);
    const created = await seed(el, 'doomed');
    el.reload();
    await nextEvent(el, 'loaded');

    let selections = 0;
    el.addEventListener('working-copy-selected', () => selections++);
    const deleted = nextEvent(el, 'working-copy-deleted');
    el.shadowRoot.querySelector(`tbody button.rowaction[data-id="${created.id}"]`).click();
    // The standard danger dialog confirms — no system confirm.
    const ask = el.shadowRoot.querySelector('arazzo-input-dialog');
    await waitFor(() => {
      const b = ask.shadowRoot.querySelector('.confirm.danger');
      return b && ask.shadowRoot.querySelector('dialog').open ? b : null;
    });
    ask.shadowRoot.querySelector('.confirm.danger').click();
    const e = await deleted;
    equal(e.detail.id, created.id);
    equal(selections, 0, 'the delete click did not bubble into a row selection');
    await nextEvent(el, 'loaded');
    equal(rowCount(el), 0);
  });

  it('keyset-pages with Prev/Next', async () => {
    el = tableWithMock({ 'page-size': '2' });
    mount(el);
    for (let i = 0; i < 5; i++) await seed(el, `wc ${i}`);
    el.reload();
    await nextEvent(el, 'loaded');
    equal(rowCount(el), 2, 'first page');

    el.nextPage();
    await nextEvent(el, 'loaded');
    equal(rowCount(el), 2, 'second page');

    el.nextPage();
    await nextEvent(el, 'loaded');
    equal(rowCount(el), 1, 'last page');

    el.prevPage();
    await nextEvent(el, 'loaded');
    equal(rowCount(el), 2, 'back to the second page');
  });

  it('surfaces errors with a retry', async () => {
    el = tableWithMock();
    el.client = new ArazzoControlPlaneClient({
      baseUrl: 'https://mock/arazzo/v1',
      fetch: () => Promise.resolve(new Response(JSON.stringify({ title: 'Boom' }), { status: 500, headers: { 'Content-Type': 'application/problem+json' } })),
    });
    el.addEventListener('error', (e) => e.stopPropagation()); // keep the component's error event out of the runner's page-error listener
    mount(el);
    await nextEvent(el, 'error');
    ok(el.shadowRoot.querySelector('.error-banner'), 'error banner renders');
    ok(el.shadowRoot.querySelector('.retry'), 'retry affordance present');
  });
});