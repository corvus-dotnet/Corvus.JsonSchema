let version = "0.1.0"

type dialect = Dialect.t = Draft4 | Draft6 | Draft7 | Draft2019_09 | Draft2020_12

let dialect_to_string = Dialect.to_string

module Json = Document

module Document = struct
  type t = Json.t
  type error = Json.error = { message : string; offset : int }

  let max_depth = Json.max_depth
  let error_to_string = Json.error_to_string
  let parse_string = Json.parse_string
  let parse_bytes = Json.parse_bytes
  let to_string = Json.to_string
end

module Collector = Results

module Results = struct
  type level = Collector.level = Basic | Detailed | Verbose

  let level_to_string = Collector.level_to_string

  type result = Collector.result = {
    is_match : bool;
    message : string;
    evaluation_location : string;
    schema_evaluation_location : string;
    document_evaluation_location : string;
  }

  type t = Collector.t

  let create = Collector.create
  let level = Collector.level
  let results = Collector.results
  let reset = Collector.reset

  type annotation = Collector.annotation = {
    instance_location : string;
    keyword : string;
    schema_location : string;
    value : string;
  }

  let annotations = Collector.annotations
  let collect_annotations = Collector.collect_annotations
  let schema_location_fragment = Collector.schema_location_fragment
end

type options = {
  default_dialect : dialect;
  assert_format : bool option;
  assert_format_in_legacy_drafts : bool;
  assert_content : bool;
  formats : (string * (string -> bool)) list;
  resolver : (string -> Document.t option) option;
  base_uri : string option;
  entry_point : string option;
  max_depth : int;
}

let default_options =
  {
    default_dialect = Draft2020_12;
    assert_format = None;
    assert_format_in_legacy_drafts = false;
    assert_content = true;
    formats = [];
    resolver = None;
    base_uri = None;
    entry_point = None;
    max_depth = 128;
  }

let options ?(default_dialect = default_options.default_dialect) ?assert_format
    ?(assert_format_in_legacy_drafts = default_options.assert_format_in_legacy_drafts)
    ?(assert_content = default_options.assert_content) ?(formats = []) ?resolver ?base_uri ?entry_point
    ?(max_depth = default_options.max_depth) () =
  {
    default_dialect;
    assert_format;
    assert_format_in_legacy_drafts;
    assert_content;
    formats;
    resolver;
    base_uri;
    entry_point;
    max_depth;
  }

type error = Parse_error of Document.error | Compile_error of string | Depth_exceeded

let error_to_string = function
  | Parse_error e -> Document.error_to_string e
  | Compile_error message -> message
  | Depth_exceeded -> "the schema recursed in place beyond the maximum depth"

(* The options as the compiler takes them. *)
let internal_options (o : options) : Options.t =
  {
    Options.default_dialect = o.default_dialect;
    assert_format = (match o.assert_format with Some true -> 1 | Some false -> -1 | None -> 0);
    assert_format_in_legacy_drafts = o.assert_format_in_legacy_drafts;
    assert_content = o.assert_content;
    (* Last first, so that a later format of a name is the one found. *)
    formats = List.rev o.formats;
    resolver = o.resolver;
    base_uri = (match o.base_uri with Some uri -> uri | None -> "");
    entry_point = o.entry_point;
    (* Values below 1 are ignored. *)
    max_depth = (if o.max_depth >= 1 then o.max_depth else Options.default.max_depth);
  }

module Scratch = struct
  type t = Eval.t

  let create = Eval.create
end

