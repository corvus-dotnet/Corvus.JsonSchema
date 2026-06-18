// Tier 3 — <arazzo-access-request-dialog> mounted in a real browser against the in-memory mock.
import { ArazzoControlPlaneClient } from '../../src/arazzo-client.js';
import { createMockControlPlane } from '../../demo/mock-api.js';
import '../../src/components/access-request-dialog.js';
import { ok, equal, nextEvent, waitFor, mount } from './helpers.js';

function dialogWithMock() {
  const mock = createMockControlPlane({ latencyMs: 0 });
  const el = document.createElement('arazzo-access-request-dialog');
  el.client = new ArazzoControlPlaneClient({ baseUrl: 'https://mock/arazzo/v1', fetch: mock.fetch });
  return el;
}

const $ = (el, sel) => el.shadowRoot.querySelector(sel);
const $$ = (el, sel) => [...el.shadowRoot.querySelectorAll(sel)];
const cb = (el, scope) => $$(el, '.scope-cb').find((c) => c.value === scope);

describe('<arazzo-access-request-dialog>', () => {
  let el;
  afterEach(() => el?.remove());

  it('submits a free-choice request and emits access-request-submitted', async () => {
    el = dialogWithMock();
    mount(el);
    el.open();
    ok($(el, '.sub-wf'), 'offers a workflow picker when not locked');
    // runs:read is the least-privilege default; runs:write is opt-in.
    ok(cb(el, 'runs:read').checked, 'runs:read is checked by default');
    ok(!cb(el, 'runs:write').checked, 'runs:write is opt-in, not on by default');
    $(el, '.sub-wf').value = 'onboard-customer';
    const submitted = nextEvent(el, 'access-request-submitted');
    $(el, '.ok').click();
    const e = await submitted;
    equal(e.detail.request.baseWorkflowId, 'onboard-customer', 'submits for the chosen workflow');
    ok(e.detail.request.requestedScopes.includes('runs:read'), 'requests the least-privilege read scope by default');
    ok(!e.detail.request.requestedScopes.includes('runs:write'), 'does not request write unless asked');
  });

  it('forces read on (and locks it) whenever write is requested — you cannot operate on runs you cannot read', async () => {
    el = dialogWithMock();
    mount(el);
    el.open();
    // Ticking write pulls read on and disables it so it cannot be unticked.
    cb(el, 'runs:write').checked = true;
    cb(el, 'runs:write').dispatchEvent(new Event('change'));
    ok(cb(el, 'runs:read').checked, 'read is forced on by write');
    ok(cb(el, 'runs:read').disabled, 'read is locked while write is selected');
    $(el, '.sub-wf').value = 'onboard-customer';
    const submitted = nextEvent(el, 'access-request-submitted');
    $(el, '.ok').click();
    const e = await submitted;
    const scopes = e.detail.request.requestedScopes;
    ok(scopes.includes('runs:read') && scopes.includes('runs:write'), 'write requests carry read too');

    // Releasing write re-enables read so it can be deselected again.
    cb(el, 'runs:write').checked = false;
    cb(el, 'runs:write').dispatchEvent(new Event('change'));
    ok(!cb(el, 'runs:read').disabled, 'read is editable again once write is cleared');
  });

  it('locks the workflow when opened from a catalog entry', async () => {
    el = dialogWithMock();
    mount(el);
    el.open({ baseWorkflowId: 'nightly-reconcile', lockWorkflow: true });
    ok(!$(el, '.sub-wf'), 'no workflow picker when locked');
    ok($(el, '.locked-wf').textContent.includes('nightly-reconcile'), 'shows the fixed workflow');
    const submitted = nextEvent(el, 'access-request-submitted');
    $(el, '.ok').click();
    const e = await submitted;
    equal(e.detail.request.baseWorkflowId, 'nightly-reconcile', 'submits for the locked workflow');
  });

  it('refuses to submit without a workflow', async () => {
    el = dialogWithMock();
    mount(el);
    el.open();
    $(el, '.ok').click();
    await waitFor(() => !$(el, '.error-banner').hidden);
    ok($(el, '.error-banner').textContent.toLowerCase().includes('workflow'), 'explains a workflow + scope are required');
  });
});
