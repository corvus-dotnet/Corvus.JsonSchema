// Tier 3 — <arazzo-tag-editor> (the reusable key/value tag editor) in a real browser.
import '../../src/components/tag-editor.js';
import { ok, equal, waitFor, mount } from './helpers.js';

describe('<arazzo-tag-editor>', () => {
  let el;
  afterEach(() => el?.remove());

  it('seeds rows from .tags and reads them back', async () => {
    el = document.createElement('arazzo-tag-editor');
    mount(el);
    el.tags = [{ key: 'domain', value: 'payments' }, { key: 'team', value: 'ops' }];
    await waitFor(() => el.shadowRoot.querySelectorAll('.tag-row').length === 2);
    equal(el.tags.length, 2, 'reads both rows back');
    equal(el.tags[0].key, 'domain', 'first key');
    equal(el.tags[1].value, 'ops', 'second value');
  });

  it('adds a row via the Add button and includes it once filled', async () => {
    el = document.createElement('arazzo-tag-editor');
    mount(el);
    el.tags = [{ key: 'a', value: '1' }];
    await waitFor(() => el.shadowRoot.querySelector('.add'));
    el.shadowRoot.querySelector('.add').click();
    equal(el.shadowRoot.querySelectorAll('.tag-row').length, 2, 'Add appends a row');
    const rows = el.shadowRoot.querySelectorAll('.tag-row');
    rows[1].querySelector('.tk').value = 'b';
    rows[1].querySelector('.tv').value = '2';
    equal(el.tags.length, 2, 'the filled row is included');
    equal(el.tags[1].key, 'b', 'new row key');
  });

  it('removes a row via the ✕ button', async () => {
    el = document.createElement('arazzo-tag-editor');
    mount(el);
    el.tags = [{ key: 'a', value: '1' }, { key: 'b', value: '2' }];
    await waitFor(() => el.shadowRoot.querySelectorAll('.tag-row').length === 2);
    el.shadowRoot.querySelectorAll('.rm')[0].click();
    equal(el.shadowRoot.querySelectorAll('.tag-row').length, 1, '✕ drops a row');
    equal(el.tags[0].key, 'b', 'the correct row survived');
  });

  it('excludes wholly-empty rows from .tags', async () => {
    el = document.createElement('arazzo-tag-editor');
    mount(el);
    el.tags = [{ key: 'a', value: '1' }];
    await waitFor(() => el.shadowRoot.querySelector('.add'));
    el.shadowRoot.querySelector('.add').click(); // a blank row
    equal(el.tags.length, 1, 'the blank row is not returned');
    ok(true);
  });
});