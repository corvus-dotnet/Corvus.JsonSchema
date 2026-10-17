(* Differential testing of the fail-fast plans (fused objects, type dispatch, name tables and the rest) against the
   collecting evaluator, which walks the general keyword-by-keyword path: every instance of the jsonschema-benchmark
   corpora, and mutations of it (a property removed, given another type, nulled or added, an array item changed, at
   every depth), must get the same verdict from both. A port of differential_test.go of the Go module.

   Set JSONSCHEMA_BENCHMARK to a checkout of https://github.com/sourcemeta-research/jsonschema-benchmark. The test
   does nothing without it. DIFF_ONLY narrows the corpora, DIFF_INSTANCES caps the instances of each corpus (default
   60). It takes a minute or two.

   Go reads an instance into maps and writes each mutation with encoding/json, which sorts the names of every object
   and writes a number as the shortest text of its float. Here an object's names are sorted the same way, and a
   number keeps the text it was written with. *)

open Corvus_json_schema
open Corvus_json_schema_test_support
module Suite = Corvus_json_schema_test_helpers.Suite

type json =
  | Null
  | Bool of bool
  | Number of string  (** As written. *)
  | String of string
  | Array of json list
  | Object of (string * json) list  (** In order of the names, each name once. *)

(* Sets a property of an object, keeping the names in order. *)
let rec with_property o name value =
  match o with
  | [] -> [ (name, value) ]
  | (k, _) :: rest when String.equal k name -> (name, value) :: rest
  | (k, _) :: _ when String.compare name k < 0 -> (name, value) :: o
  | pair :: rest -> pair :: with_property rest name value

let without_property o name = List.filter (fun (k, _) -> not (String.equal k name)) o

let rec of_document d n =
  let module J = Suite.Json in
  let kind = J.kind d n in
  if kind = J.kind_null then Null
  else if kind = J.kind_bool then Bool (J.boolean d n)
  else if kind = J.kind_number then Number (J.number_text d n)
  else if kind = J.kind_string then String (J.string_value d n)
  else if kind = J.kind_array then Array (List.map (of_document d) (Suite.items d n))
  else Object (List.fold_left (fun o (name, v) -> with_property o name (of_document d v)) [] (Suite.properties d n))

let parse text =
  match Suite.Json.parse_string text with
  | Ok d -> of_document d (Suite.Json.root d)
  | Error e -> failwith (Suite.Json.error_to_string e)

let add_string out s = Suite.Json.add_quoted out (Bytes.of_string s) 0 (String.length s)

let rec add out = function
  | Null -> Buffer.add_string out "null"
  | Bool b -> Buffer.add_string out (if b then "true" else "false")
  | Number text -> Buffer.add_string out text
  | String s -> add_string out s
  | Array items ->
      Buffer.add_char out '[';
      List.iteri
        (fun i item ->
          if i > 0 then Buffer.add_char out ',';
          add out item)
        items;
      Buffer.add_char out ']'
  | Object properties ->
      Buffer.add_char out '{';
      List.iteri
        (fun i (name, value) ->
          if i > 0 then Buffer.add_char out ',';
          add_string out name;
          Buffer.add_char out ':';
          add out value)
        properties;
      Buffer.add_char out '}'

let to_text v =
  let out = Buffer.create 256 in
  add out v;
  Buffer.contents out

(* replacements are values of every JSON type, tried in place of a value. *)
let replacements =
  [|
    Null;
    Bool true;
    Number "0";
    Number "-1.5";
    String "";
    String "x";
    Array [];
    Object [];
    Array [ Number "1"; String "a" ];
  |]

let with_item a i value = List.mapi (fun j item -> if j = i then value else item) a
let rec take n = function [] -> [] | x :: rest -> if n <= 0 then [] else x :: take (n - 1) rest

(* mutations are mutations of v: at the root and, recursively, inside it (bounded at each level to keep the count
   sane). *)
