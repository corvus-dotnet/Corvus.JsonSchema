(* A port of ucd.go of the Go module. See the interface for what differs. *)

type table = Tables.table = { r16 : string; r32 : string }

let version = Tables.version
let max_rune = 0x10FFFF

(* Bound [i] of the ranges that end at or below U+FFFF: two bytes, the low byte first. *)
let get16 s i = Char.code (String.get s (2 * i)) lor (Char.code (String.get s ((2 * i) + 1)) lsl 8)

(* Bound [i] of the ranges that start above U+FFFF: three bytes, the low byte first. *)
let get24 s i =
  Char.code (String.get s (3 * i))
  lor (Char.code (String.get s ((3 * i) + 1)) lsl 8)
  lor (Char.code (String.get s ((3 * i) + 2)) lsl 16)

let rec search16 ranges c lo hi =
  if lo >= hi then false
  else
    let mid = (lo + hi) lsr 1 in
    if c > get16 ranges ((2 * mid) + 1) then search16 ranges c (mid + 1) hi
    else if c < get16 ranges (2 * mid) then search16 ranges c lo mid
    else true

let rec search24 ranges c lo hi =
  if lo >= hi then false
  else
    let mid = (lo + hi) lsr 1 in
    if c > get24 ranges ((2 * mid) + 1) then search24 ranges c (mid + 1) hi
    else if c < get24 ranges (2 * mid) then search24 ranges c lo mid
    else true

let contains t r =
  if r < 0 then false
  else if r <= 0xFFFF then search16 t.r16 r 0 (String.length t.r16 / 4)
  else search24 t.r32 r 0 (String.length t.r32 / 6)

let bounds16 t = Array.init (String.length t.r16 / 2) (get16 t.r16)
let bounds32 t = Array.init (String.length t.r32 / 3) (get24 t.r32)

let ranges t =
  let n16 = String.length t.r16 / 2 and n32 = String.length t.r32 / 3 in
  (* A range that crosses U+FFFF is stored in two parts. *)
  let joined = n16 > 0 && n32 > 0 && get16 t.r16 (n16 - 1) = 0xFFFF && get24 t.r32 0 = 0x10000 in
  let dst = Array.make (if joined then n16 + n32 - 2 else n16 + n32) 0 in
  for i = 0 to n16 - 1 do
    dst.(i) <- get16 t.r16 i
  done;
  if joined then begin
    dst.(n16 - 1) <- get24 t.r32 1;
    for i = 2 to n32 - 1 do
      dst.(n16 + i - 2) <- get24 t.r32 i
    done
  end
  else
    for i = 0 to n32 - 1 do
      dst.(n16 + i) <- get24 t.r32 i
    done;
  dst

(* Builds a table from inclusive pairs in ascending order that neither overlap nor touch. *)
let new_table pairs =
  let r16 = Buffer.create 64 and r32 = Buffer.create 64 in
  let add16 c =
    Buffer.add_char r16 (Char.chr (c land 0xFF));
    Buffer.add_char r16 (Char.chr (c lsr 8))
  and add24 c =
    Buffer.add_char r32 (Char.chr (c land 0xFF));
    Buffer.add_char r32 (Char.chr ((c lsr 8) land 0xFF));
    Buffer.add_char r32 (Char.chr (c lsr 16))
  in
  let i = ref 0 in
  while !i < Array.length pairs do
    let lo = pairs.(!i) and hi = pairs.(!i + 1) in
    if hi <= 0xFFFF then begin
      add16 lo;
      add16 hi
    end
    else if lo > 0xFFFF then begin
      add24 lo;
      add24 hi
    end
    else begin
      add16 lo;
      add16 0xFFFF;
      add24 0x10000;
      add24 hi
    end;
    i := !i + 2
  done;
  { r16 = Buffer.contents r16; r32 = Buffer.contents r32 }

let union tables =
  let all = Array.concat (List.map ranges tables) in
  let n = Array.length all / 2 in
  (* The ranges are sorted by their first code point. A range is one number here, its first code point above its last,
     so that sorting the numbers sorts the ranges. A code point has 21 bits, so the number fits an int of 63 bits. *)
  let order = Array.init n (fun i -> (all.(2 * i) lsl 32) lor all.((2 * i) + 1)) in
  Array.sort compare order;
  let merged = Array.make (Array.length all) 0 in
  let m = ref 0 in
  Array.iter
    (fun pair ->
      let lo = pair lsr 32 and hi = pair land 0xFFFFFFFF in
      if !m > 0 && lo <= merged.(!m - 1) + 1 then
        begin if hi > merged.(!m - 1) then merged.(!m - 1) <- hi
        end
      else begin
        merged.(!m) <- lo;
        merged.(!m + 1) <- hi;
        m := !m + 2
      end)
    order;
  new_table (Array.sub merged 0 !m)

