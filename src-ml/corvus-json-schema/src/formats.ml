(* Format assertions, applied only when format is asserted (the format-assertion vocabulary or the assert format
   option). They match the C# evaluator's format checks.

   Ported from formats.go. The checks read UTF-8 bytes in place: a text is the bytes [from .. stop-1] of a larger
   buffer, and a check passes positions on and never a copy of part of the text. Where the Go module uses its
   linear-time regular expressions (URI, IRI, URI template, e-mail local part), this module reads the same grammars
   directly, as the Object Pascal and Julia ports do, so no check here needs the pattern engine but the regex format.

   No check allocates but the regex format, the internationalized host name (and so the internationalized e-mail
   address), and a host name with a label that starts with "xn--" (and so an e-mail address whose domain has one).
   That shapes the code: a function that would give two results in the Go source gives them packed in one int, a
   closure of the Go source is a function of its own that takes what the closure captured, and a loop that would leave
   early runs on a flag. *)

module Ucd = Corvus_json_schema_ucd
module Ecmaregex = Corvus_json_schema_ecmaregex

(* kind is the format a dialect recognises for a format value. *)
type kind = int

let unknown = 0
let date = 1
let time = 2
let date_time = 3
let duration = 4
let uuid = 5
let ipv4 = 6
let ipv6 = 7
let hostname = 8
let idn_hostname = 9
let email = 10
let idn_email = 11
let uri = 12
let uri_reference = 13
let iri = 14
let iri_reference = 15
let uri_template = 16
let json_pointer = 17
let relative_json_pointer = 18
let regex = 19

(* Numeric formats (a Corvus extension). *)
let byte = 20
let uint16 = 21
let uint32 = 22
let uint64 = 23
let uint128 = 24
let sbyte = 25
let int16 = 26
let int32 = 27
let int64 = 28
let int128 = 29
let half = 30
let single = 31
let double = 32
let decimal = 33

