# Optimizations: sweep of the C#, Rust, Java and Go evaluators

Every performance technique of the other Corvus evaluators, checked against this port by reading its source. The
inventory is the Go module's `src-go/corvus-json-schema/OPTIMIZATIONS.md`, which this package was ported from, and
the inventories that file names. Those are the C# V5 runtime evaluator
(`src/Corvus.Text.Json/Corvus/Text/Json/RuntimeEvaluator` and the code generator's `CodeGeneration/OPTIMIZATIONS.md`),
the Rust crate (`src-rs/corvus-json-schema`, `eval/plan.rs` and `eval/plan/fused.rs`) and the Java port
(`src-java/corvus-json-schema/OPTIMIZATIONS.md`).

Status is **done** (with where it is in the Julia source), **partial** (with what is missing), **todo** (in the
order they should be taken) or **n/a** with the reason. The todo order is the order in which the techniques paid in
the other ports, and each one has to be measured here before it is kept. What was measured is under "Measured" and
"Measured and declined" at the end. The Go module's own measurements and reverted experiments are accounted for
under "Measured" and "Tried and not kept", each with what this port's source has in its place.

Keep this file current. A technique added to any of the other evaluators should be checked off here, or ruled out
with the reason.

## Done

- **Instance representation** (`document.jl`). A flat tape, `Document.tape`, two `UInt64` words per value. The
  header has the kind in the low byte (the `KIND_` constants are the evaluator's type bits), flags, and a 32-bit
  count or length in the high half. The second word is the data. A container's children are consecutive, and an
  object's are name and value pairs. Strings are read in place where they have no escapes (`STR_TEXT` marks the
  others, which `escaped!` unescapes into `Document.text`), and the parser records whether a string has a byte
  outside ASCII (`STR_WIDE`). Strings are scanned eight bytes at a time (`string!`, with `SWAR_ONES` and
  `SWAR_HIGHS`), then by the `SCAN` table. Numbers are classified at parse as an `Int64`, a `UInt64` beyond an
  `Int64`, or a `Float64` (`NUM_INT`, `NUM_UINT`, `NUM_FLOAT`), and the header keeps the offset of the text for
  exact decimals. The parser is iterative, with its open containers on its own stack (`parse!`, `Parser.frames`,
  `close!`). Duplicate names are found pairwise up to 16 properties and by sorted hashes in scratch beyond that
  (`dedupe!`, `sort_words!`). `parse_document` sizes the document exactly (`parse_new!`) with a parser from a pool
  (`take_parser`, `return_parser`), and the validator's text entry points parse into reused arrays (`parse_into!`).
  A string value is read as a `Bytes`, which is the vector, an offset and a length, with no copy (`str`).
- **Number conversion.** Clinger's fast path for a mantissa and a power of ten that a `Float64` holds exactly
  (`number!`, `POWERS10`). Then the Eisel-Lemire algorithm, written here as the Java port wrote it
  (`decimal_to_float`, `float_bits`, the `POWERS5` table, `mulhi`), which is exact for up to 19 significant digits
  and allocates nothing. A longer significand is accepted when its truncation and the next value round alike. Only
  the rest reaches `Base.tryparse` (`decimal_to_float_slow`, see Partial). A parser that only checks syntax
  (`is_valid_json!`) does not convert a number that is well inside the range of a `Float64`.
- **Type tests.** One mask test against the kind (`type_ok`, `plan.jl`). The integer test runs only for integer
  without number (`integer_ok`, `all_of_type`). A child that only tests the type is tested where it is applied and
  never entered (`Child.types` and `Child.pass` with `SHAPE_TRIVIAL`, `@run_child`, `run_branch`). An `anyOf` of
  type-only branches, or a `oneOf` of type-only branches with no type in common, becomes part of the node's own
  type mask (`type_union`, `type_only_mask`, `meet_types`, which is the C# `TypeUnion` plan).
- **References.** Pure `$ref` chains are elided and a node that is only a one-branch `allOf` forwards to the branch
  (the `Program` constructor in `eval.jl` fills `fast_target` through `pure_ref_target` and `forward_target`, which
  is the C# `Forward` plan), up to 16 hops, not across resources under a dynamic scope, and keeping the guard of a
  node on a cycle. Collecting mode elides the same hops with their path suffix (`resolve`, `collect_pure_ref`). A
  `$dynamicRef` or `$recursiveRef` that can be resolved at compile time is (`compile_dynamic_ref!` and
  `finalize_dynamic_refs!` set `static_dynamic_ref`). A program with no dynamic scope takes the fallback of every
  dynamic reference (`plan_node`). In draft 7 and earlier `$ref` replaces its siblings (`compile_node!`).
- **Plan shapes** (the `SHAPE_` constants in `plan_types.jl`, `shape_of` and `enter_child` in `plan.jl`). A caller
  enters a child by what its keywords come to, which is nothing but the type, a leaf, a string enum, string
  keywords, an object plan, an array plan, in-place applicators, or a fused object plan. Every child takes the
  final shape of its node once the fused plans are built (`compile_plans`, its `final` step), and a node entered by
  its id is entered as a child (`Plan.self`, `Evaluator.selfs`, `@run`). Keywords are grouped by the kind of value
  they apply to, so only those for the instance's kind are looked at (`Body`, `run_keywords`).
- **Objects** (`ObjectPlan`, `run_object`). One pass over the properties, specialised by which keywords apply (the
  `VISIT_` constants). A map of values against one child has a tight loop when that child is a type
  (`visit_values`). The others are declared names (`visit_names`), one pattern and nothing declared
  (`visit_pattern`), and the general loop (`visit_general`). `required` is a mask of the names seen, over at most
  64 names, which include the names only `required` or a dependency mentions (`plan_object`,
  `ObjectPlan.required_mask`). Dependencies are decided from the same bits (`PlanDependency.has_bits`,
  `object_rest`). Count bounds come from the header. The patterns a declared name matches are worked out at compile
  time, so only undeclared names are matched at run time (`ObjectPlan.name_patterns`). `propertyNames` is evaluated
  on the name where it lies in the document, with no document made for it (`visit_general`).
- **The strict object loop** (`run_strict_object`, and the same loop written out at the end of `enter_child`).
  Declared names, `additionalProperties` for the rest and the required mask, entered directly by a nested object
  (`SHAPE_OBJECT` with `ObjectPlan.strict`).
- **Small objects probed by name** (`visit_lookup`, `LOOKUP_NAMES`, `LOOKUP_BUDGET`). A plan of at most 4 names and
  no `additionalProperties` looks each name up in the instance when the instance's properties times the names is at
  most 24, comparing a name's length from its header, then its word, then its text.
- **Name lookup** (`names.jl`, the C# `Utf8NameMap`). A set of the lengths the names have settles most misses with
  one test (`Names.lengths`, `length_bit`). A name's key is its length, its first eight bytes as a word and, beyond
  eight bytes, its second word (`NameKey`, `name_word`, `second_word`), which is the whole name up to sixteen
  bytes. A longer name has only the bytes after its first sixteen compared, a word at a time (`rest_equal`). The
  loops try the name after the previous match first, in the loop itself (`find_next` with `name_at` and
  `name_rest`), then the next name in sorted order (`Names.sorted_next` in `find_after`), then a hash table from
  the key to the index (`Names.table`, `name_hash`). The table has four to eight slots for each name and its multiplier is the one of 24 that leaves the
  fewest names away from their first slot (`fill_table!`, `NAME_HASH_MULTIPLIERS`), so a search is one
  multiplication and one read to the index. A set of more than `MAX_TABLE_NAMES` names is a `Dict`
  (`Names.is_large`).
- **Flat composition** (`FusedObject.flat`, built at the end of `try_fuse`). A `$ref` and `allOf` chain of plain
  object schemas that apply unconditionally merges into one strict object loop.
- **Fused object plans** (`fused.jl`, the C# `FusedObject` plan). One pass over an object for a schema whose object
  semantics are spread over `$ref`, `allOf`, `if`/`then`/`else`, dependencies and `oneOf`/`anyOf`. Every name any
  branch knows resolves at compile time to the children that apply to it (`FusedEntry`, `try_fuse`), with identical
  resolutions from several branches applied once (`coalesce_apps`). Conditions are decided from the names seen and
  from tests on property values, whose constants are merged into one lookup per property (`MergedTests`,
  `merge_value_tests`, `allowed_tests`). Applications under a condition are deferred to a second step over only the
  properties that have one (`run_fused_pass`, `FUSED_DEFER`). The conditions that apply an application or a
  contributor are two masks worked out when the plan is built (`FusedApp.then_` and `els`, `FusedContributor.then_`
  and `els`, `applies`), and only the contributors with required names or count bounds are visited after the
  properties (`FusedObject.finals`). Required-only alternatives (`FusedAlternative`), alternative groups of object
  branches (`FusedAltGroup`), `not: {required}` (`FusedForbidden`) and conditions on names no property may match
  (`FusedAbsent`) are decided from the same pass. Below a live dynamic reference only contributors in the node's
  own resource fuse (`reaches_dynamic_reference`, `FuseCollector.same_resource`).
- **`unevaluatedProperties` from the fused pass.** The pass tracks which properties some branch covered, and the
  rest go to the unevaluated schema (`FusedObject.has_unevaluated`, `FUSED_COVER`).
- **`unevaluatedItems` from static coverage** (`static_item_coverage`, `Body.unevaluated_from`). When every
  contribution to the evaluated items is unconditional, the items after the longest prefix are checked against the
  schema with no tracking.
- **Evaluated properties and items elsewhere.** The marking analysis (`compute_marking!`) says which nodes can
  mark. Only a child that can mark gets a set of its own (`can_mark`, `eval_in_place_child`). Sets live in the
  evaluator's arena (`Bitset`, `new_bits!`, `free_bits!`). A node the general evaluator runs evaluates only its own
  location, and each value below it goes back to its plan (`run(e, target(e.p, child), v)` in `eval.jl`).
- **Arrays** (`run_array`). The length is read once. `prefixItems` is guarded by the length. An array of type-only
  items is one loop over the tape (`ArrayPlan.is_simple`, `all_of_type`), and so are the items after a prefix.
  Items that are themselves simple arrays are checked inline without being entered (`ArrayPlan.has_nested`,
  `SimpleArray`, GeoJSON's positions). `contains` stops at `minContains` when there is no maximum.
- **`uniqueItems`** (`all_unique`, `values.jl`). Nothing for fewer than two items. Up to 32 strings pairwise,
  lengths before bytes (`all_strings`). Up to 16 other values pairwise. Longer arrays sorted by hash in reused
  scratch (`Evaluator.unique`, `value_hash`, `sort_words!`), comparing only the items whose hashes are equal. Two
  objects that list their members in the same order are compared position by position, and values of different
  counts are unequal without a comparison (`values_equal`).
- **String lengths** (`length_ok`). Exact from the byte length for a string the parser marked ASCII (`str_ascii`).
  Otherwise decided from the byte length where it can be, and code points counted only in the band it does not
  decide (`code_point_count`, `rune_count`).
- **Pattern shapes** (`pattern.jl`, the `MATCH_` constants, `choose_pattern`). Patterns every string matches, `.+`,
  line lengths (`^.{m,n}$`, `line_range`), `^X.*` reduced to `^X`, anchored literals, whole literal sets (compared
  in turn up to 8, through a `Names` table beyond), anchored class sequences matched greedily where that is exact
  and by length where one item is variable (`parse_sequence`, `greedy_before`, `match_pinned`), alternatives of
  literals and sequences once groups are multiplied out (`parse_alternatives`, `expand_alternatives`), separated
  lists (`SeparatedList`, `match_list`), and the excluded class with a word lookahead
  (`excluded_class_with_word`). A sequence has a byte-per-character path for a string the document knows is ASCII
  (`match_ascii`). Patterns compile once per process (`PATTERN_CACHE`, under `PATTERN_CACHE_LOCK`).
- **The regular expression engine** (`src/ecmaregex`, the `EcmaRegex` submodule, named only by
  `pattern_engine.jl`). The Go module translates to RE2, builds an automaton and keeps a backtracking matcher of
  its own. This port, like the Java one, translates an ECMA-262 pattern exactly into a pattern for another engine,
  which here is the PCRE2 that Julia bundles (`parser.jl`, `emitter.jl`, `pcre.jl`). Julia's own `Regex` is not
  used, since its matches allocate and it compiles with options of its own. PCRE2's JIT compiler is used where it
  takes the pattern (`pcrecompile`), and the interpreter runs the rest and a match too deep for the JIT stack
  (`matchagain`). A match allocates nothing. What it writes to is C memory that belongs to the thread it runs on
  (`Frame`, `threadframe`), and the text is matched where it lies through a pointer held under `GC.@preserve`
  (`ismatch`, `unsafe_ismatch`). Every class, property and case-insensitive set is written from Unicode data the
  package carries, so nothing depends on the PCRE2 version's tables. A lookbehind of no fixed length runs as a
  PCRE2 callout that searches for its body (`callout`, `lookbehind` in `emitter.jl`). A match that reaches PCRE2's
  step or memory limit, or the module's own limit on the steps of lookbehind searches (`STEP_LIMIT`), throws a
  `MatchError` and is never reported as no match. A pattern that cannot be written with the same meaning is refused
  when the schema is compiled (the `REFUSED_` constants, which are the backreference cases, more than 253
  lookbehinds of no fixed length, and anything PCRE2 does not compile). PCRE2 is told not to work out where a match
  can start (`PCRE2_NO_START_OPTIMIZE`), because both PCRE2 versions the module was written against answer wrongly
  with it for some patterns. That costs the skip of positions in a search, and nothing for a pattern anchored
  with `^`.
- **Numbers** (`numbers.jl`). Exact comparison across `Int64`, `UInt64` and `Float64` (`compare_numbers`). Bounds
  are digested at compile time into the same representation (`NumberOp`). An integer `multipleOf` of an integer is
  `rem`. Any other `multipleOf` is decided on the decimal digits of the text with integer arithmetic
  (`divides_text`, `Divisor`).
- **Formats and content.** The format kind and whether it asserts are decided at compile time for the node's
  dialect (`format_check_for` in `plan_node`, `FormatCheck`). Formats read the UTF-8 bytes (`check_string_format`).
  Where the Go module uses `regexp` for the URI, IRI, URI template and e-mail local part formats, this port reads
  their grammars directly with no engine (`is_uri`, `uri_authority_ok`, `uri_hier_ok`, `uri_chars_ok`,
  `is_uri_template`, `email_local_ok`). Content that is JSON is checked by a reused parser that builds nothing
  (`is_valid_json!`, `Evaluator.content_parser`).
- **Enum and const.** An enum of strings is a name lookup (`OP_ENUM_STRINGS`), decided where the child is entered
  when that is all the child is (`SHAPE_STRING_ENUM`). Other values compare by JSON equality with the kind first
  and string lengths before bytes (`values_equal`).
- **Composition.** `anyOf` stops at the first match and `oneOf` at the second (`run_op`). Only the branches whose
  types admit the instance's kind are tried (`Branches.by_kind`, `dispatch!`, which is the C# `TypeDispatch` plan),
  and a `oneOf` that leaves one candidate is that branch. Discriminators narrow both, with string values through a
  name lookup and a fast failure when every branch requires the property (`DiscriminatorIndex`, `select_branches`,
  `candidates`). `if` without `then` or `else` is dropped (`plan_node`). `not` fails fast.
- **Dynamic scope and cycles.** A resource is pushed only in a program that keeps a dynamic scope, and only when it
  is not the innermost already (`run_body`, `push_scope!`). The depth guard applies only to nodes on an in-place
  cycle (`Plan.guard`, `Evaluator.guards`, `run_in_place`).
- **Allocation.** A `Validator` owns one evaluation state (`Validator.primary`, an `Evaluator`) and takes it for a
  validation with one atomic exchange of `Validator.busy` (`acquire`). A validation that overlaps another, from
  another task or from a format callback, takes a state from the validator's pool under a spin lock
  (`acquire_pooled`, `release_pooled`). The state has every buffer an evaluation may need (the arena, the
  `uniqueItems` scratch, the dynamic scope, the document and parser for JSON text, the content buffer and parser,
  the fused passes), so an evaluation allocates nothing once they have grown. Buffers that grew beyond
  `SCRATCH_RETAINED_LIMIT` are dropped (`drop_buffers!`). A program that may throw (a custom format, or a pattern
  on the engine) runs inside `try` and `finally` so that the state is given back (`run_guarded`,
  `Program.may_throw`), and other programs take the path with no handler. `test/allocations.jl` holds the keyword
  paths it lists to zero allocated bytes for a `Document`, a vector of bytes and a `String`.
- **Inlining.** Julia has no fixed inlining budget to write to. The small functions of the loops are marked
  `@inline`, and the calls that should stay calls are marked `@noinline`. The type test (`type_ok`), the test of
  the expected name (`name_at`, `name_rest`, `name_word`) and the tape readers (`header`, `kind`, `count`, `first`,
  `str`, `str_ascii`) are inlined into the loops. `@inline` does nothing for a function that is part of the cycle
  of functions that evaluate a value, so the application of a type-only child and the entry of a node by its id
  are macros (`@run_child`, `@run`, see Measured 2). What the package image really has is checked by disassembling
  it, not with `code_typed`. `enter_child`, `find_after` and `find` are the calls, each with what it needs written
  out in it, so that a value costs one call and not a chain of them. The rare halves are kept out of line
  (`integer_ok`, `find_long`, `find_after_long`, `divides_slow`, `decimal_to_float_slow`, `call_custom_format`,
  `acquire_pooled`, `run_guarded`). A value's header is read once where the value is entered (`enter_child`), and
  the count and first child are handed to the property loop.
- **Type stability.** This is the Julia counterpart of what a static compiler gives the other ports. Every field
  of the tape, the nodes, the plans and the evaluator has a concrete type or is a concrete type with `Nothing`, and
  the recursive entry functions declare their result (`::Bool`, `::Tuple{UInt64,Bool}`, `::UInt8`).
  `test/stability.jl` checks that the optimised code of the hot functions has no dynamic dispatch and that the
  structures have no field of an abstract type. The one dynamic call is the user's custom format, behind
  `call_custom_format`.
- **Compile time and start-up.** Analyses run only when the schema has what they analyse (`analyse!`). In-place
  cycles are found by an iterative Tarjan (`compute_in_place_cycles!`). Annotation keywords are digested on the
  first evaluation with a collector (`node_annotations`). The metaschemas are read into the package image when the
  package is precompiled (`METASCHEMA_FILES`) and parsed on demand. The interpreter itself is compiled into the
  package image by the precompile workload (`precompile.jl`, see "The execution model").

## Partial

- **Leaves decided in the loop.** As in the Go module, only a type-only child is decided in the loop. Any other
  child is one call to `enter_child`, and a leaf then runs `run_leaf`, which loops over its operations. Missing:
  const, string enum and length tests in `visit_names`, `visit_lookup`, the fused entry and the array loops,
  without the calls.
- **`true` subschemas skipped.** The fused plan treats a `true` child as cover with no test (`OptChild`,
  `app_child` in `try_fuse`). The object plan does not drop one. `additionalProperties: true` stays a child, which
  costs the inlined test of `@run_child` for each undeclared property and rules out the probe by name
  (`ObjectPlan.lookup` asks for no `additionalProperties`). `propertyNames: true` selects the general loop and is
  entered for every name. A `true` pattern property is still matched. The Java port drops each of these.
- **Keywords that cannot apply under the node's type.** Object and array keywords are dropped when `type` excludes
  objects or arrays (`plan_node`). Number and string keywords are kept whatever the type, so such a node is a leaf
  where it could be a type test.
- **Enums that are not all strings.** An enum with one value that is not a string is compared value by value
  (`OP_ENUM`, `enum_contains`). The Java port hashes larger sets, and the strings of a mixed set could still take
  the name lookup.
- **Searching for an unanchored match.** An unanchored literal is tried at every position (`index_text`), and so
  is an unanchored class sequence (`ALT_ANYWHERE` in `match_alternative`). The C# matcher skips the positions its
  first atom rejects and uses a vectorised search.
- **JSON text given as a `String`.** A vector of bytes is parsed where it is. The bytes of a `String` are first
  copied into the evaluation state's reused buffer (`parse_text!`, `Evaluator.source`), and so is any other
  `AbstractVector{UInt8}`. The Go module reads a string without a copy. The copy allocates nothing, but it is a
  pass over the text.
- **The state of a fused pass.** The Go module keeps it on the stack (its Measured 7), where the gain was not
  taking the scratch from a pool. Here there is no pool to avoid, since the evaluation state is already held, but a
  pass takes a `FusedPass` from the evaluator's stack of them (`take_pass!`, `Evaluator.passes`) and clears all of
  it for every object (`reset!`, twelve words in two vectors and five fields), whatever the plan uses.
- **Loops over the tape.** `visit_lookup`, `visit_values` and `all_of_type` hold `d.tape` in a local and read it
  by offsets from one base, as the Go module's loops over a slice of the tape do (its Measured 8). In Go the slice
  removes the bounds check per value. Here every read is still checked (see "Measured and declined").
- **Content.** Base64 content is decoded into scratch even when only its validity is asked (`content_ok`,
  `base64_decode!`). JSON content that is not base64 is copied into the content buffer before it is checked,
  because the parser reads a whole vector. The Java port checks base64 without decoding.
- **Allocation outside the common paths.**
  - A `multipleOf` whose divisor has more than 18 significant digits, or an exponent out of range, is decided with
    `Rational{BigInt}`, which allocates (`divides_slow`, `rational_of`).
  - A number with more than 19 significant digits whose truncation is ambiguous is converted by `Base.tryparse`
    on a `String` made for it (`decimal_to_float_slow`).
  - A custom format receives a copy of the string, or of a number's text (`check_format_string`,
    `check_format_number`).
  - The `regex` format makes a `String` of the value for the pattern validator (`valid_regex` for a `Bytes`). The
    validator itself is pooled and allocates nothing (`EcmaRegex.isvalidpattern`, `VALIDATORS`).
  - `idn-hostname` and `idn-email` decode the labels into new vectors of code points (`is_idn_hostname`,
    `code_points`, `punycode_encode`).
  - A host name label that starts with `xn--` is decoded and encoded again (`hostname_label`,
    `punycode_label_ok`). That is reached from `hostname` and from the domain of `email`.
  - A lookup in a name set too large for the table makes a `String` of the name (`find_long` with
    `Names.is_large`).

## Todo

Each has to be measured before it is kept.

1. **Leaves decided in the loop** (see Partial). The Go module's measurements say how not to do it there (its Tried
   and not kept, 3 and 5): more code in the body of the property loop made every property dearer. Whether Julia's
   compiler behaves the same way is not known, so the first measurement is of a leaf test reached from
   `enter_child` without growing `visit_names`.
2. **`true` subschemas dropped from the object plan** (see Partial).
3. **Equivalent subschemas canonicalised.** The C# compiler points fail-fast references at one representative of
   each set of identical subschemas, and the Java port merges structurally identical nodes. There is no counterpart
   here. `get_node!` makes one node per schema location.
4. **Unanchored search** with a first-atom skip (see Partial). `Base` has a byte search over a vector that is
   `memchr` underneath. Whether it can be used on a run inside a vector without a view or a pointer has to be
   looked at first.
5. **Mixed and large enums** (see Partial).
6. **Keywords pruned by type** for numbers and strings (see Partial).
7. **A `String` parsed where it is** (see Partial). The parser reads a `Vector{UInt8}`. Reading the code units of
   the string in place needs either a second specialisation of the parser and of `Document.source`, or a pointer
   to the string's bytes, which is an unchecked read (see "Measured and declined").
8. **Collecting mode without allocation.** The C# collector is pooled and writes its paths into reused buffers.
   Here the paths are reused byte vectors (`ResultsCollector.eval_path`, `doc_path`), but each row's locations and
   message are strings built for it (`path_text`, `evaluated_keyword!`), and `empty!` makes a new vector of rows
   where the Go module keeps its slices after `Reset`. The Go and Java ports list the same item.
9. **Base64 validity without decoding, `multipleOf` beyond 18 digits without `BigInt`, and the long-number
   conversion without a `String`** (see Partial).
10. **The fused pass.** It is the largest part of the corpora with conditionals in the other ports and is a set of
    small loops over contributors, applications and tests for each property. The C#, Rust and Go plans have the
    same shape, so there is nothing to port. The state reset is the one difference found here (see Partial).
11. **An automaton over ASCII text for the patterns on the engine** (the Go module's Measured 10). There it stood
    in for the standard library's `regexp`. Here the engine is PCRE2 with its JIT compiler, so the first
    measurement is of how much of a pass the engine takes on the corpora whose patterns reach it. The search
    without PCRE2's start optimisation is part of the same question.

## Not applicable

- **Runtime code generation** (the C# IL emitter, the Java bytecode generator, the TypeScript source generator),
  and everything that belongs to it. That is one method per node, constants as data of the generated type, an
  entry method per schema, inlining thresholds, name dispatch as generated comparisons or a trie of words, a search
  on a distinguishing window of bits, merged methods, the method size guard, the JVM's 8000-byte limit, and the
  experiments on locals and switches in the C# list. Julia can generate and load code at run time, and it was
  measured and ruled out. See "The execution model". The plans here are data that one evaluator interprets, as in
  the Rust crate and the Go module.
- **A program image, ReadyToRun and tiered compilation** (C#), **an ahead-of-time cache** (Java). These are not
  ruled out. The package image with the precompile workload is their counterpart and is under Done.
- **The Go inliner's budget of 80 and `-gcflags=-m`**, and the Go measurements that were about getting a function
  under that budget. Julia's inliner has no budget a source is written to. `@inline` and `@noinline` say it
  directly (see Done). The structure those measurements arrived at is in the source all the same (see Measured).
- **Escape analysis that keeps the evaluator on the stack, and `sync.Pool`** (Go), **stack allocation,
  `SkipLocalsInit`, `ArrayPool` and thread-static buffers** (C#), **per-thread buffers** (Rust and Java). A Julia
  task may move between threads, so nothing of an evaluation is kept by thread. The `Evaluator` is a heap object
  the `Validator` owns and reuses (see Done, Allocation). The one per-thread state is the engine's match memory
  (`threadframe`), which is sound because a match is one C call that does not yield.
- **Profile-guided optimisation** (the Go module's Tried and not kept, 9). Julia has no counterpart.
- **Generic specialisation over a document type** (C#), **the `Instance` trait and monomorphised fail-fast and
  collecting evaluators** (Rust). Instances are always `Document` values, and every hot function takes concrete
  argument types, so Julia compiles one specialisation of each. Fail-fast evaluation runs the plans, and the
  general evaluator serves collecting.
- **One row on after a scalar, integers read in one pass, integer bounds and consts with no conversion** (the C#
  code generator). They work around metadata rows of varying size and numbers kept as text. Here every value is two
  words, a container's children are consecutive, and a number's value is in the tape.
- **Translation to RE2 with an automaton and a backtracking matcher** (Go), **translation to `java.util.regex`
  with reused matchers** (Java), **the `regex` and `regress` crates** (Rust), **compiled IL regular expressions**
  (C#). `EcmaRegex` over PCRE2 is this port's engine. The automaton alone is Todo 11.
- **A vectorised search API** (`SearchValues` and `IndexOfAny` in C#). The source has no `@simd` loop and no
  vector API. Todo 4 would use the search `Base` has.
- **Release profile settings** (the Rust crate's `lto` and `codegen-units`). The optimisation level belongs to the
  process that loads the package. The package sets none (no `@optlevel`, no `@fastmath`, no `@assume_effects`).

## The execution model

Julia compiles each generated validator-shaped function through LLVM before its first run, at 25 to 50 ms per
function. 900 functions took 45 s at the default optimisation level and 20 s at level 0, on Julia 1.13.1. So a
schema is not turned into generated Julia code. It is interpreted from compiled plans by one type-stable
interpreter, and the interpreter is precompiled into the package image.

- **The precompile workload** (`precompile.jl`). `precompile_workload` runs only while Julia is writing the package
  image (`jl_generating_output`). It compiles schemas that between them use every kind of plan (strict, lookup,
  general and pattern object loops, fused plans with conditions and alternatives, flat composition, static and
  tracked `unevaluated*`, a dynamic reference, every format, content, an engine pattern), and validates and
  evaluates instances by every entry point (`isvalid` and `validate` for a `Document`, a `String` and a vector of
  bytes, `evaluate` at each level, the metaschemas, the errors). It keeps nothing but the compiled code. The
  pattern cache, the parser pool and the IDN tables are emptied, since a compiled engine pattern holds memory of
  the process that made it. A pattern that does come back from an image with no code is compiled again on its
  first match (`restore!`). The package has no dependencies, so the workload is plain code and not a macro of
  another package.
- **Handles and values.** What a loop reads once per value is a handle, a `mutable struct` whose fields are
  `const`, so that the loop reads a reference and not a copy of a wide structure. These are `Names`, `Pattern`,
  `Divisor`, `Op`, `FusedContributor` and `SeparatedList`. The plans that are fixed up after they are built are
  plain mutable structures (`ObjectPlan`, `ArrayPlan`, `FusedObject`, `Body`, `Plan`, `Program`), and so are
  `Document`, `Parser`, `Evaluator` and `FusedPass`. What is small is an immutable value with no reference in it,
  stored inline in its vector. These are `Child` (eight bytes), `NameKey`, `Bitset`, `Gate`, `AltBranch`,
  `OptChild`, `OptCount`, `FormatCheck`, `SimpleArray`, `CharSet` and `SequenceItem`. `Bytes` is an immutable
  value that holds the vector, so reading a string makes no object.
- **What an evaluation reads side by side.** The evaluator holds the program's bodies, children and guards as
  three vectors of its own (`Evaluator.bodies`, `selfs`, `guards`), so entering a node by its id is one read and
  not a walk through the program and the plan.
- **Strings.** A string is never a Julia `String` on the validation path. It is a `Bytes` over the document's
  source or its unescaped text, read by index (`at`, `le64`, `le32`). `le64` and `le32` are written as checked
  single-byte reads put together, with no `unsafe_load`. The engine is the one place that takes a pointer to the
  bytes (through `view`, `pointer` and `GC.@preserve`).
- **Tasks.** A `Validator` may be used from several tasks at once. Each validation owns one `Evaluator` from start
  to end (see Done, Allocation), the process-wide caches are behind locks (`PATTERN_CACHE_LOCK`, `PARSERS_LOCK`,
  `IDN_TABLES_LOCK`, the engine's `VALIDATOR_LOCK`, `FRAME_LOCK` and `RESTORE_LOCK`), and the annotations of a
  program are built once under `Program.annotations_lock`. The source spawns no task and has no threaded loop.
- **What is not used.** There is no `@simd`, no `Base.@propagate_inbounds`, no `@generated` function and no
  `@nospecialize` in the source. `@inbounds`, `unsafe_*` and `GC.@preserve` are listed under "Measured and
  declined".

## Measured

Warm validation over the 37 jsonschema-benchmark corpora, 2026-10-08 and 2026-10-09, linux/amd64. Every figure is
an A/B of two builds, each with its own project environment and package image. One process per build runs the
benchmark protocol's loop over every corpus (600 ms of warm-up passes for each, at least 100), the two builds
alternate for five rounds, pinned to eight cores, and the figure is the median over the rounds of the median pass,
the change over what was there before it, as a geometric mean over the corpora with the range. Julia 1.13.1 unless
1.10.12 is named.

A build measured against a second checkout of the same commit gave 1.001, with corpora between 0.934 and 1.102. So
one corpus inside about 0.93 to 1.10 did not move in a single run of five rounds, and a geometric mean inside 0.99
to 1.01 is nothing. The noise is larger than the Go module's, because every process places the code and the heap
differently.

1. **A word read as one load** (`le64`, `le32` in `document.jl`). Eight checked byte reads were eight tests and
   eight loads. With the offset's sign tested first and the last byte read first, the compiler proves the other
   reads in range, and the eight become one test and one load on Julia 1.13 and two tests and one load on Julia
   1.10 (see "Bounds checks the compiler removes"). 0.978 (0.879 to 1.049), parse 0.981.

2. **The test of a type-only child written where the child is applied** (`@run_child`, `@run` in
   `plan_types.jl`). `run_child`, `run` and `apply_opt` were functions marked `@inline`, and in the package image
   on both Julia versions they were calls all the same: the property loops called `run_child`, which tested the
   kind and called `enter_child`. They are part of the cycle of functions that evaluate a value, and Julia does not
   inline a call to a function of the cycle it is compiling. `code_typed` and `code_native` in a session do not
   show this, since they compile the caller again when the whole cycle is known. `objdump` of the package image
   and `perf annotate` do. They are now macros, so the test is in the loop whatever the compiler decides. 0.941
   (0.864 to 1.020). This is the Go module's Measured 2, which this port had in its source and not in its code.

3. **A name's key read with two loads, and the search called with the key** (`word_at`, `second_word_at` in
   `document.jl`, `name_word`, `second_word`, `find_next`, `find_after`, `rest_equal` in `names.jl`). Counting the
   instructions of a pass with cachegrind showed a search for a name at 112 instructions, and half the properties
   of some corpora searched. A name of four to seven bytes was read as two overlapping halves and a long name's
   last eight bytes at an offset worked out from its length, and for a word at such an offset the compiler keeps
   all eight checks and loads (see "Bounds checks the compiler removes"). A name's word is now its first eight
   bytes read as one word and masked to its length, which reads the text after a short name where the vector has
   it, and a long name's second word is bytes 8 to 15 read the same way at a constant distance from the same base.
   The names of a set carry eight zero bytes after them so that the rest of a name longer than sixteen bytes is
   compared the same way (`Names.padded`). The test that some name has the length is made in the loop, where it
   was the first thing the call did, and its shift is masked so that it is one instruction where it was twelve
   (`length_bit`). The call takes the length and the two words and returns the index, where it took the name as
   three words in memory and returned two values through memory. 0.895 (0.771 to 1.056), compile 0.970.
   Instructions per pass over 8 corpora: 0.848 (omnisharp 0.724, helm-chart-lock 0.783). The Go module tried the
   masked read of a short name and reverted it (its Tried and not kept 2). There a checked eight-byte read is one
   test whatever the offset, so the branch on the length was all it replaced.

The Go module's ten measured changes are techniques too. Its figures are for Go and are not repeated here. This is
what this port's source has for each.

1. **The type test inlined.** Present. `type_ok` is `@inline` and the integer test is the call (`integer_ok`,
   `@noinline`).
2. **A type-only child decided where it is applied.** Present since Measured 2 above. `@run_child` is one test of
   the value's kind against `Child.pass`, written into the loop by a macro, and `enter_child` is the one call for a
   child with keywords.
3. **The expected name tested in the property loops.** Present. `find_next` is inlined into `visit_names`,
   `visit_general` and `run_fused_pass` with `name_at` and `name_rest`, the name's length and word are side by side
   in `Names.keys`, and `find_after` is the call.
4. **Names found through a hash of their key.** Present, in the form the Go module kept (`Names.table`,
   `name_hash`, see Name lookup under Done).
5. **The search of the name table written out in its two callers.** Present. `find` and `find_after` each have the
   probe loop in them, and a name over sixteen bytes or a set held in a `Dict` goes to `find_long` or
   `find_after_long`.
6. **Values entered through one function, and a strict object's loop called from it.** Present. `@run` is
   `enter_child` on the node's own child (`Evaluator.selfs`), a node with a fused plan has a shape of its own
   (`SHAPE_FUSED`), `enter_child` calls `visit_lookup` or `visit_names` for a strict object itself, and a child
   takes the shape its node has after the fused plans are built.
7. **The state of a fused pass on the stack.** Not in that form. See Partial.
8. **A value's header read once, and loops over a slice of the tape.** The header is read once in `enter_child`,
   which hands the first child and the count to the loop. The loops over one base of the tape are there, with
   checked reads. See Partial.
9. **Conditions of a fused pass as masks.** Present (`FusedApp.then_` and `els`, `FusedContributor.then_` and
   `els`, `FusedPass.then_` and `els`, `applies`, `FusedObject.finals`).
10. **An automaton over ASCII text for the patterns on the engine.** Absent. Todo 11.

## Tried and not kept

Nothing has been tried and reverted in this port. The Go module reverted nine experiments. For each, this is what
the Julia source has, which in every case is the form the Go module kept. None has been tried again here, and a Go
result does not decide a Julia one.

1. **The tape as one struct of two words per value.** The tape is a `Vector{UInt64}` with two words per value.
2. **A name's word read with no branch on its length.** Kept here, where the Go module reverted it. See Measured
   3.
3. **The properties that need no call decided in a function that calls nothing.** There is no such function.
   `visit_names` is one loop.
4. **The expected name's word read from the text in place.** The name is taken as a `Bytes` first (`str`), then
   `name_word` reads it. A `Bytes` is three values in registers here, not an object.
5. **The sorted-order prediction tested in the loops.** It is in `find_after`, not in the loops.
6. **A pointer to the node's keywords in the child.** `Child` is eight bytes and the body is read through
   `Evaluator.bodies` in `enter_child`.
7. **`visit_names` and the array item loop over a slice of the tape.** `visit_names` and the item loop of
   `run_array` read through the document.
8. **A perfect hash for the names, and linear probing with the keys in the slots.** Neither is here. The slots
   hold indexes and the keys are in `Names.keys`.
9. **Profile-guided optimisation.** Not applicable (see Not applicable).

## Measured and declined

**Unchecked reads.** `@inbounds` on the tape reads and the byte reads was measured at 0.82 to 0.94 of the checked
time on most of 12 corpora, 1.00 on two and 0.59 on one, and declined on 2026-10-08. A mistake in one would read
out of bounds where a checked read throws. The Go module made the same decision for the same reason, and the Rust
crate reads its tape and text unchecked.

What the source does today. Every read of the tape and of a document's bytes is checked. That covers the parser,
the evaluator, the plans, the fused pass, the name table, the pattern shapes, the formats and the values
(`document.jl`, `plan.jl`, `fused.jl`, `eval.jl`, `names.jl`, `pattern.jl`, `formats.jl`, `values.jl` have no
`@inbounds` and no pointer). The pattern submodule, `src/ecmaregex`, has no `@inbounds` either: the six it was
ported with were removed when the decision was made to cover it too (2026-10-08). What remains unsafe there is the
binding to PCRE2, which cannot be otherwise.

- Pointers. `pcre.jl` is the binding to PCRE2 and is unsafe by nature. It has 8 `unsafe_load`, 7 `unsafe_store!`
  and one `unsafe_pointer_to_objref` over the `Frame` memory it allocates with `Libc.malloc` and over the callout
  block PCRE2 passes, and 13 `ccall`. `EcmaRegex.jl` takes the pointer to the text and to the pattern under
  `GC.@preserve` in the two `ismatch` methods and calls `unsafe_ismatch`.
- `unsafe_trunc` appears four times outside the engine (`numbers.jl` twice, `values.jl`, `compiler.jl`). It
  converts a `Float64` to an integer with no range check and reads no memory. Each is behind a test of the range.

The decision covers every array and string read of the package. Do not add `@inbounds` anywhere in it.
