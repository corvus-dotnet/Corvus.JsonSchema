(* Loads schema documents, identifies resources and anchors, and resolves references. Elements are identified by
   (document, value index). A port of loader.go of the Go module. *)

open Document

let default_root_uri = "https://corvus-oss.org/runtime-evaluator/root.json"

type schema_document = {
  doc : Document.t;
  (* The resource that owns each visited schema value. *)
  resource_of : (int, int) Hashtbl.t;
}

type resource = {
  document : int;
  root_pointer : string;
  mutable uri : string;
  dialect : Dialect.t;
  vocabularies : int;
  mutable recursive_anchor : bool;
  anchors : (string, string) Hashtbl.t;
  dynamic_anchors : (string, string) Hashtbl.t;
}

(* A target is the target of a reference: a value in a document, and the resource it belongs to. *)
type target = { t_document : int; t_pointer : string; t_value : int; t_resource : int }
type dialect_info = { d_dialect : Dialect.t; d_vocabularies : int }

type t = {
  documents : schema_document Vec.t;
  resources : resource Vec.t;
  resources_by_uri : (string, int) Hashtbl.t;
  documents_by_uri : (string, int) Hashtbl.t;
  metaschema_info : (string, dialect_info) Hashtbl.t;
  mutable metaschema_loading : string list;
  reference_cache : (int * string, target option) Hashtbl.t;
  options : Options.t;
}

let create options =
  {
    documents = Vec.create ();
    resources = Vec.create ();
    resources_by_uri = Hashtbl.create 16;
    documents_by_uri = Hashtbl.create 16;
    metaschema_info = Hashtbl.create 4;
    metaschema_loading = [];
    reference_cache = Hashtbl.create 16;
    options;
  }

(* string_value is the text of a string value, if n is one. *)
let string_value d n = if n < 0 || kind d n <> kind_string then None else Some (Document.string_value d n)

(* member is the value of a property of n, or -1 when n is not an object or has no such property. *)
let member d n name = if n < 0 || kind d n <> kind_object then -1 else property d n name
let is_kind d n k = n >= 0 && kind d n = k

let is_schema_value d n =
  let k = kind d n in
  k = kind_bool || k = kind_object

let document l doc = (Vec.get l.documents doc).doc
let resource l id = Vec.get l.resources id

let root_resource l doc =
  let d = Vec.get l.documents doc in
  Hashtbl.find d.resource_of (root d.doc)

(* resource_root is the root value of a resource. *)
let resource_root l id =
  let r = resource l id in
  let d = document l r.document in
  let v, _ = Uri.resolve_pointer d (root d) r.root_pointer in
  if v >= 0 then v else root d

let root_target l id =
  let r = resource l id in
  { t_document = r.document; t_pointer = r.root_pointer; t_value = resource_root l id; t_resource = id }

let resource_of l doc value = Hashtbl.find_opt (Vec.get l.documents doc).resource_of value

let default_dialect_info l =
  { d_dialect = l.options.Options.default_dialect; d_vocabularies = Dialect.vocab_all_annotating }

let create_resource l doc pointer uri info =
  let id = Vec.length l.resources in
  Vec.push l.resources
    {
      document = doc;
      root_pointer = pointer;
      uri;
      dialect = info.d_dialect;
      vocabularies = info.d_vocabularies;
      recursive_anchor = false;
      anchors = Hashtbl.create 4;
      dynamic_anchors = Hashtbl.create 4;
    };
  if not (Hashtbl.mem l.resources_by_uri uri) then Hashtbl.replace l.resources_by_uri uri id;
  id

let add_anchor l res name pointer =
  let anchors = (resource l res).anchors in
  if not (Hashtbl.mem anchors name) then Hashtbl.replace anchors name pointer

let try_resolve_fragment l res fragment =
  if fragment = "" then Some (root_target l res)
  else begin
    let r = resource l res in
    let d = document l r.document in
    if fragment.[0] = '/' then begin
      let value, path = Uri.resolve_pointer d (resource_root l res) fragment in
      if value < 0 then None
      else begin
        let owner = match resource_of l r.document value with Some o -> o | None -> res in
        Some { t_document = r.document; t_pointer = r.root_pointer ^ path; t_value = value; t_resource = owner }
      end
    end
    else
      match Hashtbl.find_opt r.anchors fragment with
      | None -> None
      | Some anchor ->
          let value, _ = Uri.resolve_pointer d (root d) anchor in
          if value < 0 then None
          else Some { t_document = r.document; t_pointer = anchor; t_value = value; t_resource = res }
  end

