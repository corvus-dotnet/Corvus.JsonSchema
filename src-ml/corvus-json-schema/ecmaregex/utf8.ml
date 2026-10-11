(* What the Go source takes from the unicode/utf8 package, over a text that is a range of a [Bytes.t] or of a
   [string]. The Object Pascal port has the same functions in Corvus.JsonSchema.EcmaRegex.Utf8.

   A decoded character is returned as one [int], the code point above its width in bytes ([(r lsl 3) lor w]), because
   a pair would be allocated. {!rune} and {!width} take it apart. *)

type text = { mutable b : Bytes.t; mutable s : string; mutable is_string : bool }
(** The text of a match. Exactly one of [b] and [s] is in use, and [is_string] says which. One record serves both kinds
    of text, so the matcher is written once and reads a byte through one branch that the processor predicts. *)

(** U+FFFD, which a malformed byte of the text decodes to. *)
let rune_error = 0xFFFD

(** Bytes below this are one character each. *)
let rune_self = 0x80

let new_text () = { b = Bytes.empty; s = ""; is_string = true }
let byte t i = if t.is_string then Char.code (String.get t.s i) else Char.code (Bytes.get t.b i)
let rune packed = packed lsr 3
let width packed = packed land 7
let error1 = (rune_error lsl 3) lor 1

(** [decode t at limit] reads the character at [at] of the text, which ends before [limit]. [at] is below [limit].
    Malformed UTF-8 is U+FFFD with a width of one byte, as utf8.DecodeRune of Go reads it: an encoding that is too long,
    a surrogate and a value beyond U+10FFFF are all malformed. *)
let decode t at limit =
  let p0 = byte t at in
  if p0 < 0x80 then (p0 lsl 3) lor 1
  else if p0 < 0xC2 || p0 > 0xF4 then error1
  else
    let n = limit - at in
    if p0 < 0xE0 then
      if n < 2 then error1
      else
        let b1 = byte t (at + 1) in
        if b1 < 0x80 || b1 > 0xBF then error1 else ((((p0 land 0x1F) lsl 6) lor (b1 land 0x3F)) lsl 3) lor 2
    else if p0 < 0xF0 then
      if n < 3 then error1
      else
        let b1 = byte t (at + 1) in
        (* E0 must not start an encoding that is too long, and ED must not start a surrogate. *)
        if (b1 < if p0 = 0xE0 then 0xA0 else 0x80) || b1 > if p0 = 0xED then 0x9F else 0xBF then error1
        else
          let b2 = byte t (at + 2) in
          if b2 < 0x80 || b2 > 0xBF then error1
          else ((((p0 land 0x0F) lsl 12) lor ((b1 land 0x3F) lsl 6) lor (b2 land 0x3F)) lsl 3) lor 3
    else if n < 4 then error1
    else
      let b1 = byte t (at + 1) in
      (* F0 must not start an encoding that is too long, and F4 must not pass U+10FFFF. *)
      if (b1 < if p0 = 0xF0 then 0x90 else 0x80) || b1 > if p0 = 0xF4 then 0x8F else 0xBF then error1
      else
        let b2 = byte t (at + 2) in
        if b2 < 0x80 || b2 > 0xBF then error1
        else
          let b3 = byte t (at + 3) in
          if b3 < 0x80 || b3 > 0xBF then error1
          else
            ((((p0 land 0x07) lsl 18) lor ((b1 land 0x3F) lsl 12) lor ((b2 land 0x3F) lsl 6) lor (b3 land 0x3F)) lsl 3)
            lor 4

(* The first position at or above [lim], going down from [start], that holds a byte that can start a character. *)
let rec rune_start t start lim =
  if start < lim then lim else if byte t start land 0xC0 <> 0x80 then start else rune_start t (start - 1) lim

(** [decode_last t lo limit] reads the character that ends before [limit] in the text that starts at [lo]. [limit] is
    above [lo]. It is utf8.DecodeLastRune of Go. *)
let decode_last t lo limit =
  let r = byte t (limit - 1) in
  if r < 0x80 then (r lsl 3) lor 1
  else
    let lim = if limit - 4 > lo then limit - 4 else lo in
    let start = rune_start t (limit - 2) lim in
    let packed = decode t start limit in
    if start + width packed <> limit then error1 else packed

