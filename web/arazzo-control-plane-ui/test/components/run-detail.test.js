// Tier 3 — <arazzo-run-detail>: correlation id visibility (#101) and its copy button (#99).
import { ArazzoControlPlaneClient } from '../../src/arazzo-client.js';
import { createMockControlPlane } from '../../demo/mock-api.js';
import '../../src/components/run-detail.js';
import { ok, equal, waitFor, mount } from './helpers.js';

function detailWithMock(runId, attrs = {}) {
  const mock = createMockControlPlane({ latencyMs: 0 });
  const el = document.createElement('arazzo-run-detail');
  el.setAttribute('runid', runId);
  for (const [k, v] of Object.entries(attrs)) el.setAttribute(k, v);
  el.client = new ArazzoControlPlaneClient({ baseUrl: 'https://mock/arazzo/v1', fetch: mock.fetch });
  return el;
}

describe('<arazzo-run-detail> correlation id', () => {
  let el;
  afterEach(() => el?.remove());

  // #101 — every run carries a telemetry correlation id, not just running ones. A Completed run shows it.
  it('shows the correlation id for a non-running (Completed) run', async () => {
    el = detailWithMock('run-0a5512cd');
    mount(el);
    const corr = await waitFor(() => el.shadowRoot.querySelector('[part="correlation"]'));
    ok(corr.textContent.includes('0a5512cd'), 'the correlation id is rendered');
  });

  // #101 — a Suspended message-wait run also has a top-level telemetry correlation id.
  it('shows the correlation id for a Suspended run', async () => {
    el = detailWithMock('run-9c0142ab');
    mount(el);
    const corr = await waitFor(() => el.shadowRoot.querySelector('[part="correlation"]'));
    ok(corr.textContent.includes('9c0142ab'), 'the correlation id is rendered');
  });

  // §5.5 — a run pinned to a deployment environment shows it.
  it('shows the pinned environment for a run', async () => {
    el = detailWithMock('run-7f3a9c21');
    mount(el);
    const env = await waitFor(() => el.shadowRoot.querySelector('[part="environment"]'));
    ok(env.textContent.includes('production'), 'the pinned environment is rendered');
  });

  // #99 — a copy button sits next to the correlation id and confirms on click.
  it('has a copy button for the correlation id that confirms on click', async () => {
    el = detailWithMock('run-0a5512cd');
    mount(el);
    const copy = await waitFor(() => el.shadowRoot.querySelector('[part="copy-correlation"]'));
    ok(copy, 'copy button present');
    copy.click();
    // copyToClipboard resolves (in test contexts navigator.clipboard may be absent → text stays); either
    // way the click must not throw. When the clipboard is available the glyph flips to a check.
    await new Promise((r) => setTimeout(r, 10));
    ok(['⧉', '✓'].includes(copy.textContent), 'glyph is the copy or confirmed state');
  });
});

describe('<arazzo-run-detail> sensitive-output redaction (#859)', () => {
  let el;
  afterEach(() => el?.remove());

  function detailForPersona(runId, persona) {
    const mock = createMockControlPlane({ latencyMs: 0, persona });
    const e = document.createElement('arazzo-run-detail');
    e.setAttribute('runid', runId);
    e.client = new ArazzoControlPlaneClient({ baseUrl: 'https://mock/arazzo/v1', fetch: mock.fetch });
    return e;
  }

  // run-33aa71f9 runs the KYC onboard-customer version (outputsSensitivity: sensitive). The auditor holds
  // runs:outputs:read but no write access, so the journal comes back redacted — the UI shows a held-back marker.
  it('shows a held-back marker for a sensitive versions outputs below the stronger grant, never the payload', async () => {
    el = detailForPersona('run-6610ffac', 'viewer');
    mount(el);
    const held = await waitFor(() => el.shadowRoot.querySelector('.pos.out.held'));
    ok(held.textContent.toLowerCase().includes('withheld'), 'the held-back marker is labelled');
    ok(!el.shadowRoot.textContent.includes('881-22-9034'), 'the sensitive identity data is not disclosed');
    ok(!el.shadowRoot.querySelector('.step-out'), 'no expandable outputs are rendered for a redacted journal');
  });

  // The operator (runs:write, the stronger-grant proxy) reads the same journal in full — no held-back marker.
  it('shows the outputs in full for the operator', async () => {
    el = detailForPersona('run-6610ffac', 'administrator');
    mount(el);
    await waitFor(() => el.shadowRoot.querySelector('.step-out'));
    ok(!el.shadowRoot.querySelector('.pos.out.held'), 'no held-back marker for a full read');
    ok(el.shadowRoot.textContent.includes('881-22-9034'), 'the identity data is disclosed to the operator');
  });
});

