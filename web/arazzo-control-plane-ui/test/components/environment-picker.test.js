// Tier 3 — <arazzo-environment-picker> mounted in a real browser against the in-memory mock: the standard
// reach-scoped environment autocomplete (the environment sibling of the workflow/grantee pickers).
import { ArazzoControlPlaneClient } from '../../src/arazzo-client.js';
import { createMockControlPlane } from '../../demo/mock-api.js';
import '../../src/components/environment-picker.js';
import { ok, equal, nextEvent, waitFor, mount } from './helpers.js';

function pickerWithMock(attrs = {}) {
  const mock = createMockControlPlane({ latencyMs: 0 });
  const el = document.createElement('arazzo-environment-picker');
  for (const [k, v] of Object.entries(attrs)) el.setAttribute(k, v);
  el.client = new ArazzoControlPlaneClient({ baseUrl: 'https://mock/arazzo/v1', fetch: mock.fetch });
  return el;
}

const type = (el, text) => {
  const input = el.shadowRoot.querySelector('.q');
  input.value = text;
  input.dispatchEvent(new Event('input'));
};
const results = (el) => el.shadowRoot.querySelectorAll('.results li[data-index]');

describe('<arazzo-environment-picker>', () => {
  let el;
  afterEach(() => el?.remove());

  it('suggests registry environments as you type ("prod" finds production)', async () => {
    el = pickerWithMock();
    mount(el);
    type(el, 'prod');
    await waitFor(() => results(el).length === 1);
    ok(results(el)[0].textContent.includes('production'), 'substring match over the registry');
  });

  it('picking emits environment-picked + change and renders a clearable chip; clearing fires change again', async () => {
    el = pickerWithMock();
    mount(el);
    type(el, 'staging');
    await waitFor(() => results(el).length === 1);
    const picked = nextEvent(el, 'environment-picked');
    const changed = nextEvent(el, 'change');
    results(el)[0].click();
    equal((await picked).detail.name, 'staging', 'the picked name rides the event');
    await changed;
    equal(el.value, 'staging', '.value mirrors the pick');
    ok(el.shadowRoot.querySelector('.chip'), 'a chip replaces the input');
    const cleared = nextEvent(el, 'change');
    el.shadowRoot.querySelector('.clear').click();
    await cleared;
    equal(el.value, '', 'clearing empties .value');
  });

  it('round-trips an initial value attribute as a preselected chip', async () => {
    el = pickerWithMock({ value: 'development' });
    mount(el);
    equal(el.value, 'development');
    ok(el.shadowRoot.querySelector('.chip')?.textContent.includes('development'), 'renders the preselected chip');
  });
});