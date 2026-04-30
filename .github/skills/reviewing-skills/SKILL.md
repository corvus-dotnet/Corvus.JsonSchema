---
name: reviewing-skills
description: >
  Review and update copilot instructions and skill files after completing a piece of
  work. Covers when to review, what to check, common failure modes, and the verification
  process. USE FOR: post-work skill review, periodic instruction audits, adding new skills,
  fixing skill drift after codebase changes. DO NOT USE FOR: writing library code
  (use the relevant domain skill), verifying documentation code samples
  (use the code sample verification section in copilot-instructions.md).
---

# Reviewing and Updating Skills

## When to review

Review the relevant skills and instructions after any work that changes:

- **Public API surface** — new methods, renamed parameters, changed signatures
- **Build or test infrastructure** — new solution files, changed filters, new TFMs
- **Architecture** — new projects, moved files, changed conventions
- **Code generation** — new keywords, changed handler behaviour, new CLI options
- **Documentation pipeline** — new build steps, changed tool behaviour

The rule is simple: if the code changed, check whether the instructions still match.

## What to check

For each skill touched by your changes, verify these in order:

### 1. Code examples compile and are accurate

Every code block in a skill should use real types, real method signatures, and real parameter names from the current codebase. The most common failure mode is **fabricated or stale API usage** — a method that was renamed, a parameter that was added, or a constructor whose signature changed.

To verify, pick each code example and check the actual source:

```powershell
# Find the real signature
grep -rn "public.*MethodName" src/
```

Common drift patterns:
- Constructor gains a new required parameter (skill example silently omits it)
- Method renamed but skill still uses the old name
- Enum values or constants changed
- File paths moved (e.g., `Common/src/` vs `src/Common/`)

### 2. Numeric values and thresholds match source

When a skill states a specific number (priority value, buffer threshold, step count, character limit), verify it against the source constant or definition. These drift silently when someone changes a constant without updating the documentation.

### 3. Scope boundaries are still correct

Check the `USE FOR` / `DO NOT USE FOR` fields in the YAML frontmatter. After adding a new feature or project, an existing skill's scope may need updating — either to include the new area or to explicitly redirect to a new skill.

### 4. Cross-references point to the right places

Each skill's `## Cross-References` section should link to skills that still exist and still cover the referenced topic. If a skill was renamed, split, or merged, update all inbound references.

### 5. No duplication with main instructions

The main `copilot-instructions.md` should contain brief summaries with cross-references to skills for depth. If you find the same detailed content in both places, condense the main instructions copy to a summary and cross-reference.

## Adding a new skill

When a new area of the codebase deserves its own skill:

1. **Create the directory and file:** `.github/skills/<name>/SKILL.md`
2. **Write the YAML frontmatter** with `name`, `description`, `USE FOR`, and `DO NOT USE FOR`
3. **Structure the content:** overview → code examples → configuration tables → common pitfalls → cross-references
4. **Add cross-references** from related skills back to the new one
5. **Update the skill inventory table** in `copilot-instructions.md`
6. **Update the code sample catalog:**
   ```powershell
   .\docs\update-code-sample-catalog.ps1 -UpdateFile .github/skills/<name>/SKILL.md
   ```

A skill earns its place when the assistant repeatedly struggles with a specific area, not when an area merely exists.

## Root-causing trigger failures

When a skill or instruction existed but you did not follow it, treat that as a defect in the instructions — not just a one-off mistake. The instruction failed to trigger at the right moment, and the fix is to understand why and close the gap.

Ask these questions:

1. **Was the instruction framed for the wrong trigger?** An instruction that says "when you edit a documentation file" may not fire when the task is "fix the CI build." If the instruction applies in both situations, rewrite it to cover both entry points.
2. **Was the instruction buried inside a larger workflow?** A critical step hidden as step 5 of a 5-step process gets skipped when you jump to the end. Promote it to a standalone gate with its own heading.
3. **Did the instruction assume proactive compliance?** Instructions that only describe the happy path ("do X when you change Y") need a reactive counterpart ("if you forgot to do X, here is how to recover — and it still requires doing X").
4. **Did multi-turn conversation obscure the trigger?** When changes accumulate across many conversation turns before a commit, per-edit triggers get lost. Anchor the instruction to the commit point instead, since that is where all changes converge regardless of how they were made.

After root-causing, update the instruction to close the gap — then verify the updated instruction would have caught the original failure.

## Running a full review

For a periodic audit of all skills (e.g., after a major release):

1. **List all skills** and check each against the current source
2. **Use parallel explore agents** to verify 4-5 skills each — look for fabricated APIs, wrong parameters, stale paths, missing examples
3. **Cross-check findings against source code** before fixing — review agents produce false positives (they may flag correct code as wrong)
4. **Track findings** in a structured format (SQL table or similar) with severity and status
5. **Fix in priority order:** critical inaccuracies → missing examples → duplication → cosmetic

### Common false positives

Review agents frequently flag things that are actually correct:
- Generated type APIs that don't appear in hand-written source (they're emitted by the code generator)
- Methods found only via generic type inference (the agent's grep misses them)
- Shared source files in `src-v4/` that are referenced by V5 projects via project references

Always verify against the actual source before changing a skill.

## Design principles for skill content

These principles keep skills effective as AI context:

- **Code-first** — every skill should have at least one copy-paste-ready code example with real syntax
- **Concrete over abstract** — real file paths, real type names, real method signatures
- **Self-contained sections** — each section should make sense without reading the whole skill
- **Tables for reference data** — configuration options, threshold values, priority levels
- **Pitfalls earn their place** — only document pitfalls that have actually caused problems

## Cross-References
- For verifying code samples in documentation, see the "Documentation Code Sample Verification" section in `copilot-instructions.md`
- For the code sample catalog tools, see `docs/CodeSampleCatalog.md`
