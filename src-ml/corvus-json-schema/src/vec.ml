(* A growable array, for the loader and the compiler. *)

type 'a t = { mutable items : 'a array; mutable len : int }

let create () = { items = [||]; len = 0 }
let length v = v.len
let get v i = if i < v.len then v.items.(i) else invalid_arg "Vec.get"
let set v i x = if i < v.len then v.items.(i) <- x else invalid_arg "Vec.set"

let push v x =
  if v.len = Array.length v.items then begin
    let grown = Array.make ((2 * v.len) + 8) x in
    Array.blit v.items 0 grown 0 v.len;
    v.items <- grown
  end;
  v.items.(v.len) <- x;
  v.len <- v.len + 1

let to_array v = Array.sub v.items 0 v.len
