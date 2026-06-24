// Tiny runtime-behaviour test harness for the output-shape reference models. Each suite is
// self-contained (own counters); `done()` prints a tally and throws on failure (node exits non-zero).
//
// Type-check (the contract gate):  npx -y -p typescript tsc -p tsconfig.json
// Run the behaviour suites:        npx -y -p typescript tsc -p tsconfig.run.json
//                                  for s in access conversions matching mutations; do node dist/$s.test.js; done
export function suite(name: string) {
  let pass = 0;
  let fail = 0;
  const fails: string[] = [];
  return {
    eq<T>(label: string, got: T, want: T): void {
      if (JSON.stringify(got) === JSON.stringify(want)) { pass++; }
      else { fail++; fails.push(`${label}: got ${JSON.stringify(got)} want ${JSON.stringify(want)}`); }
    },
    ok(label: string, cond: boolean): void {
      if (cond) { pass++; } else { fail++; fails.push(label); }
    },
    throws(label: string, fn: () => unknown): void {
      try { fn(); fail++; fails.push(`${label}: expected throw`); } catch { pass++; }
    },
    done(): void {
      console.log(`${name}: ${pass} passed, ${fail} failed`);
      for (const f of fails) { console.log("  FAIL " + f); }
      if (fail > 0) { throw new Error(`${name}: ${fail} failed`); }
    },
  };
}
