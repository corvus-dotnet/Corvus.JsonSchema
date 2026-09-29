// Validates a corpus in a tight loop, for profiling (node --cpu-prof bench/loop.mjs <dir> [seconds]) and quick A/B
// timing. Prints the fastest pass over all instances.
import fs from 'node:fs';
import path from 'node:path';
import { compile } from '../dist/index.js';

const dir = process.argv[2];
const seconds = Number(process.argv[3] ?? '3');
const schema = JSON.parse(fs.readFileSync(path.join(dir, 'schema.json'), 'utf8'));
const instances = fs.readFileSync(path.join(dir, 'instances.jsonl'), 'utf8').split('\n').filter(Boolean).map((l) => JSON.parse(l));
const validate = compile(schema);
if (process.env.DUMP) fs.writeFileSync(process.env.DUMP, validate.source);
let best = Infinity;
const end = performance.now() + seconds * 1000;
while (performance.now() < end) {
  const t = performance.now();
  for (let i = 0; i < instances.length; i++) if (!validate(instances[i])) throw new Error(`instance ${i} is invalid`);
  best = Math.min(best, performance.now() - t);
}
console.log(`${path.basename(dir)}: ${(best * 1000).toFixed(1)} µs per pass`);
