#!/usr/bin/env python3
"""Runs Blaze (via the Sourcemeta `jsonschema` CLI) over the Sourcemeta corpora and compares it with a `quick` run.

Usage:
  blaze-compare.py --cli <path to jsonschema binary> --corpus <dir with *-schema.json and *-instances.jsonl>
                   [--quick <quick-run or blazebasis log>] [--loop 20] [--pin 0-11] [corpus ...]

For a like-for-like comparison use the harness's `blazebasis <loop>` command, which times every instance the way
the CLI does (per evaluation, clock overhead subtracted, mean over the loop, after a JIT warm-up), and pass its
output as --quick.

The CLI's `validate --benchmark --loop N --fast` prints, per instance, the mean and standard deviation of the
evaluation time over N loops (parsing excluded). Summing the means over a corpus gives the time to evaluate every
instance once, which is what the harness's `quick` runtime column measures (minimum over rounds rather than mean).
Format assertion is off on both sides. The `jsonschema` binary is a GitHub release of sourcemeta/jsonschema, e.g.
jsonschema-<version>-linux-x86_64.zip; it embeds Blaze.
"""
import argparse, os, re, subprocess, sys, math

def us(text):
    v, u = text.split()
    return float(v) * {"ns": 0.001, "us": 1.0, "ms": 1000.0, "s": 1_000_000.0}[u]

def fmt(v):
    return f"{v/1000:.2f} ms" if v >= 1000 else f"{v:.2f} us"

def blaze_total(cli, schema, instances, loop, pin):
    cmd = [cli, "validate", schema, instances, "--benchmark", "--loop", str(loop), "--fast"]
    if pin:
        cmd = ["taskset", "-c", pin] + cmd
    out = subprocess.run(cmd, capture_output=True, text=True)
    total = 0.0
    count = 0
    for line in out.stdout.splitlines() + out.stderr.splitlines():
        m = re.search(r"\]: (PASS|FAIL) ([\d.]+) \+- ([\d.]+) (ns|us|ms|s)", line)
        if m:
            total += us(f"{m.group(2)} {m.group(4)}")
            count += 1
    if count == 0:
        raise RuntimeError(f"no benchmark lines for {schema}: {out.stderr[:300]}")
    return total, count

def quick_runtimes(path):
    """Reads either a `quick` log (generated, runtime, ratio) or a `blazebasis` log (instances, sum of means, ...)."""
    rows = {}
    for line in open(path):
        m = re.match(r"^(\S[\w-]*)\s+([\d.]+ (?:us|ms|ns))\s+([\d.]+ (?:us|ms|ns))\s+([\d.]+)\s*$", line)
        if m:
            rows[m.group(1)] = (us(m.group(2)), us(m.group(3)))
            continue
        m = re.match(r"^(\S[\w-]*)\s+(\d+)\s+([\d.]+ (?:us|ms))\s+([\d.]+ (?:us|ms))\s+[\d.]+\s*$", line)
        if m:
            rows[m.group(1)] = (float("nan"), us(m.group(3)))
    return rows

def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("--cli", required=True)
    ap.add_argument("--corpus", required=True)
    ap.add_argument("--quick")
    ap.add_argument("--loop", type=int, default=20)
    ap.add_argument("--pin", default="")
    ap.add_argument("names", nargs="*")
    a = ap.parse_args()
    names = a.names or sorted(f[:-len("-schema.json")] for f in os.listdir(a.corpus) if f.endswith("-schema.json"))
    quick = quick_runtimes(a.quick) if a.quick else {}
    print(f"{'corpus':24} {'instances':>9} {'blaze':>12} {'corvus rt':>12} {'corvus/blaze':>13} {'generated':>12} {'gen/blaze':>10}")
    logs = []
    for name in names:
        schema = os.path.join(a.corpus, f"{name}-schema.json")
        instances = os.path.join(a.corpus, f"{name}-instances.jsonl")
        if not os.path.exists(instances):
            continue
        total, count = blaze_total(a.cli, schema, instances, a.loop, a.pin)
        q = quick.get(name)
        if q:
            gen, rt = q
            logs.append(math.log(rt / total))
            gen_text = "" if math.isnan(gen) else f"{fmt(gen):>12} {gen/total:>9.2f}x"
            print(f"{name:24} {count:>9} {fmt(total):>12} {fmt(rt):>12} {rt/total:>12.2f}x {gen_text}")
        else:
            print(f"{name:24} {count:>9} {fmt(total):>12}")
    if logs:
        print(f"geometric mean corvus runtime / blaze: {math.exp(sum(logs)/len(logs)):.2f}x over {len(logs)} corpora")

if __name__ == "__main__":
    main()
