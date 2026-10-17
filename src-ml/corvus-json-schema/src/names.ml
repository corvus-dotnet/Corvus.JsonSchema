(* Property name lookup for the fail-fast plans. A port of names.go of the Go module.

   Go's words are 64 bits and an OCaml [int] is 63, so a word of eight bytes loses the top bit of its last byte. Two
   names of one length that differ in that bit alone cannot both be valid UTF-8 (one would have a continuation byte
   where the other, after the same bytes, has an ASCII one), and every name here comes from a parsed document, which
   is valid UTF-8. So a word still tells names apart exactly as Go's does. *)

(* name_word is a word that tells names of one length apart cheaply. For a name of at most eight bytes it is unique
   among names of the same length: the first and last four bytes (overlapping, so every byte is in one of them), or
   for shorter names the first, middle and last byte. For a longer name it is the first eight bytes, so names with
   different words differ, and names with the same word are compared in full. *)
let name_word b off n =
  if n > 8 then Int64.to_int (Bytes.get_int64_le b off)
  else if n >= 4 then
    Int32.to_int (Bytes.get_int32_le b off)
    land 0xffffffff
    lor ((Int32.to_int (Bytes.get_int32_le b (off + n - 4)) land 0xffffffff) lsl 32)
  else if n > 0 then
    Char.code (Bytes.get b off)
    lor (Char.code (Bytes.get b (off + (n / 2))) lsl 8)
    lor (Char.code (Bytes.get b (off + n - 1)) lsl 16)
  else 0

(* tail_word is the last eight bytes of a name longer than eight bytes. *)
let tail_word b off n = Int64.to_int (Bytes.get_int64_le b (off + n - 8))

(* The first multiplier tried for a map's hash (Go's, cut to what an [int] holds), and the number tried. *)
let name_hash_multiplier = 0x1e3779b97f4a7c15
let name_hash_multipliers = 24

(* The high bits of the hash that give a slot. *)
let name_hash_shift = 63 - 18

(* More names than this are held in a map (the table has four to eight slots for each name). *)
let max_table_names = 1 lsl 15

(* A name_map finds a property name among a set of names, through a hash table from a name's key to its index, with
   open addressing. A key is what decides whether a name is a given one: its length, its word, and for a name longer
   than eight bytes its last eight bytes. That is the whole name up to sixteen bytes, so only a longer name has its
   text compared. The table has at least four slots for each name and its hash is the one of a few that leaves the
   fewest names away from their first slot (for most sets, none), so a search is one multiplication and one read to
   the index, then the comparison of the key. *)
