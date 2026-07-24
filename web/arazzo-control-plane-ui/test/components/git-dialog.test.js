// Tier 3 — <arazzo-git-dialog> mounted in a real browser against the in-memory mock: bind a
// working copy to a branch, commit (authored as the signed-in user, §4.7), and pull back.
import { ArazzoControlPlaneClient } from '../../src/arazzo-client.js';
import { createMockControlPlane } from '../../demo/mock-api.js';
import '../../src/components/git-dialog.js';
import { ok, equal, nextEvent, waitFor, mount } from './helpers.js';

async function dialogWithMock() {
  const mock = createMockControlPlane({ latencyMs: 0 });
  const client = new ArazzoControlPlaneClient({ baseUrl: 'https://mock/arazzo/v1', fetch: mock.fetch });
  const wc = await client.createWorkingCopy({
    name: 'adopt',
    document: { arazzo: '1.1.0', info: { title: 'Adopt', version: '1' }, workflows: [{ workflowId: 'adopt', steps: [] }] },
  });
  await client.putScenario(wc.id, { name: 'happy', expect: { outcome: 'completed' } });
  const el = document.createElement('arazzo-git-dialog');
  el.client = client;
  mount(el);
  return { el, client, wc, mock };
}

describe('<arazzo-git-dialog>', () => {
  let el;
  afterEach(() => el?.remove());

  it('binds, commits as the signed-in user, and pulls back under the etag guard', async () => {
    const ctx = await dialogWithMock();
    el = ctx.el;
    el.open({ workingCopyId: ctx.wc.id });

    // Connect through the injected popup (the mock's authorize URL self-completes).
    const gh = await waitFor(() => el.shadowRoot.querySelector('.gh-connect'));
    gh.pollIntervalMs = 10;
    gh.windowOpener = (url) => { ctx.mock.fetch(url); return { closed: false, close() { this.closed = true; } }; };
    (await waitFor(() => gh.shadowRoot.querySelector('.connect'))).click();
    await waitFor(() => (el.shadowRoot.querySelector('.b-repo').items ?? []).length > 0, 'the repositories seed the binding form');

    // Bind: repo from the session; the branch picker browses the repo's REAL branches.
    const sel = el.shadowRoot.querySelector('.b-repo');
    sel.value = 'acme-org/specs';
    sel.dispatchEvent(new Event('change'));
    const branchSel = await waitFor(() => {
      const b = el.shadowRoot.querySelector('.b-branch');
      return b.disabled || !(b.items ?? []).length ? null : b;
    }, 'the branch picker loads the repository branches');
    ok(branchSel.items.some((i) => i.value === 'main'), 'the default branch lists');

    // The typeahead deepens the combo: an owner-qualified query lists repositories the session's
    // seed never contains (the mock's canned public catalog), after the debounce.
    const repoBox = el.shadowRoot.querySelector('.b-repo');
    const repoField = repoBox.shadowRoot.querySelector('input');
    repoField.value = 'dotnet/';
    repoField.dispatchEvent(new Event('input'));
    await waitFor(() => [...repoBox.shadowRoot.querySelectorAll('li')].some((li) => li.textContent.includes('dotnet/runtime')), 'the lookup lists the owner-qualified match');
    repoField.value = 'acme-org/specs';
    repoField.dispatchEvent(new Event('input'));
    repoField.dispatchEvent(new Event('change', { bubbles: true, composed: true }));

    // ＋ New branch…: create feature/adopt from main — a ref only, then it selects itself.
    branchSel.value = '__new__';
    branchSel.dispatchEvent(new Event('change'));
    const nb = el.shadowRoot.querySelector('.new-branch');
    ok(!nb.hidden, 'the create form reveals');
    el.shadowRoot.querySelector('.nb-name').value = 'feature/adopt';
    el.shadowRoot.querySelector('.nb-create').click();
    await waitFor(() => el.shadowRoot.querySelector('.b-branch').value === 'feature/adopt', 'the created branch selects itself');

    el.shadowRoot.querySelector('.b-path').value = 'flows/adopt.arazzo.json';
    el.shadowRoot.querySelector('.b-scenarios').value = 'scenarios/adopt';
    el.shadowRoot.querySelector('.b-path').dispatchEvent(new Event('input'));
    const boundEvent = nextEvent(el, 'binding-saved');
    (await waitFor(() => {
      const b = el.shadowRoot.querySelector('.save-binding');
      return b.disabled ? null : b;
    })).click();
    const bound = await boundEvent;
    equal(bound.detail.workingCopy.gitBinding.branch, 'feature/adopt', 'the binding persisted');

    // Commit: the document and the scenario file, plus a draft PR — authored as the user.
    el.shadowRoot.querySelector('.c-message').value = 'sync';
    el.shadowRoot.querySelector('.c-message').dispatchEvent(new Event('input'));
    el.shadowRoot.querySelector('.c-pr').checked = true;
    const committedEvent = nextEvent(el, 'committed');
    (await waitFor(() => {
      const b = el.shadowRoot.querySelector('.commit');
      return b.disabled ? null : b;
    })).click();
    const committed = await committedEvent;
    equal(committed.detail.result.files.map((f) => f.path).join(','), 'flows/adopt.arazzo.json,scenarios/adopt/happy.scenario.json');
    ok(committed.detail.result.pullRequest.url.includes('/pull/'), 'the draft pull request opened');
    ok(el.shadowRoot.querySelector('.result').textContent.includes('octo'), 'the result names the signed-in identity');

    // Pull: refreshes from the branch and emits the updated working copy (new etag).
    const pulledEvent = nextEvent(el, 'pulled');
    el.shadowRoot.querySelector('.pull').click();
    // Pull REPLACES the working copy — the standard danger dialog says so first.
    const ask = el.shadowRoot.querySelector('arazzo-input-dialog');
    (await waitFor(() => {
      const b = ask.shadowRoot.querySelector('.confirm.danger');
      return b && ask.shadowRoot.querySelector('dialog').open ? b : null;
    }, 'the pull confirmation')).click();
    const pulled = await pulledEvent;
    equal(pulled.detail.workingCopy.document.info.title, 'Adopt', 'the document round-tripped');
    ok(pulled.detail.workingCopy.etag !== bound.detail.workingCopy.etag, 'the pull bumped the etag');
  });

  it('browses history, compares side-by-side, and rolls back via pull-at-ref (snag 9)', async () => {
    const ctx = await dialogWithMock();
    el = ctx.el;
    el.open({ workingCopyId: ctx.wc.id });
    const gh = await waitFor(() => el.shadowRoot.querySelector('.gh-connect'));
    gh.pollIntervalMs = 10;
    gh.windowOpener = (url) => { ctx.mock.fetch(url); return { closed: false, close() { this.closed = true; } }; };
    (await waitFor(() => gh.shadowRoot.querySelector('.connect'))).click();
    await waitFor(() => (el.shadowRoot.querySelector('.b-repo').items ?? []).length > 0);

    // Bind to main at the seeded document — the branch that carries the seeded history.
    const sel = el.shadowRoot.querySelector('.b-repo');
    sel.value = 'acme-org/specs';
    sel.dispatchEvent(new Event('change'));
    await waitFor(() => {
      const b = el.shadowRoot.querySelector('.b-branch');
      return b.disabled || !(b.items ?? []).length ? null : b;
    });
    el.shadowRoot.querySelector('.b-path').value = 'flows/adopt.arazzo.json';
    el.shadowRoot.querySelector('.b-path').dispatchEvent(new Event('input'));
    const boundEvent = nextEvent(el, 'binding-saved');
    (await waitFor(() => {
      const b = el.shadowRoot.querySelector('.save-binding');
      return b.disabled ? null : b;
    })).click();
    await boundEvent;

    // The history section reveals as the headered list: title + count, header actions disabled
    // until something is selected, the bound branch's commits newest first.
    const rows = await waitFor(() => {
      const r = [...el.shadowRoot.querySelectorAll('.hist-commit')];
      return r.length === 3 ? r : null;
    }, 'the seeded history loads');
    ok(rows[0].textContent.includes('Confirm adoptions'), 'the newest commit lists first');
    ok(rows[2].textContent.includes('Initial adopt workflow'), 'the oldest commit lists last');
    ok(el.shadowRoot.querySelector('.hist .count').textContent.includes('3 commits'), 'the header counts the commits');
    const cmpBtn = el.shadowRoot.querySelector('.hist-cmp');
    const rollbackBtn = el.shadowRoot.querySelector('.hist-rollback');
    ok(cmpBtn.disabled && rollbackBtn.disabled, 'no selection → both header actions disabled');

    // Selecting the oldest commit enables both; Compare "with the working copy" opens the
    // reusable side-by-side visualizer: two read-only design surfaces.
    rows[2].click();
    equal(rows[2].getAttribute('aria-selected'), 'true', 'the row selects');
    ok(!cmpBtn.disabled && !rollbackBtn.disabled, 'one selection → Compare and Roll back enable');
    cmpBtn.click();
    const withWc = await waitFor(() => [...el.shadowRoot.querySelectorAll('.cmp-menu .menu-item')]
      .find((m) => m.textContent.includes('working copy')), 'the compare menu offers the working copy');
    withWc.click();
    const compare = el.shadowRoot.querySelector('arazzo-workflow-compare');
    await waitFor(() => compare.shadowRoot?.querySelector('dialog[open]'), 'the comparison dialog opens');
    const surfaces = compare.shadowRoot.querySelectorAll('arazzo-design-surface');
    equal(surfaces.length, 2, 'two surfaces render side-by-side');
    ok([...surfaces].every((s) => s.hasAttribute('readonly')), 'both sides are read-only');
    compare.close();

    // Roll back (header action, exactly one selection): a danger-confirmed pull at that ref
    // replaces the working copy — the binding never changes, so the next commit records it.
    const pulledEvent = nextEvent(el, 'pulled');
    rollbackBtn.click();
    const ask = el.shadowRoot.querySelector('arazzo-input-dialog');
    (await waitFor(() => {
      const b = ask.shadowRoot.querySelector('.confirm.danger');
      return b && ask.shadowRoot.querySelector('dialog').open ? b : null;
    }, 'the rollback confirmation')).click();
    const pulled = await pulledEvent;
    equal(pulled.detail.workingCopy.document.workflows[0].steps.length, 2, 'the oldest commit state replaced the working copy');
    equal(pulled.detail.workingCopy.gitBinding.branch, 'main', 'a rollback never rebinds');
  });

  it('history selection: max two (third pick evicts the first), predecessor + pair compares, rollback gating', async () => {
    const ctx = await dialogWithMock();
    el = ctx.el;
    el.open({ workingCopyId: ctx.wc.id });
    const gh = await waitFor(() => el.shadowRoot.querySelector('.gh-connect'));
    gh.pollIntervalMs = 10;
    gh.windowOpener = (url) => { ctx.mock.fetch(url); return { closed: false, close() { this.closed = true; } }; };
    (await waitFor(() => gh.shadowRoot.querySelector('.connect'))).click();
    await waitFor(() => (el.shadowRoot.querySelector('.b-repo').items ?? []).length > 0);
    const sel = el.shadowRoot.querySelector('.b-repo');
    sel.value = 'acme-org/specs';
    sel.dispatchEvent(new Event('change'));
    await waitFor(() => {
      const b = el.shadowRoot.querySelector('.b-branch');
      return b.disabled || !(b.items ?? []).length ? null : b;
    });
    el.shadowRoot.querySelector('.b-path').value = 'flows/adopt.arazzo.json';
    el.shadowRoot.querySelector('.b-path').dispatchEvent(new Event('input'));
    const boundEvent = nextEvent(el, 'binding-saved');
    (await waitFor(() => {
      const b = el.shadowRoot.querySelector('.save-binding');
      return b.disabled ? null : b;
    })).click();
    await boundEvent;
    const rows = await waitFor(() => {
      const r = [...el.shadowRoot.querySelectorAll('.hist-commit')];
      return r.length === 3 ? r : null;
    });
    const selected = () => [...el.shadowRoot.querySelectorAll('.hist-commit[aria-selected="true"]')];
    const rollbackBtn = el.shadowRoot.querySelector('.hist-rollback');
    const cmpBtn = el.shadowRoot.querySelector('.hist-cmp');

    // One selection: the menu offers working copy AND the immediate predecessor.
    rows[0].click();
    cmpBtn.click();
    let items = [...el.shadowRoot.querySelectorAll('.cmp-menu .menu-item')];
    equal(items.length, 2, 'one selection → two compare choices');
    ok(items.some((m) => m.textContent.includes('predecessor') && !m.disabled), 'the newest commit has a predecessor');

    // Two selections: rollback disables, the menu collapses to the pair compare (older ↔ newer).
    rows[2].click();
    equal(selected().length, 2);
    ok(rollbackBtn.disabled, 'two selections → Roll back disabled');
    cmpBtn.click();
    items = [...el.shadowRoot.querySelectorAll('.cmp-menu .menu-item')];
    equal(items.length, 1, 'two selections → one compare choice');
    ok(items[0].textContent.includes('older → newer'), 'the pair compares older to newer');
    items[0].click();
    const compare = el.shadowRoot.querySelector('arazzo-workflow-compare');
    await waitFor(() => compare.shadowRoot?.querySelector('dialog[open]'), 'the pair comparison opens');
    compare.close();

    // A third pick evicts the EARLIEST pick — never more than two, never a dead click.
    rows[1].click();
    equal(selected().length, 2, 'still two selected');
    equal(rows[0].getAttribute('aria-selected'), 'false', 'the earliest pick was evicted');
    ok(el.shadowRoot.querySelector('.hist .count').textContent.includes('2 selected'), 'the header reports the selection');

    // Deselect both: actions disable again; the oldest commit's predecessor item is disabled.
    rows[1].click();
    rows[2].click();
    rows[2].click(); // reselect just the oldest
    cmpBtn.click();
    items = [...el.shadowRoot.querySelectorAll('.cmp-menu .menu-item')];
    const pred = items.find((m) => m.textContent.includes('predecessor'));
    ok(pred.disabled, 'the first commit has no predecessor — the item is disabled, not hidden');
  });
});