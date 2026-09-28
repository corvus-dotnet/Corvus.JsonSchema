// createSessionFetch — the fetch a BFF host hands the kit: cookie + X-CSRF, and a 401 sends the browser to sign in,
// except while the page is navigating away (sign-out's end-session redirect must not be cancelled by a sign-in).
import { createSessionFetch } from '../../src/components/auth-status.js';
import { ok, equal } from './helpers.js';

const respond = (status) => async () => new Response('{}', { status, headers: { 'Content-Type': 'application/json' } });
const settled = (promise, ms = 50) => Promise.race([promise.then(() => true), new Promise((r) => setTimeout(() => r(false), ms))]);

describe('createSessionFetch', () => {
  it('sends the session cookie and the X-CSRF header, and hands back any response but a 401', async () => {
    let seen;
    const fetch = createSessionFetch({ fetch: async (input, init) => { seen = init; return new Response('{}', { status: 200 }); }, target: new EventTarget(), navigate: () => {} });
    const response = await fetch('/arazzo/v1/catalog');
    equal(response.status, 200);
    equal(seen.credentials, 'include');
    equal(seen.headers.get('X-CSRF'), '1');
  });

  it('sends the browser to sign in on a 401, back to where it was', async () => {
    const visits = [];
    const fetch = createSessionFetch({ fetch: respond(401), target: new EventTarget(), navigate: (url) => visits.push(url) });
    ok(!(await settled(fetch('/arazzo/v1/catalog'))), 'the refused call never settles for its caller');
    equal(visits.length, 1);
    ok(visits[0].startsWith('/login?returnUrl='), visits[0]);
  });

  it('does not start a sign-in while the page is navigating away, as sign-out does on its way to end-session', async () => {
    const visits = [];
    const page = new EventTarget();
    const fetch = createSessionFetch({ fetch: respond(401), target: page, navigate: (url) => visits.push(url) });
    page.dispatchEvent(new Event('beforeunload'));
    ok(!(await settled(fetch('/arazzo/v1/catalog'))), 'the refused call still never settles');
    equal(visits.length, 0, 'no sign-in cancels the navigation under way');
  });

  it('counts the page as in use again once a navigation has not taken it away', async () => {
    const visits = [];
    const page = new EventTarget();
    const fetch = createSessionFetch({ fetch: respond(401), target: page, navigate: (url) => visits.push(url), settleMs: 30 });
    page.dispatchEvent(new Event('beforeunload'));
    await new Promise((resolve) => setTimeout(resolve, 60));
    void fetch('/arazzo/v1/catalog');
    await new Promise((resolve) => setTimeout(resolve, 20));
    equal(visits.length, 1, 'a 401 after the page stayed sends it to sign in');
  });
});
