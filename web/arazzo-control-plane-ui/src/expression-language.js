// Arazzo expression language support — DOM-free tokenizer + completions for runtime expressions
// ($inputs / $steps / $response …), the `simple` criterion grammar, and embedded JSONPath
// (RFC 9535, ported from the playground's Monarch grammar). Consumed by
// <arazzo-expression-input> through CodeMirror 6's StreamLanguage, and unit-tested directly with a
// fake stream (test/expression-language.test.mjs) — the CM6 wrapper stays a thin adapter.

/** The runtime-expression roots Arazzo defines (ArazzoExpressionSource). */
export const EXPRESSION_ROOTS = [
  '$url', '$method', '$statusCode', '$request', '$response', '$message',
  '$inputs', '$outputs', '$steps', '$workflows', '$sourceDescriptions', '$components', '$self',
];

/** RFC 9535 filter functions. */
const JSONPATH_FUNCTIONS = new Set(['length', 'count', 'match', 'search', 'value']);

/**
 * A CodeMirror StreamParser (see @codemirror/language StreamLanguage.define) over the expression
 * grammar. Token names are resolved through the component's tokenTable; keep them stable.
 */
export const expressionStreamParser = {
  name: 'arazzo-expression',
  startState: () => ({ str: null, afterDot: false }),
  // NB: a plain function, not a method using `this` — CodeMirror invokes `token` detached.
  token: tokenExpression,
};

