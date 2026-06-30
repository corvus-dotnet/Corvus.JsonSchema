// Tier 3 — <arazzo-runner-authorizations> mounted in a real browser against the in-memory mock (§5.5 runner inbox).
import { ArazzoControlPlaneClient } from '../../src/arazzo-client.js';
import { createMockControlPlane } from '../../demo/mock-api.js';
import '../../src/components/runner-authorizations-panel.js';
import { ok, equal, nextEvent, waitFor, mount } from './helpers.js';

function panelWithMock(attrs = {}) {
  const mock = createMockControlPlane({ latencyMs: 0 });
  const el = document.createElement('arazzo-runner-authorizations');
  for (const [k, v] of Object.entries(attrs)) el.setAttribute(k, v);
  el.client = new ArazzoControlPlaneClient({ baseUrl: 'https://mock/arazzo/v1', fetch: mock.fetch });
  return el;
}

const rows = (el) => el.shadowRoot.querySelectorAll('tbody tr[data-key]');
const rowFor = (el, environment, runnerId) => [...rows(el)].find((r) => r.dataset.key === `${environment} ${runnerId}`);

describe('<arazzo-runner-authorizations>', () => {
  let el;
  afterEach(() => el?.remove());

  it('opens straight to the actionable inbox (Pending) across administered environments', async () => {
    el = panelWithMock();
    mount(el);
    await nextEvent(el, 'loaded');
    // The seed has two pending authorizations: runner-us-1 (production) and runner-eu-2 (staging).
    equal(rows(el).length, 2, 'two pending authorizations in the inbox');
    ok(el.shadowRoot.textContent.includes('runner-us-1'), 'shows the pending runner');
    ok(el.shadowRoot.querySelector('.act[data-action="authorize"]'), 'a pending row offers Authorize');
    ok(el.shadowRoot.querySelector('.act[data-action="revoke"]'), 'a pending row offers Revoke');
  });

  it('narrows the inbox to a single environment’s queue', async () => {
    el = panelWithMock({ environment: 'staging' });
    mount(el);
    await nextEvent(el, 'loaded');
    // staging has one pending authorization (runner-eu-2).
    equal(rows(el).length, 1, 'one pending authorization for staging');
    ok(el.shadowRoot.textContent.includes('runner-eu-2'), 'shows the staging runner');
  });

  it('filters by status — Authorized shows the authorized roster', async () => {
    el = panelWithMock();
    mount(el);
    await nextEvent(el, 'loaded');
    const status = el.shadowRoot.querySelector('.status');
    status.value = 'Authorized';
    status.dispatchEvent(new Event('change'));
    await nextEvent(el, 'loaded');
    equal(rows(el).length, 1, 'one authorized runner');
    ok(el.shadowRoot.textContent.includes('runner-eu-1'), 'shows the authorized runner');
  });

  it('authorizes a pending runner from the inbox', async () => {
    el = panelWithMock();
    mount(el);
    await nextEvent(el, 'loaded');
    const authorize = rowFor(el, 'production', 'runner-us-1')?.querySelector('.act[data-action="authorize"]');
    ok(authorize, 'the pending row offers Authorize');
    authorize.click();
    const dlg = await waitFor(() => el.shadowRoot.querySelector('.decision-dialog'));
    const decided = nextEvent(el, 'runner-authorization-decided');
    dlg.querySelector('.ok').click();
    const e = await decided;
    equal(e.detail.action, 'authorize', 'the authorize decision fired');
    equal(e.detail.authorization.status, 'Authorized', 'the authorization is now Authorized');
    // It leaves the Pending inbox after the reload.
    await waitFor(() => rows(el).length === 1);
  });

  it('revokes a runner from the inbox', async () => {
    el = panelWithMock();
    mount(el);
    await nextEvent(el, 'loaded');
    const revoke = rowFor(el, 'staging', 'runner-eu-2')?.querySelector('.act[data-action="revoke"]');
    ok(revoke, 'the pending row offers Revoke');
    revoke.click();
    const dlg = await waitFor(() => el.shadowRoot.querySelector('.decision-dialog'));
    const decided = nextEvent(el, 'runner-authorization-decided');
    dlg.querySelector('.ok').click();
    const e = await decided;
    equal(e.detail.action, 'revoke', 'the revoke decision fired');
    equal(e.detail.authorization.status, 'Revoked', 'the authorization is now Revoked');
  });

  it('surfaces a 403 from a non-administered queue as a banner', async () => {
    el = document.createElement('arazzo-runner-authorizations');
    el.setAttribute('environment', 'someone-elses-environment');
    el.client = {
      listRunnerAuthorizations: async () => { const err = new Error('Forbidden'); err.problem = { title: 'Forbidden', status: 403 }; throw err; },
    };
    el.addEventListener('error', (e) => e.stopPropagation());
    mount(el);
    const e = await nextEvent(el, 'error');
    equal(e.detail.problem.status, 403, 'listing a non-administered queue is forbidden');
    ok(el.shadowRoot.querySelector('.error-banner'), 'shows an error banner');
  });
});