(* The dialects as kind_of takes them: the rank of the evaluator's dialect, in specification order. *)
let draft6 = 1
let draft7 = 2
let draft2019_09 = 3
let draft2020_12 = 4
let at_least dialect d k = if dialect >= d then k else unknown

let kind_of format dialect =
  match format with
  | "float" | "single" -> single
  | "byte" -> byte
  | "uint16" -> uint16
  | "uint32" -> uint32
  | "uint64" -> uint64
  | "uint128" -> uint128
  | "sbyte" -> sbyte
  | "int16" -> int16
  | "int32" -> int32
  | "int64" -> int64
  | "int128" -> int128
  | "half" -> half
  | "double" -> double
  | "decimal" -> decimal
  | "date-time" -> date_time
  | "email" -> email
  | "hostname" -> hostname
  | "ipv4" -> ipv4
  | "ipv6" -> ipv6
  | "uri" -> uri
  | "uri-reference" -> at_least dialect draft6 uri_reference
  | "uri-template" -> at_least dialect draft6 uri_template
  | "json-pointer" -> at_least dialect draft6 json_pointer
  | "date" -> at_least dialect draft7 date
  | "time" -> at_least dialect draft7 time
  | "regex" -> at_least dialect draft7 regex
  | "relative-json-pointer" -> at_least dialect draft7 relative_json_pointer
  | "idn-email" -> at_least dialect draft7 idn_email
  | "idn-hostname" -> at_least dialect draft7 idn_hostname
  | "iri" -> at_least dialect draft7 iri
  | "iri-reference" -> at_least dialect draft7 iri_reference
  | "duration" -> at_least dialect draft2019_09 duration
  | "uuid" -> at_least dialect draft2019_09 uuid
  | _ -> unknown

let is_numeric k = k >= byte

let format_names =
  [|
    "unknown";
    "date";
    "time";
    "date-time";
    "duration";
    "uuid";
    "ipv4";
    "ipv6";
    "hostname";
    "idn-hostname";
    "email";
    "idn-email";
    "uri";
    "uri-reference";
    "iri";
    "iri-reference";
    "uri-template";
    "json-pointer";
    "relative-json-pointer";
    "regex";
    "byte";
    "uint16";
    "uint32";
    "uint64";
    "uint128";
    "sbyte";
    "int16";
    "int32";
    "int64";
    "int128";
    "half";
    "single";
    "double";
    "decimal";
  |]

(* name is the canonical name, for messages. *)
let name k = format_names.(k)

(* message is the message for a string format failure (the C# evaluator's text), or "". A pattern cannot name a
   constant, so the kinds are written here, in check_string and in kind_validator as their numbers. *)
let message k =
  match k with
  | 1 -> "Expected an ISO8601 Date string."
  | 3 -> "Expected an ISO8601 Offset DateTime string."
  | 2 -> "Expected an ISO8601 Offset Time string."
  | 4 -> "Expected an ISO8601 Duration string."
  | 10 -> "Expected an RFC5321 Section-4.1.2 Email string."
  | 11 -> "Expected an RFC6531 IDN Email string."
  | 8 -> "Expected an RFC1035 hostname."
  | 9 -> "Expected an RFC5890 Section-2.3.2.3 IDN hostname."
  | 6 -> "Expected an RFC2673 IP V4 address."
  | 7 -> "Expected an RFC2373 IP V6 address."
  | 12 -> "Expected an absolute URI."
  | 13 -> "Expected a URI reference."
  | 14 -> "Expected an absolute IRI."
  | 15 -> "Expected an IRI reference."
  | 5 -> "Expected an RFC4122 UUID."
  | 16 -> "Expected an RFC6570 URI Template."
  | 17 -> "Expected an RFC6901 JSON Pointer."
  | 18 -> "Expected a Relative JSON Pointer. (https://json-schema.org/draft/2020-12/relative-json-pointer)."
  | 19 -> "Expected a regular expression specification."
  | _ -> ""

(* ------------------------------------------------------------------------------------------------------------------ *)
(* Reading bytes *)

(* rune_self is the first code point that UTF-8 writes in more than one byte. *)
let rune_self = 0x80

(* rune_error is what a byte that is not well-formed UTF-8 reads as. *)
let rune_error = 0xfffd
let max_rune = 0x10ffff

(* The largest 32-bit unsigned integer. The Punycode arithmetic of the Go source is that of uint32, with uint64 for the
   products. Here each value is an int, which has 63 bits, kept within 32 bits where the Go source would wrap or
   saturate. No product of the arithmetic reaches 63 bits: a factor is at most 2^32 and the other is a count of code
   points or a digit. *)
let max_uint32 = 0xffff_ffff

(* at is the byte of the text at a position. *)
let at b i = Char.code (Bytes.get b i)
let is_ascii_letter c = (c >= 0x41 && c <= 0x5a) || (c >= 0x61 && c <= 0x7a)
let is_ascii_digit c = c >= 0x30 && c <= 0x39

(* hex_value is the value of a hex digit, or -1. *)
let hex_value c =
  if c >= 0x30 && c <= 0x39 then c - 0x30
  else if c >= 0x61 && c <= 0x66 then c - 0x61 + 10
  else if c >= 0x41 && c <= 0x46 then c - 0x41 + 10
  else -1

(* index_byte is the index of the first c in b[from .. stop-1], or -1. *)
let index_byte b from stop c =
  let i = ref from in
  while !i < stop && at b !i <> c do
    incr i
  done;
  if !i < stop then !i else -1

(* last_index_byte is the index of the last c in b[from .. stop-1], or -1. *)
let last_index_byte b from stop c =
  let i = ref (stop - 1) in
  while !i >= from && at b !i <> c do
    decr i
  done;
  if !i >= from then !i else -1

(* decode_rune reads the code point at b[i], which is before stop. A byte that does not start well-formed UTF-8 (an
   overlong form, a surrogate, a value above U+10FFFF, a sequence cut short) reads as rune_error with a size of one, as
   it does in the Go source. The Go function gives the code point and its size in bytes. Two results would be a tuple
   on the heap here, so they come packed in one int: the code point in the low 21 bits (rune_of) and the size above
   them (size_of). *)
let decode_rune b i stop =
  let one = (1 lsl 21) lor rune_error in
  let b0 = at b i in
  if b0 < rune_self then (1 lsl 21) lor b0
  else if b0 < 0xc2 || b0 > 0xf4 || i + 1 >= stop then one
  else
    let b1 = at b (i + 1) in
    if b0 < 0xe0 then if b1 < 0x80 || b1 > 0xbf then one else (2 lsl 21) lor ((b0 land 0x1f) lsl 6) lor (b1 land 0x3f)
    else if b0 < 0xf0 then
      let lo = if b0 = 0xe0 then 0xa0 else 0x80 in
      let hi = if b0 = 0xed then 0x9f else 0xbf in
      if b1 < lo || b1 > hi || i + 2 >= stop then one
      else
        let b2 = at b (i + 2) in
        if b2 < 0x80 || b2 > 0xbf then one
        else (3 lsl 21) lor ((b0 land 0x0f) lsl 12) lor ((b1 land 0x3f) lsl 6) lor (b2 land 0x3f)
    else
      let lo = if b0 = 0xf0 then 0x90 else 0x80 in
      let hi = if b0 = 0xf4 then 0x8f else 0xbf in
      if b1 < lo || b1 > hi || i + 3 >= stop then one
      else
        let b2 = at b (i + 2) in
        let b3 = at b (i + 3) in
        if b2 < 0x80 || b2 > 0xbf || b3 < 0x80 || b3 > 0xbf then one
        else
          (4 lsl 21)
          lor ((b0 land 0x07) lsl 18)
          lor ((b1 land 0x3f) lsl 12)
          lor ((b2 land 0x3f) lsl 6)
          lor (b3 land 0x3f)

(* rune_of is the code point of what decode_rune gives. *)
let rune_of d = d land 0x1fffff

(* size_of is the size in bytes of what decode_rune gives. *)
let size_of d = d lsr 21

(* code_points is the code points of UTF-8 text. *)
let code_points b from stop =
  let out = Array.make (stop - from) 0 in
  let count = ref 0 in
  let i = ref from in
  while !i < stop do
    let d = decode_rune b !i stop in
    out.(!count) <- rune_of d;
    incr count;
    i := !i + size_of d
  done;
  if !count = stop - from then out else Array.sub out 0 !count

let is_ascii_range b from stop =
  let i = ref from in
  while !i < stop && at b !i < rune_self do
    incr i
  done;
  !i >= stop

(* ------------------------------------------------------------------------------------------------------------------ *)
(* Dates, times and durations *)

let is_leap_year y = y mod 4 = 0 && (y mod 100 <> 0 || y mod 400 = 0)

(* digits_value is the value of a run of ASCII digits, or -1. *)
let digits_value b from stop =
  if from >= stop then -1
  else begin
    let v = ref 0 in
    let i = ref from in
    while !i < stop && !v >= 0 do
      let c = at b !i in
      if is_ascii_digit c then begin
        v := (!v * 10) + (c - 0x30);
        incr i
      end
      else v := -1
    done;
    !v
  end

let month_days = [| 0; 31; 28; 31; 30; 31; 30; 31; 31; 30; 31; 30; 31 |]

let date_ok b from stop =
  if stop - from <> 10 || at b (from + 4) <> 0x2d || at b (from + 7) <> 0x2d then false
  else
    let y = digits_value b from (from + 4) in
    let m = digits_value b (from + 5) (from + 7) in
    let d = digits_value b (from + 8) (from + 10) in
    if y < 0 || m < 1 || m > 12 || d < 1 then false
    else if m = 2 && is_leap_year y then d <= 29
    else d <= month_days.(m)

(* What time_offset gives for an offset that is not valid. A valid offset is within a day either way. *)
let no_offset = 1 lsl 20

(* time_offset reads the offset that ends a time, from position i: "z" or "Z", or a sign, hours, ":" and minutes. It
   gives the minutes to add to the time to reach UTC (the sign of the Go source times the hours and minutes of the
   offset), or no_offset. *)
let time_offset b i stop =
  let c = at b i in
  if c = 0x7a || c = 0x5a then if i + 1 <> stop then no_offset else 0
  else if c = 0x2b || c = 0x2d then
    if stop - i <> 6 || at b (i + 3) <> 0x3a then no_offset
    else
      let oh = digits_value b (i + 1) (i + 3) in
      let om = digits_value b (i + 4) (i + 6) in
      if oh < 0 || om < 0 || oh > 23 || om > 59 then no_offset
      else
        let sign = if c = 0x2d then 1 else -1 in
        sign * ((oh * 60) + om)
  else no_offset

let time_ok b from stop =
  if stop - from < 9 || at b (from + 2) <> 0x3a || at b (from + 5) <> 0x3a then false
  else
    let h = digits_value b from (from + 2) in
    let mi = digits_value b (from + 3) (from + 5) in
    let sec = digits_value b (from + 6) (from + 8) in
    if h < 0 || mi < 0 || sec < 0 then false
    else begin
      let i = ref (from + 8) in
      let fraction = ref true in
      if at b !i = 0x2e then begin
        incr i;
        let start = !i in
        while !i < stop && is_ascii_digit (at b !i) do
          incr i
        done;
        if !i = start then fraction := false
      end;
      if (not !fraction) || !i >= stop then false
      else
        let offset = time_offset b !i stop in
        if offset = no_offset then false
        else if h > 23 || mi > 59 || sec > 60 then false
        else if sec = 60 then begin
          (* A leap second is only valid at 23:59:60 UTC. *)
          let utc = ((h * 60) + mi + offset) mod 1440 in
          let utc = if utc < 0 then utc + 1440 else utc in
          utc = (23 * 60) + 59
        end
        else true
    end

let date_time_ok b from stop =
  stop - from > 11
  && (at b (from + 10) = 0x54 || at b (from + 10) = 0x74)
  && date_ok b from (from + 10)
  && time_ok b (from + 11) stop

(* duration_part reads digits and one of the designators from position `first` in order, from position i of the text.
   The closure of the Go source moves the position and returns the designator's index. Here both come packed in one
   int, the position after the designator shifted left two bits and the index below it. It is -1 when there is no such
   part. *)
let duration_part b stop i designators first =
  let p = ref i in
  while !p < stop && is_ascii_digit (at b !p) do
    incr p
  done;
  if !p = i || !p >= stop then -1
  else begin
    let c = Bytes.get b !p in
    let found = ref first in
    while !found < String.length designators && String.get designators !found <> c do
      incr found
    done;
    if !found >= String.length designators then -1 else ((!p + 1) lsl 2) lor !found
  end

(* duration_run reads one or more consecutive parts in designator order from position i, and gives the position after
   them, or -1. *)
let duration_run b stop i designators =
  let first = duration_part b stop i designators 0 in
  if first < 0 then -1
  else begin
    let i = ref (first lsr 2) in
    let found = ref (first land 3) in
    let ok = ref true in
    while !ok && !found + 1 < String.length designators && !i < stop && is_ascii_digit (at b !i) do
      let next = duration_part b stop !i designators (!found + 1) in
      if next < 0 || next land 3 <> !found + 1 then ok := false
      else begin
        found := next land 3;
        i := next lsr 2
      end
    done;
    if !ok then !i else -1
  end

(* duration_ok reads an ISO 8601 duration: P then weeks, or date parts in order then an optional time, or a time. *)
let duration_ok b from stop =
  if stop - from < 2 || at b from <> 0x50 then false
  else
    let i = from + 1 in
    if at b i = 0x54 then duration_run b stop (i + 1) "HMS" = stop
    else
      let weeks = duration_part b stop i "W" 0 in
      if weeks >= 0 then weeks lsr 2 = stop
      else
        let i = duration_run b stop i "YMD" in
        if i < 0 then false
        else if i < stop && at b i = 0x54 then duration_run b stop (i + 1) "HMS" = stop
        else i = stop

(* ------------------------------------------------------------------------------------------------------------------ *)
(* UUIDs and IP addresses *)

let is_hex_digit c = hex_value c >= 0

let uuid_ok b from stop =
  if stop - from <> 36 then false
  else begin
    let i = ref 0 in
    let ok = ref true in
    while !ok && !i < 36 do
      let c = at b (from + !i) in
      if !i = 8 || !i = 13 || !i = 18 || !i = 23 then (if c <> 0x2d then ok := false)
      else if not (is_hex_digit c) then ok := false;
      incr i
    done;
    !ok
  end

let ipv4_ok b from stop =
  let parts = ref 0 in
  let p = ref from in
  (* -1 while the parts are being read, then 1 for a valid address and 0 for any other text. *)
  let result = ref (-1) in
  while !result < 0 do
    let dot = index_byte b !p stop 0x2e in
    let part_stop = if dot >= 0 then dot else stop in
    let n = part_stop - !p in
    incr parts;
    if !parts > 4 || n = 0 || n > 3 || (n > 1 && at b !p = 0x30) then result := 0
    else
      let v = digits_value b !p part_stop in
      if v < 0 || v > 255 then result := 0 else if dot < 0 then result := if !parts = 4 then 1 else 0 else p := dot + 1
  done;
  !result = 1

(* The Go source reads an address that ends in an IPv4 address from a buffer on the stack, where it has written the
   address with "0:0" in the place of the IPv4 tail. A buffer here would be on the heap, so the address is read in
   place through ipv6_at: byte i is the byte of the text at from + i for the first `real` bytes, and a byte of "0:0"
   after them. The positions hex_groups and index_double_colon take are positions in the address read that way. *)
let ipv6_at b from real i = if i < real then at b (from + i) else Char.code (String.get "0:0" (i - real))

(* hex_groups is the number of colon-separated groups of one to four hex digits in positions lo .. hi-1 of the
   address, or -1 when one is not valid. *)
let hex_groups b from real lo hi =
  if lo >= hi then 0
  else begin
    let n = ref 0 in
    let p = ref lo in
    (* -2 while the groups are being read. *)
    let result = ref (-2) in
    while !result = -2 do
      let colon = ref !p in
      while !colon < hi && ipv6_at b from real !colon <> 0x3a do
        incr colon
      done;
      let part_stop = !colon in
      if part_stop = !p || part_stop - !p > 4 then result := -1
      else begin
        let i = ref !p in
        while !i < part_stop && is_hex_digit (ipv6_at b from real !i) do
          incr i
        done;
        if !i < part_stop then result := -1
        else begin
          incr n;
          if part_stop >= hi then result := !n else p := part_stop + 1
        end
      end
    done;
    !result
  end

(* index_double_colon is the index of the first "::" in positions lo .. hi-1 of the address, or -1. *)
let index_double_colon b from real lo hi =
  let i = ref lo in
  while !i < hi - 1 && not (ipv6_at b from real !i = 0x3a && ipv6_at b from real (!i + 1) = 0x3a) do
    incr i
  done;
  if !i < hi - 1 then !i else -1

let ipv6_ok b from stop =
  (* A valid address is at most 51 characters (an IPv4 tail after six groups), so longer text fails early. *)
  let n = stop - from in
  if n < 2 || n > 64 then false
  else begin
    let has_dot = ref false in
    let i = ref from in
    let ok = ref true in
    while !ok && !i < stop do
      let c = at b !i in
      if not (is_hex_digit c || c = 0x3a || c = 0x2e) then ok := false;
      if c = 0x2e then has_dot := true;
      incr i
    done;
    if not !ok then false
    else begin
      (* An IPv4 tail counts as two groups: check it, then read the address with "0:0" in its place. *)
      let last_colon = if !has_dot then last_index_byte b from stop 0x3a else -1 in
      if !has_dot && (last_colon < 0 || not (ipv4_ok b (last_colon + 1) stop)) then false
      else
        let real = if !has_dot then last_colon + 1 - from else n in
        let length = if !has_dot then real + 3 else n in
        let dbl = index_double_colon b from real 0 length in
        if dbl >= 0 then
          if index_double_colon b from real (dbl + 1) length >= 0 then false
          else
            let l = hex_groups b from real 0 dbl in
            let r = hex_groups b from real (dbl + 2) length in
            l >= 0 && r >= 0 && l + r < 8
        else hex_groups b from real 0 length = 8
    end
  end

(* ------------------------------------------------------------------------------------------------------------------ *)
(* Host names *)

let is_ascii_alphanumeric c = is_ascii_letter c || is_ascii_digit c

let is_ldh_label b from stop =
  if
    stop - from = 0
    || stop - from > 63
    || (not (is_ascii_alphanumeric (at b from)))
    || not (is_ascii_alphanumeric (at b (stop - 1)))
  then false
  else begin
    let i = ref from in
    while !i < stop && (is_ascii_alphanumeric (at b !i) || at b !i = 0x2d) do
      incr i
    done;
    !i >= stop
  end

let starts_with_xn b from stop =
  stop - from >= 4
  && at b from lor 0x20 = 0x78
  && at b (from + 1) lor 0x20 = 0x6e
  && at b (from + 2) = 0x2d
  && at b (from + 3) = 0x2d

let hostname_label_ok b from stop =
  if not (is_ldh_label b from stop) then false
  else
    (* "--" in the third and fourth positions is reserved for A-labels (xn--). *)
    not (stop - from >= 4 && at b (from + 2) = 0x2d && at b (from + 3) = 0x2d && not (starts_with_xn b from stop))

(* each_label calls ok for each dot-separated label, stopping at the first it refuses. *)
let each_label b from stop (ok : Bytes.t -> int -> int -> bool) =
  let p = ref from in
  (* -1 while the labels are being read. *)
  let result = ref (-1) in
  while !result < 0 do
    let dot = index_byte b !p stop 0x2e in
    if dot < 0 then result := if ok b !p stop then 1 else 0 else if not (ok b !p dot) then result := 0 else p := dot + 1
  done;
  !result = 1

(* legacy_hostname_ok checks RFC 1123 host names (draft 4 and 6): no IDNA rules for "--" or A-labels. *)
let legacy_hostname_ok b from stop = stop - from <> 0 && stop - from <= 253 && each_label b from stop is_ldh_label

(* idn_tables holds the Unicode properties the host name checks read. They are built from the tables of
   Corvus_json_schema_ucd, which hold one fixed version of Unicode (Ucd.version). OCaml's standard library has no
   Unicode properties, so there is no other data a check could read. A format therefore accepts the same strings
   whichever compiler built the program.

   RFC 5892 defines the IDNA2008 code point classes by rules over Unicode properties and not by a list for one
   version of Unicode, so that they extend to each new version. The checks here apply those rules to the properties
   of Ucd.version. *)
type idn_tables = {
  (* disallowed is the general categories that IDNA2008 never makes PVALID: controls, private use, unassigned,
     spaces, uppercase and titlecase letters (mapped away), mathematical and other symbols, and punctuation. *)
  disallowed : Ucd.table;
  (* invisible is the controls, the format characters, the spaces and the unassigned code points. *)
  invisible : Ucd.table;
  (* mark is the group M, nonspacing is Mn, and nonspacing_or_enclosing is Mn and Me, the stand-in for Bidi class
     NSM. *)
  mark : Ucd.table;
  nonspacing : Ucd.table;
  nonspacing_or_enclosing : Ucd.table;
  (* letter_or_spacing_mark is the group L and Mc, the stand-in for Bidi class L. *)
  letter_or_spacing_mark : Ucd.table;
  (* The scripts the contextual rules of RFC 5892 and the Bidi rule of RFC 5893 name. *)
  hebrew : Ucd.table;
  greek : Ucd.table;
  arabic_like : Ucd.table;
  kana_or_han : Ucd.table;
  (* letter_mark_number is the groups L, M and N together, for the local part of an internationalized e-mail
     address. *)
  letter_mark_number : Ucd.table;
}

(* idn is the tables. The Go source builds them when a host name check first needs them. Here the module builds them
   as it is initialised, as the Object Pascal port does, so they are immutable, no check takes a lock, and any number
   of threads and domains may read them. A union of properties is one table, so a code point is tested against it
   with one search. *)
let idn =
  let gc name =
    match Ucd.category name with Some table -> table | None -> invalid_arg ("Formats: no General_Category " ^ name)
  in
  let sc name = match Ucd.script name with Some table -> table | None -> invalid_arg ("Formats: no Script " ^ name) in
  {
    disallowed =
      Ucd.union [ gc "Cc"; gc "Co"; gc "Zs"; gc "Zl"; gc "Zp"; gc "Lu"; gc "Lt"; gc "Sm"; gc "So"; gc "P"; gc "Cn" ];
    invisible = Ucd.union [ gc "Cc"; gc "Cf"; gc "Zs"; gc "Cn" ];
    mark = gc "M";
    nonspacing = gc "Mn";
    nonspacing_or_enclosing = Ucd.union [ gc "Mn"; gc "Me" ];
    letter_or_spacing_mark = Ucd.union [ gc "L"; gc "Mc" ];
    hebrew = sc "Hebrew";
    greek = sc "Greek";
    arabic_like = Ucd.union [ sc "Arabic"; sc "Syriac"; sc "Thaana"; sc "Nko" ];
    kana_or_han = Ucd.union [ sc "Hiragana"; sc "Katakana"; sc "Han" ];
    letter_mark_number = Ucd.union [ Ucd.letter; Ucd.mark; Ucd.number ];
  }

let is_disallowed_exception r =
  match r with 0x06fd | 0x06fe | 0x0f0b | 0x00b7 | 0x05f3 | 0x05f4 | 0x30fb -> true | _ -> false

(* disallowed reports code points IDNA2008 disallows that the tests exercise: controls, private use, unassigned,
   spaces, uppercase and titlecase letters (mapped away, never PVALID), symbols and punctuation. *)
let disallowed cps =
  let found = ref false in
  let i = ref 0 in
  while (not !found) && !i < Array.length cps do
    let r = cps.(!i) in
    if not (r = 0x2d || is_disallowed_exception r) then found := Ucd.contains idn.disallowed r;
    incr i
  done;
  !found

let puny_base = 36
let puny_t_min = 1
let puny_t_max = 26
let puny_skew = 38
let puny_damp = 700

let puny_adapt delta num_points first_time =
  let delta = if first_time then delta / puny_damp else delta lsr 1 in
  let delta = ref (delta + (delta / num_points)) in
  let k = ref 0 in
  while !delta > ((puny_base - puny_t_min) * puny_t_max) lsr 1 do
    delta := !delta / (puny_base - puny_t_min);
    k := !k + puny_base
  done;
  !k + ((puny_base - puny_t_min + 1) * !delta / (!delta + puny_skew))

let puny_threshold k bias = if k <= bias then puny_t_min else if k >= bias + puny_t_max then puny_t_max else k - bias
let saturating_add a b = if a > max_uint32 - b then max_uint32 else a + b
let puny_digit d = Char.chr (if d < 26 then d + 97 else d + 22)

(* punycode_encode is the RFC 3492 encoding, for the canonical round trip and A-label length checks. *)
let punycode_encode cps =
  let out = Buffer.create (2 * Array.length cps) in
  Array.iter (fun c -> if c < rune_self then Buffer.add_char out (Char.chr c)) cps;
  let basic_length = Buffer.length out in
  let h = ref basic_length in
  if basic_length > 0 then Buffer.add_char out '-';
  let n = ref 128 in
  let delta = ref 0 in
  let bias = ref 72 in
  while !h < Array.length cps do
    let m = ref max_uint32 in
    for i = 0 to Array.length cps - 1 do
      let c = cps.(i) in
      if c >= !n && c < !m then m := c
    done;
    (* A code point is always at or above n here, since the ones below it are the ones already written. *)
    let step = (!m - !n) * (!h + 1) in
    let step = if step > max_uint32 then max_uint32 else step in
    delta := saturating_add !delta step;
    n := !m;
    for i = 0 to Array.length cps - 1 do
      let c = cps.(i) in
      if c < !n then delta := saturating_add !delta 1;
      if c = !n then begin
        let q = ref !delta in
        let k = ref puny_base in
        let more = ref true in
        while !more do
          let t = puny_threshold !k !bias in
          if !q < t then more := false
          else begin
            Buffer.add_char out (puny_digit (t + ((!q - t) mod (puny_base - t))));
            q := (!q - t) / (puny_base - t);
            k := !k + puny_base
          end
        done;
        Buffer.add_char out (puny_digit !q);
        bias := puny_adapt !delta (!h + 1) (!h = basic_length);
        delta := 0;
        incr h
      end
    done;
    delta := (!delta + 1) land max_uint32;
    n := (!n + 1) land max_uint32
  done;
  Buffer.contents out

exception Not_punycode
(* Raised inside punycode_decode to leave it at the first thing that is not Punycode. *)

(* punycode_decode is the RFC 3492 decoding, enough to validate A-labels. *)
let punycode_decode b from stop =
  try
    let n = ref 128 in
    let i = ref 0 in
    let bias = ref 72 in
    let count = ref 0 in
    let output = Array.make (stop - from) 0 in
    (* basic is the number of bytes before the last "-", and not its index. *)
    let last = last_index_byte b from stop 0x2d in
    let basic = if last < 0 then 0 else last - from in
    for j = 0 to basic - 1 do
      let c = at b (from + j) in
      if c >= 0x80 then raise Not_punycode;
      output.(!count) <- c;
      incr count
    done;
    let index = ref (if basic > 0 then from + basic + 1 else from) in
    while !index < stop do
      let oldi = !i in
      let w = ref 1 in
      let k = ref puny_base in
      let more = ref true in
      while !more do
        if !index >= stop then raise Not_punycode;
        let c = at b !index in
        incr index;
        let digit =
          if c >= 0x30 && c <= 0x39 then c - 22
          else if c >= 0x41 && c <= 0x5a then c - 65
          else if c >= 0x61 && c <= 0x7a then c - 97
          else raise Not_punycode
        in
        let next = !i + (digit * !w) in
        if next > max_uint32 then raise Not_punycode;
        i := next;
        let t = puny_threshold !k !bias in
        if digit < t then more := false
        else begin
          w := !w * (puny_base - t);
          if !w > max_uint32 then raise Not_punycode;
          k := !k + puny_base
        end
      done;
      let length = !count + 1 in
      bias := puny_adapt (!i - oldi) length (oldi = 0);
      if !n + (!i / length) > max_uint32 then raise Not_punycode;
      n := !n + (!i / length);
      i := !i mod length;
      if !n > max_rune || (!n >= 0xd800 && !n <= 0xdfff) then raise Not_punycode;
      (* Each code point takes at least one byte of the text, so the output never outgrows the text. *)
      Array.blit output !i output (!i + 1) (!count - !i);
      output.(!i) <- !n;
      incr count;
      incr i
    done;
    Some (Array.sub output 0 !count)
  with Not_punycode -> None

let is_arabic_like r = Ucd.contains idn.arabic_like r

(* The Bidi classes. *)
let bidi_l = 0
let bidi_r = 1
let bidi_al = 2
let bidi_an = 3
let bidi_en = 4
let bidi_nsm = 5
let bidi_on = 6

(* bidi_class approximates the Bidi classes of the RFC 5893 Bidi rule by script and general category. *)
let bidi_class c =
  if Ucd.contains idn.nonspacing_or_enclosing c then bidi_nsm
  else if (c >= 0x660 && c <= 0x669) || c = 0x66b || c = 0x66c then bidi_an
  else if (c >= 0x30 && c <= 0x39) || (c >= 0x6f0 && c <= 0x6f9) then bidi_en
  else if Ucd.contains idn.hebrew c then bidi_r
  else if is_arabic_like c then bidi_al
  else if Ucd.contains idn.letter_or_spacing_mark c then bidi_l
  else bidi_on

let bidi_domain cps =
  let rtl = ref false in
  let strong = ref false in
  Array.iter
    (fun c ->
      rtl :=
        !rtl || Ucd.contains idn.hebrew c || is_arabic_like c || (c >= 0x660 && c <= 0x669) || c = 0x66b || c = 0x66c;
      let cls = bidi_class c in
      strong := !strong || cls = bidi_r || cls = bidi_al || cls = bidi_an)
    cps;
  !rtl && !strong

let bidi_label_ok cps is_bidi_domain =
  if not is_bidi_domain then true
  else begin
    let classes = Array.map bidi_class cps in
    let has = Array.make (bidi_on + 1) false in
    Array.iter (fun cls -> has.(cls) <- true) classes;
    if Array.length classes = 0 then false
    else begin
      let last = ref (Array.length classes - 1) in
      while !last > 0 && classes.(!last) = bidi_nsm do
        decr last
      done;
      let last = classes.(!last) in
      let first = classes.(0) in
      if first = bidi_r || first = bidi_al then
        (not has.(bidi_l))
        && (last = bidi_r || last = bidi_al || last = bidi_en || last = bidi_an)
        && not (has.(bidi_en) && has.(bidi_an))
      else if first = bidi_l then
        (not has.(bidi_r)) && (not has.(bidi_al)) && (not has.(bidi_an)) && (last = bidi_l || last = bidi_en)
      else false
    end
  end

let is_virama r =
  match r with
  | 0x094d | 0x09cd | 0x0a4d | 0x0acd | 0x0b4d | 0x0bcd | 0x0c4d | 0x0ccd | 0x0d3b | 0x0d3c | 0x0d4d | 0x0dca | 0x0e3a
  | 0x0eba | 0x0f84 | 0x1039 | 0x103a | 0x1714 | 0x1734 | 0x17d2 | 0x1a60 | 0x1b44 | 0x1baa | 0x1bab | 0x1bf2 | 0x1bf3
  | 0x2d7f | 0xa806 | 0xa8c4 | 0xa953 | 0xa9c0 | 0xaaf6 | 0xabed ->
      true
  | _ -> false

let is_joiner c = (c >= 0x0620 && c <= 0x064a) || (c >= 0x066e && c <= 0x06d3)

(* zwnj_joining_context approximates (Joining_Type:{L,D})(Joining_Type:T)*ZWNJ(Joining_Type:T)*(Joining_Type:{R,D})
   with Arabic letters. *)
let zwnj_joining_context cps i =
  let l = ref (i - 1) in
  while !l >= 0 && Ucd.contains idn.nonspacing cps.(!l) do
    decr l
  done;
  let r = ref (i + 1) in
  while !r < Array.length cps && Ucd.contains idn.nonspacing cps.(!r) do
    incr r
  done;
  !l >= 0 && !r < Array.length cps && is_joiner cps.(!l) && is_joiner cps.(!r)

(* has_range reports whether one of the code points is in lo .. hi. *)
let has_range cps lo hi = Array.exists (fun c -> c >= lo && c <= hi) cps

(* idn_label_ok checks the contextual and disallowed code points from RFC 5892 that the test suite exercises. *)
let idn_label_ok cps =
  let n = Array.length cps in
  if n = 0 || cps.(0) = 0x2d || cps.(n - 1) = 0x2d then false
  else if n >= 4 && cps.(2) = 0x2d && cps.(3) = 0x2d then false
  else if Ucd.contains idn.mark cps.(0) then false
  else if has_range cps 0x660 0x669 && has_range cps 0x6f0 0x6f9 then false
  else begin
    let ok = ref true in
    let i = ref 0 in
    while !ok && !i < n do
      let c = cps.(!i) in
      let i' = !i in
      if c = 0x302e || c = 0x302f || c = 0x0640 || c = 0x07fa || (c >= 0x3031 && c <= 0x3035) || c = 0x303b then
        ok := false
      else if c = 0x00b7 then ok := i' > 0 && i' < n - 1 && cps.(i' - 1) = 0x6c && cps.(i' + 1) = 0x6c
      else if c = 0x0375 then ok := i' < n - 1 && Ucd.contains idn.greek cps.(i' + 1)
      else if c = 0x05f3 || c = 0x05f4 then ok := i' > 0 && Ucd.contains idn.hebrew cps.(i' - 1)
      else if c = 0x30fb then ok := Array.exists (fun d -> d <> 0x30fb && Ucd.contains idn.kana_or_han d) cps
      else if c = 0x200d then ok := i' > 0 && is_virama cps.(i' - 1)
      else if c = 0x200c then ok := (i' > 0 && is_virama cps.(i' - 1)) || zwnj_joining_context cps i';
      incr i
    done;
    !ok
  end