function tokenExpression(stream, state) {
  {
    // Inside a string literal (JSON-style escapes; RFC 9535 allows \' in single-quoted).
    if (state.str) {
      while (!stream.eol()) {
        if (stream.match('\\')) { stream.next(); continue; }
        const ch = stream.next();
        if (ch === state.str) { state.str = null; return 'exprString'; }
      }
      return 'exprString';
    }

    if (stream.eatSpace?.() || stream.match(/^\s+/)) return null;

    const wasAfterDot = state.afterDot;
    state.afterDot = false;

    if (stream.match('"')) { state.str = '"'; return tokenExpression(stream, state); }
    if (stream.match("'")) { state.str = "'"; return tokenExpression(stream, state); }

    // Numbers (incl. negative + decimal).
    if (stream.match(/^-?\d+(\.\d+)?/)) return 'exprNumber';

    // Runtime-expression roots ($inputs, $steps, …); a bare `$`/`@` is the JSONPath root/current node.
    const root = stream.match(/^\$[A-Za-z_][A-Za-z0-9_]*/);
    if (root) return EXPRESSION_ROOTS.includes(root[0]) ? 'exprRoot' : 'exprBadRoot';
    if (stream.match('$') || stream.match('@')) return 'exprRoot';

    // JSON Pointer tail of an expression ($response.body#/receipt/id).
    if (stream.match(/^#(\/[^\s\]'"),]*)*/)) return 'exprPointer';

    // Recursive descent, filter-bracket, wildcard.
    if (stream.match('..')) return 'exprKeyword';
    if (stream.match('[?')) return 'exprKeyword';
    if (stream.match('*')) return 'exprKeyword';

    // Operators (the `simple` grammar + JSONPath filters).
    if (stream.match('&&') || stream.match('||')) return 'exprOperator';
    if (stream.match(/^[!=]=/) || stream.match(/^[<>]=?/)) return 'exprOperator';
    if (stream.match('!')) return 'exprOperator';

    if (stream.match(/^[[\]()]/)) return 'exprBracket';
    if (stream.match(/^[:,]/)) return 'exprPunct';
    if (stream.match('.')) { state.afterDot = true; return 'exprPunct'; }

    const ident = stream.match(/^[A-Za-z_][A-Za-z0-9_-]*/);
    if (ident) {
      const word = ident[0];
      if (wasAfterDot) return 'exprProperty';
      if (word === 'true' || word === 'false' || word === 'null') return 'exprAtom';
      if (JSONPATH_FUNCTIONS.has(word) && stream.match(/^\s*\(/, false)) return 'exprFunction';
      return 'exprVariable';
    }

    stream.next();
    return null;
  }
}

/**
 * Schema-driven completions for a partial expression, including JSON-Pointer descent into payload
 * schemas: `$response.body#/receipt/…` completes from the response body's JSON Schema.
 *
 * Context shape (every surface optional):
 *   {
 *     inputs:  <JSON Schema> | string[],          // the workflow inputs (schema preferred)
 *     outputs: string[],                          // workflow output names
 *     steps:   { [stepId]: { outputs?: string[], outputSchemas?: {[name]: <JSON Schema>}, summary? } },
 *     response: { body?: <JSON Schema> },         // the selected step's response body schema
 *     request:  { body?: <JSON Schema> },
 *     message:  { payload?: <JSON Schema> },      // the selected channel's message payload schema
 *   }
 *
 * @param {string} text  The whole field text.
 * @param {number} pos   The caret position within it.
 * @returns {{from: number, options: {label: string, type: string, detail?: string}[]} | null}
 */
export function completionsFor(text, pos, context = {}) {
  const head = text.slice(0, pos);
  const frag = /(\$[A-Za-z0-9_.-]*(?:#[A-Za-z0-9_./-]*)?)$/.exec(head)?.[1];
  if (!frag) return null;

  // A `#/json/pointer` tail: walk the matching schema surface.
  const hashAt = frag.indexOf('#');
  if (hashAt >= 0) {
    const schema = schemaForExpression(frag.slice(0, hashAt), context);
    const pointer = frag.slice(hashAt + 1);
    if (!schema || (pointer !== '' && !pointer.startsWith('/'))) return null;
    const segments = pointer === '' ? [''] : pointer.split('/').slice(1);
    const last = segments.pop() ?? '';
    let target = schema;
    for (const seg of segments) {
      target = stepInto(target, seg);
      if (!target) return null;
    }
    const options = Object.entries(target.properties || {})
      .filter(([name]) => name.toLowerCase().startsWith(last.toLowerCase()))
      .map(([name, s]) => ({ label: name, type: 'property', ...(schemaDetail(s) ? { detail: schemaDetail(s) } : {}) }));
    return options.length ? { from: pos - last.length, options } : null;
  }

  const parts = frag.split('.');
  const last = parts[parts.length - 1];
  const from = pos - last.length;
  const filter = (labels, type, details) => {
    const options = labels
      .filter((l) => l.toLowerCase().startsWith(last.toLowerCase()))
      .map((label) => ({ label, type, ...(details?.[label] ? { detail: details[label] } : {}) }));
    return options.length ? { from, options } : null;
  };

  if (parts.length === 1) {
    return filter(EXPRESSION_ROOTS, 'keyword');
  }
  switch (parts[0]) {
    case '$inputs': {
      if (parts.length !== 2) return null;
      if (isSchema(context.inputs)) {
        const props = context.inputs.properties || {};
        return filter(Object.keys(props), 'property', Object.fromEntries(
          Object.entries(props).map(([n, s]) => [n, schemaDetail(s)]).filter(([, v]) => v),
        ));
      }
      return filter(context.inputs || [], 'property');
    }
    case '$outputs':
      return parts.length === 2 ? filter(context.outputs || [], 'property') : null;
    case '$steps': {
      const steps = context.steps || {};
      if (parts.length === 2) {
        return filter(Object.keys(steps), 'property', Object.fromEntries(
          Object.entries(steps).map(([id, s]) => [id, s?.summary]).filter(([, v]) => v),
        ));
      }
      if (parts.length === 3) return filter(['outputs'], 'keyword');
      if (parts.length === 4 && parts[2] === 'outputs') {
        const step = steps[parts[1]];
        return filter(step?.outputs || Object.keys(step?.outputSchemas || {}), 'property');
      }
      return null;
    }
    case '$response':
    case '$request':
    case '$message': {
      // Only the surface position (`$response.`) completes here. Each surface carries the continuation
      // the Arazzo grammar requires, so the author never has to guess: the body / payload is addressed
      // by a JSON Pointer (`body#/…`), the flat maps by a name (`header.…`). A response exposes body +
      // header; a request adds query + path; a message exposes its payload + header.
      if (parts.length !== 2) {
        // A dotted tail into the body / payload (`$response.body.foo`) is not a valid Arazzo expression:
        // the runtime reads `$response.body.foo` as a literal. Descent is the `#/pointer` form, served by
        // the hash branch. So offer nothing here rather than lead the author into an invalid expression.
        return null;
      }
      const surfaces = parts[0] === '$message'
        ? [['payload', 'payload#/'], ['header', 'header.']]
        : parts[0] === '$response'
          ? [['body', 'body#/'], ['header', 'header.']]
          : [['body', 'body#/'], ['header', 'header.'], ['query', 'query.'], ['path', 'path.']];
      const options = surfaces
        .filter(([name]) => name.toLowerCase().startsWith(last.toLowerCase()))
        .map(([name, apply]) => ({ label: name, type: 'keyword', apply }));
      return options.length ? { from, options } : null;
    }
    default:
      return null;
  }
}

/**
 * The expression-completion context for a step's editors: the workflow-wide roots (`$inputs` / `$steps`
 * / `$outputs`) plus THIS step's OWN operation surfaces, so `$response.body#/…` and `$request.body#/…`
 * complete against the REAL response / request body schema of the operation the step calls, not a
 * stand-in. Body descent is a JSON Pointer, so these are the JSON Schemas the pointer walk reads.
 * `$response` / `$request` / `$message` are per-step (they depend on the operation the step calls), so
 * they are derived here rather than carried on a workflow-wide context.
 *
 * @param {object} base The workflow-wide context (`$inputs` / `$steps` / `$outputs`); any response /
 *   request / message it carries is dropped, since those are per-step.
 * @param {{ responses?: Record<string, {schema?: object}>, request?: {schema?: object} }} [op] The
 *   step's resolved operation (the `listSourceOperations` shape): a response map keyed by status code,
 *   each with a body `schema`, and a request with a body `schema`.
 * @param {{ channelPath?: string }} [step] The step, so a message (channel) step surfaces its payload
 *   as `$message.payload` rather than a `$response` / `$request` body.
 * @returns {object} The per-step completion context.
 */
export function stepCompletionContext(base, op, step) {
  const ctx = { ...base };
  delete ctx.response;
  delete ctx.request;
  delete ctx.message;
  if (!op) {
    return ctx;
  }
  if (step?.channelPath) {
    // A message (send / receive) step: its payload is the channel operation's message schema, the same
    // schema the body-skeleton template derives from.
    if (op.request?.schema) ctx.message = { payload: op.request.schema };
    return ctx;
  }
  if (op.responses) {
    const codes = Object.keys(op.responses);
    const status = codes.find((c) => /^2\d\d$/.test(c)) ?? codes[0];
    const schema = status ? op.responses[status]?.schema : undefined;
    if (schema) ctx.response = { body: schema };
  }
  if (op.request?.schema) {
    ctx.request = { body: op.request.schema };
  }
  return ctx;
}

/** The JSON Schema a pointer-completed expression addresses, if the context carries one. */
function schemaForExpression(expr, ctx) {
  if (expr === '$response.body') return ctx.response?.body;
  if (expr === '$request.body') return ctx.request?.body;
  if (expr === '$message.payload') return ctx.message?.payload;
  const parts = expr.split('.');
  if (parts[0] === '$inputs' && parts.length === 2 && isSchema(ctx.inputs)) {
    return ctx.inputs.properties?.[parts[1]];
  }
  if (parts[0] === '$steps' && parts.length === 4 && parts[2] === 'outputs') {
    return ctx.steps?.[parts[1]]?.outputSchemas?.[parts[3]];
  }
  return null;
}

/** One pointer segment of schema descent: object properties, or array items by index. */
function stepInto(schema, seg) {
  if (!schema) return null;
  if (schema.properties?.[seg]) return schema.properties[seg];
  if (schema.items && (/^\d+$/.test(seg) || seg === '-')) return schema.items;
  return null;
}

function schemaDetail(s) {
  if (!s || typeof s !== 'object') return undefined;
  const type = Array.isArray(s.type) ? s.type.join('|') : s.type;
  if (!type) return undefined;
  return s.format ? `${type} (${s.format})` : type;
}

function isSchema(v) {
  return !!v && typeof v === 'object' && !Array.isArray(v);
}

/**
 * Resolves a runtime expression to its VALUE against a paused debug frame (design §3.3's
 * expression console): `$inputs.…`, `$outputs.…`, `$steps.<id>.outputs.<name>…`, `$statusCode`,
 * and `$response.body`, each with optional dotted descent and a `#/json/pointer` suffix. The
 * frame comes from the recorded trace at the scrub cursor — stateless like everything else in the
 * debugger: no server call, the trace already holds the context.
 *
 * @param {string} text The expression (a single value expression, not a criterion).
 * @param {{ inputs?: object, outputs?: any, steps?: Record<string, {outputs?: any}>,
 *           current?: {statusCode?: number, responseBody?: any} }} frame
 * @returns {{ found: true, value: any } | { found: false, reason: string }}
 */
export function resolveAgainstFrame(text, frame) {
  const trimmed = (text ?? '').trim();
  if (!trimmed.startsWith('$')) return { found: false, reason: 'enter a runtime expression (e.g. $steps.a.outputs.x)' };

  const hash = trimmed.indexOf('#');
  const head = hash < 0 ? trimmed : trimmed.slice(0, hash);
  const pointer = hash < 0 ? null : trimmed.slice(hash);
  const segments = head.split('.');
  const root = segments[0];

  let value;
  let rest;
  switch (root) {
    case '$inputs':
      value = frame?.inputs;
      rest = segments.slice(1);
      break;
    case '$outputs':
      value = frame?.outputs;
      rest = segments.slice(1);
      break;
    case '$statusCode':
      value = frame?.current?.statusCode;
      rest = segments.slice(1);
      break;
    case '$response':
      if (segments[1] !== 'body') return { found: false, reason: 'only $response.body is recorded in the trace' };
      value = frame?.current?.responseBody;
      rest = segments.slice(2);
      break;
    case '$steps': {
      const stepId = segments[1];
      if (!stepId) return { found: false, reason: 'name a step: $steps.<id>.outputs.<name>' };
      const step = frame?.steps?.[stepId];
      if (!step) return { found: false, reason: `step '${stepId}' has not executed in this frame` };
      if (segments[2] !== 'outputs') return { found: false, reason: 'only a step’s outputs are recorded: $steps.<id>.outputs.<name>' };
      value = step.outputs;
      rest = segments.slice(3);
      break;
    }

    default:
      return { found: false, reason: `'${root}' is not resolvable from a recorded trace` };
  }

  for (const segment of rest) {
    if (value == null || typeof value !== 'object') return { found: false, reason: `'${segment}' is not present in this frame` };
    if (!(segment in value)) return { found: false, reason: `'${segment}' is not present in this frame` };
    value = value[segment];
  }

  if (pointer !== null) {
    if (pointer === '#' || pointer === '#/') {
      // The whole value.
    } else if (!pointer.startsWith('#/')) {
      return { found: false, reason: 'a pointer suffix starts #/' };
    } else {
      for (const token of pointer.slice(2).split('/')) {
        const key = token.replaceAll('~1', '/').replaceAll('~0', '~');
        if (Array.isArray(value)) value = value[Number(key)];
        else if (value != null && typeof value === 'object' && key in value) value = value[key];
        else return { found: false, reason: `pointer '${pointer}' does not resolve in this frame` };
        if (value === undefined) return { found: false, reason: `pointer '${pointer}' does not resolve in this frame` };
      }
    }
  }

  return value === undefined
    ? { found: false, reason: 'undefined in this frame' }
    : { found: true, value };
}