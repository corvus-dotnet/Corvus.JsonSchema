"""assemble.py <dir>: the four-axis tables from measure.sh's logs in <dir>.

Writes summary.md / summary.csv (implementation x corpus: cold_ns, warm_ns, compile_ns, memory as bytes allocated
per evaluation) with the medians on top, and corvus-vs-blaze.md (the per-process warm table of the JIT row).
Rows whose measured clock overhead falls outside 11 to 18 ns are listed as suspect.
"""
import math, os, re, statistics, sys

D = sys.argv[1].rstrip('/')

def basis(name):
    d = {}
    p = os.path.join(D, name)
    if not os.path.exists(p):
        return d
    for line in open(p):
        m = re.match(r'^(\S+)\s+(\d+)\s+([\d.]+) (us|ms)\s+([\d.]+) (us|ms)\s+([\d.]+)\s*([\d.]+)?', line)
        if m:
            d[m.group(1)] = (int(m.group(2)), float(m.group(3)) * (1000 if m.group(4) == 'ms' else 1),
                             float(m.group(7)), float(m.group(8)) if m.group(8) else None)
    return d

blaze = {}
for line in open(os.path.join(D, 'blaze-compare.log')):
    m = re.match(r'^(\S+)\s+(\d+)\s+([\d.]+) (us|ms)\s', line)
    if m:
        blaze[m.group(1)] = float(m.group(3)) * (1000 if m.group(4) == 'ms' else 1)

cold = {}
for line in open(os.path.join(D, 'cold.log')):
    m = re.match(r'(\S+) base ([\d.]+) blaze ([\d.]+) blazetemplate ([\d.]+) blazecompilecmd ([\d.]+) jit ([\d.]+) jitimage ([\d.]+) r2r ([\d.]+) aot ([\d.]+) aotimage ([\d.]+) genjit ([\d.]+) genaot ([\d.]+) jitload ([\d.]*) aotload ([\d.]*) \| (.*) \| (.*) \| (.*) \| (.*)$', line)
    if not m:
        continue
    def ms_of(text, key):
        mm = re.search(key + r' ([\d.]+) ms', text)
        return float(mm.group(1)) if mm else 0.0
    f = [float(x) if x else 0.0 for x in m.groups()[1:13]]
    cold[m.group(1)] = dict(base=f[0], blaze=f[1], blazetemplate=f[2], blazecompilecmd=f[3], jit=f[4], jitimage=f[5], r2r=f[6],
                            aot=f[7], aotimage=f[8], genjit=f[9], genaot=f[10], jitload=f[11], aotload=float(m.group(14) or 0),
                            jitcompile=ms_of(m.group(15), 'compile'), aotcompile=ms_of(m.group(16), 'compile'),
                            genjitfirst=ms_of(m.group(17), 'first'), genaotfirst=ms_of(m.group(18), 'first'))

w = {k: basis(f'basis-{k}.log') for k in ('jit', 'r2r', 'aot', 'gen', 'genaot')}
corpora = sorted(k for k in cold if k in blaze and k in w['jit'])

def mem(x):
    return 'n/a' if x is None else f"{x:.0f}"

rows = []
for k in corpora:
    c = cold[k]
    rows.append(('blaze-compile', k, c['blaze'], blaze[k], max(0.0, c['blazecompilecmd'] - c['base']), None))
    rows.append(('blaze-template', k, c['blazetemplate'], blaze[k], 0.0, None))
    for name, wk, coldk, compk in (('corvus-runtime-jit', 'jit', 'jit', c['jitcompile']), ('corvus-runtime-r2r', 'r2r', 'r2r', None),
                                    ('corvus-runtime-aot', 'aot', 'aot', c['aotcompile']), ('corvus-image-jit', 'jit', 'jitimage', c['jitload']),
                                    ('corvus-image-aot', 'aot', 'aotimage', c['aotload']), ('corvus-generated-jit', 'gen', 'genjit', c['genjitfirst']),
                                    ('corvus-generated-aot', 'genaot', 'genaot', c['genaotfirst'])):
        if k in w[wk]:
            rows.append((name, k, c[coldk], w[wk][k][1], compk, w[wk][k][3]))

