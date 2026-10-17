(* Results collection: the expectations of the C# evaluator's ResultsTests and ResultPathTests, plus worked examples
   traced through the C# collecting path (row order, locations, messages, levels). A port of results_test.go of the Go
   module, every test in its order, with the same schemas, instances and expected rows.

   The Object Pascal port's TestResults.pas has the same tests and no expectation beyond Go's, so nothing is added
   after them.

   Where a Go test stops at once (t.Fatal), the test here raises [Fatal], which [run] records as a failed check before
   it goes on to the next test. *)

open Corvus_json_schema
open Corvus_json_schema_test_support

exception Fatal of string

let fatal fmt = Printf.ksprintf (fun what -> raise (Fatal what)) fmt

(* Runs one test under its section name. *)
let run name test =
  Check.section name;
  try test () with Fatal what -> Check.fail what

let must_compile ?options schema =
  match Validator.compile_string ?options schema with
  | Ok v -> v
  | Error e -> fatal "compile %s: %s" schema (error_to_string e)

let entry_point reference = options ~entry_point:reference ()

(* The rows of an evaluation, a line for each: outcome, schema location, evaluation location, document location and
   message, with a bar between them. *)
let dump_results v instance level =
  let c = Results.create level in
  (match Validator.evaluate_string v ~results:c instance with
  | Ok _ -> ()
  | Error e -> fatal "evaluate %s: %s" instance (error_to_string e));
  String.concat "\n"
    (List.map
       (fun (r : Results.result) ->
         String.concat "|"
           [
             (if r.is_match then "match" else "fail");
             r.schema_evaluation_location;
             r.evaluation_location;
             r.document_evaluation_location;
             r.message;
           ])
       (Results.results c))

let expect_rows got want =
  let expected = String.concat "\n" want in
  Check.checkf (String.equal got expected) "rows:\n%s\nwant:\n%s" got expected

(* Whether [s] has [sub] somewhere in it. *)
let contains s sub =
  let n = String.length s and m = String.length sub in
  let rec at i = i + m <= n && (String.equal (String.sub s i m) sub || at (i + 1)) in
  at 0

(* The rows as text, for the message of a failed check. *)
let show_rows rows =
  String.concat "; "
    (List.map (fun (r : Results.result) -> Printf.sprintf "%b %s %S" r.is_match r.evaluation_location r.message) rows)

let person_schema =
  {|{
	"$schema": "https://json-schema.org/draft/2020-12/schema",
	"type": "object",
	"title": "Person",
	"properties": {
		"name": { "type": "string", "minLength": 1, "description": "The name" },
		"age": { "type": "integer", "minimum": 0 }
	},
	"required": ["name"],
	"additionalProperties": false
}|}

let refs_schema =
  {|{
	"$schema": "https://json-schema.org/draft/2020-12/schema",
	"$defs": {
		"fooId": { "type": "integer", "minimum": 0 },
		"holder": { "type": "object", "properties": { "fooId": { "$ref": "#/$defs/fooId" } } },
		"viaRef": { "$ref": "#/$defs/fooId" }
	}
}|}

let example_schema =
  {|{
	"type": "object",
	"properties": { "a": { "type": "string" } },
	"required": ["b"],
	"anyOf": [{ "required": ["a"] }, { "minProperties": 5 }]
}|}

let results_levels = [ Results.Basic; Results.Detailed; Results.Verbose ]

(* Go ranges over a map here, in no particular order. The instances are in the order they are written in. *)
let test_flag_and_collecting_evaluation_agree_at_every_level () =
  let v = must_compile person_schema in
  List.iter
    (fun (instance, expected) ->
      let got = Validator.is_valid_string v instance in
      Check.checkf (got = expected) "IsValid(%s) = %b" instance got;
      List.iter
        (fun level ->
          match Validator.evaluate_string v ~results:(Results.create level) instance with
          | Ok got ->
              Check.checkf (got = expected) "Evaluate(%s) at %s = %b" instance (Results.level_to_string level) got
          | Error e ->
              Check.fail
                (Printf.sprintf "Evaluate(%s) at %s: %s" instance (Results.level_to_string level) (error_to_string e)))
        results_levels)
    [
      ({|{ "name": "a", "age": 3 }|}, true);
      ({|{ "name": "", "age": 3 }|}, false);
      ({|{ "age": 3 }|}, false);
      ({|{ "name": "a", "extra": 1 }|}, false);
      ({|{ "name": "a", "age": -1 }|}, false);
      ({|[]|}, false);
    ]

