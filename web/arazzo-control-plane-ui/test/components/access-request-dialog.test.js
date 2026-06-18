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

describe('<arazzo-access-request-dialog>', () => {
  let el;
  afterEach(() => el?.remove());

  it('submits a free-choice request and emits access-request-submitted', async () => {
    el = dialogWithMock();
    mount(el);
    el.open();
    ok($(el, '.sub-wf'), 'offers a workflow picker when not locked');
    $(el, '.sub-wf').value = 'onboard-customer'; // the runs:write scope is checked by default
    const submitted = nextEvent(el, 'access-request-submitted');
    $(el, '.ok').click();
    const e = await submitted;
    equal(e.detail.request.baseWorkflowId, 'onboard-customer', 'submits for the chosen workflow');
    ok(e.detail.request.requestedScopes.includes('runs:write'), 'requests the default run scope');
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