let punycode_label_ok b from stop =
  match punycode_decode b from stop with
  | None -> false
  | Some decoded ->
      if Array.length decoded = 0 || Array.for_all (fun r -> r < rune_self) decoded then false
      else begin
        (* The encoding must be canonical: encoding the U-label again gives the same A-label. *)
        let encoded = punycode_encode decoded in
        let same = ref (String.length encoded = stop - from) in
        let i = ref 0 in
        while !same && !i < String.length encoded do
          let c = at b (from + !i) in
          let c = if c >= 0x41 && c <= 0x5a then c + 0x20 else c in
          if Char.code (String.get encoded !i) <> c then same := false;
          incr i
        done;
        !same && idn_label_ok decoded && (not (disallowed decoded)) && bidi_label_ok decoded (bidi_domain decoded)
      end

let hostname_label b from stop =
  hostname_label_ok b from stop && ((not (starts_with_xn b from stop)) || punycode_label_ok b (from + 4) stop)

let hostname_ok b from stop =
  if stop - from = 0 || stop - from > 253 then false else each_label b from stop hostname_label

let is_label_separator r =
  (* Full stop, ideographic full stop, fullwidth full stop, halfwidth ideographic full stop. *)
  r = 0x2e || r = 0x3002 || r = 0xff0e || r = 0xff61

