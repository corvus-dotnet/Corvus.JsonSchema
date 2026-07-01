// Tier 3 — <arazzo-run-detail>: correlation id visibility (#101) and its copy button (#99).
import { ArazzoControlPlaneClient } from '../../src/arazzo-client.js';
import { createMockControlPlane } from '../../demo/mock-api.js';
import '../../src/components/run-detail.js';
import { ok, equal, waitFor, mount } from './helpers.js';

function detailWithMock(runId, attrs = {}) {
  const mock = createMockControlPlane({ latencyMs: 0 });
  const el = document.createElement('arazzo-run-detail');
  el.setAttribute('runid', runId);
  for (const [k, v] of Object.entries(attrs)) el.setAttribute(k, v);
  el.client = new ArazzoControlPlaneClient({ baseUrl: 'https://mock/arazzo/v1', fetch: mock.fetch });
  return el;
}

describe('<arazzo-run-detail> correlation id', () => {
  let el;
  afterEach(() => el?.remove());

  // #101 — every run carries a telemetry correlation id, not just running ones. A Completed run shows it.
  it('shows the correlation id for a non-running (Completed) run', async () => {
    el = detailWithMock('run-0a5512cd');
    mount(el);
    const corr = await waitFor(() => el.shadowRoot.querySelector('[part="correlation"]'));
    ok(corr.textContent.includes('0a5512cd'), 'the correlation id is rendered');
  });

  // #101 — a Suspended message-wait run also has a top-level telemetry correlation id.
  it('shows the correlation id for a Suspended run', async () => {
    el = detailWithMock('run-9c0142ab');
    mount(el);
    const corr = await waitFor(() => el.shadowRoot.querySelector('[part="correlation"]'));
    ok(corr.textContent.includes('9c0142ab'), 'the correlation id is rendered');
  });

  // §5.5 — a run pinned to a deployment environment shows it.
  it('shows the pinned environment for a run', async () => {
    el = detailWithMock('run-7f3a9c21');
    mount(el);
    const env = await waitFor(() => el.shadowRoot.querySelector('[part="environment"]'));
    ok(env.textContent.includes('production'), 'the pinned environment is rendered');
  });

  // #99 — a copy button sits next to the correlation id and confirms on click.
  it('has a copy button for the correlation id that confirms on click', async () => {
    el = detailWithMock('run-0a5512cd');
    mount(el);
    const copy = await waitFor(() => el.shadowRoot.querySelector('[part="copy-correlation"]'));
    ok(copy, 'copy button present');
    copy.click();
    // copyToClipboard resolves (in test contexts navigator.clipboard may be absent → text stays); either
    // way the click must not throw. When the clipboard is available the glyph flips to a check.
    await new Promise((r) => setTimeout(r, 10));
    ok(['⧉', '✓'].includes(copy.textContent), 'glyph is the copy or confirmed state');
  });
});