let rec mutations v depth =
  if depth > 6 then []
  else
    match v with
    | Object o ->
        let out = ref [] in
        List.iteri
          (fun i (k, value) ->
            if i < 12 then begin
              out := Object (without_property o k) :: !out;
              let j = ref (i mod 3) in
              while !j < Array.length replacements do
                out := Object (with_property o k replacements.(!j)) :: !out;
                j := !j + 3
              done;
              List.iter
                (fun inner -> out := Object (with_property o k inner) :: !out)
                (take 40 (mutations value (depth + 1)))
            end)
          o;
        out := Object (with_property o "zzUnknownProperty" (Number "1")) :: !out;
        List.rev !out
    | Array a ->
        let out = ref [] in
        List.iteri
          (fun i item ->
            if i < 4 then begin
              let j = ref (i mod 2) in
              while !j < Array.length replacements do
                out := Array (with_item a i replacements.(!j)) :: !out;
                j := !j + 2
              done;
              List.iter (fun inner -> out := Array (with_item a i inner) :: !out) (take 40 (mutations item (depth + 1)))
            end)
          a;
        (match a with first :: _ -> out := Array (a @ [ first ]) :: !out | [] -> ());
        List.rev !out
    | _ -> Array.to_list replacements

let rec strip_format = function
  | Object o ->
      Object
        (List.filter_map
           (fun (k, v) -> match (k, v) with "format", String _ -> None | _ -> Some (k, strip_format v))
           o)
  | Array a -> Array (List.map strip_format a)
  | v -> v

let () =
  match Sys.getenv_opt "JSONSCHEMA_BENCHMARK" with
  | None | Some "" ->
      print_endline "set JSONSCHEMA_BENCHMARK to a checkout of jsonschema-benchmark to run the differential test";
      Check.finish "differential"
  | Some root ->
      let schemas = Filename.concat root "schemas" in
      let dirs =
        match Sys.readdir schemas with
        | dirs -> List.sort String.compare (Array.to_list dirs)
        | exception Sys_error e -> failwith e
      in
      let only = Option.value (Sys.getenv_opt "DIFF_ONLY") ~default:"" in
      let limit = match Sys.getenv_opt "DIFF_INSTANCES" with Some s when s <> "" -> int_of_string s | _ -> 60 in
      let checked = ref 0 and corpora = ref 0 in
      List.iter
        (fun name ->
          let dir = Filename.concat schemas name in
          let selected = only = "" || List.mem name (String.split_on_char ',' only) in
          match if selected then Suite.read_file (Filename.concat dir "schema.json") else None with
          | None -> ()
          | Some text -> (
              Check.section name;
              let stripped = to_text (strip_format (parse text)) in
              match Validator.compile_string stripped with
              | Error e -> Check.fail ("compile error " ^ error_to_string e)
              | Ok v ->
                  incr corpora;
                  let instances =
                    match Suite.read_file (Filename.concat dir "instances.jsonl") with
                    | Some instances -> instances
                    | None -> failwith (name ^ ": no instances")
                  in
                  let failures = ref 0 and lines = ref 0 in
                  List.iter
                    (fun line ->
                      if String.trim line <> "" && !lines < limit then begin
                        incr lines;
                        let x = parse line in
                        Check.checkf (Validator.is_valid_bytes v (Bytes.of_string line)) "instance %d is valid" !lines;
                        List.iter
                          (fun c ->
                            incr checked;
                            let text = to_text c in
                            let fast = Validator.is_valid_string v text in
                            match Validator.evaluate_string v ~results:(Results.create Results.Basic) text with
                            | Ok collected when collected = fast -> ()
                            | outcome ->
                                incr failures;
                                if !failures <= 3 then
                                  Check.fail
                                    (Printf.sprintf "fail-fast %b, collecting %s on %s" fast
                                       (match outcome with Ok ok -> string_of_bool ok | Error e -> error_to_string e)
                                       (if String.length text > 400 then String.sub text 0 400 else text)))
                          (x :: mutations x 0)
                      end)
                    (String.split_on_char '\n' instances);
                  if !failures > 3 then Check.fail (Printf.sprintf "%d disagreements in all" !failures)
                  else Check.check "the plans agree with the general evaluator" (!failures = 0)))
        dirs;
      Printf.printf "%d instances checked in %d corpora\n" !checked !corpora;
      Check.check "some instances were checked" (!checked > 0);
      Check.finish "differential"
