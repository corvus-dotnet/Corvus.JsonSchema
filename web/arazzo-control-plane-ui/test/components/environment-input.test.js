// Tier 3 — <arazzo-environment-input> against the in-memory mock: a themed, filtered dropdown of the real
// environments that behaves like a form control (.value / .readOnly) and still accepts a free-typed value.
import { ArazzoControlPlaneClient } from '../../src/arazzo-client.js';
import { createMockControlPlane } from '../../demo/mock-api.js';
import '../../src/components/environment-input.js';
import { ok, equal, waitFor, mount } from './helpers.js';

describe('<arazzo-environment-input>', () => {
  let el;
  afterEach(() => el?.remove());

  function make() {
    const mock = createMockControlPlane({ latencyMs: 0 });
    el = document.createElement('arazzo-environment-input');
    el.client = new ArazzoControlPlaneClient({ baseUrl: 'https://mock/arazzo/v1', fetch: mock.fetch });
    return el;
  }

  const focusOpen = async () => {
    const input = el.shadowRoot.querySelector('input');
    input.focus();
    input.dispatchEvent(new Event('input', { bubbles: true, composed: true }));
    return waitFor(() => el.shadowRoot.querySelector('.results:not([hidden]) li'));
  };

  it('offers the system environments in the dropdown on focus', async () => {
    make();
    mount(el);
    await waitFor(() => el._envs.length > 0); // list loaded from the API
    await focusOpen();
    const names = [...el.shadowRoot.querySelectorAll('.results li')].map((li) => li.dataset.name);
    ok(names.includes('production'), 'offers the production environment');
    ok(names.includes('staging'), 'offers the staging environment');
  });

  it('filters as you type (case-insensitive substring)', async () => {
    make();
    mount(el);
    await waitFor(() => el._envs.length > 0);
    const input = el.shadowRoot.querySelector('input');
    input.value = 'stag';
    input.dispatchEvent(new Event('input', { bubbles: true, composed: true }));
    await waitFor(() => el.shadowRoot.querySelector('.results:not([hidden]) li'));
    const names = [...el.shadowRoot.querySelectorAll('.results li')].map((li) => li.dataset.name);
    equal(names.length, 1, 'narrowed to one match');
    equal(names[0], 'staging', 'matched staging');
  });

  it('selecting a result sets the value and closes the dropdown', async () => {
    make();
    mount(el);
    await waitFor(() => el._envs.length > 0);
    await focusOpen();
    const staging = [...el.shadowRoot.querySelectorAll('.results li')].find((li) => li.dataset.name === 'staging');
    staging.dispatchEvent(new MouseEvent('mousedown', { bubbles: true, composed: true }));
    equal(el.value, 'staging', 'value set to the chosen environment');
    ok(el.shadowRoot.querySelector('.results').hidden, 'dropdown closed after selection');
  });

  it('behaves like a form control — .value and .readOnly delegate to the inner input', async () => {
    make();
    mount(el);
    await waitFor(() => el.shadowRoot.querySelector('input'));
    el.value = 'staging';
    equal(el.value, 'staging', 'value round-trips through the getter');
    equal(el.shadowRoot.querySelector('input').value, 'staging', 'value delegates to the inner input');
    el.readOnly = true;
    ok(el.shadowRoot.querySelector('input').readOnly, 'readOnly delegates to the inner input');
    equal(el.readOnly, true, 'readOnly getter reflects it');
  });

  it('still accepts a free-typed environment (best-effort, not strict)', async () => {
    make();
    mount(el);
    const input = await waitFor(() => el.shadowRoot.querySelector('input'));
    input.value = 'preprod';
    input.dispatchEvent(new Event('input', { bubbles: true, composed: true }));
    equal(el.value, 'preprod', 'accepts a value not present in the list');
  });
});
