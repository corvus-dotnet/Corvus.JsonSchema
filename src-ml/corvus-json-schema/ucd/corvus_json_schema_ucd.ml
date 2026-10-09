(* A stub. The tables and the lookups follow. *)

let version = "17.0.0"
let max_rune = 0x10FFFF

type table = int array

let contains (_ : table) (_ : int) = false
let ranges (t : table) = Array.copy t
let union (_ : table list) : table = [||]
let letter : table = [||]
let cased_letter : table = [||]
let mark : table = [||]
let number : table = [||]
let punctuation : table = [||]
let symbol : table = [||]
let separator : table = [||]
let other : table = [||]
let assigned : table = [||]
let unknown_script : table = [||]
let category (_ : string) : table option = None
let binary (_ : string) : table option = None
let script (_ : string) : table option = None
let script_extensions (_ : string) : table option = None
let script_names : string array array = [||]
let binary_names : string array = [||]
let fold (r : int) = r
let to_upper (r : int) = r
let folding () : int array = [||]
let fold_by_table (r : int) = r
let to_upper_by_table (r : int) = r
