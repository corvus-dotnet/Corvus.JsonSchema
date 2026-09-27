// The kit's source meets the Content-Security-Policy its host sends (ADR 0073), where script-src and style-src are
// 'self' alone. So no markup the kit writes may carry a style element, a style attribute, an inline script or an inline event
// handler; a component styles its shadow root with adoptStyles (constructable stylesheets, which style-src does not
// govern) and sets a dynamic style through the CSSOM. The smoke and UX suites catch a violation only where they render
// it; this catches it in any file, before a browser runs. The vendored CodeMirror bundle is excluded, since it adopts its
// sheets through the CSSOM and sets style through element.style.
import { test } from 'node:test';
import assert from 'node:assert/strict';
import { readdirSync, readFileSync } from 'node:fs';
import { join, relative } from 'node:path';
import { fileURLToPath } from 'node:url';

const ROOT = fileURLToPath(new URL('..', import.meta.url));

function* files(dir, extensions) {
  for (const entry of readdirSync(dir, { withFileTypes: true })) {
    const path = join(dir, entry.name);
    if (entry.isDirectory()) {
      if (entry.name !== 'vendor') yield* files(path, extensions);
    } else if (extensions.some((extension) => entry.name.endsWith(extension))) {
      yield path;
    }
  }
}

const SOURCES = [...files(join(ROOT, 'src'), ['.js', '.html']), ...files(join(ROOT, 'demo'), ['.js', '.html'])];

const FORBIDDEN = [
  { what: 'a style element', pattern: /<style[\s>]/i },
  { what: 'a style attribute', pattern: /\sstyle\s*=\s*["'`$]/i },
  { what: 'an inline script', pattern: /<script(?![^>]*\ssrc\s*=)[^>]*>/i },
  { what: 'an inline event handler', pattern: /<[a-z][^>]*\son[a-z]+\s*=\s*["'`$]/i },
];

test('the scan covers the kit', () => {
  const names = SOURCES.map((path) => relative(ROOT, path).replaceAll('\\', '/'));
  assert.ok(names.includes('src/components/base.js'));
  assert.ok(names.includes('demo/designer.html'));
  assert.ok(names.length > 80, `only ${names.length} files scanned`);
});

for (const { what, pattern } of FORBIDDEN) {
  test(`no file of the kit writes ${what}`, () => {
    const offenders = [];
    for (const path of SOURCES) {
      readFileSync(path, 'utf8').split('\n').forEach((line, i) => {
        if (pattern.test(line)) offenders.push(`${relative(ROOT, path)}:${i + 1}: ${line.trim()}`);
      });
    }
    assert.deepEqual(offenders, []);
  });
}
