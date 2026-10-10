# corvus-json-schema (OCaml)

A JSON Schema evaluator for OCaml (draft 4, 6, 7, 2019-09 and 2020-12), ported from the Corvus.Text.Json V5 runtime
evaluator (`Corvus.Text.Json.RuntimeEvaluator`) by way of its
[Go port](https://github.com/corvus-dotnet/Corvus.JsonSchema/tree/main/src-go/corvus-json-schema). It is a native
port of the Go module, with its own JSON parser and its own ECMAScript regular expression engine. It has no
dependencies beyond the OCaml standard library, and no C code.

OCaml 4.14 or later, on a 64-bit platform. It is tested with OCaml 4.14 and 5.5.

- **Conformant**: passes all 7,966 tests of the JSON-Schema-Test-Suite (required, optional and `optional/format`,
  every draft), with the same single exclusion as the C# runner (`draft4/optional/zeroTerminatedFloats.json`), and all
  217 assertions of the suite's annotation tests.
- **Compiled to plans**: a schema compiles once into a node graph and fail-fast plans, each holding only the checks
  its subschema needs, with an object's keywords fused into one pass over its properties.
- **No allocation**: validating a parsed document, or JSON text through reused buffers, allocates nothing on the
  OCaml heap in the steady state. [What allocates](#what-allocates) lists the exceptions.
- **Results and annotations**: evaluate with a results collector at the Basic, Detailed or Verbose level for the same
  rows (locations, messages, order) as the C# `JsonSchemaResultsCollector`, and annotations as
  `JsonSchemaAnnotationProducer` extracts them.
- **Threads and domains**: a validator is immutable once compiled and safe to share between system threads and
  between the domains of OCaml 5. See [Threads and domains](#threads-and-domains).
- **Every array read is checked**: the package is built with the compiler's bounds checks on. It uses no
  `unsafe_get`, no `Obj` and no `-unsafe`, so an index out of range raises `Invalid_argument` and never reads other
  memory. The Go module likewise has no unchecked reads.

## Install

Until the package is in the opam repository, pin it from its own repository,
[corvus-dotnet/corvus-json-schema-ocaml](https://github.com/corvus-dotnet/corvus-json-schema-ocaml), which holds
this directory and nothing else, with a tag `v<version>` for each release:

```sh
opam pin add corvus-json-schema https://github.com/corvus-dotnet/corvus-json-schema-ocaml.git#v0.1.0
```

Once it is in the [opam repository](https://opam.ocaml.org/packages/corvus-json-schema/):

```sh
opam install corvus-json-schema
```

With dune, name the library in the `libraries` field:

```
(executable
 (name yourprogram)
 (libraries corvus-json-schema))
```

The one module a program uses is `Corvus_json_schema`. The package also installs two libraries it is built from,
`corvus-json-schema.ecmaregex` (the regular expression engine) and `corvus-json-schema.ucd` (the Unicode data). They
are internal. Their interfaces are documented, and they can change in any release.

## Usage

```ocaml
open Corvus_json_schema

let () =
  match
    Validator.compile_string
      {|{
          "type": "object",
          "properties": {"id": {"type": "integer", "minimum": 1}},
          "required": ["id"]
        }|}
  with
  | Error e -> prerr_endline (error_to_string e)
  | Ok validator ->
      (* true *)
      Printf.printf "%b\n" (Validator.is_valid_string validator {|{"id": 3}|});
      (* false *)
      Printf.printf "%b\n" (Validator.is_valid_string validator {|{"id": 0}|})
```

A validator is not modified by validation and is safe to use from several threads or domains at once (see
[Threads and domains](#threads-and-domains)). `Validator.compile_string` (and `Validator.compile`, which is the same
function) takes the schema as JSON text, `compile_bytes` as UTF-8 bytes, `compile_document` as a parsed
`Document.t`, and `compile_uri` fetches it through the document resolver (or takes a standard metaschema).

No function of the package raises an exception for a schema or an instance that is wrong. Each returns a `result`
or a `bool`. The samples after the first use `Result.get_ok` for brevity, which raises `Invalid_argument` for an
`Error`.

Instances can be given as JSON text (a `string`, or `Bytes.t`), parsed into buffers that are reused, or as a
`Document.t` parsed once and validated any number of times. A document is immutable and safe to share.

```ocaml
let () =
  let validator = Result.get_ok (Validator.compile_string {|{"type": "array", "items": {"type": "integer"}}|}) in

  (* Parse once, validate any number of times: true, then [1,2,3] *)
  let document = Result.get_ok (Document.parse_string {|[1, 2, 3]|}) in
  Printf.printf "%b\n" (Validator.is_valid_document validator document);
  print_endline (Document.to_string document);

  (* JSON text is copied and parsed into buffers that are reused: false *)
  Printf.printf "%b\n" (Validator.is_valid_string validator {|[1, "two"]|});

  (* Bytes are parsed where they are: true *)
  let bytes = Bytes.of_string {|[4, 5]|} in
  Printf.printf "%b\n" (Validator.is_valid_bytes validator bytes)
```

`Document.parse_string` copies the text. `Document.parse_bytes` keeps the bytes it is given and copies nothing, so
they must not be changed afterwards. `Validator.is_valid_bytes` keeps nothing of the bytes it is given.

The `is_valid` functions report text that is not JSON as not valid, and likewise an instance on which the schema
recursed in place beyond the maximum depth. The `validate` functions tell those apart: they return
`Error (Parse_error e)`, where `e` has the `message` and the byte `offset`, and `Error Depth_exceeded`.

```ocaml
let () =
  let validator = Result.get_ok (Validator.compile_string {|{"type": "object"}|}) in

  (* is_valid_string reports text that is not JSON as not valid: false *)
  Printf.printf "%b\n" (Validator.is_valid_string validator {|{"id": 3|});

  (* validate_string gives the reason: 8: unexpected end of input *)
  (match Validator.validate_string validator {|{"id": 3|} with
  | Ok valid -> Printf.printf "%b\n" valid
  | Error (Parse_error e) -> Printf.printf "%d: %s\n" e.offset e.message
  | Error e -> print_endline (error_to_string e));

  (* A schema that recurses without consuming the instance is stopped at the maximum depth. *)
  let looping = Result.get_ok (Validator.compile_string {|{"not": {"$ref": "#"}}|}) in
  match Validator.validate_string looping "1" with
  | Ok valid -> Printf.printf "%b\n" valid
  | Error Depth_exceeded -> print_endline "the schema recursed beyond the maximum depth"
  | Error e -> print_endline (error_to_string e)
```

A schema that is not JSON gives `Error (Parse_error _)` from the compile functions, and one that cannot be compiled
(an unresolvable reference, an invalid pattern) gives `Error (Compile_error reason)`. `error_to_string` is the
reason as text for any of them.

```ocaml
let () =
  (match Validator.compile_string {|{"pattern": "("}|} with
  | Error (Compile_error reason) -> print_endline ("The schema cannot be compiled: " ^ reason)
  | Error e -> print_endline (error_to_string e)
  | Ok _ -> ());
  match Validator.compile_string {|{"type": |} with
  | Error (Parse_error e) -> print_endline ("The schema is not JSON: " ^ Document.error_to_string e)
  | Error e -> print_endline (error_to_string e)
  | Ok _ -> ()
(* The schema cannot be compiled: Invalid regular expression '(' in pattern. *)
(* The schema is not JSON: invalid JSON at offset 9: unexpected end of input *)
```

### Options

The compile functions take `?options`, a record of type `options` (the OCaml counterpart of
`JsonSchemaEvaluatorOptions`). The function `options` builds one from optional arguments, each of which defaults to
the field of `default_options`. A custom format and a document resolver are plain functions.

```ocaml
let () =
  let item = Result.get_ok (Document.parse_string {|{"type": "string", "format": "even-length"}|}) in
  let options =
    options ~default_dialect:Draft2019_09 (* for schemas without $schema (the default is Draft2020_12) *)
      ~assert_format:true (* true or false. Without it the vocabularies decide *)
      ~formats:[ ("even-length", fun value -> String.length value mod 2 = 0) ]
      ~resolver:(fun uri -> if uri = "https://example.com/item.json" then Some item else None)
      ~base_uri:"https://example.com/root.json" ~entry_point:"#/$defs/item" ~max_depth:128 ()
  in
  let validator = Result.get_ok (Validator.compile_string ~options {|{"$defs": {"item": {"$ref": "item.json"}}}|}) in
  (* true *)
  Printf.printf "%b\n" (Validator.is_valid_string validator {|"four"|});
  (* false *)
  Printf.printf "%b\n" (Validator.is_valid_string validator {|"three"|})
```

| Argument and field | Meaning |
|---|---|
| `default_dialect` | Dialect for schemas without `$schema` (default `Draft2020_12`). The others are `Draft4`, `Draft6`, `Draft7` and `Draft2019_09`. |
| `assert_format` | `true` asserts `format`, `false` never does. Without it (the field is `None`) the vocabularies decide (2020-12 `format-assertion`). |
| `assert_format_in_legacy_drafts` | Without `assert_format`, also assert `format` in drafts 4 to 7. |
| `assert_content` | Assert `contentEncoding`/`contentMediaType` in draft 7 (default `true`). |
| `formats` | Custom format assertions by name, which take precedence over a built-in one of the same name. The function receives a copy of the string, or of a number's JSON text. |
| `resolver` | A function that resolves remote `$ref`s by absolute URI, and returns `None` for a document it does not know. The standard metaschemas are built in. |
| `base_uri` | Base URI of the root document. |
| `entry_point` | Evaluate from a subschema, for example `#/$defs/item`. |
| `max_depth` | Depth limit for in-place recursion on a cycle (default 128). |

The options are an ordinary record, so `{ default_options with ... }` works as well. `dialect_to_string` gives a
dialect's usual name, and `version` is the version of the package.

```ocaml
let () =
  (* The options are a record, so one field can be changed from the defaults. *)
  let options = { default_options with default_dialect = Draft4 } in
  let schema = Result.get_ok (Document.parse_string {|{"minimum": 1, "exclusiveMinimum": true}|}) in
  let validator = Result.get_ok (Validator.compile_document ~options schema) in
  (* draft4: false *)
  Printf.printf "%s: %b\n" (dialect_to_string options.default_dialect) (Validator.is_valid_string validator "1");

  (* The standard metaschemas are built in, so a schema can be checked against its dialect's. *)
  let metaschema = Result.get_ok (Validator.compile_uri "https://json-schema.org/draft/2020-12/schema") in
  (* true *)
  Printf.printf "%b\n" (Validator.is_valid_string metaschema {|{"type": "object"}|});
  (* false *)
  Printf.printf "%b\n" (Validator.is_valid_string metaschema {|{"type": "objekt"}|})
```

### Results and annotations

```ocaml
let () =
  let validator =
    Result.get_ok (Validator.compile_string {|{"properties": {"id": {"type": "integer"}}, "required": ["name"]}|})
  in
  let instance = Result.get_ok (Document.parse_string {|{"id": "seven"}|}) in
  let results = Results.create Results.Detailed in
  (match Validator.evaluate validator ~results instance with
  | Ok valid -> Printf.printf "%b\n" valid
  | Error e -> print_endline (error_to_string e));
  List.iter
    (fun (row : Results.result) ->
      if row.evaluation_location <> "" && row.message <> "" then
        Printf.printf "%s at %S: %s\n" row.evaluation_location row.document_evaluation_location row.message)
    (Results.results results)
(* false *)
(* /properties/id at "/id": The value was expected to match the subschema. *)
(* /properties/id/type at "/id": The value was expected to be of type 'integer' *)
(* /required at "/name": Required property not present 'name' *)
```

The levels and rows are those of the C# collector. `Results.Basic` records failures without message text,
`Results.Detailed` adds the text, and `Results.Verbose` records every keyword, passing ones and annotations
included. Each row is a `Results.result` with `is_match`, `message`, `evaluation_location`,
`schema_evaluation_location` and `document_evaluation_location`. A collector accumulates across evaluations until
`Results.reset`, and is used by one evaluation at a time. `Validator.evaluate` takes a `Document.t`, and
`evaluate_string` and `evaluate_bytes` take JSON text. Each returns `Error (Parse_error _)` for text that is not
JSON and `Error Depth_exceeded` when the schema recursed in place beyond the maximum depth.

```ocaml
let () =
  let validator =
    Result.get_ok
      (Validator.compile_string
         {|{
             "title": "Person",
             "properties": {"name": {"title": "Name", "type": "string"}}
           }|})
  in
  let results = Results.create Results.Verbose in
  (match Validator.evaluate_string validator ~results {|{"name": "Ada"}|} with
  | Ok valid -> Printf.printf "%b\n" valid
  | Error e -> print_endline (error_to_string e));

  (* Instance location, then keyword, then schema location, then the value as JSON text. *)
  List.iter
    (fun (instance_location, keywords) ->
      List.iter
        (fun (keyword, values) ->
          List.iter
            (fun (schema_location, value) ->
              Printf.printf "%S %s %s %s\n" instance_location keyword schema_location value)
            values)
        keywords)
    (Results.collect_annotations results);

  (* The same annotations as a list, in the order of the results. *)
  Printf.printf "%d annotations\n" (List.length (Results.annotations results));

  (* A collector keeps its results until it is reset. *)
  Results.reset results;
  Printf.printf "%d rows\n" (List.length (Results.results results))
(* true *)
(* "" title # "Person" *)
(* "/name" title #/properties/name "Name" *)
(* 2 annotations *)
(* 0 rows *)
```

`Results.collect_annotations` groups the annotations of a verbose collector by instance location, then keyword,
then schema location, as association lists in ascending order of their keys. `Results.annotations` returns the same
annotations as a list of records, and `Results.schema_location_fragment` is the `#`-fragment form of a schema
location that the grouped form uses. Collecting runs the general evaluator over the compiled graph, not the
fail-fast plans.

### Threads and domains

A validator's compiled program is never written to after compilation, so any number of system threads (OCaml 4.14
and 5) and domains (OCaml 5) can validate with one validator at once, with no lock. A `Document.t` is immutable and
can be shared the same way. A `Results.t` collector belongs to one evaluation at a time.

An evaluation needs working memory: a few buffers that grow to what the largest instance so far needed, and are
kept. The package has no global state and no thread-local state, so that memory is in one of two places.

- **In the validator.** A validator keeps one set, which one evaluation at a time holds, taken with an atomic compare
  and set. An evaluation that finds it held, by another thread or domain or by its own caller further up the stack
  (a custom format that validates), makes its own for that one evaluation. Every call is therefore correct wherever
  it is made from. A validator used by one thread at a time allocates nothing in the steady state. Evaluations that
  overlap each allocate their working memory, except the one that holds the validator's.
- **In a scratch the caller owns.** Every validating function takes `?scratch`, a `Scratch.t` made with
  `Scratch.create ()`. A caller that validates from several threads or domains at once gives each its own, and then
  no evaluation contends with another and none allocates. A scratch serves any number of validators, one evaluation
  at a time. An evaluation given a scratch that is already in use makes buffers of its own for that evaluation.

An optional argument is passed as an option, which is a block of its own when it is built at the call
(`~scratch:s` builds `Some s` each time). To allocate nothing, build the option once and pass it as it is, with
`?scratch`:

```ocaml
let () =
  let validator = Result.get_ok (Validator.compile_string {|{"required": ["id"]}|}) in
  let texts = [ {|{"id": 1}|}; {|{"id": 2}|}; {|{}|} ] in
  let count_valid () =
    (* One scratch for each thread, built once and passed as it is. *)
    let scratch = Some (Scratch.create ()) in
    List.fold_left
      (fun count text -> if Validator.is_valid_string ?scratch validator text then count + 1 else count)
      0 texts
  in
  let counts = Array.make 4 0 in
  let threads = List.init 4 (fun i -> Thread.create (fun () -> counts.(i) <- count_valid ()) ()) in
  List.iter Thread.join threads;
  (* 2 2 2 2 *)
  print_endline (String.concat " " (List.map string_of_int (Array.to_list counts)))
```

With domains the code is the same, with `Domain.spawn` and `Domain.join` in place of `Thread.create` and
`Thread.join`. The package is built for OCaml 4.14, so it uses neither domains nor effects itself.

Every sample above is in `test/readme_test.ml`, which compiles and runs each one, compares what it writes with its
comments, and fails if a block of OCaml in this file is not in that program line for line.

## Performance

Figures are being measured.

## How it works

The pipeline follows the C# evaluator stage for stage, as the Rust, Go and Object Pascal ports do. The loader
identifies documents, resources, anchors, dialects and vocabularies. The compiler builds one node per schema location
with its keywords digested and `$ref`s resolved, and analyses evaluated-property marking, in-place cycles and
`oneOf`/`anyOf` discriminators. The node graph then compiles to fail-fast plans, which one evaluator interprets:

- each plan holds only the keywords its node has, grouped by the kind of value they apply to, and a child that only
  tests a type is tested where it is used and never entered;
- an object is checked in one pass over its properties, with names looked up by length and then as machine words,
  and `required` as a bit mask filled in the same pass;
- `$ref`, `allOf`, `if`/`then`/`else`, dependencies and `oneOf`/`anyOf` over object schemas fuse into that one pass,
  which also decides `unevaluatedProperties` from the properties it covered;
- `oneOf`/`anyOf` narrow by a discriminator property or by type;
- instances are a flat tape of two words per value over the UTF-8 text, with strings read in place and numbers
  classified when parsed;
- common pattern shapes (literals, class sequences, separated lists, line lengths) match the UTF-8 bytes without a
  regular expression engine, and other patterns run on the package's own ECMA-262 engine (see
  [Patterns](#patterns));
- numbers compare exactly across the integers from -2^63 to 2^64 - 1 and the floats, and `multipleOf` is decided on
  the decimal digits of the text.

The compiled program is flat arrays that refer to each other by index, as in the Go source. An OCaml `int` has 63
bits where Go's has 64, so a bit mask here holds 62 members where Go's holds 64, and a set that needs more takes the
path the Go module takes beyond 64. The answer is the same either way, and `test/limits_test.ml` crosses each of
those edges. A float is never boxed during validation: the floats of a document are in one flat float array.

[OPTIMIZATIONS.md](OPTIMIZATIONS.md) checks each technique of the Go module against this source, and lists what is
not done yet.

## What allocates

Validating a `Document.t`, a `string` or a `Bytes.t` with the `is_valid` and `validate` functions allocates nothing
on the OCaml heap in the steady state, which is after the first validations have grown the working memory.
`test/allocations_test.ml` measures the words allocated (`Gc.minor_words` and `Gc.allocated_bytes`) around 200 calls
for each of 117 inputs across the keyword paths it lists, and holds every one to none. These allocate, whichever
way they are called:

- an asserted `regex`, `idn-hostname` or `idn-email` format;
- an asserted `hostname` format for a host name with a label that starts with `xn--`, and an asserted `email` format
  for an address whose domain has one;
- a custom format, which is given a copy of the string;
- a `multipleOf` whose divisor has more than 17 significant digits;
- JSON text with a number of more than 18 significant digits, in the case that its nearest float cannot be decided
  from the first 18;
- a property name or a string `enum` value looked up in a set of more than 32,768 names;
- text that is not JSON given to a `validate` function, which returns the reason (the `is_valid` functions allocate
  nothing for it);
- an evaluation that finds its working memory held (see [Threads and domains](#threads-and-domains));
- a `?scratch` argument written as `~scratch:s` or `?scratch:(Some s)` at the call, which builds the option each
  time.

Compiling a schema, parsing with `Document.parse_string` and `Document.parse_bytes`, and evaluating with a results
collector allocate.

## Limits

- **Platform.** A 64-bit OCaml, where an `int` has 63 bits. The package is not available for 32-bit platforms or
  for compilers to JavaScript, whose `int` is 32 bits.
- **Size.** A document's text may have 2^31 - 1 bytes. Longer text is a parse error ("document too large").
- **Nesting.** A document's arrays and objects may nest 1,000 deep (`Document.max_depth`). Deeper text is a parse
  error, for a schema and for an instance.
- **Recursion.** A schema that recurses in place on a cycle, without consuming the instance, is stopped at
  `max_depth` (128 unless the options say otherwise). The instance is then not valid, and the `validate` and
  `evaluate` functions return `Error Depth_exceeded`.
- **Numbers.** Integers from -2^63 to 2^64 - 1 are held exactly and compared exactly with each other and with every
  other number. Any other number is held as the nearest float. A number beyond the range of a float is a parse error.
- **Duplicate property names.** The last value is kept, at the position of the first.
- **Patterns.** Groups may nest 1,000 deep, and the bounds of a counted quantifier saturate at 2,147,483,647. The
  backtracking matcher has no step budget (see [Patterns](#patterns)).

## Patterns

`pattern` and `patternProperties` have ECMA-262 semantics (the `u` flag grammar, then the Annex B grammar for a
pattern only that accepts), whichever compiler built the program. A pattern outside the common shapes runs on the
package's own engine, a port of the Go module's `internal/ecmaregex`. The grammar is that of ECMAScript 2025, with
lookbehind, named groups, modifier groups and Unicode property escapes. The engine was compared with answers
recorded from V8: it agrees on 61,383 patterns and 1,057,650 match answers.

The engine has two matchers:

- **An automaton over ASCII text.** A pattern with no lookaround, backreference, multiline anchor or word boundary,
  whose automaton has at most 256 states, gets one when the schema is compiled. It decides an ASCII text with one
  table read per byte.
- **A backtracking matcher.** Every other pattern, and a text that is not ASCII for a pattern that has an automaton.
  Its stack is explicit and kept in the evaluation's working memory, so a long text cannot overflow the thread's
  stack and a match allocates nothing once the stack has grown.

The backtracking matcher has no step budget. A pattern with nested quantifiers can take exponential time on a text
it does not match, as it can in any backtracking engine. A schema whose patterns are not trusted should not be given
texts that are not trusted.

## Differences from the Go module and the Object Pascal port

- **Patterns.** The Go module hands a pattern with no lookaround or backreference to the Go standard library's
  `regexp`, which matches in linear time. OCaml's standard library has no counterpart, so every pattern here runs
  on the package's own matchers, as in the Object Pascal port (see [Patterns](#patterns)). A pattern can therefore
  take exponential time on a text in cases where the Go module's would not: a pattern that has no automaton, and a
  text that is not ASCII.
- **Errors are values.** Nothing raises for a schema or an instance that is wrong. The compile functions and the
  `validate` and `evaluate` functions return a `result` whose error is `Parse_error`, `Compile_error` or
  `Depth_exceeded`, where the Go module returns an `error` and the Object Pascal port raises an exception (with a
  `Try` form beside it). The `is_valid` functions report text that is not JSON as not valid, as the Go module's do.
- **Working memory.** A validator keeps one set of working memory and a caller can give each thread or domain a
  `Scratch.t`. The Go module takes it from a pool that belongs to the validator, and the Object Pascal port keeps
  it in thread variables that a thread releases before it ends. Here nothing is released by hand, since a scratch
  is collected like any other value.
- **No process-wide pattern cache.** The Go module keeps every compiled pattern for the life of the process. Here a
  pattern is compiled with its schema, and the identical patterns of one schema share a matcher.
- **Options.** The options are a record, built by a function with optional arguments. A custom format is a function
  from a `string` to a `bool`, and receives a copy of the value, as in the Go module (the Object Pascal port hands it
  the bytes in place). A resolver returns a `Document.t option`.
- **Annotations.** `Results.collect_annotations` returns association lists in ascending order of their keys, where
  the Go module returns maps.
- **`multipleOf`.** A divisor of more than 17 significant digits takes the path that allocates, where the Go module
  and the Object Pascal port take it beyond 18. The answers are the same.
- The URI, IRI, URI template and e-mail formats read their grammars directly, as in the Object Pascal port, where
  the Go module uses the standard library's `regexp`.

## Unicode

The package takes no Unicode data from the compiler or the system. Every property it reads is in its own tables,
which hold Unicode 17 (`ucd/tables.ml`, read through `Corvus_json_schema_ucd`). They are the general categories, the
scripts and script extensions, the binary properties ECMA-262 lists, simple case folding and the simple uppercase
mapping. `pattern` and `patternProperties` build their `\p{...}` classes and their case-insensitive groups from
those tables, and the `hostname`, `idn-hostname` and `idn-email` formats read the same data. A URI is normalized by
lowering the letters A to Z only, as RFC 3986 and RFC 3987 specify.

`tools/gen-ucd-tables.ps1` writes the tables from the Unicode data of the `regress` crate, which the Rust port uses.
It is the Go module's generator with the part that writes Go replaced, so the two sets of tables hold the same data.

## Tests

```sh
dune build
dune test
```

The tests are plain executables that print their counts and exit with a non-zero status when a check fails. Those
that read the JSON-Schema-Test-Suite take it from `JSON_SCHEMA_TEST_SUITE`, or from the submodule of the
Corvus.JsonSchema repository when the package is built inside it. Where neither is there (a build of the published
package alone), they say that they were skipped.

- `test/suite_test.ml`: the JSON-Schema-Test-Suite, every case fail-fast (as a document, as bytes and as a string)
  and through a collector at each level. `SUITE_DRAFT` and `SUITE_FILTER` narrow it.
- `test/results`: the suite's annotation tests and the results expectations shared with the C#, Rust, Go, Java and
  TypeScript evaluators.
- `test/allocations_test.ml`: validation of a document, of bytes and of a string allocates nothing in the steady
  state.
- `test/api`: the public functions, the options, validators shared between threads (and between domains on
  OCaml 5), and the samples of the Go module's documentation.
- `test/document_test.ml`, `test/formats_test.ml`, `test/number_formats_test.ml`, `test/pattern_test.ml`,
  `test/plan_test.ml`, `test/metaschemas_test.ml`: the parser and the number conversions, the format checks against
  the suite's format tests, the regex-free matchers, the fail-fast plans against the general evaluator, and the
  embedded metaschemas.
- `test/limits_test.ml`: the edges where a mask holds 62 members and the Go module's holds 64.
- `test/ecmaregex` and `test/ucd`: the pattern engine against answers recorded from V8 (`v8_oracle.json`, the Go
  module's) and the patterns of the suite, and the Unicode tables.
- `test/differential_test.ml`: the plans against the general evaluator on the jsonschema-benchmark corpora and on
  mutations of their instances. It runs when `JSONSCHEMA_BENCHMARK` names a checkout, and says that it was skipped
  otherwise.
- `test/readme_test.ml`: the samples in this README and in the documentation page.
- `test/version_test.ml`: the version is the one `dune-project` declares, and has a section in
  [VERSIONHISTORY.md](VERSIONHISTORY.md).

`dune build @fmt` checks the formatting (ocamlformat, the version `.ocamlformat` names) and `dune build @doc` builds
the documentation of the interface.

## Releases

A release is made by the Corvus.JsonSchema repository's `ocaml-publish` workflow when the version in `dune-project`
changes. It pushes this directory to
[corvus-dotnet/corvus-json-schema-ocaml](https://github.com/corvus-dotnet/corvus-json-schema-ocaml) and tags it
`v<version>`. Submitting a release to the opam repository is done by hand afterwards, with
`opam publish https://github.com/corvus-dotnet/corvus-json-schema-ocaml/archive/refs/tags/v<version>.tar.gz` or a
pull request to [ocaml/opam-repository](https://github.com/ocaml/opam-repository) that adds
`packages/corvus-json-schema/corvus-json-schema.<version>/opam`.

Issues and pull requests belong in
[corvus-dotnet/Corvus.JsonSchema](https://github.com/corvus-dotnet/Corvus.JsonSchema), where the source is
developed (`src-ml/corvus-json-schema`).

## License

Apache 2.0.
