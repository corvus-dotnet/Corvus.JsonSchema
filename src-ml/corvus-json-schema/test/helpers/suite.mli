(** What the tests share: the JSON-Schema-Test-Suite and its files, read with the evaluator's own parser. *)

module Json = Corvus_json_schema__Document

val root : unit -> string
(** The directory of the JSON-Schema-Test-Suite: the value of [JSON_SCHEMA_TEST_SUITE], or the submodule of the
    repository found from the current directory. *)

val read_file : string -> string option
(** [read_file path] is the bytes of a file, or [None] when it cannot be read. *)

val json_files : string -> string list
(** [json_files dir] is the paths of the [.json] files of a directory, in order of their names. It is empty when the
    directory cannot be read. *)

val member : Json.t -> int -> string -> int
(** [member d n name] is the value of a property of an object value, or -1. *)

val text : Json.t -> int -> string
(** [text d n] is a value as compact JSON text. *)

val string : Json.t -> int -> string
(** [string d n] is the text of a string value. *)

val items : Json.t -> int -> int list
(** [items d n] is the values of an array value. *)

val properties : Json.t -> int -> (string * int) list
(** [properties d n] is the names and values of an object value. *)

val remote_resolver : string -> string -> Corvus_json_schema.Document.t option
(** [remote_resolver root] resolves the suite's remotes ([http://localhost:1234/...]) from its remotes directory. *)

type group = { description : string; schema : string; tests : case list }
(** A group of tests of a suite file: a schema and the instances to evaluate against it. *)

and case = { case_description : string; data : string; valid : bool }

val groups : string -> group list
(** [groups path] reads the groups of a suite file.

    @raise Failure when the file cannot be read or is not JSON. *)
