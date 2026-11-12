// Runs the JSON-Schema-Test-Suite annotation tests (JSON-Schema-Test-Suite/annotations) through a verbose results
// collector, as the C# AnnotationSuiteTests do: every draft, cases filtered by "compatibility", format asserted per
// vocabulary, and each assertion compared with the annotations grouped by instance location, keyword and schema
// location. Exits non-zero on any failure.
import fs from 'node:fs';
import path from 'node:path';
import { fileURLToPath } from 'node:url';
import { collectAnnotations, compile, Dialect, JsonSchemaResultsCollector, ResultsLevel } from '../dist/index.js';
import { equal } from '../dist/runtime.js';

const here = path.dirname(fileURLToPath(import.meta.url));
const suiteRoot = process.env.JSON_SCHEMA_TEST_SUITE ?? path.resolve(here, '../../../JSON-Schema-Test-Suite');
const dir = path.join(suiteRoot, 'annotations', 'tests');
const drafts = { draft4: [Dialect.Draft4, 4], draft6: [Dialect.Draft6, 6], draft7: [Dialect.Draft7, 7], 'draft2019-09': [Dialect.Draft201909, 2019], 'draft2020-12': [Dialect.Draft202012, 2020] };

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

let total = 0;
let failed = 0;
for (const [draft, [dialect, release]] of Object.entries(drafts)) {
  for (const file of fs.readdirSync(dir).filter((f) => f.endsWith('.json')).sort()) {
    const suite = JSON.parse(fs.readFileSync(path.join(dir, file), 'utf8'));
    for (const group of suite.suite) {
      if (!compatible(group.compatibility, release)) continue;
      const validator = compile(group.schema, { defaultDialect: dialect, resolveDocument: resolverFor(group) });
      for (const test of group.tests) {
        const collector = JsonSchemaResultsCollector.create(ResultsLevel.Verbose);
        validator.evaluate(test.instance, collector);
        const produced = collectAnnotations(collector);
        for (const assertion of test.assertions) {
          total++;
          const actual = produced[assertion.location]?.[assertion.keyword];
          const expectedEmpty = Object.keys(assertion.expected).length === 0;
          const ok = expectedEmpty ? actual === undefined : actual !== undefined && equal(actual, assertion.expected);
          if (!ok) {
            failed++;
            console.log(`${draft}/${file} [${group.description}] instance ${JSON.stringify(test.instance)} '${assertion.location}' ${assertion.keyword}: expected ${JSON.stringify(assertion.expected)}, actual ${JSON.stringify(actual)}`);
          }
        }
      }
    }
  }
}
console.log(`${total - failed}/${total} annotation assertions passed`);
process.exit(failed === 0 ? 0 : 1);
