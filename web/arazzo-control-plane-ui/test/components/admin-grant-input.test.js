// Tier 3 — <arazzo-admin-grant-input> mounted in a real browser.
import '../../src/components/admin-grant-input.js';
import { ok, equal, mount } from './helpers.js';

const $ = (el, sel) => el.shadowRoot.querySelector(sel);
const $$ = (el, sel) => [...el.shadowRoot.querySelectorAll(sel)];

function input(attrs = {}) {
  const el = document.createElement('arazzo-admin-grant-input');
  for (const [k, v] of Object.entries(attrs)) el.setAttribute(k, v);
  return el;
}

describe('<arazzo-admin-grant-input>', () => {
  let el;
  afterEach(() => el?.remove());

  it('offers only the well-known dimensions and defaults to the workflow autocomplete', () => {
    el = input();
    mount(el);
    equal($$(el, '.dim option').map((o) => o.value).join(','), 'workflow,tenant', 'workflow + tenant only');
    equal($(el, '.dim').value, 'workflow', 'defaults to workflow');
    ok($(el, 'arazzo-workflow-id-input'), 'workflow value uses the catalog autocomplete');
    ok(!$(el, '.val-text'), 'no plain text field for the workflow dimension');
  });

  it('swaps to a plain text value for tenant and reports the grant', () => {
    el = input();
    mount(el);
    $(el, '.dim').value = 'tenant';
    $(el, '.dim').dispatchEvent(new Event('change'));
    ok($(el, '.val-text'), 'tenant uses a plain text value');
    ok(!$(el, 'arazzo-workflow-id-input'), 'no autocomplete for tenant');
    equal(el.grant, null, 'no grant until a value is entered');
    $(el, '.val-text').value = 'platform';
    equal(el.grant.dimension, 'tenant', 'dimension reported');
    equal(el.grant.value, 'platform', 'value reported');
  });

  it('reports the workflow id typed into the autocomplete', () => {
    el = input();
    mount(el);
    $(el, 'arazzo-workflow-id-input').value = 'nightly-reconcile';
    equal(el.grant.dimension, 'workflow', 'workflow dimension');
    equal(el.grant.value, 'nightly-reconcile', 'autocomplete value flows into the grant');
  });

  it('locks the workflow value read-only when fixed-workflow is set', () => {
    el = input({ 'fixed-workflow': 'onboard-customer' });
    mount(el);
    const fixed = $(el, '.val-fixed');
    ok(fixed, 'a fixed, read-only value field is shown');
    ok(fixed.readOnly, 'the workflow value cannot be edited');
    equal(fixed.value, 'onboard-customer', 'pinned to the workflow being added');
    equal(el.grant.value, 'onboard-customer', 'the grant carries the fixed workflow id');
    // Switching to tenant still allows a free value even while a workflow is pinned.
    $(el, '.dim').value = 'tenant';
    $(el, '.dim').dispatchEvent(new Event('change'));
    ok($(el, '.val-text'), 'tenant value is free even with a pinned workflow');
  });
});