// Tier 3 — <arazzo-schedules> mounted in a real browser against a scripted fetch.
import '../../src/components/schedules-panel.js';
import { ok, equal, nextEvent, mount, waitFor } from './helpers.js';

const SCHEDULE = {
  scheduleId: 'nightly', environment: 'development', targetBaseWorkflowId: 'reconcile', targetVersionNumber: 2,
  targetWorkflowId: 'reconcile-v2', cron: '0 3 * * *', timeZone: 'UTC', includeSeconds: false, status: 'Suspended',
  createdAt: '2026-09-01T00:00:00Z',
};

const json = (status, body) => new Response(JSON.stringify(body), { status, headers: { 'Content-Type': 'application/json' } });

/** A control plane with one schedule whose run-now answers `runNow`. */
function panel(runNow) {
  const el = document.createElement('arazzo-schedules');
  el.setAttribute('base-url', 'https://cp/arazzo/v1');
  el.setAttribute('poll', '0');
  el.fetch = async (input, init = {}) => {
    const url = new URL(typeof input === 'string' ? input : input.url);
    if (url.pathname.endsWith('/schedules')) return json(200, { schedules: [SCHEDULE], nextPageToken: null });
    if (url.pathname.endsWith('/run-now') && (init.method || 'GET') === 'POST') return runNow();
    if (url.pathname.endsWith('/runners')) return json(200, { runners: [], nextPageToken: null });
    return json(404, { title: 'Not found' });
  };
  return el;
}

async function runNowAndConfirm(el) {
  el.shadowRoot.querySelector('[data-run="nightly"]').click();
  const dialog = await waitFor(() => el.shadowRoot.querySelector('dialog.arazzo-confirm'));
  dialog.querySelector('button.ok').click();
  return waitFor(() => {
    const flash = el.shadowRoot.querySelector('.flash');
    return flash && !flash.hidden && flash.textContent.trim() ? flash : null;
  });
}

describe('<arazzo-schedules>', () => {
  let el;
  afterEach(() => el?.remove());

  it('flashes the started run after Run now', async () => {
    el = mount(panel(() => json(202, { runId: 'r-1', workflowId: 'reconcile-v2' })));
    await nextEvent(el, 'loaded');
    const flash = await runNowAndConfirm(el);
    ok(flash.classList.contains('ok'));
    ok(flash.textContent.includes('reconcile-v2'), flash.textContent);
  });

  it('says why a refused Run now was refused: the validation result names what the target inputs lack', async () => {
    el = mount(panel(() => json(422, {
      valid: false,
      errors: [
        { message: 'The value was expected to match the subschema.', schemaLocation: '/$defs/__corvusTarget' },
        { message: "Required property not present 'date'", instancePath: '/date', schemaLocation: '/$defs/__corvusTarget/required' },
      ],
    })));
    await nextEvent(el, 'loaded');
    const flash = await runNowAndConfirm(el);
    ok(flash.classList.contains('err'), flash.className);
    ok(flash.textContent.includes("Required property not present 'date'"), flash.textContent);
    equal(el.shadowRoot.querySelectorAll('.flash').length, 1);
  });

  it('detaches on remove() like any element: its delete action does not shadow Element.remove', async () => {
    el = mount(panel(() => json(202, {})));
    await nextEvent(el, 'loaded');
    const result = el.remove();
    equal(result, undefined, 'Element.remove, not an async action');
    ok(!el.isConnected, 'detached');
    equal(el.shadowRoot.querySelector('dialog.arazzo-confirm'), null, 'no delete confirmation opened');
  });
});
