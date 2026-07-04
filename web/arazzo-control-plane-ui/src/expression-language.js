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
  token(stream, state) {
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

    if (stream.match('"')) { state.str = '"'; return this.token(stream, state); }
    if (stream.match("'")) { state.str = "'"; return this.token(stream, state); }

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
  },
};

/**
 * Schema-driven completions for a partial expression.
 *
 * @param {string} text  The whole field text.
 * @param {number} pos   The caret position within it.
 * @param {{inputs?: string[], outputs?: string[], steps?: Record<string, {outputs?: string[], summary?: string}>}} [context]
 * @returns {{from: number, options: {label: string, type: string, detail?: string}[]} | null}
 */
export function completionsFor(text, pos, context = {}) {
  const head = text.slice(0, pos);
  const frag = /(\$[A-Za-z0-9_.-]*)$/.exec(head)?.[1];
  if (!frag) return null;

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
    case '$inputs':
      return parts.length === 2 ? filter(context.inputs || [], 'property') : null;
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
        return filter(steps[parts[1]]?.outputs || [], 'property');
      }
      return null;
    }
    case '$response':
    case '$request':
      return parts.length === 2 ? filter(['body', 'header', 'query', 'path'], 'keyword') : null;
    case '$message':
      return parts.length === 2 ? filter(['payload', 'header'], 'keyword') : null;
    default:
      return null;
  }
}
