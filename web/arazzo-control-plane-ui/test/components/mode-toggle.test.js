// Tier 3 — <arazzo-mode-toggle>: the shared segmented control (Form | JSON, etc.).
import '../../src/components/mode-toggle.js';
import { ok, equal, nextEvent, mount } from './helpers.js';

describe('<arazzo-mode-toggle>', () => {
  let el;
  afterEach(() => el?.remove());

  function make(value) {
    el = document.createElement('arazzo-mode-toggle');
    el.options = [{ value: 'form', label: 'Form' }, { value: 'json', label: 'JSON' }];
    if (value) el.value = value;
    mount(el);
    return el;
  }
  const btn = (v) => el.shadowRoot.querySelector(`button[data-value="${v}"]`);

  it('renders a button per option and presses the current value (defaulting to the first)', () => {
    make();
    equal(el.shadowRoot.querySelectorAll('button').length, 2);
    equal(btn('form').getAttribute('aria-pressed'), 'true', 'defaults to the first option');
    equal(btn('json').getAttribute('aria-pressed'), 'false');
    equal(btn('form').textContent, 'Form');
  });

  it('a click fires mode-changed and moves the pressed state', async () => {
    make('form');
    const changed = nextEvent(el, 'mode-changed');
    btn('json').click();
    equal((await changed).detail.value, 'json');
    equal(btn('json').getAttribute('aria-pressed'), 'true');
    equal(btn('form').getAttribute('aria-pressed'), 'false');
    equal(el.value, 'json', 'the property tracks the selection');
  });

  it('setting value reflects the pressed state WITHOUT firing an event', async () => {
    make('form');
    let fired = false;
    el.addEventListener('mode-changed', () => { fired = true; });
    el.value = 'json';
    equal(btn('json').getAttribute('aria-pressed'), 'true');
    ok(!fired, 'a programmatic set is silent');
  });

  it('disabled swallows clicks', () => {
    make('form');
    el.disabled = true;
    btn('json').click();
    equal(el.value, 'form', 'a disabled toggle does not change');
    ok(btn('json').disabled);
  });
});
