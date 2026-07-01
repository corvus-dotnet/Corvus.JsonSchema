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

  it('starts empty in the "My requests" view (the demo user has made no requests yet)', async () => {
    el = panelWithMock();
    mount(el);
    await nextEvent(el, 'loaded');
    // The mock's demo identity is an approver, not a requester — "mine" is empty until they submit one.
    equal(rows(el).length, 0, 'no own requests yet');
    ok(el.shadowRoot.textContent.includes('no access requests'), 'shows the empty state');
  });

  it('filters the approver inbox by status', async () => {
    el = panelWithMock({ view: 'queue' });
    mount(el);
    await nextEvent(el, 'loaded');
    // The inbox opens to the actionable Pending set (a seeded operate request + a seeded view request); clearing the
    // filter shows them all.
    equal(rows(el).length, 2, 'two pending requests by default (an operate and a view grant)');
    const status = el.shadowRoot.querySelector('.status');
    status.value = '';
    status.dispatchEvent(new Event('change'));
    await nextEvent(el, 'loaded');
    equal(rows(el).length, 4, 'all four seeded requests with no status filter');
  });

  it('submits a new request and emits access-request-submitted', async () => {
    el = panelWithMock();
    mount(el);
    await nextEvent(el, 'loaded');
    equal(rows(el).length, 0, 'no own requests yet');
    el.shadowRoot.querySelector('.new').click();
    // The submit form is the shared <arazzo-access-request-dialog>, with its own shadow root.
    const dlg = await waitFor(() => el.shadowRoot.querySelector('arazzo-access-request-dialog'));
    dlg.shadowRoot.querySelector('.sub-wf').value = 'nightly-reconcile';
    const submitted = nextEvent(el, 'access-request-submitted');
    dlg.shadowRoot.querySelector('.ok').click();
    const e = await submitted;
    equal(e.detail.request.baseWorkflowId, 'nightly-reconcile', 'the new request targets the chosen workflow');
    // The new request is the demo user's own, so it now appears in "My requests".
    await waitFor(() => rows(el).length === 1);
  });

  it('shows a workflow’s queue and approves a pending request', async () => {
    el = panelWithMock({ view: 'queue', 'base-workflow-id': 'onboard-customer' });
    mount(el);
    await nextEvent(el, 'loaded');
    // onboard-customer's inbox opens to its one pending request (carol's is denied, filtered out by the Pending default).
    equal(rows(el).length, 1, 'one pending request for this workflow');
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

  it('pages the approver inbox with Prev/Next (keyset, small page-size)', async () => {
    // The queue seeds four requests total; with page-size=2 and the status filter cleared, that is two keyset pages.
    el = panelWithMock({ view: 'queue', 'page-size': '2' });
    mount(el);
    await nextEvent(el, 'loaded');
    // Clear the status filter (the Pending default is only two rows → a single page) to expose two pages of four.
    const status = el.shadowRoot.querySelector('.status');
    status.value = '';
    status.dispatchEvent(new Event('change'));
    await nextEvent(el, 'loaded');

    // Page 1: two rows, Prev disabled (nothing before it), Next enabled (a second page exists).
    equal(rows(el).length, 2, 'page 1 shows the page-size (2) rows');
    const prev = el.shadowRoot.querySelector('.pager .prev');
    const next = el.shadowRoot.querySelector('.pager .next');
    ok(prev && next, 'the pager offers Prev and Next');
    ok(prev.disabled, 'Prev is disabled on page 1');
    ok(!next.disabled, 'Next is enabled while another page exists');
    const page1Ids = [...rows(el)].map((r) => r.dataset.id).join(',');

    // Next → page 2: a different set of rows, Prev now enabled, Next disabled (the last page of four).
    next.click();
    await nextEvent(el, 'loaded');
    equal(rows(el).length, 2, 'page 2 shows the remaining two rows');
    const page2Ids = [...rows(el)].map((r) => r.dataset.id).join(',');
    ok(page1Ids !== page2Ids, 'page 2 shows a different keyset page than page 1');
    ok(el.shadowRoot.querySelector('.pager .prev').disabled === false, 'Prev is enabled on page 2');
    ok(el.shadowRoot.querySelector('.pager .next').disabled === true, 'Next is disabled on the last page');

    // Prev → back to page 1's rows.
    el.shadowRoot.querySelector('.pager .prev').click();
    await nextEvent(el, 'loaded');
    equal([...rows(el)].map((r) => r.dataset.id).join(','), page1Ids, 'Prev returns to page 1');
    ok(el.shadowRoot.querySelector('.pager .prev').disabled, 'Prev is disabled again on page 1');
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