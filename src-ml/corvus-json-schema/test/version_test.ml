(* The version of the package. A port of version_test.go of the Go module, for what the OCaml package has: the
   constant is a plain semantic version and is the version dune-project gives the package, and the package's
   VERSIONHISTORY.md has a section for the version. *)

open Corvus_json_schema_test_support
module Suite = Corvus_json_schema_test_helpers.Suite

let is_digit c = c >= '0' && c <= '9'

(* A semantic version: three numbers, and an optional pre-release of letters, digits, dots and hyphens. *)
let is_semantic_version v =
  let core, pre =
    match String.index_opt v '-' with
    | Some i -> (String.sub v 0 i, Some (String.sub v (i + 1) (String.length v - i - 1)))
    | None -> (v, None)
  in
  let numbers = String.split_on_char '.' core in
  List.length numbers = 3
  && List.for_all (fun n -> n <> "" && String.for_all is_digit n) numbers
  &&
  match pre with
  | None -> true
  | Some pre ->
      pre <> ""
      && String.for_all
           (fun c -> is_digit c || (c >= 'a' && c <= 'z') || (c >= 'A' && c <= 'Z') || c = '.' || c = '-')
           pre

(* A file of the package, found from the current directory. *)
let package_file name =
  let rec find dir depth =
    let project = Filename.concat dir "dune-project" in
    if Sys.file_exists project && Sys.file_exists (Filename.concat dir "corvus-json-schema.opam") then
      Suite.read_file (Filename.concat dir name)
    else if depth = 0 then None
    else find (Filename.concat dir "..") (depth - 1)
  in
  find "." 10

let lines text = List.map (fun line -> String.trim line) (String.split_on_char '\n' text)

let () =
  Check.section "TestVersionIsReleasable";
  let version = Corvus_json_schema.version in
  Check.checkf (is_semantic_version version) "the version %S is a semantic version" version;
  (match package_file "dune-project" with
  | None -> Check.fail "dune-project was not found"
  | Some project ->
      Check.checkf
        (List.mem ("(version " ^ version ^ ")") (lines project))
        "dune-project declares (version %s) on a line of its own" version);
  (match package_file "VERSIONHISTORY.md" with
  | None -> Check.fail "VERSIONHISTORY.md was not found"
  | Some history ->
      Check.checkf (List.mem ("## V" ^ version) (lines history)) "VERSIONHISTORY.md has a V%s section" version);
  Check.finish "version"
