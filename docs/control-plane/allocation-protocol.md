# Control-plane allocation campaign — working protocol

**This protocol is binding for any session working on the bytes-to-bytes allocation campaign
(issue #803).** It exists to stop the recurring failures: charging ahead, anchoring on the code
being replaced, and claiming work "done" from memory instead of measuring it. The plan of record
is [`allocation-matrix.md`](allocation-matrix.md) — read it before doing anything.

## The clean-slate rule (non-negotiable)

- **Prior commits, conversation summaries, and memory are NOT evidence of completion.** They
  describe only the *current code shape*. The single proof a row is done is a **measured
  before→after number recorded in Part D of the matrix**.
- **Do not anchor on the existing code.** Derive every change from the conventions in
  `.github/skills/` and `docs/control-plane/execution-host-design.md` (§13, §13.4.1, §14.2). The
  existing code is the anti-pattern corpus being replaced — see the memory note
  *dont-anchor-on-existing-bad-code*.

## Per-row process (exact order — never skip or reorder)

1. **Ground.** Read the named skill(s) + design-doc § for the row. State the target pattern you
   derived from the conventions.
2. **Baseline.** Ensure exactly **one** end-to-end benchmark (handler → `InMemory*` store) exists
   for the row; **run it and paste the actual allocation number.** That is the "before".
3. **Ledger.** Post the ownership ledger for the whole path (every allocation: owner, lifetime,
   verdict). Never excuse an allocation as "admin-rare" / "cold" (memory: *frequency-is-not-a-licence*).
4. **STOP.** Present grounding + baseline number + ledger and **wait for explicit go-ahead** before
   changing any code.
5. **Change.** Apply the seam/layer change. Keep a `[Benchmark(Baseline = true)]` old-path arm so
   the delta is *measured*, not asserted.
6. **After.** Re-run the benchmark; paste before→after. Run the affected tests and confirm a
   warning-free build (commands below).
7. **Document.** Fill the row's Part D entry (ledger + pattern applied + before→after). If a better
   pattern emerged, **update the skill/doc** and link it. Commit only when asked.

**A row is ✅ only when it appears in Part D with numbers.** No number = not done.

## Guardrails

- **One row at a time.** Never batch rows or run ahead.
- **Stop, don't improvise.** If anything is ambiguous, blocked, or out of the row's scope, stop and
  ask.
- **Verify the matrix as you go.** Build it bottom-up by checking the doc against the actual code as
  you touch each row; correct the doc where it is wrong.

## Environment & commands

- Work only in the worktree
  `/home/mwa/src/Corvus.JsonSchema/.claude/worktrees/arazzo-workflow-engine-plan`
  (branch `worktree-arazzo-workflow-engine-plan`). Never `cd` to the repo root.
- Warning-free build gate: `dotnet build Corvus.Text.Json.slnx` → `0 Warning(s)`.
- Affected tests: `dotnet test --solution Corvus.Text.Json.slnx --filter "TestCategory!=failing&TestCategory!=outerloop&TestCategory!=integration"` (use `FullyQualifiedName~`).
- **Benchmarks and `samples/` are NOT in `Corvus.Text.Json.slnx`** — a green slnx build does not
  cover them; build/run them separately.
- Commit only when told; end commit messages with
  `Co-Authored-By: Claude Opus 4.8 (1M context) <noreply@anthropic.com>`.

## Benchmark facts (verified)

- `MemoryDiagnoser` is applied **globally** in
  `benchmarks/Corvus.Text.Json.Arazzo.Durability.Benchmarks/Program.cs`, so every benchmark there
  measures allocation.
- `BenchmarkDotNet.Artifacts/` is **gitignored** → run output is transient. **Record every
  before→after number in the matrix (Part D) and the commit message** — never rely on the artifact.

## Kickoff prompt for a fresh session

```
Work ONLY in the git worktree /home/mwa/src/Corvus.JsonSchema/.claude/worktrees/arazzo-workflow-engine-plan
(branch worktree-arazzo-workflow-engine-plan). Run all commands there; never cd to the repo root.

We are systematically working through the control-plane bytes-to-bytes allocation campaign (issue #803),
one API row at a time. Read docs/control-plane/allocation-protocol.md and docs/control-plane/allocation-matrix.md
first, and obey the protocol literally — especially the clean-slate rule and the per-row STOP gate.

Do not trust prior commits, summaries, or memory as proof a row is done; the only proof is a measured
before→after number in the matrix Part D. Do not anchor on existing code; derive from .github/skills/ and
execution-host-design.md (§13, §13.4.1, §14.2).

First action: read those two docs + .github/copilot-instructions.md, then propose which single row to start
with (expected: POST /credentials → ISourceCredentialStore.AddAsync) and STOP for my confirmation. Do not
write code yet.
```

## Cross-references

- [`allocation-matrix.md`](allocation-matrix.md) — the row-by-row plan and the Part D completion log.
- `docs/control-plane/execution-host-design.md` §13 / §13.4.1 / §14.2.
- `.github/copilot-instructions.md` and `.github/skills/` — authoritative conventions.
