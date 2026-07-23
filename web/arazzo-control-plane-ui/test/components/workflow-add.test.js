// Tier 3 — <arazzo-workflow-add>: the shared "new workflowId + Add workflow" widget. Emits
// workflow-add {workflowId} on a non-empty submit (button or Enter); the host does dedup/creation and
// may reject an id via setError(). clear() empties the field.
import '../../src/components/workflow-add.js';
import { ok, equal, mount } from './helpers.js';

describe('<arazzo-workflow-add>', () => {
  let el;
  afterEach(() => el?.remove());

  function make() {
    el = document.createElement('arazzo-workflow-add');
    mount(el);
    return el;
  }

  const input = () => el.shadowRoot.querySelector('.newwf');
  const button = () => el.shadowRoot.querySelector('.addwf');

  it('emits the trimmed id on submit; a blank submit is a no-op', () => {
    make();
    let detail;
    el.addEventListener('workflow-add', (e) => { detail = e.detail; });
    input().value = '   ';
    button().click();
    equal(detail, undefined, 'a blank id never emits');
    input().value = '  place-order  ';
    button().click();
    equal(detail.workflowId, 'place-order', 'the id is trimmed');
  });

  it('Enter in the field submits', () => {
    make();
    let id;
    el.addEventListener('workflow-add', (e) => { id = e.detail.workflowId; });
    input().value = 'cancel-order';
    input().dispatchEvent(new KeyboardEvent('keydown', { key: 'Enter', bubbles: true }));
    equal(id, 'cancel-order');
  });

  it('clear() empties the field; setError() flags it and typing clears the flag', () => {
    make();
    input().value = 'x';
    el.clear();
    equal(input().value, '', 'clear empties the field');
    el.setError('a workflow with this id already exists');
    ok(input().classList.contains('invalid'), 'setError flags the field');
    input().value = 'y';
    input().dispatchEvent(new Event('input', { bubbles: true }));
    ok(!input().classList.contains('invalid'), 'typing clears the flag');
  });
});
