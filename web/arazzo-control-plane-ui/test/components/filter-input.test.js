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
