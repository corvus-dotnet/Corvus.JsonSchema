// Tier 3 — <arazzo-resume-dialog>: the four resume modes against the in-memory mock.
import { ArazzoControlPlaneClient } from '../../src/arazzo-client.js';
import { createMockControlPlane } from '../../demo/mock-api.js';
import '../../src/components/resume-dialog.js';
import { ok, waitFor, mount } from './helpers.js';

describe('<arazzo-resume-dialog>', () => {
  let el;
  afterEach(() => { try { el?.close(); } catch { /* ignore */ } el?.remove(); });

  function open(run) {
    const mock = createMockControlPlane({ latencyMs: 0 });
    el = document.createElement('arazzo-resume-dialog');
    el.client = new ArazzoControlPlaneClient({ baseUrl: 'https://mock/arazzo/v1', fetch: mock.fetch });
    mount(el);
    el.open(run);
    return el;
  }

  it('records skip outputs only when the operator opts in', async () => {
    open({ id: 'run-b2c3d4e5', workflowId: 'adopt-pet-v1', cursor: 2, fault: { stepId: 'submitAdoption', attempt: 1 } });

    const skip = await waitFor(() => [...el.shadowRoot.querySelectorAll('input[name="mode"]')].find((r) => r.value === 'Skip'));
    skip.checked = true;
    skip.dispatchEvent(new Event('change'));

    ok(el.shadowRoot.querySelector('.skip-outputs').hidden, 'the outputs builder is hidden by default');
    ok(!('skipOutputs' in el.buildRequest()), 'no outputs are recorded while the checkbox is unchecked');

    const record = el.shadowRoot.querySelector('.record-outputs');
    record.checked = true;
    record.dispatchEvent(new Event('change'));
    ok(!el.shadowRoot.querySelector('.skip-outputs').hidden, 'checking the box reveals the outputs builder');
  });

  it('blocks a Skip whose recorded outputs fail server-side validation', async () => {
    open({ id: 'run-aa11bb22', workflowId: 'onboard-customer-v1', cursor: 1, fault: { stepId: 'verifyIdentity', attempt: 1 } });
    await waitFor(() => el._skipStepId, 'skip target resolves from the catalog');

    const skip = [...el.shadowRoot.querySelectorAll('input[name="mode"]')].find((r) => r.value === 'Skip');
    skip.checked = true;
    skip.dispatchEvent(new Event('change'));
    const record = el.shadowRoot.querySelector('.record-outputs');
    record.checked = true;
    record.dispatchEvent(new Event('change'));

    // Use the raw-JSON builder for a simple value, and stub the client to fail validation + track resume.
    const builder = el.shadowRoot.querySelector('.skip-builder');
    builder.descriptor = null;
    const textarea = await waitFor(() => builder.shadowRoot.querySelector('textarea'));
    textarea.value = '{ "score": 5 }';

    let resumed = false;
    el.client.resumeRun = async () => { resumed = true; return {}; };
    el.client.validateCatalogValue = async () => ({ valid: false, errors: [{ instancePath: '/score', message: 'must be <= 1' }] });

    await el.submit();

    ok(!el.shadowRoot.querySelector('.error-banner').hidden, 'surfaces the validation error');
    ok(el.shadowRoot.querySelector('.error-banner').textContent.includes('score'), 'names the failing path');
    ok(!resumed, 'does not resume when the outputs are invalid');
  });
});