md = ["| implementation | corpus_name | cold_ns | warm_ns | compile_ns | memory |", "|---|---|---:|---:|---:|---:|"]
csv = ["implementation,corpus_name,cold_ns,warm_ns,compile_ns,memory_bytes_per_eval"]
for n, k, c, wv, cp, m in rows:
    cps = 'n/a' if cp is None else f"{cp * 1e6:,.0f}"
    md.append(f"| {n} | {k} | {c * 1e6:,.0f} | {wv * 1e3:,.0f} | {cps} | {mem(m)} |")
    csv.append(f"{n},{k},{c * 1e6:.0f},{wv * 1e3:.0f},{'' if cp is None else f'{cp * 1e6:.0f}'},{'' if m is None else f'{m:.0f}'}")

impls = []
for r in rows:
    if r[0] not in impls:
        impls.append(r[0])
med = statistics.median
summ = ["| implementation | cold_ns (median) | warm_ns (median) | compile_ns (median) | memory (median B/eval) | warm faster than blaze on |", "|---|---:|---:|---:|---:|---:|"]
for n in impls:
    rs = [r for r in rows if r[0] == n]
    wins = sum(1 for r in rs if r[3] < blaze[r[1]])
    comp = [r[4] for r in rs if r[4] is not None]
    mems = [r[5] for r in rs if r[5] is not None]
    summ.append(f"| {n} | {med(r[2] for r in rs) * 1e6:,.0f} | {med(r[3] for r in rs) * 1e3:,.0f} | {'n/a' if not comp else f'{med(comp) * 1e6:,.0f}'} | {'n/a' if not mems else f'{med(mems):.0f}'} | {wins} of {len(rs)} |")

suspect = []
for k, d in w.items():
    for name, (n, v, oh, al) in d.items():
        if oh < 11 or oh > 18:
            suspect.append(f"{k} {name} overhead {oh:.1f} ns")

head = ("# Corvus against Blaze: cold, warm, compile, memory\n\n"
        "cold_ns: one fresh process reading the schema, preparing it (compile, template or image load, nothing for generated code), reading the instances and validating every instance once (median of 3). "
        "warm_ns: one pass over the corpus at steady state, the sum over instances of the per-evaluation mean over 200 loops with the clock overhead subtracted, each corpus in its own process. "
        "compile_ns: the preparation step alone (Blaze: its `compile --fast --minify` command less a bare `--version`; the in-process compile call for corvus-runtime; the image load for corvus-image; the first evaluation for the generated rows). "
        "memory: bytes allocated per evaluation at steady state; not measurable for Blaze from outside (its binary carries its own allocator; its heap is flat across loops).\n\n"
        "## Medians\n\n")
open(os.path.join(D, 'summary.md'), 'w').write(head + "\n".join(summ) + ("\n\nSuspect rows (clock overhead outside 11 to 18 ns): " + "; ".join(suspect) + "\n" if suspect else "\n") + "\n## Every corpus\n\n" + "\n".join(md) + "\n")
open(os.path.join(D, 'summary.csv'), 'w').write("\n".join(csv) + "\n")

def fmt(us):
    return f"{us / 1000:.2f} ms" if us >= 1000 else f"{us:.1f} µs"
t = ["| Corpus | Instances | Corvus runtime (JIT) | Blaze | Corvus / Blaze | Corvus fastest |", "|---|---:|---:|---:|---:|:---:|"]
ratios = []
for k in corpora:
    n, o = w['jit'][k][0], w['jit'][k][1]
    r = o / blaze[k]; ratios.append(r)
    t.append(f"| {k} | {n} | {fmt(o)} | {fmt(blaze[k])} | {r:.2f} | {'x' if r < 1 else ''} |")
gm = math.exp(sum(map(math.log, ratios)) / len(ratios))
open(os.path.join(D, 'corvus-vs-blaze.md'), 'w').write("# Corvus runtime evaluator (JIT) against Blaze, warm, one process per corpus\n\n" + "\n".join(t) + f"\n\nCorvus is fastest on {sum(1 for r in ratios if r < 1)} of {len(ratios)} corpora. Geometric mean of Corvus over Blaze: {gm:.2f}.\n")
print("\n".join(summ))
if suspect:
    print("suspect:", "; ".join(suspect))
