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
- **Plan shapes** (`shape`, `shapeOf`, `enter` in `plan.go`). A caller enters a child by what its keywords come to:
  nothing but the type, a leaf, a string enum, string keywords, an object plan, an array plan, or in-place
  applicators. These are the C# `Leaf`, `Object`, `StrictObject`, `SimpleArray`, `ArrayItems`, `Composite` and
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
- **Small objects probed by name** (`visitLookup`, `Document.propertyWord`). A plan of at most 4 names and no
  `additionalProperties` looks each name up in the instance when the instance's properties times the names is at
  most 24, comparing names by length and word before text.
- **Name lookup** (`names.go`, the C# `Utf8NameMap`). A set of the lengths the names have settles most misses with
  one test (`names.lengths`). A name of at most eight bytes is one word that is unique among names of its length
  (`nameWord`), so a miss never touches the text. A few names of one length are compared in turn, and more are split
  by the byte position that best tells them apart (`byLengthTable`). The loops try the name after the previous match
  first, then the next name in sorted order, since instances tend to list their properties in the schema's order or
  sorted (`names.findFrom`).
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
  translated to the standard library's `regexp` and matched in linear time. Anything else runs on a backtracking
  matcher whose stack is explicit and pooled, so a match allocates nothing once it has grown.
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
  from the validator's pool only when first needed (`evaluator.state`), so most schemas touch no pool. JSON text is
  parsed into a pooled document, and a string is read without a copy (`stringBytes`).
  `TestValidationAllocatesNothingInTheSteadyState` holds the keyword paths it lists to zero allocations.
- **Compile time.** Analyses run only when the schema has what they analyse (`analyse`). In-place cycles are found by
  an iterative Tarjan (`computeInPlaceCycles`). Annotation keywords are digested on the first evaluation with a
  collector (`nodeAnnotations`). The metaschemas are embedded and read on demand.

## Partial

- **Leaves decided in the loop.** The C# strict loop decides a string enum, a string const, a string length and a
  nested strict object for a property without leaving the loop (`StrictEntry`), the C# code generator emits leaf
  children in the loop, and the TypeScript generator inlines const and enum leaves. Here only a type-only child is
  decided in the loop. Any other child goes through `runChild` and `enter`, and a leaf then runs `runLeaf`, which
  loops over its operations. Missing: const, string enum and length tests in `visitNames`, `visitLookup`, the fused
  entry and the array loops, without the calls.
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

1. **Leaves decided in the loop** (see Partial). Every corpus has objects of scalar properties, so this is the first
   to measure.
2. **`true` subschemas dropped from the object plan** (see Partial).
3. **Equivalent subschemas canonicalised.** The C# compiler points fail-fast references at one representative of each
   set of identical subschemas (`CanonicalizeEquivalentNodes`), and the Java port merges structurally identical nodes.
   A chain of conditionals that restates a property's schema at every level then applies one node, which a fused
   object tests once. There is no counterpart here: `getNode` makes one node per schema location.
4. **Unanchored search** with `bytes.Index` and a first-atom skip (see Partial).
5. **Inlining and bounds checks audited.** The C# evaluator marks its small hot methods for inlining and the Rust
   crate keeps its type test small enough to inline. Go decides inlining by a cost budget and says what it did with
   `-gcflags=-m`. Check `Document.kind`, `typeOK`, `runChild`, `names.findFrom` and the tape loops, and where the
   compiler keeps bounds checks in them (`-gcflags=-d=ssa/check_bce`).
6. **Profile-guided optimisation.** Go applies a `default.pgo` profile when it builds a main package. This is the
   counterpart of the C# MIBC profile and belongs to the benchmark harness and to users' own binaries, not to the
   library. Measure it in `corvus-json-schema-bench` before recommending it.
7. **Mixed and large enums** (see Partial).
8. **Keywords pruned by type** for numbers and strings (see Partial).
9. **Collecting mode without allocation.** The C# collector is pooled and writes its paths into reused buffers. Here
   a collector reuses its row slices after `Reset`, but each row's locations and message are strings built for it.
   The Java port lists the same item.
10. **Base64 validity without decoding, and `multipleOf` beyond 18 digits without `math/big`** (see Partial).

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
mean over the corpora, with the range over the corpora. A build measured against itself gave 1.000, with corpora
between 0.963 and 1.024, so a corpus inside 0.96 to 1.03 did not move.

1. **The type test inlined** (`typeOK`, `integerOK`). The mask test was 124 on the inliner's scale, against a budget
   of 80, so every type test was a call. The integer test, which few values reach, is now the call. 0.979 (0.724 to
   1.034). The 0.724 is a map of strings (importmap).
2. **A type-only child decided where it is applied** (`runChild`, `child.pass`, `enterChild`). `runChild` was a call
   that made a second call to `enter`. It is now one test of the value's kind against `child.pass`, inlined into the
   property and item loops, and one call (`enterChild`, which has the dispatch by shape in it) for a child with
   keywords. 0.929 (0.840 to 0.999).