(* idn_label checks one label of an internationalized host name, the bytes b[from .. stop-1], whose code points with
   an A-label decoded are `unicode`. It gives the length of the label as an A-label, or -1 when the label is not
   valid. *)
let idn_label b from stop unicode is_bidi =
  if from = stop then -1
  else if is_ascii_range b from stop then
    if not (hostname_label_ok b from stop) then -1
    else if starts_with_xn b from stop && not (punycode_label_ok b (from + 4) stop) then -1
    else if not (bidi_label_ok unicode is_bidi) then -1
    else stop - from
  else if
    (* A label that is not ASCII is never an A-label that decodes, so its code points are those of its text. *)
    not (idn_label_ok unicode)
  then -1
  else begin
    let without_joiners = Array.of_list (List.filter (fun r -> r <> 0x200c && r <> 0x200d) (Array.to_list unicode)) in
    if Array.exists (fun r -> Ucd.contains idn.invisible r) without_joiners then -1
    else if disallowed without_joiners then -1
    else if not (bidi_label_ok unicode is_bidi) then -1
    else
      let a_label_length = 4 + String.length (punycode_encode unicode) in
      if a_label_length > 63 then -1 else a_label_length
  end

let idn_hostname_ok b from stop =
  if from >= stop then false
  else begin
    (* The labels: the host name split at every label separator, keeping empty labels. *)
    let count = ref 1 in
    let i = ref from in
    while !i < stop do
      let d = decode_rune b !i stop in
      if is_label_separator (rune_of d) then incr count;
      i := !i + size_of d
    done;
    let label_from = Array.make !count from in
    let label_stop = Array.make !count stop in
    let label = ref 0 in
    i := from;
    while !i < stop do
      let d = decode_rune b !i stop in
      if is_label_separator (rune_of d) then begin
        label_stop.(!label) <- !i;
        incr label;
        label_from.(!label) <- !i + size_of d
      end;
      i := !i + size_of d
    done;
    let unicode_labels =
      Array.init !count (fun l ->
          let from = label_from.(l) and stop = label_stop.(l) in
          let decoded = if starts_with_xn b from stop then punycode_decode b (from + 4) stop else None in
          match decoded with Some cps -> cps | None -> code_points b from stop)
    in
    let is_bidi = Array.exists bidi_domain unicode_labels in
    let ascii_length = ref 0 in
    let ok = ref true in
    let l = ref 0 in
    while !ok && !l < !count do
      let length = idn_label b label_from.(!l) label_stop.(!l) unicode_labels.(!l) is_bidi in
      if length < 0 then ok := false else ascii_length := !ascii_length + length + 1;
      incr l
    done;
    !ok && !ascii_length - 1 <= 253
  end

