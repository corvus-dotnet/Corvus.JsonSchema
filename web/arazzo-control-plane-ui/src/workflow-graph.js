// Workflow graph projection — the DOM-free heart of the workflow designer's design surface.
//
// A pure function from an Arazzo document + workflowId to the node/edge/defaults shape the
// <arazzo-design-surface> renders (design: workflow-designer-design.md §6.2/§6.3). Nothing in here
// knows SVG exists; nothing in the renderer knows Arazzo exists. Exhaustively unit-tested in
// test/workflow-graph.test.mjs.
//
//   import { projectWorkflow, listWorkflows } from './workflow-graph.js';
//   const graph = projectWorkflow(document, 'place-order');
//   // → { workflowId, summary, nodes[], edges[], defaults, problems[] }

/**
 * @typedef {object} GraphNode
 * @property {string} id                 The stepId (unique per workflow).
 * @property {'operation'|'channel'|'workflow'|'unknown'} kind  The step's binding kind.
 * @property {string} label              Primary label (the stepId).
 * @property {string} sublabel           Secondary label (operationId / channelPath / workflowId).
 * @property {object} binding            The raw binding fields ({operationId} | {operationPath} | {channelPath, action?, correlationId?, timeout?} | {workflowId}).
 * @property {string} [description]
 * @property {number} criteriaCount      Number of successCriteria.
 * @property {{retryAfter?: number, retryLimit?: number, actionName: string}|null} retry  A local retry failure action, if any.
 * @property {boolean} end               Whether a local action ends the workflow at this step.
 * @property {number} localSuccessCount  Local onSuccess actions (resolved, incl. reusable).
 * @property {number} localFailureCount  Local onFailure actions (resolved, incl. reusable).
 * @property {boolean} usesDefaultSuccess  No local onSuccess → workflow-level successActions apply.
 * @property {boolean} usesDefaultFailure  No local onFailure → workflow-level failureActions apply.
 * @property {number} outputCount        Number of declared outputs.
 */

/**
 * @typedef {object} GraphEdge
 * @property {string} id
 * @property {string} from               Source stepId.
 * @property {string} to                 Target stepId.
 * @property {'seq'|'success'|'failure'} kind  Implicit sequence, or an explicit goto action edge.
 * @property {string} [actionName]       The action's name (goto edges).
 * @property {string} [criteriaSummary]  First criterion condition (+n more), for the edge label.
 * @property {boolean} [reusable]        True when the action came from a `$components` reference.
 */

/**
 * Reserved ids for the start/end pseudo-nodes. Projection-only artifacts (`#` cannot appear in a
 * stepId): the start node anchors the workflow's inputs and the entry edge; the end terminal is
 * the target of every `end` action edge and of the implicit fall-off-the-last-step completion.
 * They are never written into the Arazzo document.
 */
export const START_ID = '#start';
export const END_ID = '#end';

/**
 * Mints the id for an action edge (a local `goto` or `end`), single-sourced so the projection and
 * the debug tray's `frameAt` re-minting agree — a mismatch means the taken edge never lights
 * (design §10). Grammar: `end:<stepId>:<name||'end'>:<kind>` and `goto:<stepId>:<name||target>:<kind>`,
 * where `target` is the goto's step id (local) or workflow id (cross-workflow exit), `kind` is
 * `success`/`failure` (the onSuccess/onFailure list the action came from), and the fallback uses `||`
 * (an empty name falls back, matching the projection). Cross-workflow gotos carry `:kind` like every
 * sibling.
 * @param {string} stepId the source step's id
 * @param {'success'|'failure'} kind the action's list
 * @param {{type: 'goto'|'end', name?: string, target?: string}} action
 */
export function actionEdgeId(stepId, kind, action) {
  if (action.type === 'end') return `end:${stepId}:${action.name || 'end'}:${kind}`;
  return `goto:${stepId}:${action.name || action.target}:${kind}`;
}

/** List the workflows in a document as `{workflowId, summary}` (for the designer's switcher). */
export function listWorkflows(doc) {
  return (doc?.workflows || []).map((w) => ({ workflowId: w.workflowId, summary: w.summary || '' }));
}

/**
 * Project one workflow of an Arazzo document onto the design surface's graph shape.
 *
 * @param {object} doc         The Arazzo document (plain JSON).
 * @param {string} workflowId  Which workflow to project.
 * @returns {{workflowId: string, summary: string, nodes: GraphNode[], edges: GraphEdge[],
 *            defaults: {successActions: object[], failureActions: object[]},
 *            problems: {message: string, stepId?: string}[]}}
 */
