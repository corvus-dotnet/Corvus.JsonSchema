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
    await waitFor(() => [...el.shadowRoot.querySelectorAll('.b-repo option')].length > 1, 'the repositories seed the binding form');

    // Bind: repo from the session; the branch picker browses the repo's REAL branches.
    const sel = el.shadowRoot.querySelector('.b-repo');
    sel.value = 'acme-org/specs';
    sel.dispatchEvent(new Event('change'));
    const branchSel = await waitFor(() => {
      const b = el.shadowRoot.querySelector('.b-branch');
      return b.disabled || !b.options.length ? null : b;
    }, 'the branch picker loads the repository branches');
    ok([...branchSel.options].some((o) => o.value === 'main'), 'the default branch lists');

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
    await waitFor(() => [...el.shadowRoot.querySelectorAll('.b-repo option')].length > 1);

    // Bind to main at the seeded document — the branch that carries the seeded history.
    const sel = el.shadowRoot.querySelector('.b-repo');
    sel.value = 'acme-org/specs';
    sel.dispatchEvent(new Event('change'));
    await waitFor(() => {
      const b = el.shadowRoot.querySelector('.b-branch');
      return b.disabled || !b.options.length ? null : b;
    });
    el.shadowRoot.querySelector('.b-path').value = 'flows/adopt.arazzo.json';
    el.shadowRoot.querySelector('.b-path').dispatchEvent(new Event('input'));
    const boundEvent = nextEvent(el, 'binding-saved');
    (await waitFor(() => {
      const b = el.shadowRoot.querySelector('.save-binding');
      return b.disabled ? null : b;
    })).click();
    await boundEvent;

    // The history section reveals with the bound branch's commits, newest first.
    const rows = await waitFor(() => {
      const r = [...el.shadowRoot.querySelectorAll('.hist-commit')];
      return r.length === 3 ? r : null;
    }, 'the seeded history loads');
    ok(rows[0].textContent.includes('Confirm adoptions'), 'the newest commit lists first');
    ok(rows[2].textContent.includes('Initial adopt workflow'), 'the oldest commit lists last');

    // Compare opens the reusable side-by-side visualizer: two read-only design surfaces.
    rows[2].querySelector('.cmp').click();
    const compare = el.shadowRoot.querySelector('arazzo-workflow-compare');
    await waitFor(() => compare.shadowRoot?.querySelector('dialog[open]'), 'the comparison dialog opens');
    const surfaces = compare.shadowRoot.querySelectorAll('arazzo-design-surface');
    equal(surfaces.length, 2, 'two surfaces render side-by-side');
    ok([...surfaces].every((s) => s.hasAttribute('readonly')), 'both sides are read-only');
    compare.close();

    // Roll back to the oldest commit: a danger-confirmed pull at that ref replaces the working
    // copy — and the binding never changes, so the next commit records the rollback.
    const pulledEvent = nextEvent(el, 'pulled');
    rows[2].querySelector('.rollback').click();
    const ask = el.shadowRoot.querySelector('arazzo-input-dialog');
    (await waitFor(() => {
      const b = ask.shadowRoot.querySelector('.confirm.danger');
      return b && ask.shadowRoot.querySelector('dialog').open ? b : null;
    }, 'the rollback confirmation')).click();
    const pulled = await pulledEvent;
    equal(pulled.detail.workingCopy.document.workflows[0].steps.length, 2, 'the oldest commit state replaced the working copy');
    equal(pulled.detail.workingCopy.gitBinding.branch, 'main', 'a rollback never rebinds');
  });
});