(* ------------------------------------------------------------------------------------------------------------------ *)
(* E-mail addresses *)

(* The ASCII characters of an e-mail atom: letters, digits and the punctuation RFC 5322 gives atext. *)
let is_email_atom c =
  is_ascii_alphanumeric c
  ||
  match Char.chr c with
  | '!' | '#' | '$' | '%' | '&' | '\'' | '*' | '+' | '/' | '=' | '?' | '^' | '_' | '`' | '{' | '|' | '}' | '~' | '-' ->
      true
  | _ -> false

(* email_quoted_ok reads the rest of a quoted local part from position i, after its opening quote: characters that are
   not a quote, a backslash or a line break, or a backslash and any character but a line feed, then a quote that ends
   the text. *)
let email_quoted_ok b i stop =
  let i = ref i in
  (* -1 while the characters are being read. *)
  let result = ref (-1) in
  while !result < 0 do
    if !i >= stop then result := 0
    else
      let c = at b !i in
      if c = 0x22 then result := if !i = stop - 1 then 1 else 0
      else if c = 0x5c then
        if !i + 1 >= stop || at b (!i + 1) = 10 then result := 0
        else i := !i + 1 + size_of (decode_rune b (!i + 1) stop)
      else if c = 13 || c = 10 then result := 0
      else incr i
  done;
  !result = 1