type name_map = {
  names : string array;
  (* The key of each name: its word (see name_word), its tail and its length. *)
  words : int array;
  tails : int array;
  key_lengths : int array;
  (* The index + 1 of the name in each slot (0: empty). The length is a power of two. *)
  table : int array;
  (* The multiplier of the hash. A slot is the high bits of the product, cut to the table's length. *)
  mul : int;
  (* A set too large for the table (which no schema in practice has) is a map instead. *)
  is_large : bool;
  large : (string, int) Hashtbl.t;
}

(* hash is the hash of a key. *)
let[@inline] hash mul word tail length = ((word lxor ((tail lsl 29) lor (tail lsr 34))) + length) * mul

(* fill puts the names in a table under a hash, each in the first free slot from its own. It returns how many are
   not in their own slot. *)
let fill names words tails lengths mul table =
  let mask = Array.length table - 1 in
  let moved = ref 0 in
  Array.iteri
    (fun i name ->
      let home = hash mul words.(i) tails.(i) lengths.(i) lsr name_hash_shift in
      let slot = ref home in
      let duplicate = ref false in
      while (not !duplicate) && table.(!slot land mask) <> 0 do
        (* The first of equal names keeps the slot (a set built from a schema has no equal names). *)
        if String.equal names.(table.(!slot land mask) - 1) name then duplicate := true else incr slot
      done;
      if not !duplicate then begin
        table.(!slot land mask) <- i + 1;
        if !slot <> home then incr moved
      end)
    names;
  !moved

let new_name_map names =
  let count = Array.length names in
  let bytes = Array.map Bytes.of_string names in
  let key_lengths = Array.map String.length names in
  let words = Array.mapi (fun i b -> name_word b 0 key_lengths.(i)) bytes in
  let tails = Array.mapi (fun i b -> if key_lengths.(i) > 8 then tail_word b 0 key_lengths.(i) else 0) bytes in
  if count > max_table_names then begin
    let large = Hashtbl.create count in
    for i = count - 1 downto 0 do
      Hashtbl.replace large names.(i) i
    done;
    { names; words; tails; key_lengths; table = [| 0 |]; mul = name_hash_multiplier; is_large = true; large }
  end
  else begin
    let size = ref 4 in
    while !size < 4 * count do
      size := !size lsl 1
    done;
    (* The multiplier that leaves the fewest names away from their first slot. *)
    let best = ref name_hash_multiplier and best_moved = ref (-1) in
    let table = Array.make !size 0 in
    let mul = ref name_hash_multiplier in
    let filled = ref 0 in
    let attempt = ref 0 in
    while !attempt < name_hash_multipliers do
      Array.fill table 0 !size 0;
      filled := !mul;
      let moved = fill names words tails key_lengths !mul table in
      if !best_moved < 0 || moved < !best_moved then begin
        best := !mul;
        best_moved := moved
      end;
      if moved = 0 then attempt := name_hash_multipliers
      else begin
        (* The next odd multiplier (a step of an xorshift generator). *)
        mul := !mul lxor (!mul lsl 13);
        mul := !mul lxor (!mul lsr 7);
        mul := !mul lxor (!mul lsl 17);
        mul := !mul lor 1;
        incr attempt
      end
    done;
    if !filled <> !best then begin
      Array.fill table 0 !size 0;
      ignore (fill names words tails key_lengths !best table : int)
    end;
    { names; words; tails; key_lengths; table; mul = !best; is_large = false; large = Hashtbl.create 1 }
  end

(* rest reports whether name i is the given name, which is longer than eight bytes, when their lengths and words are
   equal. *)
let rest m i b off len =
  tail_word b off len = m.tails.(i) && (len <= 16 || Document.bytes_equal_string b off len m.names.(i))

(* equal reports whether name i is the given name, whose word is w. *)
let equal m i b off len w = m.key_lengths.(i) = len && m.words.(i) = w && (len <= 8 || rest m i b off len)

(* find_key is the index of the name with a key, or -1, for a name of at most sixteen bytes (which its key
   decides). *)
let find_key m w tail length =
  let table = m.table in
  let mask = Array.length table - 1 in
  let slot = ref (hash m.mul w tail length lsr name_hash_shift) in
  let result = ref (-2) in
  while !result = -2 do
    let at = table.(!slot land mask) in
    if at = 0 then result := -1
    else if m.words.(at - 1) = w && m.tails.(at - 1) = tail && m.key_lengths.(at - 1) = length then result := at - 1
    else incr slot
  done;
  !result

(* find_long is find for a name longer than sixteen bytes, whose text is compared as well, and for a set held in a
   map (where the lookup copies the name). *)
let find_long m b off len w =
  if m.is_large then match Hashtbl.find_opt m.large (Bytes.sub_string b off len) with Some i -> i | None -> -1
  else begin
    let tail = if len > 8 then tail_word b off len else 0 in
    let table = m.table in
    let mask = Array.length table - 1 in
    let slot = ref (hash m.mul w tail len lsr name_hash_shift) in
    let result = ref (-2) in
    while !result = -2 do
      let at = table.(!slot land mask) in
      if at = 0 then result := -1
      else if
        m.words.(at - 1) = w
        && m.tails.(at - 1) = tail
        && m.key_lengths.(at - 1) = len
        && Document.bytes_equal_string b off len m.names.(at - 1)
      then result := at - 1
      else incr slot
    done;
    !result
  end

(* map_find is the index of a name (whose word is w) in a name_map, or -1. *)
let map_find m b off len w =
  if len > 16 || m.is_large then find_long m b off len w
  else find_key m w (if len > 8 then tail_word b off len else 0) len

let no_name = -1

(* Declared property names, mapped to their index. *)
type t = {
  (* Bit n set: some name has length n (lengths of 62 and more share bit 62). A name whose length is not in the set
     is not declared, which settles most misses without a search. *)
  lengths : int;
  m : name_map;
  (* For a hint h (the index after the previous match), the name after that match in sorted order (entry 0: the
     first name in sorted order). no_name after the last. *)
  sorted_next : int array;
}

let[@inline] length_bit length = 1 lsl if length > 62 then 62 else length

let create list =
  let m = new_name_map list in
  let lengths = Array.fold_left (fun bits n -> bits lor length_bit (String.length n)) 0 list in
  let order = Array.init (Array.length list) (fun i -> i) in
  (* A stable sort, by name. *)
  let order = Array.of_list (List.stable_sort (fun a b -> String.compare list.(a) list.(b)) (Array.to_list order)) in
  let sorted_next = Array.make (Array.length list + 1) no_name in
  Array.iteri (fun i at -> if i = 0 then sorted_next.(0) <- at else sorted_next.(order.(i - 1) + 1) <- at) order;
  { lengths; m; sorted_next }

let length ns = Array.length ns.m.names
let name ns i = ns.m.names.(i)

(* word is the word of a name (see name_word). *)
let word ns i = ns.m.words.(i)

(* find is the index of a name, without the ordering hint, or -1. *)
let find ns b off len =
  if ns.lengths land length_bit len = 0 then -1
  else begin
    let m = ns.m in
    let w = name_word b off len in
    if len > 16 || m.is_large then find_long m b off len w
    else find_key m w (if len > 8 then tail_word b off len else 0) len
  end

(* find_string is find for a name held as a string. *)
let find_string ns (name : string) = find ns (Bytes.of_string name) 0 (String.length name)

(* at reports whether the name at a hint (the index after the previous match) has the given length and word. For a
   name of at most eight bytes that is the name. *)
let[@inline] at ns hint length w =
  let m = ns.m in
  hint >= 0 && hint < Array.length m.key_lengths && m.key_lengths.(hint) = length && m.words.(hint) = w

(* find_after_long is find_after for a name longer than sixteen bytes, whose text is compared, and for a set held
   in a map. *)
let find_after_long ns b off len w hint =
  let m = ns.m in
  let next = if hint < Array.length ns.sorted_next then ns.sorted_next.(hint) else no_name in
  if next <> no_name && equal m next b off len w then next else find_long m b off len w

(* find_after is find_from for a name (whose word is w) that is not the one at the hint. It tries the name after the
   previous match in sorted order (instances written by tools that sort their keys), then searches the table. *)
let find_after ns b off len w hint =
  if ns.lengths land length_bit len = 0 then -1
  else begin
    let m = ns.m in
    if len > 16 || m.is_large then find_after_long ns b off len w hint
    else begin
      let tail = if len > 8 then tail_word b off len else 0 in
      let next = if hint < Array.length ns.sorted_next then ns.sorted_next.(hint) else no_name in
      if next <> no_name && m.words.(next) = w && m.tails.(next) = tail && m.key_lengths.(next) = len then next
      else find_key m w tail len
    end
  end

(* find_from finds a name, trying the one after the previous match first: instances tend to list their properties in
   the schema's order, so the next name is usually the next one declared. It returns the index, or -1. Go returns
   the hint for the next call as well. Here the caller works it out, since a pair would be a block: it is the index
   plus one after a match, and the hint it gave otherwise. *)
let find_from ns b off len hint =
  let w = name_word b off len in
  if at ns hint len w && (len <= 8 || rest ns.m hint b off len) then hint else find_after ns b off len w hint
