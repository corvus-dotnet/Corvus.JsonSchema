(* The options for compiling a schema, and the error that reports a schema that could not be compiled. A port of
   options.go of the Go module. Internal: the public names are in {!Corvus_json_schema}. *)

type t = {
  default_dialect : Dialect.t;
  (* Whether format is asserted: 1 always, -1 never, 0 as the vocabularies say. *)
  assert_format : int;
  assert_format_in_legacy_drafts : bool;
  assert_content : bool;
  (* The custom formats, by name. *)
  formats : (string * (string -> bool)) list;
  resolver : (string -> Document.t option) option;
  base_uri : string;
  entry_point : string option;
  max_depth : int;
}

let default =
  {
    default_dialect = Dialect.Draft2020_12;
    assert_format = 0;
    assert_format_in_legacy_drafts = false;
    assert_content = true;
    formats = [];
    resolver = None;
    base_uri = "";
    entry_point = None;
    max_depth = 128;
  }

(* Raised inside the compiler with the reason compilation failed (an unresolvable reference, an invalid pattern).
   The public functions return it as an [Error]. *)
exception Compile_error of string

let compile_error message = raise (Compile_error message)
