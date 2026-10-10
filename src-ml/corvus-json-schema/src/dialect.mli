(** The JSON Schema dialects the evaluator understands, their vocabularies, and which keywords hold subschemas. A port
    of dialect.go of the Go module. Internal: the public names are in {!Corvus_json_schema}. *)

(** A JSON Schema dialect. The constructors are in specification order. *)
type t = Draft4 | Draft6 | Draft7 | Draft2019_09 | Draft2020_12

val rank : t -> int
(** [rank d] is the position of the dialect in specification order, from 0 for draft 4 to 4 for 2020-12. *)

val to_string : t -> string
(** [to_string d] is the dialect's usual name, such as ["draft2020-12"]. *)

val is_legacy : t -> bool
(** [is_legacy d] reports draft 7 and earlier, where [$ref] replaces its siblings and there are no vocabularies. *)

(** {1 Vocabularies}

    The vocabularies in effect for a schema resource (2019-09 and later), as bits. *)

val vocab_none : int
val vocab_core : int
val vocab_applicator : int
val vocab_validation : int
val vocab_meta_data : int
val vocab_format_annotation : int
val vocab_format_assertion : int
val vocab_content : int
val vocab_unevaluated : int

val vocab_all_annotating : int
(** Every vocabulary but format-assertion. *)

val known : string -> t option
(** [known uri] maps a well-known metaschema URI (normalised, no fragment) to its dialect. *)

val vocabulary_flag : string -> int
(** [vocabulary_flag uri] maps a vocabulary URI to its bit, or {!vocab_none}. *)

(** How a keyword holds subschemas. *)
type subschema_kind = Subschema_none | Subschema_single | Subschema_single_or_array | Subschema_array | Subschema_map

val subschema_kind_of : string -> t -> bool -> subschema_kind
(** [subschema_kind_of keyword dialect legacy_ref_overrides_siblings] says which keywords hold subschemas, by dialect.
*)
