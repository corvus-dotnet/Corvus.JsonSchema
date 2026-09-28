// A kit element is an HTMLElement, so a method named after a DOM method shadows it for every host that calls it. A
// panel's delete action named remove() made element.remove() open a delete confirmation instead of detaching the
// panel. This refuses any component method named after a DOM method whose meaning it would change.
import { test } from 'node:test';
import assert from 'node:assert/strict';
import { readdirSync, readFileSync } from 'node:fs';
import { join } from 'node:path';
import { fileURLToPath } from 'node:url';

const ROOT = fileURLToPath(new URL('..', import.meta.url));
const SHADOWED = ['remove', 'append', 'prepend', 'before', 'after', 'replaceWith', 'replaceChildren', 'appendChild',
  'removeChild', 'insertBefore', 'cloneNode', 'contains', 'closest', 'matches', 'click', 'blur', 'scrollIntoView',
  'setAttribute', 'getAttribute', 'removeAttribute', 'toggleAttribute', 'dispatchEvent', 'addEventListener',
  'attachShadow', 'animate'];
const METHOD = new RegExp(`^\\s+(?:async\\s+)?(?:static\\s+)?(${SHADOWED.join('|')})\\s*\\([^)]*\\)\\s*\\{`);

const files = [
  ...readdirSync(join(ROOT, 'src')).filter((f) => f.endsWith('.js')).map((f) => join(ROOT, 'src', f)),
  ...readdirSync(join(ROOT, 'src', 'components')).filter((f) => f.endsWith('.js')).map((f) => join(ROOT, 'src', 'components', f)),
];

test('no kit component defines a method that shadows a DOM method', () => {
  const offenders = [];
  for (const path of files) {
    const text = readFileSync(path, 'utf8');
    if (!/extends (ArazzoElement|HTMLElement)/.test(text)) continue;
    text.split('\n').forEach((line, i) => {
      const match = METHOD.exec(line);
      if (match) offenders.push(`${path.slice(ROOT.length)}:${i + 1}: ${match[1]}()`);
    });
  }
  assert.deepEqual(offenders, []);
});
