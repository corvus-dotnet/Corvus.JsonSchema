// Tier 3 — <arazzo-provider-connect> mounted in a real browser against the in-memory mock: the
// ADR 0052 popup flow (beginProviderAuth → the popup rides the provider's redirect to the
// callback → the control polls listProviders and closes it), the connected chip, and disconnect.
import { ArazzoControlPlaneClient } from '../../src/arazzo-client.js';
import { createMockControlPlane } from '../../demo/mock-api.js';
import '../../src/components/provider-connect.js';
import { ok, equal, nextEvent, waitFor, mount } from './helpers.js';

function connectWithMock() {
  const mock = createMockControlPlane({ latencyMs: 0 });
  const el = document.createElement('arazzo-provider-connect');
  el.pollIntervalMs = 10;
  el.client = new ArazzoControlPlaneClient({ baseUrl: 'https://mock/arazzo/v1', fetch: mock.fetch });
  el.provider = { name: 'portal', displayName: 'Dev Portal', hosts: ['specs.example'], connected: false };
  mount(el);
  return { el, mock };
}

describe('<arazzo-provider-connect>', () => {
  let el;
  afterEach(() => el?.remove());

  it('connects through the popup flow, shows the connected chip, and disconnects', async () => {
    const ctx = connectWithMock();
    el = ctx.el;

    // The injectable opener "navigates the popup": fetching the mock's authorize URL completes the
    // callback (state-authenticated, single-use, provider-bound), and the poll flips to connected.
    let popup;
    el.windowOpener = (url) => {
      ok(url.includes('/providers/portal/auth/callback'), 'the mock self-completes via its own callback');
      ctx.mock.fetch(url);
      popup = { closed: false, close() { this.closed = true; } };
      return popup;
    };

    const connectButton = await waitFor(() => el.shadowRoot.querySelector('.connect'));
    const connected = nextEvent(el, 'provider-connected');
    connectButton.click();
    const e = await connected;
    equal(e.detail.provider.name, 'portal', 'the event names the connected provider');
    await waitFor(() => el.shadowRoot.querySelector('.chip'));
    ok(el.shadowRoot.querySelector('.chip').textContent.includes('Dev Portal'), 'the chip names the provider');
    ok(popup.closed, 'the opener closes the popup once connected');

    const disconnected = nextEvent(el, 'provider-disconnected');
    el.shadowRoot.querySelector('.disconnect').click();
    await disconnected;
    await waitFor(() => el.shadowRoot.querySelector('.connect'));
    equal(el.provider.connected, false, 'the provider reads disconnected');
  });

  it('a cancelled sign-in returns to the connect affordance without a session', async () => {
    const ctx = connectWithMock();
    el = ctx.el;
    el.windowOpener = () => ({ closed: false, close() { this.closed = true; } }); // never completes
    const connectButton = await waitFor(() => el.shadowRoot.querySelector('.connect'));
    connectButton.click();
    const cancel = await waitFor(() => el.shadowRoot.querySelector('.cancel'));
    cancel.click();
    await waitFor(() => el.shadowRoot.querySelector('.connect'));
    ok(!el.provider.connected, 'no session was established');
  });
});