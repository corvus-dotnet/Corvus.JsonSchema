// Tier 3 — <arazzo-access-requests> mounted in a real browser against the in-memory mock.
import { ArazzoControlPlaneClient } from '../../src/arazzo-client.js';
import { createMockControlPlane } from '../../demo/mock-api.js';
import '../../src/components/access-requests-panel.js';
import { ok, equal, nextEvent, waitFor, mount } from './helpers.js';

function panelWithMock(attrs = {}) {
  const mock = createMockControlPlane({ latencyMs: 0 });
  const el = document.createElement('arazzo-access-requests');
  for (const [k, v] of Object.entries(attrs)) el.setAttribute(k, v);
  el.client = new ArazzoControlPlaneClient({ baseUrl: 'https://mock/arazzo/v1', fetch: mock.fetch });
  return el;
}

const rows = (el) => el.shadowRoot.querySelectorAll('tbody tr[data-id]');

describe('<arazzo-access-requests>', () => {
  let el;
  afterEach(() => el?.remove());

  it('lists the caller’s own requests in the "My requests" view', async () => {
    el = panelWithMock();
    mount(el);
    await nextEvent(el, 'loaded');
    equal(rows(el).length, 3, 'three seeded requests');
    ok(el.shadowRoot.textContent.includes('onboard-customer'), 'shows a target workflow');
    ok(el.shadowRoot.querySelector('.act[data-action="withdraw"]'), 'a pending request offers Withdraw');
  });

  it('filters by status client-side', async () => {
    el = panelWithMock();
    mount(el);
    await nextEvent(el, 'loaded');
    const status = el.shadowRoot.querySelector('.status');
    status.value = 'Approved';
    status.dispatchEvent(new Event('change'));
    equal(rows(el).length, 1, 'only the approved request remains');
  });

  it('submits a new request and emits access-request-submitted', async () => {
    el = panelWithMock();
    mount(el);
    await nextEvent(el, 'loaded');
    el.shadowRoot.querySelector('.new').click();
    // The submit form is the shared <arazzo-access-request-dialog>, with its own shadow root.
    const dlg = await waitFor(() => el.shadowRoot.querySelector('arazzo-access-request-dialog'));
    dlg.shadowRoot.querySelector('.sub-wf').value = 'nightly-reconcile';
    const submitted = nextEvent(el, 'access-request-submitted');
    dlg.shadowRoot.querySelector('.ok').click();
    const e = await submitted;
    equal(e.detail.request.baseWorkflowId, 'nightly-reconcile', 'the new request targets the chosen workflow');
    await waitFor(() => rows(el).length === 4);
  });

  it('shows a workflow’s queue and approves a pending request', async () => {
    el = panelWithMock({ view: 'queue', 'base-workflow-id': 'onboard-customer' });
    mount(el);
    await nextEvent(el, 'loaded');
    // onboard-customer has alice (pending) + carol (denied).
    equal(rows(el).length, 2, 'two requests for this workflow');
    const approve = el.shadowRoot.querySelector('.act[data-action="approve"]');
    ok(approve, 'the pending request offers Approve');
    approve.click();
    const dlg = await waitFor(() => el.shadowRoot.querySelector('.decision-dialog'));
    const decided = nextEvent(el, 'access-request-decided');
    dlg.querySelector('.ok').click();
    const e = await decided;
    equal(e.detail.action, 'approve', 'the approve decision fired');
    equal(e.detail.request.status, 'Approved', 'the request is now approved');
  });

  it('surfaces a 403 from the approver queue as a banner (not an admin)', async () => {
    el = document.createElement('arazzo-access-requests');
    el.setAttribute('view', 'queue');
    el.setAttribute('base-workflow-id', 'someone-elses-workflow');
    el.client = {
      listAccessRequests: async () => { const err = new Error('Forbidden'); err.problem = { title: 'Forbidden', status: 403 }; throw err; },
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
    equal(el.view, 'queue', 'the queue tab is selected');
    ok(el.shadowRoot.querySelector('.wf'), 'the queue toolbar offers a workflow picker');
  });
});