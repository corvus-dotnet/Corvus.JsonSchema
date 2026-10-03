# Optimizations: sweep of the C#, Rust and TypeScript evaluators

Every performance technique of the other Corvus evaluators (the C# V5 runtime evaluator's fused plans, the Rust
crate's plans and matchers, the TypeScript code generator), checked against this port. Status: **done**, **todo**
(in the order they will be taken), or **n/a** with the reason. The source inventories list file and line for each
technique; this is the checklist.

## Done

- **Instance representation.** A flat tape (two longs per value, kind as the type bits, children consecutive, object
  pairs adjacent); strings read in place when unescaped; SWAR string scan; numbers classified at parse (long, unsigned
  long, double) with the text kept for exact decimals; iterative parser; duplicate-key tiers; exactly sized arrays.
- **Type tests.** One AND against the kind bits; the integer test only for integer-without-number; type-only children
  tested at the call site (no call); a type mask that excludes a kind skips that kind's keywords.
- **References.** Pure-`$ref` chains elided and one-branch `allOf` forwarded (fail-fast targets precomputed), not across
  resources under a dynamic scope; collecting-mode elision with the path suffix; static `$dynamicRef`/`$recursiveRef`
  resolution with single-candidate and uniform-entry demotion; one node per schema value; legacy `$ref` overrides its
  siblings.
- **Objects.** One pass over the properties; declared names by linear compare (up to 8) or a byte-keyed hash table;
  `required` of declared names as a seen mask (up to 63), the rest looked up; count bounds from the header, only when
  present; small objects probed by name, required first; `additionalProperties` and `propertyNames` skipped when
  `true`; a `true` pattern property skipped unless additionalProperties needs the match; dependencies guarded by
  presence.
- **Flat composition.** A `$ref`/`allOf` chain of plain object schemas checks an object in one merged pass.
- **Arrays.** Length read once; `prefixItems` guarded by length and skipped when `true`; `items: false` as a length
  check and `items: true` as nothing; `contains` skipped when it cannot fail and stopped at `minContains` when there is
  no maximum; `uniqueItems` only above one item, pairwise up to 16, otherwise sorted by hash in reused scratch.
- **Strings.** Code points counted only for non-ASCII strings; patterns compiled once per process; pattern shapes
  decided without a regex engine over the UTF-8 bytes: everything (`.*` …), has-content (`.`, `.+`), line length
  (`^.{m,n}$`), `^X.*` reduced to `^X`, anchored literals and anchored class sequences (greedy where exact); everything
  else translated to `java.util.regex` with a matcher reused per evaluator.
- **Numbers.** Exact comparison across long, unsigned long and double; bounds digested at compile time; integer
  `multipleOf` by `%`; other `multipleOf` exactly on the decimal digits of the text with long arithmetic.
- **Formats and content.** Format kind and assertion decided at compile time per dialect; all formats over the UTF-8
  view without allocation (hand-written dates, times, UUIDs, IP addresses, host names, IDNA with reused buffers, the
  regex format by an allocation-free reader; regex-based URI formats with reused matchers); content checked without
  decoding (base64) or with a reused validate-only parser (JSON).
- **Enum and const.** String const compared as bytes; string enums compared as bytes (up to 8) or by a hashed lookup;
  other values by JSON equality with prefilters (kind first, objects position by position).
- **Composition.** `anyOf` stops at the first match and `oneOf` at the second; discriminators (positive, negative and
  wildcard branches, all-require fast fail) narrow both; `if` without `then`/`else` dropped; `not` fail-fast.
- **unevaluated\*.** Marking analysis; bits only where consumed and scratch only for children that can mark; bit sets
  in an arena (no allocation).
- **Allocation.** Zero allocation on the validation path, every keyword, compiled and interpreted, gated by a test that
  also runs without escape analysis.
- **Compile time and start-up.** Analyses gated on presence; iterative Tarjan with the depth guard only on cycle nodes;
  lazy annotations; schemas compiled to bytecode (one method per node, constants as class data); an ahead-of-time cache
  (JDK 25) in the benchmark image, trained on the metaschemas.

## Todo

1. **Merge identical methods** (TypeScript's partition refinement; C#'s canonical equivalent subschemas): fewer methods
   for the JIT to compile, so large schemas reach steady state sooner and compile faster.
2. **Pattern shapes** not yet ported: whole literal sets `^(a|b)$`; expanded alternatives (groups multiplied out, with
   start/end/anywhere sequences); separated lists `^I(SR)*$`; the excluded-class-with-word lookahead form (cspell);
   unanchored sequences searched from their first atom; free `.*` stripping.
3. **Name dispatch**: switch on the name's length, then word compares (C#'s `Utf8NameMap`: length, distinguishing byte,
   overlapping word loads), in place of the linear byte compare and the general hash.
4. **Type dispatch and type union** for `anyOf`/`oneOf` whose branches assert disjoint types; small const/enum leaves
   tested at the call site.
5. **Simple arrays**: items that are leaves or type-only checked in the array's own loop; nested simple arrays inline
   (geojson).
6. **Discriminators by hashed lookup** for many string values, and the discriminator property found in the one pass.
7. **String length bounds from the byte length** before counting code points.
8. **uniqueItems over strings**: pairwise by length up to 32.
9. **Static unevaluated coverage** (TypeScript's `staticCoverage`/`guardedCoverage`): `unevaluated*` decided at compile
   time where every contributor is unconditional, so those nodes compile instead of interpreting (openapi).
10. **Fused object plans** (C#/Rust): one pass deciding `if` conditions, required-only alternatives, alternative groups
    of object branches, `not: {required}`, and `unevaluatedProperties` from seen bits.
11. **Allocation-free `isValid(byte[])`/`isValid(String)`**: parse into reused buffers (the Rust crate's
    `validate_json`).
12. **Collecting mode without allocation** (C#: static lambdas over a context struct, pooled collector).
13. **Compiled dynamic scope and in-place cycles** (TypeScript's scope wrapper and switch resolver, depth wrapper), so no
    schema runs on the interpreter.

## Not applicable

- **Generic specialisation over a document-access type** (C#) and **monomorphised Fast/Collect** (Rust): the compiled
  methods are already specialised per schema; the interpreter serves collecting mode.
- **Stack allocation, SkipLocalsInit, ArrayPool, inline/no-inline attributes** (C#/Rust): the JVM has no equivalents;
  the arena and per-evaluator scratch play their part, and the JIT decides inlining.
- **Program image, ReadyToRun, MIBC profiles** (C#): the AOT cache is the JVM's counterpart.
- **Linear-time `regex` crate translation and `regress`** (Rust), **compiled IL regex** (C#): `java.util.regex` is the
  JVM's engine; the shapes avoid it where they can.
- **Vectorised search start** (C#'s `IndexOfAny`): no portable vector API on Java 17.
