// bench_table.js — Produces a formatted comparison table from BenchmarkDotNet results.
//
// Usage:
//   node benchmarks/bench_table.js [results-dir]
//
// If results-dir is omitted, defaults to:
//   benchmarks/Corvus.Text.Json.Jsonata.Benchmarks/BenchmarkDotNet.Artifacts/results
//
// Output: Markdown table to stdout with Native/RT/CG columns for Mean, Alloc, and Ratios.

const fs = require("fs");
const path = require("path");

const defaultDir = path.join(
  __dirname,
  "Corvus.Text.Json.Jsonata.Benchmarks",
  "BenchmarkDotNet.Artifacts",
  "results"
);
const dir = process.argv[2] || defaultDir;

if (!fs.existsSync(dir)) {
  console.error(`Results directory not found: ${dir}`);
  process.exit(1);
}

const files = fs
  .readdirSync(dir)
  .filter((f) => f.endsWith("-report-default.md"))
  .sort();

if (files.length === 0) {
  console.error("No *-report-default.md files found.");
  process.exit(1);
}

// Parse all data rows from every report file.
const rows = [];
for (const fname of files) {
  const suite = fname
    .replace("-report-default.md", "")
    .replace("Corvus.Text.Json.Jsonata.Benchmarks.", "")
    .replace("Benchmark", "");

  const lines = fs.readFileSync(path.join(dir, fname), "utf8").split(/\r?\n/);

  // Find the header row to locate column indices dynamically.
  // BDN tables may have varying columns (Gen0, Gen1, Gen2, etc.).
  let colRatio = 5, colAlloc = -1;
  for (const line of lines) {
    const t = line.trim();
    if (t.includes("Method") && t.includes("|")) {
      const hdrs = t.split("|").map((p) => p.trim());
      for (let i = 0; i < hdrs.length; i++) {
        if (hdrs[i] === "Ratio") colRatio = i;
        if (hdrs[i] === "Allocated") colAlloc = i;
      }
      break;
    }
  }

  for (const line of lines) {
    const t = line.trim();
    if (!t.includes("|") || t.includes("Method") || t.startsWith("-")) continue;

    const parts = t.split("|").map((p) => p.trim());
    const method = parts[0];
    if (!method) continue;

    const cat = parts[1];
    const mean = parts[2];
    const ratio = parts[colRatio] || "";
    const alloc = colAlloc >= 0 ? (parts[colAlloc] || "-") : "-";

    let typ;
    if (method.startsWith("Corvus_CodeGen")) typ = "CG";
    else if (method.startsWith("JsonataDotNet")) typ = "Native";
    else if (method.startsWith("Corvus_")) typ = "RT";
    else continue;

    rows.push({ suite, cat, typ, mean, ratio, alloc });
  }
}

// Group by suite + category.
const groups = new Map();
for (const r of rows) {
  const key = `${r.suite}|${r.cat}`;
  if (!groups.has(key)) groups.set(key, {});
  groups.get(key)[r.typ] = r;
}

// Sort keys for display.
const sortedKeys = [...groups.keys()].sort();

// Column definitions.
const cols = [
  { label: "Benchmark", width: 38, right: false },
  { label: "Native Mean", width: 16, right: true },
  { label: "RT Mean", width: 16, right: true },
  { label: "CG Mean", width: 16, right: true },
  { label: "RT/N", width: 6, right: true },
  { label: "CG/N", width: 6, right: true },
  { label: "Native Alloc", width: 13, right: true },
  { label: "RT Alloc", width: 13, right: true },
  { label: "CG Alloc", width: 13, right: true },
];

function pad(s, w, right) {
  s = String(s);
  return right ? s.padStart(w) : s.padEnd(w);
}

function formatRow(values) {
  return (
    "| " +
    values.map((v, i) => pad(v, cols[i].width, cols[i].right)).join(" | ") +
    " |"
  );
}

// Header.
console.log("");
console.log(formatRow(cols.map((c) => c.label)));
console.log(
  "| " +
    cols
      .map((c) => (c.right ? "-".repeat(c.width) + ":" : ":" + "-".repeat(c.width)))
      .join(" | ") +
    " |"
);

// Data rows.
let lastSuite = "";
for (const key of sortedKeys) {
  const [suite, cat] = key.split("|");
  if (suite !== lastSuite) {
    if (lastSuite) {
      // Empty separator row.
      console.log(formatRow(cols.map(() => "")));
    }
    lastSuite = suite;
  }

  const d = groups.get(key);
  const n = d.Native || {};
  const rt = d.RT || {};
  const cg = d.CG || {};

  const label = `${suite}/${cat}`;

  let flag = "";
  try { if (cg.ratio && parseFloat(cg.ratio) > 1.0) flag += " <<<"; } catch {}
  try { if (rt.ratio && parseFloat(rt.ratio) > 1.0) flag += " (RT>1)"; } catch {}

  console.log(
    formatRow([
      label + flag,
      n.mean || "-",
      rt.mean || "-",
      cg.mean || "-",
      rt.ratio || "-",
      cg.ratio || "-",
      n.alloc || "-",
      rt.alloc || "-",
      cg.alloc || "-",
    ])
  );
}

// Summary.
let totalCG = 0, aboveParity = 0;
for (const d of groups.values()) {
  if (d.CG && d.CG.ratio) {
    totalCG++;
    try { if (parseFloat(d.CG.ratio) > 1.0) aboveParity++; } catch {}
  }
}
console.log("");
console.log(`${groups.size} benchmarks total, ${totalCG} with CG, ${aboveParity} CG above parity.`);
