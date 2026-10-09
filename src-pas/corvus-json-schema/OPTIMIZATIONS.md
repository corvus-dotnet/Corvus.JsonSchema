# Optimizations: sweep of the Go module

Every performance technique of the Go module, checked against this port by reading its source. The inventory is the
Go module's `src-go/corvus-json-schema/OPTIMIZATIONS.md`, which this package was ported from, taken item by item in
that file's order. That file in turn sweeps the C# V5 runtime evaluator, the Rust crate and the Java port, so an
item here names its counterpart there where the Go file does.

Status is **done** (with where it is in the Pascal source), **partial** (with what is missing), **todo** (in the
order they should be taken) or **n/a** with the reason. Each "done" was found in the source of this package, in the
unit and under the name given. A unit is named without its `Corvus.JsonSchema.` prefix, so `Eval` is
`src/Corvus.JsonSchema.Eval.pas`.

Nothing in this file has been measured in this port yet. The statuses say what the source does, and not what it is
worth. The todo order is the Go module's, which is the order in which the techniques paid in the other ports, and
each one has to be measured here before it is kept.

Keep this file current. A technique added to any of the other evaluators should be checked off here, or ruled out
with the reason.

## Done

- **Instance representation** (`Document`). A flat tape, `TDocument.Tape`, two `UInt64` words per value. The header
  has the kind in the low byte (the `Kind` constants are the evaluator's type bits), flags, and a 32-bit count or
  length in the high half. The second word is the data. A container's children are consecutive, and an object's are
  name and value pairs. Strings are read in place where they have no escapes (`StrText` marks the others, which
  `Escaped` unescapes into `TDocument.Text`), and the parser records whether a string has a byte outside ASCII
  (`StrWide`). Strings are scanned eight bytes at a time (`ReadStr`, with `LoadLE64`, `SwarOnes` and `SwarHighs`).
  Numbers are classified at parse as an `Int64`, a `UInt64` beyond an `Int64`, or a `Double` (`NumInt`, `NumUint`,
  `NumFloat`), and the header keeps the offset of the text for exact decimals (`DocNumberStart`). The parser is
  iterative, with its open containers on its own stack (`Parse`, `PushFrame`, `CloseContainer`). Duplicate names are
  found pairwise up to 16 properties and by sorted hashes in reused scratch beyond that (`Dedupe`). `ParseDocument`
  sizes the document exactly (`ParseNew`), and the validator's text entry points parse into reused arrays
  (`ParserParseInto`).
- **Number conversion.** Clinger's fast path for a mantissa and a power of ten that a `Double` holds exactly
  (`ReadNumber` in `Document`). The Go module then calls `strconv.ParseFloat`. The Pascal run-time libraries have no
  conversion that is the same in Free Pascal and Delphi, so this port has its own (`DecimalToDouble` in `Numbers`):
  the Eisel-Lemire algorithm as the Java port wrote it (`EiselLemire`, `MulHigh`, the table of
  `Corvus.JsonSchema.PowersOfFive.inc`), which decides up to 19 significant digits and allocates nothing. A longer
  significand is accepted when its truncation and the next value round alike, and only the rest is compared with
  the halfway point in big integers (`CompareWithHalfway`, see Partial). A parser that only checks syntax
  (`ParserIsValid`) does not convert a number that is well inside the range of a `Double`.
- **Type tests.** One mask test against the kind (`TypeOK` in `Plan`, marked `inline`). The integer test runs only
  for integer without number (`IntegerOK`, `AllOfType` in `Eval`). A child that only tests the type is tested where
  it is applied and never entered (`TChild.Types` with `ShapeTrivial`, `RunChild`, `RunBranch`). An `anyOf` of
  type-only branches, or a `oneOf` of type-only branches with no type in common, becomes part of the node's own type
  mask (`TypeUnion`, `TypeOnlyMask`, `MeetTypes` in `Plan`: the C# `TypeUnion` plan).
- **References.** Pure `$ref` chains are elided and a node that is only a one-branch `allOf` forwards to the branch
  (`NewProgram` in `Eval` fills `FastTarget` through `PureRefTarget` and `ForwardTarget`: the C# `Forward` plan), up
  to 16 hops, not across resources under a dynamic scope, and keeping the guard of a node on a cycle. Collecting
  mode elides the same hops with their path suffix (`Resolve`, `CollectPureRef`). A `$dynamicRef` or `$recursiveRef`
  that can be resolved at compile time is (`FinalizeDynamicRefs` in `Compiler` sets `StaticDynamicRef`). A program
  with no dynamic scope takes the fallback of every dynamic reference (`PlanNode` in `Plan`). In draft 7 and earlier
  `$ref` replaces its siblings (`CompileNode`).
- **Plan shapes** (`TShape` and `ShapeOf` in `Plan`, `EnterChild` in `Eval`). A caller enters a child by what its
  keywords come to: nothing but the type, a leaf, a string enum, string keywords, an object plan, an array plan,
  in-place applicators, or a fused object plan. Every child takes the final shape of its node once the fused plans
  are built (`CompilePlans`), and a node entered by its id is entered as a child (`TPlan.Self`, `Run`). Keywords are
  grouped by the kind of value they apply to, so only those for the instance's kind are looked at (`TBody`,
  `RunKeywords`).
- **Objects** (`TObjectPlan` in `Plan`, `RunObject` in `Eval`). One pass over the properties, specialised by which
  keywords apply (`TObjectPlan.Visit`): a map of values against one child, with a tight loop when that child is a
  type (`ObjectVisitValues`), declared names (`ObjectVisitNames`), one pattern and nothing declared
  (`ObjectVisitPattern`), or the general loop (`ObjectVisitGeneral`). `required` is a mask of the names seen, over
  at most 64 names, which include the names only `required` or a dependency mentions (`PlanObject`,
  `RequiredMask`). Dependencies are decided from the same bits (`TPlanDependency.HasBits`). Count bounds come from
  the header. The patterns a declared name matches are worked out at compile time, so only undeclared names are
  matched at run time (`NamePatterns`). `propertyNames` is evaluated on the name where it lies in the document,
  with no document made for it (`ObjectVisitGeneral`).
- **The strict object loop** (`RunStrictObject`). Declared names, `additionalProperties` for the rest and the
  required mask, which a nested object enters directly from `EnterChild` (`ShapeObject` with
  `TObjectPlan.IsStrict`).
- **Small objects probed by name** (`ObjectVisitLookup`). A plan of at most 4 names (`LookupNames`) and no
  `additionalProperties` looks each name up in the instance when the instance's properties times the names is at
  most 24 (`LookupBudget`).
- **Name lookup** (`Names`, the C# `Utf8NameMap`). A set of the lengths the names have settles most misses with one
  test (`TNames.Lengths`, `LengthBit`). A name's key is its length, a word and, beyond eight bytes, its last eight
  bytes (`TNameKey`, `NameWord`, `TailWord`), which is the whole name up to sixteen bytes, so only a longer name has
  its text compared (`NamesFindAfterLong`, `NameMapFindLong`). The loops try the name after the previous match
  first, in the loop itself (`NamesAt`, marked `inline`), then the next name in sorted order (`NamesFindAfter`,
  `TNames.SortedNext`), then a hash table from the key to the index (`NameMapFindKey`). The table has four to eight
  slots for each name and its multiplier is the one of 24 that leaves the fewest names away from their first slot
  (`NewNameMap`, `NameHashMultipliers`). A set too large for the table's indexes is a map in the Go source. Here it
  is the names' indexes in sorted order, searched by halving.
- **Flat composition** (`TFusedObject.Flat`, built in `TryFuse` in `Fused`). A `$ref` and `allOf` chain of plain
  object schemas that apply unconditionally merges into one strict object loop.
- **Fused object plans** (`Fused` builds them, `RunFused` and `RunFusedPass` in `Eval` run them: the C#
  `FusedObject` plan). One pass over an object for a schema whose object semantics are spread over `$ref`, `allOf`,
  `if`/`then`/`else`, dependencies and `oneOf`/`anyOf`. Every name any branch knows resolves at compile time to the
  children that apply to it (`FusedEntryAt`), with identical resolutions from several branches applied once
  (`CoalesceApps`). Conditions are decided from the names seen and from tests on property values, whose constants
  are merged into one lookup per property (`MergeValueTests`, `TMergedTests`, `MergedAllowed`). Applications under a
  condition are deferred to a second step over only the properties that have one. Required-only alternatives
  (`TFusedAlternative`), alternative groups of object branches (`TFusedAltGroup`), `not: {required}`
  (`TFusedForbidden`) and conditions on names no property may match (`TFusedAbsent`) are decided from the same pass.
  The conditions of a pass are two masks, so whether an application applies is two ANDs (`FusedApplies`). Below a
  live dynamic reference only contributors in the node's own resource fuse (`ReachesDynamicReference` in `Plan`).
- **`unevaluatedProperties` from the fused pass.** The pass tracks which properties some branch covered, and the
  rest go to the unevaluated schema (`TFusedObject.HasUnevaluated`).
- **`unevaluatedItems` from static coverage** (`StaticItemCoverage` in `Plan`). When every contribution to the
  evaluated items is unconditional, the items after the longest prefix are checked against the schema with no
  tracking.
- **Evaluated properties and items elsewhere.** The marking analysis (`ComputeMarking` in `Compiler`) says which
  nodes can mark. Only a child that can mark gets a set of its own (`CanMark` in `Eval`). Sets live in the
  evaluation's arena (`NewBits`). A node the general evaluator runs evaluates only its own location: each value
  below it goes back to its plan (`Run(E, E.P^.FastTarget[...], ...)` in `EvalObject` and `EvalArray`).
- **Arrays** (`RunArray`). The length is read once. `prefixItems` is guarded by the length. An array of type-only
  items is one loop over the tape (`AllOfType`), and so are the items after a prefix. Items that are themselves
  simple arrays are checked inline without being entered (`TArrayPlan.HasNested`). `contains` stops at
  `minContains` when there is no maximum.
- **`uniqueItems`** (`AllUnique` in `Values`). Nothing for fewer than two items. Up to 32 strings pairwise, lengths
  before bytes. Up to 16 other values pairwise. Longer arrays sorted by hash in reused scratch, comparing only the
  items whose hashes are equal. Two objects that list their members in the same order are compared position by
  position, and values of different counts are unequal without a comparison (`ValuesEqual`).
- **String lengths** (`LengthOK` in `Eval`). Exact from the byte length for a string the parser marked ASCII.
  Otherwise decided from the byte length where it can be, and code points counted only in the band it does not
  decide (`CodePoints`).
- **Pattern shapes** (`Pattern`). Patterns every string matches, `.+`, line lengths (`LineLength`), anchored
  literals, whole literal sets, anchored class sequences matched in one pass and by length where one item is
  variable (`MatchPinned`), alternatives of literals and sequences once groups are multiplied out
  (`ExpandAlternatives`), separated lists (`ParseSeparatedList`, `ListMatch`), and the excluded class with a word
  lookahead (`ExcludedClassWithWord`). A sequence has a byte-per-character path for a string the document knows is
  ASCII (`MatchASCII`). The pattern cache is under Partial.
- **Numbers** (`Numbers`). Exact comparison across `Int64`, `UInt64` and `Double` (`CompareNumbers`). Bounds are
  digested at compile time into the same representation (`TNumberOp` in `Plan`, `RunNumber` in `Eval`). An integer
  `multipleOf` of an integer is `mod` (`DivisorDivides`). Any other `multipleOf` is decided on the decimal digits of
  the text with integer arithmetic (`DividesText`).
- **Formats.** The format kind and whether it asserts are decided at compile time for the node's dialect
  (`FormatCheckFor` in `Plan`). Formats read the UTF-8 bytes in place (`Formats`). Where the Go module uses its
  linear-time regular expressions (URI, IRI, URI template, e-mail local part), `Formats` reads the same grammars
  directly, as the Julia port does, so no format but `regex` needs the pattern engine. A custom format is handed the
  bytes where they lie (`CallCustom` in `Eval`), where the Go module gives it a copy of the string.
- **Enum and const.** An enum of strings is a name lookup (`OpEnumStrings`, through `NamesFind`), decided where the
  child is entered when that is all the child is (`ShapeStringEnum` in `EnterChild`). Other values compare by JSON
  equality with the kind first and string lengths before bytes (`ValuesEqual`, `StringsEqual`).
- **Composition.** `anyOf` stops at the first match and `oneOf` at the second (`RunOp`). Only the branches whose
  types admit the instance's kind are tried (`TBranches.ByKind`, `Candidates`: the C# `TypeDispatch` plan), and a
  `oneOf` that leaves one candidate is that branch. Discriminators narrow both, with string values through a name
  lookup (`TDiscriminatorIndex`, `NewDiscriminatorIndex`, `SelectIndexed`). `if` without `then` or `else` is dropped
  (`PlanNode`). `not` fails fast.
- **Dynamic scope and cycles.** A resource is pushed only in a program that keeps a dynamic scope
  (`TProgram.UsesDynamicScope`), and only when it is not the innermost already (`PushScope`). The depth guard
  applies only to nodes on an in-place cycle (`TPlan.Guard`, `RunInPlace`).
- **Allocation.** The evaluator is a record on the stack of the call that validates (`TEvaluator`, in
  `TJsonSchemaValidator.Run`). Its buffers are a scratch taken only when first needed (`State`, `TakeScratch`), so
  most schemas touch none. The Go module takes the scratch from a pool that belongs to the validator. Here it is a
  thread variable (`TheScratch` in `Eval`), as the C#, Rust and Java evaluators keep per-thread buffers, so the
  validation path takes no lock. The state of a fused pass is on the stack too (`TFusedPass` in `RunFused`), and a
  pass takes the scratch only to track covered properties or for an object of more than 64 properties. JSON text is
  parsed into the thread's document (`RunParsed` in `Corvus.JsonSchema`), and a string is read without a copy
  (`StrBytes`). The backtracking matcher keeps its stack in a thread variable in the same way (`EcmaRegex.Vm`).
  `tests/TestAllocations.pas` holds the keyword paths it lists to no request for memory.
- **Inlining.** The small functions of the loops are marked `inline` and corvus.inc turns inlining on: the type
  test (`TypeOK`), the application of a type-only child (`RunChild`), the test of the expected name (`NamesAt`), the
  accessors of the tape (`DocKind`, `DocCount`, `DocFirst` and the others of `Document`) and `FusedApplies`.
  `EnterChild`, `NamesFindAfter` and `NamesFind` are the calls, each with what it needs written out in it, as in the
  Go source. A value's header is read once where the value is entered (`EnterChild`). A whole build with Free
  Pascal 3.2.2 at `-O3` reports two calls to an `inline` function that it did not inline (`TypeOK` and `NoChild`,
  both called from `Fused`, which builds plans and does not validate). What the inlining is worth has not been
  measured. For the bounds checks, see Not applicable.
- **Compile time.** Analyses run only when the schema has what they analyse (`Analyse` in `Compiler`). In-place
  cycles are found by an iterative Tarjan (`ComputeInPlaceCycles`). Annotation keywords are digested on the first
  evaluation with a collector (`ComputeNodeAnnotations`, called once under the validator's lock). The metaschemas
  are constants of an include file and are read on demand (`Metaschema` in `Metaschemas`).

## Partial

- **The regular expression engine** (`EcmaRegex` and its units). The Go module translates a pattern in the subset
  that RE2 reads the same way to the standard library's `regexp` and matches it in linear time. Object Pascal has no
  such library and this package uses none, so that translation is not ported. Done: such a pattern with no word
  boundary gets a deterministic automaton over ASCII text of at most 256 states (`EcmaRegex.Dfa`, `MaxDFAStates`),
  built when the pattern is compiled, which decides an ASCII text with one table read per byte. Done: the
  backtracking matcher has an explicit stack kept by the thread, so a match allocates nothing once it has grown
  (`EcmaRegex.Vm`). Missing: a linear-time matcher for a pattern of that subset that has no automaton, and for a
  text that is not ASCII. Those run on the backtracking matcher, which has no step budget.
- **Patterns compiled once.** The Go module keeps every compiled pattern in a cache for the whole process
  (`patternCache`). Here `CompilePattern` compiles the pattern it is given each time, and the schema compiler
  shares one matcher between the identical patterns of one schema (`CompilerPattern` and `PatternIndex` in
  `Compiler`). Missing: nothing is shared between schemas.
- **Leaves decided in the loop.** As in the Go module, only a type-only child is decided in the loop. Any other
  child is one call to `EnterChild`, and a leaf then runs `RunLeaf`, which loops over its operations. Missing:
  const, string enum and length tests in `ObjectVisitNames`, `ObjectVisitLookup`, the fused entry and the array
  loops, without the calls.
- **`true` subschemas skipped.** The fused plan treats a `true` child as cover with no test (`TOptChild`,
  `ApplyOpt`). The object plan does not, as in the Go module: `additionalProperties: true` stays a child that
  accepts everything, which costs a call per undeclared property and rules out the probe by name
  (`TObjectPlan.Lookup` needs no `additionalProperties`), `propertyNames: true` selects the general loop, and a
  `true` pattern property is still matched (`PlanObject`).
- **Keywords that cannot apply under the node's type.** Object and array keywords are dropped when `type` excludes
  objects or arrays (`PlanNode`). Number and string keywords are kept whatever the type, so such a node is a leaf
  where it could be a type test.
- **Enums that are not all strings.** An enum with one value that is not a string is compared value by value
  (`OpEnum`, `EnumContains`).
- **Searching for an unanchored match.** An unanchored literal is searched for a byte at a time (`IndexBytes` in
  `Pattern`), and an unanchored class sequence is tried at every position (`AltAnywhere`).
- **Content.** Base64 content is decoded into scratch even when only its validity is asked (`ContentOK`,
  `Base64Decode`). Content that is JSON is checked by a parser that builds nothing (`ParserIsValid`), but the
  string is first copied into the thread's content buffer, because the parser reads from the start of an array. The
  Go module checks it where it lies.
- **Allocation outside the common paths.** A `multipleOf` whose divisor has more than 18 significant digits is
  decided with the unit's own big integer, which allocates (`DividesTextBig`, `TBig` in `Numbers`). So does the
  conversion of a number of more than 19 significant digits whose nearest `Double` the first 19 do not decide
  (`CompareWithHalfway`).

## Todo

Nothing here has been measured in this port. The first thing to do is to measure it (see Measured).

1. **Leaves decided in the loop** (see Partial). The Go module's measurements say that code added to the body of
   the property loop made every property dearer there. Whether that holds for Free Pascal's code generator is not
   known.
2. **`true` subschemas dropped from the object plan** (see Partial).
3. **Equivalent subschemas canonicalised.** The C# compiler points fail-fast references at one representative of
   each set of identical subschemas, and the Java port merges structurally identical nodes. There is no counterpart
   here, as there is none in the Go module: `GetNode` in `Compiler` makes one node per schema location.
4. **Unanchored search** with a first-atom skip (see Partial).
5. **Mixed and large enums** (see Partial).
6. **Keywords pruned by type** for numbers and strings (see Partial).
7. **Collecting mode without allocation.** A collector keeps its arrays at what they have grown to (`Results`), but
   each row's locations and message are `UTF8String` values built for it.
8. **Base64 validity without decoding, JSON content checked where it lies, and `multipleOf` beyond 18 digits
   without a big integer that allocates** (see Partial).
9. **The fused pass.** It is a set of small loops over contributors, applications and tests for each property, as
   in the Go module. It would have to be measured line by line.
10. **A linear-time matcher for the patterns that have no automaton, and for texts that are not ASCII** (see
    Partial, the regular expression engine). This is the one place where a pattern can cost more here than in the
    Go module by more than a constant.
11. **A pattern cache for the process** (see Partial), if compiling schemas that share patterns turns out to
    matter.

## Not applicable

- **Reads without bounds checks.** The Go module reads some loops over a slice of the tape, which lets the Go
  compiler drop the bounds check per value (`visitLookup`, `visitValues`, `allOfType`). Here every array read is
  range checked. `{$R+}` in `src/corvus.inc` is never turned off, and nothing indexes through a raw pointer to avoid
  the check. That is a decision, the same one the Go module made when it declined `Document.str` without bounds
  checks, and the same one the Julia package made in using no `@inbounds`. A mistake in an unchecked read would read
  other memory, where a checked read raises `ERangeError`. What the checks cost has not been measured.
- **Runtime code generation** (the C# IL emitter, the Java bytecode generator, the TypeScript source generator), and
  everything that belongs to it. Object Pascal compiles ahead of time and has no portable way to load code at run
  time. The plans here are data that one evaluator interprets, as in the Go module and the Rust crate.
- **A program image, ReadyToRun and tiered compilation** (C#), **an ahead-of-time cache** (Java), **profile-guided
  optimisation** (Go, tried and not kept there). Object Pascal compiles ahead of time, and the package sets no
  optimisation options: they belong to the program that uses it. The tests build with `-O3`.
- **Generic specialisation over a document type** (C#), **the `Instance` trait and monomorphised fail-fast and
  collecting evaluators** (Rust). Instances are always `TJsonDocument` values. Fail-fast evaluation runs the plans,
  and the general evaluator serves collecting.
- **One row on after a scalar, integers read in one pass, integer bounds and consts with no conversion** (the C#
  code generator). They work around metadata rows of varying size and numbers kept as text. Here every value is two
  words, a container's children are consecutive, and a number's value is in the tape.
- **`sync.Pool` and what follows from it** (Go: the scratch pooled by the validator, the working memory of a match
  pooled by each program, the automaton built on the first match behind a `sync.Once`). The buffers here belong to
  the thread, and the automaton is built when the pattern is compiled, so a compiled pattern and a compiled schema
  are values that nothing writes to.
- **The inliner's budget** (Go: functions shaped to cost at most 80 on the Go inliner's scale). Free Pascal inlines
  what is marked `inline` (see Inlining under Done).
- **Translation to another engine** (`regexp` in Go, `java.util.regex` in Java, the `regex` and `regress` crates in
  Rust, compiled IL regular expressions in C#, PCRE2 in Julia). The package has no dependencies, so its own engine
  is the only one (see Partial).
- **A vectorised search API** (`SearchValues` and `IndexOfAny` in C#, `bytes.Index` in Go). The source uses nothing
  that only one of Free Pascal and Delphi has, so its searches are loops over the bytes.
- **Release profile settings** (the Rust crate's `lto` and `codegen-units`). See the program image item above.

## Measured

Nothing has been measured yet. No timing, instruction count or profile of this port exists, and no figure of the Go
module's is repeated here as if it were this port's.

When measurements are made, each entry should say what was measured, by what, on which compiler and target, and
against what, as the Go module's and the Julia package's files do.

## Tried and not kept

Nothing has been tried and reverted yet, since nothing has been measured. The Go module's list of reverted
experiments is about the Go compiler's code generation (registers kept across a call, the inliner's budget, bounds
checks of slices) and says nothing about what Free Pascal would do with the same changes.
