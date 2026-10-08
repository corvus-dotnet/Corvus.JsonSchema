# Optimizations: sweep of the C#, Rust and Java evaluators

Every performance technique of the other Corvus evaluators, checked against this port by reading its source. The
inventories are the C# V5 runtime evaluator (`src/Corvus.Text.Json/Corvus/Text/Json/RuntimeEvaluator`: the plans of
`SchemaNode.cs`, `SchemaCompiler.cs`, `FusedObjects.cs` and `Evaluator.cs`, and the code generator's
`CodeGeneration/OPTIMIZATIONS.md`), the Rust crate (`src-rs/corvus-json-schema`, `eval/plan.rs` and
`eval/plan/fused.rs`) and the Java port (`src-java/corvus-json-schema/OPTIMIZATIONS.md`).

Status is **done** (with where it is in the Go source), **partial** (with what is missing), **todo** (in the order
they should be taken) or **n/a** with the reason. The todo order is the order in which the techniques paid in the
other ports, and each one has to be measured here before it is kept. What was measured, kept or not, is under
"Measured" and "Tried and not kept" at the end.

Keep this file current. A technique added to any of the other evaluators should be checked off here, or ruled out
with the reason.

## Done

- **Instance representation** (`document.go`). A flat tape, two words per value: the kind in the low byte (the kinds
  are the evaluator's type bits), flags, and a 32-bit count or length in the header, the data in the second word.
  A container's children are consecutive, and an object's are name and value pairs. Strings are read in place where
  they have no escapes (`strText` marks the others, which are unescaped into one buffer), and the parser records
  whether a string is all ASCII (`strWide`). Strings are scanned eight bytes at a time (`parser.str`). Numbers are
  classified at parse as an int64, a uint64 beyond an int64, or a float64 (`numInt`, `numUint`, `numFloat`), and the
  header keeps the offset of the text for exact decimals. The parser is iterative, with its open containers on its
  own stack (`parser.parse`). Duplicate names in a large object are found through scratch (`parser.dedupe`).
  `ParseDocument` sizes the document exactly (`parseNew`), and the validator's text entry points parse into reused
  arrays (`parseInto`).
- **Number conversion.** Clinger's fast path for a mantissa and a power of ten that a float64 holds exactly, then
  `strconv.ParseFloat` over the text without copying it (`parser.number`). `strconv` has the Eisel-Lemire algorithm
  that the Java port wrote for itself.
- **Type tests.** One mask test against the kind (`typeOK`, `eval.go`). The integer test runs only for integer
  without number (`typeOK`, `allOfType`). A child that only tests the type is tested at the call site and never
  entered (`child.types` with `shapeTrivial`, `runChild`, `runBranch`). An `anyOf` of type-only branches, or a
  `oneOf` of type-only branches with no type in common, becomes part of the node's own type mask (`typeUnion`,
  `meetTypes`: the C# `TypeUnion` plan).
- **References.** Pure `$ref` chains are elided and a node that is only a one-branch `allOf` forwards to the branch
  (`newProgram` fills `fastTarget` through `pureRefTarget` and `forwardTarget`: the C# `Forward` plan), up to 16
  hops, not across resources under a dynamic scope, and keeping the guard of a node on a cycle. Collecting mode elides
  the same hops with their path suffix (`evaluator.resolve`, `collectPureRef`). A `$dynamicRef` or `$recursiveRef`
  with one candidate, one that is unreachable from the entry, or one the entry resource always answers is resolved at
  compile time (`finalizeDynamicRefs` sets `staticDynamicRef`). A program with no dynamic scope takes the fallback of
  every dynamic reference (`planNode`). In draft 7 and earlier `$ref` replaces its siblings (`compileNode`).
- **Plan shapes** (`shape`, `shapeOf`, `enterChild` in `plan.go`). A caller enters a child by what its keywords come
  to: nothing but the type, a leaf, a string enum, string keywords, an object plan, an array plan, in-place
  applicators, or a fused object plan. Every child takes the final shape of its node once the fused plans are built
  (`compilePlans`), and a node entered by its id is entered as a child (`plan.self`, `run`). These are the C# `Leaf`, `Object`, `StrictObject`, `SimpleArray`, `ArrayItems`, `Composite` and
  `Conditional` plans in the form the Rust port gave them. Keywords are grouped by the kind of value they apply to,
  so only those for the instance's kind are looked at (`body`, `runKeywords`).
- **Objects** (`objectPlan`, `runObject`). One pass over the properties, specialised by which keywords apply
  (`visit`): a map of values against one child, with a tight loop when that child is a type (`visitValues`),
  declared names (`visitNames`), one pattern and nothing declared (`visitPattern`), or the general loop
  (`visitGeneral`). `required` is a mask of the names seen, over at most 64 names, which include the names only
  `required` or a dependency mentions (`planObject`, `requiredMask`). Dependencies are decided from the same bits
  (`planDependency.hasBits`). Count bounds come from the header. The patterns a declared name matches are worked out
  at compile time, so only undeclared names are matched at run time (`namePatterns`). `propertyNames` is evaluated
  on the name where it lies in the document, with no document made for it (`visitGeneral`).
- **The strict object loop** (`runStrictObject`). Declared names, `additionalProperties` for the rest and the
  required mask, as a function of its own that a nested object enters directly (`shapeObject` in `enter`).
- **Small objects probed by name** (`visitLookup`). A plan of at most 4 names and no `additionalProperties` looks
  each name up in the instance when the instance's properties times the names is at most 24, comparing names by
  length and word before text, over the properties as one slice of the tape.
- **Name lookup** (`names.go`, the C# `Utf8NameMap`). A set of the lengths the names have settles most misses with
  one test (`names.lengths`). A name's key is its length, a word and, beyond eight bytes, its last eight bytes
  (`nameKey`, `nameWord`), which is the whole name up to sixteen bytes, so only a longer name has its text compared.
  The loops try the name after the previous match first, in the loop itself (`names.at`), then the next name in
  sorted order, since instances tend to list their properties in the schema's order or sorted (`findAfter`), then a
  hash table from the key to the index (`nameMap.findKey`). The table has four to eight slots for each name and its
  multiplier is the one of 24 that leaves the fewest names away from their first slot, so a search is one
  multiplication and one read to the index.
- **Flat composition** (`fusedObject.flat`). A `$ref` and `allOf` chain of plain object schemas that apply
  unconditionally merges into one strict object loop.
- **Fused object plans** (`fused.go`, the C# `FusedObject` plan). One pass over an object for a schema whose object
  semantics are spread over `$ref`, `allOf`, `if`/`then`/`else`, dependencies and `oneOf`/`anyOf`. Every name any
  branch knows resolves at compile time to the children that apply to it (`fusedEntry`), with identical resolutions
  from several branches applied once (`coalesceApps`). Conditions are decided from the names seen and from tests on
  property values, whose constants are merged into one lookup per property (`mergedTests`). Applications under a
  condition are deferred to a second step over only the properties that have one (`runFusedPass`). Required-only
  alternatives (`fusedAlternative`), alternative groups of object branches (`fusedAltGroup`), `not: {required}`
  (`fusedForbidden`) and conditions on names no property may match (`fusedAbsent`) are decided from the same pass.
  Below a live dynamic reference only contributors in the node's own resource fuse (`reachesDynamicReference`).
- **`unevaluatedProperties` from the fused pass.** The pass tracks which properties some branch covered, and the
  rest go to the unevaluated schema (`fusedObject.hasUnevaluated`).
- **`unevaluatedItems` from static coverage** (`staticItemCoverage`). When every contribution to the evaluated items
  is unconditional, the items after the longest prefix are checked against the schema with no tracking.
- **Evaluated properties and items elsewhere.** The marking analysis (`computeMarking`) says which nodes can mark.
  Only a child that can mark gets a set of its own (`canMark`). Sets live in the evaluator's arena (`newBits`). A
  node the general evaluator runs evaluates only its own location: each value below it goes back to its plan
  (`e.run(e.p.fastTarget[...])` in `eval.go`).
- **Arrays** (`runArray`). The length is read once. `prefixItems` is guarded by the length. An array of type-only
  items is one loop over the tape (`isSimple`, `allOfType`), and so are the items after a prefix. Items that are
  themselves simple arrays are checked inline without being entered (`hasNested`, GeoJSON's positions). `contains`
  stops at `minContains` when there is no maximum.
- **`uniqueItems`** (`allUnique`, `values.go`). Nothing for fewer than two items. Up to 32 strings pairwise, lengths
  before bytes. Up to 16 other values pairwise. Longer arrays sorted by hash in reused scratch, comparing only the
  items whose hashes are equal. Two objects that list their members in the same order are compared position by
  position, and values of different counts are unequal without a comparison (`valuesEqual`).
- **String lengths** (`lengthOK`). Exact from the byte length for a string the parser marked ASCII. Otherwise decided
  from the byte length where it can be, and code points counted only in the band it does not decide.
- **Pattern shapes** (`pattern.go`). Patterns every string matches, `.+`, line lengths (`^.{m,n}$`), `^X.*` reduced
  to `^X`, anchored literals, whole literal sets, anchored class sequences matched greedily where that is exact and
  by length where one item is variable (`matchPinned`), alternatives of literals and sequences once groups are
  multiplied out, separated lists, and the excluded class with a word lookahead. A sequence has a byte-per-character
  path for a string the document knows is ASCII (`matchASCII`). Patterns compile once per process (`patternCache`).
- **The regular expression engine** (`internal/ecmaregex`). A pattern in the subset that RE2 reads the same way is
  translated to the standard library's `regexp` and matched in linear time. Such a pattern with no word boundary
  also gets a deterministic automaton over ASCII text (`dfa.go`), of at most 256 states, built on the first match:
  an ASCII text is then decided with one table read per byte, and `regexp` only sees the texts that are not ASCII.
  Anything else runs on a backtracking matcher whose stack is explicit and pooled, so a match allocates nothing
  once it has grown.
- **Numbers** (`numbers.go`). Exact comparison across int64, uint64 and float64 (`compareNumbers`). Bounds are
  digested at compile time into the same representation (`numberOp`). An integer `multipleOf` of an integer is `%`.
  Any other `multipleOf` is decided on the decimal digits of the text with integer arithmetic (`dividesText`).
- **Formats and content.** The format kind and whether it asserts are decided at compile time for the node's dialect
  (`formatCheckFor`). Formats read the UTF-8 bytes. The URI, IRI, URI template and e-mail local part formats use the
  standard library's linear-time `regexp`, compiled once. Content that is JSON is checked by a reused parser that
  builds nothing (`parser.isValid`).
- **Enum and const.** An enum of strings is a name lookup (`opEnumStrings`), decided where the child is entered when
  that is all the child is (`shapeStringEnum`). Other values compare by JSON equality with the kind first and string
  lengths before bytes (`valuesEqual`).
- **Composition.** `anyOf` stops at the first match and `oneOf` at the second. Only the branches whose types admit
  the instance's kind are tried (`branches.byKind`: the C# `TypeDispatch` plan), and a `oneOf` that leaves one
  candidate is that branch. Discriminators narrow both, with string values through a name lookup and a fast failure
  when every branch requires the property (`discriminatorIndex`, `candidates`). `if` without `then` or `else` is
  dropped. `not` fails fast.
- **Dynamic scope and cycles.** A resource is pushed only in a program that keeps a dynamic scope, and only when it
  is not the innermost already (`runBody`, `pushScope`). The depth guard applies only to nodes on an in-place cycle
  (`plan.guard`, `runInPlace`).
- **Allocation.** The evaluator is a value on the stack of the call that validates. Its buffers are a scratch taken
  from the validator's pool only when first needed (`evaluator.state`), so most schemas touch no pool. The state of
  a fused pass is on the stack too (`runFused`), and a pass takes the scratch only to track covered properties or
  for an object of more than 64 properties. JSON text is
  parsed into a pooled document, and a string is read without a copy (`stringBytes`).
  `TestValidationAllocatesNothingInTheSteadyState` holds the keyword paths it lists to zero allocations.
- **Inlining and bounds checks.** The Go compiler inlines a function whose cost is at most 80 on its own scale and
  says what it did with `-gcflags=-m`. The type test (`typeOK`), the application of a type-only child (`runChild`)
  and the test of the expected name (`names.at`) are under that and inline into the loops. `enterChild`, `findAfter`
  and `names.find` are the calls, each with what it needs written out in it, so that a value costs one call and not
  a chain of them. A value's header is read once where the value is entered. `visitLookup`, `visitValues` and
  `allOfType` run over a slice of the tape, with no bounds check per value. See Measured for what each was worth,
  and Tried and not kept for the same ideas where they did not pay.
- **Compile time.** Analyses run only when the schema has what they analyse (`analyse`). In-place cycles are found by
  an iterative Tarjan (`computeInPlaceCycles`). Annotation keywords are digested on the first evaluation with a
  collector (`nodeAnnotations`). The metaschemas are embedded and read on demand.

## Partial

- **Leaves decided in the loop.** The C# strict loop decides a string enum, a string const, a string length and a
  nested strict object for a property without leaving the loop (`StrictEntry`), the C# code generator emits leaf
  children in the loop, and the TypeScript generator inlines const and enum leaves. Here only a type-only child is
  decided in the loop. Any other child is one call to `enterChild`, and a leaf then runs `runLeaf`, which loops over
  its operations. Missing: const, string enum and length tests in `visitNames`, `visitLookup`, the fused entry and
  the array loops, without the calls.
- **`true` subschemas skipped.** The fused plan treats a `true` child as cover with no test (`optChild`). The object
  plan does not: `additionalProperties: true` stays a child that accepts everything, which costs a call per
  undeclared property and rules out the probe by name (`objectPlan.lookup`), `propertyNames: true` selects the
  general loop, and a `true` pattern property is still matched. The Java port drops each of these.
- **Keywords that cannot apply under the node's type.** Object and array keywords are dropped when `type` excludes
  objects or arrays (`planNode`). Number and string keywords are kept whatever the type, so such a node is a leaf
  where it could be a type test.
- **Enums that are not all strings.** An enum with one value that is not a string (`["a", "b", null]`) is compared
  value by value (`opEnum`, `enumContains`). The Java port hashes larger sets, and the strings of a mixed set could
  still take the name lookup.
- **Searching for an unanchored match.** An unanchored literal is searched for a byte at a time (`indexString`), and
  an unanchored class sequence is tried at every position (`altAnywhere`). The C# matcher skips the positions its
  first atom rejects and uses a vectorised search. `bytes.Index` is the standard library's.
- **Content.** Base64 content is decoded into scratch even when only its validity is asked (`contentOK`). The Java
  port checks it without decoding.
- **Allocation outside the common paths.** A `multipleOf` whose divisor has more than 18 significant digits, or an
  exponent out of range, is decided with `math/big`, which allocates (`divisor.divides`). A custom format receives a
  copy of the string (`formatCheck.checkString`).

## Todo

1. **Leaves decided in the loop** (see Partial). Not measured yet. What was measured says how not to do it: every
   attempt that put more code in the body of `visitNames` made every property dearer (see Tried and not kept, 5 and
   3), because the loop has more live values than the compiler has registers once it holds a call. A leaf test
   would have to be reached without growing that loop, for instance from `enterChild`.
2. **`true` subschemas dropped from the object plan** (see Partial).
3. **Equivalent subschemas canonicalised.** The C# compiler points fail-fast references at one representative of each
   set of identical subschemas (`CanonicalizeEquivalentNodes`), and the Java port merges structurally identical nodes.
   A chain of conditionals that restates a property's schema at every level then applies one node, which a fused
   object tests once. There is no counterpart here: `getNode` makes one node per schema location.
4. **Unanchored search** with `bytes.Index` and a first-atom skip (see Partial).
5. **Mixed and large enums** (see Partial).
6. **Keywords pruned by type** for numbers and strings (see Partial).
7. **Collecting mode without allocation.** The C# collector is pooled and writes its paths into reused buffers. Here
   a collector reuses its row slices after `Reset`, but each row's locations and message are strings built for it.
   The Java port lists the same item.
8. **Base64 validity without decoding, and `multipleOf` beyond 18 digits without `math/big`** (see Partial).
9. **The fused pass.** It is the largest part of the corpora with conditionals (ui5, jsconfig, openapi,
   ansible-meta) and is still a set of small loops over contributors, applications and tests for each property.
   The C# and Rust plans have the same shape, so there is nothing to port. It would have to be measured line by
   line as the property loop was.

## Not applicable

- **Runtime code generation** (the C# IL emitter, the Java bytecode generator, the TypeScript source generator), and
  everything that belongs to it: one method per node, constants as data of the generated type, an entry method per
  schema, inlining thresholds, name dispatch as generated comparisons or a trie of words, a search on a
  distinguishing window of bits, merged methods, the method size guard, the JVM's 8000-byte limit, and the
  experiments on locals and switches in the C# list. Go has no JIT and no portable way to load code at run time. The
  plans here are data that one evaluator interprets, as in the Rust crate.
- **A program image, ReadyToRun and tiered compilation** (C#), **an ahead-of-time cache** (Java). Go compiles ahead
  of time. Profile-guided optimisation is the nearest counterpart (Todo 6).
- **Generic specialisation over a document type** (C#), **the `Instance` trait and monomorphised fail-fast and
  collecting evaluators** (Rust). Instances are always `Document` values. Fail-fast evaluation runs the plans, and
  the general evaluator serves collecting.
- **One row on after a scalar, integers read in one pass, integer bounds and consts with no conversion** (the C# code
  generator). They work around metadata rows of varying size and numbers kept as text. Here every value is two
  words, a container's children are consecutive, and a number's value is in the tape.
- **Stack allocation, `SkipLocalsInit`, `ArrayPool` and thread-static buffers** (C#), **per-thread buffers** (Rust
  and Java). Go has no thread-local storage. The evaluator stays on the stack by escape analysis, and the scratch
  comes from a `sync.Pool`.
- **Translation to `java.util.regex` with reused matchers** (Java), **the `regex` and `regress` crates** (Rust),
  **compiled IL regular expressions** (C#). `internal/ecmaregex` is this port's engine.
- **A vectorised search API** (`SearchValues` and `IndexOfAny` in C#). The standard library has no portable SIMD
  API. `bytes.IndexByte` and `bytes.Index` are vectorised in the runtime, and Todo 4 uses them.
- **Release profile settings** (the Rust crate's `lto` and `codegen-units`). The Go compiler has no counterpart.

## Measured

Warm validation over the 37 jsonschema-benchmark corpora, 2026-10-08, Go 1.27.1, linux/amd64. Every figure is an A/B
of two builds of the benchmark's protocol program, run alternately five times each, pinned to eight cores, comparing
the median pass of the warm-up loop. The figure is the time of the change over the time before it, as a geometric
mean over the corpora, with the range over the corpora.

Two measurements say what such a figure can tell. A build measured against itself gave 1.000, with corpora between
0.963 and 1.024. A build measured against the same source with one unused function added, which only moves the
code, gave 1.004, with corpora between 0.950 and 1.048. So a corpus inside 0.95 to 1.05 did not move, and a
geometric mean inside 0.995 to 1.005 is nothing. One corpus (babelrc) has two states about 7 percent apart that
code layout chooses between (see 4).

From 6 on, a change was first measured by the instructions one validation pass executes, counted by cachegrind as
the difference between two runs with different numbers of passes, which is repeatable to 0.1 percent. On the machine
used the evaluator runs close to five instructions per cycle, and the time followed the instruction count wherever
both were measured. Hardware counters were not available (WSL2).

1. **The type test inlined** (`typeOK`, `integerOK`). The mask test was 124 on the inliner's scale, against a budget
   of 80, so every type test was a call. The integer test, which few values reach, is now the call. 0.979 (0.724 to
   1.034). The 0.724 is a map of strings (importmap).
2. **A type-only child decided where it is applied** (`runChild`, `child.pass`, `enterChild`). `runChild` was a call
   that made a second call to `enter`. It is now one test of the value's kind against `child.pass`, inlined into the
   property and item loops, and one call (`enterChild`, which has the dispatch by shape in it) for a child with
   keywords. 0.929 (0.840 to 0.999).
3. **The expected name tested in the property loops** (`names.at`, `nameMap.keys`, `findAfter`). `findFrom` cost 346,
   so every property name was a call. The test of the name after the previous match (its length and word, side by
   side in `nameMap.keys`) is now in `visitNames`, `visitGeneral` and the fused pass, and the search is the call. 0.986
   (0.938 to 1.073). The corpora above 1.03 in that run (openapi, ansible-meta, cmake-presets) were measured again
   with nine runs each at the protocol's warm-up time and gave 1.019, 1.012 and 1.022, which is inside the noise.
4. **Names found through a hash of their key** (`nameMap`). The map was by length, then by the byte that best told
   the names of a length apart, then a chain, with a call to compare the text of any name over eight bytes. It is
   now one table (see Name lookup under Done), and `findKey` calls nothing. 0.979 (0.854 to 1.082), and compile
   0.90. Three forms were measured before this one. Linear probing with the keys in the slots and two slots for each
   name, and a perfect hash by displacement (two reads to the slot), were both slower on a corpus of one or two
   properties per object (babelrc, 1.11 and 1.16 on a subset run). A likely reason, not proven: what counts there
   is the time until the index is known, since the dispatch on the child's shape waits for it, and both add a read
   or a second probe before it. The 1.082 of the kept form is on that same corpus, where twelve runs of each build put the build before the
   change between 40.4 and 43.6 microseconds a pass, this one between 44.3 and 47.7, and a build of the old code
   with nothing but an unused function added at 41.4 to 42.0 in seven runs and 44.6 to 45.6 in five. So that corpus
   has two states a pass can be in, about 7 percent apart, that code layout alone chooses between.
5. **The search of the name table written out in its two callers** (`names.findAfter`, `names.find`). A name that
   was not the expected one went through three calls (`findAfter`, `nameMap.find`, `findKey`), and so did a string
   tested against an enum. Each now has the search in it, with a call only for a name over sixteen bytes.
   0.959 (0.788 to 1.047).
6. **Values entered through one function, and a strict object's loop called from it** (`run`, `enterChild`,
   `shapeFused`, `plan.self`). The root went through `run`, `enter`, `runBody`, `runKeywords`, `runFused` and
   `runStrictObject` before its first property, and a nested object through `enterChild` and `runStrictObject`.
   `run` is now `enterChild` on the node's own child, a node with a fused plan has a shape of its own, and
   `enterChild` calls the property loop of a strict object itself. A child also now takes the shape its node has
   after the fused plans are built: before, a child whose node was only applicators kept that shape and entered them
   one by one, where the node's fused plan decides them in one pass. 0.966 (0.887 to 1.028). Instructions per pass
   over 18 corpora: 0.971.
7. **The state of a fused pass on the stack** (`runFused`). Every fused pass took its state from the scratch, and so
   the scratch from the validator's `sync.Pool`, once per validation. 0.993 over all 37, and over the corpora that
   run a fused pass 0.918 (ansible-meta) to 0.983, with nothing else moved.
8. **A value's header read once, and loops over a slice of the tape** (`enterChild`, `visitLookup`, `visitValues`).
   An object's count and first child were read again by each function it went through, each read with its bounds
   check. `enterChild` reads the header once and hands the first child and the count to the property loop. The
   search for a name in a small object and the type test of a map's values run over the properties as one slice of
   the tape, so they read with no bounds check. 0.976 (0.876 to 1.041). Instructions per pass over 18 corpora:
   0.984, and a map of strings (importmap) 0.861. The same slice in the array item loop and in `visitNames` did not
   lower the instruction count (1.004 and 1.002 over the corpora measured) and is not in.
9. **Conditions of a fused pass as masks** (`fusedApp.then`, `fusedContributor.then`, `fusedPass.applies`,
   `fusedObject.finals`). After the properties, the pass asked for every contributor and for every deferred
   application whether its condition applied, one condition at a time, through its contributors. The conditions
   that apply an application are now two masks worked out when the plan is built, the pass has the conditions that
   hold and those that do not as two words, and the question is two ANDs. Only the contributors that have required
   names or count bounds are visited after the properties. 0.991 over all 37, and over the corpora with conditional
   fused plans jsconfig 0.862, ui5 0.900, ansible-meta 0.942, openapi 0.975. Instructions per pass: ui5 0.893,
   jsconfig 0.936.
10. **An automaton over ASCII text for the patterns on `regexp`** (`internal/ecmaregex/dfa.go`). Of the patterns in
    the 37 schemas that no faster matcher takes, all but one run on `regexp` (case-insensitive words spelt as
    classes, alternatives of words after a prefix, a semantic version), and `regexp` was 17 percent of the samples
    of jsconfig and 12 of cspell. 0.980 over all 37, jsconfig 0.700, cspell 0.850, krakend 0.875, ui5 0.953.
    Instructions per pass: jsconfig 0.778, cspell 0.893, krakend 0.902. Compile time is as before, since the
    automaton is built on a pattern's first match (5 to 170 microseconds for the patterns of these schemas, and at
    most about a millisecond for a pattern that turns out too large for one). The differential test of the package
    compares the automaton, `regexp` itself and the backtracking matcher on 1500 texts for each of 1462 patterns,
    1418 of which have an automaton.

**The ten together.** The build after 10 against the build before 1, over all 37 corpora at the benchmark's own
warm-up time of 2 seconds, five alternating runs each: the benchmark's warm figure (the last pass) 0.755, corpora
from 0.526 (jsconfig) to 0.923 (geojson), and the median pass 0.753, from 0.513 to 0.939. Compile 0.941. Parse
1.020, where the parser was not touched and a build measured against itself gave 1.021 (one parse is timed per
run). Instructions per pass, where counted at both ends: helm-chart-lock 0.70, importmap 0.59.

**Where the time is after them.** CPU profiles of the benchmark's loop, the mean share of samples over the 37
corpora: finding property names 26 percent (`findAfter` 12, `names.find`, `nameWord`, `names.at`), entering values
23 (`enterChild` 16, which has the dispatch and the start of a strict object in it), the object loops 15
(`visitNames` 10), reading the tape and the text 15 (`Document.str` 10), arrays 6, applicators 6, patterns 4, the
fused pass 3, leaves 2. Against the Rust crate, which this is a port of and which interprets the same plans, what
is left is not a missing analysis: the Go compiler keeps the bounds checks the crate does without (`get_unchecked`),
inlines by a budget where the crate's hot functions are marked to inline, and keeps a loop's state on the stack
once the loop has a call in it.

## Tried and not kept

Each was measured as under Measured and reverted. "Instructions" is the instruction count of a validation pass.

1. **The tape as one struct of two words per value** in place of two `uint64` (one bounds check for a value's two
   words). 0.988 over all 37, but cypress 1.148 and lerna 1.050, and cypress stayed there in two more builds with
   unused functions added (45.1 to 46.5 microseconds a pass against 38.7 to 40.6 before), so it is the change and
   not layout. Parse 1.000.
2. **A name's word read with no branch on its length**: one eight-byte read masked to the length (a short name in a
   document's text has the bytes that follow it to read into), and its last eight bytes as a second word for every
   name. The idea was that the three-way branch on the length is mispredicted. 1.026 over 20 corpora, 16 of them
   slower, the worst 1.095.
3. **The properties that need no call decided in a function that calls nothing** (`scanNames`), so that the loop's
   state stays in registers. With the function run for every property, and the loop only for those that need a
   call: instructions 0.80 to 0.92 for objects of scalars (jshintrc, helm-chart-lock, stale) and 1.06 to 1.20 for
   objects of objects and arrays (importmap, aws-cdk, babelrc, dependabot). Entered only after a property that
   needed no call: instructions 0.960 over 10 corpora with none above 1.01, but omnisharp, whose names are longer
   than sixteen bytes and were left to the loop, 1.21, and 1.141 in time. With long names compared in the function:
   omnisharp 0.89 and cypress 0.83, but helm-chart-lock back to 0.99, since the function then has more live values
   than registers too. No variant was better everywhere. The best had a corpus 8 to 14 percent slower.
4. **The expected name's word read from the text in place**, without taking the name as a slice first
   (`Document.str` is the largest single item of the profile, 11 percent of the samples on average). Instructions
   1.050 over 18 corpora, all but one slower: each read from `text[at:]` has its own two tests, where the reads from
   a slice already made have none.
5. **The sorted-order prediction tested in the loops** (it is in `findAfter`), for instances that list their
   properties sorted. Instructions 1.038 over 18 corpora, and 1.034 on the corpus it was for (helm-chart-lock): the
   larger loop body costs every property more than the call it saves.
6. **A pointer to the node's keywords in `child`** (16 bytes in place of 8), in place of the read through the plans
   in `enterChild`. Instructions 0.999 over 18 corpora.
7. **`visitNames` and the array item loop over a slice of the tape**, as `visitLookup` and `visitValues` are.
   Instructions 1.002 and 1.004 over the corpora measured: with a call in the loop the slice is one more value to
   keep on the stack.
8. **A perfect hash for the names** (hash and displace: the high bits of the hash choose a bucket, whose
   displacement gives the one slot), and **linear probing with the keys in the slots**. See Measured, 4.
9. **Profile-guided optimisation of the benchmark's entry** (a `default.pgo` in its main package, from profiles of
   the protocol loop over all 37 corpora, which is the best case for it since it is then measured on the same
   corpora). 0.994 over all 37, from 0.874 (openapi) to 1.151 (cypress): some corpora faster (cql2 0.898, ui5
   0.906, jsconfig 0.924), as many slower (yamllint 1.090, lerna 1.065, babelrc 1.064). It belongs to a user's own
   binary and cannot be shipped in the library, and here it comes to nothing.

Measured and not committed, for a decision: **`Document.str` without bounds checks**, built with `unsafe.Slice` and
`unsafe.Add` in place of a slice expression of the source or the text. Over 17 corpora the median pass was 0.90 to
0.997 of the time before, most corpora 0.95 to 0.99. The Rust crate reads its tape and text unchecked
(`get_unchecked` in `document.rs`). The tape reads would be the other half of it and were not measured.
