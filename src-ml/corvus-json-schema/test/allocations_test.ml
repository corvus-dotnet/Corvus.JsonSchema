(* Validation allocates nothing in the steady state: the evaluator's buffers are kept by the validator (or by the
   scratch the caller gives), JSON text is parsed into reused buffers, and the document is evaluated where it was
   parsed. A port of allocations_test.go of the Go module.

   Four asserted formats are outside this: regex (the pattern is parsed), idn-hostname and idn-email (the labels are
   decoded), and hostname for a label that starts with "xn--". A custom format receives a copy of the string.

   Go counts the allocations of a run with testing.AllocsPerRun. Here the words the garbage collector reports as
   allocated are read before and after a run. Reading them allocates a float, so the same readings with nothing
   between them are taken first and subtracted. What is measured is the measuring code alone: the readings are taken
   in this thread, around calls that start no other. *)

open Corvus_json_schema
open Corvus_json_schema_test_support

type case = { name : string; schema : string; options : options; instances : string list }

let allocation_cases () =
  let large = String.concat "," (List.init 300 (fun i -> Printf.sprintf {|"p%d": %d|} i i)) in
  let unique = String.concat "," (List.init 100 (fun i -> Printf.sprintf {|{"id": %d, "name": "n%d"}|} i i)) in
  [
    {
      name = "objects and arrays";
      options = default_options;
      schema =
        {|{
          "type": "object",
          "properties": { "name": { "type": "string", "minLength": 1 }, "tags": { "type": "array", "items": { "type": "string" } } },
          "required": ["name"]
        }|};
      instances =
        [
          {|{"name": "a", "tags": ["x", "y\nz"]}|};
          {|{"name": "", "tags": []}|};
          "{\"name\": \"caf\xc3\xa9\", \"tags\": [\"1\", \"2\", \"3\", \"4\", \"5\", \"6\", \"7\", \"8\"], \"other\": \
           {\"deep\": [1, [2, [3]]]}}";
        ];
    };
    {
      name = "keywords of every kind";
      options = default_options;
      schema =
        {|{
          "type": "object",
          "properties": {
            "n": { "type": "number", "minimum": 0, "exclusiveMaximum": 100, "multipleOf": 0.25 },
            "s": { "type": "string", "maxLength": 5, "pattern": "^[a-z]+$" },
            "e": { "enum": ["a", "b", 1, null] },
            "c": { "const": { "k": [1, 2] } },
            "u": { "type": "array", "uniqueItems": true, "contains": { "type": "integer" }, "minContains": 1 },
            "o": { "oneOf": [{ "type": "string" }, { "type": "integer" }, { "required": ["x"] }] },
            "a": { "anyOf": [{ "minimum": 5 }, { "maxLength": 2 }] },
            "i": { "if": { "type": "integer" }, "then": { "minimum": 1 }, "else": { "type": "string" } },
            "x": { "not": { "type": "null" } }
          },
          "patternProperties": { "^x-": { "type": "boolean" } },
          "additionalProperties": { "type": "integer" },
          "propertyNames": { "maxLength": 10 },
          "dependentRequired": { "n": ["s"] },
          "minProperties": 1
        }|};
      instances =
        [
          {|{"n": 1.5, "s": "abc", "e": "b", "c": {"k": [1, 2.0]}, "u": [1, "a", [2], {"b": 1}], "o": 3, "a": "ab", "i": 2, "x": 0, "x-flag": true, "extra": 7}|};
          {|{"n": 1.3, "s": "abc"}|};
          {|{"u": [|} ^ unique ^ {|, 5]}|};
          {|{"u": [|} ^ unique ^ {|, {"name": "n3", "id": 3}]}|};
          {|{"o": {"x": 1}, "a": 3, "i": "s", "x": null}|};
          {|{"a-name-that-is-too-long": 1}|};
        ];
    };
    {
      name = "unevaluated properties and items";
      options = default_options;
      schema =
        {|{
          "$defs": { "base": { "properties": { "a": { "type": "integer" } }, "patternProperties": { "^x-": true } } },
          "allOf": [{ "$ref": "#/$defs/base" }],
          "anyOf": [{ "properties": { "b": true }, "required": ["b"] }, { "properties": { "c": true } }],
          "oneOf": [{ "properties": { "d": { "type": "string" } } }, { "properties": { "d": { "type": "integer" } } }],
          "properties": { "list": { "prefixItems": [true], "contains": { "type": "string" }, "unevaluatedItems": false } },
          "unevaluatedProperties": false
        }|};
      instances =
        [
          {|{"a": 1, "b": 2, "d": "s", "x-y": null, "list": [1, "a", "b"]}|};
          {|{"a": 1, "c": 2, "d": 3, "list": [1, "a", 2]}|};
          {|{"a": 1, "e": 2}|};
          "{" ^ large ^ "}";
        ];
    };
    {
      name = "fused objects with conditions";
      options = default_options;
      schema =
        {|{
          "type": "object",
          "properties": { "kind": { "enum": ["a", "b"] }, "value": true, "other": { "type": "string" } },
          "allOf": [{ "$ref": "#/$defs/ext" }, { "properties": { "c": { "type": "integer" } } }],
          "if": { "properties": { "kind": { "const": "a" } }, "required": ["kind"] },
          "then": { "required": ["value"], "properties": { "extra": { "type": "integer" } } },
          "else": { "not": { "required": ["value", "other"] } },
          "dependentSchemas": { "c": { "properties": { "d": true } } },
          "unevaluatedProperties": false,
          "$defs": { "ext": { "patternProperties": { "^x-": true } } }
        }|};
      instances =
        [
          {|{"kind": "a", "value": 1, "extra": 2, "x-a": 1}|};
          {|{"kind": "b", "value": 1, "c": 3, "d": 4}|};
          {|{"kind": "b", "value": 1, "other": "x"}|};
          {|{"kind": "a"}|};
          {|{"kind": "a", "value": 1, |} ^ large ^ "}";
        ];
    };
    {
      name = "a dynamic scope";
      options = default_options;
      schema =
        {|{
          "$schema": "https://json-schema.org/draft/2020-12/schema",
          "$id": "https://example.com/strict-tree",
          "$dynamicAnchor": "node",
          "$ref": "tree",
          "unevaluatedProperties": false,
          "$defs": {
            "tree": {
              "$id": "https://example.com/tree",
              "$dynamicAnchor": "node",
              "type": "object",
              "properties": { "data": true, "children": { "type": "array", "items": { "$dynamicRef": "#node" } } }
            }
          }
        }|};
      instances =
        [ {|{"children": [{"data": 1, "children": [{"data": [1, 2, 3]}]}]}|}; {|{"children": [{"daat": 1}]}|} ];
    };
    {
      name = "asserted formats and content";
      options = options ~default_dialect:Draft7 ~assert_format:true ();
      schema =
        {|{
          "properties": {
            "date": { "format": "date-time" }, "ip": { "format": "ipv6" }, "host": { "format": "hostname" },
            "id": { "format": "uuid" }, "n": { "format": "int32" }, "pointer": { "format": "json-pointer" },
            "uri": { "format": "uri" }, "ref": { "format": "uri-reference" }, "iri": { "format": "iri" },
            "template": { "format": "uri-template" }, "email": { "format": "email" },
            "duration": { "format": "duration" }, "time": { "format": "time" }, "v4": { "format": "ipv4" },
            "json": { "contentMediaType": "application/json", "contentEncoding": "base64" }
          }
        }|};
      instances =
        [
          {|{"date": "2020-01-02T03:04:05.678Z", "ip": "::ffff:192.168.0.1", "host": "example.com", "id": "2eb8aa08-aa98-11ea-b4aa-73b441d16380", "n": 12, "pointer": "/a/~0b", "json": "eyJhIjogWzEsIDIsIDNdfQ=="}|};
          {|{"uri": "http://example.com/a/b?c=d#e", "ref": "../a/b?c#d", "iri": "http://éxample.com/ü", "template": "http://example.com/{id}/x{?q,r}", "email": "joe.bloggs@example.com", "duration": "P4DT12H30M5S", "time": "08:30:06.283185+01:00", "v4": "1.2.3.4"}|};
          {|{"n": 1e30}|};
          {|{"json": "bm90IGpzb24="}|};
        ];
    };
    (* Patterns that are valid only without the u flag, which are matched by UTF-16 code unit, on strings with
       characters beyond the Basic Multilingual Plane: one with a faster matcher, which hands such a string to the
       engine, and ones the engine matches. *)
    {
      name = "patterns matched by code unit";
      options = default_options;
      schema =
        {|{
          "type": "object",
          "properties": {
            "a": { "pattern": "^[^\\%]{1,3}$" },
            "b": { "pattern": "\\-{0}^(..)\\1" },
            "c": { "pattern": "\\-{0}(?<=\\W)\\W$" }
          },
          "patternProperties": { "^\\-?.$": { "type": "integer" } }
        }|};
      instances =
        [
          "{\"a\": \"\xf0\x90\x90\x80\", \"b\": \"\xf0\x9f\x98\x80\xf0\x9f\x98\x80\", \"c\": \"x\xf0\x90\x90\x80\"}";
          "{\"a\": \"\xf0\x90\x90\x80\xf0\x90\x90\x80\", \"b\": \"\xf0\x9f\x98\x80\xf0\x90\x90\x80\", \"c\": \"ab\"}";
          "{\"a\": \"abc\", \"b\": \"abab\", \"\xf0\x90\x90\x80\": 1, \"z\": 1, \"\xc3\xa9\": 2}";
          "{\"z\": \"no\"}";
        ];
    };
    (* Beyond Go's cases: numbers of every representation against bounds of every representation, which is where an
       OCaml port could box a float or an Int64. *)
    {
      name = "numbers of every representation";
      options = default_options;
      schema =
        {|{
          "type": "array",
          "items": {
            "anyOf": [
              { "type": "integer", "minimum": -9223372036854775808, "maximum": 18446744073709551615 },
              { "type": "number", "exclusiveMinimum": 0.5, "exclusiveMaximum": 4611686018427387904.5, "multipleOf": 0.125 },
              { "enum": [4611686018427387904, 1e300, -1.5, 13835058055282163712] },
              { "const": 9223372036854775808.0 }
            ]
          },
          "uniqueItems": true
        }|};
      instances =
        [
          {|[1, 2.0, 3.5, 0.125, 4611686018427387903, 4611686018427387904, 4611686018427387905, 9223372036854775807]|};
          {|[9223372036854775808, 18446744073709551615, -9223372036854775808, -4611686018427387905, 1.0e3, 123456789012345678.25]|};
          {|[0.1]|};
          {|[18446744073709551616]|};
          {|[972783798187987123879878123.18878137, 2.2250738585072011e-308, 1e22, 1e23]|};
          {|[1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 14, 15, 16, 17, 18, 19, 20, 1.0]|};
          {|[1.5, 2.5, 3.5, 4.5, 5.5, 6.5, 7.5, 8.5, 9.5, 10.5, 11.5, 12.5, 13.5, 14.5, 15.5, 16.5, 17.5, 18.5, 19.5]|};
        ];
    };
  ]

(* The words allocated by run, on the minor heap and directly on the major heap, beyond what reading the counters
   allocates. *)
let allocated run =
  let minor_0 = Gc.minor_words () in
  let minor_1 = Gc.minor_words () in
  let bytes_0 = Gc.allocated_bytes () in
  let bytes_1 = Gc.allocated_bytes () in
  let bytes_2 = Gc.allocated_bytes () in
  let minor_2 = Gc.minor_words () in
  for _ = 1 to 100 do
    run ()
  done;
  let minor_3 = Gc.minor_words () in
  let bytes_3 = Gc.allocated_bytes () in
  let bytes_4 = Gc.allocated_bytes () in
  ignore (bytes_0, bytes_4);
  let minor = minor_3 -. minor_2 -. (minor_1 -. minor_0) in
  let bytes = bytes_3 -. bytes_2 -. (bytes_2 -. bytes_1) in
  (* The readings of the bytes are between the readings of the minor words, and allocate too. *)
  let overhead = minor_2 -. minor_1 -. (minor_1 -. minor_0) in
  ignore overhead;
  (int_of_float minor, int_of_float bytes)

let sink = ref false

let check_no_allocation case what run =
  (* The measurement of the measurement: a run that does nothing. *)
  let base_minor, base_bytes = allocated (fun () -> ()) in
  let minor, bytes = allocated run in
  Check.checkf
    (minor - base_minor = 0 && bytes - base_bytes = 0)
    "%s: %d words on the minor heap and %d bytes in all for 100 passes over the %s" case (minor - base_minor)
    (bytes - base_bytes) what

let test_validation_allocates_nothing_in_the_steady_state () =
  Check.section "TestValidationAllocatesNothingInTheSteadyState";
  List.iter
    (fun c ->
      let v =
        match Validator.compile_string ~options:c.options c.schema with
        | Ok v -> v
        | Error e -> failwith (c.name ^ ": " ^ error_to_string e)
      in
      let documents =
        Array.of_list
          (List.map
             (fun text ->
               match Document.parse_string text with
               | Ok d -> d
               | Error e -> failwith (c.name ^ ": " ^ Document.error_to_string e))
             c.instances)
      in
      let strings = Array.of_list c.instances in
      let texts = Array.map Bytes.of_string strings in
      let scratch = Some (Scratch.create ()) in
      (* The first validations size the buffers. *)
      for _ = 1 to 2 do
        Array.iteri
          (fun i document ->
            ignore (Validator.is_valid_document v document : bool);
            ignore (Validator.is_valid_bytes v texts.(i) : bool);
            ignore (Validator.is_valid_string v strings.(i) : bool);
            ignore (Validator.is_valid_document ?scratch v document : bool);
            ignore (Validator.is_valid_bytes ?scratch v texts.(i) : bool);
            ignore (Validator.is_valid_string ?scratch v strings.(i) : bool))
          documents
      done;
      let n = Array.length documents in
      check_no_allocation c.name "documents" (fun () ->
          for i = 0 to n - 1 do
            sink := Validator.is_valid_document v documents.(i) <> !sink
          done);
      check_no_allocation c.name "documents (validate_document)" (fun () ->
          for i = 0 to n - 1 do
            sink := (match Validator.validate_document v documents.(i) with Ok ok -> ok | Error _ -> false) <> !sink
          done);
      check_no_allocation c.name "texts as bytes" (fun () ->
          for i = 0 to n - 1 do
            sink := Validator.is_valid_bytes v texts.(i) <> !sink
          done);
      check_no_allocation c.name "texts as bytes (validate_bytes)" (fun () ->
          for i = 0 to n - 1 do
            sink := (match Validator.validate_bytes v texts.(i) with Ok ok -> ok | Error _ -> false) <> !sink
          done);
      check_no_allocation c.name "texts as strings" (fun () ->
          for i = 0 to n - 1 do
            sink := Validator.is_valid_string v strings.(i) <> !sink
          done);
      check_no_allocation c.name "texts as strings (validate_string)" (fun () ->
          for i = 0 to n - 1 do
            sink := (match Validator.validate_string v strings.(i) with Ok ok -> ok | Error _ -> false) <> !sink
          done);
      (* The same with a scratch of the caller's, built once. *)
      check_no_allocation c.name "documents, with a scratch" (fun () ->
          for i = 0 to n - 1 do
            sink := Validator.is_valid_document ?scratch v documents.(i) <> !sink
          done);
      check_no_allocation c.name "texts as bytes, with a scratch" (fun () ->
          for i = 0 to n - 1 do
            sink := Validator.is_valid_bytes ?scratch v texts.(i) <> !sink
          done);
      check_no_allocation c.name "texts as strings, with a scratch" (fun () ->
          for i = 0 to n - 1 do
            sink := Validator.is_valid_string ?scratch v strings.(i) <> !sink
          done);
      check_no_allocation c.name "texts as strings (validate_string), with a scratch" (fun () ->
          for i = 0 to n - 1 do
            sink :=
              (match Validator.validate_string ?scratch v strings.(i) with Ok ok -> ok | Error _ -> false) <> !sink
          done))
    (allocation_cases ())

let test_text_that_is_not_json_allocates_nothing_for_is_valid () =
  Check.section "TestTextThatIsNotJSONAllocatesNothingForIsValid";
  let v =
    match Validator.compile_string {|{"type": "object"}|} with Ok v -> v | Error e -> failwith (error_to_string e)
  in
  let text = Bytes.of_string {|{"a": [1, 2, {"b": "c"}|} in
  ignore (Validator.is_valid_bytes v text : bool);
  check_no_allocation "text that is not JSON" "text" (fun () -> sink := Validator.is_valid_bytes v text <> !sink)

(* The measurement sees an allocation when there is one: the exceptions the interface documents do allocate. *)
let test_the_measurement_sees_allocation () =
  Check.section "the measurement";
  let v =
    match
      Validator.compile_string
        ~options:(options ~assert_format:true ~formats:[ ("even", fun s -> String.length s mod 2 = 0) ] ())
        {|{"format": "even"}|}
    with
    | Ok v -> v
    | Error e -> failwith (error_to_string e)
  in
  let text = Bytes.of_string {|"four"|} in
  ignore (Validator.is_valid_bytes v text : bool);
  let base_minor, _ = allocated (fun () -> ()) in
  let minor, _ = allocated (fun () -> sink := Validator.is_valid_bytes v text <> !sink) in
  Check.checkf
    (minor - base_minor > 0)
    "a custom format is given a copy of the string, which allocates (%d words)" (minor - base_minor)

let () =
  test_validation_allocates_nothing_in_the_steady_state ();
  test_text_that_is_not_json_allocates_nothing_for_is_valid ();
  test_the_measurement_sees_allocation ();
  Check.finish "allocations"
