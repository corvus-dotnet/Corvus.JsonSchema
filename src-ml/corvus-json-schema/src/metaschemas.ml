(* The standard metaschemas, embedded in the library (metaschema_files.ml, which tools/gen-metaschemas.ps1 writes from
   the files the Go module embeds). A port of metaschemas.go of the Go module. *)

let cut_prefix ~prefix s =
  if String.starts_with ~prefix s then
    Some (String.sub s (String.length prefix) (String.length s - String.length prefix))
  else None

(* file_name is the name of the file of a standard metaschema, by its canonical URI (no trailing empty fragment). *)
let file_name uri =
  match uri with
  | "http://json-schema.org/draft-04/schema" -> Some "draft4/schema.json"
  | "http://json-schema.org/draft-06/schema" -> Some "draft6/schema.json"
  | "http://json-schema.org/draft-07/schema" -> Some "draft7/schema.json"
  | _ -> (
      match cut_prefix ~prefix:"https://json-schema.org/draft/" uri with
      | None -> None
      | Some rest -> (
          let draft, name =
            match String.index_opt rest '/' with
            | Some i -> (String.sub rest 0 i, String.sub rest (i + 1) (String.length rest - i - 1))
            | None -> (rest, "")
          in
          if draft <> "2019-09" && draft <> "2020-12" then None
          else if name = "schema" then Some ("draft" ^ draft ^ "/schema.json")
          else
            match cut_prefix ~prefix:"meta/" name with
            | Some vocabulary
              when vocabulary <> "" && not (String.contains vocabulary '/' || String.contains vocabulary '.') ->
                Some ("draft" ^ draft ^ "/meta/" ^ vocabulary ^ ".json")
            | _ -> None))

(* text is the text of a standard metaschema, by its canonical URI. *)
let text uri =
  match file_name uri with
  | None -> None
  | Some file ->
      let found = ref None in
      Array.iter (fun (name, text) -> if String.equal name file then found := Some text) Metaschema_files.files;
      !found
