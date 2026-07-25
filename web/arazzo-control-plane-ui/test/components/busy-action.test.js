// ArazzoElement.runAction — the kit's shared "busy action": re-entrancy protection for a triggered
// async action, plus a spinner that only appears once the work outlives the delay threshold.
import { ArazzoElement, define } from '../../src/components/base.js';
import { ok, equal, waitFor, mount } from './helpers.js';

// A throwaway element with a single button, to exercise runAction directly.
class BusyProbe extends ArazzoElement {
  connectedCallback() { this.shadowRoot.innerHTML = '<button class="go">Go</button>'; }
}
define('busy-probe', BusyProbe);

describe('ArazzoElement.runAction', () => {
  let el;
  afterEach(() => el?.remove());
  function probe() { el = mount(document.createElement('busy-probe')); return el; }

  // A promise you resolve/reject by hand, so a test controls exactly when the work settles.
  function deferred() {
    let resolve; let reject;
    const promise = new Promise((res, rej) => { resolve = res; reject = rej; });
    return { promise, resolve, reject };
  }

  it('blocks re-entrancy: a second activation while in flight never re-runs the work', async () => {
    probe();
    const btn = el.$('.go');
    let runs = 0;
    const gate = deferred();
    const work = () => { runs += 1; return gate.promise; };
    const p1 = el.runAction(btn, work);
    const p2 = el.runAction(btn, work); // the double-click — must be ignored
    equal(runs, 1, 'the work started exactly once');
    equal(await p2, undefined, 'the re-entrant call resolves undefined without running the work');
    gate.resolve('done');
    equal(await p1, 'done', 'the first call returns the work result');
  });

  it('shows the spinner only after the delay, then clears it on settle', async () => {
    probe();
    const btn = el.$('.go');
    const gate = deferred();
    const p = el.runAction(btn, () => gate.promise, { delay: 20 });
    equal(btn.getAttribute('aria-busy'), null, 'no spinner at the instant of activation');
    await waitFor(() => btn.getAttribute('aria-busy') === 'true', 'the spinner appears once the work outlives the delay');
    gate.resolve();
    await p;
    equal(btn.getAttribute('aria-busy'), null, 'the spinner clears on settle');
  });

  it('a fast action never flashes a spinner', async () => {
    probe();
    const btn = el.$('.go');
    await el.runAction(btn, () => Promise.resolve('quick'), { delay: 50 });
    equal(btn.getAttribute('aria-busy'), null, 'the work settled before the delay, so no spinner ever showed');
  });

  it('restores the trigger and releases the guard after the work throws', async () => {
    probe();
    const btn = el.$('.go');
    let threw = null;
    try { await el.runAction(btn, () => Promise.reject(new Error('boom')), { delay: 0 }); }
    catch (e) { threw = e; }
    ok(threw, 'the work error propagates to the caller');
    equal(btn.getAttribute('aria-busy'), null, 'aria-busy is cleared even on failure');
    equal(await el.runAction(btn, () => Promise.resolve('ok')), 'ok', 'the guard released, so a retry runs');
  });

  it('runs the work directly when there is no trigger to guard', async () => {
    probe();
    equal(await el.runAction(null, () => Promise.resolve('ran')), 'ran', 'a null trigger just runs the work');
  });
});