describe('<arazzo-run-detail> polling vs open modals', () => {
  let el;
  afterEach(() => el?.remove());

  // Every load() rebuilds the body and re-prepends the persistent cancel-button, whose reconnect
  // re-renders its shadow DOM — destroying an OPEN confirm dialog under the user's pointer. The
  // poll tick must skip while any nested modal is open and resume once it closes.
  it('a poll tick never repaints under an open confirm dialog', async () => {
    el = detailWithMock('run-9c0142ab', { scopes: 'runs:read runs:write', poll: '30' });
    mount(el);
    const cancel = await waitFor(() => {
      const b = el.shadowRoot.querySelector('arazzo-cancel-button');
      return b && !b.hidden ? b : null;
    });
    cancel.shadowRoot.querySelector('.trigger').click(); // opens the confirm modal
    const dlg = cancel.shadowRoot.querySelector('dialog');
    ok(dlg.open, 'the confirm dialog is open');

    await new Promise((r) => setTimeout(r, 150)); // several poll ticks land meanwhile
    ok(dlg.isConnected, 'the dialog survived the poll window');
    ok(dlg.open, 'the dialog is still open');

    dlg.close();
    await new Promise((r) => setTimeout(r, 100));
    ok(!el.hasOpenModal(), 'polling resumes once the modal closes');
  });
});

describe('<arazzo-run-detail> progress projection', () => {
  let el;
  afterEach(() => el?.remove());

  // The operator's "what has this run done": the catalogued step list with the run's POSITION
  // marked. Earlier entries are "dispatched", never "completed" (goto/retries can revisit), and
  // the raw cursor/ETag internals no longer leak into the pane.
  it('a running run shows the step list with its position marked, and no internals', async () => {
    el = detailWithMock('run-33aa71f9'); // Running, onboard-customer-v1, position 3 of 4
    mount(el);
    const prog = await waitFor(() => {
      const p = el.shadowRoot.querySelector('.progress');
      return p && !p.hidden ? p : null;
    });
    ok(/Position 3 of 4/.test(prog.textContent), 'a position line, not a raw cursor');
    equal(prog.querySelectorAll('.prog-steps li').length, 4, 'the catalogued step list renders');
    ok(prog.querySelector('.pos'), 'the next step is marked');
    equal(prog.querySelectorAll('.prog-steps li.dispatched').length, 3, 'earlier steps read as dispatched');
    const dl = el.shadowRoot.querySelector('dl');
    ok(!dl.textContent.includes('ETag'), 'the concurrency token stays internal');
    ok(!dl.textContent.includes('Cursor'), 'the raw cursor row is gone');
  });

  it('a suspended run whose steps all dispatched says so instead of inventing a next step', async () => {
    el = detailWithMock('run-1b88de40'); // Suspended, adopt-pet-v1, cursor past the last step
    mount(el);
    const prog = await waitFor(() => {
      const p = el.shadowRoot.querySelector('.progress');
      return p && !p.hidden ? p : null;
    });
    ok(prog.textContent.includes('All 4 steps dispatched'), 'the at-end suspension is stated plainly');
    ok(prog.textContent.includes('waiting'), 'and points at the wait record');
  });

  it('a faulted run marks the faulted step in the list', async () => {
    el = detailWithMock('run-dd44ee55'); // Faulted at provisionResources
    mount(el);
    const prog = await waitFor(() => {
      const p = el.shadowRoot.querySelector('.progress');
      return p && !p.hidden ? p : null;
    });
    const fault = prog.querySelector('.pos.fault');
    ok(fault, 'the faulted marker renders');
    ok(fault.closest('li').textContent.includes('provisionResources'), 'on the step the fault record names');
  });
});

