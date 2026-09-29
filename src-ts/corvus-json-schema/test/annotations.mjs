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
const drafts = { draft4: [Dialect.Draft4, '4'], draft6: [Dialect.Draft6, '6'], draft7: [Dialect.Draft7, '7'], 'draft2019-09': [Dialect.Draft201909, '2019'], 'draft2020-12': [Dialect.Draft202012, '2020'] };
const order = ['3', '4', '6', '7', '2019', '2020'];
const compatible = (draft, compat) => {
  const level = order.indexOf(draft);
  if (compat.startsWith('<=')) {
    const max = order.indexOf(compat.slice(2));
    return max >= 0 && level <= max;
  }
  const min = order.indexOf(compat);
  return min >= 0 && level >= min;
};
const resolveRemote = (uri) => {
  const prefix = 'http://localhost:1234/';
  if (!uri.startsWith(prefix)) return undefined;
  const file = path.join(suiteRoot, 'remotes', uri.slice(prefix.length));
  return fs.existsSync(file) ? JSON.parse(fs.readFileSync(file, 'utf8')) : undefined;
};

let total = 0;
let failed = 0;
for (const [draft, [dialect, compatLevel]] of Object.entries(drafts)) {
  for (const file of fs.readdirSync(dir).filter((f) => f.endsWith('.json')).sort()) {
    const suite = JSON.parse(fs.readFileSync(path.join(dir, file), 'utf8'));
    for (const group of suite.suite) {
      if (group.compatibility !== undefined && !compatible(compatLevel, group.compatibility)) continue;
      const validator = compile(group.schema, { defaultDialect: dialect, resolveDocument: resolveRemote });
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
