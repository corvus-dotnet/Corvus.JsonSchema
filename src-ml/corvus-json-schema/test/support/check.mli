(** A tiny assertion helper for the test executables. A test records each check, prints the failures as they happen, and
    ends with {!finish}, which prints the counts and exits with a non-zero status if any check failed. *)

val check : string -> bool -> unit
(** [check what ok] records one check. A failing check prints [what]. *)

val checkf : bool -> ('a, unit, string, unit) format4 -> 'a
(** [checkf ok fmt ...] is {!check} with a formatted description, which is built whether or not the check passes. *)

val equal_string : string -> expected:string -> string -> unit
(** [equal_string what ~expected actual] checks that two strings are equal, printing both when they are not. *)

val equal_int : string -> expected:int -> int -> unit
(** [equal_int what ~expected actual] checks that two integers are equal, printing both when they are not. *)

val equal_bool : string -> expected:bool -> bool -> unit
(** [equal_bool what ~expected actual] checks that two booleans are equal, printing both when they are not. *)

val fail : string -> unit
(** [fail what] records a failed check. *)

val section : string -> unit
(** [section name] names the checks that follow in the failure messages. *)

val passed : unit -> int
(** The number of checks that have passed so far. *)

val failed : unit -> int
(** The number of checks that have failed so far. *)

val finish : string -> unit
(** [finish name] prints the counts for the test named [name] and exits, with status 1 if any check failed. *)
