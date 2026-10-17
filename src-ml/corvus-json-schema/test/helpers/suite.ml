module Json = Corvus_json_schema__Document

let root () =
  match Sys.getenv_opt "JSON_SCHEMA_TEST_SUITE" with
  | Some root when root <> "" -> root
  | _ ->
      (* The tests run from a directory of the build, some way below the root of the repository. *)
      let rec find dir depth =
        let candidate = Filename.concat dir "JSON-Schema-Test-Suite" in
        if Sys.file_exists (Filename.concat candidate "tests") then candidate
        else if depth = 0 then Filename.concat (Filename.concat ".." "..") "JSON-Schema-Test-Suite"
        else find (Filename.concat dir "..") (depth - 1)
      in
      find "." 10

let absent root =
  (match Sys.getenv_opt "JSON_SCHEMA_TEST_SUITE" with Some named when named <> "" -> false | _ -> true)
  && not (Sys.file_exists (Filename.concat root "tests"))

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

let remote_resolver root =
  let remotes = Filename.concat root "remotes" in
  let cache = Hashtbl.create 16 in
  let prefix = "http://localhost:1234/" in
  fun uri ->
    if not (String.starts_with ~prefix uri) then None
    else begin
      let rest = String.sub uri (String.length prefix) (String.length uri - String.length prefix) in
      match Hashtbl.find_opt cache rest with
      | Some doc -> Some doc
      | None -> (
          let path = List.fold_left Filename.concat remotes (String.split_on_char '/' rest) in
          match read_file path with
          | None -> None
          | Some text -> (
              match Corvus_json_schema.Document.parse_string text with
              | Error _ -> None
              | Ok doc ->
                  Hashtbl.replace cache rest doc;
                  Some doc))
    end

type group = { description : string; schema : string; tests : case list }
and case = { case_description : string; data : string; valid : bool }

let groups path =
  match read_file path with
  | None -> failwith (path ^ ": cannot be read")
  | Some source -> (
      match Json.parse_string source with
      | Error e -> failwith (path ^ ": " ^ Json.error_to_string e)
      | Ok d ->
          List.map
            (fun group ->
              {
                description = string d (member d group "description");
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
            (items d (Json.root d)))
