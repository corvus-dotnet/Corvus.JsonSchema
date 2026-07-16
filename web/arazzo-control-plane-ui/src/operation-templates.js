// Operation-derived templates — DOM-free (design §5.3): from the binding's OpenAPI/AsyncAPI
// surface (OperationDescriptor via listSourceOperations; the demo supplies fixture tables),
// derive editable starting points the author refines:
//
//   templatesFromResponses(responses)  → successCriteria + per-response failure actions
//   payloadSkeletonFromSchema(schema)  → a request-body / message-payload skeleton
//
// Criteria templates: given the response status codes an operation documents, derive template
// successCriteria and onFailure actions:
//
//   - success codes (2xx / '2XX')   → a successCriteria condition ($statusCode == 201, or the
//                                     range form when several succeed)
//   - documented error codes        → one failure action each, criteria'd to the code
//                                     ('4XX'/'5XX' wildcards become range conditions)
//   - 'default' documented          → a criteria-less catch-all failure action
//   - NO 'default' documented       → an 'unexpected-failure' fallback action is still added,
//                                     so undocumented failures always have an explicit home
//
// Actions template as `end` — the author retargets them (goto/retry) in the action editor.

/**
 * @param {string[] | Record<string, unknown>} responses  Status codes ('200', '402', '5XX',
 *   'default'), or an object keyed by them (an OpenAPI responses map shape).
 * @returns {{successCriteria: object[], failureActions: object[]} | null} null when there is
 *   nothing to derive (no responses supplied).
 */
export function templatesFromResponses(responses) {
  const codes = Array.isArray(responses) ? [...responses] : Object.keys(responses || {});
  if (!codes.length) return null;

  const norm = codes.map((c) => String(c).toUpperCase());
  const successes = norm.filter((c) => /^2\d\d$/.test(c) || c === '2XX');
  const failures = norm.filter((c) => !successes.includes(c) && c !== 'DEFAULT');
  const hasDefault = norm.includes('DEFAULT');

  const successCriteria = [];
  if (successes.length === 1 && /^\d+$/.test(successes[0])) {
    successCriteria.push({ condition: `$statusCode == ${successes[0]}` });
  } else if (successes.length) {
    successCriteria.push({ condition: '$statusCode >= 200 && $statusCode < 300' });
  }

  const failureActions = [];
  for (const code of failures) {
    if (/^\d+$/.test(code)) {
      failureActions.push({
        name: `on-${code}`,
        type: 'end',
        criteria: [{ condition: `$statusCode == ${code}` }],
      });
    } else if (/^[1-5]XX$/.test(code)) {
      const base = Number(code[0]) * 100;
      failureActions.push({
        name: `on-${code.toLowerCase()}`,
        type: 'end',
        criteria: [{ condition: `$statusCode >= ${base} && $statusCode < ${base + 100}` }],
      });
    }
  }
  // The catch-all comes last: the documented `default` response when there is one, and an
  // explicit fallback for undocumented failures when there is not.
  failureActions.push(hasDefault
    ? { name: 'on-default', type: 'end' }
    : { name: 'unexpected-failure', type: 'end' });

  return { successCriteria, failureActions };
}

/**
 * A payload skeleton from a JSON Schema: every property present with a type-appropriate stub, so
 * the author fills values (typically runtime expressions) instead of typing structure. Honors
 * `default`, `const`, and `enum[0]`; arrays template one item; recursion is depth-capped so
 * cyclic schemas terminate.
 *
 * @param {object} schema  A JSON Schema (the operation's request-body or message-payload schema).
 * @returns {unknown} The skeleton value, or undefined when there is no schema to derive from.
 */
export function payloadSkeletonFromSchema(schema, depth = 0) {
  if (!schema || typeof schema !== 'object' || depth > 6) return undefined;
  if (schema.default !== undefined) return structuredClone(schema.default);
  if (schema.const !== undefined) return structuredClone(schema.const);
  if (Array.isArray(schema.enum) && schema.enum.length) return structuredClone(schema.enum[0]);

  const type = Array.isArray(schema.type) ? schema.type[0] : schema.type;
  if (type === 'object' || (!type && schema.properties)) {
    const out = {};
    for (const [name, propSchema] of Object.entries(schema.properties || {})) {
      const v = payloadSkeletonFromSchema(propSchema, depth + 1);
      if (v !== undefined) out[name] = v;
    }
    return out;
  }
  if (type === 'array') {
    const item = payloadSkeletonFromSchema(schema.items, depth + 1);
    return item !== undefined ? [item] : [];
  }
  if (type === 'string') return '';
  if (type === 'number' || type === 'integer') return 0;
  if (type === 'boolean') return false;
  if (type === 'null') return null;
  return undefined;
}
