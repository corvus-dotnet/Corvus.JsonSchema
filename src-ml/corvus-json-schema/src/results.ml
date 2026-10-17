(* Results collection. The evaluator opens a context per subschema application and writes keyword rows into the open
   context. Closing a context either commits it (a summary row, then its own rows newest first, after its committed
   descendants) or pops it (everything it and its descendants wrote is discarded). Levels decide which rows exist and
   which carry message text. A port of results.go of the Go module. *)

(* How much a collector records. *)
type level = Basic | Detailed | Verbose

let level_to_string = function Basic -> "Basic" | Detailed -> "Detailed" | Verbose -> "Verbose"

(* One result row. *)
type result = {
  is_match : bool;
  message : string;
  evaluation_location : string;
  schema_evaluation_location : string;
  document_evaluation_location : string;
}

type frame = { eval_length : int; frame_schema_path : string; doc_length : int; commit_index : int; rows_start : int }

(* A collector of the results of an evaluation. It is used by one evaluation at a time. *)
type t = {
  level : level;
  committed : result Vec.t;
  mutable frames : frame list;
  (* The rows written into open frames (each frame owns the tail from its rows_start). *)
  pending : result Vec.t;
  eval_path : Buffer.t;
  mutable schema_path : string;
  doc_path : Buffer.t;
}

let create level =
  {
    level;
    committed = Vec.create ();
    frames = [];
    pending = Vec.create ();
    eval_path = Buffer.create 64;
    schema_path = "";
    doc_path = Buffer.create 64;
  }

let level c = c.level
let results c = Array.to_list (Vec.to_array c.committed)

let truncate (v : result Vec.t) n =
  (* The rows beyond the length stay in the array until they are written over. *)
  v.Vec.len <- n

let reset c =
  truncate c.committed 0;
  c.committed.Vec.items <- [||];
  c.frames <- [];
  truncate c.pending 0;
  Buffer.clear c.eval_path;
  c.schema_path <- "";
  Buffer.clear c.doc_path

(* with_text reports whether a row with the given result carries its message. *)
let with_text c is_match = c.level = Verbose || ((not is_match) && c.level <> Basic)

(* records reports whether a keyword row with the given result is recorded at all. *)
let records c is_match = (not is_match) || c.level = Verbose

let restore c frame =
  Buffer.truncate c.eval_path frame.eval_length;
  c.schema_path <- frame.frame_schema_path;
  Buffer.truncate c.doc_path frame.doc_length

(* begin_child_context opens a child context. The evaluation path is extended by eval_segment (verbatim), the
   schema path is replaced by schema_location, and the document path is extended by doc_segment (already
   pointer-encoded). has_eval and has_doc say whether there is a segment at all. *)
let begin_child_context c has_eval eval_segment schema_location has_doc doc_segment =
  c.frames <-
    {
      eval_length = Buffer.length c.eval_path;
      frame_schema_path = c.schema_path;
      doc_length = Buffer.length c.doc_path;
      commit_index = Vec.length c.committed;
      rows_start = Vec.length c.pending;
    }
    :: c.frames;
  if has_eval then begin
    Buffer.add_char c.eval_path '/';
    Buffer.add_string c.eval_path eval_segment
  end;
  c.schema_path <- schema_location;
  if has_doc then begin
    Buffer.add_char c.doc_path '/';
    Buffer.add_string c.doc_path doc_segment
  end

let pop_frame c =
  match c.frames with
  | frame :: rest ->
      c.frames <- rest;
      frame
  | [] -> invalid_arg "Results: no open context"

(* pop_child_context closes a child context and discards everything it and its descendants wrote. *)
let pop_child_context c =
  let frame = pop_frame c in
  truncate c.committed frame.commit_index;
  truncate c.pending frame.rows_start;
  restore c frame

