(* The examples of example_test.go of the Go module. Go runs each Example function and compares what it printed with
   its "Output:" comment. Here each example is a function that gives the lines it would print, written with the public
   interface only, and the test compares them with the lines of the Go comment.

   Where Go prints an error value, the text is made here by the caller, with error_to_string. Go prints a nil error as
   "<nil>", and so does [show] for a result without an error, so that the lines are the ones of the Go comments. *)

open Corvus_json_schema
module Check = Corvus_json_schema_test_support.Check

(* show is what Go's fmt.Println(valid, err) prints for the two values a validation returns. *)
let show = function Ok valid -> string_of_bool valid ^ " <nil>" | Error e -> "false " ^ error_to_string e

(* ExampleCompileString *)
let example_compile_string () =
  match
    Validator.compile_string
      {|{
        "type": "object",
        "properties": { "id": { "type": "integer", "minimum": 1 } },
        "required": ["id"]
      }|}
  with
  | Error e -> [ error_to_string e ]
  | Ok validator ->
      [
        string_of_bool (Validator.is_valid_string validator {|{"id": 3}|});
        string_of_bool (Validator.is_valid_string validator {|{"id": 0}|});
      ]

(* ExampleValidator_Evaluate *)
let example_validator_evaluate () =
  match Validator.compile_string {|{"properties": {"id": {"type": "integer"}}, "required": ["name"]}|} with
  | Error e -> [ error_to_string e ]
  | Ok validator -> (
      match Document.parse_string {|{"id": "seven"}|} with
      | Error e -> [ Document.error_to_string e ]
      | Ok instance ->
          let collector = Results.create Results.Detailed in
          let valid =
            match Validator.evaluate validator ~results:collector instance with Ok v -> v | Error _ -> false
          in
          string_of_bool valid
          :: List.filter_map
               (fun (r : Results.result) ->
                 if r.evaluation_location <> "" && r.message <> "" then
                   Some (Printf.sprintf "%s at %S: %s" r.evaluation_location r.document_evaluation_location r.message)
                 else None)
               (Results.results collector))

(* ExampleParseDocument *)
let example_parse_document () =
  match Validator.compile_string {|{"type": "array", "items": {"type": "integer"}}|} with
  | Error e -> [ error_to_string e ]
  | Ok validator -> (
      (* Parse once, validate any number of times. *)
      match Document.parse_bytes (Bytes.of_string {|[1, 2, 3]|}) with
      | Error e -> [ Document.error_to_string e ]
      | Ok document ->
          let first = string_of_bool (Validator.is_valid_document validator document) in
          (* JSON text is parsed into buffers the validator reuses. *)
          let second = string_of_bool (Validator.is_valid_bytes validator (Bytes.of_string {|[1, "two"]|})) in
          [ first; second ])

(* ExampleValidator_ValidateString *)
let example_validator_validate_string () =
  match Validator.compile_string {|{"type": "object"}|} with
  | Error e -> [ error_to_string e ]
  | Ok validator ->
      let first = show (Validator.validate_string validator {|{"id": 3|}) in
      (* is_valid_string reports text that is not JSON as invalid. *)
      let second = string_of_bool (Validator.is_valid_string validator {|{"id": 3|}) in
      [ first; second ]

(* ExampleCompileString_options *)
let example_compile_string_options () =
  match Document.parse_string {|{"type": "string", "format": "even"}|} with
  | Error e -> [ Document.error_to_string e ]
  | Ok item -> (
      match
        Validator.compile_string
          ~options:
            (options ~default_dialect:Draft2019_09 (* for schemas without $schema (default 2020-12) *)
               ~assert_format:true (* true or false. Without it the vocabularies decide *)
               ~formats:[ ("even", fun value -> String.length value mod 2 = 0) ]
               ~resolver:(fun uri -> if uri = "https://example.com/item.json" then Some item else None)
               ~base_uri:"https://example.com/root.json" ~entry_point:"#/$defs/item" ~max_depth:128 ())
          {|{"$defs": {"item": {"$ref": "item.json"}}}|}
      with
      | Error e -> [ error_to_string e ]
      | Ok validator ->
          [
            string_of_bool (Validator.is_valid_string validator {|"four"|});
            string_of_bool (Validator.is_valid_string validator {|"three"|});
          ])

(* ExampleResultsCollector_CollectAnnotations *)
let example_results_collect_annotations () =
  match
    Validator.compile_string
      {|{
        "title": "Person",
        "properties": { "name": { "title": "Name", "type": "string" } }
      }|}
  with
  | Error e -> [ error_to_string e ]
  | Ok validator ->
      let collector = Results.create Results.Verbose in
      let first = show (Validator.evaluate_string validator ~results:collector {|{"name": "Ada"}|}) in
      (* Instance location, then keyword, then schema location, then the value as JSON text. Go indexes maps, and a
         missing key gives the empty string. *)
      let annotations = Results.collect_annotations collector in
      let find instance keyword schema =
        match List.assoc_opt instance annotations with
        | None -> ""
        | Some keywords -> (
            match List.assoc_opt keyword keywords with
            | None -> ""
            | Some schemas -> Option.value (List.assoc_opt schema schemas) ~default:"")
      in
      [ first; find "" "title" "#"; find "/name" "title" "#/properties/name" ]

(* ExampleCompileString_errors. Go asks errors.As for the kind of the error, which here is its constructor. *)
let example_compile_string_errors () =
  let is_compile_error =
    match Validator.compile_string {|{"$ref": "#/$defs/missing"}|} with
    | Error (Compile_error _) -> true
    | Ok _ | Error _ -> false
  in
  let is_parse_error =
    match Validator.compile_string {|{"type": |} with Error (Parse_error _) -> true | Ok _ | Error _ -> false
  in
  [ string_of_bool is_compile_error; string_of_bool is_parse_error ]

(* example runs one example and compares its lines with the lines of the Go "Output:" comment. *)
let example name run expected =
  Check.section name;
  match run () with
  | lines -> Check.equal_string "output" ~expected:(String.concat "\n" expected) (String.concat "\n" lines)
  | exception raised -> Check.fail ("stopped by " ^ Printexc.to_string raised)

let () =
  example "example_compile_string" example_compile_string [ "true"; "false" ];
  example "example_validator_evaluate" example_validator_evaluate
    [
      "false";
      {|/properties/id at "/id": The value was expected to match the subschema.|};
      {|/properties/id/type at "/id": The value was expected to be of type 'integer'|};
      {|/required at "/name": Required property not present 'name'|};
    ];
  example "example_parse_document" example_parse_document [ "true"; "false" ];
  example "example_validator_validate_string" example_validator_validate_string
    [ "false invalid JSON at offset 8: unexpected end of input"; "false" ];
  example "example_compile_string_options" example_compile_string_options [ "true"; "false" ];
  example "example_results_collector_collect_annotations" example_results_collect_annotations
    [ "true <nil>"; {|"Person"|}; {|"Name"|} ];
  example "example_compile_string_errors" example_compile_string_errors [ "true"; "true" ];
  Check.finish "example"
