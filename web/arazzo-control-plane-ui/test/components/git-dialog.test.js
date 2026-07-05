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

    // Bind: repo from the session, branch/path/scenarios typed.
    const sel = el.shadowRoot.querySelector('.b-repo');
    sel.value = 'acme-org/specs';
    sel.dispatchEvent(new Event('input'));
    el.shadowRoot.querySelector('.b-branch').value = 'feature/adopt';
    el.shadowRoot.querySelector('.b-path').value = 'flows/adopt.arazzo.json';
    el.shadowRoot.querySelector('.b-scenarios').value = 'scenarios/adopt';
    el.shadowRoot.querySelector('.b-branch').dispatchEvent(new Event('input'));
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
    const pulled = await pulledEvent;
    equal(pulled.detail.workingCopy.document.info.title, 'Adopt', 'the document round-tripped');
    ok(pulled.detail.workingCopy.etag !== bound.detail.workingCopy.etag, 'the pull bumped the etag');
  });
});