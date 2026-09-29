// Drives bowtie/bowtie_corvus.js over IHOP the way Bowtie does (start, dialect, run with a registry of the suite's
// remotes, stop) for every required test in the JSON-Schema-Test-Suite, and reports disagreements. No containers.
//
//   node test/bowtie-ihop.mjs
import { spawn } from 'node:child_process';
import fs from 'node:fs';
import path from 'node:path';
import readline from 'node:readline';
import { fileURLToPath, pathToFileURL } from 'node:url';

const here = path.dirname(fileURLToPath(import.meta.url));
const suiteRoot = process.env.JSON_SCHEMA_TEST_SUITE ?? path.resolve(here, '../../../JSON-Schema-Test-Suite');
const dialects = {
  draft4: 'http://json-schema.org/draft-04/schema#',
  draft6: 'http://json-schema.org/draft-06/schema#',
  draft7: 'http://json-schema.org/draft-07/schema#',
  'draft2019-09': 'https://json-schema.org/draft/2019-09/schema',
  'draft2020-12': 'https://json-schema.org/draft/2020-12/schema',
};

// Bowtie's registry: every remote, keyed by its http://localhost:1234/ URI.
const registry = {};
const remotes = path.join(suiteRoot, 'remotes');
(function walk(dir) {
  for (const entry of fs.readdirSync(dir, { withFileTypes: true })) {
    const full = path.join(dir, entry.name);
    if (entry.isDirectory()) walk(full);
    else if (entry.name.endsWith('.json')) registry['http://localhost:1234/' + path.relative(remotes, full).split(path.sep).join('/')] = JSON.parse(fs.readFileSync(full, 'utf8'));
  }
})(remotes);

const harness = spawn(process.execPath, [path.resolve(here, '../bowtie/bowtie_corvus.js')], {
  env: { ...process.env, CORVUS_JSON_SCHEMA: pathToFileURL(path.resolve(here, '../dist/index.js')).href },
  stdio: ['pipe', 'pipe', 'inherit'],
});
const lines = readline.createInterface({ input: harness.stdout })[Symbol.asyncIterator]();
async function send(request) {
  harness.stdin.write(JSON.stringify(request) + '\n');
  const { value } = await lines.next();
  return JSON.parse(value);
}

const start = await send({ cmd: 'start', version: 1 });
console.log(`${start.implementation.name} ${start.implementation.version} (${start.implementation.language_version})`);
let seq = 0;
let total = 0;
const failures = [];
for (const [draft, uri] of Object.entries(dialects)) {
  const ok = await send({ cmd: 'dialect', dialect: uri });
  if (!ok.ok) throw new Error(`dialect ${uri} refused`);
  const dir = path.join(suiteRoot, 'tests', draft);
  for (const file of fs.readdirSync(dir).filter((f) => f.endsWith('.json')).sort()) {
    for (const group of JSON.parse(fs.readFileSync(path.join(dir, file), 'utf8'))) {
      const request = {
        cmd: 'run',
        seq: ++seq,
        output: 'flag',
        case: { description: group.description, schema: group.schema, registry, tests: group.tests.map((t) => ({ description: t.description, instance: t.data })) },
      };
      const response = await send(request);
      if (response.seq !== seq) throw new Error(`seq mismatch ${response.seq} != ${seq}`);
      group.tests.forEach((t, i) => {
        total++;
        const r = response.errored ? response : response.results[i];
        if (r.errored || r.skipped || r.valid !== t.valid) failures.push(`${draft}/${file} [${group.description}] ${t.description}: ${JSON.stringify(r).slice(0, 200)}`);
      });
    }
  }
}
const annotations = await send({
  cmd: 'run',
  seq: ++seq,
  output: 'annotations',
  case: { description: 'a', schema: { properties: { 'a/b': { title: 'x' } } }, tests: [{ description: 't', instance: { 'a/b': 1 } }] },
});
const expectedAnnotations = [{ keyword: 'title', instanceLocation: '/a~1b', keywordLocation: '#/properties/a~1b/title', annotation: 'x' }];
if (JSON.stringify(annotations.results[0]) !== JSON.stringify({ valid: true, annotations: expectedAnnotations })) {
  failures.push(`annotations output: ${JSON.stringify(annotations.results[0])}`);
}
harness.stdin.write(JSON.stringify({ cmd: 'stop' }) + '\n');
for (const f of failures) console.log(f);
console.log(`${total - failures.length}/${total} agree with the suite through the Bowtie harness`);
process.exit(failures.length === 0 ? 0 : 1);
