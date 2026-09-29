// Runs the jsonschema-benchmark corpora through this evaluator (and, optionally, jsu-js) in fresh processes, as the
// benchmark's Makefile does, and prints a comparison table.
//
//   node bench/corpora.mjs --schemas <jsonschema-benchmark>/schemas [--runs 3] [--only a,b] [--jsu <dir>]
//
// --jsu points at a directory holding jsu-js's jsonschema_benchmark.js with json_model_runtime installed, and
// needs jsu-compile on PATH (see bench/README.md). Results are written to bench/results/.

import { spawnSync } from 'node:child_process';
import fs from 'node:fs';
import path from 'node:path';
import { fileURLToPath, pathToFileURL } from 'node:url';

const here = path.dirname(fileURLToPath(import.meta.url));
const args = process.argv.slice(2);
const arg = (name, fallback) => (args.includes(name) ? args[args.indexOf(name) + 1] : fallback);
const schemasDir = arg('--schemas', process.env.JSONSCHEMA_BENCHMARK ? path.join(process.env.JSONSCHEMA_BENCHMARK, 'schemas') : undefined);
const runs = Number(arg('--runs', '3'));
const only = arg('--only', undefined)?.split(',');
const jsuDir = arg('--jsu', undefined);

if (!schemasDir) {
  console.error('Pass --schemas <jsonschema-benchmark>/schemas (or set JSONSCHEMA_BENCHMARK).');
  process.exit(2);
}

function parseLine(stdout) {
  const line = stdout.trim().split('\n').pop() ?? '';
  const parts = line.split(',').map(Number);
  return parts.length >= 4 && parts.every(Number.isFinite) ? parts : undefined;
}

function runCorvus(schema, instances) {
  const r = spawnSync(process.execPath, [path.join(here, 'jsonschema-benchmark', 'main.mjs'), schema, instances], {
    encoding: 'utf8',
    env: { ...process.env, CORVUS_JSON_SCHEMA: pathToFileURL(path.join(here, '..', 'dist', 'index.js')).href },
  });
  const parts = parseLine(r.stdout);
  return parts && { cold: parts[0], warm: parts[1], compile: parts[2], parse: parts[3], status: r.status };
}

function runJsu(schema, instances) {
  const out = path.join(jsuDir, 'schema.js');
  const compileStart = process.hrtime.bigint();
  const c = spawnSync(
    'jsu-compile',
    ['--quiet', '--no-id', '--no-strict', '--no-fix', '--no-format', '--no-reporting', '--loose', '-o', out, schema, '--', '--quiet'],
    { encoding: 'utf8' },
  );
  const compileNs = Number(process.hrtime.bigint() - compileStart);
  if (c.status !== 0) return undefined;
  const r = spawnSync(process.execPath, [path.join(jsuDir, 'jsonschema_benchmark.js'), instances], { encoding: 'utf8', cwd: jsuDir });
  const line = r.stdout.trim().split('\n').pop() ?? '';
  const parts = line.split(',').map(Number);
  if (parts.length !== 3 || !parts.every(Number.isFinite)) return undefined;
  return { cold: parts[0], warm: parts[1], compile: compileNs, parse: parts[2], status: r.status };
}

const median = (xs) => {
  const s = [...xs].sort((a, b) => a - b);
  return s[Math.floor(s.length / 2)];
};

function measure(fn, schema, instances) {
  const results = [];
  for (let i = 0; i < runs; i++) {
    const r = fn(schema, instances);
    if (r === undefined) return undefined;
    results.push(r);
  }
  return {
    cold: median(results.map((r) => r.cold)),
    warm: median(results.map((r) => r.warm)),
    compile: median(results.map((r) => r.compile)),
    parse: median(results.map((r) => r.parse)),
    valid: results.every((r) => r.status === 0),
  };
}

const fmt = (ns) => (ns === undefined ? 'n/a' : ns >= 1e6 ? `${(ns / 1e6).toFixed(2)} ms` : `${(ns / 1e3).toFixed(1)} µs`);

const corpora = fs.readdirSync(schemasDir).filter((d) => !only || only.includes(d)).sort();
const rows = [];
for (const name of corpora) {
  const schema = path.join(schemasDir, name, 'schema.json');
  const instances = path.join(schemasDir, name, 'instances.jsonl');
  const count = fs.readFileSync(instances, 'utf8').split('\n').filter((l) => l.length > 0).length;
  const corvus = measure(runCorvus, schema, instances);
  const jsu = jsuDir ? measure(runJsu, schema, instances) : undefined;
  rows.push({ name, count, corvus, jsu });
  const ratio = corvus && jsu ? (corvus.warm / jsu.warm).toFixed(2) : '';
  console.error(`${name}: corvus warm ${fmt(corvus?.warm)}${corvus?.valid === false ? ' (INVALID)' : ''}${jsu ? `, jsu-js warm ${fmt(jsu.warm)}, ratio ${ratio}` : ''}`);
}

const lines = [];
if (jsuDir) {
  lines.push('| Corpus | Instances | Corvus TS warm | jsu-js warm | Corvus / jsu | Corvus TS cold | jsu-js cold | Corvus TS compile |');
  lines.push('|---|---:|---:|---:|---:|---:|---:|---:|');
} else {
  lines.push('| Corpus | Instances | Corvus TS warm | Corvus TS cold | Corvus TS compile | Parse |');
  lines.push('|---|---:|---:|---:|---:|---:|');
}
const ratios = [];
for (const r of rows) {
  if (jsuDir) {
    const ratio = r.corvus && r.jsu ? r.corvus.warm / r.jsu.warm : undefined;
    if (ratio !== undefined) ratios.push(ratio);
    lines.push(
      `| ${r.name} | ${r.count} | ${fmt(r.corvus?.warm)} | ${fmt(r.jsu?.warm)} | ${ratio?.toFixed(2) ?? 'n/a'} | ${fmt(r.corvus?.cold)} | ${fmt(r.jsu?.cold)} | ${fmt(r.corvus?.compile)} |`,
    );
  } else {
    lines.push(`| ${r.name} | ${r.count} | ${fmt(r.corvus?.warm)} | ${fmt(r.corvus?.cold)} | ${fmt(r.corvus?.compile)} | ${fmt(r.corvus?.parse)} |`);
  }
}
if (ratios.length > 0) {
  const geo = Math.exp(ratios.reduce((a, b) => a + Math.log(b), 0) / ratios.length);
  lines.push('', `Corvus TS faster on ${ratios.filter((x) => x < 1).length} of ${ratios.length}; geometric mean Corvus / jsu-js ${geo.toFixed(2)}.`);
}
const invalid = rows.filter((r) => r.corvus && !r.corvus.valid).map((r) => r.name);
if (invalid.length > 0) lines.push('', `Corpora with instances Corvus TS reported invalid: ${invalid.join(', ')}.`);
console.log(lines.join('\n'));

fs.mkdirSync(path.join(here, 'results'), { recursive: true });
const stamp = new Date().toISOString().replace(/[:.]/g, '-');
fs.writeFileSync(path.join(here, 'results', `corpora-${stamp}.json`), JSON.stringify({ node: process.version, runs, rows }, null, 2));
fs.writeFileSync(path.join(here, 'results', `corpora-${stamp}.md`), lines.join('\n') + '\n');