export function projectWorkflow(doc, workflowId) {
  const workflow = (doc?.workflows || []).find((w) => w.workflowId === workflowId);
  if (!workflow) {
    return {
      workflowId,
      summary: '',
      nodes: [],
      edges: [],
      defaults: { successActions: [], failureActions: [] },
      problems: [{ message: `Workflow '${workflowId}' not found in the document.` }],
    };
  }

  const problems = [];
  const steps = workflow.steps || [];
  const stepIds = new Set(steps.map((s) => s.stepId));

  // Start/end pseudo-nodes bracket the step nodes: start carries the workflow's typed inputs,
  // end carries its outputs. Projection-only — see START_ID/END_ID.
  const nodes = [
    {
      id: START_ID,
      kind: 'start',
      pseudo: true,
      label: 'start',
      sublabel: '',
      binding: {},
      criteriaCount: 0,
      retry: null,
      localSuccessCount: 0,
      localFailureCount: 0,
      usesDefaultSuccess: false,
      usesDefaultFailure: false,
      outputCount: 0,
      inputCount: Object.keys(workflow.inputs?.properties || {}).length,
    },
    ...steps.map((step) => projectNode(doc, workflow, step, problems)),
    {
      id: END_ID,
      kind: 'end',
      pseudo: true,
      label: 'end',
      sublabel: '',
      binding: {},
      criteriaCount: 0,
      retry: null,
      localSuccessCount: 0,
      localFailureCount: 0,
      usesDefaultSuccess: false,
      usesDefaultFailure: false,
      outputCount: Object.keys(workflow.outputs || {}).length,
      inputCount: 0,
    },
  ];
  const edges = [];

  const divertsAfterSuccess = (step) => {
    const actions = resolveActions(doc, step.onSuccess, problems, step.stepId);
    return actions.some((a) => (a.type === 'end' || a.type === 'goto') && !(a.criteria?.length));
  };

  // Implicit sequence edges: entry → first step, step i → i+1, last step → end; all muted. A step
  // whose *unconditional* local success action is `end` or `goto` never falls through, so its
  // sequence edge is elided.
  if (steps.length) {
    edges.push({ id: `seq:${START_ID}`, from: START_ID, to: steps[0].stepId, kind: 'seq' });
    for (let i = 0; i < steps.length; i++) {
      if (divertsAfterSuccess(steps[i])) continue;
      edges.push({
        id: `seq:${steps[i].stepId}`,
        from: steps[i].stepId,
        to: i < steps.length - 1 ? steps[i + 1].stepId : END_ID,
        kind: 'seq',
      });
    }
  }

  // Explicit action edges: local goto and end actions, success and failure. Arazzo dispatch is
  // FIRST-MATCH-WINS in declaration order (ControlFlowEmitter), so each edge carries its order,
  // and any action following a criteria-less catch-all is flagged unreachable.
  for (const step of steps) {
    for (const [list, kind, listName] of [[step.onSuccess, 'success', 'onSuccess'], [step.onFailure, 'failure', 'onFailure']]) {
      const actions = resolveActions(doc, list, problems, step.stepId);
      let catchAllName = null;
      for (let i = 0; i < actions.length; i++) {
        const action = actions[i];
        const unreachable = catchAllName != null;
        if (unreachable) {
          problems.push({
            stepId: step.stepId,
            message: `${listName} action '${action.name || action.type}' in '${step.stepId}' is unreachable — '${catchAllName}' has no criteria and always matches first.`,
          });
        }
        if (!(action.criteria?.length) && catchAllName == null) {
          catchAllName = action.name || action.type;
        }
        const orderInfo = actions.length > 1 ? { order: i + 1, orderCount: actions.length } : {};
        if (unreachable) orderInfo.unreachable = true;
        if (action.type === 'end') {
          edges.push({
            id: actionEdgeId(step.stepId, kind, { type: 'end', name: action.name }),
            from: step.stepId,
            to: END_ID,
            kind,
            actionName: action.name,
            criteriaSummary: summarizeCriteria(action.criteria),
            reusable: action.__reusable || undefined,
            ...orderInfo,
          });
          continue;
        }
        if (action.type !== 'goto') continue;
        if (action.workflowId) {
          // A goto to another workflow: no in-graph target; surfaced as a problem-free badge case
          // later — for now record an edge to a synthetic id the renderer shows as an exit chip.
          edges.push({
            id: actionEdgeId(step.stepId, kind, { type: 'goto', name: action.name, target: action.workflowId }),
            from: step.stepId,
            to: `workflow:${action.workflowId}`,
            kind,
            actionName: action.name,
            criteriaSummary: summarizeCriteria(action.criteria),
            reusable: action.__reusable || undefined,
            ...orderInfo,
          });
          continue;
        }
        if (!stepIds.has(action.stepId)) {
          problems.push({ stepId: step.stepId, message: `goto target '${action.stepId}' is not a step in '${workflow.workflowId}'.` });
          continue;
        }
        edges.push({
          id: actionEdgeId(step.stepId, kind, { type: 'goto', name: action.name, target: action.stepId }),
          from: step.stepId,
          to: action.stepId,
          kind,
          actionName: action.name,
          criteriaSummary: summarizeCriteria(action.criteria),
          reusable: action.__reusable || undefined,
          ...orderInfo,
        });
      }
    }
  }

  // The same first-match rule governs the workflow-level defaults lists.
  const defaults = {
    successActions: resolveActions(doc, workflow.successActions, problems),
    failureActions: resolveActions(doc, workflow.failureActions, problems),
  };
  for (const [actions, listName] of [[defaults.successActions, 'successActions'], [defaults.failureActions, 'failureActions']]) {
    let catchAllName = null;
    for (const action of actions) {
      if (catchAllName != null) {
        problems.push({
          message: `workflow ${listName} action '${action.name || action.type}' is unreachable — '${catchAllName}' has no criteria and always matches first.`,
        });
      }
      if (!(action.criteria?.length) && catchAllName == null) catchAllName = action.name || action.type;
    }
  }

  // Edge ids derive from action names; duplicate names (e.g. two identically-named gotos) must
  // still yield unique ids or the renderer's keyed updates leave stale twins behind on re-path.
  const seen = new Map();
  for (const edge of edges) {
    const n = (seen.get(edge.id) || 0) + 1;
    seen.set(edge.id, n);
    if (n > 1) edge.id = `${edge.id}~${n}`;
  }

  return {
    workflowId: workflow.workflowId,
    summary: workflow.summary || '',
    nodes,
    edges,
    defaults,
    problems,
  };
}