describe('<arazzo-run-detail> recorded step outputs', () => {
  let el;
  afterEach(() => el?.remove());

  // The journal endpoint's UI: a step with recorded outputs expands to show them verbatim; steps
  // that recorded nothing stay plain rows (nothing is invented).
  it('expands recorded outputs on the steps that have them', async () => {
    el = detailWithMock('run-0a5512cd'); // Completed adopt-pet run with a 4-step journal
    mount(el);
    const prog = await waitFor(() => {
      const p = el.shadowRoot.querySelector('.progress');
      return p && !p.hidden && p.querySelector('.step-out') ? p : null;
    });
    equal(prog.querySelectorAll('.step-out').length, 4, 'all four recorded steps are expandable');
    const first = prog.querySelector('.step-out');
    first.open = true;
    ok(first.querySelector('pre').textContent.includes('pet-77'), 'the recorded outputs render verbatim');
  });

  it('a faulted run mixes recorded and unrecorded steps faithfully', async () => {
    el = detailWithMock('run-dd44ee55'); // journal holds the two steps BEFORE the fault
    mount(el);
    const prog = await waitFor(() => {
      const p = el.shadowRoot.querySelector('.progress');
      return p && !p.hidden && p.querySelector('.step-out') ? p : null;
    });
    equal(prog.querySelectorAll('.step-out').length, 2, 'only the steps that recorded outputs expand');
    const faulted = [...prog.querySelectorAll('.prog-steps li')].find((li) => li.textContent.includes('provisionResources'));
    ok(faulted.querySelector('.pos.fault'), 'the faulted step keeps its marker');
    ok(!faulted.querySelector('.step-out'), 'and does not pretend to have recorded outputs');
  });
});

