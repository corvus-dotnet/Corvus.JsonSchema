// Tier 3 — <arazzo-source-acquisition-dialog> mounted in a real browser against the in-memory mock.
import { ArazzoControlPlaneClient } from '../../src/arazzo-client.js';
import { createMockControlPlane } from '../../demo/mock-api.js';
import '../../src/components/source-acquisition-dialog.js';
import { ok, equal, nextEvent, waitFor, mount } from './helpers.js';

async function dialogWithMock() {
  const mock = createMockControlPlane({ latencyMs: 0 });
  const client = new ArazzoControlPlaneClient({ baseUrl: 'https://mock/arazzo/v1', fetch: mock.fetch });
  const wc = await client.createWorkingCopy({ name: 'wc', document: { arazzo: '1.1.0' } });
  const el = document.createElement('arazzo-source-acquisition-dialog');
  el.client = client;
  mount(el);
  return { el, client, wc };
}

describe('<arazzo-source-acquisition-dialog>', () => {
  let el;
  afterEach(() => el?.remove());

  it('registry mode lists registered sources, defaults the name, and attaches a reference', async () => {
    const ctx = await dialogWithMock();
    el = ctx.el;
    el.open({ workingCopyId: ctx.wc.id });
    await waitFor(() => [...el.shadowRoot.querySelectorAll('.registry-in option')].length > 1, 'registry loads');

    const select = el.shadowRoot.querySelector('.registry-in');
    select.value = select.options[1].value; // the first real registered source
    select.dispatchEvent(new Event('change'));
    equal(el.shadowRoot.querySelector('.name-in').value, select.value, 'the attachment name defaults to the picked source');
    ok(!el.shadowRoot.querySelector('button.attach').disabled, 'attach enables');

    const attached = nextEvent(el, 'source-attached');
    el.shadowRoot.querySelector('button.attach').click();
    const e = await attached;
    equal(e.detail.attachment.kind, 'registry');
    ok(e.detail.attachment.etag, 'the attachment carries the fresh working-copy etag');

    const { sources } = await ctx.client.listWorkingCopySources(ctx.wc.id);
    equal(sources.length, 1);
  });

  it('fetch mode previews the server-detected document then attaches it inline', async () => {
    const ctx = await dialogWithMock();
    el = ctx.el;
    el.open({ workingCopyId: ctx.wc.id });
    el.shadowRoot.querySelector('.tabs button[data-mode="fetch"]').click();

    el.shadowRoot.querySelector('.url-in').value = 'https://specs.example/payments.openapi.json';
    el.shadowRoot.querySelector('button.fetch').click();
    await waitFor(() => !el.shadowRoot.querySelector('.fetch-preview').hidden
      && el.shadowRoot.querySelector('.fetch-preview').textContent.includes('openapi'), 'the preview shows the detected type');
    equal(el.shadowRoot.querySelector('.name-in').value, 'payments', 'the name defaults from the URL stem');

    const attached = nextEvent(el, 'source-attached');
    el.shadowRoot.querySelector('button.attach').click();
    const e = await attached;
    equal(e.detail.attachment.kind, 'inline');
    equal(e.detail.attachment.name, 'payments');
  });

  it('upload mode parses a JSON document file and attaches it inline', async () => {
    const ctx = await dialogWithMock();
    el = ctx.el;
    el.open({ workingCopyId: ctx.wc.id });
    el.shadowRoot.querySelector('.tabs button[data-mode="upload"]').click();

    const file = new File([JSON.stringify({ asyncapi: '2.6.0', channels: {} })], 'events.asyncapi.json', { type: 'application/json' });
    const input = el.shadowRoot.querySelector('.file-in');
    const transfer = new DataTransfer();
    transfer.items.add(file);
    input.files = transfer.files;
    input.dispatchEvent(new Event('change'));
    await waitFor(() => !el.shadowRoot.querySelector('.upload-preview').hidden, 'the preview shows the parsed document');
    equal(el.shadowRoot.querySelector('.name-in').value, 'events', 'the name defaults from the file stem');

    const attached = nextEvent(el, 'source-attached');
    el.shadowRoot.querySelector('button.attach').click();
    const e = await attached;
    equal(e.detail.attachment.kind, 'inline');
    equal(e.detail.attachment.type, 'asyncapi');
  });

  it('requires a name and a chosen source before attach enables, and surfaces fetch failures', async () => {
    const ctx = await dialogWithMock();
    el = ctx.el;
    el.addEventListener('error', (e) => e.stopPropagation());
    el.open({ workingCopyId: ctx.wc.id });
    ok(el.shadowRoot.querySelector('button.attach').disabled, 'attach starts disabled');

    el.shadowRoot.querySelector('.tabs button[data-mode="fetch"]').click();
    el.shadowRoot.querySelector('.url-in').value = 'http://insecure.example/spec.json'; // the mock rejects http
    el.shadowRoot.querySelector('button.fetch').click();
    await waitFor(() => !el.shadowRoot.querySelector('.error-banner').hidden, 'the failure surfaces in the banner');
    ok(el.shadowRoot.querySelector('button.attach').disabled, 'attach stays disabled after a failed fetch');
  });
});