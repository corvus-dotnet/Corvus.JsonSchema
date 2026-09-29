// The jsonschema-benchmark (https://github.com/sourcemeta-research/jsonschema-benchmark) implementation entry point:
//
//   node main.mjs <schema.json> <instances.jsonl>
//
// Mirrors the other implementations: read the instance file, parse every instance (timed), compile the schema
// (timed), validate every instance once cold, warm up, validate once more warm. Prints one line
// "cold,warm,compile,parse" in nanoseconds and exits non-zero if any instance is invalid.
import fs from 'node:fs';
import { performance } from 'node:perf_hooks';

// CORVUS_JSON_SCHEMA lets a local checkout run this file against its own build (see bench/corpora.mjs).
const { compile } = await import(process.env.CORVUS_JSON_SCHEMA ?? '@corvus-dotnet/json-schema');

const WARMUP_ITERATIONS = 1000;
const MAX_WARMUP_TIME = 1e9 * 10; // 10 seconds

function validateAll(instances, validate) {
  let failed = false;
  for (let i = 0; i < instances.length; i++) {
    if (!validate(instances[i])) {
      failed = true;
    }
  }
  return failed;
}

if (process.argv.length !== 4) {
  console.error('Usage: main.mjs <schema> <instances>');
  process.exit(1);
}

const schema = JSON.parse(fs.readFileSync(process.argv[2], 'utf8'));
const lines = fs.readFileSync(process.argv[3], 'utf8').split('\n').filter((line) => line.length > 0);

const parseStart = performance.now();
const instances = lines.map((line) => JSON.parse(line));
const parseNs = (performance.now() - parseStart) * 1e6;

// The benchmark schemas keep "format" as an annotation, as in the other implementations.
const compileStart = performance.now();
const validate = compile(schema, { assertFormat: false });
const compileNs = (performance.now() - compileStart) * 1e6;

const coldStart = performance.now();
const failed = validateAll(instances, validate);
const coldNs = (performance.now() - coldStart) * 1e6;

const iterations = Math.min(WARMUP_ITERATIONS, Math.ceil(MAX_WARMUP_TIME / Math.max(coldNs, 1)));
for (let i = 0; i < iterations; i++) {
  validateAll(instances, validate);
}

const warmStart = performance.now();
validateAll(instances, validate);
const warmNs = (performance.now() - warmStart) * 1e6;

console.log(`${coldNs.toFixed(0)},${warmNs.toFixed(0)},${compileNs.toFixed(0)},${parseNs.toFixed(0)}`);
if (failed) {
  process.exit(1);
}
