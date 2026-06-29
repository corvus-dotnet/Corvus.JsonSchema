// Tier 3 — <arazzo-availability-requests> mounted in a real browser against the in-memory mock (§7.8 promotion inbox).
import { ArazzoControlPlaneClient } from '../../src/arazzo-client.js';
import { createMockControlPlane } from '../../demo/mock-api.js';
import '../../src/components/availability-requests-panel.js';
import { ok, equal, nextEvent, waitFor, mount } from './helpers.js';

function panelWithMock(attrs = {}) {
  const mock = createMockControlPlane({ latencyMs: 0 });
  const el = document.createElement('arazzo-availability-requests');
  for (const [k, v] of Object.entries(attrs)) el.setAttribute(k, v);
  el.client = new ArazzoControlPlaneClient({ baseUrl: 'https://mock/arazzo/v1', fetch: mock.fetch });
  return el;
}

const rows = (el) => el.shadowRoot.querySelectorAll('tbody tr[data-id]');

describe('<arazzo-availability-requests>', () => {
  let el;
  afterEach(() => el?.remove());

  it('lists the caller’s own requests in the "My requests" view', async () => {
    el = panelWithMock();
    mount(el);
    await nextEvent(el, 'loaded');
    // The seed has one request created by the demo user (a denied roll-back of nightly-reconcile v2).
    equal(rows(el).length, 1, 'one own request');
    ok(el.shadowRoot.textContent.includes('nightly-reconcile'), 'shows the target workflow');
    ok(el.shadowRoot.textContent.includes('production'), 'shows the target environment');
  });

  it('opens straight to the actionable inbox (Pending) in the approver view', async () => {
    el = panelWithMock({ view: 'queue' });
    mount(el);
    await nextEvent(el, 'loaded');
    // Three pending requests across all environments; the denied one (the demo user's own) is filtered out by the default.
    equal(rows(el).length, 3, 'three pending requests in the inbox');
    ok(el.shadowRoot.querySelector('.act[data-action="approve"]'), 'a pending request offers Approve');
    ok(el.shadowRoot.querySelector('.act[data-action="deny"]'), 'a pending request offers Deny');
  });

  it('narrows the inbox to a single environment’s queue', async () => {
    el = panelWithMock({ view: 'queue', environment: 'staging' });
    mount(el);
    await nextEvent(el, 'loaded');
    // staging has one pending request (bob → onboard-customer v1).
    equal(rows(el).length, 1, 'one pending request for staging');
    ok(el.shadowRoot.textContent.includes('onboard-customer'), 'shows the staging request');
  });

  it('approves a ready pending request from the inbox', async () => {
    el = panelWithMock({ view: 'queue' });
    mount(el);
    await nextEvent(el, 'loaded');
    // areq-3002 promotes adopt-pet to production, where its 'petstore' source is credentialed → ready to approve.
    const approve = el.shadowRoot.querySelector('tr[data-id="areq-3002"] .act[data-action="approve"]');
    ok(approve, 'the ready request offers Approve');
    approve.click();
    const dlg = await waitFor(() => el.shadowRoot.querySelector('.decision-dialog'));
    const decided = nextEvent(el, 'availability-request-decided');
    dlg.querySelector('.ok').click();
    const e = await decided;
    equal(e.detail.action, 'approve', 'the approve decision fired');
    equal(e.detail.request.status, 'Approved', 'the request is now approved');
  });

  it('blocks approval when the version is not ready in the target environment', async () => {
    el = panelWithMock({ view: 'queue' });
    el.addEventListener('error', (e) => e.stopPropagation());
    mount(el);
    await nextEvent(el, 'loaded');
    // areq-3001 promotes onboard-customer to production, where its 'events' source has no credential → not ready (409).
    const approve = el.shadowRoot.querySelector('tr[data-id="areq-3001"] .act[data-action="approve"]');
    ok(approve, 'the not-ready request still offers Approve (the gate fires on the decision)');
    approve.click();
    const dlg = await waitFor(() => el.shadowRoot.querySelector('.decision-dialog'));
    const errored = nextEvent(el, 'error');
    dlg.querySelector('.ok').click();
    const e = await errored;
    equal(e.detail.problem.status, 409, 'approval is blocked by the readiness gate');
    ok(el.shadowRoot.querySelector('.error-banner'), 'shows the not-ready error');
  });

  it('submits a promotion request, offering only the workflow’s versions and only ready environments', async () => {
    el = panelWithMock();
    mount(el);
    await nextEvent(el, 'loaded');
    el.shadowRoot.querySelector('.new').click();
    const dlg = await waitFor(() => el.shadowRoot.querySelector('arazzo-availability-request-dialog'));
    // Choosing a workflow loads its actual catalogued versions into the Version dropdown.
    const wf = dlg.shadowRoot.querySelector('.sub-wf');
    wf.value = 'onboard-customer';
    wf.dispatchEvent(new Event('change', { bubbles: true, composed: true }));
    const verSel = dlg.shadowRoot.querySelector('.ver-in');
    await waitFor(() => [...verSel.options].some((o) => o.value === '1'));
    // onboard-customer v1's 'events' source is credentialed in staging only → only staging is offered.
    const envSel = dlg.shadowRoot.querySelector('.env-in');
    await waitFor(() => [...envSel.options].some((o) => o.value === 'staging'));
    ok(![...envSel.options].some((o) => o.value === 'production'), 'production is not offered (events is not credentialed there)');
    envSel.value = 'staging';
    envSel.dispatchEvent(new Event('change'));
    const submitted = nextEvent(el, 'availability-request-submitted');
    dlg.shadowRoot.querySelector('.ok').click();
    const e = await submitted;
    equal(e.detail.request.baseWorkflowId, 'onboard-customer', 'the new request targets the chosen workflow');
    equal(e.detail.request.environment, 'staging', 'targets the ready environment');
    await waitFor(() => rows(el).length === 2);
  });

  it('offers the environment a version is actually ready in (adopt-pet → production)', async () => {
    el = panelWithMock();
    mount(el);
    await nextEvent(el, 'loaded');
    el.shadowRoot.querySelector('.new').click();
    const dlg = await waitFor(() => el.shadowRoot.querySelector('arazzo-availability-request-dialog'));
    const wf = dlg.shadowRoot.querySelector('.sub-wf');
    wf.value = 'adopt-pet';
    wf.dispatchEvent(new Event('change', { bubbles: true, composed: true }));
    await waitFor(() => [...dlg.shadowRoot.querySelector('.ver-in').options].some((o) => o.value === '1'));
    const envSel = dlg.shadowRoot.querySelector('.env-in');
    // adopt-pet's 'petstore' source is credentialed in production only.
    await waitFor(() => [...envSel.options].some((o) => o.value === 'production'));
    ok(![...envSel.options].some((o) => o.value === 'staging'), 'staging is not offered for adopt-pet');
  });

  it('surfaces a 403 from the approver inbox as a banner (not an admin)', async () => {
    el = document.createElement('arazzo-availability-requests');
    el.setAttribute('view', 'queue');
    el.setAttribute('environment', 'someone-elses-environment');
    el.client = {
      listAvailabilityRequests: async () => { const err = new Error('Forbidden'); err.problem = { title: 'Forbidden', status: 403 }; throw err; },
    };
    el.addEventListener('error', (e) => e.stopPropagation());
    mount(el);
    const errored = nextEvent(el, 'error');
    const e = await errored;
    equal(e.detail.problem.status, 403, 'listing a non-administered queue is forbidden');
    ok(el.shadowRoot.querySelector('.error-banner'), 'shows an error banner');
  });

  it('switches between the two views via the tabs', async () => {
    el = panelWithMock();
    mount(el);
    await nextEvent(el, 'loaded');
    el.shadowRoot.querySelector('.tab-queue').click();
    equal(el.view, 'queue', 'the inbox tab is selected');
    ok(el.shadowRoot.querySelector('.env'), 'the inbox toolbar offers an environment filter');
  });
});