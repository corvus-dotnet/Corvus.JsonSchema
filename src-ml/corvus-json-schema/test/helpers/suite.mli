(** What the tests share: the JSON-Schema-Test-Suite and its files, read with the evaluator's own parser. *)

module Json = Corvus_json_schema__Document

val root : unit -> string
(** The directory of the JSON-Schema-Test-Suite: the value of [JSON_SCHEMA_TEST_SUITE], or the submodule of the
    repository found from the current directory. *)

val absent : string -> bool
(** [absent root] says that the suite is not at [root] and that [JSON_SCHEMA_TEST_SUITE] does not name one. That is the
    case for the published package built on its own, which does not hold the suite, and a test that reads the suite then
    says that it was skipped (see {!skipped}). A suite that [JSON_SCHEMA_TEST_SUITE] names and that is not there is a
    failure, as it is for the repository's workflow, which names it. *)

val skipped : string -> unit
(** [skipped what] prints the line that says the test named [what] did not read the suite. *)

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

val tests_dir : string -> string
(** [tests_dir root] is the directory of the suite's validation tests ([validation/tests]), in which every dialect's
    cases are, the optional ones in [optional]. *)

val compatible : string option -> int -> bool
(** [compatible compatibility release] says whether a test case's [compatibility] (a comma-separated list of minimums,
    [<=] maximums and [=] releases, from the smallest release to the largest) admits the release, by the algorithm of
    the suite's validation/README.md. A test case without one is compatible with every release. *)

type group = {
  description : string;
  compatibility : string option;
  tags : string list;
  external_schemas : (string * string) list;  (** The schemas the test case references, as JSON text by URI. *)
  schema : string;
  tests : case list;
}
(** A test case of a suite file: a schema and the instances to evaluate against it, the releases it applies to, its tags
    (whether it expects format to be asserted), and the schemas it references. *)

and case = { case_description : string; data : string; valid : bool }

val has_tag : group -> string -> bool
(** [has_tag group tag] says whether a test case has a tag. *)

val case_resolver : group -> (string -> Corvus_json_schema.Document.t option) option
(** [case_resolver group] serves a test case's external schemas at their retrieval URIs, or is [None] when it has none.
*)

val groups : string -> group list
(** [groups path] reads the test cases of a suite file.

    @raise Failure when the file cannot be read or is not JSON. *)
