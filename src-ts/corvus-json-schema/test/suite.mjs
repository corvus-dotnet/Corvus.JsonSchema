// Runs the JSON-Schema-Test-Suite (the repository's submodule) against the evaluator, mirroring the C# SuiteRunner:
// required and optional tests with format as an annotation, optional/format with format asserted.
//
//   node test/suite.mjs [--verbose] [--draft draft2020-12] [--filter text]
//
// Exits non-zero when any case outside the documented exclusions fails.

import fs from 'node:fs';
import path from 'node:path';
import { fileURLToPath, pathToFileURL } from 'node:url';
import { compile, Dialect, generateModule } from '../dist/index.js';

const here = path.dirname(fileURLToPath(import.meta.url));
const suiteRoot = process.env.JSON_SCHEMA_TEST_SUITE ?? path.resolve(here, '../../../JSON-Schema-Test-Suite');
const remotesRoot = path.join(suiteRoot, 'remotes');

const args = process.argv.slice(2);
const verbose = args.includes('--verbose');
// --module: evaluate through generateModule (the standalone ES module) instead of compile.
const viaModule = args.includes('--module');
const runtimeUrl = pathToFileURL(path.resolve(here, '../dist/runtime.js')).href;
const draftArg = args.includes('--draft') ? args[args.indexOf('--draft') + 1] : undefined;
const filter = args.includes('--filter') ? args[args.indexOf('--filter') + 1] : undefined;

const drafts = {
  draft4: Dialect.Draft4,
  draft6: Dialect.Draft6,
  draft7: Dialect.Draft7,
  'draft2019-09': Dialect.Draft201909,
  'draft2020-12': Dialect.Draft202012,
};

// Exclusions, matching the C# runner: zero-terminated floats (JSON.parse cannot tell 1.0 from 1 either).
const excludedFiles = new Set(['draft4/optional/zeroTerminatedFloats.json']);

const remoteCache = new Map();
function resolveRemote(uri) {
  const prefix = 'http://localhost:1234/';
  if (!uri.startsWith(prefix)) return undefined;
  const file = path.join(remotesRoot, uri.slice(prefix.length));
  if (!fs.existsSync(file)) return undefined;
  if (!remoteCache.has(file)) remoteCache.set(file, JSON.parse(fs.readFileSync(file, 'utf8')));
  return remoteCache.get(file);
}

function files(draft, sub) {
  const dir = path.join(suiteRoot, 'tests', draft, sub);
  if (!fs.existsSync(dir)) return [];
  return fs
    .readdirSync(dir)
    .filter((f) => f.endsWith('.json'))
    .sort()
    .map((f) => path.join(dir, f));
}

let total = 0;
let failed = 0;
const failures = [];
const summary = [];

async function build(schema, options) {
  if (!viaModule) return compile(schema, options);
  const source = generateModule(schema, { ...options, runtimeImport: runtimeUrl });
  const mod = await import('data:text/javascript,' + encodeURIComponent(source));
  return Object.assign((x) => mod.default(x), { source });
}

async function runFile(draft, file, label, assertFormat) {
  const groups = JSON.parse(fs.readFileSync(file, 'utf8'));
  let fileTotal = 0;
  let fileFailed = 0;
  for (const group of groups) {
    if (filter && !group.description.includes(filter) && !label.includes(filter)) continue;
    let validate;
    let compileError;
    try {
      validate = await build(group.schema, {
        defaultDialect: drafts[draft],
        assertFormat: assertFormat ? true : undefined,
        resolveDocument: resolveRemote,
      });
    } catch (e) {
      compileError = e;
    }
    for (const test of group.tests) {
      fileTotal++;
      let actual;
      let error;
      if (compileError) {
        error = compileError;
      } else {
        try {
          actual = validate(test.data);
        } catch (e) {
          error = e;
        }
      }
      if (error !== undefined || actual !== test.valid) {
        // Leap seconds are skipped in the format run, as in the C# runner.
        if (assertFormat && /leap second/i.test(test.description)) continue;
        fileFailed++;
        failures.push(`${label} [${group.description}] ${test.description}: expected ${test.valid}, got ${error ? `${error.name}: ${error.message}` : actual}`);
        if (verbose && validate) failures.push(validate.source);
      }
    }
  }
  total += fileTotal;
  failed += fileFailed;
  summary.push([label, fileTotal, fileFailed]);
}

for (const draft of Object.keys(drafts)) {
  if (draftArg && draft !== draftArg) continue;
  for (const f of files(draft, '')) await runFile(draft, f, `${draft}/${path.basename(f)}`, false);
  for (const f of files(draft, 'optional')) {
    const label = `${draft}/optional/${path.basename(f)}`;
    if (!excludedFiles.has(label)) await runFile(draft, f, label, false);
  }
  for (const f of files(draft, path.join('optional', 'format'))) await runFile(draft, f, `${draft}/optional/format/${path.basename(f)}`, true);
}

for (const line of failures) console.log(line);
const byArea = {};
for (const [label, t, f] of summary) {
  const area = label.split('/').slice(0, label.includes('/optional/format/') ? 3 : label.includes('/optional/') ? 2 : 1).join('/');
  byArea[area] ??= [0, 0];
  byArea[area][0] += t;
  byArea[area][1] += f;
}
for (const [area, [t, f]] of Object.entries(byArea)) console.log(`${area.padEnd(34)} ${String(t - f).padStart(5)}/${t}`);
console.log(`\n${total - failed}/${total} passed, ${failed} failed`);
process.exit(failed === 0 ? 0 : 1);
