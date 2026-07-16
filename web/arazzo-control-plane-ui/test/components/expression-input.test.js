// Tier 3 — <arazzo-expression-input>: the plain-input fallback contract (deterministic, loader
// stubbed) and, when the CDN is reachable, the CM6 upgrade path with root: shadowRoot.
import { ArazzoExpressionInput } from '../../src/components/expression-input.js';
import { ok, equal, nextEvent, waitFor, mount } from './helpers.js';

describe('<arazzo-expression-input>', () => {
  let el;
  const realLoader = { cmLoader: ArazzoExpressionInput.cmLoader, cm: null };

  beforeEach(() => { realLoader.cm = ArazzoExpressionInput._cm; });
  afterEach(() => {
    el?.remove();
    ArazzoExpressionInput.cmLoader = realLoader.cmLoader;
    ArazzoExpressionInput._cm = realLoader.cm;
  });

  function makeOffline() {
    ArazzoExpressionInput._cm = null;
    ArazzoExpressionInput.cmLoader = () => Promise.reject(new Error('offline'));
    el = document.createElement('arazzo-expression-input');
    el.setAttribute('placeholder', '$steps.…');
    mount(el);
    return el;
  }

  it('serves a themed plain input while CM6 is unavailable, with the same value/events', async () => {
    makeOffline();
    await new Promise((r) => setTimeout(r, 10)); // let the rejected upgrade settle
    ok(el.usingFallback, 'fallback in service');
    const input = el.shadowRoot.querySelector('input');
    ok(input, 'plain input rendered');
    equal(input.placeholder, '$steps.…');

    el.value = '$statusCode == 200';
    equal(el.value, '$statusCode == 200', 'value round-trips');

    const changed = nextEvent(el, 'value-changed');
    input.value = '$statusCode == 201';
    input.dispatchEvent(new Event('input', { bubbles: true }));
    equal((await changed).detail.value, '$statusCode == 201');

    const committed = nextEvent(el, 'commit');
    input.dispatchEvent(new KeyboardEvent('keydown', { key: 'Enter', bubbles: true }));
    equal((await committed).detail.value, '$statusCode == 201');
  });

  it('runs the validator debounced and surfaces errors', async () => {
    makeOffline();
    el.validator = async (value) => (value.includes('$bogus')
      ? { valid: false, errors: [{ message: "unknown root '$bogus'" }] }
      : { valid: true });
    el.value = '$bogus.thing';
    const validated = await nextEvent(el, 'validated');
    equal(validated.detail.valid, false);
    ok(el.shadowRoot.querySelector('.xin').classList.contains('invalid'), 'invalid style applied');
    ok(el.shadowRoot.querySelector('.err').textContent.includes('$bogus'), 'message shown');

    el.value = '$statusCode == 200';
    const revalidated = await nextEvent(el, 'validated');
    equal(revalidated.detail.valid, true);
    ok(!el.shadowRoot.querySelector('.xin').classList.contains('invalid'), 'invalid style cleared');
  });

  it('readonly reflects onto the fallback input', async () => {
    makeOffline();
    el.setAttribute('readonly', '');
    ok(el.shadowRoot.querySelector('input').readOnly);
  });

  it('upgrades to CodeMirror in the shadow root from the vendored bundle', async function () {
    this.timeout(20000);
    el = document.createElement('arazzo-expression-input');
    el.setAttribute('value', '$steps.authorize-payment.outputs.authorizationId');
    el.completionContext = { steps: { 'authorize-payment': { outputs: ['authorizationId'] } } };
    mount(el);
    await ArazzoExpressionInput.loadCm(); // vendored — no network involved
    await waitFor(() => !el.usingFallback, 15000);
    const cmEditor = el.shadowRoot.querySelector('.cm-editor');
    ok(cmEditor, 'CM6 mounted inside the shadow root');
    equal(el.value, '$steps.authorize-payment.outputs.authorizationId', 'value carried over');
    await waitFor(() => el.shadowRoot.querySelector('.cm-line [class*="ͼ"], .cm-line span[class]'), 5000);
    ok(el.shadowRoot.querySelector('.cm-line span[class]'), 'syntax highlighting produced styled spans');

    const changed = nextEvent(el, 'value-changed');
    el.value = '$statusCode == 200';
    equal((await changed).detail.value, '$statusCode == 200', 'programmatic set emits change');
  });
});
