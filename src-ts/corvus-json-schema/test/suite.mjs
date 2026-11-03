// Runs the JSON-Schema-Test-Suite (the repository's submodule) against the evaluator, mirroring the C# SuiteRunner.
// Every dialect's cases are in one tree (validation/tests, required and optional): each case's compatibility selects
// the drafts it applies to, its tags whether format is asserted, and its externalSchemas are served to that case alone.
//
//   node test/suite.mjs [--verbose] [--draft draft2020-12] [--filter text]
//
// Exits non-zero when any case outside the documented exclusions fails.

import fs from 'node:fs';
import path from 'node:path';
import { fileURLToPath, pathToFileURL } from 'node:url';
import { compile, Dialect, generateModule, JsonSchemaResultsCollector, ResultsLevel } from '../dist/index.js';

const here = path.dirname(fileURLToPath(import.meta.url));
const suiteRoot = process.env.JSON_SCHEMA_TEST_SUITE ?? path.resolve(here, '../../../JSON-Schema-Test-Suite');
const testsRoot = path.join(suiteRoot, 'validation', 'tests');

const args = process.argv.slice(2);
const verbose = args.includes('--verbose');
// --module: evaluate through generateModule (the standalone ES module) instead of compile.
const viaModule = args.includes('--module');
const runtimeUrl = pathToFileURL(path.resolve(here, '../dist/runtime.js')).href;
const collectingUrl = pathToFileURL(path.resolve(here, '../dist/collecting.js')).href;
// --collect basic|detailed|verbose: evaluate through a results collector at that level.
const collectLevel = args.includes('--collect') ? { basic: ResultsLevel.Basic, detailed: ResultsLevel.Detailed, verbose: ResultsLevel.Verbose }[args[args.indexOf('--collect') + 1]] : undefined;
const draftArg = args.includes('--draft') ? args[args.indexOf('--draft') + 1] : undefined;
const filter = args.includes('--filter') ? args[args.indexOf('--filter') + 1] : undefined;

const drafts = {
  draft4: [Dialect.Draft4, 4],
  draft6: [Dialect.Draft6, 6],
  draft7: [Dialect.Draft7, 7],
  'draft2019-09': [Dialect.Draft201909, 2019],
  'draft2020-12': [Dialect.Draft202012, 2020],
};

// Exclusions, matching the C# runner: zero-terminated floats (JSON.parse cannot tell 1.0 from 1 either).
const excludedFiles = new Set(['draft4/optional/zero-terminated-floats.json']);

// Test cases left out by name (file and test description):
// - the six required leap-second cases, which optional/format-time.json contradicts for the same drafts (the
//   evaluator accepts leap seconds);
// - the high-precision cases of optional/bignum.json: the evaluator takes JavaScript values, so a number has at most
//   the precision of a double by the time it sees it.
const excludedCases = new Set([
  'format-time.json|a leap second is not a valid time',
  'format-time.json|a leap second with a zero time-offset is not a valid time',
  'format-time.json|a leap second with a positive time-offset is not a valid time',
  'format-time.json|a leap second with a large positive time-offset is not a valid time',
  'format-time.json|a leap second with a negative time-offset is not a valid time',
  'format-time.json|a leap second with a large negative time-offset is not a valid time',
  'bignum.json|below the exclusive maximum is valid',
  'bignum.json|above the exclusive minimum is valid',
  'bignum.json|just above the maximum is invalid',
  'bignum.json|just below the minimum is invalid',
  'bignum.json|a slightly smaller decimal is invalid',
  'bignum.json|a slightly larger decimal is invalid',
]);

// Whether a test case's compatibility (a comma-separated list of minimums, <= maximums and = releases, from the
// smallest release to the largest) admits the release, by the algorithm of the suite's validation/README.md.
function compatible(compatibility, release) {
  if (compatibility === undefined) return true;
  let valid = true;
  for (const constraint of compatibility.split(',')) {
    const [op, version] = constraint.startsWith('<=') ? ['<=', constraint.slice(2)] : constraint.startsWith('=') ? ['=', constraint.slice(1)] : ['', constraint];
    const number = Number.parseInt(version.trim(), 10);
    valid = op === '<=' ? release <= number : op === '=' ? release === number : release >= number;
    if (release <= number) break;
  }
  return valid;
}

// A resolver serving a test case's externalSchemas at their retrieval URIs.
const resolverFor = (group) => (group.externalSchemas === undefined ? undefined : (uri) => group.externalSchemas[uri]);

function files(sub) {
  const dir = path.join(testsRoot, sub);
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
  let evaluate;
  let source;
  if (!viaModule) {
    const v = compile(schema, options);
    evaluate = v.evaluate;
    source = v.source;
  } else {
    source = generateModule(schema, { ...options, runtimeImport: runtimeUrl, collectingImport: collectingUrl });
    const mod = await import('data:text/javascript,' + encodeURIComponent(source));
    evaluate = mod.evaluate;
  }
  if (collectLevel === undefined) return Object.assign((x) => evaluate(x), { source });
  return Object.assign((x) => {
    const collector = JsonSchemaResultsCollector.create(collectLevel);
    const valid = evaluate(x, collector);
    // The root summary row always exists and carries the overall result.
    if (!collector.results.some((r) => r.evaluationLocation === '' && r.documentEvaluationLocation === '' && r.isMatch === valid)) {
      throw new Error('no root summary row matching the result');
    }
    return valid;
  }, { source });
}

async function runFile(draft, file, label) {
  const [dialect, release] = drafts[draft];
  const { tests: groups } = JSON.parse(fs.readFileSync(file, 'utf8'));
  const fileName = path.basename(file);
  let fileTotal = 0;
  let fileFailed = 0;
  for (const group of groups) {
    if (!compatible(group.compatibility, release)) continue;
    const tags = group.tags ?? [];
    // The evaluator asserts format by default up to draft7, so a format-annotation case does not apply there.
    if (tags.includes('format-annotation') && release <= 7) continue;
    const assertFormat = tags.includes('format-assertion');
    if (filter && !group.description.includes(filter) && !label.includes(filter)) continue;
    let validate;
    let compileError;
    try {
      validate = await build(group.schema, {
        defaultDialect: dialect,
        assertFormat: assertFormat ? true : undefined,
        resolveDocument: resolverFor(group),
      });
    } catch (e) {
      compileError = e;
    }
    for (const test of group.tests) {
      if (excludedCases.has(`${fileName}|${test.description}`)) continue;
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
  for (const f of files('')) await runFile(draft, f, `${draft}/${path.basename(f)}`);
  for (const f of files('optional')) {
    const label = `${draft}/optional/${path.basename(f)}`;
    if (!excludedFiles.has(label)) await runFile(draft, f, label);
  }
}

for (const line of failures) console.log(line);
const byArea = {};
for (const [label, t, f] of summary) {
  const area = label.split('/').slice(0, label.includes('/optional/') ? 2 : 1).join('/');
  byArea[area] ??= [0, 0];
  byArea[area][0] += t;
  byArea[area][1] += f;
}
for (const [area, [t, f]] of Object.entries(byArea)) console.log(`${area.padEnd(34)} ${String(t - f).padStart(5)}/${t}`);
console.log(`\n${total - failed}/${total} passed, ${failed} failed`);
process.exit(failed === 0 ? 0 : 1);
