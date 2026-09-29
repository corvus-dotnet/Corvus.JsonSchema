// Times the phases of the first compile in a fresh process (what the benchmark's compile figure measures), and a
// second compile of the same schema (the compiler already JIT-compiled).
//   node bench/compile-phases.mjs <schema.json>
import fs from 'node:fs';
import { SchemaCompiler } from '../dist/compiler.js';
import { CodeGenerator } from '../dist/codegen.js';
import { compile } from '../dist/index.js';

const text = fs.readFileSync(process.argv[2], 'utf8');
for (const round of ['first', 'second']) {
  const t0 = performance.now();
  const schema = JSON.parse(text);
  const t1 = performance.now();
  const program = SchemaCompiler.compile(schema, { assertFormat: false });
  const t2 = performance.now();
  const g = new CodeGenerator(program).generate();
  const t3 = performance.now();
  const v = compile(schema, { assertFormat: false });
  const t4 = performance.now();
  console.log(`${round}: parse ${(t1 - t0).toFixed(1)} compile ${(t2 - t1).toFixed(1)} codegen ${(t3 - t2).toFixed(1)} | whole compile() ${(t4 - t3).toFixed(1)} ms; nodes ${program.nodes.length}, source ${(v.source.length / 1024).toFixed(0)} KB`);
}
