// Tier 3 — <arazzo-git-tree>: a lazy tree over a fake loader — children fetch ON EXPAND only,
// fetched levels cache, and picking respects the mode and file filter.
import '../../src/components/git-tree.js';
import { ok, equal, nextEvent, waitFor, mount } from './helpers.js';

describe('<arazzo-git-tree>', () => {
  let el;
  afterEach(() => el?.remove());

  const WORLD = {
    '': [{ name: 'flows', path: 'flows', type: 'dir' }, { name: 'README.md', path: 'README.md', type: 'file' }],
    flows: [{ name: 'deep', path: 'flows/deep', type: 'dir' }, { name: 'adopt.arazzo.json', path: 'flows/adopt.arazzo.json', type: 'file' }],
    'flows/deep': [{ name: 'nested.json', path: 'flows/deep/nested.json', type: 'file' }],
  };

  function make(mode) {
    el = document.createElement('arazzo-git-tree');
    el.mode = mode;
    el.calls = [];
    el.loader = async (path) => { el.calls.push(path); return WORLD[path] ?? []; };
    if (mode === 'file') el.pickableFile = (entry) => entry.name.endsWith('.json');
    mount(el);
    return el;
  }

  it('fetches lazily: only the expanded directory loads, and only once', async () => {
    make('file');
    await waitFor(() => el.shadowRoot.textContent.includes('flows'));
    equal(el.calls.join(','), '', 'only the root loaded');

    const dir = [...el.shadowRoot.querySelectorAll('button.entry')].find((b) => b.textContent.includes('flows'));
    dir.click();
    await waitFor(() => el.shadowRoot.textContent.includes('adopt.arazzo.json'));
    equal(el.calls.join(','), ',flows', 'expanding fetched exactly that level; flows/deep is untouched');

    dir.click(); // collapse
    dir.click(); // re-expand — cached, no refetch
    await waitFor(() => el.shadowRoot.textContent.includes('adopt.arazzo.json'));
    equal(el.calls.join(','), ',flows', 'collapse/expand reuses the cache');
  });

  it('file mode picks matching files and disables the rest', async () => {
    make('file');
    await waitFor(() => el.shadowRoot.textContent.includes('README'));
    const readme = [...el.shadowRoot.querySelectorAll('button.entry')].find((b) => b.textContent.includes('README'));
    ok(readme.disabled, 'non-matching files are visible but not pickable');

    [...el.shadowRoot.querySelectorAll('button.entry')].find((b) => b.textContent.includes('flows')).click();
    const picked = nextEvent(el, 'picked');
    const file = await waitFor(() => [...el.shadowRoot.querySelectorAll('button.entry')].find((b) => b.textContent.includes('adopt.arazzo.json')));
    file.click();
    equal((await picked).detail.path, 'flows/adopt.arazzo.json');
  });

  it('dir mode picks directories via their pick affordance', async () => {
    make('dir');
    const pick = await waitFor(() => el.shadowRoot.querySelector('.pick-dir'));
    const picked = nextEvent(el, 'picked');
    pick.click();
    equal((await picked).detail.path, 'flows');
  });
});
