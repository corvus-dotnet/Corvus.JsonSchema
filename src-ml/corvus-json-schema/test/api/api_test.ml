(* Public interface behaviour: api_test.go of the Go module, which was ported from the Rust crate's tests/api.rs. Each
   Go test is a section here, with the Go test's name in snake case, the same schemas, the same instances and the same
   expectations.

   Go's t.Fatal stops the test it is in. Here a test is a function run by [test], and a failure that stops it is an
   exception, which [test] records as a failed check. *)

open Corvus_json_schema
module Check = Corvus_json_schema_test_support.Check

(* The internal modules, for the two tests that look at the plan a schema was compiled to. *)
module Internal_document = Corvus_json_schema__Document
module Internal_options = Corvus_json_schema__Options
module Compiler = Corvus_json_schema__Compiler
module Plan = Corvus_json_schema__Plan
module Program = Corvus_json_schema__Program

(* test runs one Go test as a section. *)
let test name body =
  Check.section name;
  try body () with raised -> Check.fail ("stopped by " ^ Printexc.to_string raised)

(* mustParse of document_test.go. *)
let must_parse json =
  match Document.parse_string json with
  | Ok d -> d
  | Error e -> failwith (Printf.sprintf "parse %S: %s" json (Document.error_to_string e))

(* mustCompile of results_test.go. *)
let must_compile ?options schema =
  match Validator.compile_string ?options schema with
  | Ok v -> v
  | Error e -> failwith (Printf.sprintf "compile %s: %s" schema (error_to_string e))

(* expect_agreement checks that fail-fast evaluation (through the plans) and collecting evaluation (through the
   general evaluator) give the same answer for each instance. *)
let expect_agreement v label instances =
  List.iter
    (fun instance ->
      match Validator.evaluate_string v ~results:(Results.create Results.Basic) instance with
      | Error e -> Check.fail (Printf.sprintf "%s on %s: %s" label instance (error_to_string e))
      | Ok collected ->
          let fast = Validator.is_valid_string v instance in
          Check.checkf (fast = collected) "%s on %s: fail-fast %b, collecting %b" label instance fast collected)
    instances

let expect_valid v instance want =
  let got = Validator.is_valid_string v instance in
  Check.checkf (got = want) "is_valid_string(%s) = %b, want %b" instance got want;
  let document = must_parse instance in
  let got = Validator.is_valid_document v document in
  Check.checkf (got = want) "is_valid_document(%s) = %b, want %b" instance got want

let is_depth_exceeded = function Error Depth_exceeded -> true | Ok _ | Error _ -> false

(* describe is a result as text, for the description of a check. *)
let describe = function Ok b -> string_of_bool b | Error e -> error_to_string e

(* root_body is the body of the plan of a schema's root, which Go reads from the unexported fields of its validator
   (v.program.plans[v.program.root].body). A validator's program is not reachable here, so this compiles the schema
   the way Validator.compile_string does, with the default options, and reads the plan of that program. *)
let root_body schema =
  match Internal_document.parse_string schema with
  | Error e -> failwith (Printf.sprintf "parse %S: %s" schema (Internal_document.error_to_string e))
  | Ok document ->
      let options = Internal_options.default in
      let program = Plan.new_program (Compiler.compile_document document options) options in
      program.Program.plans.(program.Program.root).Program.body

let fused_of (body : Program.body option) = match body with Some b -> b.fused | None -> None

let keeps_the_dynamic_scope () =
  let tree =
    must_parse
      {|{
        "$schema": "https://json-schema.org/draft/2020-12/schema",
        "$id": "https://example.com/tree",
        "$dynamicAnchor": "node",
        "type": "object",
        "properties": { "data": true, "children": { "type": "array", "items": { "$dynamicRef": "#node" } } }
      }|}
  in
  let strict =
    {|{
      "$schema": "https://json-schema.org/draft/2020-12/schema",
      "$id": "https://example.com/strict-tree",
      "$dynamicAnchor": "node",
      "$ref": "tree",
      "unevaluatedProperties": false
    }|}
  in
  let v =
    must_compile
      ~options:(options ~resolver:(fun uri -> if uri = "https://example.com/tree" then Some tree else None) ())
      strict
  in
  expect_valid v {|{ "children": [{ "data": 1, "children": [] }] }|} true;
  expect_valid v {|{ "children": [{ "daat": 1 }] }|} false

