# Optimizations: sweep of the Go module

Every performance technique of the Go module, checked against this port by reading its source. The inventory is the
Go module's `src-go/corvus-json-schema/OPTIMIZATIONS.md`, taken item by item in that file's order, in the form the
Object Pascal port gave it in `src-pas/corvus-json-schema/OPTIMIZATIONS.md`. The OCaml package was ported from the Go
module, with the Pascal port as the model for what Go takes from its standard library, and like the Pascal port it
has its own regular expression engine and generated Unicode tables. The Go file in turn sweeps the C# V5 runtime evaluator, the Rust crate and the Java port, so a row
here names its counterpart there where the Go file does.

Status is **done** (with where it is in the OCaml source), **partial** (with what is there and what is missing),
**todo** or **n/a** with the reason. Each "done" was found in the source of this package, in the file and under the
name given. A file is named from the package directory, so `src/eval.ml` is
`src-ml/corvus-json-schema/src/eval.ml`. The rows are numbered in the order of the Pascal file, 1 to 66, and the
section headings say which of its lists a row comes from. A row that file lists as done can be partial here, and the
Status column says so.

Nothing in this file has been measured in this port. The statuses say what the source does, and not what it is
worth. The todo order is the Go module's, and each item has to be measured here before it is kept.

Three facts of the language shape many rows.

- An OCaml `int` is 63 bits. A mask that holds 64 members in the Go source holds 62 here (`mask_bits` in
  `src/program.ml`), and a set that needs more takes the path Go takes beyond 64. `test/limits_test.ml` crosses each
  of those edges. Each 64-bit quantity of the Go code has a comment that says how it is held (the headers of
  `src/document.ml`, `src/numbers.ml`, `src/names.ml` and `src/pattern.ml`).
- A float, an `Int64` or a pair that is stored or returned is a block on the heap. So floats stay in a
  `Float.Array.t`, two results come back packed in one `int`, and nothing on the fail-fast path builds a closure.
- Every array, `Bytes` and string read is bounds checked by the compiler. The source has no `Obj`, no `unsafe_get`
  or `unsafe_set`, and the dune files pass no `-unsafe` and no other compiler flag. That was the owner's decision.

Keep this file current. A technique added to any of the other evaluators should be checked off here, or ruled out
with the reason.

## The Go module's "Done" list

