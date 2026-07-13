// Tier 3 — <arazzo-document-inspector>: info, sourceDescriptions, workflows, and the components
// reusable library (the $components.… targets the other editors reference and localize from).
import '../../src/components/document-inspector.js';
import { ok, equal, nextEvent, mount } from './helpers.js';

const DOC = {
  arazzo: '1.1.0',
  info: { title: 'Orders', version: '1.0.0' },
  sourceDescriptions: [{ name: 'payments', url: './payments.json', type: 'openapi' }],
  workflows: [
    { workflowId: 'place-order', steps: [{ stepId: 'a' }, { stepId: 'b' }] },
    { workflowId: 'refund-order', steps: [] },
  ],
  components: {
    parameters: { page: { name: 'page', in: 'query', value: '1' } },
    failureActions: { 'give-up': { name: 'give-up', type: 'end' } },
  },
};

describe('<arazzo-document-inspector>', () => {
  let el;
  afterEach(() => el?.remove());

  function make(value = DOC) {
    el = document.createElement('arazzo-document-inspector');
    el.stepIds = ['a', 'b'];
    el.workflowIds = ['place-order', 'refund-order'];
    mount(el);
    el.value = value;
    return el;
  }

  it('renders info, sources, workflows, and the components library', () => {
    make();
    equal(el.shadowRoot.querySelector('.ititle').value, 'Orders');
    equal(el.shadowRoot.querySelector('.sources .sname').textContent, 'payments');
    ok(el.shadowRoot.textContent.includes('place-order'), 'workflows list');
    ok(el.shadowRoot.textContent.includes('$components.parameters.page'), 'component entries render their reference form');
    ok(el.shadowRoot.querySelector('arazzo-action-editor'), 'a component action embeds the action editor');
  });

  it('edits info fields; source descriptions are read-only (the Sources panel owns them)', async () => {
    make();
    const title = el.shadowRoot.querySelector('.ititle');
    title.value = 'Orders v2';
    const changed = nextEvent(el, 'document-changed');
    title.dispatchEvent(new Event('input'));
    equal((await changed).detail.document.info.title, 'Orders v2');

    ok(!el.shadowRoot.querySelector('.addsrc'), 'no add affordance — attach from the Sources panel instead');
    ok(!el.shadowRoot.querySelector('.sources input'), 'no editable source fields');
    ok(el.shadowRoot.querySelector('.sources .sname'), 'the declared names still display');
  });

  it('adds and removes workflows (empty skeleton, duplicate ids refused)', async () => {
    make();
    const input = el.shadowRoot.querySelector('.newwf');
    input.value = 'place-order'; // duplicate — refused silently
    el.shadowRoot.querySelector('.addwf').click();
    equal(el.value.workflows.length, 2);

    input.value = 'cancel-order';
    const changed = nextEvent(el, 'document-changed');
    el.shadowRoot.querySelector('.addwf').click();
    const doc = (await changed).detail.document;
    equal(doc.workflows.length, 3);
    equal(doc.workflows[2].workflowId, 'cancel-order');
    equal(doc.workflows[2].steps.length, 0);

    const removed = nextEvent(el, 'document-changed');
    el.shadowRoot.querySelectorAll('.workflows .wdel')[2].click();
    equal((await removed).detail.document.workflows.length, 2);
  });

  it('manages component entries: add per kind, edit through the embedded editor, delete prunes', async () => {
    make();
    // Add a new success action component.
    const groups = [...el.shadowRoot.querySelectorAll('.components > div')];
    const successGroup = groups.find((g) => g.querySelector('h4').textContent === 'successActions');
    successGroup.querySelector('.newkey').value = 'carry-on';
    let changed = nextEvent(el, 'document-changed');
    successGroup.querySelector('.addkey').click();
    let doc = (await changed).detail.document;
    equal(doc.components.successActions['carry-on'].type, 'end');

    // Edit the failure action through its embedded editor (scoped to its group — the add above
    // re-rendered, and successActions now embeds an editor earlier in DOM order).
    const failureGroup = [...el.shadowRoot.querySelectorAll('.components > div')].find((g) => g.querySelector('h4').textContent === 'failureActions');
    const editor = failureGroup.querySelector('arazzo-action-editor');
    const name = editor.shadowRoot.querySelector('.name');
    name.value = 'give-up-renamed';
    changed = nextEvent(el, 'document-changed');
    name.dispatchEvent(new Event('input'));
    doc = (await changed).detail.document;
    equal(doc.components.failureActions['give-up'].name, 'give-up-renamed');

    // Delete the only parameter component — the empty group prunes.
    changed = nextEvent(el, 'document-changed');
    const paramGroup = [...el.shadowRoot.querySelectorAll('.components > div')].find((g) => g.querySelector('h4').textContent === 'parameters');
    paramGroup.querySelector('.edel').click();
    doc = (await changed).detail.document;
    equal(doc.components.parameters, undefined);
  });

  it('a component input schema is authored via the typed schema editor', async () => {
    make({ ...DOC, components: { inputs: { pagination: { type: 'object' } } } });
    const ed = el.shadowRoot.querySelector('.entry arazzo-schema-editor');
    ok(ed, 'the schema editor renders for a components.inputs entry');
    equal(ed.value.type, 'object', 'seeded from the component schema');
    const changed = nextEvent(el, 'document-changed');
    ed.dispatchEvent(new CustomEvent('schema-changed', { detail: { schema: { type: 'string' } }, bubbles: true, composed: true }));
    equal((await changed).detail.document.components.inputs.pagination.type, 'string');
  });

  it('sections attribute scopes the form: document vs components', () => {
    el = document.createElement('arazzo-document-inspector');
    el.setAttribute('sections', 'components');
    el.value = { arazzo: '1.1.0', info: { title: 't', version: '1' }, workflows: [], components: { failureActions: { esc: { name: 'esc', type: 'end' } } } };
    mount(el);
    ok(!el.shadowRoot.querySelector('.ititle'), 'no info fields in components scope');
    ok(el.shadowRoot.querySelector('.components'), 'the library renders');
    ok(el.shadowRoot.textContent.includes('$components.failureActions.esc'), 'with its entries');

    el.remove();
    el = document.createElement('arazzo-document-inspector');
    el.setAttribute('sections', 'document');
    el.value = { arazzo: '1.1.0', info: { title: 't', version: '1' }, workflows: [] };
    mount(el);
    ok(el.shadowRoot.querySelector('.ititle'), 'info fields in document scope');
    ok(!el.shadowRoot.querySelector('.components'), 'no library on the settings page — it has its own tab');
  });
});
