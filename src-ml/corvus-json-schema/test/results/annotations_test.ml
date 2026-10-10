(* Runs the JSON-Schema-Test-Suite annotation tests (JSON-Schema-Test-Suite/annotations) through a verbose results
   collector, as the C# and Rust annotation tests do: every draft, cases filtered by "compatibility", and each
   assertion compared with the annotations grouped by instance location, keyword and schema location. A port of
   annotations_test.go of the Go module. It skips nothing but what "compatibility" excludes, as Go does.

   Go hands the evaluator the bytes of a schema and of an instance as they are in the file. Here they are the compact
   JSON text of the same values, written by the parser that read the file, with numbers as they were written. *)

open Corvus_json_schema
open Corvus_json_schema_test_support
open Corvus_json_schema_test_helpers
module Json = Suite.Json
module Values = Corvus_json_schema__Values

exception Fatal of string

let fatal fmt = Printf.ksprintf (fun what -> raise (Fatal what)) fmt
let compatibility_order = [ "3"; "4"; "6"; "7"; "2019"; "2020" ]

(* The position of a level in the order, or -1. *)
let index_of level =
  let rec find i = function [] -> -1 | head :: tail -> if String.equal head level then i else find (i + 1) tail in
  find 0 compatibility_order

let compatible level compatibility =
  let at = index_of level in
  if String.length compatibility > 2 && String.equal (String.sub compatibility 0 2) "<=" then begin
    let limit = index_of (String.sub compatibility 2 (String.length compatibility - 2)) in
    limit >= 0 && at <= limit
  end
  else begin
    let least = index_of compatibility in
    least >= 0 && at >= least
  end

(* The text of a string member of an object, or the empty string when there is none, as Go decodes a missing one. *)
let string_member d n name =
  let m = Suite.member d n name in
  if m >= 0 && Json.kind d m = Json.kind_string then Suite.string d m else ""

(* [same_annotations actual d expected] compares the produced annotations of a keyword at a location with the expected
   ones: the same schema locations, each with an equal JSON value (numbers by value, objects unordered). [expected] is
   an object of the suite's document, from a schema location fragment to a value. *)
let same_annotations actual d expected =
  let expected = Suite.properties d expected in
  List.length actual = List.length expected
  && List.for_all
       (fun (location, want) ->
         match List.assoc_opt location actual with
         | None -> false
         | Some got -> (
             match Json.parse_string got with Error _ -> false | Ok a -> Values.values_equal a (Json.root a) d want))
       expected

let show_values values = String.concat ", " (List.map (fun (location, value) -> location ^ ": " ^ value) values)

let drafts =
  [
    ("draft4", Draft4, "4");
    ("draft6", Draft6, "6");
    ("draft7", Draft7, "7");
    ("draft2019-09", Draft2019_09, "2019");
    ("draft2020-12", Draft2020_12, "2020");
  ]

let test_annotation_suite () =
  let root = Suite.root () in
  let dir = Filename.concat (Filename.concat root "annotations") "tests" in
  if not (Sys.file_exists dir && Sys.is_directory dir) then
    fatal "annotation tests not found at %s (set JSON_SCHEMA_TEST_SUITE, or check out the submodule)" dir;
  let resolver = Suite.remote_resolver root in
  let files = Suite.json_files dir in
  let file_runs = ref 0 and groups = ref 0 and skipped = ref 0 and cases = ref 0 in
  let total = ref 0 and failed = ref 0 in
  List.iter
    (fun (draft, dialect, level) ->
      List.iter
        (fun file ->
          let d =
            match Suite.read_file file with
            | None -> fatal "%s: cannot be read" file
            | Some source -> (
                match Json.parse_string source with
                | Ok d -> d
                | Error e -> fatal "%s: %s" file (Json.error_to_string e))
          in
          incr file_runs;
          let name = draft ^ "/" ^ Filename.basename file in
          List.iter
            (fun group ->
              let compatibility = string_member d group "compatibility" in
              let description = string_member d group "description" in
              if compatibility <> "" && not (compatible level compatibility) then incr skipped
              else begin
                incr groups;
                let options = options ~default_dialect:dialect ~resolver () in
                match Validator.compile_string ~options (Suite.text d (Suite.member d group "schema")) with
                | Error e ->
                    incr failed;
                    Check.fail (Printf.sprintf "%s [%s]: compile error: %s" name description (error_to_string e))
                | Ok validator ->
                    List.iter
                      (fun test ->
                        incr cases;
                        let instance = Suite.text d (Suite.member d test "instance") in
                        let collector = Results.create Results.Verbose in
                        (match Validator.evaluate_string validator ~results:collector instance with
                        | Ok _ -> ()
                        | Error e -> fatal "%s [%s]: %s" name description (error_to_string e));
                        let produced = Results.collect_annotations collector in
                        List.iter
                          (fun assertion ->
                            incr total;
                            let location = string_member d assertion "location" in
                            let keyword = string_member d assertion "keyword" in
                            let expected = Suite.member d assertion "expected" in
                            let actual =
                              match List.assoc_opt location produced with
                              | None -> []
                              | Some keywords -> (
                                  match List.assoc_opt keyword keywords with None -> [] | Some values -> values)
                            in
                            let same = same_annotations actual d expected in
                            if not same then incr failed;
                            Check.checkf same "%s [%s] instance %s '%s' %s: expected %s, actual %s" name description
                              instance location keyword (Suite.text d expected) (show_values actual))
                          (Suite.items d (Suite.member d test "assertions")))
                      (Suite.items d (Suite.member d group "tests"))
              end)
            (Suite.items d (Suite.member d (Json.root d) "suite")))
        files)
    drafts;
  Printf.printf "%d files, each for %d drafts (%d runs of a file)\n" (List.length files) (List.length drafts) !file_runs;
  Printf.printf "%d schemas compiled, %d left out by their compatibility, %d instances evaluated\n" !groups !skipped
    !cases;
  Printf.printf "%d/%d annotation assertions passed\n%!" (!total - !failed) !total;
  Check.check "no annotation assertions ran" (!total <> 0)

let () =
  Check.section "annotation_suite";
  (if Suite.absent (Suite.root ()) then Suite.skipped "annotations"
   else try test_annotation_suite () with Fatal what -> Check.fail what);
  Check.finish "annotations"
