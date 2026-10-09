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

- **Instance representation** (`document.jl`). A flat tape, `Document.tape`, one `TapeValue` of two `UInt64` words
  per value. The header has the kind in the low byte (the `KIND_` constants are the evaluator's type bits), flags,
  and a 32-bit count or length in the high half. The second word is the data. A container's children are
  consecutive, and an object's are name and value pairs. Strings are read in place where they have no escapes
  (`STR_TEXT` marks the others, which `escaped!` unescapes into `Document.text`), and the parser records whether a
  string has a byte outside ASCII (`STR_WIDE`). Strings are scanned eight bytes at a time (`string!`, with
  `SWAR_ONES` and `SWAR_HIGHS`), then by the `SCAN` table. Numbers are classified at parse as an `Int64`, a `UInt64`
  beyond an `Int64`, or a `Float64` (`NUM_INT`, `NUM_UINT`, `NUM_FLOAT`), and the header keeps the offset of the
  text for exact decimals. The parser is iterative, with its open containers on its own stack (`parse!`,
  `Parser.frames`, `close!`). Duplicate names are found pairwise up to 16 properties and by sorted hashes in scratch
  beyond that (`dedupe!`, `sort_words!`). `parse_document` sizes the document exactly (`parse_new!`) with a parser
  from a pool (`take_parser`, `return_parser`), and the validator's text entry points parse into reused arrays
  (`parse_into!`). A string value is read as a `Bytes`, which is the vector, an offset and a length, with no copy
  (`str`).
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
  keywords, an object plan, an object plan that is one strict loop, an array plan, in-place applicators, or a fused
  object plan. Every child takes the
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
- **The strict object loop** (`run_strict_object`, and the same lines in `enter_child`). Declared names,
  `additionalProperties` for the rest and the required mask, entered directly by a nested object, with the plan
  read from `Evaluator.strict` and nothing else of the node (`SHAPE_STRICT`, see Measured 4).
- **Small objects probed by name** (`visit_lookup`, `LOOKUP_NAMES`, `LOOKUP_BUDGET`, `lookup_limit`). A plan of at
  most 4 names and no `additionalProperties` looks each name up in the instance when the instance's properties
  times the names is at most 24, comparing a name's length from its header, then its word, then its text.
- **Name lookup** (`names.jl`, the C# `Utf8NameMap`). A set of the lengths the names have, modulo 64, settles most
  misses of a small set with one test (`Names.lengths`, `length_bit`). A name's key is its length, its first eight
  bytes as a word and, beyond eight bytes, its second word (`NameKey`, `name_word`, `second_word`), which is the
  whole name up to sixteen bytes. A longer name has only the bytes after its first sixteen compared, a word at a
  time (`rest_equal`). The loops try the name after the previous match first, in the loop itself (`find_next` with
  `name_at` and `name_rest`), for as long as the object's names come in the schema's order (see Measured 5), then a
  hash table from the key to the index (`Names.table`, `name_hash`, `find_key`). The table has four to eight slots
  for each name and its multiplier is the one of 24 that leaves the fewest names away from their first slot
  (`fill_table!`, `NAME_HASH_MULTIPLIERS`), so a search is one multiplication and one read to the index. A set of
  more than `MAX_TABLE_NAMES` names is a `Dict` (`Names.is_large`).
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
  the count and first child are handed to the property loop. A function that returns two values returns them
  through memory, so the loops that are called for every object return one (`visit_names`, `visit_lookup`,
  `find_key`), and a shift by a count the compiler cannot bound is masked (`length_bit`, the `seen` bits).
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
  from one base, as the Go module's loops over a slice of the tape do (its Measured 8). In Go the slice removes the
  bounds check per value. Here every read is checked in the source, and the compiler removes the check from the
  loop of `all_of_type` itself on Julia 1.13 (see "Bounds checks the compiler removes"). It does not from a loop
  that has a call in it.
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
   and not kept, 3 and 5). More code in the body of the property loop made every property dearer. Julia's compiler
   behaves the same way. A test of the child's shape in the loop, to call a strict object's loop from it, cost every
   other property as much as it saved (Tried and not kept 7). So a leaf test has to be reached from `enter_child`
   without growing `visit_names`, and what `enter_child` costs to enter is the first thing to cut (Todo 11).
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
11. **What an object costs before its first property, and a document before its first value.** After Measured 4
    `enter_child` is 75 instructions for an object of a strict plan and `visit_names` or `visit_lookup` about as
    many again before and after their loops, of which half is saving registers, the frame the collector reads and
    the safepoint. `run_validation` is 85 for a document. A small document is mostly this (see "Where the time is
    after them"). The attempts to shorten `visit_lookup` are under Tried and not kept, 4 to 6.
12. **An automaton over ASCII text for the patterns on the engine** (the Go module's Measured 10). There it stood
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
  (C#). `EcmaRegex` over PCRE2 is this port's engine. The automaton alone is Todo 12.
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
  source or its unescaped text, read by index (`at`, `le64`, `le32`, `word_at`). `le64` and `le32` are written as
  checked single-byte reads put together, with no `unsafe_load`, in the order that lets the compiler join them
  into one load (see "Bounds checks the compiler removes"). The engine is the one place that takes a pointer to the
  bytes (through `view`, `pointer` and `GC.@preserve`).
- **Tasks.** A `Validator` may be used from several tasks at once. Each validation owns one `Evaluator` from start
  to end (see Done, Allocation), the process-wide caches are behind locks (`PATTERN_CACHE_LOCK`, `PARSERS_LOCK`,
  `IDN_TABLES_LOCK`, the engine's `VALIDATOR_LOCK`, `FRAME_LOCK` and `RESTORE_LOCK`), and the annotations of a
  program are built once under `Program.annotations_lock`. The source spawns no task and has no threaded loop.
- **What the image has is not what a session shows.** `code_typed`, `code_llvm` and `code_native` compile a
  function again in the session that asks, when every function it calls is known. The package image was compiled
  while the functions of a cycle were still being inferred, and a call into the cycle is not inlined there
  (Measured 2). The image is a shared object. `objdump -d` of the file `Base.pkgorigins` names for the package
  shows its code, `perf` reads its symbols, and valgrind's cachegrind and callgrind count its instructions when the
  image is built for a processor valgrind runs (`JULIA_CPU_TARGET=generic`, in a depot of its own).
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

1. **A word read with one bounds test** (`le64`, `le32` in `document.jl`). Eight checked byte reads were eight
   tests. With the offset's sign tested first and the last byte read first, the compiler proves the other reads in
   range, and a word is the sign test, one bounds test (two on Julia 1.10) and one load (see "Bounds checks the
   compiler removes"). 0.978 (0.879 to 1.049), parse 0.981. Julia 1.10.12: 0.996, parse 0.978.

2. **The test of a type-only child written where the child is applied** (`@run_child`, `@run` in `plan_types.jl`).
   `run_child`, `run` and `apply_opt` were functions marked `@inline`, and in the package image on both Julia
   versions they were calls all the same. The property loops called `run_child`, which tested the kind and called
   `enter_child`. They are part of the cycle of functions that evaluate a value, and Julia does not inline a call to
   a function of the cycle it is compiling. `code_typed` and `code_native` in a session do not show this, since they
   compile the caller again when the whole cycle is known. `objdump` of the package image and `perf annotate` do.
   They are now macros, so the test is in the loop whatever the compiler decides. 0.941 (0.864 to 1.020). Julia
   1.10.12: 0.928 (0.833 to 1.023). This is the Go module's Measured 2, which this port had in its source and not in
   its code.

3. **A name's key read with two loads, and the search called with the key** (`word_at`, `second_word_at` in
   `document.jl`, `name_word`, `second_word`, `find_next`, `rest_equal` in `names.jl`). Counting the
   instructions of a pass with cachegrind showed a search for a name at 112 instructions, and half the properties of
   some corpora searched. A name of four to seven bytes was read as two overlapping halves and a long name's last
   eight bytes at an offset worked out from its length, and for a word at such an offset the compiler keeps all
   eight checks and loads (see "Bounds checks the compiler removes"). A name's word is now its first eight bytes
   read as one word and masked to its length, which reads the text after a short name where the vector has it, and a
   long name's second word is bytes 8 to 15 read the same way at a constant distance from the same base. The names
   of a set carry eight zero bytes after them so that the rest of a name longer than sixteen bytes is compared the
   same way (`Names.padded`). The test that some name has the length is made in the loop, where it was the first
   thing the call did, and its shift is masked so that it is one instruction where it was twelve (`length_bit`). The
   call takes the length and the two words and returns the index, where it took the name as three words in memory
   and returned two values through memory. 0.895 (0.771 to 1.056), compile 0.970. Julia 1.10.12: 0.909 (0.767 to
   1.017). Instructions per pass over 8 corpora: 0.848 (omnisharp 0.724, helm-chart-lock 0.783). The Go module tried
   the masked read of a short name and reverted it (its Tried and not kept 2). There a checked eight-byte read is
   one test whatever the offset, so the branch on the length was all it replaced.

4. **An object of a strict plan entered without reading the node's keywords** (`SHAPE_STRICT`, `Evaluator.strict`,
   `strict_plan`, `ObjectPlan.lookup_max`, `Evaluator.seen`). `enter_child` was 111 instructions for an object
   before its first property. It read the node's `Body` from a vector of `Union{Nothing,Body}`, tested it for
   `nothing`, read the object plan from a field that is a `Union` too and asserted its type, worked out whether to
   probe by name from the number of names, and took the names seen and the outcome back from the loop as a pair,
   which Julia returns through memory. A node whose object keywords are one strict loop now has a shape of its own
   and its plan in a vector of plans by node, the limit of the probe is a number in the plan, and the loops
   (`visit_names`, `visit_lookup`) test the required names themselves and return a `Bool`, leaving the names seen in
   the evaluator for the one caller that goes on to dependencies. 0.982 (0.927 to 1.037). Julia 1.10.12: 0.987
   (0.864 to 1.041). Instructions per pass over 8 corpora: 0.959 (yamllint 0.886).

5. **The expected name given up for an object whose names are not in the schema's order** (`find_next`, `find_key`,
   `length_bit`). Counting what each instruction of the property loop ran showed the expected name to be the name
   for one property in six of one corpus (jshintrc), and the names of most corpora to come in an order of their own.
   Each test that failed went on to the test of the length set, the test of the next name in sorted order and then
   the table. Now a name found before the one expected sets the hint to -1 for the rest of the object, which fails
   the test of the expected name at its first comparison. A name found after the one expected keeps the hint, so an
   instance that lists a subset of the properties in the schema's order still has each name found by one comparison.
   The sorted-order test is gone (`Names.sorted_next`), since the table costs no more than it did and finds every
   name. The length set is by length modulo 64, which is one bit test. 0.981 (0.887 to 1.061). Julia 1.10.12: 0.972
   (0.849 to 1.023). Instructions per pass over all 37: 0.971 (cypress 0.896, stale 0.910, jshintrc 0.919).

6. **The parser's values appended a word at a time, and a string's last bytes scanned only at the end of the text**
   (`string!` in `document.jl`, and `push_words!` and `append_words!`, which 7 made `push_value!` and
   `append_values!`). This is a parse change. Counting the instructions
   of a parse showed a seventh of them in `copyto!`. `push!` of two items is `append!` of a tuple, which goes
   through the general `copyto!`, at more than fifty instructions a word, and every value was pushed that way. A
   closed container's children were moved to the tape with one `push!` for each word. A value is now two pushes of
   one word, and a container's children are one `resize!` and one `copyto!` of a vector into a vector. In `string!`,
   the byte loop for the last bytes of the text ran its set-up for every string, though a run that ends inside a
   word (every string but one at the very end of the text) never enters it. Parse passes over the 37 corpora, the
   same A/B method with the parse of every instance as the pass: 0.846 (0.764 to 0.966). Julia 1.10.12: 0.895 (0.834
   to 1.034). Instructions per parse over 4 corpora: 0.75 to 0.86. Warm validation 0.997, which is no change.

7. **The tape as one element of two words per value** (`TapeValue`, `Document.tape`, `push_value!`,
   `append_values!`, the short path of `all_of_type`). A value's header and data were two elements of a vector of
   words, so reading both (a string, a container's count and first child, a number) was two indexes and two bounds
   checks, and the parser pushed twice for a value. They are now one element. The loop over the items of a simple
   array changed with it. For the new element type the compiler works out before the loop the range of items it
   can read without a check, about thirty instructions, which is a loss for the two or three numbers of a position.
   Up to three items are now tested one by one. Warm validation 0.990 (0.905 to 1.055), which alone is at the edge
   of what a run shows, and parse 0.972 (0.887 to 1.000). On Julia 1.10.12, where `push!` costs more, warm 0.991
   and parse 0.870. Instructions per validation pass over all 37: 0.975 without the short path, with it geojson
   0.927. The Go module tried the same layout and reverted it (its Tried and not kept 1) for one corpus that got
   slower. Nothing did here beyond the noise.

8. **The engine's callout made only for a pattern that has one** (`calloutframe`, `Frame.callout`, `threadframe`
   in `src/ecmaregex/pcre.jl`). This is a change to the cold pass. Timing each document's first validation showed
   one document of every corpus with a pattern on the engine at 3.3 to 3.6 ms, where the whole pass afterwards is
   0.3 to 0.8 ms. It was the first match of a pattern in the process, which made the first frame of the thread,
   and with it the C function PCRE2 calls for a callout. Julia takes 5 to 9 ms to make that function in a process,
   however it is written (a `@cfunction` in a precompiled method costs the same). Only a pattern with a lookbehind
   of no fixed length has callouts. The function is now made when such a pattern is first matched, and set in a
   thread's frame then. The cold figure of the protocol program, fresh processes, three alternating runs: krakend
   7.56 ms to 0.17, jsconfig 6.95 to 0.31, cspell 7.69 to 0.42, ui5 8.19 to 0.83, ui5-manifest 9.09 to 2.06, and
   corpora with no pattern on the engine as before (helm-chart-lock 0.958, jshintrc 0.986). Warm validation is
   not touched. `threadframe` also reads the thread's frame again after making it, since making it takes a lock a
   task may wait on and be resumed on another thread.

**The eight together.** The commit after 8 against the commit before 1, Julia 1.13.1.

- Warm validation, the A/B method above over all 37 corpora (after 7, since 8 does not touch it): 0.762 of the
  time, corpora from 0.562 (cypress) to 0.924 (yamllint). The warm figure of the benchmark's own protocol program
  (its last pass after 2 seconds), one fresh process for each corpus and build, three alternating runs: 0.748.
  Instructions per pass by cachegrind, from after 2 to after 8: 0.787.
- Parse, compile and cold as the protocol has them, the first of each in a process. One fresh process for each
  corpus and build, five alternating runs, with a collection forced before the compile and before the cold pass:
  parse 0.864 (0.752 to 1.138), cold 0.642 (0.045 to 1.038), compile 1.024 (0.870 to 1.185). Two checkouts of the
  commit before 1 measured against each other the same way gave parse 1.044, cold 0.985 and compile 1.043 (0.937
  to 1.164), so the compile figure is inside what two builds of one source differ by. A compile executes 0.91 to
  0.96 of the instructions it did (cachegrind, 8 corpora), and the first compile of a process fewer too.
- Without the forced collections, the protocol program's compile for one corpus (openapi) was 10.4 ms against 1.04,
  and in a repeat its cold pass 13.3 ms against 6.3. That is one collection of about 8 ms. Before, it fell inside
  the parse of the instances. The parser now allocates less, and the same collection falls in whichever phase
  allocates next. The total is no more, but a figure of the protocol can show it.
- Julia 1.10.12, the same fresh processes: warm 0.773 (0.574 to 0.957), parse 0.787, cold 0.650, compile 0.991.
  The A/B over all 37 after 7: warm 0.782 (0.550 to 0.962).
- The time from the start of a process to its first validation did not change: 189.8 ms before and 191.3 after on
  Julia 1.13.1, of which the package's load is 43.6 and 43.9 and the compile of a schema with the first validation
  10.36 and 10.39 (medians of 15 alternating processes). Julia 1.10.12: 215.2 and 206.4. A validation still
  compiles nothing.

**Where the time is after them.** Instructions per pass by cachegrind, the mean share over the 37 corpora. The
property loops and the search for names 47 percent (`visit_names` 31, which has the search in it, `visit_lookup` 6,
`find`, `find_long` and `rest_equal` 6, `visit_general`, `visit_values`, `run_object`). Entering values 24
(`enter_child` 18, `run_validation` 4, `enter`). Arrays 8 (`run_array` 5, `all_of_type`). Applicators 6. Strings
and patterns 6. The fused pass 4.

What a value costs before and after its own work is now most of it. Counted instruction by instruction for a
corpus of small objects: `run_validation` is 85 instructions for a document, `enter_child` 75 for an object of a
strict plan, and `visit_lookup` 131 for an object of two properties with one name to find, of which the search is
about 55. In each, a third to a half is the entry and exit of the function. That is saving registers, the frame the
collector reads its roots from (two to seven references), the safepoint, and values moved to the stack and back
because the function has more of them alive than there are registers. The corpora with the smallest documents or
the most small objects are the ones this weighs on most (yamllint 289 instructions a document, importmap 656,
helm-chart-lock 1731 for four objects and an array). They are also the ones furthest from Blaze in the container
baseline of 2026-10-08, with ui5, and scaling that baseline by the ratios above (an estimate, not a measurement of
Blaze) leaves those four behind it.

**What the gap to the Go module is made of.** The two interpret the same plans, and after these changes the same
structure runs, with the same loops, the same calls and the same tape. The items below are what the code Julia
generates spends around that structure. The Go module's generated code was not read for this, so how much of each
Go avoids is not measured.

- A call. Every compiled function starts with a safepoint, which is three loads. A function that holds a reference
  across a call also builds a frame for the collector, clears it, links it and stores each reference in it
  (two references in `enter_child`, six in `visit_lookup` and seven in `visit_names`).
- A vector is an object. `d.tape[i]` loads the vector's length and its data from the vector, and loads them again
  after every call, because a vector can grow. The `Document` is itself a reference in the evaluator, so a read
  after a call is three loads before the element.
- A bounds check that fails names the vector and the index, so each check has an exit of its own that needs them,
  and the loops with calls in them have more values alive than registers. `visit_names` has a frame of 272 bytes
  and `visit_lookup` one of 304, and both move values to it and back on every pass of their loops.
- Two values come back from a function through memory, and the three words of a `Bytes` go to one through memory.

**What parse and cold are made of.** Parse, after 6 and 7, by instructions for a corpus of small documents: the
string scan 38 percent (about 50 instructions for the bytes of a string and 115 for the rest of the call, which are
its entry, its frame and the push of the value), the value loop 18, property names 13, closing containers 8,
duplicate names 6, growing vectors 4, allocation of the document 4. The parser keeps its position and its text in a
mutable structure, so each step loads and stores them. Those loads, and the per-string overhead, are what is left.

Cold is the first pass over the instances after the compile. For a corpus with no pattern on the engine it is the
warm pass and the cost of running it for the first time, with no allocation and no page fault: 418 microseconds
against 369 (helm-chart-lock), 232 against 167 (jshintrc), 45 against 13 (yamllint). That is 30 to 65 microseconds
whatever the corpus, which is the processor meeting the code and the documents for the first time. A corpus with a
pattern on the engine also pays for the thread's first match, which makes its frame with a stack for PCRE2's
compiled code. That is 30 to 50 microseconds since 8, and was 3.3 to 3.6 ms before it.

The Go module's ten measured changes are techniques too. Its figures are for Go and are not repeated here. This is
what this port's source has for each.

1. **The type test inlined.** Present. `type_ok` is `@inline` and the integer test is the call (`integer_ok`,
   `@noinline`).
2. **A type-only child decided where it is applied.** Present since Measured 2 above. `@run_child` is one test of
   the value's kind against `Child.pass`, written into the loop by a macro, and `enter_child` is the one call for a
   child with keywords.
3. **The expected name tested in the property loops.** Present. `find_next` is inlined into `visit_names`,
   `visit_general` and `run_fused_pass` with `name_at` and `name_rest`, the name's length and word are side by side
   in `Names.keys`, and `find_key` is the call.
4. **Names found through a hash of their key.** Present, in the form the Go module kept (`Names.table`,
   `name_hash`, see Name lookup under Done).
5. **The search of the name table written out in its two callers.** Not in that form. The probe loop is one
   function that takes a key (`find_key`), which `find` calls and the compiler puts into the property loops, and a
   name over sixteen bytes or a set held in a `Dict` goes to `find_long`.
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
10. **An automaton over ASCII text for the patterns on the engine.** Absent. Todo 12.

## Tried and not kept

Each was measured as under Measured and reverted. "Instructions" is the instruction count of a validation pass by
cachegrind. A change that did not move the instruction count by more than about one percent was not timed, since a
timing run cannot show it.

1. **An evaluation state for each thread, taken without an atomic exchange.** `perf` put 70 percent of the samples
   of `run_validation` on the instruction after the `lock cmpxchg` that takes the validator's state, and
   `run_validation` is a third of the smallest corpus. A state for each thread by thread id, taken with a plain
   read and a plain write of a flag that only tasks on that thread set, removed the exchange. 1.026 over 10
   corpora, and with nine rounds on the four corpora of the smallest documents 1.036 (importmap 1.092, yamllint
   1.015), compile 1.10. So the samples on that instruction were skid. Sampling without hardware counters (WSL2)
   charges an instruction with what the ones before it cost. Instructions were counted from then on.
2. **`const` on the reference fields of the plans and the evaluator** (`ObjectPlan.names` and `children`, the
   vectors of `Body`, `Evaluator.bodies` and the like), in the hope that a reference read from a field that cannot
   change needs no slot of its own in the frame the collector reads. Instructions 1.006 over 8 corpora. The frames
   were as large.
3. **The table searched without the sorted-order test**, on its own, before Measured 5. Instructions 0.994 over 8
   corpora. It is in Measured 5, where giving up the expected name is what pays.
4. **The budget of the probe by name** (`LOOKUP_BUDGET` 8 and 12 in place of 24). Instructions 0.998 each over 8
   corpora. With no probe at all: tmuxinator 0.935 and dependabot 0.943, ui5 1.043 and yamllint 1.224. So the
   probe pays for an object of two or three properties and costs for some others, and the budget is not what
   tells them apart. What `visit_lookup` costs is not its search but what it does before and after it: 75
   instructions of 131 for an object of two properties.
5. **The search of the probe in a function of its own**, so that `visit_lookup` has fewer values to keep. Called
   for each name: instructions 1.044 over 8 corpora (tmuxinator 1.122, yamllint 1.095). Called once, returning the
   position of every name packed in a word: yamllint 1.143, dependabot 1.060, code-climate 1.043.
6. **The probe's references read for each name** and not held across the call that applies a child. Instructions
   1.005 over 8 corpora, and the function's frame was as large as before.
7. **A strict object's loop called from the property loop and the item loop**, without the call to `enter_child`
   in between (a macro that tested the child's shape and the value's kind where `@run_child` is). Instructions:
   helm-chart-lock 0.958, code-climate 0.965, dependabot 0.974, and babelrc 1.019, geojson 1.018, importmap 1.015,
   jshintrc 1.012, about 0.995 over 16 corpora. What a nested object saves, every other property and item pays
   for the larger loop body. The Go module found the same (its 3 and 5).

The Go module reverted nine experiments. For each, this is what the Julia source has. Two of them were tried here
and kept (1 and 2), and a Go result does not decide a Julia one.

1. **The tape as one struct of two words per value.** Kept here, where the Go module reverted it. See Measured 7.
2. **A name's word read with no branch on its length.** Kept here, where the Go module reverted it. See Measured
   3.
3. **The properties that need no call decided in a function that calls nothing.** There is no such function.
   `visit_names` is one loop.
4. **The expected name's word read from the text in place.** The name is taken as a `Bytes` first (`str`), then
   `name_word` reads it. A `Bytes` is three values in registers here, not an object.
5. **The sorted-order prediction tested in the loops.** There is no sorted-order prediction any more (Measured 5).
6. **A pointer to the node's keywords in the child.** `Child` is eight bytes and the body is read through
   `Evaluator.bodies` in `enter_child`.
7. **`visit_names` and the array item loop over a slice of the tape.** `visit_names` and the item loop of
   `run_array` read through the document.
8. **A perfect hash for the names, and linear probing with the keys in the slots.** Neither is here. The slots
   hold indexes and the keys are in `Names.keys`.
9. **Profile-guided optimisation.** Not applicable (see Not applicable).

## Bounds checks the compiler removes

Every read of the tape and of a document's bytes is a checked index (see "Measured and declined"). What follows is
what the compiler makes of the forms a checked read can be written in, read from the code it generates
(`code_native` for the small functions below, and the disassembly of the package image for the loops). "Tests" are
conditional jumps on the path that returns. Julia 1.13.1 and 1.10.12, x86-64.

Eight bytes of a vector as one word. In every form the eight loads become one once the tests before them are out
of the way, so what differs is the number of tests:

| Form | Julia 1.13 | Julia 1.10 |
|---|---|---|
| The eight bytes in order, `b[i+1]` first | 8 tests | 8 tests |
| `i >= 0 \|\| throw`, then `b[i+8]` first and down to `b[i+1]` (`le64`) | 2 tests | 3 tests |
| `b[i+8]` first and down, with no test of the sign | 8 tests | 8 tests |
| `b[i+1]` first, then `b[i+8]` and down | 8 tests, and 8 loads | 2 tests |
| `(i >= 0 && i + 8 <= length(b)) \|\| throw`, then in order | 10 tests | 10 tests |
| `n = length(b); (n >= 8 && i % UInt <= (n - 8) % UInt) \|\| throw`, then in order | 2 tests | 10 tests |
| `checkbounds(b, i+1:i+8)`, then in order | 10 tests | 10 tests |
| `v = view(b, i+1:i+8)`, then `v[1]` to `v[8]` | 9 tests | 10 tests |
| `reinterpret(UInt64, view(b, i+1:i+8))[1]` | 4 tests, and a frame for the collector | 4 tests, and the frame |
| A tuple of the eight reads, then `reinterpret(UInt64, tuple)` | 8 tests, and 8 loads | 8 tests, and 16 loads |
| The second form at an offset the caller computed, `le64(b, off + len - 8)` | 9 tests, and 8 loads | 9 tests, and 8 loads |
| The same with `stop >= 8 \|\| throw` and `b[stop]` first and down | 9 tests, and 8 loads | 9 tests, and 8 loads |
| The second form at a constant distance from a tested base, `le64(b, i, 8)` | 2 tests | 2 tests |
| The second form inside `if off >= 0 && (off + 7) % UInt < length(b) % UInt` (`word_at`) | the 2 tests of the `if` | 3 tests |

The second form is the one that gives one load and at most three tests on both versions, and the source uses it.
The fourth is better still on Julia 1.10 and no better than the first on Julia 1.13.

So the tests go when three things hold. The base index is one value whose sign has been tested, the other indexes
are that value plus constants, and the highest is read first. Arithmetic in the caller breaks the first. The
compiler folds `off + len - 8 + 7` into `off + len - 1`, and the sign it knows is of another value. Masking the
offset or taking `max(offset, 0)` after the test does not help, since the compiler removes both. This is why a
name's words are read forwards from its start and masked (`word_at`, `second_word_at`), and never backwards from
its end.

Two words of one value:

| Form | Julia 1.13 | Julia 1.10 |
|---|---|---|
| `t[2n+1]` then `t[2n+2]` of a `Vector{UInt64}` | 2 tests | 2 tests |
| `t[2n+2]` then `t[2n+1]` | 1 test | 2 tests |
| `t[n+1]` of a vector of two-word elements, then its fields (`TapeValue`) | 1 test | 1 test |

Loops:

- `for i in 0:n-1` over `tape[first+i+1].h`, with nothing called in the loop (`all_of_type`). Julia 1.13 works out
  before the loop the range of `i` that needs no check, about thirty instructions, and the loop then has no check
  in it. For two or three items that is a loss, and `all_of_type` tests up to three one by one. With the tape as a
  `Vector{UInt64}` read at `base + 2i + 1` it kept one check for each item. Not looked at on Julia 1.10.
- A loop with a call in it (`visit_names`, the item loop of `run_array`) keeps every check. The vector's length
  and data are read again after the call, since the callee could have changed them.
- A view of the tape for an object's properties was not tried. Each index of a view is tested against the view's
  length, which the compiler has in a register, and the load of the vector's data is still made after each call.

Three things that are not bounds checks and cost as much:

- A function marked `@inline` that is part of a cycle of functions is a call in the package image (Measured 2),
  on both versions.
- `UInt64(1) << n` for an `Int` `n` the compiler cannot bound is about twelve instructions, since a count that is
  negative or 64 and more has a meaning in Julia. `UInt64(1) << (n & 63)` is one.
- `push!(v, a, b)` is `append!(v, (a, b))`, which copies through the general `copyto!` (Measured 6). A function
  that returns a tuple of two values returns it through memory (Measured 3 and 4).

## Measured and declined

**Unchecked reads.** `@inbounds` on the tape reads and the byte reads was measured at 0.82 to 0.94 of the checked
time on most of 12 corpora, 1.00 on two and 0.59 on one, and declined on 2026-10-08. A mistake in one would read
out of bounds where a checked read throws. The Go module made the same decision for the same reason, and the Rust
crate reads its tape and text unchecked.

What the source does today. Every read of the tape and of a document's bytes is written as a checked index, and
"Bounds checks the compiler removes" says which of the checks the compiler then proves and drops. That covers the
parser, the evaluator, the plans, the fused pass, the name table, the pattern shapes, the formats and the values
(`document.jl`, `plan.jl`, `fused.jl`, `eval.jl`, `names.jl`, `pattern.jl`, `formats.jl`, `values.jl` have no
`@inbounds` and no pointer). The pattern submodule, `src/ecmaregex`, has no `@inbounds` either: the six it was
ported with were removed when the decision was made to cover it too (2026-10-08). What remains unsafe there is the
binding to PCRE2, which cannot be otherwise.

- Pointers. `pcre.jl` is the binding to PCRE2 and is unsafe by nature. It has 11 `unsafe_load`, 8 `unsafe_store!`
  and one `unsafe_pointer_to_objref` over the `Frame` memory it allocates with `Libc.malloc` and over the callout
  block PCRE2 passes, and 14 `ccall`. `EcmaRegex.jl` takes the pointer to the text and to the pattern under
  `GC.@preserve` in the two `ismatch` methods and calls `unsafe_ismatch`.
- `unsafe_trunc` appears four times outside the engine (`numbers.jl` twice, `values.jl`, `compiler.jl`). It
  converts a `Float64` to an integer with no range check and reads no memory. Each is behind a test of the range.

The decision covers every array and string read of the package. Do not add `@inbounds` anywhere in it.
