// Drives the corvus-ts Bowtie harness with the official JSON-Schema-Test-Suite (the same suite Bowtie runs),
// locally, via the Bowtie protocol. Per dialect: send start + dialect + one `run` per top-level test group +
// stop, then tally validity against the suite's expectations. refRemote.json is excluded (the harness has no
// Bowtie registry / remote-$ref support yet). Usage: node drive-suite.mjs [dialect ...]   (default: all five)
import { readFileSync, readdirSync } from "node:fs";
import { execFileSync } from "node:child_process";
import { join } from "node:path";

const SUITE = "/home/mwa/src/Corvus.JsonSchema/JSON-Schema-Test-Suite/tests";
const URI = {
  "draft2020-12": "https://json-schema.org/draft/2020-12/schema",
  "draft2019-09": "https://json-schema.org/draft/2019-09/schema",
  "draft7": "http://json-schema.org/draft-07/schema#",
  "draft6": "http://json-schema.org/draft-06/schema#",
  "draft4": "http://json-schema.org/draft-04/schema#",
};
const dialects = process.argv.slice(2).length ? process.argv.slice(2) : Object.keys(URI);

for (const d of dialects) {
  const dir = join(SUITE, d);
  const files = readdirSync(dir).filter((f) => f.endsWith(".json") && f !== "refRemote.json").sort();
  const cmds = [JSON.stringify({ cmd: "start", version: 1 }), JSON.stringify({ cmd: "dialect", dialect: URI[d] })];
  const expected = {};
  let seq = 0;
  for (const f of files) {
    for (const g of JSON.parse(readFileSync(join(dir, f), "utf8"))) {
      seq++;
      expected[seq] = { file: f, desc: g.description, tests: g.tests };
      cmds.push(JSON.stringify({ cmd: "run", seq, case: { description: g.description, schema: g.schema, tests: g.tests.map((t) => ({ description: t.description, instance: t.data, valid: t.valid })) } }));
    }
  }
  cmds.push(JSON.stringify({ cmd: "stop" }));

  const t0 = Date.now();
  const resp = execFileSync("node", ["bowtie-harness.mjs"], { input: cmds.join("\n") + "\n", maxBuffer: 1 << 30, cwd: import.meta.dirname, stdio: ["pipe", "pipe", "inherit"] }).toString();

  let pass = 0, fail = 0, errored = 0, total = 0;
  const samples = [];
  for (const line of resp.split("\n")) {
    if (!line.trim()) continue;
    let r; try { r = JSON.parse(line); } catch { continue; }
    if (r.seq === undefined) continue;
    const exp = expected[r.seq];
    if (r.errored) { errored += exp.tests.length; total += exp.tests.length; if (samples.length < 12) samples.push(`ERR  ${exp.file} :: ${exp.desc} :: ${String(r.context && r.context.message).split("\n")[0].slice(0, 90)}`); continue; }
    r.results.forEach((res, i) => {
      total++;
      if (res.errored) { errored++; if (samples.length < 12) samples.push(`ERR  ${exp.file} :: ${exp.tests[i].description}`); }
      else if (res.valid === exp.tests[i].valid) { pass++; }
      else { fail++; if (samples.length < 12) samples.push(`FAIL ${exp.file} :: ${exp.desc} :: ${exp.tests[i].description} (got ${res.valid}, want ${exp.tests[i].valid})`); }
    });
  }
  console.log(`\n=== ${d}: ${pass}/${total} pass, ${fail} fail, ${errored} errored  (${seq} groups, ${((Date.now() - t0) / 1000).toFixed(0)}s) ===`);
  for (const s of samples) console.log("  " + s);
}