let test_basic_results_report_failing_keywords_with_locations () =
  let c = Results.create Results.Basic in
  (match Validator.evaluate_string (must_compile person_schema) ~results:c {|{ "name": "", "age": -1 }|} with
  | Ok true -> fatal "valid"
  | Ok false | Error _ -> ());
  let min_length = ref false and minimum = ref false and schema_location = ref false in
  List.iter
    (fun (r : Results.result) ->
      Check.checkf (r.message = "") "a Basic row has a message: %S" r.message;
      if not r.is_match then begin
        min_length :=
          !min_length
          || (String.ends_with ~suffix:"/minLength" r.evaluation_location && r.document_evaluation_location = "/name");
        minimum :=
          !minimum
          || (String.ends_with ~suffix:"/minimum" r.evaluation_location && r.document_evaluation_location = "/age");
        schema_location := !schema_location || r.schema_evaluation_location = "/properties/name"
      end)
    (Results.results c);
  Check.checkf
    (!min_length && !minimum && !schema_location)
    "missing rows: minLength %b, minimum %b, schema location %b" !min_length !minimum !schema_location

(* The value of a key of an association list, or the empty list, as a Go map gives its zero value. *)
let under key entries = match List.assoc_opt key entries with Some inner -> inner | None -> []
let show_values values = String.concat ", " (List.map (fun (location, value) -> location ^ ": " ^ value) values)

let test_verbose_annotations_are_produced () =
  let c = Results.create Results.Verbose in
  (match Validator.evaluate_string (must_compile person_schema) ~results:c {|{ "name": "a" }|} with
  | Ok true -> ()
  | Ok false | Error _ -> fatal "invalid");
  let annotations = Results.collect_annotations c in
  let titles = under "title" (under "" annotations) in
  Check.checkf
    (List.assoc_opt "#" titles = Some {|"Person"|} && List.length titles = 1)
    "title: %s" (show_values titles);
  let descriptions = under "description" (under "/name" annotations) in
  Check.checkf
    (List.assoc_opt "#/properties/name" descriptions = Some {|"The name"|})
    "description: %s" (show_values descriptions)

let test_verbose_output_follows_the_c_sharp_row_order () =
  expect_rows
    (dump_results (must_compile person_schema) {|{ "name": "a" }|} Results.Verbose)
    [
      "match|/properties/name|/properties/name|/name|The value was expected to match the subschema.";
      "match|/properties/name|/properties/name/description|/name|\"The name\"";
      "match|/properties/name/minLength|/properties/name/minLength|/name|Expected the length of the value to be \
       greater than or equal to '1'";
      "match|/properties/name/type|/properties/name/type|/name|The value was expected to be of type 'string'";
      "match||||The value was expected to match the subschema.";
      "match||/title||\"Person\"";
      "match|/required|/required|/name|Required property present 'name'";
      "match|/type|/type||The value was expected to be of type 'object'";
    ]