(* email_local_ok reads the local part of an e-mail address: atoms separated by single dots, or a quoted string. An
   atom of an internationalized local part (RFC 6531 extends atext of RFC 5322) is taken here to be a letter, a mark
   or a number of any script, or one of the ASCII atom characters, as in the other ports of the evaluator. The Go
   source reads the same grammar with two regular expressions. *)
let email_local_ok b from stop idn_local =
  if from >= stop then false
  else if at b from = 0x22 then email_quoted_ok b (from + 1) stop
  else begin
    let atom_length = ref 0 in
    let i = ref from in
    let ok = ref true in
    while !ok && !i < stop do
      let c = at b !i in
      if c = 0x2e then begin
        if !atom_length = 0 then ok := false;
        atom_length := 0;
        incr i
      end
      else if c < rune_self then begin
        if not (is_email_atom c) then ok := false;
        incr atom_length;
        incr i
      end
      else if not idn_local then ok := false
      else begin
        let d = decode_rune b !i stop in
        if not (Ucd.contains idn.letter_mark_number (rune_of d)) then ok := false;
        incr atom_length;
        i := !i + size_of d
      end
    done;
    !ok && !atom_length <> 0
  end

let email_ok b from stop idn_address =
  let at_sign = last_index_byte b from stop 0x40 in
  if at_sign <= from then false
  else
    let domain_from = at_sign + 1 in
    if not (email_local_ok b from at_sign idn_address) then false
    else if stop - domain_from >= 2 && at b domain_from = 0x5b && at b (stop - 1) = 0x5d then
      let inner_from = domain_from + 1 in
      let inner_stop = stop - 1 in
      if
        inner_stop - inner_from >= 5
        && at b inner_from lor 0x20 = 0x69
        && at b (inner_from + 1) lor 0x20 = 0x70
        && at b (inner_from + 2) lor 0x20 = 0x76
        && at b (inner_from + 3) = 0x36
        && at b (inner_from + 4) = 0x3a
      then ipv6_ok b (inner_from + 5) inner_stop
      else ipv4_ok b inner_from inner_stop
    else if idn_address then idn_hostname_ok b domain_from stop
    else hostname_ok b domain_from stop

(* ------------------------------------------------------------------------------------------------------------------ *)
(* The RFC 3986 (URI) and RFC 3987 (IRI) grammars, read directly. The Go source builds them as regular expressions
   (uriRegexpSource). The grammar is deterministic once the fragment and the query are cut off at the first "#" and
   the first "?", so each part is one pass. *)

(* The sets of ASCII characters the grammar names. A set is a mask of the bits below, and uri_class gives the bits of
   each ASCII character. *)
let uri_unreserved_bit = 1
let uri_sub_delims_bit = 2
let uri_colon_bit = 4
let uri_at_bit = 8
let uri_slash_bit = 16
let uri_question_bit = 32
let uri_reg_name = uri_unreserved_bit lor uri_sub_delims_bit
let uri_userinfo = uri_reg_name lor uri_colon_bit
let uri_pchar = uri_userinfo lor uri_at_bit
let uri_segment_nc = uri_reg_name lor uri_at_bit

(* The characters of path segments and the "/" between them. *)
let uri_query_path = uri_pchar lor uri_slash_bit
let uri_query = uri_query_path lor uri_question_bit

let uri_class =
  let table = Bytes.make 128 '\000' in
  let add bit characters =
    String.iter
      (fun c -> Bytes.set table (Char.code c) (Char.chr (Char.code (Bytes.get table (Char.code c)) lor bit)))
      characters
  in
  add uri_unreserved_bit "0123456789ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz-._~";
  add uri_sub_delims_bit "!$&'()*+,;=";
  add uri_colon_bit ":";
  add uri_at_bit "@";
  add uri_slash_bit "/";
  add uri_question_bit "?";
  Bytes.to_string table

(* in_uri_set reports whether an ASCII character is in a set. *)
let in_uri_set c allowed = Char.code (String.get uri_class c) land allowed <> 0

let is_ucs_char c =
  (c >= 0xa0 && c <= 0xd7ff)
  || (c >= 0xf900 && c <= 0xfdcf)
  || (c >= 0xfdf0 && c <= 0xffef)
  || (c >= 0x10000 && c <= 0xefffd)

let is_iprivate c = (c >= 0xe000 && c <= 0xf8ff) || (c >= 0xf0000 && c <= 0xffffd) || (c >= 0x100000 && c <= 0x10fffd)

