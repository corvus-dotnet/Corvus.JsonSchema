(* The mapping step of UTS #46 (Unicode IDNA Compatibility Processing, section 4 step 1), which the idn-hostname and
   idn-email formats apply to a name before they check it.

   An "ignored" code point is removed and a "mapped" one is replaced by its mapping. Every other code point is kept:
   "valid" and "deviation" ones (nontransitional processing, so ZERO WIDTH JOINER and ZERO WIDTH NON-JOINER stay and
   meet the contextual rules of RFC 5892), and "disallowed" ones, which the IDNA2008 checks that follow refuse. ASCII
   is kept as it is: its mapping only lowercases the letters, which does not change whether a name is valid. The
   normalization step (step 2) is not applied: a name that is not in Normalization Form C is valid as a host name, as
   the JSON-Schema-Test-Suite expects. The table (uts46_data.ml) is the one the .NET engine and the Rust crate use. *)

let byte s i = Char.code (String.unsafe_get s i)

(* find gives the offset and length of the mapping of a code point outside ASCII in the pool (a length of 0 when it is
   ignored), packed as offset * 256 + length, or -1 when the code point is neither ignored nor mapped. *)
let find cp =
  let entries = Uts46_data.entries in
  let low = ref 0 and high = ref Uts46_data.count and result = ref (-1) in
  while !low < !high do
    let middle = (!low + !high) / 2 in
    let e = middle * 8 in
    let start = (byte entries e lsl 16) lor (byte entries (e + 1) lsl 8) lor byte entries (e + 2) in
    if cp < start then high := middle
    else
      let stop = start + ((byte entries (e + 3) lsl 8) lor byte entries (e + 4)) in
      if cp > stop then low := middle + 1
      else begin
        result := (((byte entries (e + 5) lsl 8) lor byte entries (e + 6)) lsl 8) lor byte entries (e + 7);
        low := !high
      end
  done;
  !result

(* map gives the mapped form of the UTF-8 text b[from .. stop - 1], whose code points decode reads (as a code point and
   a size in bytes, packed as Formats does), as a new buffer, or None when no code point of it is ignored or mapped. *)
let map ~decode ~rune_of ~size_of b from stop =
  let changes = ref false in
  let i = ref from in
  while (not !changes) && !i < stop do
    let d = decode b !i stop in
    if rune_of d >= 0x80 && find (rune_of d) >= 0 then changes := true;
    i := !i + size_of d
  done;
  if not !changes then None
  else begin
    let out = Buffer.create (stop - from) in
    let i = ref from in
    while !i < stop do
      let d = decode b !i stop in
      let found = if rune_of d >= 0x80 then find (rune_of d) else -1 in
      (* An ignored code point (a length of 0, and an offset of 0xFFFF) adds nothing. *)
      if found >= 0 then (
        if found land 0xff > 0 then Buffer.add_substring out Uts46_data.pool (found lsr 8) (found land 0xff))
      else Buffer.add_subbytes out b !i (size_of d);
      i := !i + size_of d
    done;
    Some (Buffer.to_bytes out)
  end
