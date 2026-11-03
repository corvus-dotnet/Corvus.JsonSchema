module Json = Corvus_json_schema__Document

let root () =
  match Sys.getenv_opt "JSON_SCHEMA_TEST_SUITE" with
  | Some root when root <> "" -> root
  | _ ->
      (* The tests run from a directory of the build, some way below the root of the repository. *)
      let rec find dir depth =
        let candidate = Filename.concat dir "JSON-Schema-Test-Suite" in
        if Sys.file_exists (Filename.concat (Filename.concat candidate "validation") "tests") then candidate
        else if depth = 0 then Filename.concat (Filename.concat ".." "..") "JSON-Schema-Test-Suite"
        else find (Filename.concat dir "..") (depth - 1)
      in
      find "." 10

let absent root =
  (match Sys.getenv_opt "JSON_SCHEMA_TEST_SUITE" with Some named when named <> "" -> false | _ -> true)
  && not (Sys.file_exists (Filename.concat (Filename.concat root "validation") "tests"))

let skipped what =
  Printf.printf
    "%s: SKIPPED, the JSON-Schema-Test-Suite is not there (set JSON_SCHEMA_TEST_SUITE, or check out the submodule)\n"
    what

let read_file path = try Some (In_channel.with_open_bin path In_channel.input_all) with Sys_error _ -> None

let json_files dir =
  match Sys.readdir dir with
  | exception Sys_error _ -> []
  | names ->
      let names = List.filter (fun name -> Filename.check_suffix name ".json") (Array.to_list names) in
      let names = List.filter (fun name -> not (Sys.is_directory (Filename.concat dir name))) names in
      List.map (Filename.concat dir) (List.sort String.compare names)

let member d n name = if n < 0 || Json.kind d n <> Json.kind_object then -1 else Json.property d n name
let text d n = Json.json_of d n
let string d n = Json.string_value d n

let items d n =
  if n < 0 || Json.kind d n <> Json.kind_array then [] else List.init (Json.count d n) (fun i -> Json.first d n + i)

let properties d n =
  if n < 0 || Json.kind d n <> Json.kind_object then []
  else
    List.init (Json.count d n) (fun i ->
        let key = Json.first d n + (2 * i) in
        (Json.string_value d key, key + 1))

let tests_dir root = Filename.concat (Filename.concat root "validation") "tests"

let compatible compatibility release =
  match compatibility with
  | None -> true
  | Some compatibility ->
      let valid = ref true and stop = ref false in
      List.iter
        (fun constraint_ ->
          if not !stop then begin
            let op, version =
              if String.starts_with ~prefix:"<=" constraint_ then
                ("<=", String.sub constraint_ 2 (String.length constraint_ - 2))
              else if String.starts_with ~prefix:"=" constraint_ then
                ("=", String.sub constraint_ 1 (String.length constraint_ - 1))
              else ("", constraint_)
            in
            let number = int_of_string (String.trim version) in
            valid := if op = "<=" then release <= number else if op = "=" then release = number else release >= number;
            if release <= number then stop := true
          end)
        (String.split_on_char ',' compatibility);
      !valid

type group = {
  description : string;
  compatibility : string option;
  tags : string list;
  external_schemas : (string * string) list;
  schema : string;
  tests : case list;
}

and case = { case_description : string; data : string; valid : bool }

let has_tag group tag = List.mem tag group.tags

let case_resolver group =
  match group.external_schemas with
  | [] -> None
  | schemas ->
      let documents =
        List.map
          (fun (uri, text) ->
            match Corvus_json_schema.Document.parse_string text with
            | Ok doc -> (uri, doc)
            | Error e -> failwith (uri ^ ": " ^ Corvus_json_schema.Document.error_to_string e))
          schemas
      in
      Some (fun uri -> List.assoc_opt uri documents)

let groups path =
  match read_file path with
  | None -> failwith (path ^ ": cannot be read")
  | Some source -> (
      match Json.parse_string source with
      | Error e -> failwith (path ^ ": " ^ Json.error_to_string e)
      | Ok d ->
          List.map
            (fun group ->
              let compatibility = member d group "compatibility" in
              {
                description = string d (member d group "description");
                compatibility = (if compatibility < 0 then None else Some (string d compatibility));
                tags = List.map (string d) (items d (member d group "tags"));
                external_schemas =
                  List.map (fun (uri, value) -> (uri, text d value)) (properties d (member d group "externalSchemas"));
                schema = text d (member d group "schema");
                tests =
                  List.map
                    (fun case ->
                      let valid = member d case "valid" in
                      {
                        case_description = string d (member d case "description");
                        data = text d (member d case "data");
                        valid = valid >= 0 && Json.boolean d valid;
                      })
                    (items d (member d group "tests"));
              })
            (items d (member d (Json.root d) "tests")))
