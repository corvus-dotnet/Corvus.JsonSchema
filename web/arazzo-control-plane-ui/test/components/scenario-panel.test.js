// Tier 3 — <arazzo-scenario-panel> against the in-memory mock: list, run with judged verdicts,
// trace handoff, delete, guarded-JSON edit.
import { ArazzoControlPlaneClient } from '../../src/arazzo-client.js';
import { createMockControlPlane } from '../../demo/mock-api.js';
import '../../src/components/scenario-panel.js';
import { ok, equal, nextEvent, waitFor, mount } from './helpers.js';

async function panelWithScenario() {
  const mock = createMockControlPlane({ latencyMs: 0 });
  const client = new ArazzoControlPlaneClient({ baseUrl: 'https://mock/arazzo/v1', fetch: mock.fetch });
  const wc = await client.createWorkingCopy({
    name: 'wc',
    document: {
      arazzo: '1.1.0', info: { title: 't', version: '1' },
      sourceDescriptions: [{ name: 'petstore', url: './p.json', type: 'openapi' }],
      workflows: [{ workflowId: 'wf', steps: [{ stepId: 'a', operationId: 'listPets', successCriteria: [{ condition: '$statusCode == 200' }] }] }],
    },
  });
  await client.attachWorkingCopySource(wc.id, 'petstore', { sourceName: 'petstore' });
  await client.putScenario(wc.id, {
    name: 'ok', mocks: [{ source: 'petstore', operationId: 'listPets', responses: [{ status: 200 }] }],
    expect: { outcome: 'completed', steps: { a: { attempts: 1 } } },
  });

  const el = document.createElement('arazzo-scenario-panel');
  el.client = client;
  mount(el);
  el.workingCopyId = wc.id;
  await nextEvent(el, 'scenarios-changed');
  return { el, client, wc };
}

describe('<arazzo-scenario-panel>', () => {
  let el;
  afterEach(() => el?.remove());

  it('lists scenarios and runs one with judged verdicts and a trace handoff', async () => {
    ({ el } = await panelWithScenario());
    ok(el.shadowRoot.textContent.includes('1 scenario'));

    el.shadowRoot.querySelector('[data-run="ok"]').click();
    await waitFor(() => el.shadowRoot.textContent.includes('✓ passed'), 'the run verdict renders');
    equal(el.shadowRoot.querySelectorAll('.verdicts button').length, 2, 'outcome + step expectation');

    const handoff = nextEvent(el, 'run-trace');
    el.shadowRoot.querySelector('[data-trace="ok"]').click();
    const e = await handoff;
    equal(e.detail.scenario, 'ok');
    ok(e.detail.trace.steps.length === 1, 'the full trace rides the handoff');
  });

  it('deletes a scenario and reports the new count', async () => {
    ({ el } = await panelWithScenario());
    const changed = nextEvent(el, 'scenarios-changed');
    el.shadowRoot.querySelector('[data-del="ok"]').click();
    equal((await changed).detail.count, 0);
    ok(el.shadowRoot.textContent.includes('No scenarios yet'));
  });

  it('edits a scenario in the typed form (mock statuses constrained to the declared responses)', async () => {
    const ctx = await panelWithScenario();
    el = ctx.el;
    el.shadowRoot.querySelector('[data-edit="ok"]').click();
    const editor = await waitFor(() => el.shadowRoot.querySelector('arazzo-scenario-editor'), 'the typed editor opens');
    await waitFor(() => editor.shadowRoot.querySelector('.f-desc'), 'the form renders');

    // The mock row addresses the document's own operation; the status select offers only the
    // DECLARED responses (the simulator faults on undeclared statuses).
    const op = editor.shadowRoot.querySelector('.m-op');
    ok(op.value.includes('listPets'), 'the operation picker holds the mock target');
    const statuses = [...editor.shadowRoot.querySelectorAll('.r-status option')].map((o) => o.value);
    ok(statuses.length > 0 && statuses.every((code) => /^\d+$/.test(code)), 'statuses come from the declared responses');

    editor.shadowRoot.querySelector('.f-desc').value = 'edited!';
    const changed = nextEvent(el, 'scenarios-changed');
    editor.shadowRoot.querySelector('.save').click();
    await changed;
    ok(el.shadowRoot.textContent.includes('edited!'), 'the description renders after the round-trip');
  });

  it('the JSON view stays one toggle away and unknown fields survive a form round-trip', async () => {
    const ctx = await panelWithScenario();
    el = ctx.el;

    // Seed an unknown field through the API, then edit through the FORM: the overlay keeps it.
    await ctx.client.putScenario(ctx.wc.id, {
      name: 'ok', 'x-note': 'keep me',
      mocks: [{ source: 'petstore', operationId: 'listPets', responses: [{ status: 200 }] }],
      expect: { outcome: 'completed' },
    });
    const reloaded = nextEvent(el, 'scenarios-changed');
    el.refresh();
    await reloaded;

    el.shadowRoot.querySelector('[data-edit="ok"]').click();
    const editor = await waitFor(() => el.shadowRoot.querySelector('arazzo-scenario-editor'));
    await waitFor(() => editor.shadowRoot.querySelector('.f-desc'));

    // Toggle to JSON and back: the working object is the same one the form edits.
    editor.shadowRoot.querySelector('.toggle').click();
    const area = await waitFor(() => editor.shadowRoot.querySelector('textarea.json'));
    ok(area.value.includes('x-note'), 'the JSON view shows the unknown field');
    editor.shadowRoot.querySelector('.toggle').click();
    await waitFor(() => editor.shadowRoot.querySelector('.f-desc'));

    const saved = nextEvent(el, 'scenarios-changed');
    editor.shadowRoot.querySelector('.save').click();
    await saved;
    const { scenarios } = await ctx.client.listScenarios(ctx.wc.id);
    equal(scenarios[0]['x-note'], 'keep me', 'the unknown field survived the typed-form round-trip');
    equal(scenarios[0].expect.outcome, 'completed', 'the known fields survived too');
  });
});