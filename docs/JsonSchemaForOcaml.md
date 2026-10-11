# JSON Schema for OCaml

[corvus-json-schema](https://github.com/corvus-dotnet/Corvus.JsonSchema/tree/main/src-ml/corvus-json-schema) is the
Corvus JSON Schema evaluator for OCaml: draft 4, 6, 7, 2019-09 and 2020-12. It is a port of the .NET runtime
evaluator (see [Runtime Evaluator](RuntimeEvaluator.md)) by way of the [Go module](JsonSchemaForGo.md), and gives the
same results and annotations. It has no dependencies beyond the OCaml standard library, and no C code.

It needs OCaml 4.14 or later on a 64-bit platform, and is tested with OCaml 4.14 and 5.5.

A schema is compiled once into a node graph and fail-fast plans: each plan holds only the checks its subschema needs,
and an object's keywords are fused into one pass over its properties. Any number of instances can then be validated
against it.

- **Conformant.** Passes the whole JSON-Schema-Test-Suite (required, optional and `optional/format`, every draft) except
  `draft4/optional/zeroTerminatedFloats.json`, and all of its annotation tests.
- **Results and annotations.** Basic, Detailed and Verbose results, and annotations, the same as every other Corvus
  implementation.
- **Allocation-free validation.** In the steady state, validating a parsed document, or JSON text, allocates nothing
  on the OCaml heap. The package's README lists the exceptions, such as an asserted `regex`, `idn-hostname` or
  `idn-email` format, a custom format, and a `multipleOf` whose divisor has more than 17 significant digits.
- **Threads and domains.** A validator is immutable once compiled, and is safe to share between system threads and
  between the domains of OCaml 5.
- **Every array read is checked.** The package is built with the compiler's bounds checks on, and uses no unchecked
  read, no `Obj` and no C code.

## Install

Until the package is in the opam repository, pin it from its own repository, which holds the package and nothing
else, with a tag `v<version>` for each release:

```sh
opam pin add corvus-json-schema https://github.com/corvus-dotnet/corvus-json-schema-ocaml.git#v0.1.0
```

Once it is in the [opam repository](https://opam.ocaml.org/packages/corvus-json-schema/):

```sh
opam install corvus-json-schema
```

With dune, name the library `corvus-json-schema` in the `libraries` field. The one module a program uses is
`Corvus_json_schema`.

## Validate

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

A validator is not modified by validation. No function of the package raises an exception for a schema or an
instance that is wrong: the compile functions return a `result`, whose error is `Parse_error` for a schema that is
not JSON and `Compile_error` for one that cannot be compiled (an unresolvable reference, an invalid pattern). The
samples below use `Result.get_ok` for brevity.

## JSON text

`Validator.is_valid_string` and `is_valid_bytes` take JSON text. They parse it into buffers that are reused and
validate it in place, so in the steady state they allocate nothing. To validate the same text more than once, parse
it into a `Document.t`: the UTF-8 text and one flat array of values, with strings read in place where they have no
escapes. A document is immutable and safe to share.

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

The `is_valid` functions report text that is not JSON as not valid. The `validate` functions return the reason
instead: `Error (Parse_error e)` with the message and the byte offset, or `Error Depth_exceeded` when the schema
recursed in place beyond the maximum depth. A match of a pattern that reached its limit is neither valid nor not
valid: the `validate` functions return `Error (Pattern_match_limit _)` and the `is_valid` functions raise (see
[Limits on patterns](#limits-on-patterns)).

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

## Options

The compile functions take `?options`, a record that the function `options` builds from optional arguments:

| Argument | Meaning |
|---|---|
| `default_dialect` | The dialect of a schema without `$schema` (default `Draft2020_12`). |
| `assert_format` | `true` asserts `format`, `false` never does. Without it the schema's vocabularies decide. |
| `assert_format_in_legacy_drafts` | Without `assert_format`, assert `format` in drafts 4 to 7 too. |
| `assert_content` | Assert `contentEncoding` and `contentMediaType` in draft 7 (default `true`). |
| `formats` | Custom formats by name: a function from the string (or a number's JSON text) to whether it is valid. |
| `resolver` | A function from an absolute URI to the document, for remote references. The standard metaschemas are built in. |
| `base_uri` | The base URI of the root document. |
| `entry_point` | A subschema to validate against, such as `#/$defs/item`. |
| `max_depth` | The deepest the evaluator recurses in place (default 128). |
| `pattern_match_limit` | The seconds one match of a `pattern` or `patternProperties` regular expression may take (default 1.0). `0.` or less turns the limit off. See [Limits on patterns](#limits-on-patterns). |
| `pattern_match_clock` | The clock that limit is counted on, a function that returns the time in seconds. Without it the clock is `Sys.time`, the processor time of the process. |

A custom format and a document resolver are plain functions.

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

## Results and annotations

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

Each row is a `Results.result` with `is_match`, `message`, `evaluation_location`, `schema_evaluation_location` and
`document_evaluation_location`. `Results.Basic` records the failures without messages, `Results.Detailed` adds the
messages, and `Results.Verbose` records every keyword, passing ones and annotations included.

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

## Threads and domains

A validator is shared between threads and domains without a lock. An evaluation needs working memory, which a
validator keeps one set of. An evaluation that finds it in use makes its own, so every call is correct, but
evaluations that overlap allocate. A caller that validates from several threads or domains at once gives each its
own `Scratch.t` in the `?scratch` argument, and then none contends with another and none allocates. Build the option
once and pass it as it is, since an option built at the call is an allocation.

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

With domains the code is the same, with `Domain.spawn` and `Domain.join`.

## Patterns

`pattern` and `patternProperties` have ECMA-262 semantics. The common shapes of pattern are matched with no regular
expression engine. Any other pattern runs on the package's own engine, which reads every character class from the
package's own Unicode 17 data, so a pattern means the same whichever compiler built the program. The engine agrees
with answers recorded from V8 on 61,694 patterns and 1,091,280 match answers, and on the 13 files of the ECMA-262
regular expression test data (98,839 patterns and 4,268,313 answers).

A pattern is read with the grammar of the `u` flag and matched by Unicode code point. A pattern that grammar refuses
is read with the Annex B grammar of a pattern with no flag, which many schemas need (`\-` outside a class, `\_`, a
lone `{`). Such a pattern is matched by UTF-16 code unit, as ECMA-262 matches a pattern with no `u` flag. The two
differ only for a string with a character beyond the Basic Multilingual Plane, which is two code units. `^.$`
matches `"\ud801\udc00"` (the one character U+10400), and `^\-?.$` does not, because the dot takes one unit of the
two. A pattern that neither grammar accepts is a `Compile_error`.

The answers are V8's except where V8 itself departs from ECMA-262, and there the package follows ECMA-262. A
quantifier whose first count is greater than its second is an error whatever the size of the numbers, so
`a{2147483648,2147483647}` is refused, which V8 takes. A group name used twice is an error unless the two groups
are in separate alternatives of one disjunction, so `(?<m>a)(?:x|(?<m>b))` is refused, which V8 takes.
`\p{WSpace}` is not a property name ECMA-262 lists, so the pattern is read with no flag, as the characters
`p{WSpace}`, where V8 reads it as `\p{White_Space}`. `Katakana_Or_Hiragana` (`Hrkt`) is a Script value that no
character has, so `\p{sc=Hrkt}` is valid and matches nothing, where V8 refuses it. And the properties are those of
Unicode 17, so the scripts Sidetic, Tolong_Siki, Beria_Erfe and Tai_Yo are known, which V8 13.6 does not know.

A JSON string can name half of a surrogate pair with an escape, as in `"\ud800"`. That is not Unicode text, and a
document is UTF-8 and cannot hold it. Parsing such text is a `Parse_error`, and the `is_valid` functions report it
as not valid, whatever the schema. So a pattern is never matched against such a string.

A pattern with no lookaround, backreference, multiline anchor or word boundary gets an automaton that decides an
ASCII text with one table read per byte, when the automaton is small enough. Every other pattern, and a text that
is not ASCII, runs on a backtracking matcher. The Go module hands most patterns to Go's linear-time `regexp`
package, which OCaml's standard library has no counterpart of. A backtracking matcher can take time that doubles
with each character of a text it does not match, so a match is limited (see below), and the matcher does four
things to keep a pattern from going the long way round. It looks at the next character before it takes a way, as
V8 does, and takes no way that character rules out. It remembers the places where every way has failed, when the
pattern has no backreference, so that a repetition inside a repetition (`(a*)*b`, `^(?:a+)+$`) and alternatives
that are prefixes of each other (`^(?:a|aa)+$`) take time proportional to the pattern times the text. A repetition
whose body cannot consume a character, such as `(?:\b|(?=a)|){4,5}`, is
not such a case. Every iteration of it is empty, so the matcher runs the body once when the minimum is one or more
and not at all when it is zero, which is what ECMA-262 says such a repetition matches. And a counted repetition is
not tried at all at a place where the rest of the pattern, with the iterations the repetition has still to make,
needs more of the string than is left.

## Limits on patterns

A schema may come from outside, and so may an instance. Neither is to make a validation run without end or hold
memory out of proportion to what it was given. So one match has a limit, and one pattern has limits of size. A
limit that is reached is an error of its own, never the answer "valid" or "not valid", and never the error of a
pattern that is not valid.

| Limit | Value | When it is reached |
|---|---|---|
| Time of one match of a pattern against one string | 1 second by default (the option `pattern_match_limit`, in seconds). `0.` or less turns it off. | `Error (Pattern_match_limit {pattern; limit; memory = false})` from the `validate` and `evaluate` functions. The `is_valid` functions, which return a `bool` alone, raise `Pattern_match_limit_reached` with the same fields. |
| Backtracking memory of one match | 1,048,576 entries, which is 32 MB | The same, with `memory = true`. |
| Length of a pattern | 1,048,576 bytes | `Error (Pattern_too_large {pattern; keyword; limit = "length"; value})` from the compile functions. |
| Nesting of the groups of a pattern | 1,000 deep | The same, with `limit = "nesting"`. |
| Size of a pattern | 1,048,576 as it is read: 64 for each term, 32 for each further alternative, 8 for each range of a class, and what case folding and groups of one name add | The same, with `limit = "size"`. |

```ocaml
let () =
  (* One match of a pattern may take a twentieth of a second. The default is one second, and 0. turns the limit off. *)
  let options = options ~pattern_match_limit:0.05 () in
  let validator = Result.get_ok (Validator.compile_string ~options {|{"pattern": "^(?:(a+)\\1*)+$"}|}) in
  let text = {|"aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaab"|} in

  (* A match that reaches the limit is an error and never an answer: no answer within 0.05 s *)
  (match Validator.validate_string validator text with
  | Ok valid -> Printf.printf "%b\n" valid
  | Error (Pattern_match_limit { limit; _ }) -> Printf.printf "no answer within %g s\n" limit
  | Error e -> print_endline (error_to_string e));

  (* A function that returns a bool alone raises: abandoned *)
  (match Validator.is_valid_string validator text with
  | valid -> Printf.printf "%b\n" valid
  | exception Pattern_match_limit_reached _ -> print_endline "abandoned");

  (* A pattern over a limit of size is refused when the schema is compiled: nesting, 1000 *)
  let deep = String.concat "" (List.init 1000 (fun _ -> "(?:")) ^ "a" ^ String.make 1000 ')' in
  match Validator.compile_string (Printf.sprintf {|{"pattern": "%s"}|} deep) with
  | Ok _ -> print_endline "compiled"
  | Error (Pattern_too_large { limit; value; _ }) -> Printf.printf "%s, %d\n" limit value
  | Error e -> print_endline (error_to_string e)
```

The backtracking matcher counts its steps and reads the clock every 4,096 of them, which is every 23 to 74
microseconds as measured, so a match is abandoned at the first reading after its limit and a match that ends before
its first reading never reads the clock. The limit is on each match and not on the validation. It applies to
`pattern` and to `patternProperties`, failing fast and collecting, and to the matches the compiler makes of a
pattern against the property names the schema declares, for which the compile functions return the error. The
evaluation is abandoned at the match and its working memory is given back.

The clock is `Sys.time`, the processor time of the process, which is the one clock the OCaml standard library has.
It does not move while the process waits. And it is the time of the whole process, so in a program that computes on
several domains at once it runs that many times faster, and a match there is abandoned sooner than its limit. A
program for which either matters gives its own clock, such as `options ~pattern_match_clock:Unix.gettimeofday ()`.

The automaton and the matchers for the common shapes of pattern take time proportional to the string and have no
limit. With the limit turned off a match has no limit on its time or its memory.

A pattern over a limit of size is not read past the limit, so the error says it is too large to read and nothing
of whether it is valid. The Go module counts the same size for every pattern. The `regex` format reads a string of
the instance with the same limits, and a string over one is not a valid regular expression to it.

The memory of the places where a way has failed is at most 8 MB, is taken into use only by a match that has made
64 steps for each position of its string, and stays in the working memory of the evaluation, so a match with it
allocates nothing once it has grown.

Some patterns still reach the limit on a long enough string: a backreference inside nested repetitions
(`^(?:(a+)\1*)+$`), for which V8 takes time that doubles with each character too, nested counted ranges
(`^(?:a{0,1000}){0,1000}$`), and a search that reads the rest of the string from every position of it, such as a
lookaround that scans (`(?=.*c)b` on 100,000 characters).

## Performance

Figures are being measured.

## Links

- Package repository: [corvus-dotnet/corvus-json-schema-ocaml](https://github.com/corvus-dotnet/corvus-json-schema-ocaml)
- Source and README: [src-ml/corvus-json-schema](https://github.com/corvus-dotnet/Corvus.JsonSchema/tree/main/src-ml/corvus-json-schema)
- The other languages: see [Other languages](OtherLanguages.md)