let rec get_dialect_info l schema_uri =
  (* The standard metaschema URIs, as usually written, need no URI normalisation. *)
  let n = String.length schema_uri in
  let plain = if n > 0 && schema_uri.[n - 1] = '#' then String.sub schema_uri 0 (n - 1) else schema_uri in
  match Dialect.known plain with
  | Some d -> { d_dialect = d; d_vocabularies = Dialect.vocab_all_annotating }
  | None -> (
      let normalized = Uri.normalize schema_uri in
      match Dialect.known normalized with
      | Some d -> { d_dialect = d; d_vocabularies = Dialect.vocab_all_annotating }
      | None -> (
          match Hashtbl.find_opt l.metaschema_info normalized with
          | Some info -> info
          | None ->
              if List.mem normalized l.metaschema_loading then default_dialect_info l
              else begin
                let loading = l.metaschema_loading in
                l.metaschema_loading <- normalized :: loading;
                let info =
                  try load_metaschema_info l normalized
                  with e ->
                    l.metaschema_loading <- loading;
                    raise e
                in
                l.metaschema_loading <- loading;
                Hashtbl.replace l.metaschema_info normalized info;
                info
              end))

and load_metaschema_info l normalized =
  let meta_resource =
    match Hashtbl.find_opt l.resources_by_uri normalized with
    | Some r -> Some r
    | None -> if try_load_document l normalized then Hashtbl.find_opt l.resources_by_uri normalized else None
  in
  match meta_resource with
  | None -> default_dialect_info l
  | Some meta ->
      let r = resource l meta in
      let d = document l r.document in
      let v = member d (resource_root l meta) "$vocabulary" in
      let vocabularies =
        if is_kind d v kind_object then begin
          let bits = ref Dialect.vocab_core in
          let k = first d v in
          for i = 0 to count d v - 1 do
            bits := !bits lor Dialect.vocabulary_flag (Document.string_value d (k + (2 * i)))
          done;
          !bits
        end
        else Dialect.vocab_all_annotating
      in
      { d_dialect = r.dialect; d_vocabularies = vocabularies }

and try_load_document l absolute_uri =
  if Hashtbl.mem l.documents_by_uri absolute_uri then true
  else begin
    let resolved = match l.options.Options.resolver with Some resolve -> resolve absolute_uri | None -> None in
    match resolved with
    | Some doc ->
        ignore (add_document l absolute_uri doc : int);
        true
    | None -> (
        match Metaschemas.text absolute_uri with
        | None -> false
        | Some text -> (
            match Document.parse_string text with
            | Error _ -> Options.compile_error ("The embedded metaschema '" ^ absolute_uri ^ "' is not valid JSON.")
            | Ok doc ->
                ignore (add_document l absolute_uri doc : int);
                true))
  end

and add_document l uri doc =
  let id = Vec.length l.documents in
  Vec.push l.documents { doc; resource_of = Hashtbl.create 64 };
  Hashtbl.replace l.documents_by_uri uri id;
  let info =
    match string_value doc (member doc (root doc) "$schema") with
    | Some s -> get_dialect_info l s
    | None -> default_dialect_info l
  in
  let res = create_resource l id "" uri info in
  walk l id (root doc) "" res true;
  id