(* Builds the table of the code points that the table does not hold. *)
let complement t =
  let pairs = ranges t in
  let inverse = Array.make (Array.length pairs + 2) 0 in
  let n = ref 0 and next = ref 0 and i = ref 0 in
  while !i < Array.length pairs do
    if pairs.(!i) > !next then begin
      inverse.(!n) <- !next;
      inverse.(!n + 1) <- pairs.(!i) - 1;
      n := !n + 2
    end;
    next := pairs.(!i + 1) + 1;
    i := !i + 2
  done;
  if !next <= max_rune then begin
    inverse.(!n) <- !next;
    inverse.(!n + 1) <- max_rune;
    n := !n + 2
  end;
  new_table (Array.sub inverse 0 !n)

let letter = Tables.(union [ gc_lu; gc_ll; gc_lt; gc_lm; gc_lo ])
let cased_letter = Tables.(union [ gc_lu; gc_ll; gc_lt ])
let mark = Tables.(union [ gc_mn; gc_mc; gc_me ])
let number = Tables.(union [ gc_nd; gc_nl; gc_no ])
let punctuation = Tables.(union [ gc_pc; gc_pd; gc_ps; gc_pe; gc_pi; gc_pf; gc_po ])
let symbol = Tables.(union [ gc_sm; gc_sc; gc_sk; gc_so ])
let separator = Tables.(union [ gc_zs; gc_zl; gc_zp ])
let other = Tables.(union [ gc_cc; gc_cf; gc_cs; gc_co; gc_cn ])
let assigned = complement Tables.gc_cn
let unknown_script = Tables.(union [ gc_cn; gc_co; gc_cs ])
let some_letter = Some letter
let some_cased_letter = Some cased_letter
let some_mark = Some mark
let some_number = Some number
let some_punctuation = Some punctuation
let some_symbol = Some symbol
let some_separator = Some separator
let some_other = Some other
let some_assigned = Some assigned
let some_unknown_script = Some unknown_script

let category = function
  | "L" -> some_letter
  | "LC" -> some_cased_letter
  | "M" -> some_mark
  | "N" -> some_number
  | "P" -> some_punctuation
  | "S" -> some_symbol
  | "Z" -> some_separator
  | "C" -> some_other
  | name -> Tables.category_table name

let binary = function "Assigned" -> some_assigned | name -> Tables.binary_table name
let script = function "Unknown" | "Zzzz" -> some_unknown_script | name -> Tables.script_table name

let script_extensions = function
  | "Unknown" | "Zzzz" -> some_unknown_script
  | name -> Tables.script_extensions_table name

let script_names = Tables.script_names
let binary_names = Tables.binary_names

(* A case table holds rows of four elements. A row maps some of the code points first, first+1, ... first+length-1 to
   themselves plus delta. The ones it maps are those whose offset from first has none of the bits of mask, so a mask
   of 1 takes every other code point. *)
let rec map_case table r lo hi =
  if lo >= hi then r
  else
    let mid = (lo + hi) lsr 1 in
    let first = table.(4 * mid) in
    if r < first then map_case table r lo mid
    else
      let offset = r - first in
      if offset >= table.((4 * mid) + 2) then map_case table r (mid + 1) hi
      else if offset land table.((4 * mid) + 3) = 0 then r + table.((4 * mid) + 1)
      else r

let fold_by_table r = map_case Tables.case_folds r 0 (Array.length Tables.case_folds / 4)
let to_upper_by_table r = map_case Tables.upper_cases r 0 (Array.length Tables.upper_cases / 4)
let fold r = if r < 0x80 then if r >= 0x41 && r <= 0x5A then r + 0x20 else r else fold_by_table r
let to_upper r = if r < 0x80 then if r >= 0x61 && r <= 0x7A then r - 0x20 else r else to_upper_by_table r

let folding () =
  let table = Tables.case_folds in
  let count = ref 0 in
  let each f =
    for i = 0 to (Array.length table / 4) - 1 do
      if table.((4 * i) + 1) <> 0 then
        for offset = 0 to table.((4 * i) + 2) - 1 do
          if offset land table.((4 * i) + 3) = 0 then f (table.(4 * i) + offset)
        done
    done
  in
  each (fun _ -> incr count);
  let dst = Array.make !count 0 in
  let n = ref 0 in
  each (fun r ->
      dst.(!n) <- r;
      incr n);
  dst