module Validator = struct
  (* A compiled schema. The program is never written to after compilation. The validator also keeps one evaluator
     for the calls that are given no scratch, which one evaluation at a time holds: [busy] says that one does. *)
  type t = { program : Program.t; own : Eval.t; busy : bool Atomic.t }

  let make compiled options =
    { program = Plan.new_program compiled options; own = Eval.create (); busy = Atomic.make false }

  let compile_with build =
    match build () with
    | compiled, options -> Ok (make compiled options)
    | exception Options.Compile_error message -> Error (Compile_error message)

  let compile_document ?(options = default_options) schema =
    compile_with (fun () ->
        let options = internal_options options in
        (Compiler.compile_document schema options, options))

  let compile_bytes ?options schema =
    match Document.parse_bytes schema with
    | Error e -> Error (Parse_error e)
    | Ok document -> compile_document ?options document

  let compile_string ?options schema =
    match Document.parse_string schema with
    | Error e -> Error (Parse_error e)
    | Ok document -> compile_document ?options document

  let compile = compile_string

  let compile_uri ?(options = default_options) uri =
    compile_with (fun () ->
        let options = internal_options options in
        (Compiler.compile_from_uri uri options, options))

  (* acquire takes the evaluator of an evaluation: the scratch given, or the validator's own, or a new one when that
     one is in use (by another thread or domain, or by this evaluation's caller further up the stack). *)
  let acquire v (scratch : Scratch.t option) =
    let e =
      match scratch with
      | Some e -> if e.in_use then Eval.create () else e
      | None -> if Atomic.compare_and_set v.busy false true then v.own else Eval.create ()
    in
    e.in_use <- true;
    e

  let release v (e : Eval.t) =
    e.in_use <- false;
    if e == v.own then Atomic.set v.busy false

  (* The outcomes of a fail-fast evaluation. *)
  let invalid = 0
  let valid = 1
  let depth_exceeded = 2

  (* run validates a document with an evaluator, failing fast, and gives the evaluator back whatever happens. The
     outcome is read, and the text of the evaluator's own document let go of, before the evaluator is given back,
     since another thread may take it at once. *)
  let run v (e : Eval.t) instance is_text =
    match Eval.validate e v.program instance with
    | ok ->
        let outcome = if e.depth_exceeded then depth_exceeded else if ok then valid else invalid in
        if is_text then Json.release_source e.text;
        release v e;
        outcome
    | exception raised ->
        Eval.finish e;
        Json.release_source e.text;
        release v e;
        raise raised

  let is_valid_document ?scratch v instance = run v (acquire v scratch) instance false = valid

  (* parse_bytes parses JSON text into the evaluator's document. *)
  let parse_bytes (e : Eval.t) json = Json.parse_into e.parser e.text json (Bytes.length json)

  (* parse_string parses JSON text into the evaluator's document, by way of the evaluator's copy of the text. A byte
     that no number is written with follows the copy, where the text of a number stops. *)
  let parse_string (e : Eval.t) json =
    let length = String.length json in
    if Bytes.length e.copy <= length then e.copy <- Bytes.create (Int.max (length + 1) (2 * Bytes.length e.copy));
    Bytes.blit_string json 0 e.copy 0 length;
    Bytes.set e.copy length ' ';
    Json.parse_into e.parser e.text e.copy length

  (* run_text validates the text an evaluator has parsed, or gives the evaluator back when the text is not JSON. *)
  let run_text v (e : Eval.t) parsed =
    if parsed then run v e e.text true
    else begin
      Json.release_source e.text;
      release v e;
      invalid
    end

  let is_valid_bytes ?scratch v json =
    let e = acquire v scratch in
    run_text v e (parse_bytes e json) = valid

  let is_valid_string ?scratch v json =
    let e = acquire v scratch in
    run_text v e (parse_string e json) = valid

  let result_of outcome =
    if outcome = depth_exceeded then Error Depth_exceeded else if outcome = valid then Ok true else Ok false

  let validate_document ?scratch v instance = result_of (run v (acquire v scratch) instance false)

  let validate_text v (e : Eval.t) parsed =
    if parsed then result_of (run v e e.text true)
    else begin
      let error = Json.parser_error e.parser in
      Json.release_source e.text;
      release v e;
      Error (Parse_error error)
    end

  let validate_bytes ?scratch v json =
    let e = acquire v scratch in
    validate_text v e (parse_bytes e json)

  let validate_string ?scratch v json =
    let e = acquire v scratch in
    validate_text v e (parse_string e json)

  let evaluate ?scratch v ~results instance =
    let e = acquire v scratch in
    match Eval.evaluate e v.program instance results with
    | ok ->
        let result = if e.depth_exceeded then Error Depth_exceeded else Ok ok in
        release v e;
        result
    | exception raised ->
        Eval.finish e;
        release v e;
        raise raised

  let evaluate_bytes ?scratch v ~results json =
    match Document.parse_bytes json with
    | Error e -> Error (Parse_error e)
    | Ok instance -> evaluate ?scratch v ~results instance

  let evaluate_string ?scratch v ~results json =
    match Document.parse_string json with
    | Error e -> Error (Parse_error e)
    | Ok instance -> evaluate ?scratch v ~results instance
end
