// Tier 3 — <arazzo-access-overview> against the in-memory mock: pick a grantee, see their reach grants, administered
// workflows, and credential usage aggregated by GET /access/grants; revoke is scope-gated.
import { ArazzoControlPlaneClient } from '../../src/arazzo-client.js';
import { createMockControlPlane } from '../../demo/mock-api.js';
import '../../src/components/access-overview-panel.js';
import { ok, equal, waitFor, mount } from './helpers.js';

describe('<arazzo-access-overview>', () => {
  let el;
  afterEach(() => el?.remove());

  function make(scopes = 'security:read security:write') {
    const mock = createMockControlPlane({ latencyMs: 0 });
    el = document.createElement('arazzo-access-overview');
    el.setAttribute('scopes', scopes);
    el.client = new ArazzoControlPlaneClient({ baseUrl: 'https://mock/arazzo/v1', fetch: mock.fetch });
    return el;
  }

  const ada = { kind: 'person', value: 'u-1042', label: 'Ada Lovelace', identity: [{ dimension: 'sys:sub', value: 'u-1042' }], source: 'directory', complete: true };
  const selectGrantee = (grantee) => el.shadowRoot.querySelector('arazzo-grantee-picker').dispatchEvent(new CustomEvent('grantee-selected', { detail: { grantee } }));

  it('aggregates a grantee’s reach grants, administered workflows, and credential usage', async () => {
    make();
    mount(el);
    selectGrantee(ada);
    await waitFor(() => el.shadowRoot.querySelector('.grant'));
    ok([...el.shadowRoot.querySelectorAll('.grant .claim')].some((c) => c.textContent.includes('sub = u-1042')), 'shows the sub=u-1042 reach grant');
    ok([...el.shadowRoot.querySelectorAll('[data-workflow]')].some((b) => b.dataset.workflow === 'nightly-reconcile'), 'shows administered nightly-reconcile');
    // Only the identity-scoped credential — shared credentials (usable by any run) are omitted (design §6.1).
    equal([...el.shadowRoot.querySelectorAll('[data-cred]')].map((b) => b.dataset.cred).join(','), 'billing@staging', 'shows only the identity-scoped credential');
    ok(el.shadowRoot.querySelector('[data-revoke]'), 'revoke shown with security:write');
    ok(el.shadowRoot.querySelector('.who .gchip'), 'grantee chip shown');
  });

  it('hides Revoke without security:write (scope honesty)', async () => {
    make('security:read');
    mount(el);
    selectGrantee(ada);
    await waitFor(() => el.shadowRoot.querySelector('.grant'));
    equal(el.shadowRoot.querySelector('[data-revoke]'), null, 'no revoke without security:write');
  });
});
