// The execution budget's vocabulary (ADR 0068), in one place: the six limits as they are labelled, written and
// read, and the platform's fixed fault error types with what each means and what to do about it. The environments
// panel, the run detail and the runs table all read from here, so a label is typed once. The wording of the fault
// types is the same as the server's (WorkflowRunFaultTypes) and the REST reference's table.

/** @typedef {{ maxSteps?: number, wallClockSeconds?: number, maxSubWorkflowDepth?: number, retryAfterCeilingSeconds?: number, stepTimeoutSeconds?: number, maxResponseBytes?: number }} BudgetLimits */

const MEBIBYTE = 1024 * 1024;

/** Writes whole seconds as the exact number, with a readable form beside it when there is a clean one. */
export function formatSeconds(seconds) {
  const exact = `${seconds}s`;
  if (seconds >= 86400 && seconds % 86400 === 0) return `${exact} (${seconds / 86400}d)`;
  if (seconds >= 3600 && seconds % 3600 === 0) return `${exact} (${seconds / 3600}h)`;
  if (seconds >= 60 && seconds % 60 === 0) return `${exact} (${seconds / 60}m)`;
  return exact;
}

/** Writes a byte count as the exact number, with a readable form beside it when there is a clean one. */
export function formatBytes(bytes) {
  const exact = Number(bytes).toLocaleString('en-US');
  if (bytes >= MEBIBYTE && bytes % MEBIBYTE === 0) return `${exact} (${bytes / MEBIBYTE} MiB)`;
  if (bytes >= 1024 && bytes % 1024 === 0) return `${exact} (${bytes / 1024} KiB)`;
  return exact;
}

const formatCount = (value) => Number(value).toLocaleString('en-US');

/**
 * The six limits, in the order they are shown. `key` is the limit's name in the API, `minimum` the least an
 * override may name, and `hint` what the limit bounds, for the editor.
 */
export const BUDGET_LIMITS = Object.freeze([
  { key: 'maxSteps', label: 'Max steps', unit: 'attempts', minimum: 1, format: formatCount, hint: 'The most step attempts a run may make, retries and revisits counted.' },
  { key: 'wallClockSeconds', label: 'Wall clock', unit: 'seconds', minimum: 1, format: formatSeconds, hint: 'The longest a run may live, from its creation.' },
  { key: 'maxSubWorkflowDepth', label: 'Sub-workflow depth', unit: 'levels', minimum: 0, format: formatCount, hint: 'How deep sub-workflows may nest.' },
  { key: 'retryAfterCeilingSeconds', label: 'Retry-after ceiling', unit: 'seconds', minimum: 0, format: formatSeconds, hint: "The ceiling a step's declared retryAfter delay is clamped to." },
  { key: 'stepTimeoutSeconds', label: 'Step timeout', unit: 'seconds', minimum: 1, format: formatSeconds, hint: "The longest a single step's request may take. A breach fails the step, not the run." },
  { key: 'maxResponseBytes', label: 'Max response', unit: 'bytes', minimum: 1, format: formatBytes, hint: 'The largest response body a single step may read. A breach fails the step, not the run.' },
].map(Object.freeze));

/** Writes one limit of a budget, or a dash where the budget does not name it. */
export function formatLimit(limit, budget) {
  const value = budget?.[limit.key];
  return value === undefined || value === null ? '—' : limit.format(value);
}

/**
 * Reads an override editor's text into a limit. Empty means "leave this limit to the ceiling".
 * @param {{ key: string, label: string, minimum: number }} limit
 * @param {string} text
 * @param {BudgetLimits} [ceiling] The deployment's ceiling, which an override may only tighten.
 * @returns {{ value?: number, error?: string }} `value` undefined with no `error` for an empty input.
 */
export function parseLimit(limit, text, ceiling) {
  const trimmed = String(text ?? '').trim();
  if (trimmed === '') return {};
  if (!/^\d+$/.test(trimmed)) return { error: `${limit.label} must be a whole number.` };
  const value = Number(trimmed);
  if (!Number.isSafeInteger(value)) return { error: `${limit.label} is too large.` };
  if (value < limit.minimum) return { error: `${limit.label} must be at least ${limit.minimum}.` };
  const most = ceiling?.[limit.key];
  if (most !== undefined && value > most) return { error: `${limit.label} cannot exceed the deployment's ceiling of ${limit.format(most)}.` };
  return { value };
}

/** Whether two overrides name the same limits with the same values. */
export function sameLimits(a, b) {
  return BUDGET_LIMITS.every((limit) => (a?.[limit.key] ?? null) === (b?.[limit.key] ?? null));
}

/**
 * The fixed error types the platform itself records on a faulted run. Any other error is a step's own failure and
 * is shown as it was recorded.
 */
export const FAULT_TYPES = Object.freeze({
  'budget-fuel': Object.freeze({
    budget: true,
    meaning: "The run made as many step attempts as its budget allows (max steps).",
    remedy: "Raise the environment's max steps and resume the run, or re-run it. A run that made as many attempts as the journal holds (500) cannot be resumed.",
  }),
  'budget-deadline': Object.freeze({
    budget: true,
    meaning: "The run outlived its budget's wall clock.",
    remedy: "Raise the environment's wall clock and resume the run, or re-run it. A run older than the deployment's ceiling allows cannot be resumed.",
  }),
  'budget-depth': Object.freeze({
    budget: true,
    meaning: "The run nested sub-workflows past its budget's depth limit.",
    remedy: "Raise the environment's sub-workflow depth and resume the run, or flatten the workflow and re-run it.",
  }),
  'executor-unhandled': Object.freeze({
    budget: false,
    meaning: "The workflow's executor failed in a way nothing in the workflow handled.",
    remedy: "The failure itself is in the executor's trace, not on the run. Fix the cause, then resume the run.",
  }),
  'executor-unresolvable': Object.freeze({
    budget: false,
    meaning: "The workflow version's executor was refused: it is missing, not runnable, or failed verification.",
    remedy: 'Republish or repair the version in the catalog, then resume the run.',
  }),
  'transport-unbound': Object.freeze({
    budget: false,
    meaning: "A source the workflow calls has no usable binding in the run's environment.",
    remedy: "Add the source's credential binding for the environment, then resume the run.",
  }),
});

/** Describes a fault error, or returns null for a step's own failure. */
export function describeFault(error) {
  return Object.prototype.hasOwnProperty.call(FAULT_TYPES, error) ? FAULT_TYPES[error] : null;
}

/**
 * Whether a faulted run can be resumed now, and if not, why. A run faulted on its budget is resumable exactly when
 * the server says a re-budget would let it run (`rebudget.resumable`): the rule is the server's and is not re-derived.
 * @returns {{ resumable: boolean, reason?: string }}
 */
export function resumability(run) {
  if (run?.status !== 'Faulted') return { resumable: false, reason: 'Only a faulted run can be resumed.' };
  const described = describeFault(run.fault?.error);
  if (!described?.budget) return { resumable: true };
  if (run.rebudget?.resumable) return { resumable: true };
  return {
    resumable: false,
    reason: run.rebudget
      ? "This run is still outside the budget a resume would give it. Raise its environment's limit, or re-run it."
      : 'This run carries no budget to resolve again. Re-run it.',
  };
}