describe('<arazzo-run-detail> enriched step journal (#885)', () => {
  let el;
  afterEach(() => el?.remove());

  function detailForPersona(runId, persona) {
    const mock = createMockControlPlane({ latencyMs: 0, persona });
    const e = document.createElement('arazzo-run-detail');
    e.setAttribute('runid', runId);
    e.client = new ArazzoControlPlaneClient({ baseUrl: 'https://mock/arazzo/v1', fetch: mock.fetch });
    return e;
  }

  // The journal now attests, per executed step, its status (✓/✗/⏭ in the debug tray's grammar), the attempt it
  // settled on, and its duration — not just the outputs it recorded.
  it('shows per-step status, duration, and the retry count on a completed run', async () => {
    el = detailWithMock('run-0a5512cd'); // four succeeded steps; reservePayment settled on attempt 2
    mount(el);
    const prog = await waitFor(() => {
      const p = el.shadowRoot.querySelector('.progress');
      return p && !p.hidden && p.querySelector('.jst') ? p : null;
    });
    equal(prog.querySelectorAll('.jst.ok').length, 4, 'every succeeded step carries the status glyph');
    const durations = [...prog.querySelectorAll('.jmeta')].map((m) => m.textContent);
    ok(durations.some((t) => /^\d+ms$/.test(t)), 'a sub-second duration renders in ms');
    ok(durations.some((t) => /^\d+\.\d+s$/.test(t)), 'a longer duration renders in seconds');
    const reserve = [...prog.querySelectorAll('.prog-steps li')].find((li) => li.textContent.includes('reservePayment'));
    ok(reserve.textContent.includes('↻2'), 'a retried step shows the attempt it settled on');
  });

  it('records the faulting step in the journal with a faulted status glyph', async () => {
    el = detailWithMock('run-dd44ee55'); // faulted at provisionResources on attempt 2
    mount(el);
    const prog = await waitFor(() => {
      const p = el.shadowRoot.querySelector('.progress');
      return p && !p.hidden && p.querySelector('.jst') ? p : null;
    });
    const prov = [...prog.querySelectorAll('.prog-steps li')].find((li) => li.textContent.includes('provisionResources'));
    ok(prov.querySelector('.jst.bad'), 'the faulted step is journaled with the ✗ status glyph');
    ok(!prov.querySelector('.step-out'), 'and exposes no outputs for a step that produced none');
  });

  // §14: redaction withholds the payload, not the fact the step ran — status and timing stay visible.
  it('keeps status and timing visible under output redaction, but never the payload', async () => {
    el = detailForPersona('run-6610ffac', 'viewer'); // sensitive KYC journal read by an auditor
    mount(el);
    const prog = await waitFor(() => {
      const p = el.shadowRoot.querySelector('.progress');
      return p && !p.hidden && p.querySelector('.pos.out.held') ? p : null;
    });
    ok(prog.querySelector('.jst.ok'), 'the recorded status stays visible when outputs are withheld');
    ok(prog.querySelector('.jmeta'), 'and the timing stays visible');
    ok(!el.shadowRoot.textContent.includes('881-22-9034'), 'while the sensitive payload is not disclosed');
  });

  it('shows a step whose last attempt failed and is being retried', async () => {
    // ADR 0068: the journal holds one entry per attempt and a row shows its step's latest, so a run parked on a
    // retry timer ends on a Retrying entry.
    const mock = createMockControlPlane({ latencyMs: 0 });
    const fetch = async (url, opts) => {
      const res = await mock.fetch(url, opts);
      if (/\/runs\/run-0a5512cd\/steps/.test(String(url))) {
        const body = await res.json();
        const steps = body.steps.filter((s) => !(s.stepId === 'reservePayment' && s.status === 'Succeeded'));
        return new Response(JSON.stringify({ ...body, steps }), { status: 200, headers: { 'content-type': 'application/json' } });
      }
      return res;
    };
    el = document.createElement('arazzo-run-detail');
    el.setAttribute('runid', 'run-0a5512cd');
    el.client = new ArazzoControlPlaneClient({ baseUrl: 'https://mock/arazzo/v1', fetch });
    mount(el);
    const glyph = await waitFor(() => el.shadowRoot.querySelector('.jst.retry'));
    ok(glyph.getAttribute('title').includes('retried'), 'the retrying glyph explains itself');
    ok(el.shadowRoot.querySelector('.progress').textContent.includes('attempt 1'), 'and names the attempt that failed');
  });

  it('flags a capped journal when the server marks it truncated', async () => {
    const mock = createMockControlPlane({ latencyMs: 0 });
    const fetch = async (url, opts) => {
      const res = await mock.fetch(url, opts);
      if (/\/runs\/run-0a5512cd\/steps/.test(String(url))) {
        const body = await res.json();
        return new Response(JSON.stringify({ ...body, truncated: true }), { status: 200, headers: { 'content-type': 'application/json' } });
      }
      return res;
    };
    el = document.createElement('arazzo-run-detail');
    el.setAttribute('runid', 'run-0a5512cd');
    el.client = new ArazzoControlPlaneClient({ baseUrl: 'https://mock/arazzo/v1', fetch });
    mount(el);
    const note = await waitFor(() => el.shadowRoot.querySelector('.prog-note'));
    ok(note.textContent.toLowerCase().includes('capped'), 'the capped-journal note renders');
  });
});