let test_matching_keywords_carry_their_message_in_verbose_output () =
  let v =
    must_compile
      {|{
		"$schema": "https://json-schema.org/draft/2020-12/schema",
		"type": ["integer", "array"],
		"uniqueItems": true,
		"properties": { "n": { "type": "integer" } }
	}|}
  in
  (* Go drops the error of the evaluation here: one that fails counts as not valid. *)
  let rows v instance =
    let c = Results.create Results.Verbose in
    let valid = match Validator.evaluate_string v ~results:c instance with Ok valid -> valid | Error _ -> false in
    (valid, Results.results c)
  in
  let has rows is_match location message =
    List.exists
      (fun (r : Results.result) ->
        r.is_match = is_match && r.evaluation_location = location && r.message <> ""
        && (message = "" || r.message = message))
      rows
  in
  let valid, r = rows v {|[1, 2]|} in
  Check.checkf
    (valid
    && has r true "/type" {|The value was expected to be of type '["array", "integer"]'|}
    && has r true "/uniqueItems" "")
    "[1, 2]: %b %s" valid (show_rows r);
  let valid, r = rows v {|[1, 1]|} in
  Check.checkf ((not valid) && has r false "/uniqueItems" "") "[1, 1]: %b %s" valid (show_rows r);
  let valid, r = rows (must_compile {|{ "type": "integer" }|}) {|3|} in
  Check.checkf
    (valid && has r true "/type" "The value was expected to be of type 'integer'")
    "integer: %s" (show_rows r)

let test_an_entry_point_reports_its_own_schema_location () =
  let v = must_compile ~options:(entry_point "#/$defs/fooId") refs_schema in
  expect_rows
    (dump_results v {|"notAnInteger"|} Results.Detailed)
    [
      "fail|/$defs/fooId|||The value was expected to match the subschema.";
      "fail|/$defs/fooId/type|/type||The value was expected to be of type 'integer'";
    ]

let test_a_pure_ref_property_is_elided_with_ref_in_the_evaluation_path () =
  let v = must_compile ~options:(entry_point "#/$defs/holder") refs_schema in
  expect_rows
    (dump_results v {|{ "fooId": "notAnInteger" }|} Results.Detailed)
    [
      "fail|/$defs/fooId|/properties/fooId/$ref|/fooId|The value was expected to match the subschema.";
      "fail|/$defs/fooId/type|/properties/fooId/$ref/type|/fooId|The value was expected to be of type 'integer'";
      "fail|/$defs/holder|||The value was expected to match the subschema.";
    ]

let test_a_pure_ref_root_reports_against_its_target () =
  let v = must_compile ~options:(entry_point "#/$defs/viaRef") refs_schema in
  expect_rows
    (dump_results v {|"notAnInteger"|} Results.Detailed)
    [
      "fail|/$defs/fooId|||The value was expected to match the subschema.";
      "fail|/$defs/fooId/type|/type||The value was expected to be of type 'integer'";
    ]

let test_a_required_failure_carries_the_property_name () =
  let v = must_compile {|{ "type": "object", "required": ["name"] }|} in
  expect_rows
    (dump_results v {|{}|} Results.Detailed)
    [
      "fail||||The value was expected to match the subschema.";
      "fail|/required|/required|/name|Required property not present 'name'";
    ]

let test_detailed_output_keeps_failures_only_with_messages () =
  let expected =
    [
      "fail|/properties/a|/properties/a|/a|The value was expected to match the subschema.";
      "fail|/properties/a/type|/properties/a/type|/a|The value was expected to be of type 'string'";
      "fail||||The value was expected to match the subschema.";
      "fail|/required|/required|/b|Required property not present 'b'";
    ]
  in
  let v = must_compile example_schema in
  expect_rows (dump_results v {|{ "a": 1 }|} Results.Detailed) expected;
  let basic = List.map (fun line -> String.sub line 0 (String.rindex line '|' + 1)) expected in
  expect_rows (dump_results v {|{ "a": 1 }|} Results.Basic) basic

let test_verbose_output_reverses_a_contexts_own_rows_after_its_summary () =
  expect_rows
    (dump_results (must_compile example_schema) {|{ "a": 1 }|} Results.Verbose)
    [
      "fail|/properties/a|/properties/a|/a|The value was expected to match the subschema.";
      "fail|/properties/a/type|/properties/a/type|/a|The value was expected to be of type 'string'";
      "match|/anyOf/0|/anyOf/0||The value was expected to match the subschema.";
      "match|/anyOf/0/required|/anyOf/0/required|/a|Required property present 'a'";
      "fail||||The value was expected to match the subschema.";
      "match|/anyOf|/anyOf||The value matched at least one subschema.";
      "fail|/required|/required|/b|Required property not present 'b'";
      "match|/type|/type||The value was expected to be of type 'object'";
    ]