(* commit_child_context closes a child context. When the parent does not need the child's results (parent_is_match)
   they are discarded below Verbose. Otherwise the context's summary row is written and its rows are committed. *)
let commit_child_context c parent_is_match child_is_match message =
  if parent_is_match && c.level <> Verbose then pop_child_context c
  else begin
    Vec.push c.pending
      {
        is_match = child_is_match;
        message = (if with_text c child_is_match then message else "");
        evaluation_location = Buffer.contents c.eval_path;
        schema_evaluation_location = c.schema_path;
        document_evaluation_location = Buffer.contents c.doc_path;
      };
    let frame = pop_frame c in
    for i = Vec.length c.pending - 1 downto frame.rows_start do
      Vec.push c.committed (Vec.get c.pending i)
    done;
    truncate c.pending frame.rows_start;
    restore c frame
  end

let text c is_match message = if with_text c is_match then message else ""

(* evaluated_keyword records a keyword's result. The message is only looked at when with_text says so. *)
let evaluated_keyword c is_match message keyword =
  if records c is_match then begin
    let k = "/" ^ Uri.escape_pointer_token keyword in
    Vec.push c.pending
      {
        is_match;
        message = text c is_match message;
        evaluation_location = Buffer.contents c.eval_path ^ k;
        schema_evaluation_location = c.schema_path ^ k;
        document_evaluation_location = Buffer.contents c.doc_path;
      }
  end

let evaluated_keyword_for_property c is_match message property_name keyword =
  if records c is_match then begin
    let k = "/" ^ Uri.escape_pointer_token keyword in
    Vec.push c.pending
      {
        is_match;
        message = text c is_match message;
        evaluation_location = Buffer.contents c.eval_path ^ k;
        schema_evaluation_location = c.schema_path ^ k;
        document_evaluation_location = Buffer.contents c.doc_path ^ "/" ^ Uri.escape_pointer_token property_name;
      }
  end

(* ignored_keyword records an annotation: Verbose only. The keyword extends the evaluation path but not the schema
   path. *)
let ignored_keyword c message keyword =
  if c.level = Verbose then
    Vec.push c.pending
      {
        is_match = true;
        message;
        evaluation_location = Buffer.contents c.eval_path ^ "/" ^ Uri.escape_pointer_token keyword;
        schema_evaluation_location = c.schema_path;
        document_evaluation_location = Buffer.contents c.doc_path;
      }

let evaluated_boolean_schema c is_match =
  if records c is_match then
    Vec.push c.pending
      {
        is_match;
        message = "";
        evaluation_location = Buffer.contents c.eval_path;
        schema_evaluation_location = c.schema_path;
        document_evaluation_location = Buffer.contents c.doc_path;
      }

(* An annotation extracted from verbose results. *)
type annotation = { instance_location : string; keyword : string; schema_location : string; value : string }

(* annotations returns the annotations in a verbose collector's results. *)
let annotations c =
  let out = ref [] in
  for i = Vec.length c.committed - 1 downto 0 do
    let r = Vec.get c.committed i in
    if r.is_match && r.message <> "" then
      match String.rindex_opt r.evaluation_location '/' with
      | Some slash when r.evaluation_location <> r.schema_evaluation_location ->
          let keyword =
            String.sub r.evaluation_location (slash + 1) (String.length r.evaluation_location - slash - 1)
          in
          let first = r.message.[0] in
          if keyword <> "" && (String.contains "\"{[tfn-" first || (first >= '0' && first <= '9')) then
            out :=
              {
                instance_location = r.document_evaluation_location;
                keyword;
                schema_location = r.schema_evaluation_location;
                value = r.message;
              }
              :: !out
      | _ -> ()
  done;
  !out

(* schema_location_fragment returns "#" followed by the schema location, percent-encoded as a URI fragment
   (upper-case hex, UTF-8). *)
let schema_location_fragment schema_location =
  let hex = "0123456789ABCDEF" in
  let out = Buffer.create (String.length schema_location + 1) in
  Buffer.add_char out '#';
  String.iter
    (fun c ->
      if
        (c >= 'a' && c <= 'z')
        || (c >= 'A' && c <= 'Z')
        || (c >= '0' && c <= '9')
        || String.contains "-._~!$&'()*+,;=:@/?" c
      then Buffer.add_char out c
      else begin
        Buffer.add_char out '%';
        Buffer.add_char out hex.[Char.code c lsr 4];
        Buffer.add_char out hex.[Char.code c land 15]
      end)
    schema_location;
  Buffer.contents out

(* collect_annotations returns the annotations grouped by instance location, then keyword, then schema location
   fragment, with the values as JSON text. Each level is in ascending order of its key. Go returns maps. *)
let collect_annotations c =
  (* Sets a key of an association list in key order, with the last value given for a key. *)
  let rec set key update = function
    | [] -> [ (key, update None) ]
    | (k, v) :: rest when String.equal k key -> (k, update (Some v)) :: rest
    | (k, _) :: _ as list when String.compare key k < 0 -> (key, update None) :: list
    | pair :: rest -> pair :: set key update rest
  in
  List.fold_left
    (fun out a ->
      set a.instance_location
        (fun by_keyword ->
          set a.keyword
            (fun by_location ->
              set (schema_location_fragment a.schema_location) (fun _ -> a.value) (Option.value by_location ~default:[]))
            (Option.value by_keyword ~default:[]))
        out)
    [] (annotations c)
