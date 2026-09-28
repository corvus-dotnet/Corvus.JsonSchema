// Tier 3 — <arazzo-filter-input>: the kit's generic filtered combo (repo/branch pickers).
import '../../src/components/filter-input.js';
import { ok, equal, waitFor, mount } from './helpers.js';

describe('<arazzo-filter-input>', () => {
  let el;
  afterEach(() => el?.remove());

  function combo(items) {
    el = document.createElement('arazzo-filter-input');
    mount(el);
    el.items = items;
    return el;
  }

  it('focus shows the full list even when a value is committed; typing filters', async () => {
    combo([{ value: 'main', sub: 'default' }, { value: 'release/9.0' }, { value: '__new__', label: '＋ New branch…' }]);
    el.value = 'main';
    const input = el.shadowRoot.querySelector('input');
    input.dispatchEvent(new Event('focus'));
    await waitFor(() => el.shadowRoot.querySelectorAll('li').length === 3, 'focus lists everything, the committed value included');
    input.value = 'rel';
    input.dispatchEvent(new Event('input'));
    await waitFor(() => el.shadowRoot.querySelectorAll('li').length === 1, 'typing filters');
    ok(el.shadowRoot.querySelector('li').textContent.includes('release/9.0'));
  });

  it('closes the list when a typed value is committed, by Enter or by a change', async () => {
    // A value typed in full and committed must not leave the top-layer list over the content below: the next click
    // would land on it, and the list selects on mousedown.
    combo([{ value: 'acme-org/specs' }, { value: 'acme-org/other' }]);
    const input = el.shadowRoot.querySelector('input');
    const list = el.shadowRoot.querySelector('.results');
    input.value = 'acme-org/specs';
    input.dispatchEvent(new Event('input'));
    await waitFor(() => list.matches(':popover-open'), 'typing opens the list');
    input.dispatchEvent(new KeyboardEvent('keydown', { key: 'Enter', bubbles: true }));
    ok(!list.matches(':popover-open'), 'Enter with nothing highlighted closes it');

    input.dispatchEvent(new Event('input'));
    await waitFor(() => list.matches(':popover-open'), 'typing opens it again');
    input.dispatchEvent(new Event('change'));
    ok(!list.matches(':popover-open'), 'a committed change closes it');
  });

  it('a lookup that resolves after disconnect must not throw (the list no-ops off-DOM)', () => {
    combo([{ value: 'alpha' }, { value: 'alnitak' }]);
    el.remove(); // the dialog closing (or test teardown) races a still-pending debounced lookup
    // Drive the render a late lookup would run: pre-fix, showPopover on the now-disconnected popover
    // threw InvalidStateError; the connectedness guard makes rendering off-DOM a silent no-op.
    let threw = null;
    try { el.renderList([{ value: 'alpha' }]); } catch (e) { threw = e; }
    equal(threw, null, 'rendering after disconnect does not throw');
    equal(el.shadowRoot.querySelector('.results').matches(':popover-open'), false, 'the list stays closed off-DOM');
  });

  it('items arriving while the list is focus-opened keep the FULL list, not a filter on the committed value', async () => {
    combo([]);
    el.value = 'main';
    const input = el.shadowRoot.querySelector('input');
    input.focus(); // opens empty — the async load has not landed yet
    el.items = [{ value: 'main', sub: 'default' }, { value: 'release/9.0' }, { value: '__new__', label: '＋ New branch…' }];
    await waitFor(() => el.shadowRoot.querySelectorAll('li').length === 3, 'late items render show-all, the create sentinel included');
  });

  it('a typed value committed by blur reaches host listeners exactly once (a native change is not composed)', async () => {
    combo([{ value: 'acme/specs' }]);
    let commits = 0;
    el.addEventListener('change', () => { commits += 1; });
    const input = el.shadowRoot.querySelector('input');
    input.value = 'dotnet/runtime';
    input.dispatchEvent(new Event('change')); // the browser's blur-commit: bubbles, NOT composed
    equal(commits, 1, 'the host re-dispatches the commit across the shadow boundary');
  });

  it('a listbox pick dispatches one composed change and closes the list', async () => {
    combo([{ value: 'acme/specs' }, { value: 'acme/flows' }]);
    let commits = 0;
    el.addEventListener('change', () => { commits += 1; });
    const input = el.shadowRoot.querySelector('input');
    input.dispatchEvent(new Event('focus'));
    await waitFor(() => el.shadowRoot.querySelectorAll('li').length === 2);
    el.shadowRoot.querySelector('li').dispatchEvent(new Event('mousedown'));
    equal(el.value, 'acme/specs');
    equal(commits, 1, 'one commit per pick');
    ok(el.shadowRoot.querySelector('.results').hidden, 'the list closes');
  });

  it('the async lookup deepens the static matches, deduplicated', async () => {
    combo([{ value: 'dotnet/runtime' }]);
    el.lookup = async () => [{ value: 'dotnet/runtime' }, { value: 'dotnet/sdk' }];
    const input = el.shadowRoot.querySelector('input');
    input.value = 'dotnet/';
    input.dispatchEvent(new Event('input'));
    await waitFor(() => el.shadowRoot.querySelectorAll('li').length === 2, 'lookup results append after the debounce');
    const values = [...el.shadowRoot.querySelectorAll('li')].map((li) => li.dataset.value);
    equal(values.filter((v) => v === 'dotnet/runtime').length, 1, 'deduplicated by value');
  });
});