let test_not_subtrees_and_boolean_schemas () =
  expect_rows
    (dump_results (must_compile {|{ "not": { "type": "string" } }|}) {|"x"|} Results.Detailed)
    [
      "fail||||The value was expected to match the subschema.";
      "fail|/not|/not||The value matched the subschema in a not composition, which means the evaluation was not a \
       match.";
    ];
  expect_rows
    (dump_results (must_compile {|false|}) {|1|} Results.Detailed)
    [ "fail||||The value was expected to match the subschema."; "fail||||" ]

let test_a_valid_instance_at_detailed_level_yields_only_the_passing_root_row () =
  expect_rows (dump_results (must_compile person_schema) {|{ "name": "a" }|} Results.Detailed) [ "match||||" ]

let test_property_names_keeps_the_object_location_and_adds_a_failure_row_per_name () =
  let v = must_compile {|{ "propertyNames": { "maxLength": 2 } }|} in
  expect_rows
    (dump_results v {|{ "abc": 1 }|} Results.Detailed)
    [
      "fail|/propertyNames|/propertyNames||The value was expected to match the subschema.";
      "fail|/propertyNames/maxLength|/propertyNames/maxLength||Expected the length of the value to be less than or \
       equal to '2'";
      "fail||||The value was expected to match the subschema.";
      "fail|/propertyNames|/propertyNames||The property name did not match the schema.";
    ]

let test_draft4_exclusive_bounds_report_under_exclusive_maximum_with_the_maximum () =
  let v =
    must_compile {|{ "$schema": "http://json-schema.org/draft-04/schema#", "maximum": 3, "exclusiveMaximum": true }|}
  in
  expect_rows (dump_results v {|3|} Results.Detailed)
    [
      "fail||||The value was expected to match the subschema.";
      "fail|/exclusiveMaximum|/exclusiveMaximum||The value was expected to be less than '3'";
    ]

let test_failing_any_of_branches_are_discarded_and_unevaluated_properties_has_no_message () =
  let v =
    must_compile
      {|{ "anyOf": [{ "properties": { "a": true } }, { "required": ["zz"] }], "unevaluatedProperties": false }|}
  in
  expect_rows
    (dump_results v {|{ "a": 1, "b": 2 }|} Results.Detailed)
    [
      "fail|/unevaluatedProperties|/unevaluatedProperties|/b|The value was expected to match the subschema.";
      "fail|/unevaluatedProperties|/unevaluatedProperties|/b|";
      "fail||||The value was expected to match the subschema.";
      "fail|/unevaluatedProperties|/unevaluatedProperties||";
    ]

let test_a_collector_accumulates_across_evaluations () =
  let v = must_compile person_schema in
  let c = Results.create Results.Detailed in
  ignore (Validator.evaluate_string v ~results:c {|{ "age": "x" }|});
  let first = List.length (Results.results c) in
  ignore (Validator.evaluate_string v ~results:c {|{ "age": "x" }|});
  let second = List.length (Results.results c) in
  Check.checkf (first <> 0 && second = 2 * first) "%d rows, then %d" first second;
  Results.reset c;
  let after = List.length (Results.results c) in
  Check.checkf (after = 0) "%d rows after Reset" after

let test_dependencies_reports_under_its_own_name_in_every_dialect () =
  let v =
    must_compile
      {|{
		"$schema": "https://json-schema.org/draft/2020-12/schema",
		"dependencies": { "a": ["b"], "c": { "required": ["d"] } }
	}|}
  in
  expect_rows
    (dump_results v {|{ "a": 1, "c": 1 }|} Results.Detailed)
    [
      "fail|/dependencies/c|/dependencies/c||The value was expected to match the subschema.";
      "fail|/dependencies/c/required|/dependencies/c/required|/d|Required property not present 'd'";
      "fail||||The value was expected to match the subschema.";
      "fail|/dependencies|/dependencies|/c|The value did match the schema applied because it contained the property 'c'";
      "fail|/dependencies|/dependencies|/b|Required property not present 'b'";
    ];
  let modern =
    must_compile {|{ "dependentRequired": { "a": ["b"] }, "dependentSchemas": { "c": { "required": ["d"] } } }|}
  in
  let rows = dump_results modern {|{ "a": 1, "c": 1 }|} Results.Detailed in
  Check.checkf
    (contains rows "fail|/dependentRequired|/dependentRequired|/b|Required property not present 'b'"
    && contains rows "fail|/dependentSchemas/c|/dependentSchemas/c||")
    "rows:\n%s" rows