and walk l doc element pointer res is_resource_root =
  let entry = Vec.get l.documents doc in
  let d = entry.doc in
  if kind d element <> kind_object then Hashtbl.replace entry.resource_of element res
  else begin
    let res = ref res and is_resource_root = ref is_resource_root in
    let info =
      let r = resource l !res in
      let inherited = { d_dialect = r.dialect; d_vocabularies = r.vocabularies } in
      if !is_resource_root then inherited
      else match string_value d (property d element "$schema") with Some s -> get_dialect_info l s | None -> inherited
    in
    let dialect = info.d_dialect in
    let rank = Dialect.rank dialect in
    let legacy_ref_overrides_siblings =
      Dialect.is_legacy dialect && is_kind d (property d element "$ref") kind_string
    in
    if not legacy_ref_overrides_siblings then begin
      let id_name = if dialect = Dialect.Draft4 then "id" else "$id" in
      (match string_value d (property d element id_name) with
      | None -> ()
      | Some id ->
          let uri_part, fragment = Uri.split_fragment id in
          if uri_part = "" then
            begin if fragment <> "" && Dialect.is_legacy dialect then add_anchor l !res fragment pointer
            end
          else begin
            let absolute = Uri.resolve (resource l !res).uri uri_part in
            if (not !is_resource_root) || absolute <> (resource l !res).uri then
              if !is_resource_root then begin
                if not (Hashtbl.mem l.resources_by_uri absolute) then Hashtbl.replace l.resources_by_uri absolute !res;
                (resource l !res).uri <- absolute
              end
              else begin
                res := create_resource l doc pointer absolute info;
                is_resource_root := true
              end;
            if fragment <> "" && Dialect.is_legacy dialect then add_anchor l !res fragment pointer
          end);
      if rank >= 3 then
        begin match string_value d (property d element "$anchor") with
        | Some a -> add_anchor l !res a pointer
        | None -> ()
        end;
      if rank >= 4 then
        begin match string_value d (property d element "$dynamicAnchor") with
        | Some name ->
            let dynamic = (resource l !res).dynamic_anchors in
            if not (Hashtbl.mem dynamic name) then Hashtbl.replace dynamic name pointer;
            add_anchor l !res name pointer
        | None -> ()
        end;
      if dialect = Dialect.Draft2019_09 && !is_resource_root then begin
        let v = property d element "$recursiveAnchor" in
        if is_kind d v kind_bool && boolean d v then (resource l !res).recursive_anchor <- true
      end
    end;
    let res = !res in
    Hashtbl.replace entry.resource_of element res;
    let k = first d element in
    for i = 0 to count d element - 1 do
      let name = Document.string_value d (k + (2 * i)) in
      let value = k + (2 * i) + 1 in
      let sub = Dialect.subschema_kind_of name dialect legacy_ref_overrides_siblings in
      if sub <> Dialect.Subschema_none then begin
        let base = pointer ^ "/" ^ Uri.escape_pointer_token name in
        match sub with
        | Dialect.Subschema_none -> ()
        | Dialect.Subschema_single -> if is_schema_value d value then walk l doc value base res false
        | Dialect.Subschema_single_or_array | Dialect.Subschema_array ->
            if kind d value = kind_array then begin
              let c = first d value in
              for j = 0 to count d value - 1 do
                walk l doc (c + j) (base ^ "/" ^ string_of_int j) res false
              done
            end
            else if sub = Dialect.Subschema_single_or_array && is_schema_value d value then
              walk l doc value base res false
        | Dialect.Subschema_map ->
            if kind d value = kind_object then begin
              let c = first d value in
              for j = 0 to count d value - 1 do
                let entry = c + (2 * j) in
                if is_schema_value d (entry + 1) then
                  walk l doc (entry + 1)
                    (base ^ "/" ^ Uri.escape_pointer_token (Document.string_value d entry))
                    res false
              done
            end
      end
    done
  end

let load_root l schema =
  let uri = if l.options.Options.base_uri <> "" then Uri.normalize l.options.Options.base_uri else default_root_uri in
  root_resource l (add_document l uri schema)

let load_root_from_uri l uri =
  let normalized = Uri.normalize uri in
  if not (try_load_document l normalized) then
    Options.compile_error ("Unable to resolve the schema document '" ^ uri ^ "'.");
  root_resource l (Hashtbl.find l.documents_by_uri normalized)

let resolve_reference l from reference =
  let uri_part, fragment = Uri.split_fragment reference in
  let absolute = Uri.resolve (resource l from).uri uri_part in
  let res =
    match Hashtbl.find_opt l.resources_by_uri absolute with
    | Some r -> Some r
    | None -> if try_load_document l absolute then Hashtbl.find_opt l.resources_by_uri absolute else None
  in
  match res with None -> None | Some r -> try_resolve_fragment l r (Uri.decode_fragment fragment)

(* try_resolve_reference resolves a reference from a resource. The target is None when it does not resolve. *)
let try_resolve_reference l from reference =
  let key = (from, reference) in
  match Hashtbl.find_opt l.reference_cache key with
  | Some t -> t
  | None ->
      let target = resolve_reference l from reference in
      Hashtbl.replace l.reference_cache key target;
      target