function projectNode(doc, workflow, step, problems) {
  const binding = {};
  let kind = 'unknown';
  let sublabel = '';
  if (step.operationId != null) {
    kind = 'operation';
    binding.operationId = step.operationId;
    sublabel = step.operationId;
  } else if (step.operationPath != null) {
    kind = 'operation';
    binding.operationPath = step.operationPath;
    sublabel = step.operationPath;
  } else if (step.channelPath != null) {
    kind = 'channel';
    binding.channelPath = step.channelPath;
    if (step.action != null) binding.action = step.action;
    if (step.correlationId != null) binding.correlationId = step.correlationId;
    if (step.timeout != null) binding.timeout = step.timeout;
    sublabel = `${step.action || 'send'} ${step.channelPath}`;
  } else if (step.workflowId != null) {
    kind = 'workflow';
    binding.workflowId = step.workflowId;
    sublabel = step.workflowId;
  } else {
    problems.push({ stepId: step.stepId, message: `Step '${step.stepId}' has no operation/channel/workflow binding.` });
  }

  const onSuccess = resolveActions(doc, step.onSuccess, problems, step.stepId);
  const onFailure = resolveActions(doc, step.onFailure, problems, step.stepId);
  const retryAction = onFailure.find((a) => a.type === 'retry');

  return {
    id: step.stepId,
    kind,
    label: step.stepId,
    sublabel,
    binding,
    description: step.description,
    criteriaCount: step.successCriteria?.length || 0,
    retry: retryAction
      ? { retryAfter: retryAction.retryAfter, retryLimit: retryAction.retryLimit, actionName: retryAction.name }
      : null,
    end: onSuccess.some((a) => a.type === 'end') || onFailure.some((a) => a.type === 'end'),
    localSuccessCount: onSuccess.length,
    localFailureCount: onFailure.length,
    usesDefaultSuccess: onSuccess.length === 0 && (workflow.successActions?.length || 0) > 0,
    usesDefaultFailure: onFailure.length === 0 && (workflow.failureActions?.length || 0) > 0,
    outputCount: Object.keys(step.outputs || {}).length,
  };
}

/**
 * Resolve an onSuccess/onFailure/successActions/failureActions list: pass action objects through and
 * dereference reusable `{reference: '$components.(success|failure)Actions.<name>'}` entries against
 * the document's components. Unresolvable references become problems, not actions.
 */
function resolveActions(doc, list, problems, stepId) {
  const resolved = [];
  for (const entry of list || []) {
    if (entry && typeof entry.reference === 'string') {
      const match = /^\$components\.(successActions|failureActions)\.([^.]+)$/.exec(entry.reference);
      const target = match ? doc?.components?.[match[1]]?.[match[2]] : undefined;
      if (target) {
        resolved.push({ ...target, __reusable: true });
      } else {
        problems.push({ stepId, message: `Unresolvable reusable action reference '${entry.reference}'.` });
      }
      continue;
    }
    if (entry) resolved.push(entry);
  }
  return resolved;
}

function summarizeCriteria(criteria) {
  if (!criteria?.length) return undefined;
  const first = criteria[0].condition || '';
  return criteria.length > 1 ? `${first} (+${criteria.length - 1})` : first;
}
