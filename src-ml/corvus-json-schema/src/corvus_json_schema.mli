(** A JSON Schema evaluator for draft 4, 6, 7, 2019-09 and 2020-12, ported from the Corvus.Text.Json runtime evaluator
    by way of its Go port.

    A schema is compiled once into a node graph with fail-fast plans, then any number of instances are validated against
    it. {!Validator.is_valid_string} and its variants fail fast and report nothing. {!Validator.evaluate} is exhaustive
    and reports to a {!Results.t} collector at the Basic, Detailed or Verbose level, with the same rows (paths, messages,
    order) as the other Corvus implementations, annotations included.

    {[
      let () =
        match
          Corvus_json_schema.Validator.compile_string
            {|{
                "type": "object",
                "properties": {"id": {"type": "integer", "minimum": 1}},
                "required": ["id"]
              }|}
        with
        | Error e -> prerr_endline (Corvus_json_schema.error_to_string e)
        | Ok validator ->
            assert (Corvus_json_schema.Validator.is_valid_string validator {|{"id": 3}|});
            assert (not (Corvus_json_schema.Validator.is_valid_string validator {|{"id": 0}|}))
    ]}

    {1 Threads, domains and allocation}

    A validator is safe to share between threads and domains. Its compiled program is never written to after
    compilation.

    An evaluation needs working memory (a few buffers that grow to what the largest instance so far needed). A
    validator keeps one set of it, which one evaluation at a time holds, taken with an atomic compare and set. An
    evaluation that finds it held, by another thread or domain or by its own caller further up the stack (a custom
    format that validates), makes its own for that one evaluation. So every call is correct wherever it is made from,
    and in the steady state a validator used by one thread at a time validates a parsed document, or JSON text, without
    allocating anything on the OCaml heap.

    A caller that validates from several threads or domains at once gives each its own {!Scratch.t}, in the [?scratch]
    argument, and then no evaluation contends with another and none allocates. An optional argument is passed as an
    option, which is a block of its own when it is built at the call. To allocate nothing, build the option once and
    pass it as it is:

    {[
      let scratch = Some (Corvus_json_schema.Scratch.create ()) in
      List.for_all (fun text -> Corvus_json_schema.Validator.is_valid_string ?scratch validator text) texts
    ]}

    These allocate whichever way they are called: an asserted [regex], [idn-hostname] or [idn-email] format, a
    [hostname] (or the domain of an [email]) with a label that starts with [xn--], a custom format (which is given a
    copy of the string), and a [multipleOf] whose divisor has more than 17 significant digits. *)

val version : string
(** The version of this package. *)

(** {1 Dialects} *)

(** A JSON Schema dialect the evaluator understands. The constructors are in specification order. *)
type dialect =
  | Draft4  (** Draft 4. *)
  | Draft6  (** Draft 6. *)
  | Draft7  (** Draft 7. *)
  | Draft2019_09  (** Draft 2019-09. *)
  | Draft2020_12  (** Draft 2020-12. *)

val dialect_to_string : dialect -> string
(** [dialect_to_string d] is the dialect's usual name: ["draft4"], ["draft6"], ["draft7"], ["draft2019-09"] or
    ["draft2020-12"]. *)

(** {1 Documents} *)

(** JSON text parsed for evaluation. *)
module Document : sig
  type t
  (** JSON text parsed for evaluation: parse once, validate any number of times. It holds the UTF-8 text and one flat
      array of values that the evaluator reads in place, with no block per value. A document is immutable and safe to
      share between threads and domains.

      Parsing is strict RFC 8259. Anything but whitespace after the value, invalid UTF-8, lone surrogates in [\u]
      escapes, numbers out of the range of a float and nesting deeper than {!max_depth} are errors. Of duplicate property
      names, the last value is kept, at the position of the first.

      Integers from -2{^63} to 2{^64} - 1 are held exactly, and compared exactly with each other and with every other
      number. Any other number is held as the nearest float. *)

  type error = {
    message : string;  (** What was wrong. *)
    offset : int;  (** The byte offset at which the error was found. *)
  }
  (** Text that is not valid JSON. *)

  val error_to_string : error -> string
  (** [error_to_string e] is ["invalid JSON at offset <offset>: <message>"]. *)

  val max_depth : int
  (** The deepest nesting of arrays and objects a document may have. *)

  val parse_string : string -> (t, error) result
  (** [parse_string json] parses JSON text. The document holds a copy of it. *)

  val parse_bytes : Bytes.t -> (t, error) result
  (** [parse_bytes json] parses UTF-8 JSON text. The document keeps the bytes and copies nothing. Do not change them
      afterwards. *)

  val to_string : t -> string
  (** [to_string d] is the document as compact JSON text, with numbers as they were written. *)
end

(** {1 Results} *)

