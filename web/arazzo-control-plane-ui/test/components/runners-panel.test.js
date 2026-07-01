// Tier 3 — <arazzo-runners> mounted in a real browser against the in-memory mock.
import { ArazzoControlPlaneClient } from '../../src/arazzo-client.js';
import { createMockControlPlane } from '../../demo/mock-api.js';
import '../../src/components/runners-panel.js';
import { ok, equal, nextEvent, mount } from './helpers.js';

function panelWithMock(attrs = { 'stale-after': '90' }) {
  const mock = createMockControlPlane({ latencyMs: 0 });
  const el = document.createElement('arazzo-runners');
  for (const [k, v] of Object.entries(attrs)) el.setAttribute(k, v);
  el.client = new ArazzoControlPlaneClient({ baseUrl: 'https://mock/arazzo/v1', fetch: mock.fetch });
  return el;
}

const $$ = (el, sel) => el.shadowRoot.querySelectorAll(sel);

describe('<arazzo-runners>', () => {
  let el;
  afterEach(() => el?.remove());

  it('lists registered runners with online/stale health derived from the last heartbeat', async () => {
    el = panelWithMock();
    mount(el);
    await nextEvent(el, 'loaded');
    equal($$(el, '.runner').length, 3, 'three seeded runners');
    ok($$(el, '.health.online').length >= 2, 'the fresh-heartbeat runners are Online');
    equal($$(el, '.health.stale').length, 1, 'the lapsed-heartbeat runner is Stale');
    ok(el.shadowRoot.querySelector('.count').textContent.includes('stale'), 'the header summarises the stale count');
  });

  it('shows each runner\'s hosted versions, including one still loading', async () => {
    el = panelWithMock();
    mount(el);
    await nextEvent(el, 'loaded');
    ok(el.shadowRoot.textContent.includes('adopt-pet'), 'lists a hosted workflow');
    ok($$(el, '.hv .lstate.loading').length >= 1, 'a still-loading version is flagged');
    ok($$(el, '.hv .lstate').length >= 3, 'loaded versions are shown too');
  });

  it('shows the deployment environment each runner serves (design §5.5)', async () => {
    el = panelWithMock();
    mount(el);
    await nextEvent(el, 'loaded');
    const envs = [...$$(el, '.renv')].map((n) => n.textContent);
    equal(envs.length, 3, 'every runner shows its environment');
    ok(envs.includes('production'), 'a production runner is shown');
    ok(envs.includes('staging'), 'a staging runner is shown');
  });

  it('treats every runner as stale under a tiny threshold', async () => {
    el = panelWithMock({ 'stale-after': '1' }); // 1s → even an 18s-old heartbeat is stale
    mount(el);
    await nextEvent(el, 'loaded');
    equal($$(el, '.health.online').length, 0, 'none online under a 1s threshold');
    equal($$(el, '.health.stale').length, 3, 'all stale');
  });

  it('pages the runner registry with Prev/Next over the keyset cursor', async () => {
    el = panelWithMock({ 'stale-after': '90', 'page-size': '2' }); // 3 seeded runners → 2 pages
    mount(el);
    await nextEvent(el, 'loaded');
    equal($$(el, '.runner').length, 2, 'page 1 holds two runners');
    const next = el.shadowRoot.querySelector('.next');
    ok(next && !next.disabled, 'Next is enabled when a page follows');
    ok(el.shadowRoot.querySelector('.prev').disabled, 'Prev is disabled on page 1');

    const page2 = nextEvent(el, 'loaded');
    next.click();
    await page2;
    equal($$(el, '.runner').length, 1, 'page 2 holds the remaining runner');
    ok(el.shadowRoot.querySelector('.next').disabled, 'Next disabled on the last page');
    ok(!el.shadowRoot.querySelector('.prev').disabled, 'Prev is enabled on page 2');

    const back = nextEvent(el, 'loaded');
    el.shadowRoot.querySelector('.prev').click();
    await back;
    equal($$(el, '.runner').length, 2, 'page 1 restored — rows replaced, not appended');
    ok(el.shadowRoot.querySelector('.prev').disabled, 'Prev disabled again back on page 1');
  });
});