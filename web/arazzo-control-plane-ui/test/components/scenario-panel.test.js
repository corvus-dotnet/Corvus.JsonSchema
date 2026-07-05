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

  it('edits a scenario as guarded JSON', async () => {
    const ctx = await panelWithScenario();
    el = ctx.el;
    el.shadowRoot.querySelector('[data-edit="ok"]').click();
    await waitFor(() => el.shadowRoot.querySelector('textarea[data-json]'), 'the editor opens');
    const area = el.shadowRoot.querySelector('textarea[data-json]');
    const edited = JSON.parse(area.value);
    edited.description = 'edited!';
    area.value = JSON.stringify(edited);
    const changed = nextEvent(el, 'scenarios-changed');
    area.dispatchEvent(new Event('change'));
    await changed;
    ok(el.shadowRoot.textContent.includes('edited!'), 'the description renders after the round-trip');
  });
});