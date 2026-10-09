(** The Unicode Character Database properties the package reads, for one fixed version of Unicode.

    Nothing in the package takes Unicode properties from anywhere else. With its own tables the package gives the same
    answers whichever compiler builds it and wherever it runs, and the answers change only when the tables are generated
    again. {!version} names the version of Unicode they hold.

    The tables are in [tables.ml], which [tools/gen-ucd-tables.ps1] writes. Each is a sorted list of inclusive ranges. A
    value that follows from other values (a General_Category group such as L, the Assigned property, the Unknown script)
    has no table of its own and is built from them.

    This is a port of the ucd package of the Go module ([src-go/corvus-json-schema/internal/ucd]). One thing differs.
    The Go package builds a value that follows from others when it is first asked for, behind a [sync.Once]. Here every
    table is made when the module is initialised, as in the Object Pascal port, so that reading a table needs no lock
    and every value of the module is immutable and safe to share between threads and domains.

    A code point is an [int]. Every function accepts any [int] and treats one outside 0 to {!max_rune} as a code point
    that is in no table and has no case mapping. *)

val version : string
(** The version of Unicode the tables hold. *)

val max_rune : int
(** The last Unicode code point, U+10FFFF. *)

type table
(** An immutable set of code points. *)

val contains : table -> int -> bool
(** [contains t r] reports whether the table holds the code point. It does not allocate. *)

val ranges : table -> int array
(** [ranges t] returns the ranges of the table as inclusive pairs in ascending order ([lo0; hi0; lo1; hi1; ...]). The
    pairs neither overlap nor touch. The array is fresh and the caller may change it. *)

val bounds16 : table -> int array
(** The bounds of the ranges that end at or below U+FFFF, as the table stores them. A range that crosses U+FFFF is
    stored in two parts, and this is the first. For the tests. *)

val bounds32 : table -> int array
(** The bounds of the ranges that start above U+FFFF, as the table stores them. For the tests. *)

val union : table list -> table
(** [union tables] builds the table of the code points that any of the tables holds. A caller that tests one code point
    against several properties builds their union once and searches one table. *)

(** {1 Values built from others}

    The General_Category values that are groups of other values, the Assigned property and the Unknown script.
    [tools/gen-ucd-tables.ps1] checks each definition against the source of the tables. *)

val letter : table
(** The General_Category group L. *)

val cased_letter : table
(** The General_Category group LC. *)

val mark : table
(** The General_Category group M. *)

val number : table
(** The General_Category group N. *)

val punctuation : table
(** The General_Category group P. *)

val symbol : table
(** The General_Category group S. *)

val separator : table
(** The General_Category group Z. *)

val other : table
(** The General_Category group C. *)

val assigned : table
(** The code points that have a General_Category other than Cn. *)

val unknown_script : table
(** The code points that belong to no script. They are the unassigned, private use and surrogate code points. *)

(** {1 Tables by name} *)

val category : string -> table option
(** [category name] returns the table of a General_Category value by its short name, such as ["Lu"] or ["L"]. It returns
    [None] for any other name. *)

val binary : string -> table option
(** [binary name] returns the table of a binary property by its canonical name, such as ["Alphabetic"] or
    ["White_Space"]. It returns [None] for any other name. ASCII and Any, which ECMA-262 adds to the properties of
    Unicode, are not here. *)

val script : string -> table option
(** [script name] returns the table of a Script value by its long name or an alias, such as ["Greek"] or ["Grek"]. It
    returns [None] for any other name. *)

val script_extensions : string -> table option
(** [script_extensions name] returns the table of a Script_Extensions value by the long name of the script or an alias.
    It returns [None] for any other name. *)

val script_names : string array array
(** The names of every script. Each element is the long name of one script followed by its aliases. The caller must not
    change it. *)

val binary_names : string array
(** The canonical name of every binary property that has a table, which is every name {!binary} knows but Assigned. The
    caller must not change it. *)

(** {1 Case} *)

val fold : int -> int
(** [fold r] returns the simple case folding of a code point (the C and S rows of CaseFolding.txt), which is the code
    point itself when it has none. Two code points are equal ignoring case when [fold] gives the same result for both.
    It does not allocate. *)

val to_upper : int -> int
(** [to_upper r] returns the simple uppercase mapping of a code point, which is the code point itself when it has none.
    It does not allocate. *)

val folding : unit -> int array
(** [folding ()] returns every code point that {!fold} changes, in ascending order. The array is fresh. *)

val fold_by_table : int -> int
(** {!fold} without its path for ASCII. The tests compare the two paths. *)

val to_upper_by_table : int -> int
(** {!to_upper} without its path for ASCII. The tests compare the two paths. *)
