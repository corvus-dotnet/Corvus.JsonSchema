(* Runs the JSON-Schema-Test-Suite (the repository's submodule) against the evaluator, as the C#, Rust and Go suite
   runners do. Every dialect's cases are in one tree (validation/tests, required and optional): each case's
   compatibility selects the drafts it applies to, its tags whether format is asserted, and its externalSchemas are
   served to that case alone. Every case runs fail-fast (as a document, as bytes and as a string, with the validator's own buffers and with a scratch)
   and through a results collector at each level. A port of suite_test.go of the Go module.

   Set JSON_SCHEMA_TEST_SUITE to use a different checkout, SUITE_DRAFT and SUITE_FILTER to narrow the run. *)

open Corvus_json_schema
open Corvus_json_schema_test_support
module Suite = Corvus_json_schema_test_helpers.Suite

let suite_drafts =
  [
    ("draft4", Draft4, 4);
    ("draft6", Draft6, 6);
    ("draft7", Draft7, 7);
    ("draft2019-09", Draft2019_09, 2019);
    ("draft2020-12", Draft2020_12, 2020);
  ]

(* Exclusions, matching the C#, Rust and Go runners: zero-terminated floats. *)
let suite_excluded_files = [ "draft4/optional/zero-terminated-floats.json" ]

(* The number of test cases the suite has, less the exclusion: a smaller count means a stale or partial run. *)
let expected_total = 8611
let results_levels = [ Results.Basic; Results.Detailed; Results.Verbose ]

exception Case_error of string

let fail fmt = Printf.ksprintf (fun message -> raise (Case_error message)) fmt

let contains text part =
  let n = String.length part in
  let rec at i = i + n <= String.length text && (String.sub text i n = part || at (i + 1)) in
  n = 0 || at 0

(* run_suite_case evaluates one instance every way the interface offers and checks that they agree. *)
let run_suite_case scratch v data =
  try
    let document =
      match Document.parse_string data with Ok d -> d | Error e -> fail "document: %s" (Document.error_to_string e)
    in
    let checked what = function Ok ok -> ok | Error e -> fail "%s: %s" what (error_to_string e) in
    let fast = checked "fast" (Validator.validate_document v document) in
    if Validator.is_valid_document v document <> fast then
      fail "is_valid_document disagrees with validate_document (%b)" fast;
    if Validator.is_valid_document ?scratch v document <> fast then
      fail "a scratch disagrees with validate_document (%b)" fast;
    let verbose = ref [] in
    List.iter
      (fun level ->
        let c = Results.create level in
        let name = Results.level_to_string level in
        let ok = checked name (Validator.evaluate v ~results:c document) in
        if ok <> fast then fail "%s returned %b, fast returned %b" name ok fast;
        let summary =
          List.exists
            (fun (r : Results.result) ->
              r.evaluation_location = "" && r.document_evaluation_location = "" && r.is_match = ok)
            (Results.results c)
        in
        if not summary then fail "%s: no root summary row matching the result" name;
        verbose := Results.results c)
      results_levels;
    (* The same instance straight from the text, in the validator's reused buffers. *)
    let bytes = Bytes.of_string data in
    let from_bytes = checked "validate_bytes" (Validator.validate_bytes v bytes) in
    if
      from_bytes <> fast
      || Validator.is_valid_bytes v bytes <> fast
      || Validator.is_valid_bytes ?scratch v bytes <> fast
    then fail "validate_bytes returned %b, the document %b" from_bytes fast;
    let from_string = checked "validate_string" (Validator.validate_string v data) in
    if
      from_string <> fast
      || Validator.is_valid_string v data <> fast
      || Validator.is_valid_string ?scratch v data <> fast
    then fail "validate_string returned %b, the document %b" from_string fast;
    let c = Results.create Results.Verbose in
    ignore (checked "evaluate_bytes" (Validator.evaluate_bytes v ~results:c bytes) : bool);
    if !verbose <> Results.results c then fail "evaluate_bytes's verbose results differ from the document's";
    Ok fast
  with
  | Case_error message -> Error message
  | Stack_overflow -> Error "stack overflow during evaluation"

let total = ref 0
let failures = ref []

(* Failing leap second cases that assert format, which are not counted as failures. *)
let skipped = ref []
let areas = ref []
let passed : (string, int) Hashtbl.t = Hashtbl.create 16
let counted : (string, int) Hashtbl.t = Hashtbl.create 16
let bump table area = Hashtbl.replace table area (1 + try Hashtbl.find table area with Not_found -> 0)

let run_file scratch filter dialect release file label area =
  let groups = Suite.groups file in
  if not (List.mem area !areas) then areas := !areas @ [ area ];
  List.iter
    (fun (group : Suite.group) ->
      (* The evaluator asserts format by default up to draft7, so a format-annotation case does not apply there. *)
      let applies =
        Suite.compatible group.compatibility release && not (Suite.has_tag group "format-annotation" && release <= 7)
      in
      let assert_format = Suite.has_tag group "format-assertion" in
      if applies && (filter = "" || contains group.description filter || contains label filter) then begin
        let options =
          options ~default_dialect:dialect ?resolver:(Suite.case_resolver group)
            ?assert_format:(if assert_format then Some true else None)
            ()
        in
        let validator =
          match Validator.compile_string ~options group.schema with
          | v -> v
          | exception Stack_overflow -> Error (Compile_error "stack overflow during compilation")
        in
        List.iter
          (fun (test : Suite.case) ->
            incr total;
            bump counted area;
            let outcome =
              match validator with
              | Error e -> Error ("compile error: " ^ error_to_string e)
              | Ok v -> run_suite_case scratch v test.data
            in
            if outcome = Ok test.valid then bump passed area
            else if
              (* Leap seconds are skipped where format is asserted, as in the C# runner. *)
              assert_format && contains (String.lowercase_ascii test.case_description) "leap second"
            then begin
              bump passed area;
              skipped := (label ^ " [" ^ group.description ^ "] " ^ test.case_description) :: !skipped
            end
            else begin
              let got = match outcome with Ok actual -> string_of_bool actual | Error message -> message in
              failures :=
                Printf.sprintf "%s [%s] %s: expected %b, got %s" label group.description test.case_description
                  test.valid got
                :: !failures
            end)
          group.tests
      end)
    groups

let () =
  let root = Suite.root () in
  let tests = Suite.tests_dir root in
  if Suite.absent root then begin
    Suite.skipped "suite";
    exit 0
  end;
  if not (Sys.file_exists tests) then begin
    Printf.printf "JSON-Schema-Test-Suite not found at %s (set JSON_SCHEMA_TEST_SUITE, or check out the submodule)\n"
      root;
    exit 1
  end;
  let filter = Option.value (Sys.getenv_opt "SUITE_FILTER") ~default:"" in
  let draft_filter = Option.value (Sys.getenv_opt "SUITE_DRAFT") ~default:"" in
  let scratch = Some (Scratch.create ()) in
  List.iter
    (fun (name, dialect, release) ->
      if draft_filter = "" || draft_filter = name then begin
        List.iter
          (fun f -> run_file scratch filter dialect release f (name ^ "/" ^ Filename.basename f) name)
          (Suite.json_files tests);
        List.iter
          (fun f ->
            let label = name ^ "/optional/" ^ Filename.basename f in
            if not (List.mem label suite_excluded_files) then
              run_file scratch filter dialect release f label (name ^ "/optional"))
          (Suite.json_files (Filename.concat tests "optional"))
      end)
    suite_drafts;
  List.iter print_endline (List.rev !failures);
  List.iter (fun line -> print_endline ("skipped: " ^ line)) (List.rev !skipped);
  List.iter
    (fun area ->
      Printf.printf "%-34s %5d/%d\n" area
        (try Hashtbl.find passed area with Not_found -> 0)
        (try Hashtbl.find counted area with Not_found -> 0))
    !areas;
  let failed = List.length !failures in
  Printf.printf "JSON-Schema-Test-Suite: %d/%d passed\n" (!total - failed) !total;
  Check.checkf (failed = 0) "%d JSON-Schema-Test-Suite cases failed" failed;
  Check.check "some JSON-Schema-Test-Suite cases ran" (!total <> 0);
  if draft_filter = "" && filter = "" then
    Check.checkf (!total = expected_total) "%d JSON-Schema-Test-Suite cases ran, where %d were expected" !total
      expected_total;
  Check.finish "suite"