(* uri_chars_ok reports whether b[from .. stop-1] is characters of the set, percent-encoded triplets and, for an IRI,
   the characters beyond ASCII that RFC 3987 allows (with the private ranges too, for a query). *)
let uri_chars_ok b from stop allowed iri private_use =
  let i = ref from in
  let ok = ref true in
  while !ok && !i < stop do
    let c = at b !i in
    if c = 0x25 then
      if !i + 2 < stop && is_hex_digit (at b (!i + 1)) && is_hex_digit (at b (!i + 2)) then i := !i + 3 else ok := false
    else if c < rune_self then if in_uri_set c allowed then incr i else ok := false
    else if not iri then ok := false
    else
      let d = decode_rune b !i stop in
      let r = rune_of d in
      if is_ucs_char r || (private_use && is_iprivate r) then i := !i + size_of d else ok := false
  done;
  !ok

(* all_digits reports whether every byte of b[from .. stop-1] is a hex digit (hex) or an ASCII digit. *)
let all_digits b from stop hex =
  let i = ref from in
  while !i < stop && if hex then is_hex_digit (at b !i) else is_ascii_digit (at b !i) do
    incr i
  done;
  !i >= stop

(* uri_ip_future_ok reads the inside of an IP literal of a later version, between the "[" at host and the "]" at
   close: "v" hex digits "." then unreserved characters, sub-delimiters or ":". *)
let uri_ip_future_ok b host close =
  let dot = index_byte b (host + 1) close 0x2e in
  if dot < host + 3 then false
  else if not (all_digits b (host + 2) dot true) then false
  else if dot + 1 >= close then false
  else begin
    let i = ref (dot + 1) in
    while !i < close && at b !i < rune_self && in_uri_set (at b !i) uri_userinfo do
      incr i
    done;
    !i >= close
  end

(* uri_port_ok reads what follows the host of an authority: nothing, or ":" and digits. *)
let uri_port_ok b port stop = port >= stop || (at b port = 0x3a && all_digits b (port + 1) stop false)

(* uri_authority_ok reads an authority in b[from .. stop-1]: optional user information, a host (an IP literal, or a
   registered name, of which an IPv4 address is one) and an optional port. *)
let uri_authority_ok b from stop iri =
  let host = ref from in
  let ok = ref true in
  let i = ref from in
  while !ok && !i < stop do
    if at b !i = 0x40 then
      (* The host and the port have no "@", so the user information ends at the only one. *)
      begin if !host > from then ok := false
      else if not (uri_chars_ok b from !i uri_userinfo iri false) then ok := false
      else host := !i + 1
      end;
    incr i
  done;
  if not !ok then false
  else
    let host = !host in
    if host < stop && at b host = 0x5b then
      let close = index_byte b (host + 1) stop 0x5d in
      if close < 0 then false
      else if close > host + 1 && at b (host + 1) = 0x76 then
        uri_ip_future_ok b host close && uri_port_ok b (close + 1) stop
      else ipv6_ok b (host + 1) close && uri_port_ok b (close + 1) stop
    else
      let colon = index_byte b host stop 0x3a in
      let port = if colon >= 0 then colon else stop in
      uri_chars_ok b host port uri_reg_name iri false && uri_port_ok b port stop