(** The results of an exhaustive evaluation, and the annotations in them. *)
module Results : sig
  (** How much a collector records. *)
  type level =
    | Basic  (** Failures only, without message text (the lowest overhead). *)
    | Detailed  (** Failures only, with message text. *)
    | Verbose  (** Every evaluation, passing and failing, with message text, including annotations. *)

  val level_to_string : level -> string
  (** [level_to_string l] is the level's name: ["Basic"], ["Detailed"] or ["Verbose"]. *)

  type result = {
    is_match : bool;  (** Whether the keyword or subschema matched. *)
    message : string;
        (** The message, or [""] when the level records none or the keyword has none. Annotation rows carry raw JSON. *)
    evaluation_location : string;
        (** The path of keywords from the root schema (for example [/properties/name/type]). *)
    schema_evaluation_location : string;
        (** The JSON pointer of the evaluated schema (or keyword) within its document. *)
    document_evaluation_location : string;  (** The JSON pointer of the instance location (for example [/name]). *)
  }
  (** One result row. *)

  type t
  (** A collector of the results of an evaluation. A collector is used by one evaluation at a time. It accumulates
      across evaluations until it is {!reset}. *)

  val create : level -> t
  (** [create level] makes a collector at the given level. *)

  val level : t -> level
  (** [level c] is the collector's level. *)

  val results : t -> result list
  (** [results c] is the results, in commit order. *)

  val reset : t -> unit
  (** [reset c] discards the results, so that the collector can be used for another evaluation. *)

  type annotation = {
    instance_location : string;  (** The instance location (a JSON pointer). *)
    keyword : string;  (** The annotating keyword. *)
    schema_location : string;  (** The JSON pointer of the schema object that holds the keyword. *)
    value : string;  (** The annotation value as JSON text. *)
  }
  (** An annotation extracted from verbose results. *)

  val annotations : t -> annotation list
  (** [annotations c] is the annotations in a verbose collector's results, in the order of the results. *)

  val collect_annotations : t -> (string * (string * (string * string) list) list) list
  (** [collect_annotations c] is the annotations grouped by instance location, then keyword, then schema location
      fragment (see {!schema_location_fragment}), with the values as JSON text. Each level is an association list in
      ascending order of its keys, for example
      [[("/name", [("title", [("#/properties/name", "\"Name\"")])])]]. *)

  val schema_location_fragment : string -> string
  (** [schema_location_fragment location] is ["#"] followed by the schema location, percent-encoded as a URI fragment
      (upper-case hexadecimal, UTF-8). *)
end

(** {1 Options} *)

type options = {
  default_dialect : dialect;  (** The dialect of documents without [$schema]. The default is {!Draft2020_12}. *)
  assert_format : bool option;
      (** Whether [format] is asserted: [Some true] always, [Some false] never. With [None], the default, the
          vocabularies decide (the 2020-12 format-assertion vocabulary asserts, anything else annotates). *)
  assert_format_in_legacy_drafts : bool;
      (** Also assert [format] in drafts 4 to 7, when [assert_format] is [None]. The default is [false]. *)
  assert_content : bool;
      (** Whether [contentEncoding] and [contentMediaType] are asserted in draft 7, the only draft that asserts them.
          The default is [true]. *)
  formats : (string * (string -> bool)) list;
      (** Custom format assertions by name, each over the string value (or the number's JSON text, for numbers). A
          custom format takes precedence over a built-in one of the same name, and of two with one name the later in
          the list. It receives a copy of the string. *)
  resolver : (string -> Document.t option) option;
      (** The function that resolves remote documents by absolute URI. It returns [None] if the document is unknown. The
          standard metaschemas are always available. *)
  base_uri : string option;  (** The base URI of the root document. *)
  entry_point : string option;
      (** A reference, relative to the root, to evaluate from (for example [#/$defs/item]). The default is the root. *)
  max_depth : int;
      (** The maximum depth of in-place recursion on a cycle before evaluation is abandoned. The default is 128. Values
          below 1 are ignored. *)
}
(** The options for compiling a schema. Start from {!default_options}, or build one with {!val-options}. *)

val default_options : options
(** The default options: 2020-12 for documents without [$schema], [format] as the vocabularies say, content asserted in
    draft 7, no custom format, no resolver, no base URI, the root as the entry point and a maximum depth of 128. *)

val options :
  ?default_dialect:dialect ->
  ?assert_format:bool ->
  ?assert_format_in_legacy_drafts:bool ->
  ?assert_content:bool ->
  ?formats:(string * (string -> bool)) list ->
  ?resolver:(string -> Document.t option) ->
  ?base_uri:string ->
  ?entry_point:string ->
  ?max_depth:int ->
  unit ->
  options
(** [options ?default_dialect ... ()] is {!default_options} with the fields given, for example
    [options ~default_dialect:Draft7 ~assert_format:true ()]. *)

(** {1 Errors} *)

(** Why a schema could not be compiled or an instance could not be evaluated. *)
type error =
  | Parse_error of Document.error  (** The text is not JSON. *)
  | Compile_error of string
      (** The schema cannot be compiled (an unresolvable reference, an invalid pattern), with the reason. *)
  | Depth_exceeded
      (** Evaluation recursed in place beyond the maximum depth (a schema that loops without consuming the instance). *)

val error_to_string : error -> string
(** [error_to_string e] is the reason as text. *)

(** {1 Validators} *)

(** The working memory of evaluations. *)
module Scratch : sig
  type t
  (** The buffers an evaluation needs, which grow to what the largest instance so far needed and are kept. One
      evaluation at a time uses a scratch, so a caller that validates from several threads or domains gives each its
      own. A scratch serves any number of validators. An evaluation given a scratch that is already in use (by its own
      caller, as when a custom format validates) makes buffers of its own. *)

  val create : unit -> t
  (** [create ()] makes a scratch. *)
end

(** Compiled schemas. *)
module Validator : sig
  type t
  (** A compiled schema. It is safe to share between threads and domains (see the notes at the top). *)

  (** {1 Compiling}

      Each function returns [Error (Parse_error _)] when schema text is not JSON, and [Error (Compile_error _)] when the
      schema cannot be compiled. *)

  val compile : ?options:options -> string -> (t, error) result
  (** [compile schema] is {!compile_string}. *)

  val compile_string : ?options:options -> string -> (t, error) result
  (** [compile_string schema] compiles a schema given as JSON text. *)

  val compile_bytes : ?options:options -> Bytes.t -> (t, error) result
  (** [compile_bytes schema] compiles a schema given as UTF-8 JSON text. The validator keeps the bytes. Do not change
      them afterwards. *)

  val compile_document : ?options:options -> Document.t -> (t, error) result
  (** [compile_document schema] compiles a schema document. *)

  val compile_uri : ?options:options -> string -> (t, error) result
  (** [compile_uri uri] compiles the schema document at a URI, fetched through the document resolver given in the
      options (or one of the standard metaschemas). *)

  (** {1 Validating}

      These fail fast and report nothing but the answer. Text that is not JSON is not valid, and neither is an instance
      on which the schema recursed in place beyond the maximum depth. The [validate] functions tell those apart.

      Each takes the working memory of the evaluation from [?scratch] when it is given, and from the validator when it
      is not (see the notes at the top). *)

  val is_valid_document : ?scratch:Scratch.t -> t -> Document.t -> bool
  (** [is_valid_document v instance] reports whether an instance is valid. *)

  val is_valid_string : ?scratch:Scratch.t -> t -> string -> bool
  (** [is_valid_string v json] reports whether JSON text is a valid instance. The text is copied and parsed into buffers
      that are reused, so a validation allocates nothing in the steady state. *)

  val is_valid_bytes : ?scratch:Scratch.t -> t -> Bytes.t -> bool
  (** [is_valid_bytes v json] reports whether UTF-8 JSON text is a valid instance. The text is parsed where it is, into
      buffers that are reused, so a validation allocates nothing in the steady state. Nothing is kept of the bytes. *)

  val validate_document : ?scratch:Scratch.t -> t -> Document.t -> (bool, error) result
  (** [validate_document v instance] reports whether an instance is valid. It is [Error Depth_exceeded] when evaluation
      recursed in place beyond the maximum depth. *)

  val validate_string : ?scratch:Scratch.t -> t -> string -> (bool, error) result
  (** [validate_string v json] reports whether JSON text is a valid instance. It is [Error (Parse_error _)] when the
      text is not JSON, and [Error Depth_exceeded] when evaluation recursed in place beyond the maximum depth. *)

  val validate_bytes : ?scratch:Scratch.t -> t -> Bytes.t -> (bool, error) result
  (** [validate_bytes v json] is {!validate_string} for UTF-8 JSON text, parsed where it is. *)

  (** {1 Evaluating}

      These are exhaustive: they evaluate every keyword and report each to the collector, and report whether the
      instance is valid. A collector that already holds results keeps them, and the new ones follow. *)

  val evaluate : ?scratch:Scratch.t -> t -> results:Results.t -> Document.t -> (bool, error) result
  (** [evaluate v ~results instance] evaluates an instance exhaustively, reporting to the collector. It is
      [Error Depth_exceeded] when evaluation recursed in place beyond the maximum depth. *)

  val evaluate_string : ?scratch:Scratch.t -> t -> results:Results.t -> string -> (bool, error) result
  (** [evaluate_string v ~results json] is {!evaluate} for JSON text. It is [Error (Parse_error _)] when the text is not
      JSON. *)

  val evaluate_bytes : ?scratch:Scratch.t -> t -> results:Results.t -> Bytes.t -> (bool, error) result
  (** [evaluate_bytes v ~results json] is {!evaluate} for UTF-8 JSON text. *)
end
