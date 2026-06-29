// Tier 3 — <arazzo-grantee-picker> mounted in a real browser against the in-memory mock.
import { ArazzoControlPlaneClient } from '../../src/arazzo-client.js';
import { createMockControlPlane } from '../../demo/mock-api.js';
import '../../src/components/grantee-picker.js';
import { ok, equal, nextEvent, waitFor, mount } from './helpers.js';

function pickerWithMock(attrs = {}) {
  const mock = createMockControlPlane({ latencyMs: 0 });
  const el = document.createElement('arazzo-grantee-picker');
  for (const [k, v] of Object.entries(attrs)) el.setAttribute(k, v);
  el.client = new ArazzoControlPlaneClient({ baseUrl: 'https://mock/arazzo/v1', fetch: mock.fetch });
  return el;
}

const type = (el, text) => {
  const input = el.shadowRoot.querySelector('.q');
  input.value = text;
  input.dispatchEvent(new Event('input'));
};
const results = (el) => el.shadowRoot.querySelectorAll('.results li[data-index]');

describe('<arazzo-grantee-picker>', () => {
  let el;
  afterEach(() => el?.remove());

  it('resolves grantees as you type and shows kind, identity, source', async () => {
    el = pickerWithMock();
    mount(el);
    type(el, 'ada');
    await waitFor(() => results(el).length === 1);
    const row = results(el)[0];
    ok(row.textContent.includes('Ada Lovelace'), 'shows the label');
    ok(row.textContent.includes('person'), 'shows the kind');
    ok(row.textContent.includes('sys:sub=u-1042'), 'shows the resolved identity');
    ok(row.textContent.includes('directory'), 'shows the source');
  });

  it('selecting a result emits grantee-selected and exposes the resolved grantee as .grant', async () => {
    el = pickerWithMock();
    mount(el);
    type(el, 'payments');
    await waitFor(() => results(el).length === 1);
    const selected = nextEvent(el, 'grantee-selected');
    results(el)[0].click();
    const e = await selected;
    equal(e.detail.grantee.kind, 'team');
    equal(e.detail.grantee.value, 'payments');
    equal(el.grant.value, 'payments', '.grant is the resolved grantee');
    ok(Array.isArray(el.grant.identity) && el.grant.identity[0].dimension === 'team', '.grant carries the exact identity');
    ok(el.shadowRoot.querySelector('.selected .chip'), 'shows the selected chip');
  });

  it('flags a partial (not complete) identity as a hazard once selected', async () => {
    el = pickerWithMock();
    mount(el);
    type(el, 'grace');
    await waitFor(() => results(el).length === 1);
    const selected = nextEvent(el, 'grantee-selected');
    results(el)[0].click();
    await selected;
    equal(el.grant.complete, false);
    ok(el.shadowRoot.querySelector('.warn'), 'shows the partial-identity warning');
  });

  it('narrows the search by kind', async () => {
    el = pickerWithMock();
    mount(el);
    const kind = el.shadowRoot.querySelector('.kind');
    kind.value = 'person';
    kind.dispatchEvent(new Event('change'));
    await waitFor(() => results(el).length === 2);
    ok([...results(el)].every((r) => r.textContent.includes('person')), 'only person grantees');
  });

  it('locks to a single kind via the kind attribute (no kind selector)', async () => {
    el = pickerWithMock({ kind: 'team' });
    mount(el);
    ok(!el.shadowRoot.querySelector('.kind'), 'the kind selector is hidden when locked');
    type(el, '');
    await waitFor(() => results(el).length >= 1);
    ok([...results(el)].every((r) => r.textContent.includes('team')), 'only the locked kind');
  });

  it('restricts the offered kinds via the kinds allow-list and excludes others from "Any kind" results', async () => {
    // An administrator picker allows person/team/role but never `workflow` (a workflow is a valid credential-usage
    // grantee, not a workflow administrator).
    el = pickerWithMock({ kinds: 'person team role' });
    mount(el);
    const opts = [...el.shadowRoot.querySelectorAll('.kind option')].map((o) => o.value);
    ok(!opts.includes('workflow'), 'workflow is not offered as a selectable kind');
    ok(['person', 'team', 'role'].every((k) => opts.includes(k)), 'the allowed kinds are offered');
    // 'onboard' resolves only to the seed's workflow grantee → filtered out under "Any kind", so no actionable rows.
    type(el, 'onboard');
    await waitFor(() => el.shadowRoot.querySelector('.results:not([hidden]) li'));
    equal(results(el).length, 0, 'the workflow grantee is filtered out of an admin picker');
  });

  it('reset() clears the selection', async () => {
    el = pickerWithMock();
    mount(el);
    type(el, 'sre');
    await waitFor(() => results(el).length === 1);
    const selected = nextEvent(el, 'grantee-selected');
    results(el)[0].click();
    await selected;
    ok(el.grant, 'has a selection');
    el.reset();
    equal(el.grant, null, 'cleared');
    ok(!el.shadowRoot.querySelector('.selected .chip'), 'chip gone');
  });
});