// ADR 0068 and ADR 0072: what a run is held to, why it faulted in words, and the two ways forward.
describe('<arazzo-run-detail> budget, fault types and recovery', () => {
  let el;
  afterEach(() => el?.remove());
  const part = (name) => el.shadowRoot.querySelector(`[part="${name}"]`);
  const button = (cls) => el.shadowRoot.querySelector(`.action-buttons .${cls}`);

  it('shows the budget frozen into the run', async () => {
    el = detailWithMock('run-b0d9e701', { scopes: 'runs:read runs:write' });
    mount(el);
    const budget = await waitFor(() => part('budget'));
    equal(budget.querySelector('[data-limit="maxSteps"]').textContent, '3');
    equal(budget.querySelector('[data-limit="stepTimeoutSeconds"]').textContent, '30s');
    equal(budget.querySelectorAll('dd').length, 6, 'all six limits');
  });

  it('explains a platform fault type under the recorded error, and offers a resume the server says would run', async () => {
    el = detailWithMock('run-b0d9e701', { scopes: 'runs:read runs:write' });
    mount(el);
    const help = await waitFor(() => part('fault-help'));
    ok(part('fault').querySelector('.err').textContent.includes('budget-fuel'), 'the recorded error is still shown');
    ok(help.textContent.includes('max steps'), 'what happened');
    ok(help.textContent.includes('resume the run, or re-run it'), 'what to do');
    ok(part('rebudget').textContent.includes('Resumable now'), 'the servers answer');
    equal(part('fault').querySelector('.limits.offered [data-limit="maxSteps"]').textContent, '120', 'the budget a resume would give');
    ok(!button('resume').disabled, 'Resume is offered');
    ok(!part('resume-blocked'), 'nothing to explain');
  });

  it('disables Resume with the reason on a run a re-budget cannot rescue, and leaves Re-run as the way forward', async () => {
    el = detailWithMock('run-b0d9e702', { scopes: 'runs:read runs:write' });
    mount(el);
    await waitFor(() => part('rebudget'));
    ok(part('rebudget').textContent.includes('Not resumable yet'));
    ok(button('resume').disabled, 'Resume is disabled');
    ok(button('resume').title.includes('re-run'), 'and says why');
    ok(part('resume-blocked').textContent.includes('outside the budget'), 'the reason is on the page, not only in a tooltip');
    ok(!button('rerun').disabled, 'Re-run is offered');
  });

  it('shows a steps own failure as recorded, with no explanation and an ordinary Resume', async () => {
    el = detailWithMock('run-7f3a9c21', { scopes: 'runs:read runs:write' });
    mount(el);
    await waitFor(() => part('fault'));
    ok(!part('fault-help'), 'nothing is invented about an error the platform did not record');
    ok(!button('resume').disabled);
  });

  it('re-runs behind the kits confirm, with an idempotency key, and asks to open the new run', async () => {
    el = detailWithMock('run-b0d9e702', { scopes: 'runs:read runs:write' });
    let key;
    const rerun = el.client.rerunRun.bind(el.client);
    el.client.rerunRun = (id, opts) => { key = opts?.idempotencyKey; return rerun(id, opts); };
    mount(el);
    await waitFor(() => button('rerun'));
    button('rerun').click();
    const dialog = await waitFor(() => el.shadowRoot.querySelector('dialog.arazzo-confirm'));
    ok(dialog.textContent.includes('onboard-customer-v1'), 'names what will run');
    ok(dialog.textContent.includes('repeats'), 'and that it repeats the workflows effects');
    const rerunEvent = nextEventOf(el, 'run-rerun');
    const open = nextEventOf(el, 'run-open');
    dialog.querySelector('.ok').click();
    const started = (await rerunEvent).detail;
    equal(started.rerunOf, 'run-b0d9e702');
    equal((await open).detail.runId, started.runId, 'the new run is opened');
    ok(key && key.startsWith('rerun-run-b0d9e702-'), 'one key per confirmed intent');

    // Shown, the new run names the run it re-runs, and that is a way back to it.
    el.showRun(started.runId);
    const link = await waitFor(() => el.shadowRoot.querySelector('[part="rerun-of"] .rerun-of'));
    equal(link.textContent, 'run-b0d9e702');
    const back = nextEventOf(el, 'run-open');
    link.click();
    equal((await back).detail.runId, 'run-b0d9e702');
  });

  it('a cancelled re-run starts nothing', async () => {
    el = detailWithMock('run-b0d9e702', { scopes: 'runs:read runs:write' });
    let calls = 0;
    el.client.rerunRun = () => { calls++; return Promise.resolve({ runId: 'x' }); };
    mount(el);
    await waitFor(() => button('rerun'));
    button('rerun').click();
    const dialog = await waitFor(() => el.shadowRoot.querySelector('dialog.arazzo-confirm'));
    dialog.querySelector('.cancel').click();
    await new Promise((r) => setTimeout(r, 50));
    equal(calls, 0);
  });

  it('does not offer Re-run while the run is still going, or without runs:write', async () => {
    el = detailWithMock('run-9c0142ab', { scopes: 'runs:read runs:write' });
    mount(el);
    await waitFor(() => part('wait'));
    ok(!button('rerun'), 'a suspended run is still going');
    el.remove();

    el = detailWithMock('run-b0d9e702', { scopes: 'runs:read' });
    mount(el);
    await waitFor(() => part('fault'));
    ok(!button('rerun') && !button('resume'), 'read-only');
  });
});

function nextEventOf(el, type, timeout = 4000) {
  return new Promise((resolve, reject) => {
    const timer = setTimeout(() => reject(new Error(`timed out waiting for ${type}`)), timeout);
    el.addEventListener(type, (e) => { clearTimeout(timer); resolve(e); }, { once: true });
  });
}