(* Matching by UTF-16 code unit over UTF-8 text (units.go of the Go package).

   ECMA-262 matches a pattern that has no u flag a code unit at a time. The engine reads a pattern without the u flag
   only when the u flag grammar refuses it, and such a pattern is matched that way. A character of one, two or three
   bytes of UTF-8 is one code unit. A character of four bytes is two, a high surrogate and a low one, so the matcher
   has to be able to stand between them. That position is written as the index of the character's third byte. Each
   half then spans two bytes, every position is still an index into the text, a group is still a pair of indexes, and
   no UTF-16 copy of the text is made.

   The third byte of a character is a continuation byte, which is no position at all when matching by code point, so
   the two readings cannot be confused. Only a text with a character of four bytes differs between the two, and the
   instructions that compare bytes (a literal run, a word boundary, a line terminator) need no change: none of them
   can hold at the position between the halves, and all of them are right at every other. *)

(** The two UTF-16 code units of a code point beyond the Basic Multilingual Plane. *)
let high_surrogate r = 0xD800 + ((r - 0x10000) lsr 10)

let low_surrogate r = 0xDC00 + ((r - 0x10000) land 0x3FF)

(** [unit_at t lo at hi] reads the code unit at [at] of the text, which is the bytes [lo] up to [hi]. [at] is below
    [hi]. A half of a character of four bytes has a width of two. *)
let unit_at t lo at hi =
  let c = byte t at in
  if c >= 0xF0 then
    let packed = decode t at hi in
    if width packed = 4 then (high_surrogate (rune packed) lsl 3) lor 2 else error1
  else if c >= 0x80 && c < 0xC0 && at - lo >= 2 && byte t (at - 2) >= 0xF0 then
    (* Between the halves of the character that starts two bytes back. *)
    let packed = decode t (at - 2) hi in
    if width packed = 4 then (low_surrogate (rune packed) lsl 3) lor 2 else decode t at hi
  else decode t at hi

(** [unit_before t lo at hi] reads the code unit that ends at [at] of the text, which is the bytes [lo] up to [hi]. [at]
    is above [lo]. *)
let unit_before t lo at hi =
  let packed = decode_last t lo at in
  if width packed = 4 then (low_surrogate (rune packed) lsl 3) lor 2
  else if packed = error1 && at - lo >= 2 && hi - at >= 2 && byte t (at - 2) >= 0xF0 then
    (* Between the halves of the character that starts two bytes back. *)
    let whole = decode t (at - 2) hi in
    if width whole = 4 then (high_surrogate (rune whole) lsl 3) lor 2 else packed
  else packed

(** Reports whether a code point has an encoding: it is in range and not a surrogate. *)
let valid_rune r = (r >= 0 && r < 0xD800) || (r >= 0xE000 && r <= 0x10FFFF)

(** The number of bytes of the UTF-8 encoding of a code point that has one. *)
let rune_length r = if r < 0x80 then 1 else if r < 0x800 then 2 else if r < 0x10000 then 3 else 4

(** Appends the UTF-8 encoding of a code point. One that has none is written as U+FFFD, as utf8.AppendRune of Go does.
*)
let add_rune buffer r =
  let r = if valid_rune r then r else rune_error in
  let add c = Buffer.add_char buffer (Char.chr c) in
  if r < 0x80 then add r
  else if r < 0x800 then begin
    add (0xC0 lor (r lsr 6));
    add (0x80 lor (r land 0x3F))
  end
  else if r < 0x10000 then begin
    add (0xE0 lor (r lsr 12));
    add (0x80 lor ((r lsr 6) land 0x3F));
    add (0x80 lor (r land 0x3F))
  end
  else begin
    add (0xF0 lor (r lsr 18));
    add (0x80 lor ((r lsr 12) land 0x3F));
    add (0x80 lor ((r lsr 6) land 0x3F));
    add (0x80 lor (r land 0x3F))
  end

(** [runes s units] is the code points of a string, with U+FFFD for each malformed byte. It is the conversion to a slice
    of runes of Go. With [units] it is the UTF-16 code units of the string: a character beyond the Basic Multilingual
    Plane is its two surrogates. (Two units are no more elements than the four bytes of the character.) *)
let runes s units =
  let t = { b = Bytes.empty; s; is_string = true } in
  let n = String.length s in
  let out = Array.make n 0 in
  let count = ref 0 and at = ref 0 in
  while !at < n do
    let packed = decode t !at n in
    let r = rune packed in
    if units && r > 0xFFFF then begin
      out.(!count) <- high_surrogate r;
      out.(!count + 1) <- low_surrogate r;
      count := !count + 2
    end
    else begin
      out.(!count) <- r;
      incr count
    end;
    at := !at + width packed
  done;
  Array.sub out 0 !count
