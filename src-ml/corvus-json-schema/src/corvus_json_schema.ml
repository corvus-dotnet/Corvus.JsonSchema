let version = "0.1.1"

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
  pattern_match_limit : float;
  pattern_match_clock : (unit -> float) option;
}

let default_pattern_match_limit = 1.0
let no_pattern_match_limit = 0.0

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
    pattern_match_limit = default_pattern_match_limit;
    pattern_match_clock = None;
  }

let options ?(default_dialect = default_options.default_dialect) ?assert_format
    ?(assert_format_in_legacy_drafts = default_options.assert_format_in_legacy_drafts)
    ?(assert_content = default_options.assert_content) ?(formats = []) ?resolver ?base_uri ?entry_point
    ?(max_depth = default_options.max_depth) ?(pattern_match_limit = default_options.pattern_match_limit)
    ?pattern_match_clock () =
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
    pattern_match_limit;
    pattern_match_clock;
  }

type error =
  | Parse_error of Document.error
  | Compile_error of string
  | Depth_exceeded
  | Pattern_too_large of { pattern : string; keyword : string; limit : string; value : int }
  | Pattern_match_limit of { pattern : string; limit : float; memory : bool }

exception Pattern_match_limit_reached of { pattern : string; limit : float; memory : bool }

(* shown_pattern quotes a pattern for a message, or its first 120 bytes and its length when it is longer. *)
let shown_pattern pattern =
  let shown = 120 in
  if String.length pattern <= shown then "'" ^ pattern ^ "'"
  else begin
    (* Not in the middle of a character. *)
    let cut = ref shown in
    while !cut > 0 && Char.code pattern.[!cut] land 0xC0 = 0x80 do
      decr cut
    done;
    Printf.sprintf "'%s...' (%d bytes)" (String.sub pattern 0 !cut) (String.length pattern)
  end

let match_limit_message pattern limit memory =
  if memory then
    "A match of the regular expression " ^ shown_pattern pattern
    ^ " was abandoned: it needed more than the limit of 1,048,576 backtracking entries."
  else
    Printf.sprintf "A match of the regular expression %s was abandoned: it took longer than the limit of %gs."
      (shown_pattern pattern) limit

let error_to_string = function
  | Parse_error e -> Document.error_to_string e
  | Compile_error message -> message
  | Depth_exceeded -> "the schema recursed in place beyond the maximum depth"
  | Pattern_too_large { pattern; keyword; limit; value } ->
      Printf.sprintf "The regular expression %s in %s is too large to read as an ECMA-262 regular expression: %s."
        (shown_pattern pattern) keyword
        (Corvus_json_schema_ecmaregex.error_message (Too_large (limit, value)))
  | Pattern_match_limit { pattern; limit; memory } -> match_limit_message pattern limit memory

let () =
  Printexc.register_printer (function
    | Pattern_match_limit_reached { pattern; limit; memory } ->
        Some ("Corvus_json_schema.Pattern_match_limit_reached: " ^ match_limit_message pattern limit memory)
    | _ -> None)

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
    (* Zero or less, or a value that is not a number, is no limit. *)
    pattern_match_limit = (if o.pattern_match_limit > 0. then o.pattern_match_limit else 0.);
    pattern_match_clock = o.pattern_match_clock;
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

  (* The schema is compiled and its plans are made inside the handler: the plans match each pattern against the
     property names the schema declares, and that match has the limit too. *)
  let compile_with (options : Options.t) build =
    match make (build options) options with
    | validator -> Ok validator
    | exception Options.Compile_error message -> Error (Compile_error message)
    | exception Options.Pattern_too_large (pattern, keyword, limit, value, _) ->
        Error (Pattern_too_large { pattern; keyword; limit; value })
    | exception Pattern.Match_limit (pattern, memory) ->
        Error (Pattern_match_limit { pattern; limit = options.pattern_match_limit; memory })

  let compile_document ?(options = default_options) schema =
    compile_with (internal_options options) (Compiler.compile_document schema)

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
    compile_with (internal_options options) (Compiler.compile_from_uri uri)

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

  (* limit_reached is the exception of a match of the validator's that reached its limit. *)
  let limit_reached v pattern memory =
    Pattern_match_limit_reached { pattern; limit = v.program.pattern_match_limit; memory }

  (* run validates a document with an evaluator, failing fast, and gives the evaluator back whatever happens. The
     outcome is read, and the text of the evaluator's own document let go of, before the evaluator is given back,
     since another thread may take it at once. A match that reached its limit has no outcome: the evaluation is
     abandoned and Pattern_match_limit_reached is raised. *)
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
        raise
          (match raised with Pattern.Match_limit (pattern, memory) -> limit_reached v pattern memory | _ -> raised)

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

  (* run_result is run for the functions that return a result: the limit of a match is an error of theirs. *)
  let run_result v e instance is_text =
    match run v e instance is_text with
    | outcome -> result_of outcome
    | exception Pattern_match_limit_reached { pattern; limit; memory } ->
        Error (Pattern_match_limit { pattern; limit; memory })

  let validate_document ?scratch v instance = run_result v (acquire v scratch) instance false

  let validate_text v (e : Eval.t) parsed =
    if parsed then run_result v e e.text true
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
    | exception raised -> (
        Eval.finish e;
        release v e;
        match raised with
        | Pattern.Match_limit (pattern, memory) ->
            Error (Pattern_match_limit { pattern; limit = v.program.pattern_match_limit; memory })
        | _ -> raise raised)

  let evaluate_bytes ?scratch v ~results json =
    match Document.parse_bytes json with
    | Error e -> Error (Parse_error e)
    | Ok instance -> evaluate ?scratch v ~results instance

  let evaluate_string ?scratch v ~results json =
    match Document.parse_string json with
    | Error e -> Error (Parse_error e)
    | Ok instance -> evaluate ?scratch v ~results instance
end