(* uri_hier_ok reads the part of a URI between the scheme and the query in b[from .. stop-1]: "//" authority and a
   path, or a path. A relative reference's first segment has no ":" (no_scheme). *)
let uri_hier_ok b from stop iri no_scheme =
  if from = stop then true
  else if stop - from >= 2 && at b from = 0x2f && at b (from + 1) = 0x2f then
    let slash = index_byte b (from + 2) stop 0x2f in
    let segment_stop = if slash < 0 then stop else slash in
    uri_authority_ok b (from + 2) segment_stop iri && uri_chars_ok b segment_stop stop uri_query_path iri false
  else if at b from = 0x2f then
    (* An absolute path: "/" alone, or a first segment that is not empty. *)
    if from + 1 = stop then true
    else if at b (from + 1) = 0x2f then false
    else uri_chars_ok b (from + 1) stop uri_query_path iri false
  else
    (* A path with no root: a first segment that is not empty. *)
    let slash = index_byte b from stop 0x2f in
    let segment_stop = if slash < 0 then stop else slash in
    uri_chars_ok b from segment_stop (if no_scheme then uri_segment_nc else uri_pchar) iri false
    && uri_chars_ok b segment_stop stop uri_query_path iri false

(* uri_scheme_colon is the index of the ":" that ends a scheme at the start of b[from .. stop-1], or -1. The scheme is
   a letter, then letters, digits, "+", "-" and ".". *)
let uri_scheme_colon b from stop =
  if stop > from && is_ascii_letter (at b from) then begin
    let i = ref (from + 1) in
    while
      !i < stop
      &&
      let c = at b !i in
      is_ascii_letter c || is_ascii_digit c || c = 0x2b || c = 0x2d || c = 0x2e
    do
      incr i
    done;
    if !i < stop && at b !i = 0x3a then !i else -1
  end
  else -1

(* uri_ok reads a URI or an IRI, and with reference a relative reference too. *)
let uri_ok b from stop iri reference =
  let hash = index_byte b from stop 0x23 in
  if hash >= 0 && not (uri_chars_ok b (hash + 1) stop uri_query iri false) then false
  else
    let limit = if hash >= 0 then hash else stop in
    let question = index_byte b from limit 0x3f in
    if question >= 0 && not (uri_chars_ok b (question + 1) limit uri_query iri iri) then false
    else
      let path_stop = if question >= 0 then question else limit in
      let colon = uri_scheme_colon b from path_stop in
      (colon >= 0 && uri_hier_ok b (colon + 1) path_stop iri false)
      || (reference && uri_hier_ok b from path_stop iri true)

(* uri_template_var reads one variable of a URI template expression at i: name characters (letters, digits, "_" and
   percent-encoded triplets) with single dots between them, then an optional prefix length or "*". The next offset,
   or -1. *)
let uri_template_var b i stop =
  let i = ref i in
  let need = ref true in
  (* 0 while the name is being read, then 1 at its end and -1 when it is not a name. *)
  let state = ref 0 in
  while !state = 0 do
    if !i < stop && at b !i = 0x25 then
      if !i + 2 < stop && is_hex_digit (at b (!i + 1)) && is_hex_digit (at b (!i + 2)) then i := !i + 3 else state := -1
    else if !i < stop && (is_ascii_alphanumeric (at b !i) || at b !i = 0x5f) then incr i
    else if !need then state := -1
    else state := 1;
    if !state = 0 then begin
      need := false;
      if !i < stop && at b !i = 0x2e then begin
        incr i;
        need := true
      end
    end
  done;
  if !state < 0 then -1
  else if !i < stop && at b !i = 0x2a then !i + 1
  else if !i < stop && at b !i = 0x3a then
    if not (!i + 1 < stop && at b (!i + 1) >= 0x31 && at b (!i + 1) <= 0x39) then -1
    else begin
      i := !i + 2;
      let digits = ref 0 in
      while !i < stop && is_ascii_digit (at b !i) && !digits < 3 do
        incr i;
        incr digits
      done;
      !i
    end
  else !i

(* The operators of a URI template expression. *)
let is_uri_template_operator c =
  match Char.chr c with '+' | '#' | '.' | '/' | ';' | '?' | '&' | '=' | ',' | '!' | '@' | '|' -> true | _ -> false

(* The characters a URI template has no literal for. "{" opens an expression. *)
let is_uri_template_not_literal c =
  c <= 0x20 || match Char.chr c with '"' | '\'' | '<' | '>' | '\\' | '^' | '`' | '|' | '}' -> true | _ -> false

(* uri_template_ok reads an RFC 6570 URI template: literal characters and expressions in braces. *)
let uri_template_ok b from stop =
  let i = ref from in
  let ok = ref true in
  while !ok && !i < stop do
    let c = at b !i in
    if c = 0x7b then begin
      incr i;
      if !i < stop && is_uri_template_operator (at b !i) then incr i;
      let more = ref true in
      while !ok && !more do
        i := uri_template_var b !i stop;
        if !i < 0 || !i >= stop then ok := false
        else if at b !i = 0x2c then incr i
        else if at b !i = 0x7d then begin
          incr i;
          more := false
        end
        else ok := false
      done
    end
    else if is_uri_template_not_literal c then ok := false
    else incr i
  done;
  !ok

(* ------------------------------------------------------------------------------------------------------------------ *)
(* JSON pointers *)

(* json_pointer_ok reads an RFC 6901 JSON pointer: segments that each start with "/", where "~" is followed by 0 or
   1. *)
let json_pointer_ok b from stop =
  if from < stop && at b from <> 0x2f then false
  else begin
    let i = ref from in
    let ok = ref true in
    while !ok && !i < stop do
      if at b !i = 0x7e && (!i + 1 >= stop || (at b (!i + 1) <> 0x30 && at b (!i + 1) <> 0x31)) then ok := false;
      incr i
    done;
    !ok
  end

(* relative_json_pointer_ok reads a non-negative integer without leading zeros, then "#" or a JSON pointer. *)
let relative_json_pointer_ok b from stop =
  let i = ref from in
  while !i < stop && is_ascii_digit (at b !i) do
    incr i
  done;
  let i = !i in
  if i = from || (i > from + 1 && at b from = 0x30) then false
  else (stop - i = 1 && at b i = 0x23) || json_pointer_ok b i stop

(* ------------------------------------------------------------------------------------------------------------------ *)
(* The checks by format *)

let is_date text off len = date_ok text off (off + len)
let is_time text off len = time_ok text off (off + len)
let is_date_time text off len = date_time_ok text off (off + len)
let is_duration text off len = duration_ok text off (off + len)
let is_uuid text off len = uuid_ok text off (off + len)
let is_ipv4 text off len = ipv4_ok text off (off + len)
let is_ipv6 text off len = ipv6_ok text off (off + len)
let is_legacy_hostname text off len = legacy_hostname_ok text off (off + len)
let is_hostname text off len = hostname_ok text off (off + len)
let is_idn_hostname text off len = idn_hostname_ok text off (off + len)
let is_email text off len = email_ok text off (off + len) false
let is_idn_email text off len = email_ok text off (off + len) true
let is_uri text off len = uri_ok text off (off + len) false false
let is_uri_reference text off len = uri_ok text off (off + len) false true
let is_iri text off len = uri_ok text off (off + len) true false
let is_iri_reference text off len = uri_ok text off (off + len) true true
let is_uri_template text off len = uri_template_ok text off (off + len)
let is_json_pointer text off len = json_pointer_ok text off (off + len)
let is_relative_json_pointer text off len = relative_json_pointer_ok text off (off + len)

(* The pattern is kept as text of its own for the parser to read. *)
let is_regex text off len = Ecmaregex.valid (Bytes.sub_string text off len)

(* check_string asserts a string format. legacy_hostname selects the RFC 1123 host name rules of draft 4 and 6. *)
let check_string k text off len legacy_hostname =
  let stop = off + len in
  (* The checks read the text and keep nothing of it. *)
  match k with
  | 1 -> date_ok text off stop
  | 2 -> time_ok text off stop
  | 3 -> date_time_ok text off stop
  | 4 -> duration_ok text off stop
  | 5 -> uuid_ok text off stop
  | 6 -> ipv4_ok text off stop
  | 7 -> ipv6_ok text off stop
  | 8 -> if legacy_hostname then legacy_hostname_ok text off stop else hostname_ok text off stop
  | 9 -> idn_hostname_ok text off stop
  | 10 -> email_ok text off stop false
  | 11 -> email_ok text off stop true
  | 12 -> uri_ok text off stop false false
  | 13 -> uri_ok text off stop false true
  | 14 -> uri_ok text off stop true false
  | 15 -> uri_ok text off stop true true
  | 16 -> uri_template_ok text off stop
  | 17 -> json_pointer_ok text off stop
  | 18 -> relative_json_pointer_ok text off stop
  | 19 -> is_regex text off len
  | _ -> true

let kind_validator k legacy_hostname =
  match k with
  | 1 -> Some is_date
  | 2 -> Some is_time
  | 3 -> Some is_date_time
  | 4 -> Some is_duration
  | 5 -> Some is_uuid
  | 6 -> Some is_ipv4
  | 7 -> Some is_ipv6
  | 8 -> Some (if legacy_hostname then is_legacy_hostname else is_hostname)
  | 9 -> Some is_idn_hostname
  | 10 -> Some is_email
  | 11 -> Some is_idn_email
  | 12 -> Some is_uri
  | 13 -> Some is_uri_reference
  | 14 -> Some is_iri
  | 15 -> Some is_iri_reference
  | 16 -> Some is_uri_template
  | 17 -> Some is_json_pointer
  | 18 -> Some is_relative_json_pointer
  | 19 -> Some is_regex
  | _ -> None

let validator format = kind_validator (kind_of format draft2020_12) false

(* ------------------------------------------------------------------------------------------------------------------ *)
(* Punycode over strings, for the tests *)

let punycode_encode_string label =
  let b = Bytes.of_string label in
  punycode_encode (code_points b 0 (Bytes.length b))

let punycode_decode_string encoded =
  let b = Bytes.of_string encoded in
  match punycode_decode b 0 (Bytes.length b) with
  | None -> None
  | Some cps ->
      let out = Buffer.create (4 * Array.length cps) in
      Array.iter (fun r -> Buffer.add_utf_8_uchar out (Uchar.of_int r)) cps;
      Some (Buffer.contents out)

(* ------------------------------------------------------------------------------------------------------------------ *)
(* Content *)

let base64_value c =
  if c >= 0x41 && c <= 0x5a then c - 0x41
  else if c >= 0x61 && c <= 0x7a then c - 0x61 + 26
  else if c >= 0x30 && c <= 0x39 then c - 0x30 + 52
  else if c = 0x2b then 62
  else if c = 0x2f then 63
  else -1

let base64_decoded_capacity len = len / 4 * 3

let base64_decode text off len output =
  if len mod 4 <> 0 then -1
  else if Bytes.length output < base64_decoded_capacity len then invalid_arg "Formats.base64_decode"
  else begin
    let output_len = ref 0 in
    let i = ref off in
    let ok = ref true in
    while !ok && !i < off + len do
      let pad = ref 0 in
      while !pad < 4 && at text (!i + 3 - !pad) = 0x3d do
        incr pad
      done;
      if !pad > 2 || (!pad > 0 && !i + 4 <> off + len) then ok := false
      else begin
        let acc = ref 0 in
        let j = ref 0 in
        while !ok && !j <= 3 - !pad do
          let v = base64_value (at text (!i + !j)) in
          if v < 0 then ok := false else acc := (!acc lsl 6) lor v;
          incr j
        done;
        if !ok then begin
          let acc = !acc lsl (6 * !pad) in
          Bytes.set output !output_len (Char.chr ((acc lsr 16) land 0xff));
          if !pad < 2 then Bytes.set output (!output_len + 1) (Char.chr ((acc lsr 8) land 0xff));
          if !pad < 1 then Bytes.set output (!output_len + 2) (Char.chr (acc land 0xff));
          output_len := !output_len + 3 - !pad;
          i := !i + 4
        end
      end
    done;
    if !ok then !output_len else -1
  end
