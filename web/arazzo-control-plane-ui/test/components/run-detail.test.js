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
