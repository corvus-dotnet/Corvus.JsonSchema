(* The samples of the package's README.md and of docs/JsonSchemaForOcaml.md, compiled and run. The counterpart of
   tests/TestReadme.pas of the Object Pascal port.

   Each sample is the body of a module of this file, written as it is in the documents: top-level phrases a program
   could hold. The module is a functor of unit, so that the sample runs when the test applies it. The test runs each
   one with its standard output going to a file, and compares what it wrote with the output the sample's comments
   give. It then reads the two documents and checks that every block of OCaml in them is in this file, line for line
   (indentation and blank lines aside). So a sample that is changed in a document and not here, or here and not in a
   document, fails the run.

   dune runs the test in the build's copy of the test directory, with README.md and this file beside it as
   dependencies. The documentation page is not part of the package (the published package is this directory alone),
   so it is checked when it is found, two directories above the root of the dune project, and reported as skipped
   when it is not. *)

open Corvus_json_schema
module Check = Corvus_json_schema_test_support.Check

(* ------------------------------------------------------------------------------------------------------------------ *)
(* The samples *)

module Usage () = struct
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
end

module Documents () = struct
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
end

module Not_json () = struct
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
end

module Errors () = struct
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
end

module Options () = struct
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
end

module Options_record () = struct
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
end

module Pattern_limits () = struct
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
end

module Results_rows () = struct
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
end

module Annotations () = struct
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
end

module Threads () = struct
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
end

(* ------------------------------------------------------------------------------------------------------------------ *)
(* The checks *)

(* A file's text without its carriage returns, or None when it cannot be read. *)
let read_text path =
  match In_channel.with_open_bin path In_channel.input_all with
  | text -> Some (String.concat "" (String.split_on_char '\r' text))
  | exception Sys_error _ -> None

(* captured runs a sample with its standard output in a file, and is what it wrote. *)
let captured run =
  let path = Filename.temp_file "readme_test" ".out" in
  flush stdout;
  let saved = Unix.dup Unix.stdout in
  let file = Unix.openfile path [ Unix.O_WRONLY; Unix.O_TRUNC; Unix.O_CREAT ] 0o600 in
  Unix.dup2 file Unix.stdout;
  Unix.close file;
  let restore () =
    flush stdout;
    Unix.dup2 saved Unix.stdout;
    Unix.close saved
  in
  (match run () with
  | () -> restore ()
  | exception raised ->
      restore ();
      Check.fail ("a sample was stopped by " ^ Printexc.to_string raised));
  let text = Option.value (read_text path) ~default:"" in
  Sys.remove path;
  text

let expect name run want =
  Check.section name;
  Check.equal_string "output" ~expected:(String.concat "\n" want ^ "\n") (captured run)

(* flattened is a text's lines without the spaces around them and without the empty ones, each ended by a line feed,
   after one line feed: so a run of whole lines of one text is found in another by searching for it. *)
let flattened text =
  let lines = List.filter (fun line -> line <> "") (List.map String.trim (String.split_on_char '\n' text)) in
  "\n" ^ String.concat "" (List.map (fun line -> line ^ "\n") lines)

let contains text part =
  let n = String.length text and m = String.length part in
  let rec at i = i + m <= n && (String.sub text i m = part || at (i + 1)) in
  at 0

(* check_document checks that every block of OCaml in a Markdown document is in this file, and is the number of
   blocks. *)
let check_document name text source =
  let blocks = ref 0 and block = Buffer.create 1024 and in_block = ref false in
  List.iter
    (fun line ->
      let line = String.trim line in
      if not !in_block then
        begin if line = "```ocaml" then begin
          in_block := true;
          Buffer.clear block
        end
        end
      else if line = "```" then begin
        in_block := false;
        incr blocks;
        Check.checkf
          (contains source (flattened (Buffer.contents block)))
          "%s: OCaml block %d is in test/readme_test.ml:\n%s" name !blocks (Buffer.contents block)
      end
      else begin
        Buffer.add_string block line;
        Buffer.add_char block '\n'
      end)
    (String.split_on_char '\n' text);
  Check.checkf (not !in_block) "%s: every OCaml block is closed" name;
  Check.checkf (!blocks > 0) "%s: has OCaml blocks" name;
  !blocks

(* The documentation page, found from the root of the dune project (the directory dune runs from), which is two
   directories below the root of the repository. *)
let page () =
  let rec find dir depth =
    if Sys.file_exists (Filename.concat dir "dune-project") then
      read_text
        (List.fold_left Filename.concat dir [ Filename.parent_dir_name; Filename.parent_dir_name; "docs"; page_name ])
    else if depth = 0 then None
    else find (Filename.concat dir Filename.parent_dir_name) (depth - 1)
  and page_name = "JsonSchemaForOcaml.md" in
  match Sys.getenv_opt "DUNE_SOURCEROOT" with Some root when root <> "" -> find root 0 | _ -> find "." 10

let () =
  expect "usage"
    (fun () ->
      let module M = Usage () in
      ())
    [ "true"; "false" ];
  expect "documents"
    (fun () ->
      let module M = Documents () in
      ())
    [ "true"; "[1,2,3]"; "false"; "true" ];
  expect "text that is not JSON"
    (fun () ->
      let module M = Not_json () in
      ())
    [ "false"; "8: unexpected end of input"; "the schema recursed beyond the maximum depth" ];
  expect "errors"
    (fun () ->
      let module M = Errors () in
      ())
    [
      "The schema cannot be compiled: Invalid regular expression '(' in pattern.";
      "The schema is not JSON: invalid JSON at offset 9: unexpected end of input";
    ];
  expect "options"
    (fun () ->
      let module M = Options () in
      ())
    [ "true"; "false" ];
  expect "options as a record"
    (fun () ->
      let module M = Options_record () in
      ())
    [ "draft4: false"; "true"; "false" ];
  expect "pattern limits"
    (fun () ->
      let module M = Pattern_limits () in
      ())
    [ "no answer within 0.05 s"; "abandoned"; "nesting, 1000" ];
  expect "results"
    (fun () ->
      let module M = Results_rows () in
      ())
    [
      "false";
      {|/properties/id at "/id": The value was expected to match the subschema.|};
      {|/properties/id/type at "/id": The value was expected to be of type 'integer'|};
      {|/required at "/name": Required property not present 'name'|};
    ];
  expect "annotations"
    (fun () ->
      let module M = Annotations () in
      ())
    [ "true"; {|"" title # "Person"|}; {|"/name" title #/properties/name "Name"|}; "2 annotations"; "0 rows" ];
  expect "threads"
    (fun () ->
      let module M = Threads () in
      ())
    [ "2 2 2 2" ];
  Check.section "documents";
  (match read_text "readme_test.ml" with
  | None -> Check.fail "readme_test.ml was not found beside the test"
  | Some source -> (
      let source = flattened source in
      (match read_text (Filename.concat Filename.parent_dir_name "README.md") with
      | None -> Check.fail "README.md was not found"
      | Some readme -> Printf.printf "README.md: %d OCaml blocks\n" (check_document "README.md" readme source));
      match page () with
      | None -> print_endline "docs/JsonSchemaForOcaml.md: skipped (the file is not there)"
      | Some text ->
          Printf.printf "docs/JsonSchemaForOcaml.md: %d OCaml blocks\n"
            (check_document "docs/JsonSchemaForOcaml.md" text source)));
  Check.finish "readme"
