(* The embedded metaschemas. A port of metaschemas_test.go of the Go module. *)

open Corvus_json_schema_test_support
module Suite = Corvus_json_schema_test_helpers.Suite
module Metaschemas = Corvus_json_schema__Metaschemas
module Metaschema_files = Corvus_json_schema__Metaschema_files

let contains text part =
  let n = String.length part in
  let rec at i = i + n <= String.length text && (String.sub text i n = part || at (i + 1)) in
  n = 0 || at 0

(* The directory of the metaschemas of Corvus.Text.Json, found from the current directory, or None outside the
   repository. *)
let source_directory () =
  let rec find dir depth =
    let candidate = List.fold_left Filename.concat dir [ "src"; "Corvus.Text.Json"; "metaschema" ] in
    if Sys.file_exists candidate then Some candidate
    else if depth = 0 then None
    else find (Filename.concat dir "..") (depth - 1)
  in
  find "." 10

let rec files_below dir relative =
  List.concat_map
    (fun name ->
      let path = Filename.concat dir name in
      let relative = if relative = "" then name else relative ^ "/" ^ name in
      if Sys.is_directory path then files_below path relative else [ (relative, path) ])
    (List.sort String.compare (Array.to_list (Sys.readdir dir)))

(* Keeps the embedded metaschemas in step with those of Corvus.Text.Json (src/Corvus.Text.Json/metaschema), which
   are what the Go module embeds and tools/gen-metaschemas.ps1 reads through it. *)
let test_embedded_metaschemas_are_current () =
  Check.section "TestEmbeddedMetaschemasAreCurrent";
  match source_directory () with
  | None ->
      print_endline "outside the Corvus.JsonSchema repository: nothing to compare the embedded metaschemas against"
  | Some source ->
      let expected = ref [] in
      List.iter
        (fun draft ->
          List.iter
            (fun (relative, path) ->
              let name = draft ^ "/" ^ relative in
              expected := name :: !expected;
              let want = Suite.read_file path in
              let got = List.assoc_opt name (Array.to_list Metaschema_files.files) in
              Check.checkf
                (got <> None && got = want)
                "the embedded %s is stale: run pwsh tools/gen-metaschemas.ps1" name)
            (files_below (Filename.concat source draft) ""))
        [ "draft4"; "draft6"; "draft7"; "draft2019-09"; "draft2020-12" ];
      Array.iter
        (fun (name, _) -> Check.checkf (List.mem name !expected) "the embedded %s has no source in %s" name source)
        Metaschema_files.files

let test_every_embedded_metaschema_resolves_by_its_uri () =
  Check.section "TestEveryEmbeddedMetaschemaResolvesByItsURI";
  let open Corvus_json_schema in
  let vocabularies =
    List.concat_map
      (fun draft ->
        let prefix = "draft" ^ draft ^ "/meta/" in
        List.filter_map
          (fun (name, _) ->
            if String.starts_with ~prefix name then
              Some
                ("https://json-schema.org/draft/" ^ draft ^ "/meta/"
                ^ Filename.chop_suffix
                    (String.sub name (String.length prefix) (String.length name - String.length prefix))
                    ".json")
            else None)
          (Array.to_list Metaschema_files.files))
      [ "2019-09"; "2020-12" ]
  in
  let uris =
    [
      "http://json-schema.org/draft-04/schema";
      "http://json-schema.org/draft-06/schema";
      "http://json-schema.org/draft-07/schema";
      "https://json-schema.org/draft/2019-09/schema";
      "https://json-schema.org/draft/2020-12/schema";
    ]
    @ vocabularies
  in
  Check.equal_int "the number of metaschemas" ~expected:21 (List.length uris);
  List.iter
    (fun uri ->
      match Metaschemas.text uri with
      | None -> Check.fail (uri ^ " is not embedded")
      | Some text ->
          Check.checkf (Result.is_ok (Document.parse_string text)) "%s is JSON" uri;
          (* The hyper-schema vocabulary refers to the links schema, which is not embedded. *)
          if not (contains uri "hyper-schema") then
            begin match Validator.compile_uri uri with
            | Error e -> Check.fail (uri ^ ": " ^ error_to_string e)
            | Ok v ->
                Check.checkf (Validator.is_valid_string v {|{"type": "object"}|}) "%s accepts a schema" uri;
                (* The root metaschemas (and the validation vocabularies) know that type is a name or a list of names. *)
                if (not (contains uri "/meta/")) || String.ends_with ~suffix:"/meta/validation" uri then
                  Check.checkf
                    (not (Validator.is_valid_string v {|{"type": 12}|}))
                    "%s rejects a schema with a numeric type" uri
            end)
    uris;
  List.iter
    (fun uri -> Check.checkf (Metaschemas.text uri = None) "%S does not resolve" uri)
    [ "https://json-schema.org/draft/2020-12/meta/../schema"; "https://example.com/schema"; "" ]

let () =
  test_embedded_metaschemas_are_current ();
  test_every_embedded_metaschema_resolves_by_its_uri ();
  Check.finish "metaschemas"
