// Runs the jsonschema-benchmark corpora through this evaluator (and, optionally, jsu-js) in fresh processes, as the
// benchmark's Makefile does, and prints a comparison table.
//
//   node bench/corpora.mjs --schemas <jsonschema-benchmark>/schemas [--runs 3] [--only a,b] [--jsu <dir>]
//   node bench/corpora.mjs --render bench/results/corpora-<stamp>.json
//
// --jsu points at a directory holding jsu-js's jsonschema_benchmark.js with json_model_runtime installed, and
// needs jsu-compile on PATH (see bench/README.md). Results are written to bench/results/. --render prints the table
// for an earlier run's results without measuring again.

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
const render = arg('--render', undefined);

if (!schemasDir && !render) {
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

const fmt = (ns) =>
  ns === undefined ? 'n/a' : ns >= 1e9 ? `${(ns / 1e9).toFixed(2)} s` : ns >= 1e6 ? `${(ns / 1e6).toFixed(2)} ms` : `${(ns / 1e3).toFixed(1)} µs`;

function measureAll() {
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
  return rows;
}

const PHASES = ['warm', 'cold', 'compile'];

function table(rows) {
  const lines = [];
  const withJsu = rows.some((r) => r.jsu);
  if (withJsu) {
    const head = PHASES.map((p) => `Corvus TS ${p} | jsu-js ${p} | Corvus / jsu`).join(' | ');
    lines.push(`| Corpus | Instances | ${head} |`);
    lines.push(`|---|---:|${PHASES.map(() => '---:|---:|---:|').join('')}`);
  } else {
    lines.push('| Corpus | Instances | Corvus TS warm | Corvus TS cold | Corvus TS compile | Parse |');
    lines.push('|---|---:|---:|---:|---:|---:|');
  }

  const ratios = Object.fromEntries(PHASES.map((p) => [p, []]));
  for (const r of rows) {
    if (withJsu) {
      const cells = PHASES.map((p) => {
        const ratio = r.corvus && r.jsu ? r.corvus[p] / r.jsu[p] : undefined;
        if (ratio !== undefined) ratios[p].push(ratio);
        return `${fmt(r.corvus?.[p])} | ${fmt(r.jsu?.[p])} | ${ratio?.toFixed(2) ?? 'n/a'}`;
      });
      lines.push(`| ${r.name} | ${r.count} | ${cells.join(' | ')} |`);
    } else {
      lines.push(`| ${r.name} | ${r.count} | ${fmt(r.corvus?.warm)} | ${fmt(r.corvus?.cold)} | ${fmt(r.corvus?.compile)} | ${fmt(r.corvus?.parse)} |`);
    }
  }

  if (withJsu) {
    lines.push('');
    for (const p of PHASES) {
      const xs = ratios[p];
      const geo = Math.exp(xs.reduce((a, b) => a + Math.log(b), 0) / xs.length);
      lines.push(`- ${p[0].toUpperCase()}${p.slice(1)}: Corvus TS faster on ${xs.filter((x) => x < 1).length} of ${xs.length}; geometric mean Corvus / jsu-js ${geo.toFixed(2)}.`);
    }
  }

  const invalid = rows.filter((r) => r.corvus && !r.corvus.valid).map((r) => r.name);
  if (invalid.length > 0) lines.push('', `Corpora with instances Corvus TS reported invalid: ${invalid.join(', ')}.`);
  return lines.join('\n');
}

if (render) {
  console.log(table(JSON.parse(fs.readFileSync(render, 'utf8')).rows));
  process.exit(0);
}

const rows = measureAll();
const text = table(rows);
console.log(text);

fs.mkdirSync(path.join(here, 'results'), { recursive: true });
const stamp = new Date().toISOString().replace(/[:.]/g, '-');
fs.writeFileSync(path.join(here, 'results', `corpora-${stamp}.json`), JSON.stringify({ node: process.version, runs, rows }, null, 2));
fs.writeFileSync(path.join(here, 'results', `corpora-${stamp}.md`), text + '\n');