(* The number of code points of UTF-8 text (Go's utf8.RuneCountInString, for valid UTF-8): the bytes that are not
   continuation bytes. *)
let rune_count s =
  let count = ref 0 in
  String.iter (fun c -> if Char.code c land 0xC0 <> 0x80 then incr count) s;
  !count

let custom_formats_are_asserted_when_format_assertion_is_on () =
  let even_length s = rune_count s mod 2 = 0 in
  let v =
    must_compile
      ~options:(options ~assert_format:true ~formats:[ ("even-length", even_length) ] ())
      {|{ "type": "string", "format": "even-length" }|}
  in
  expect_valid v {|"ab"|} true;
  expect_valid v {|"abc"|} false;
  expect_agreement v "even-length" [ {|"ab"|}; {|"abc"|}; {|1|} ];
  (* A custom format on a number receives its JSON text. *)
  let digits =
    must_compile
      ~options:(options ~assert_format:true ~formats:[ ("int32", fun s -> s = "12") ] ())
      {|{ "format": "int32" }|}
  in
  expect_valid digits {|12|} true;
  expect_valid digits {|12.0|} false;
  expect_agreement digits "custom int32" [ {|12|}; {|13|}; {|"12"|} ]

let format_is_an_annotation_by_default_and_asserted_on_request () =
  let schema = {|{ "format": "ipv4" }|} in
  expect_valid (must_compile schema) {|"not an address"|} true;
  expect_valid (must_compile ~options:(options ~assert_format:true ()) schema) {|"not an address"|} false;
  expect_valid (must_compile ~options:(options ~assert_format:true ()) schema) {|"10.0.0.1"|} true;
  let draft7 = {|{ "$schema": "http://json-schema.org/draft-07/schema#", "format": "ipv4" }|} in
  expect_valid (must_compile draft7) {|"not an address"|} true;
  expect_valid
    (must_compile ~options:(options ~assert_format_in_legacy_drafts:true ()) draft7)
    {|"not an address"|} false;
  expect_valid
    (must_compile ~options:(options ~assert_format_in_legacy_drafts:true ~assert_format:false ()) draft7)
    {|"x"|} true

let content_is_asserted_in_draft7_only () =
  let schema = {|{ "contentEncoding": "base64", "contentMediaType": "application/json" }|} in
  let draft7 = must_compile ~options:(options ~default_dialect:Draft7 ()) schema in
  expect_valid draft7 {|"eyJhIjogMX0="|} true;
  expect_valid draft7 {|"bm90IGpzb24="|} false;
  expect_valid draft7 {|"not base64"|} false;
  expect_valid draft7 {|12|} true;
  expect_agreement draft7 "content" [ {|"eyJhIjogMX0="|}; {|"bm90IGpzb24="|}; {|"not base64"|}; {|12|} ];
  expect_valid
    (must_compile ~options:(options ~default_dialect:Draft7 ~assert_content:false ()) schema)
    {|"not base64"|} true;
  expect_valid (must_compile schema) {|"not base64"|} true;
  let json =
    must_compile
      ~options:{ default_options with default_dialect = Draft7 }
      {|{ "contentMediaType": "application/json" }|}
  in
  expect_valid json {|"[1, 2]"|} true;
  expect_valid json {|"[1, 2"|} false

let entry_point_evaluates_from_a_subschema () =
  let schema = {|{ "$defs": { "positive": { "type": "number", "exclusiveMinimum": 0 } }, "type": "string" }|} in
  let v = must_compile ~options:(options ~entry_point:"#/$defs/positive" ()) schema in
  expect_valid v {|3|} true;
  expect_valid v {|-3|} false;
  expect_valid v {|"s"|} false;
  Check.check "a missing entry point compiled"
    (Result.is_error (Validator.compile_string ~options:(options ~entry_point:"#/$defs/missing" ()) schema))

let base_uri_resolves_relative_references () =
  let item = must_parse {|{ "type": "integer" }|} in
  let resolver uri = if uri = "https://example.com/schemas/item.json" then Some item else None in
  let v =
    must_compile
      ~options:(options ~base_uri:"https://example.com/schemas/root.json" ~resolver ())
      {|{ "items": { "$ref": "item.json" } }|}
  in
  expect_valid v {|[1, 2]|} true;
  expect_valid v {|[1, "2"]|} false;
  let from_uri =
    match Validator.compile_uri ~options:(options ~resolver ()) "https://example.com/schemas/item.json" with
    | Ok v -> v
    | Error e -> failwith (error_to_string e)
  in
  expect_valid from_uri {|1|} true;
  expect_valid from_uri {|"1"|} false;
  Check.check "an unknown URI compiled"
    (Result.is_error (Validator.compile_uri ~options:(options ~resolver ()) "https://example.com/schemas/other.json"))

let default_dialect_applies_to_schemas_without_schema () =
  let schema = {|{ "items": [{ "type": "string" }], "additionalItems": false }|} in
  expect_valid (must_compile ~options:(options ~default_dialect:Draft7 ()) schema) {|["a", "b"]|} false;
  (* In 2020-12 an array-valued "items" is not the tuple form, so neither keyword applies. *)
  expect_valid (must_compile schema) {|["a", "b"]|} true

let compilation_errors () =
  let compiled = function Ok _ -> "it compiled" | Error e -> error_to_string e in
  let is_compile_error = function Error (Compile_error _) -> true | Ok _ | Error _ -> false in
  let is_parse_error = function Error (Parse_error _) -> true | Ok _ | Error _ -> false in
  let r = Validator.compile_string {|{ "$ref": "https://example.com/missing.json" }|} in
  Check.checkf (is_compile_error r) "an unresolvable reference: %s" (compiled r);
  let r = Validator.compile_string {|{ "pattern": "a(" }|} in
  Check.checkf (is_compile_error r) "an invalid pattern: %s" (compiled r);
  let r = Validator.compile_string {|{ "type": |} in
  Check.checkf (is_parse_error r) "schema text that is not JSON: %s" (compiled r)

let in_place_recursion_beyond_max_depth_is_an_error () =
  let schema = {|{ "$defs": { "loop": { "allOf": [{ "$ref": "#/$defs/loop" }] } }, "$ref": "#/$defs/loop" }|} in
  let v = must_compile ~options:(options ~max_depth:16 ()) schema in
  let r = Validator.validate_string v {|1|} in
  Check.checkf (is_depth_exceeded r) "validate_string: %s" (describe r);
  let r = Validator.validate_document v (must_parse {|1|}) in
  Check.checkf (is_depth_exceeded r) "validate_document: %s" (describe r);
  Check.check "is_valid_string reports a runaway schema as valid" (not (Validator.is_valid_string v {|1|}));
  let r = Validator.evaluate_string v ~results:(Results.create Results.Detailed) {|1|} in
  Check.checkf (is_depth_exceeded r) "evaluate_string: %s" (describe r);
  (* The evaluator that gave up is fit for the next validation. *)
  let r = Validator.validate_string v {|2|} in
  Check.checkf (is_depth_exceeded r) "validate_string again: %s" (describe r)

let numbers_are_compared_exactly_for_multiple_of () =
  let v = must_compile {|{ "multipleOf": 0.01 }|} in
  expect_valid v {|0.07|} true;
  expect_valid v {|19.99|} true;
  expect_valid v {|0.075|} false;
  expect_valid (must_compile {|{ "multipleOf": 0.0001 }|}) {|0.0075|} true

(* Go runs eight goroutines, which validate with one validator at once. Here they are threads, which every OCaml this
   package builds on has. On OCaml 4 one thread runs at a time and they are switched between at any allocation or
   poll. The same test with domains, which run in parallel on OCaml 5, is concurrent_domains_test.ml.

   Go's validator keeps its working memory in a pool. This one keeps one set, which an evaluation that finds it held
   does without, and a caller may give each thread a scratch of its own instead. So the test is run both ways. *)
let validators_are_safe_for_concurrent_use () =
  let v = must_compile {|{ "type": "array", "items": { "type": "integer", "minimum": 0 }, "uniqueItems": true }|} in
  List.iter
    (fun own_scratch ->
      let failures = Atomic.make 0 in
      let run g =
        try
          let scratch = if own_scratch then Some (Scratch.create ()) else None in
          let items = String.concat "," (List.init 100 (fun i -> string_of_int ((g * 1000) + i))) in
          let valid = "[" ^ items ^ "]" in
          let invalid = "[" ^ items ^ ",-1]" in
          for _ = 1 to 200 do
            if (not (Validator.is_valid_string ?scratch v valid)) || Validator.is_valid_string ?scratch v invalid then
              raise Exit
          done
        with _ -> Atomic.incr failures
      in
      let threads = List.init 8 (fun g -> Thread.create run g) in
      List.iter Thread.join threads;
      Check.equal_int
        (if own_scratch then "concurrent validations that gave the wrong answer, with a scratch for each thread"
         else "concurrent validations that gave the wrong answer")
        ~expected:0 (Atomic.get failures))
    [ false; true ]

let discriminated_one_of_and_any_of_agree_with_exhaustive_evaluation () =
  let shape kind extra =
    Printf.sprintf
      {|{ "type": "object", "properties": { "kind": { "const": %S }, %S: { "type": "number" } }, "required": ["kind", %S] }|}
      kind extra extra
  in
  let branches =
    "[" ^ shape "circle" "r" ^ "," ^ shape "square" "side" ^ {|, { "properties": { "kind": { "enum": [1, true] } } }]|}
  in
  List.iter
    (fun keyword ->
      let v = must_compile (Printf.sprintf {|{ %S: %s }|} keyword branches) in
      expect_agreement v keyword
        [
          {|{ "kind": "circle", "r": 1 }|};
          {|{ "kind": "circle", "side": 1 }|};
          {|{ "kind": "square", "side": 1 }|};
          {|{ "kind": "triangle" }|};
          {|{ "kind": 1.0 }|};
          {|{ "kind": true }|};
          {|{ "kind": false }|};
          {|{}|};
          {|"circle"|};
        ])
    [ "oneOf"; "anyOf" ]

let discriminators_key_null_and_numbers_by_value () =
  let branch kind extra =
    Printf.sprintf
      {|{ "type": "object", "properties": { "kind": { "const": %s }, %S: { "type": "string" } }, "required": ["kind", %S] }|}
      kind extra extra
  in
  let schema =
    {|{ "oneOf": [|} ^ branch "null" "a" ^ "," ^ branch "1" "b" ^ "," ^ branch "1.0" "c" ^ "," ^ branch {|"x"|} "d"
    ^ {|] }|}
  in
  let v = must_compile schema in
  expect_agreement v "discriminator"
    [
      {|{ "kind": null, "a": "s" }|};
      {|{ "kind": null, "b": "s" }|};
      {|{ "kind": 1, "b": "s" }|};
      {|{ "kind": 1, "b": "s", "c": "t" }|};
      {|{ "kind": 1.0, "c": "t" }|};
      {|{ "kind": "x", "d": "s" }|};
      {|{ "kind": "y", "d": "s" }|};
      {|{ "d": "s" }|};
    ];
  (* Both numeric branches match 1 when it has both properties: oneOf fails. *)
  expect_valid v {|{ "kind": 1, "b": "s", "c": "t" }|} false

let arrays_of_simple_arrays_match_the_general_path () =
  let position = {|{ "type": "array", "minItems": 2, "maxItems": 3, "items": { "type": "number" } }|} in
  List.iter
    (fun schema ->
      expect_agreement (must_compile schema) schema
        [
          {|[]|};
          {|[[1, 2]]|};
          {|[[1, 2], [3, 4, 5]]|};
          {|[[1]]|};
          {|[[1, 2, 3, 4]]|};
          {|[[1, "a"]]|};
          {|[[1.5, 2]]|};
          {|[["x", "y"]]|};
          {|["s", [1, 2]]|};
          {|[{}, [1, 2]]|};
          {|[[[1, 2]], [[3]]]|};
          {|[[[1, "a"]]]|};
        ])
    [
      {|{ "type": "array", "items": |} ^ position ^ {| }|};
      {|{ "type": "array", "items": { "type": ["array", "string"], "minItems": 2, "items": { "type": "integer" } } }|};
      {|{ "type": "array", "items": { "type": "object", "minItems": 2 } }|};
      {|{ "type": "array", "items": { "type": "array", "items": { "type": "array", "items": { "type": "number" } } } }|};
    ]

let fused_not_required_and_absent_pattern_conditions_match_the_general_path () =
  let extensions = {|{ "patternProperties": { "^x-": true } }|} in
  List.iter
    (fun schema ->
      let v = must_compile schema in
      Check.checkf (fused_of (root_body schema) <> None) "the schema did not take a fused plan: %s" schema;
      expect_agreement v schema
        [
          {|{}|};
          {|{ "value": 1 }|};
          {|{ "value": 1, "externalValue": "u" }|};
          {|{ "externalValue": 2 }|};
          {|{ "x-y": 1, "summary": "s" }|};
          {|{ "other": 1 }|};
          {|{ "default": 1 }|};
          {|{ "200": 1 }|};
          {|{ "2XX": 1, "x-a": 1 }|};
          {|{ "600": 1 }|};
          {|{ "default": "a", "404": 1 }|};
          {|{ "kind": "k" }|};
          {|{ "kind": "k", "c": 1 }|};
          {|{ "kind": "k", "a": 1, "b": 1, "c": 1 }|};
          {|{ "kind": "k", "a": 1, "c": 1 }|};
          {|{ "kind": "k", "x-a": 1, "d": 1 }|};
          {|{ "kind": "k", "x-z": 1, "d": 1 }|};
          {|{ "kind": "k", "d": 1, "c": 1 }|};
          {|{ "kind": "j", "a": 1, "b": 1 }|};
        ])
    [
      (* not: {required} alongside unevaluatedProperties (the OpenAPI example object). *)
      {|{
        "type": "object",
        "properties": { "value": true, "externalValue": { "type": "string" }, "summary": { "type": "string" } },
        "not": { "required": ["value", "externalValue"] },
        "$ref": "#/$defs/ext",
        "unevaluatedProperties": false,
        "$defs": { "ext": |}
      ^ extensions ^ {| }
      }|};
      (* An if deciding on no name matching a pattern (the OpenAPI responses object). *)
      {|{
        "type": "object",
        "properties": { "default": { "type": "integer" } },
        "patternProperties": { "^[1-5](?:[0-9]{2}|XX)$": { "type": "integer" } },
        "$ref": "#/$defs/ext",
        "unevaluatedProperties": false,
        "if": { "patternProperties": { "^[1-5](?:[0-9]{2}|XX)$": false } },
        "then": { "required": ["default"] },
        "$defs": { "ext": |}
      ^ extensions ^ {| }
      }|};
      (* Both, gated by another condition, with a name the pattern also matches. *)
      {|{
        "type": "object",
        "properties": { "kind": true, "a": true, "b": true, "x-a": true },
        "allOf": [{ "$ref": "#/$defs/ext" }, { "properties": { "c": true } }],
        "if": { "properties": { "kind": { "const": "k" } }, "required": ["kind"] },
        "then": {
          "not": { "required": ["a", "b"] },
          "if": { "patternProperties": { "^x-": false } },
          "then": { "required": ["c"] },
          "else": { "properties": { "d": true } }
        },
        "unevaluatedProperties": false,
        "$defs": { "ext": |}
      ^ extensions ^ {| }
      }|};
    ]

let flat_fused_objects_match_the_general_path () =
  let shared = {|{ "properties": { "a": { "type": "string" }, "b": true }, "required": ["a"], "maxProperties": 3 }|} in
  List.iteri
    (fun i schema ->
      let v = must_compile schema in
      let fused = fused_of (root_body schema) in
      let as_expected = match fused with None -> false | Some f -> f.Program.flat <> None = (i < 2) in
      Check.checkf as_expected "schema %d: unexpected plan (fused %b)" i (fused <> None);
      expect_agreement v schema
        [
          {|{}|};
          {|{ "a": "x" }|};
          {|{ "a": "xy", "c": 1 }|};
          {|{ "a": 1, "c": 1 }|};
          {|{ "a": "x", "c": "1" }|};
          {|{ "a": "x", "b": null, "c": 1 }|};
          {|{ "a": "x", "b": 1, "c": 1, "d": 1 }|};
          {|{ "c": 1 }|};
          {|[]|};
        ])
    [
      {|{ "allOf": [{ "$ref": "#/$defs/s" }], "properties": { "c": { "type": "integer" } }, "$defs": { "s": |} ^ shared
      ^ {| } }|};
      {|{
        "allOf": [{ "$ref": "#/$defs/s" }, { "properties": { "a": { "type": "string" } }, "required": ["c"] }],
        "properties": { "c": { "type": "integer" } },
        "minProperties": 2,
        "$defs": { "s": |}
      ^ shared ^ {| }
      }|};
      (* The same name with different schemas stays a fused plan. *)
      {|{ "allOf": [{ "$ref": "#/$defs/s" }], "properties": { "a": { "minLength": 2 } }, "$defs": { "s": |} ^ shared
      ^ {| } }|};
    ]

let fused_objects_below_a_dynamic_reference_match_the_general_path () =
  (* The items' $dynamicRef resolves (through the scope) to "strict", whose allOf contributor is in its own
     resource. *)
  let v =
    must_compile
      {|{
        "$schema": "https://json-schema.org/draft/2020-12/schema",
        "$id": "https://example.com/root",
        "$ref": "strict",
        "$defs": {
          "strict": {
            "$id": "https://example.com/strict",
            "$dynamicAnchor": "node",
            "type": "object",
            "properties": { "data": true, "y": true, "children": { "type": "array", "items": { "$ref": "tree#/$defs/kids" } } },
            "allOf": [{ "$ref": "#/$defs/extra" }],
            "unevaluatedProperties": false,
            "$defs": { "extra": { "properties": { "x": { "type": "integer" } } } }
          },
          "tree": {
            "$id": "https://example.com/tree",
            "$dynamicAnchor": "node",
            "type": "object",
            "$defs": { "kids": { "$dynamicRef": "#node" } }
          }
        }
      }|}
  in
  expect_agreement v "dynamic"
    [
      {|{}|};
      {|{ "data": 1, "x": 2 }|};
      {|{ "x": "a" }|};
      {|{ "y": 1, "children": [{ "y": 1 }] }|};
      {|{ "children": [{ "z": 1 }] }|};
      {|{ "children": [{ "x": 1, "children": [{ "y": 2, "data": 3 }] }] }|};
      {|{ "children": [{ "children": [{ "x": "no" }] }] }|};
      {|{ "z": 1 }|};
    ];
  expect_valid v {|{ "children": [{ "x": 1, "children": [{ "y": 2 }] }] }|} true;
  expect_valid v {|{ "children": [{ "z": 1 }] }|} false

(* Small objects are decided by looking the few declared and required names up in them, large ones by visiting their
   properties. Both agree, top-level and nested. *)
let few_names_are_looked_up_in_small_and_large_objects () =
  let v =
    must_compile
      {|{
        "properties": { "a": { "type": "string" }, "n": { "properties": { "x": { "type": "integer" } } } },
        "required": ["b"]
      }|}
  in
  List.iter
    (fun pad ->
      let p = String.concat "" (List.init pad (fun i -> Printf.sprintf {|,"p%d": %d|} i i)) in
      expect_valid v ({|{"b": 1, "a": "x"|} ^ p ^ {|}|}) true;
      expect_valid v ({|{"a": "x"|} ^ p ^ {|}|}) false;
      expect_valid v ({|{"a": 1, "b": 1|} ^ p ^ {|}|}) false;
      expect_valid v ({|{"b": null|} ^ p ^ {|, "n": {"x": 2|} ^ p ^ {|}}|}) true;
      expect_valid v ({|{"b": null|} ^ p ^ {|, "n": {"x": "2"|} ^ p ^ {|}}|}) false)
    [ 0; 3; 40; 300 ]

(* JSON text is validated in place. Invalid JSON and runaway recursion are told apart. *)
let validates_json_text () =
  let v = must_compile {|{ "type": "array", "items": { "type": "integer" } }|} in
  let r = Validator.validate_string v "[1, 2, 3]" in
  Check.checkf (r = Ok true) "[1, 2, 3]: %s" (describe r);
  let r = Validator.validate_bytes v (Bytes.of_string {|[1, "2"]|}) in
  Check.checkf (r = Ok false) {|[1, "2"]: %s|} (describe r);
  let r = Validator.validate_string v "[1, 2" in
  Check.checkf (match r with Error (Parse_error e) -> e.offset = 5 | Ok _ | Error _ -> false) "[1, 2: %s" (describe r);
  Check.check "text that is not JSON is valid"
    (not (Validator.is_valid_string v "[1, 2" || Validator.is_valid_bytes v Bytes.empty));
  let c = Results.create Results.Detailed in
  let r = Validator.evaluate_string v ~results:c {|["x"]|} in
  Check.checkf (r = Ok false) {|["x"]: %s|} (describe r);
  let found =
    List.exists
      (fun (r : Results.result) -> (not r.is_match) && r.document_evaluation_location = "/0")
      (Results.results c)
  in
  Check.checkf found "no failure at /0: %s"
    (String.concat "; "
       (List.map
          (fun (r : Results.result) ->
            Printf.sprintf "%b %s %s %s" r.is_match r.evaluation_location r.document_evaluation_location r.message)
          (Results.results c)));
  let r = Validator.evaluate_string v ~results:c {|[|} in
  Check.checkf
    (match r with Error (Parse_error _) -> true | Ok _ | Error _ -> false)
    "evaluate_string of text that is not JSON: %s" (describe r)

(* A format callback that validates JSON text itself, during a validation of JSON text on the same thread, gets
   buffers of its own. *)
let validating_json_from_a_format_callback_works () =
  let inner = must_compile {|{ "type": "object", "required": ["a"] }|} in
  let self = ref None in
  let v =
    must_compile
      ~options:
        (options ~assert_format:true
           ~formats:
             [
               ( "embedded-json",
                 fun s ->
                   (* The same validator too: an evaluation nested in its own callback. *)
                   Validator.is_valid_string inner s
                   && match !self with Some v -> Validator.is_valid_string v {|[]|} | None -> false );
             ]
           ())
      {|{ "type": "array", "items": { "format": "embedded-json" } }|}
  in
  self := Some v;
  expect_valid v {|["{\"a\": 1}", "{\"a\": 2}"]|} true;
  expect_valid v {|["{\"a\": 1}", "{\"b\": 2}"]|} false;
  expect_valid v {|["{\"a\": 1}", "not json"]|} false

(* A number in a schema and the same number in an instance are the same float, however it is written. *)
let schema_and_instance_numbers_parse_alike () =
  List.iter
    (fun (keyword, literal) ->
      let sign = if literal.[0] = '-' then "-" else "" in
      (* Go replaces the digits of the literal with the float's shortest text. *)
      List.iter
        (fun text ->
          let v = must_compile (Printf.sprintf {|{%S: %s}|} keyword text) in
          expect_valid v text false;
          expect_valid v literal false)
        [ literal; sign ^ "9.727837981879871e+26" ])
    [
      ("exclusiveMaximum", "972783798187987123879878123.18878137");
      ("exclusiveMinimum", "-972783798187987123879878123.18878137");
    ]

let unevaluated_items_with_a_static_prefix_match_the_general_path () =
  List.iter
    (fun schema ->
      expect_agreement (must_compile schema) schema
        [
          {|[]|};
          {|[1]|};
          {|[1, 2]|};
          {|[1, "a"]|};
          {|["a"]|};
          {|[1, 2, 3]|};
          {|[1, "a", "b"]|};
          {|["a", 1, "b"]|};
          {|{}|};
          {|[2, 2]|};
        ])
    [
      {|{ "prefixItems": [{ "type": "integer" }], "unevaluatedItems": { "type": "string" } }|};
      {|{ "allOf": [{ "prefixItems": [true, { "type": "integer" }] }], "prefixItems": [{ "type": "integer" }], "unevaluatedItems": false }|};
      {|{ "allOf": [{ "items": { "type": "integer" } }], "unevaluatedItems": false }|};
      {|{ "anyOf": [{ "prefixItems": [true, true] }, { "prefixItems": [{ "type": "integer" }] }], "unevaluatedItems": false }|};
      {|{ "contains": { "type": "integer" }, "unevaluatedItems": { "type": "string" } }|};
      {|{ "if": { "prefixItems": [{ "const": 1 }] }, "then": { "prefixItems": [true, true] }, "unevaluatedItems": false }|};
    ]

let unique_items_over_large_arrays () =
  let v = must_compile {|{ "uniqueItems": true }|} in
  let items =
    String.concat "," (List.init 200 (fun i -> Printf.sprintf {|{"id": %d, "tags": ["a", %d]}|} i (i mod 3)))
  in
  expect_valid v ("[" ^ items ^ "]") true;
  expect_valid v ("[" ^ items ^ {|,{"tags": ["a", 1.0], "id": 4e0}]|}) false

(* A not whose subschema leads back to the schema it is in recurses in place like any other applicator. It stops at
   the maximum depth: the validation is an error, and is_valid reports the instance as invalid. (Evaluating not went
   around the depth guard, so the first of these schemas overflowed the stack, and for the others the not turned the
   abandoned evaluation's false into true.) *)
let not_on_an_in_place_cycle_stops_at_max_depth () =
  let schemas =
    [
      {|{"not": {"$ref": "#"}}|};
      {|{"not": {"not": {"$ref": "#"}}}|};
      {|{"type": "integer", "not": {"$ref": "#"}}|};
      {|{"allOf": [{"not": {"$ref": "#"}}]}|};
      {|{"$defs": {"a": {"not": {"$ref": "#/$defs/b"}}, "b": {"not": {"$ref": "#/$defs/a"}}}, "$ref": "#/$defs/a"}|};
      {|{"$defs": {"loop": {"allOf": [{"$ref": "#/$defs/loop"}]}}, "not": {"$ref": "#/$defs/loop"}}|};
      {|{"$defs": {"loop": {"allOf": [{"$ref": "#/$defs/loop"}]}}, "not": {"not": {"$ref": "#/$defs/loop"}}}|};
      {|{"$defs": {"loop": {"allOf": [{"$ref": "#/$defs/loop"}]}}, "properties": {"a": {"not": {"$ref": "#/$defs/loop"}}}}|};
      {|{"unevaluatedProperties": false, "not": {"$ref": "#"}}|};
    ]
  in
  let instances = [ {|1|}; {|"a"|}; {|{"a": 1}|}; {|[1]|} ] in
  List.iteri
    (fun index schema ->
      let v = must_compile ~options:(options ~max_depth:16 ()) schema in
      List.iter
        (fun instance ->
          if index = 7 && instance <> {|{"a": 1}|} then
            (* Only an object with the property reaches the loop. *)
            ()
          else if index = 2 && instance <> {|1|} then
            (* Anything but an integer fails the type before the not is reached, when failing fast. *)
            ()
          else begin
            let doc = must_parse instance in
            Check.checkf
              (not
                 (Validator.is_valid_document v doc || Validator.is_valid_string v instance
                 || Validator.is_valid_bytes v (Bytes.of_string instance)))
              "%s: is_valid reported %s as valid" schema instance;
            let r = Validator.validate_document v doc in
            Check.checkf (is_depth_exceeded r) "%s: validate_document(%s) gave %s, not Depth_exceeded" schema instance
              (describe r);
            let r = Validator.validate_string v instance in
            Check.checkf (is_depth_exceeded r) "%s: validate_string(%s) gave %s, not Depth_exceeded" schema instance
              (describe r);
            List.iter
              (fun level ->
                let r = Validator.evaluate v ~results:(Results.create level) doc in
                Check.checkf (is_depth_exceeded r) "%s: evaluate(%s) at level %s gave %s, not Depth_exceeded" schema
                  instance (Results.level_to_string level) (describe r))
              [ Results.Basic; Results.Detailed; Results.Verbose ]
          end)
        instances)
    schemas

let () =
  test "keeps_the_dynamic_scope" keeps_the_dynamic_scope;
  test "custom_formats_are_asserted_when_format_assertion_is_on" custom_formats_are_asserted_when_format_assertion_is_on;
  test "format_is_an_annotation_by_default_and_asserted_on_request"
    format_is_an_annotation_by_default_and_asserted_on_request;
  test "content_is_asserted_in_draft7_only" content_is_asserted_in_draft7_only;
  test "entry_point_evaluates_from_a_subschema" entry_point_evaluates_from_a_subschema;
  test "base_uri_resolves_relative_references" base_uri_resolves_relative_references;
  test "default_dialect_applies_to_schemas_without_schema" default_dialect_applies_to_schemas_without_schema;
  test "compilation_errors" compilation_errors;
  test "in_place_recursion_beyond_max_depth_is_an_error" in_place_recursion_beyond_max_depth_is_an_error;
  test "numbers_are_compared_exactly_for_multiple_of" numbers_are_compared_exactly_for_multiple_of;
  test "validators_are_safe_for_concurrent_use" validators_are_safe_for_concurrent_use;
  test "discriminated_one_of_and_any_of_agree_with_exhaustive_evaluation"
    discriminated_one_of_and_any_of_agree_with_exhaustive_evaluation;
  test "discriminators_key_null_and_numbers_by_value" discriminators_key_null_and_numbers_by_value;
  test "arrays_of_simple_arrays_match_the_general_path" arrays_of_simple_arrays_match_the_general_path;
  test "fused_not_required_and_absent_pattern_conditions_match_the_general_path"
    fused_not_required_and_absent_pattern_conditions_match_the_general_path;
  test "flat_fused_objects_match_the_general_path" flat_fused_objects_match_the_general_path;
  test "fused_objects_below_a_dynamic_reference_match_the_general_path"
    fused_objects_below_a_dynamic_reference_match_the_general_path;
  test "few_names_are_looked_up_in_small_and_large_objects" few_names_are_looked_up_in_small_and_large_objects;
  test "validates_json_text" validates_json_text;
  test "validating_json_from_a_format_callback_works" validating_json_from_a_format_callback_works;
  test "schema_and_instance_numbers_parse_alike" schema_and_instance_numbers_parse_alike;
  test "unevaluated_items_with_a_static_prefix_match_the_general_path"
    unevaluated_items_with_a_static_prefix_match_the_general_path;
  test "unique_items_over_large_arrays" unique_items_over_large_arrays;
  test "not_on_an_in_place_cycle_stops_at_max_depth" not_on_an_in_place_cycle_stops_at_max_depth;
  Check.finish "api"