| # | Technique | Status | Where in this port |
|---|---|---|---|
| 1 | **Instance representation.** A flat tape of two words per value, the kind in the low byte as the evaluator's type bit, flags, a count or length in the header and the data in the second word. Children consecutive, an object's as name and value pairs. Strings read in place where they have no escapes, with a flag for bytes outside ASCII. Strings scanned eight bytes at a time. Numbers classified at parse, with the offset of the text kept. An iterative parser with its own stack. Duplicate names pairwise up to 16 properties and by sorted hashes beyond. A document sized exactly, and text parsed into reused arrays. | partial | `src/document.ml`. Done: the tape is an `int array` (`t.tape`), with the header as kind in bits 0 to 7, flags in bits 8 to 15 and a 47-bit field above (`kind`, `flags`, `count`, `data`, `first`). `str_text` and `str_wide` are the string flags, and `escaped` unescapes into `text`. Numbers are one of three classes (`Numbers.num_int`, `num_big`, `num_float`), so that integers from -2^63 to 2^64 - 1 are exact, and the header keeps the offset of the text (`number_start`). The parser is iterative (`parse`, `push_frame`, `close`). `dedupe` is pairwise up to 16 properties and sorts hashes in `parser.hashes` beyond. `parse_new` sizes the document exactly and `parse_into` fills reused arrays. Missing: `str` scans a byte at a time through the `scan` table, and its comment says so. |
| 2 | **Number conversion.** Clinger's fast path, then a correctly rounded conversion that allocates nothing for the usual case. A parser that only checks syntax does not convert a number well inside the range of a float. | done | `number` in `src/document.ml` has the fast path (a mantissa below 2^53 and a power of ten from the `powers` table). `Numbers.decimal_to_float` in `src/numbers.ml` is the Eisel-Lemire algorithm (`eisel_lemire`, `mul_high`, the table of `src/powers_of_five.ml`, which is generated). The significand is kept to 18 digits, one fewer than Go and Pascal keep, so that it fits an `int`. A longer one goes to `compare_with_halfway` only when its truncation and the next value round differently (see row 35). `parser.validating` skips the conversion. |
| 3 | **Type tests.** One mask test against the kind. The integer test only for integer without number. A type-only child tested where it is applied and never entered. An `anyOf` of type-only branches, or a `oneOf` of type-only branches with no type in common, folded into the node's own type mask (the C# `TypeUnion` plan). | done | `type_ok`, `integer_ok` and `all_of_type` in `src/eval.ml`. `child.pass` and `set_shape` in `src/program.ml`, tested in `run_child` and `run_branch`. `type_union`, `type_only_mask` and `meet_types` in `src/plan.ml`. For whether the test is a call, see row 25. |
| 4 | **References.** Pure `$ref` chains elided and a one-branch `allOf` forwarded (the C# `Forward` plan), up to 16 hops, not across resources under a dynamic scope, keeping the guard of a node on a cycle. Collecting mode elides the same hops with their path suffix. A dynamic reference resolved at compile time where it can be. A program with no dynamic scope takes the fallback of every dynamic reference. In draft 7 and earlier `$ref` replaces its siblings. | done | `new_program` in `src/plan.ml` fills `fast_target` through `pure_ref_target` and `forward_target`. `resolve` and `collect_pure_ref` in `src/eval.ml`. `finalize_dynamic_refs` in `src/compiler.ml` sets `static_dynamic_ref`. `plan_node` in `src/plan.ml` takes the fallback. `compile_node` in `src/compiler.ml` has the legacy `$ref`. |
| 5 | **Plan shapes.** A caller enters a child by what its keywords come to. Every child takes the final shape of its node once the fused plans are built, and a node entered by its id is entered as a child. Keywords grouped by the kind of value they apply to. | done | The `shape_` constants, `child`, `plan.self` and `body` in `src/program.ml`. `shape_of` and `compile_plans` in `src/plan.ml`. `run`, `enter_child`, `enter_kind` and `run_keywords` in `src/eval.ml`. |
| 6 | **Objects.** One pass over the properties, specialised by which keywords apply. `required` as a mask of the names seen, including the names only `required` or a dependency mentions. Dependencies from the same bits. Count bounds from the header. The patterns a declared name matches worked out at compile time. `propertyNames` evaluated on the name where it lies. | done | `object_plan` in `src/program.ml` and `plan_object` in `src/plan.ml`. `run_object` in `src/eval.ml` chooses `walk_values` (with a loop of its own for a type-only child), `walk_names`, `walk_pattern` or `walk_general` by `object_plan.visit`. The mask covers at most 62 names (`mask_bits`, `required_mask`), and the rest are looked up (`o_required`, `object_rest`). `plan_dependency.has_bits`. `name_patterns`. `walk_general` runs `property_names` on the name's own value. |
| 7 | **The strict object loop.** Declared names, `additionalProperties` for the rest and the required mask, entered directly by a nested object. | done | `run_strict_object` in `src/eval.ml`, entered from `enter_kind` for `shape_object` when `object_plan.strict` is set. |
| 8 | **Small objects probed by name.** A plan of at most 4 names and no `additionalProperties` looks each name up in the instance when properties times names is at most 24. | done | `walk_lookup` in `src/eval.ml`, with `lookup_names`, `lookup_budget` and `object_plan.lookup` in `src/program.ml`. A name is compared by length, then word (`Names.name_word`), then text beyond eight bytes. The Go loop runs over one slice of the tape to drop its bounds checks. That part is row 47. |
| 9 | **Name lookup** (the C# `Utf8NameMap`). A set of the lengths the names have. A key of length, word and last eight bytes, which is the whole name up to sixteen bytes. The name after the previous match tried first, then the next name in sorted order, then a hash table whose multiplier is the one of 24 that leaves the fewest names away from their first slot. | done | `src/names.ml`. `t.lengths` and `length_bit` (lengths of 62 and more share a bit). `name_word`, `tail_word`, `rest`, `equal`. `at` and `find_from`, then `find_after` with `t.sorted_next`, then `find_key`. `new_name_map` and `fill` choose the multiplier (`name_hash_multipliers`). A word of eight bytes loses the top bit of its last byte in an `int`, and the header of the file says why names are still told apart. A set of more than `max_table_names` names is a `Hashtbl`, and a lookup in it copies the name (`find_long`). |
| 10 | **Flat composition.** A `$ref` and `allOf` chain of plain object schemas that apply unconditionally merges into one strict object loop. | done | `flat` and `flat_plan` in `try_fuse` in `src/fused.ml`, for at most 62 names. `run_fused` in `src/eval.ml` hands a flat plan to `run_strict_object`. |
| 11 | **Fused object plans** (the C# `FusedObject` plan). One pass over an object for a schema whose object semantics are spread over `$ref`, `allOf`, `if`/`then`/`else`, dependencies and `oneOf`/`anyOf`. Names resolved at compile time, identical resolutions applied once, value tests merged into one lookup per property, conditional applications deferred to a second step, conditions as two masks. Below a live dynamic reference only contributors in the node's own resource fuse. | done | `src/fused.ml` builds (`collect`, `collect_alternative`, `coalesce_apps`, `merge_value_tests`, `try_fuse`). `src/eval.ml` runs (`run_fused`, `run_fused_pass`, `fused_entry`, `fused_unknown`, `merged_allowed`, `applies`). The types are in `src/program.ml` (`fused_entry`, `merged_tests`, `fused_alternative`, `fused_alt_group`, `fused_forbidden`, `fused_absent`, `finals`). `reaches_dynamic_reference` in `src/plan.ml`. The limits are 256 names, 62 conditions, 64 contributors, 8 alternative groups and 62 branches in a group. |
| 12 | **`unevaluatedProperties` from the fused pass.** The pass tracks which properties some branch covered. | done | `fused_object.has_unevaluated` and the `covered` set in `run_fused_pass` in `src/eval.ml`. |
| 13 | **`unevaluatedItems` from static coverage.** The items after the longest unconditional prefix are checked with no tracking. | done | `static_item_coverage` in `src/plan.ml`, `run_unevaluated_items` in `src/eval.ml`. |
| 14 | **Evaluated properties and items elsewhere.** A marking analysis. Only a child that can mark gets a set. Sets in the evaluation's arena. A node the general evaluator runs evaluates only its own location. | done | `compute_marking` in `src/compiler.ml`. `can_mark`, `new_bits`, `take` and `free_bits` in `src/eval.ml`. `apply_at`, `eval_array` and `eval_unevaluated_properties` go back to `run` with `fast_target`. The general evaluator finds a declared name through `property_maps` for a node with more than 8 properties (`property_node`). |
| 15 | **Arrays.** The length read once. `prefixItems` guarded by the length. An array of type-only items as one loop over the tape. Items that are simple arrays checked inline. `contains` stops at `minContains` when there is no maximum. | done | `run_array` and `all_of_type` in `src/eval.ml`. `array_plan.is_simple`, `has_nested` and the `nested_` fields are set in `compile_plans` in `src/plan.ml`. |
| 16 | **`uniqueItems`.** Nothing for fewer than two items. Up to 32 strings pairwise. Up to 16 other values pairwise. Longer arrays sorted by hash in reused scratch. Objects in the same order compared position by position. | done | `all_unique`, `all_strings`, `value_hash` and `values_equal` in `src/values.ml`, with `Document.sort_ints` and the `Values.scratch` the evaluator keeps (`unique` in `Eval.t`). |
| 17 | **String lengths.** Exact from the byte length for an ASCII string. Otherwise decided from the byte length where it can be. | done | `length_ok` and `code_points` in `src/eval.ml`, on `Document.str_ascii`. |
| 18 | **Pattern shapes.** Patterns every string matches, `.+`, line lengths, `^X.*` reduced to `^X`, anchored literals, whole literal sets, anchored class sequences in one pass and by length where one item is variable, alternatives once groups are multiplied out, separated lists, the excluded class with a word lookahead. A byte-per-character path for a string the document knows is ASCII. | done | `src/pattern.ml`. `choose_pattern` and `choose_shape` pick the `matcher`. `line_length`, `make_literals` (a hash set above 8 literals), `match_pinned`, `expand_alternatives`, `parse_separated_list` and `match_list`, `excluded_class_with_word`, and `match_ascii` for ASCII text. The pattern cache is row 28. |
| 19 | **Numbers.** Exact comparison across the number classes. Bounds digested at compile time. An integer `multipleOf` of an integer by remainder. Any other on the decimal digits of the text. | done | `Numbers.compare`, `compare_int_float` and `compare_big_float` in `src/numbers.ml`. `number_op` in `src/program.ml` holds a bound as its flags, its data word and its document's floats, and `run_number` in `src/eval.ml` compares them. `divides` uses `mod`, and `divides_text` streams the digits for a divisor of at most 17 significant digits (Go and Pascal have 18). |
| 20 | **Formats.** The format kind and whether it asserts decided at compile time. Formats read the UTF-8 bytes in place. The URI, IRI, URI template and e-mail local part grammars read directly, so no format but `regex` needs the pattern engine. A custom format handed the bytes where they lie. | partial | Done: `format_check_for` in `plan_node` in `src/plan.ml`, and the custom format of a node is found at compile time (`custom_formats` in `src/program.ml`). `src/formats.ml` reads positions of a `Bytes.t` (`uri_ok`, `uri_template_ok`, `email_local_ok`, `check_string`), and `src/number_formats.ml` the numeric ones. Missing: a custom format is a `string -> bool`, and the text is `Bytes`, so `check_string_format` in `src/eval.ml` gives it a copy, as the Go module does. |
| 21 | **Enum and const.** An enum of strings is a name lookup, decided where the child is entered when that is all the child is. Other values by JSON equality, the kind first and string lengths before bytes. | done | `op_enum_strings` and `shape_string_enum` in `src/program.ml`, `names_has` and `enter_kind` in `src/eval.ml`. `Values.values_equal` and `Document.str_equal`. |
| 22 | **Composition.** `anyOf` stops at the first match and `oneOf` at the second. Only branches whose types admit the instance's kind are tried (the C# `TypeDispatch` plan). Discriminators narrow both. `if` without `then` or `else` is dropped. `not` fails fast. | done | `run_op`, `candidates` and `select_index` in `src/eval.ml`. `branches.by_kind`, `dispatch` and `new_discriminator_index` in `src/plan.ml`. `build_discriminator` in `src/compiler.ml` sets `all_require`. `plan_node` adds `op_if` only when there is a `then` or an `else`. |
| 23 | **Dynamic scope and cycles.** A resource pushed only in a program that keeps a dynamic scope, and only when it is not the innermost already. The depth guard only for nodes on an in-place cycle. | done | `uses_dynamic_scope`, `run_body` and `push_scope` in `src/eval.ml`. `plan.guard` and `run_in_place`. |
| 24 | **Allocation.** The evaluator on the stack, its buffers a scratch taken only when first needed, the state of a fused pass on the stack, text parsed into a reused document, a string read without a copy, the matcher's stack reused. A test that holds the keyword paths to no allocation. | done | By other means, since an OCaml record is never on the stack. The state and the buffers of an evaluation are one `Eval.t`. A validator keeps one (`Validator.own` in `src/corvus_json_schema.ml`), which `acquire` takes with `Atomic.compare_and_set` on `busy`. An evaluation that finds it held makes its own, and a caller can pass `?scratch` (`Scratch.t`). So every evaluation takes a scratch, with one compare and set, and none is taken lazily. There is no thread-local state, no pool and nothing to release for a thread. The state of a fused pass and the evaluated sets are runs of words in `Eval.t.arena` (`take`, `pass_words`). Text is parsed into `Eval.t.text` (`parse_into`), a string is read through `str_bytes`, `str_off` and `str_len`, and the engine's stack is `Eval.t.regex`. `test/allocations_test.ml` holds the paths it lists to no words allocated. The stated exceptions are at the top of `src/corvus_json_schema.mli` (see row 35). |
| 25 | **Inlining.** The small functions of the loops inlined. The calls written out so that a value costs one call. A value's header read once where the value is entered. | partial | Done: `[@inline]` is on `Names.at`, `Names.hash` and `Names.length_bit`, on `rotate5`, `word64` and `word32` in `src/document.ml`, and on `Numbers.mul_high`. `walk_lookup` and the type-only loop of `walk_values` have the type test written out in the loop. `enter_child` reads the kind once and hands it to `enter_kind`. Missing: nothing else is marked. The tape accessors (`Document.kind`, `count`, `first`, `str_len`) and `type_ok` are left to the compiler. `run_child` is a member of the recursive group of `src/eval.ml` and is not marked, and the other loops apply a child through it. The expected name is tested in `Names.find_from`, which the loops call, and not in the loops themselves. `run_object` and `run_strict_object` read the count and the first child again after `enter_child`. The dune files set no inlining option. |
| 26 | **Compile time.** Analyses run only when the schema has what they analyse. In-place cycles by an iterative Tarjan. Annotation keywords digested on the first evaluation with a collector. Metaschemas embedded and read on demand. | done | `analyse`, `compute_in_place_cycles` and `compile_all` in `src/compiler.ml`. `Plan.node_annotations` publishes through an `Atomic` with no lock, so two evaluations that start together may each compute the same value. `Metaschemas.text` in `src/metaschemas.ml` reads `src/metaschema_files.ml` (generated) when `src/loader.ml` asks for a URI. |

## The "Partial" list

| # | Technique | Status | Where in this port |
|---|---|---|---|
| 27 | **The regular expression engine.** A pattern in the subset that needs no backtracking matched in linear time. An automaton over ASCII text for such a pattern with no word boundary. A backtracking matcher with an explicit, reused stack for the rest. | partial | `ecmaregex/`. Done: `Dfa.build` makes an automaton of at most 256 states (`max_dfa_states`) when the pattern is compiled, and `Dfa.match_bytes` reads one table entry for each byte. `Vm.match_bytes` keeps its stack in a `Vm.state` the caller gives (`create_scratch`, `is_match_with`), so a match allocates only while the stack grows. The entry points that are given no state use one the pattern keeps, taken with `Atomic.compare_and_set`. Missing: a linear-time matcher for a pattern of that subset that has no automaton, and for a text that is not ASCII. Those run on the backtracking matcher, which has no step budget (the interface says so). There is no RE2 and no translation to another engine. |
| 28 | **Patterns compiled once.** The Go module keeps every compiled pattern in a cache for the whole process. | partial | Done: `compile_pattern` in `src/compiler.ml` shares one matcher between the identical patterns of one compilation, through the `patterns` table of the compiler. Missing: nothing is shared between schemas. `Pattern.compile` compiles what it is given each time, and its interface says the package keeps no cache of patterns. See row 46. |
| 29 | **Leaves decided in the loop.** Const, string enum and length tests for a property without leaving the loop. | partial | Done: a type-only child is decided by one test (`child.pass`). Missing: any other child is a call to `enter_child`, and a leaf then runs `run_leaf`, which loops over its operations. No const, string enum or length test is in `walk_names`, `walk_lookup`, `fused_entry` or the array loops. |
| 30 | **`true` subschemas skipped.** | partial | Done: the fused plan treats a `true` child as cover with no test (`opt_child`, `apply_opt`, `app_child` in `try_fuse`). Missing: in `plan_object`, `additionalProperties: true` stays a child (`has_additional`), which rules out `object_plan.lookup`, `propertyNames: true` selects `visit_general`, and a `true` pattern property is still matched. |
| 31 | **Keywords that cannot apply under the node's type.** | partial | Done: `plan_node` in `src/plan.ml` drops the object plan and the array plan when `own_types` excludes objects or arrays. Missing: number and string keywords are kept whatever the type, so such a node is a leaf where it could be a type test. |
| 32 | **Enums that are not all strings.** | partial | Done: an enum of strings only is a name lookup (row 21). Missing: an enum with one value that is not a string is compared value by value (`op_enum`, `enum_contains` in `src/eval.ml`). |
| 33 | **Searching for an unanchored match.** | partial | Done: the backtracking matcher searches for a literal prefix (`search_prefix`) and skips the positions its first set rejects (`search_first` in `ecmaregex/vm.ml`). Missing: in `src/pattern.ml` an unanchored literal is searched a byte at a time (`index_string`), and an unanchored class sequence is tried at every position (`anywhere_ascii`, `anywhere_chars`). |
| 34 | **Content.** Base64 validity without decoding. JSON content checked where it lies. | partial | Done: JSON content is checked where it lies by a parser that builds nothing (`Document.is_valid_range` from `content_ok` in `src/eval.ml`), with no copy. Missing: Base64 content is decoded into `Eval.t.content` even when only its validity is asked (`Formats.base64_decode`). |
| 35 | **Allocation outside the common paths.** | partial | These allocate. A `multipleOf` whose divisor has more than 17 significant digits (`divides_text_big` and `big` in `src/numbers.ml`). The conversion of a number of more than 18 significant digits whose nearest float the first 18 do not decide (`compare_with_halfway`). A custom format, which is given a copy. A lookup in a set of more than 32768 names (`find_long` in `src/names.ml`). An asserted `regex`, `idn-hostname` or `idn-email` format, and a `hostname` with a label that starts with `xn--` (`src/formats.ml`). |

## The "Todo" list

| # | Technique | Status | Where in this port |
|---|---|---|---|
| 36 | **Leaves decided in the loop.** | todo | See row 29. |
| 37 | **`true` subschemas dropped from the object plan.** | todo | See row 30. |
| 38 | **Equivalent subschemas canonicalised.** The C# compiler points fail-fast references at one representative of each set of identical subschemas, and the Java port merges structurally identical nodes. | todo | No counterpart. `get_node` in `src/compiler.ml` makes one node for each schema location (`node_of`). |
| 39 | **Unanchored search with a first-atom skip.** | todo | For the matchers of `src/pattern.ml`. The engine has it. See row 33. |
| 40 | **Mixed and large enums.** | todo | See row 32. |
| 41 | **Keywords pruned by type** for numbers and strings. | todo | See row 31. |
| 42 | **Collecting mode without allocation.** | todo | `src/results.ml` builds each row's locations and message as strings (`Buffer.contents` and concatenation). `reset` also lets go of the array of committed rows, so a collector that is used again grows it again. |
| 43 | **Base64 validity without decoding, JSON content checked where it lies, and `multipleOf` beyond the digit limit without a big integer that allocates.** | partial | JSON content where it lies is done (row 34). The other two are not (rows 34 and 35). |
| 44 | **The fused pass.** A set of small loops over contributors, applications and tests for each property. | todo | `run_fused_pass` has the shape of the Go function, so there is nothing to port. It would have to be measured line by line. Its state is read from the arena, where Go reads the stack. |
| 45 | **A linear-time matcher for the patterns that have no automaton, and for texts that are not ASCII.** | todo | See row 27. This is the one place where a pattern can cost more here than in the Go module by more than a constant. |
| 46 | **A pattern cache for the process.** | n/a | A decision. `src/pattern.ml` and `src/compiler.ml` say the package keeps no process-wide cache of patterns, so a validator is a value with nothing shared behind it. The only process-wide values built at run time are the two case-folding tables of `ecmaregex/charset.ml`, published through an `Atomic`. |

## Not applicable

| # | Technique | Status | Where in this port |
|---|---|---|---|
| 47 | **Reads without bounds checks.** Loops over a slice of the tape in Go, `get_unchecked` in Rust. | n/a | Bounds checks stay on, and the compiler's check is inline. No `unsafe_get`, `unsafe_set`, `Obj` or `-unsafe` anywhere in `src/`, `ecmaregex/` or `ucd/`. The same decision as the Go module's and the Julia package's. |
| 48 | **Runtime code generation** (the C# IL emitter, the Java bytecode generator, the TypeScript source generator), and everything that belongs to it. | n/a | The package compiles ahead of time and loads no code at run time. The plans are data that one evaluator interprets, as in the Go module and the Rust crate. |
| 49 | **A program image, ReadyToRun and tiered compilation** (C#), **an ahead-of-time cache** (Java), **profile-guided optimisation** (Go). | n/a | OCaml compiles ahead of time. The dune files set no optimisation options. They belong to the program that uses the package and to its compiler. |
| 50 | **Generic specialisation over a document type** (C#), **the `Instance` trait and monomorphised evaluators** (Rust). | n/a | Instances are always `Document.t` values. Fail-fast evaluation runs the plans, and the general evaluator serves collecting. |
| 51 | **One row on after a scalar, integers read in one pass, integer bounds and consts with no conversion** (the C# code generator). | n/a | Every value is two words, a container's children are consecutive, and a number's value is in the tape or in the document's `floats`. |
| 52 | **`sync.Pool` and what follows from it** (Go), **thread variables and a call that releases a thread's scratch** (Pascal). | n/a | Neither. See row 24. The automaton is built when the pattern is compiled, so a compiled pattern and a compiled program are values that nothing writes to after compilation, apart from the annotations of row 26. |
| 53 | **The inliner's budget** (Go). | n/a | No function here is shaped to fit a compiler's inlining threshold. See row 25 for what is marked. |
| 54 | **Translation to another engine** (`regexp` in Go, `java.util.regex` in Java, the `regex` and `regress` crates in Rust, compiled IL in C#, PCRE2 in Julia). | n/a | The package depends on nothing but the compiler's standard library (`dune-project`), so its own engine is the only one. See row 27. |
| 55 | **A vectorised search API** (`SearchValues` and `IndexOfAny` in C#, `bytes.Index` in Go). | n/a | The standard library has none, and the package has no C stubs. Its searches are loops over the bytes. |
| 56 | **Release profile settings** (the Rust crate's `lto` and `codegen-units`). | n/a | See row 49. |

## Rows specific to Free Pascal

The Pascal file records these under "Measured" and "Tried and not kept". They are about Free Pascal's code
generation. Each is given here with what the OCaml package does in its place.

| # | Technique | Status | Where in this port |
|---|---|---|---|
| 57 | **The compiler's range check is a call.** | n/a | Free Pascal specific. The OCaml native compiler checks an index inline, with the failure out of line. |
| 58 | **Inline checks in accessor functions** (`Corvus.JsonSchema.Checked`). | n/a | There is no such module and no region with checks off. Every read is the compiler's own checked read. |
| 59 | **Indexes as wide as a length** (`NativeInt`). | n/a | Every index is an `int`, which is the only integer the code indexes with, so nothing is narrowed on the way to a read and nothing is checked on the way. |
| 60 | **A document has a size limit.** | todo | No limit is stated or tested. The header field has 47 bits (`src/document.ml`). `dedupe` in `src/document.ml` and `all_unique` in `src/values.ml` keep a position in the low 32 bits of a sort key, so an object or an array of 2^32 members or more is beyond what they hold. The parser does not refuse such a document. |
| 61 | **A name's header is read once.** | todo | The object loops read it three times for a name. `str_bytes` and `str_len` each read the header word and `str_off` the data word (`walk_names`, `walk_general`, `run_fused_pass` in `src/eval.ml`). `enter_child` does read a value's kind once (row 25). |
| 62 | **What remains is not a check.** | n/a | A measurement of the Pascal port. |
| 63 | **One check for the name headers of an object in the lookup loop.** | n/a | Free Pascal specific, and row 47 rules it out here. |
| 64 | **Telling the compiler that the failure routine does not return.** | n/a | Free Pascal specific. |
| 65 | **One run check for the bytes the ASCII matcher reads.** | n/a | Free Pascal specific, and row 47 rules it out here. |
| 66 | **Explicit casts for the narrowing that is left on the fused path.** | n/a | Free Pascal specific. There is no narrowing here (row 59). |

The Go module's list of reverted experiments is about the Go compiler's code generation and says nothing about what
the OCaml compiler would do with the same changes.

## OCaml-specific

Techniques this port has that the Go list does not. Each was found in the source named.

| # | Technique | Status | Where in this port |
|---|---|---|---|
| O1 | **Two results in one `int`.** A pair returned from a function is a block, so a function that gives two results in Go gives one `int` here. | done | The walks of `src/eval.ml` return the mask of names seen, or -1 for a failure (`walk_names`, `walk_lookup`, `walk_general`). `resolve_unknown` returns 0, 1 or 2. `Names.find_from` returns the index and the caller works the next hint out. `decode_rune` in `src/pattern.ml` packs a code point and its width. `last_non_zero_digit` and `digits_exponent` in `src/numbers.ml` are two functions for the same reason. |
| O2 | **No float is boxed.** | done | A document's floats are a `Float.Array.t` (`Document.t.floats`) and the tape holds an index. A bound is its flags, data word and array (`number_op` in `src/program.ml`). `Numbers.compare`, `compare_int_float` and `Number_formats.integral` read the float where it is compared. The 64-bit words of `eisel_lemire` are local `Int64` values. |
| O3 | **Three classes of number in an `int` tape.** An integer that fits an `int`, any other integer from -2^63 to 2^64 - 1 as a top part in the flags and a low part in the data word, and a float. | done | `num_int`, `num_big`, `num_float`, `big_flags` and `top_of` in `src/numbers.ml`. `long_integer` in `src/document.ml` classifies an integer of more than 18 digits from two 32-bit halves. Comparing the top and low parts orders every integer with no arithmetic wider than an `int`. |
| O4 | **The arena.** Where Go keeps a small structure on the stack, this port takes a run of words from one `int array` and releases runs in the reverse order. | done | `take`, `new_bits`, `free_bits`, `set_bit`, `merge_bits` in `src/eval.ml`. `run_fused` releases the state of a pass and its sets together by restoring `narena`. |
| O5 | **A failed parse allocates nothing.** | done | `fail` in `src/document.ml` leaves the message and the offset in the parser and raises the constant exception `Failed` with `raise_notrace`. `test/allocations_test.ml` has a case for text that is not JSON. |
| O6 | **A string parsed through a kept copy with a sentinel.** `is_valid_string` copies the text into a buffer the evaluator keeps and writes one byte after it that no number is written with, so that a number's text stops there. | done | `parse_string` in `Validator` in `src/corvus_json_schema.ml` (`Eval.t.copy`), and the note on `parse_into` in `src/document.ml`. `is_valid_bytes` parses the caller's bytes where they are. |
| O7 | **An evaluator keeps nothing alive between evaluations.** | done | `Eval.finish` puts back `no_program` and `no_document`, and `Document.release_source` lets go of the text. `Vm.match_text` lets go of the text and drops a stack beyond `4 * max_retained_stack`. |
| O8 | **Tables as immutable strings.** | done | A character set of `src/pattern.ml` is a string of 256 bytes read with one checked load (`char_set.table`). The automaton's `trans`, `byte_class` and `at_end` are strings (`ecmaregex/dfa.ml`). The Unicode ranges are packed strings searched by halving (`ucd/corvus_json_schema_ucd.ml` over the generated `ucd/tables.ml`). The powers of five are one string read with `String.get_int64_le`. |
| O9 | **Loops as top-level functions over positions.** The matchers pass a position and the end of the text where the Go source slices, and the loop of the automaton takes the tables as arguments. | done | `take_ascii`, `match_ascii_from`, `consume_from` and the rest of `src/pattern.ml`. `run_bytes` and `run_string` in `ecmaregex/dfa.ml`. `step` and `backtrack` in `ecmaregex/vm.ml` call each other in tail position. |
| O10 | **A custom format found at compile time.** | done | `new_program` in `src/plan.ml` fills `custom_formats`, one entry for each node, where Go looks the name up in a map when the format is asserted. |
| O11 | **A scratch passed as an option built once.** An optional argument built at the call is a block. | done | The note at the top of `src/corvus_json_schema.mli`, and the "with a scratch" cases of `test/allocations_test.ml`. |
| O12 | **Sorting in place.** | done | `sort_ints` in `src/document.ml` is a heap sort over the first elements of an `int array`, for `dedupe` and `all_unique`. |

## Measurements

Nothing has been measured yet.

## Not done yet

Partial, with what is missing.

- Row 1. Strings are scanned a byte at a time (`str` in `src/document.ml`).
- Row 20. A custom format is given a copy of the string.
- Row 25. Only seven small functions are marked `[@inline]`. In most loops the type-only child and the expected name
  are tested in functions that are not marked, and an object's count and first child are read again after
  `enter_child`.
- Row 27. No linear-time matcher for a pattern without an automaton or for a text that is not ASCII, and no step
  budget in the backtracking matcher.
- Row 28. Patterns are shared within one compilation only.
- Row 29. Only a type-only child is decided without a call.
- Row 30. `true` subschemas are kept in the object plan.
- Row 31. Number and string keywords are kept under a type that excludes them.
- Row 32. A mixed enum is compared value by value.
- Row 33. The matchers of `src/pattern.ml` search an unanchored match at every position.
- Row 34. Base64 content is decoded to check it.
- Row 35. The paths listed there allocate.
- Row 43. Two of its three parts are open.

Todo, in the Go module's order.

1. Leaves decided in the loop (row 36).
2. `true` subschemas dropped from the object plan (row 37).
3. Equivalent subschemas canonicalised (row 38).
4. Unanchored search with a first-atom skip in the matchers of `src/pattern.ml` (row 39).
5. Mixed and large enums (row 40).
6. Keywords pruned by type for numbers and strings (row 41).
7. Collecting mode without allocation (row 42).
8. Base64 validity without decoding, and `multipleOf` beyond 17 digits without a big integer that allocates (row
   43).
9. The fused pass, measured line by line (row 44).
10. A linear-time matcher for the patterns that have no automaton, and for texts that are not ASCII (row 45).
11. A stated size limit for a document (row 60).
12. A name's header read once (row 61).
