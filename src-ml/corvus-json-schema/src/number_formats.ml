(* The numeric format assertions (a Corvus extension): checkNumber of formats.go of the Go module. It is apart from
   formats.ml because it reads a number of a document.

   Nothing here allocates: a float is read from the document's floats where it is compared, and the bounds are
   constants. *)

open Document

(* integral reports a number of the float class that is whole and within the bounds. *)
let integral d n (lo : float) (hi : float) =
  let f = Float.Array.get (floats d) (data d n) in
  f = Float.floor f && f >= lo && f <= hi

(* int_range reports a whole number within bounds that an [int] holds. *)
let int_range d n lo hi (float_lo : float) (float_hi : float) =
  let c = Numbers.class_of (flags d n) in
  if c = Numbers.num_int then begin
    let i = data d n in
    i >= lo && i <= hi
  end
  else if c = Numbers.num_big then false
  else integral d n float_lo float_hi

(* magnitude reports a number whose size is at most the limit. *)
let magnitude d n (limit : float) =
  let flags = flags d n and data = data d n in
  let c = Numbers.class_of flags in
  if c = Numbers.num_float then Float.abs (Float.Array.get (floats d) data) <= limit
  else if c = Numbers.num_int then Float.abs (Float.of_int data) <= limit
  else Float.abs ((Float.of_int (Numbers.top_of flags - 2) *. Numbers.two62) +. Float.of_int data) <= limit

(* check asserts a numeric format of a number value. *)
let check (kind : Formats.kind) d n =
  let flags = flags d n in
  let c = Numbers.class_of flags in
  let top = Numbers.top_of flags in
  if kind = Formats.byte then int_range d n 0 255 0. 255.
  else if kind = Formats.uint16 then int_range d n 0 65535 0. 65535.
  else if kind = Formats.uint32 then int_range d n 0 4294967295 0. 4294967295.
  else if kind = Formats.uint64 then
    (* An integer of the big class is below 2^64, and positive when its top is 3 or more. *)
    if c = Numbers.num_int then data d n >= 0
    else if c = Numbers.num_big then top >= 3
    else integral d n 0. 18446744073709551615.
  else if kind = Formats.uint128 then
    if c = Numbers.num_int then data d n >= 0
    else if c = Numbers.num_big then top >= 3
    else integral d n 0. 3.402823669209385e38
  else if kind = Formats.sbyte then int_range d n (-128) 127 (-128.) 127.
  else if kind = Formats.int16 then int_range d n (-32768) 32767 (-32768.) 32767.
  else if kind = Formats.int32 then int_range d n (-2147483648) 2147483647 (-2147483648.) 2147483647.
  else if kind = Formats.int64 then
    (* An int64 is an [int], or an integer of the big class below 2^63 (its top is 0 or 3). *)
    if c = Numbers.num_int then true
    else if c = Numbers.num_big then top <= 3
    else integral d n (-9223372036854775808.) 9223372036854775807.
  else if kind = Formats.int128 then
    c <> Numbers.num_float || integral d n (-1.7014118346046923e38) 1.7014118346046923e38
  else if kind = Formats.half then magnitude d n 65504.
  else if kind = Formats.single then magnitude d n 3.4028234663852886e38
  else if kind = Formats.double then magnitude d n Float.max_float
  else if kind = Formats.decimal then magnitude d n 7.922816251426434e28
  else true
