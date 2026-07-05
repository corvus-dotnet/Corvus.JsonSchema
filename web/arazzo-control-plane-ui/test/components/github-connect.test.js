// Tier 3 — <arazzo-github-connect> mounted in a real browser against the in-memory mock: the §4.7
// popup flow (begin → the popup rides GitHub's redirect to the callback → the opener polls the
// session and closes it), the connected chip, and disconnect.
import { ArazzoControlPlaneClient } from '../../src/arazzo-client.js';
import { createMockControlPlane } from '../../demo/mock-api.js';
import '../../src/components/github-connect.js';
import { ok, equal, nextEvent, waitFor, mount } from './helpers.js';

function connectWithMock() {
  const mock = createMockControlPlane({ latencyMs: 0 });
  const el = document.createElement('arazzo-github-connect');
  el.pollIntervalMs = 10;
  el.client = new ArazzoControlPlaneClient({ baseUrl: 'https://mock/arazzo/v1', fetch: mock.fetch });
  mount(el);
  return { el, mock };
}

describe('<arazzo-github-connect>', () => {
  let el;
  afterEach(() => el?.remove());

  it('connects through the popup flow, shows the identity chip, and disconnects', async () => {
    const ctx = connectWithMock();
    el = ctx.el;

    // The injectable opener "navigates the popup": fetching the mock's authorize URL completes the
    // callback (state-authenticated, single-use), and the poll flips to connected.
    let popup;
    el.windowOpener = (url) => {
      ok(url.includes('/github/auth/callback'), 'the mock self-completes via its own callback');
      ctx.mock.fetch(url);
      popup = { closed: false, close() { this.closed = true; } };
      return popup;
    };

    const connectButton = await waitFor(() => el.shadowRoot.querySelector('.connect'));
    const connected = nextEvent(el, 'github-connected');
    connectButton.click();
    const e = await connected;
    equal(e.detail.session.login, 'octo', 'the session carries the signed-in identity');
    await waitFor(() => el.shadowRoot.querySelector('.chip'));
    ok(el.shadowRoot.querySelector('.chip').textContent.includes('octo'), 'the chip shows the login');
    ok(popup.closed, 'the opener closes the popup once connected');

    const disconnected = nextEvent(el, 'github-disconnected');
    el.shadowRoot.querySelector('.disconnect').click();
    await disconnected;
    await waitFor(() => el.shadowRoot.querySelector('.connect'));
    equal(el.session.connected, false, 'the session reads disconnected');
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
    ok(!el.session?.connected, 'no session was established');
  });
});