let test_a_statically_resolved_dynamic_ref_hop_is_named_dynamic_ref_in_the_evaluation_path () =
  let v =
    must_compile
      {|{
		"$schema": "https://json-schema.org/draft/2020-12/schema",
		"properties": { "p": { "$dynamicRef": "#/$defs/n" } },
		"$defs": { "n": { "type": "integer" } }
	}|}
  in
  expect_rows
    (dump_results v {|{ "p": "x" }|} Results.Detailed)
    [
      "fail|/$defs/n|/properties/p/$dynamicRef|/p|The value was expected to match the subschema.";
      "fail|/$defs/n/type|/properties/p/$dynamicRef/type|/p|The value was expected to be of type 'integer'";
      "fail||||The value was expected to match the subschema.";
    ]

let () =
  run "flag_and_collecting_evaluation_agree_at_every_level" test_flag_and_collecting_evaluation_agree_at_every_level;
  run "basic_results_report_failing_keywords_with_locations" test_basic_results_report_failing_keywords_with_locations;
  run "verbose_annotations_are_produced" test_verbose_annotations_are_produced;
  run "verbose_output_follows_the_c_sharp_row_order" test_verbose_output_follows_the_c_sharp_row_order;
  run "matching_keywords_carry_their_message_in_verbose_output"
    test_matching_keywords_carry_their_message_in_verbose_output;
  run "an_entry_point_reports_its_own_schema_location" test_an_entry_point_reports_its_own_schema_location;
  run "a_pure_ref_property_is_elided_with_ref_in_the_evaluation_path"
    test_a_pure_ref_property_is_elided_with_ref_in_the_evaluation_path;
  run "a_pure_ref_root_reports_against_its_target" test_a_pure_ref_root_reports_against_its_target;
  run "a_required_failure_carries_the_property_name" test_a_required_failure_carries_the_property_name;
  run "detailed_output_keeps_failures_only_with_messages" test_detailed_output_keeps_failures_only_with_messages;
  run "verbose_output_reverses_a_contexts_own_rows_after_its_summary"
    test_verbose_output_reverses_a_contexts_own_rows_after_its_summary;
  run "not_subtrees_and_boolean_schemas" test_not_subtrees_and_boolean_schemas;
  run "a_valid_instance_at_detailed_level_yields_only_the_passing_root_row"
    test_a_valid_instance_at_detailed_level_yields_only_the_passing_root_row;
  run "property_names_keeps_the_object_location_and_adds_a_failure_row_per_name"
    test_property_names_keeps_the_object_location_and_adds_a_failure_row_per_name;
  run "draft4_exclusive_bounds_report_under_exclusive_maximum_with_the_maximum"
    test_draft4_exclusive_bounds_report_under_exclusive_maximum_with_the_maximum;
  run "failing_any_of_branches_are_discarded_and_unevaluated_properties_has_no_message"
    test_failing_any_of_branches_are_discarded_and_unevaluated_properties_has_no_message;
  run "a_collector_accumulates_across_evaluations" test_a_collector_accumulates_across_evaluations;
  run "dependencies_reports_under_its_own_name_in_every_dialect"
    test_dependencies_reports_under_its_own_name_in_every_dialect;
  run "a_statically_resolved_dynamic_ref_hop_is_named_dynamic_ref_in_the_evaluation_path"
    test_a_statically_resolved_dynamic_ref_hop_is_named_dynamic_ref_in_the_evaluation_path;
  Check.finish "results"
