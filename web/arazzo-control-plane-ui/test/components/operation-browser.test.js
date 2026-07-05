// Tier 3 — <arazzo-operation-browser> mounted in a real browser against the in-memory mock.
import { ArazzoControlPlaneClient } from '../../src/arazzo-client.js';
import { createMockControlPlane } from '../../demo/mock-api.js';
import '../../src/components/operation-browser.js';
import { ok, equal, nextEvent, mount } from './helpers.js';

const petstore = {
  openapi: '3.1.0',
  info: { title: 'Petstore', version: '1.0' },
  paths: {
    '/pets': {
      get: {
        operationId: 'listPets',
        summary: 'List pets',
        responses: { 200: { description: 'ok', content: { 'application/json': { schema: { type: 'array' } } } } },
      },
      post: { operationId: 'createPet', responses: { 201: { description: 'created' } } },
    },
  },
};

const orderEvents = {
  asyncapi: '2.6.0',
  channels: {
    'order/confirmations': {
      publish: { operationId: 'onConfirmation', message: { payload: { type: 'object' } } },
    },
  },
};

async function browserWithSources() {
  const mock = createMockControlPlane({ latencyMs: 0 });
  const client = new ArazzoControlPlaneClient({ baseUrl: 'https://mock/arazzo/v1', fetch: mock.fetch });
  const wc = await client.createWorkingCopy({ name: 'wc', document: { arazzo: '1.1.0' } });
  await client.attachWorkingCopySource(wc.id, 'pets', { document: petstore });
  await client.attachWorkingCopySource(wc.id, 'events', { document: orderEvents });

  const el = document.createElement('arazzo-operation-browser');
  el.client = client;
  mount(el);
  el.workingCopyId = wc.id;
  await nextEvent(el, 'loaded');
  return { el, client, wc };
}

const opRows = (el) => [...el.shadowRoot.querySelectorAll('button.op')];

describe('<arazzo-operation-browser>', () => {
  let el;
  afterEach(() => el?.remove());

  it('renders each attachment as a group with its operation surface', async () => {
    ({ el } = await browserWithSources());
    const groups = [...el.shadowRoot.querySelectorAll('.group-head')].map((g) => g.textContent);
    ok(groups.some((g) => g.includes('pets') && g.includes('openapi')), 'openapi group renders with its type');
    ok(groups.some((g) => g.includes('events') && g.includes('asyncapi')), 'asyncapi group renders');
    equal(opRows(el).length, 3, 'two REST operations + one channel operation');
    ok(el.shadowRoot.textContent.includes('List pets'), 'summaries render');
  });

  it('filters operations across groups', async () => {
    ({ el } = await browserWithSources());
    const input = el.shadowRoot.querySelector('.search input');
    input.value = 'confirmation';
    input.dispatchEvent(new Event('input'));
    equal(opRows(el).length, 1, 'only the matching channel operation remains');
    ok(el.shadowRoot.textContent.includes('No operations match'), 'non-matching groups say so');
  });

  it('emits operation-selected with the FULL descriptor on click', async () => {
    ({ el } = await browserWithSources());
    const row = opRows(el).find((r) => r.textContent.includes('listPets'));
    const selected = nextEvent(el, 'operation-selected');
    row.click();
    const e = await selected;
    equal(e.detail.sourceName, 'pets');
    equal(e.detail.operation.operationId, 'listPets');
    equal(e.detail.operation.method, 'GET');
    ok(e.detail.operation.responses['200'].schema, 'the descriptor carries the raw response schema');
  });

  it('detaches a source and reports it (the host refreshes its etag)', async () => {
    ({ el } = await browserWithSources());
    const detached = nextEvent(el, 'source-detached');
    el.shadowRoot.querySelector('button.detach[data-name="events"]').click();
    equal((await detached).detail.name, 'events');
    await nextEvent(el, 'loaded');
    equal(opRows(el).length, 2, 'the channel operation left with its source');
  });

  it('requests the acquisition dialog from the Add button', async () => {
    ({ el } = await browserWithSources());
    const requested = nextEvent(el, 'add-source-requested');
    el.shadowRoot.querySelector('button.add').click();
    await requested;
  });

  it('exposes the loaded surfaces for the host operation index', async () => {
    ({ el } = await browserWithSources());
    const surfaces = el.surfaces;
    equal(surfaces.get('pets').length, 2);
    equal(surfaces.get('events')[0].channelPath, 'order/confirmations');
  });
});