(* pattern and patternProperties matching with ECMA-262 semantics (the u flag), as JSON Schema specifies. A port of
   pattern.go of the Go module.

   A pattern gets the cheapest matcher that decides it exactly:

   - patterns every string matches ("", ".*", "[\s\S]*" and the like), ".+", and line lengths ("^.{1,256}$");
   - anchored sequences of quantified ASCII character classes and literals ("^[a-z][a-z0-9_]{0,29}$", "^x-",
     "^[@$_#]"), matched in one pass over the string;
   - alternatives of such sequences once groups are multiplied out ("^([a|A]uto)|([n|N]one)$"), sets of literals,
     and separated lists ("^([a-z]+)(\.[a-z]+)*$");
   - anything else by the regular expression engine (see pattern_engine.ml).

   The Go source keeps every compiled pattern in a cache for the whole process. This module has no such cache, because
   the library has no global mutable state: compile compiles the pattern it is given each time, and the schema compiler
   shares one matcher between the identical patterns of a schema.

   Matching allocates nothing. The text matched is the bytes of a Bytes.t from one position up to another, never a
   copy. Every matcher is a top-level function over the bytes and integer positions that returns a bool or an int (a
   negative int for "no"), so that nothing is boxed and no closure is built. Where the Go source slices the text and
   passes the rest on, the functions here pass the position (at) and the end of the text (stop).

   The 64-bit quantities of the Go source:

   - The ASCII members of a character set are two uint64 masks in Go. Here they are a string of 256 bytes, one per byte
     value, non-zero for a member (the entries from 128 up are zero), read with one checked load.
   - The counts of a quantifier are uint32 in Go, with the largest uint32 for "unbounded". Here they are OCaml ints
     holding the same values, which fit the 63-bit int of a 64-bit system with room to spare. Sums of them, and
     comparisons that the Go source widens to uint64, are plain int arithmetic for the same reason. *)

(* The largest count of a quantifier (math.MaxUint32 of the Go source), which also says "unbounded". *)
let max_uint32 = (1 lsl 32) - 1

(* The largest fixed width of a pinned sequence (math.MaxInt32 of the Go source). *)
let max_int32 = (1 lsl 31) - 1

(* The first byte value that is not a character by itself (utf8.RuneSelf of the Go source). *)
let rune_self = 0x80

(* ---------------------------------------------------------------------------------------------------------------------
   Character sets *)

(* char_set is a set of characters: ASCII by table, then either all or none of the line separators U+2028 and U+2029,
   and either all or none of the other non-ASCII characters. *)
type char_set = { table : string; non_ascii : bool; separators : bool }

let empty_set = { table = String.make 256 '\000'; non_ascii = false; separators = false }

(* char_range is ASCII lo to hi inclusive (hi below 128). *)
let char_range lo hi =
  { empty_set with table = String.init 256 (fun c -> if c >= lo && c <= hi then '\001' else '\000') }

let char_single c = char_range (Char.code c) (Char.code c)

(* has_ascii reports whether the set holds an ASCII character, given as its byte. A byte of 128 or more is not in the
   table's first half and is never a member. *)
let has_ascii s c = String.get s.table (Char.code c) <> '\000'

let contains s c =
  if c < rune_self then String.get s.table c <> '\000'
  else if c = 0x2028 || c = 0x2029 then s.separators
  else s.non_ascii

(* dot_set is ECMA-262's ".": everything but the line terminators. *)
let dot_set =
  {
    table = String.init 256 (fun c -> if c < rune_self && c <> 10 && c <> 13 then '\001' else '\000');
    non_ascii = true;
    separators = false;
  }

let union s other =
  {
    table = String.init 256 (fun c -> if s.table.[c] <> '\000' || other.table.[c] <> '\000' then '\001' else '\000');
    non_ascii = s.non_ascii || other.non_ascii;
    separators = s.separators || other.separators;
  }

(* complement_ascii is the ASCII characters the set does not hold, and nothing else. *)
let complement_ascii s =
  {
    table = String.init 256 (fun c -> if c < rune_self && s.table.[c] = '\000' then '\001' else '\000');
    non_ascii = false;
    separators = false;
  }

let negate s = { (complement_ascii s) with non_ascii = not s.non_ascii; separators = not s.separators }

let disjoint s other =
  let rec ascii c = c = rune_self || ((s.table.[c] = '\000' || other.table.[c] = '\000') && ascii (c + 1)) in
  ascii 0 && (not (s.non_ascii && other.non_ascii)) && not (s.separators && other.separators)

(* single is the set's character when it holds exactly one ASCII character and nothing else, or -1. *)
let single s =
  if s.non_ascii || s.separators then -1
  else
    let found = ref (-1) and count = ref 0 in
    for c = 0 to rune_self - 1 do
      if s.table.[c] <> '\000' then begin
        found := c;
        incr count
      end
    done;
    if !count = 1 then !found else -1

let digit_set = char_range (Char.code '0') (Char.code '9')

(* word_set is "\w". *)
let word_set =
  union
    (union (char_range (Char.code '0') (Char.code '9')) (char_range (Char.code 'A') (Char.code 'Z')))
    (union (char_range (Char.code 'a') (Char.code 'z')) (char_single '_'))

(* ---------------------------------------------------------------------------------------------------------------------
   The compiled pattern *)

type sequence_item = {
  set : char_set;
  min : int;
  (* max_uint32: unbounded. *)
  max : int;
}

(* sequence is "^" then quantified character sets, optionally "$". Matched greedily, which is exact because every
   variable item's set is disjoint from the next item's (a character the item leaves cannot be taken by the next one
   either). Where it is not, a sequence anchored at both ends with a single variable item is still decided in one pass:
   the string's length fixes how many characters that item takes (pinned). *)
type sequence = {
  items : sequence_item array;
  to_end : bool;
  (* The index of the one variable item of a sequence matched by length, or -1 for greedy matching. *)
  pinned : int;
  (* For a pinned sequence, the characters the other items take. *)
  fixed_width : int;
}

(* separated_list is a list of items between separators: "^I(SR)*$" or "^I(SR)+$" (no final), or "^(RS)*F$" and
   "^(RS)+F$". The separator is a fixed sequence and no variable class can run into what follows it, so greedy matching
   splits the string exactly where the pattern does. *)
type separated_list = {
  (* I of the first form. None says the list has the second form. *)
  first : sequence option;
  repeated : sequence;
  separator : sequence;
  (* F of the second form, matched against the whole remainder. *)
  final : sequence option;
  min_repeats : int;
}

(* alternative is one alternative of Alternatives: a literal anchored at either end (or neither), a class sequence
   anchored at the start, or a class sequence of fixed width anchored at the end or not at all. *)
type alternative =
  | Alt_literal of { text : string; at_start : bool; at_end : bool }
  (* Alt_start is "^sequence" (with "$" when the sequence runs to the end). *)
  | Alt_start of sequence
  (* Alt_end is "sequence$" of width characters: matched over the string's last width characters. *)
  | Alt_end of { seq : sequence; width : int }
  (* Alt_anywhere is an unanchored sequence of width characters: matched at each position. *)
  | Alt_anywhere of { seq : sequence; width : int }

(* The strings of Literals. Up to eight are tried in turn. More are found through a hash table (a map in the Go source,
   which looks a byte slice up without allocating. Hashtbl would need a string for the key, so the table is this
   module's own): slots holds indexes into texts, -1 for an empty slot, with open addressing and linear probing. Its
   length is a power of two and at least twice the number of strings, so a search always ends at an empty slot. *)
type literals = Few of string array | Many of { texts : string array; slots : int array }

type matcher =
  (* Everything matches every string. *)
  | Everything
  (* Literal is "^literal" (with "$": the whole string). *)
  | Literal of { text : string; whole : bool }
  | Sequence of sequence
  | Separated_list of separated_list
  (* Has_content is ".+" ("^.+" with start): some (the first) character is not a line terminator. *)
  | Has_content of { start : bool }
  (* Line is "^.{min,max}$": between min and max characters, none a line terminator. *)
  | Line of { min : int; max : int }
  (* Literals is "^(a|b|...)$": one of a set of strings. *)
  | Literals of literals
  (* Alternatives is top-level alternatives of literals or class sequences, each optionally anchored. *)
  | Alternatives of alternative array
  (* Excluded_class_with_word is "^(?=[^SET]+$)(?=(.*\w)).+$": a non-empty line without a character of the set,
     containing a word character. With bangs ("^(?=!+[^SET]+$)..."), that line follows one or more "!". *)
  | Excluded_class_with_word of { set : char_set; bangs : bool }
  | Engine of Pattern_engine.t

(* t is a compiled pattern. Nothing writes to it after compile.

   units is the engine of a pattern that has a faster matcher and is valid only without the u flag. ECMA-262 matches
   such a pattern by UTF-16 code unit, and the faster matchers read code points, so the engine answers for a text with
   a character beyond the Basic Multilingual Plane. *)
type t = { source : string; matcher : matcher; units : Pattern_engine.t option }

let source p = p.source

let kind_name p =
  match p.matcher with
  | Everything -> "everything"
  | Literal _ -> "literal"
  | Sequence _ -> "sequence"
  | Separated_list _ -> "separated list"
  | Has_content _ -> "has content"
  | Line _ -> "line"
  | Literals _ -> "literals"
  | Alternatives _ -> "alternatives"
  | Excluded_class_with_word _ -> "excluded class with word"
  | Engine _ -> "engine"

(* ---------------------------------------------------------------------------------------------------------------------
   Reading the text *)

(* A decoded character is one int: the code point shifted left by three bits, with the number of bytes it takes in the
   low three bits. That is how decode_rune returns two results without a tuple. *)
let rune_error = (0xFFFD lsl 3) lor 1
let is_continuation c = c land 0xC0 = 0x80

(* decode_long reads the character at i whose first byte c0 is not ASCII, reading no byte at or beyond stop. Bytes that
   are not a well-formed character read as U+FFFD, one byte at a time (utf8.DecodeRune of the Go source). *)
let decode_long b i stop c0 =
  if c0 < 0xC2 then rune_error
  else if c0 < 0xE0 then
    if i + 1 < stop then
      let c1 = Char.code (Bytes.get b (i + 1)) in
      if is_continuation c1 then ((((c0 land 0x1F) lsl 6) lor (c1 land 0x3F)) lsl 3) lor 2 else rune_error
    else rune_error
  else if c0 < 0xF0 then
    if i + 2 < stop then
      let c1 = Char.code (Bytes.get b (i + 1)) in
      let c2 = Char.code (Bytes.get b (i + 2)) in
      let c = ((c0 land 0x0F) lsl 12) lor ((c1 land 0x3F) lsl 6) lor (c2 land 0x3F) in
      if is_continuation c1 && is_continuation c2 && c >= 0x800 && (c < 0xD800 || c > 0xDFFF) then (c lsl 3) lor 3
      else rune_error
    else rune_error
  else if c0 < 0xF5 then
    if i + 3 < stop then
      let c1 = Char.code (Bytes.get b (i + 1)) in
      let c2 = Char.code (Bytes.get b (i + 2)) in
      let c3 = Char.code (Bytes.get b (i + 3)) in
      let c = ((c0 land 0x07) lsl 18) lor ((c1 land 0x3F) lsl 12) lor ((c2 land 0x3F) lsl 6) lor (c3 land 0x3F) in
      if is_continuation c1 && is_continuation c2 && is_continuation c3 && c >= 0x10000 && c <= 0x10FFFF then
        (c lsl 3) lor 4
      else rune_error
    else rune_error
  else rune_error

(* decode_rune reads the character at i (a byte for ASCII): the code point is the result shifted right by three bits
   and the size in bytes is its low three bits. *)
let decode_rune b i stop =
  let c0 = Char.code (Bytes.get b i) in
  if c0 < rune_self then (c0 lsl 3) lor 1 else decode_long b i stop c0

let rec is_ascii_range b at stop =
  at >= stop || (Char.code (Bytes.get b at) < rune_self && is_ascii_range b (at + 1) stop)

let is_ascii b off len = is_ascii_range b off (off + len)

(* has_supplementary reports whether the bytes at up to stop have a character beyond the Basic Multilingual Plane,
   which is the only kind of text that code units and code points read differently. Such a character starts with a
   byte of 0xF0 or more. *)
let rec has_supplementary b at stop = at < stop && (Bytes.get b at >= '\xF0' || has_supplementary b (at + 1) stop)

let text_is_ascii s =
  let rec from i = i = String.length s || (Char.code s.[i] < rune_self && from (i + 1)) in
  from 0

(* rune_count is n plus the number of characters from at up to stop (a byte that starts no character counts as one). *)
let rec rune_count b at stop n =
  if at >= stop then n else rune_count b (at + (decode_rune b at stop land 7)) stop (n + 1)

(* skip_chars is the position n characters after at. *)
let rec skip_chars b at stop n = if n <= 0 then at else skip_chars b (at + (decode_rune b at stop land 7)) stop (n - 1)

(* is_line_terminator reports an ECMA-262 LineTerminator, which "." does not match. *)
let is_line_terminator c = c = 10 || c = 13 || c = 0x2028 || c = 0x2029

(* line_length is n plus the number of characters from at up to stop, when none is a line terminator, else -1. *)
let rec line_length b at stop n =
  if at >= stop then n
  else
    let d = decode_rune b at stop in
    if is_line_terminator (d lsr 3) then -1 else line_length b (at + (d land 7)) stop (n + 1)

(* has_content reports whether some character from at up to stop is not a line terminator. *)
let rec has_content b at stop =
  at < stop
  &&
  let d = decode_rune b at stop in
  (not (is_line_terminator (d lsr 3))) || has_content b (at + (d land 7)) stop

(* range_equals reports whether the bytes of b from at are those of text from its index i on. The caller has checked
   that b has that many bytes before the end of its text. *)
let rec range_equals b at text i =
  i = String.length text || (Bytes.get b (at + i) = String.get text i && range_equals b at text (i + 1))

(* index_string is the position of the first occurrence of text at or after at, or -1. *)
let rec index_string b at stop text =
  if at + String.length text > stop then -1
  else if range_equals b at text 0 then at
  else index_string b (at + 1) stop text

(* ---------------------------------------------------------------------------------------------------------------------
   The set of literals *)

(* hash_range is the hash of the bytes from at up to stop, continuing from h. The arithmetic wraps in the int, which
   is as good for a hash. *)
let rec hash_range b at stop h =
  if at >= stop then h land max_int else hash_range b (at + 1) stop (h lxor Char.code (Bytes.get b at) * 16777619)

let hash_seed = 5381

(* probe searches the table from a slot on for the len bytes of b from off. *)
let rec probe texts slots b off len slot =
  let k = Array.get slots slot in
  k >= 0
  &&
  let text = Array.get texts k in
  (String.length text = len && range_equals b off text 0)
  || probe texts slots b off len ((slot + 1) land (Array.length slots - 1))

let rec few_contains texts b off len i =
  i < Array.length texts
  &&
  let text = Array.get texts i in
  (String.length text = len && range_equals b off text 0) || few_contains texts b off len (i + 1)

let make_literals texts =
  if Array.length texts <= 8 then Few texts
  else begin
    let size = ref 16 in
    while !size < 2 * Array.length texts do
      size := 2 * !size
    done;
    let slots = Array.make !size (-1) in
    Array.iteri
      (fun k text ->
        let b = Bytes.of_string text in
        let len = Bytes.length b in
        if not (probe texts slots b 0 len (hash_range b 0 len hash_seed land (!size - 1))) then begin
          let slot = ref (hash_range b 0 len hash_seed land (!size - 1)) in
          while slots.(!slot) >= 0 do
            slot := (!slot + 1) land (!size - 1)
          done;
          slots.(!slot) <- k
        end)
      texts;
    Many { texts; slots }
  end

(* ---------------------------------------------------------------------------------------------------------------------
   Class sequences *)

(* take_ascii is the position after the bytes of the set from at, going no further than limit. *)
let rec take_ascii set b at limit =
  if at < limit && has_ascii set (Bytes.get b at) then take_ascii set b (at + 1) limit else at

(* take_chars greedily takes characters of the set from at, where n are already taken and max is the most: the position
   after them, or -1 when they are fewer than min. *)
let rec take_chars set b at stop n min max =
  if n < max && at < stop then
    let d = decode_rune b at stop in
    if contains set (d lsr 3) then take_chars set b (at + (d land 7)) stop (n + 1) min max
    else if n < min then -1
    else at
  else if n < min then -1
  else at

(* take_exact takes count characters of the set from at: the position after them, or -1. *)
let rec take_exact set b at stop count =
  if count <= 0 then at
  else if at >= stop then -1
  else
    let d = decode_rune b at stop in
    if contains set (d lsr 3) then take_exact set b (at + (d land 7)) stop (count - 1) else -1

let rec match_pinned_from q b at stop variable i =
  i = Array.length q.items
  ||
  let item = Array.get q.items i in
  let next = take_exact item.set b at stop (if i = q.pinned then variable else item.min) in
  next >= 0 && match_pinned_from q b next stop variable (i + 1)

(* match_pinned matches a pinned sequence: every item but one takes a fixed number of characters, so the string's
   length says how many the variable one takes. *)
let match_pinned q b off stop ascii =
  let length = if ascii then stop - off else rune_count b off stop 0 in
  let variable = length - q.fixed_width in
  let v = Array.get q.items q.pinned in
  variable >= v.min && variable <= v.max && match_pinned_from q b off stop variable 0

let rec consume_from items b at stop i =
  if i = Array.length items then at
  else
    let item = Array.get items i in
    let next = take_chars item.set b at stop 0 item.min item.max in
    if next < 0 then -1 else consume_from items b next stop (i + 1)

(* consume greedily matches the items at the start of the text from at (ignoring to_end): the position after the bytes
   taken, or -1. (The Go source returns the number of bytes taken, and slices.) *)
let consume q b at stop = consume_from q.items b at stop 0

(* match_chars matches over any text, a character at a time. *)
let match_chars q b off stop =
  if q.pinned >= 0 then match_pinned q b off stop false
  else
    let at = consume q b off stop in
    at >= 0 && ((not q.to_end) || at = stop)

let rec match_ascii_from q b at stop i =
  if i = Array.length q.items then (not q.to_end) || at = stop
  else
    let item = Array.get q.items i in
    let limit = if item.max < stop - at then at + item.max else stop in
    let next = take_ascii item.set b at limit in
    next - at >= item.min && match_ascii_from q b next stop (i + 1)

(* match_ascii matches over ASCII text, a byte per character. *)
let match_ascii q b off stop = if q.pinned >= 0 then match_pinned q b off stop true else match_ascii_from q b off stop 0

(* match_sequence is match of the Go source's sequence: for a text that is not known to be ASCII. *)
let match_sequence q b off stop =
  if is_ascii_range b off stop then match_ascii q b off stop else match_chars q b off stop

(* ---------------------------------------------------------------------------------------------------------------------
   Separated lists *)

(* The first form after its first item: separators and repeated items up to the end of the text. *)
let rec match_list_rest l b at stop repeats =
  if at = stop then repeats >= l.min_repeats
  else
    let n = consume l.separator b at stop in
    n >= 0
    &&
    let m = consume l.repeated b n stop in
    m >= 0 && match_list_rest l b m stop (repeats + 1)

(* The second form: repeated items and separators until the final item matches the whole remainder. *)
let rec match_list_final l final b at stop repeats =
  (repeats >= l.min_repeats && match_sequence final b at stop)
  ||
  let n = consume l.repeated b at stop in
  n >= 0
  &&
  let m = consume l.separator b n stop in
  m >= 0 && match_list_final l final b m stop (repeats + 1)

let match_list l b off stop =
  match l.first with
  | Some first ->
      let n = consume first b off stop in
      n >= 0 && match_list_rest l b n stop 0
  | None -> ( match l.final with Some final -> match_list_final l final b off stop 0 | None -> false)

(* ---------------------------------------------------------------------------------------------------------------------
   Alternatives *)

let rec anywhere_ascii seq width b at stop =
  at + width <= stop && (match_ascii seq b at stop || anywhere_ascii seq width b (at + 1) stop)

(* A sequence of fixed width at each of the positions left. A position that fails is before the end of the text, so
   there is a character to step over. *)
let rec anywhere_chars seq b at stop positions =
  positions > 0
  && (match_chars seq b at stop || anywhere_chars seq b (at + (decode_rune b at stop land 7)) stop (positions - 1))

(* match_alternative reports whether the alternative matches the text, ascii saying whether the text is ASCII. *)
let match_alternative a b off stop ascii =
  match a with
  | Alt_literal { text; at_start; at_end } ->
      let n = String.length text in
      if at_start && at_end then stop - off = n && range_equals b off text 0
      else if at_start then stop - off >= n && range_equals b off text 0
      else if at_end then stop - off >= n && range_equals b (stop - n) text 0
      else index_string b off stop text >= 0
  | Alt_start seq -> if ascii then match_ascii seq b off stop else match_chars seq b off stop
  | Alt_end { seq; width } ->
      if ascii then stop - off >= width && match_ascii seq b (stop - width) stop
      else
        let skip = rune_count b off stop 0 - width in
        skip >= 0 && match_chars seq b (skip_chars b off stop skip) stop
  | Alt_anywhere { seq; width } ->
      if ascii then anywhere_ascii seq width b off stop
      else anywhere_chars seq b off stop (rune_count b off stop 0 - width + 1)

let rec match_alternatives alts b off stop ascii i =
  i < Array.length alts
  && (match_alternative (Array.get alts i) b off stop ascii || match_alternatives alts b off stop ascii (i + 1))

(* ---------------------------------------------------------------------------------------------------------------------
   The excluded class with a word *)

let rec skip_bangs b at stop = if at < stop && Bytes.get b at = '!' then skip_bangs b (at + 1) stop else at

(* A line from at with no character of the set, word saying whether a word character has been seen. *)
let rec match_excluded set b at stop word =
  if at >= stop then word
  else
    let d = decode_rune b at stop in
    let c = d lsr 3 in
    (not (contains set c || is_line_terminator c))
    && match_excluded set b (at + (d land 7)) stop (word || contains word_set c)

(* ---------------------------------------------------------------------------------------------------------------------
   Matching *)

exception Match_limit of string * bool

(* engine_match matches the text on the engine with the limit of the scratch. A match that reached the limit has no
   answer: Match_limit carries the source of the pattern and whether the memory of the match was the limit, not its
   time. Entering the handler costs nothing, and the exception is made only when a limit is reached. *)
let engine_match p scratch engine b off len =
  try Pattern_engine.is_match scratch engine b off len with
  | Pattern_engine.Match_time -> raise (Match_limit (p.source, false))
  | Pattern_engine.Match_memory -> raise (Match_limit (p.source, true))

(* matches_shape is matches for a pattern matched by code point: the matcher the pattern was given. The engine matches
   with the limit of the scratch. Every other matcher reads the text once, or a bounded number of times, and has no
   limit. *)
let matches_shape p scratch b off len ascii =
  let stop = off + len in
  match p.matcher with
  | Everything -> true
  | Literal { text; whole } ->
      if whole then len = String.length text && range_equals b off text 0
      else len >= String.length text && range_equals b off text 0
  | Sequence seq -> if ascii then match_ascii seq b off stop else match_chars seq b off stop
  | Separated_list list -> match_list list b off stop
  | Has_content { start } ->
      if start then len > 0 && not (is_line_terminator (decode_rune b off stop lsr 3)) else has_content b off stop
  | Line { min; max } ->
      if ascii && len < min then false
      else
        (* A line terminator gives -1, which is below every min. *)
        let n = line_length b off stop 0 in
        n >= min && n <= max
  | Literals (Few texts) -> few_contains texts b off len 0
  | Literals (Many { texts; slots }) ->
      probe texts slots b off len (hash_range b off stop hash_seed land (Array.length slots - 1))
  | Alternatives alts -> match_alternatives alts b off stop ascii 0
  | Excluded_class_with_word { set; bangs } ->
      if bangs then
        let rest = skip_bangs b off stop in
        rest <> off && rest <> stop && match_excluded set b rest stop false
      else match_excluded set b off stop false
  | Engine engine -> engine_match p scratch engine b off len

(* matches reports whether the pattern matches somewhere in the len bytes of b from off, ascii saying whether that
   text is known to be ASCII (a document knows that of its strings). A pattern that is valid only without the u flag
   goes to the engine, which matches by code unit, when the text has a character beyond the Basic Multilingual
   Plane. *)
let matches p scratch b off len ascii =
  match p.units with
  | Some engine when (not ascii) && has_supplementary b off (off + len) -> engine_match p scratch engine b off len
  | _ -> matches_shape p scratch b off len ascii

(* match_string is matches for a string that is not known to be ASCII. It copies the string. *)
let match_string p scratch s =
  let b = Bytes.of_string s in
  matches p scratch b 0 (Bytes.length b) (is_ascii b 0 (Bytes.length b))

(* ---------------------------------------------------------------------------------------------------------------------
   Reading a pattern. From here on everything runs when a pattern is compiled, and allocates freely.

   Where the Go source reads a pattern as code points (to multiply groups out), this module reads bytes, as the Object
   Pascal port does. Every character that is syntax is ASCII and no byte of a longer character is, so the texts built
   are the same. *)

(* slice is s[from:stop] of Go. *)
let slice s from stop = String.sub s from (stop - from)

(* cut_prefix is s without the prefix, when it starts with it (strings.CutPrefix). *)
let cut_prefix s prefix =
  if String.starts_with ~prefix s then Some (slice s (String.length prefix) (String.length s)) else None

(* cut_suffix is s without the suffix, when it ends with it (strings.CutSuffix). *)
let cut_suffix s suffix =
  if String.ends_with ~suffix s then Some (slice s 0 (String.length s - String.length suffix)) else None

(* byte_or is the byte of s at i, or -1 beyond its end (runeAt of the Go source). *)
let byte_or s i = if i < String.length s then Char.code s.[i] else -1
let is_ascii_digit c = c >= '0' && c <= '9'

let is_ascii_punctuation c =
  (c >= '!' && c <= '/') || (c >= ':' && c <= '@') || (c >= '[' && c <= '`') || (c >= '{' && c <= '~')

(* ends_with_escape reports whether the text ends in an unpaired backslash (so a "$" after it would be escaped). *)
let ends_with_escape t =
  let rec count i n = if i >= 0 && t.[i] = '\\' then count (i - 1) (n + 1) else n in
  count (String.length t - 1) 0 mod 2 = 1

(* parse_count reads a decimal count that fits 32 bits. *)
let parse_count s =
  if s = "" || String.length s > 10 || not (String.for_all is_ascii_digit s) then None
  else
    let n = String.fold_left (fun n c -> (n * 10) + Char.code c - Char.code '0') 0 s in
    if n <= max_uint32 then Some n else None

(* parse_quantifier reads an optional quantifier ("*", "+", "?", "{n}", "{n,}", "{n,m}", each optionally lazy) at i:
   the least and the most, and the position after it. *)
let parse_quantifier b i =
  let finish min max i =
    if min > max then None
    else
      (* Laziness does not change whether the whole pattern matches. *)
      Some (min, max, if i < String.length b && b.[i] = '?' then i + 1 else i)
  in
  if i >= String.length b then Some (1, 1, i)
  else
    match b.[i] with
    | '*' -> finish 0 max_uint32 (i + 1)
    | '+' -> finish 1 max_uint32 (i + 1)
    | '?' -> finish 0 1 (i + 1)
    | '{' -> (
        match String.index_from_opt b i '}' with
        | None -> None
        | Some close -> (
            let body = slice b (i + 1) close in
            let lo, hi =
              match String.index_opt body ',' with
              | Some comma -> (slice body 0 comma, Some (slice body (comma + 1) (String.length body)))
              | None -> (body, None)
            in
            match parse_count lo with
            | None -> None
            | Some min -> (
                match hi with
                | None -> finish min min (close + 1)
                | Some "" -> finish min max_uint32 (close + 1)
                | Some hi -> ( match parse_count hi with None -> None | Some max -> finish min max (close + 1)))))
    | _ -> Some (1, 1, i)

(* class_escape is the set of a class escape ("\d", "\w", their negations, or an escaped punctuation character). None
   for "\s" (not ASCII-only) and anything else. *)
let class_escape c =
  match c with
  | 'd' -> Some digit_set
  | 'D' -> Some (negate digit_set)
  | 'w' -> Some word_set
  | 'W' -> Some (negate word_set)
  | 'n' -> Some (char_single '\n')
  | 'r' -> Some (char_single '\r')
  | 't' -> Some (char_single '\t')
  | c -> if is_ascii_punctuation c then Some (char_single c) else None

exception No_shape

(* parse_class reads a class body from after "[" to after "]", with ASCII members (a negated class also takes every
   non-ASCII character): the set, and the position after the class. *)
let parse_class b i =
  let n = String.length b in
  let negated = i < n && b.[i] = '^' in
  let i = ref (if negated then i + 1 else i) in
  let set = ref empty_set in
  let first = ref true in
  let closed = ref false in
  try
    while not !closed do
      if !i >= n then raise No_shape;
      let c = b.[!i] in
      if c = ']' then begin
        (* "[]" and "[^]" *)
        if !first then raise No_shape;
        incr i;
        closed := true
      end
      else begin
        first := false;
        (* The set holds ASCII members only. A member outside ASCII leaves the pattern to the engine. (This is tested
           before the set is built: a byte of 128 or more is not an entry of it.) *)
        if Char.code c >= rune_self then raise No_shape;
        (* One atom: a single character (for ranges) or a set escape. *)
        let atom, lo =
          if c = '\\' then begin
            if !i + 1 >= n then raise No_shape;
            let e = b.[!i + 1] in
            let atom = match class_escape e with Some s -> s | None -> raise No_shape in
            i := !i + 2;
            (atom, if e = 'n' || e = 'r' || e = 't' || is_ascii_punctuation e then single atom else -1)
          end
          else begin
            incr i;
            (char_single c, Char.code c)
          end
        in
        if !i + 1 < n && b.[!i] = '-' && b.[!i + 1] <> ']' then begin
          if lo < 0 then raise No_shape;
          let hi =
            if b.[!i + 1] = '\\' then begin
              if !i + 2 >= n || not (is_ascii_punctuation b.[!i + 2]) then raise No_shape;
              i := !i + 3;
              b.[!i - 1]
            end
            else begin
              i := !i + 2;
              b.[!i - 1]
            end
          in
          if lo > Char.code hi || Char.code hi >= rune_self then raise No_shape;
          set := union !set (char_range lo (Char.code hi))
        end
        else set := union !set atom
      end
    done;
    Some ((if negated then negate !set else !set), !i)
  with No_shape -> None

(* greedy_before reports whether greedy matching is exact when next can follow the items: every variable item is
   disjoint from the items that can directly follow it (up to the first that cannot match nothing), next included. *)
let greedy_before items next =
  let n = Array.length items in
  (* Whether the variable item is disjoint from what can follow it, from the item at j on. *)
  let rec followed item j =
    if j = n then disjoint item.set next
    else
      let following = items.(j) in
      disjoint item.set following.set && (following.min > 0 || followed item (j + 1))
  in
  let rec from i = i = n || ((items.(i).min = items.(i).max || followed items.(i) (i + 1)) && from (i + 1)) in
  from 0

let greedy_sequence items = { items; to_end = false; pinned = -1; fixed_width = 0 }

let parse_sequence p =
  let n = String.length p in
  let finish items to_end =
    if greedy_before items empty_set then Some { (greedy_sequence items) with to_end }
    else if
      (* Greedy matching is not exact. With both ends anchored and one variable item, the length decides. *)
      not to_end
    then None
    else begin
      let pinned = ref (-1) and fixed_width = ref 0 and one = ref true in
      Array.iteri
        (fun i item ->
          if item.min = item.max then fixed_width := !fixed_width + item.min
          else if !pinned >= 0 then one := false
          else pinned := i)
        items;
      if (not !one) || !fixed_width > max_int32 then None
      else Some { items; to_end = true; pinned = !pinned; fixed_width = !fixed_width }
    end
  in
  let rec items_from i items to_end =
    if i >= n then finish (Array.of_list (List.rev items)) to_end
    else
      match p.[i] with
      | '$' -> if i <> n - 1 then None else items_from (i + 1) items true
      | '[' -> ( match parse_class p (i + 1) with None -> None | Some (set, next) -> quantified set next items to_end)
      | '\\' ->
          if i + 1 >= n then None
          else
            begin match class_escape p.[i + 1] with None -> None | Some set -> quantified set (i + 2) items to_end
            end
      | '.' -> quantified dot_set (i + 1) items to_end
      | '(' | ')' | '|' | '^' | '*' | '+' | '?' | '{' | '}' | ']' -> None
      | c -> quantified (char_single c) (i + 1) items to_end
  and quantified set i items to_end =
    match parse_quantifier p i with
    | None -> None
    | Some (min, max, next) -> items_from next ({ set; min; max } :: items) to_end
  in
  if n = 0 || p.[0] <> '^' || not (text_is_ascii p) then None else items_from 1 [] false

(* literal is the text, when every item is one fixed character. *)
let literal q =
  let text = Buffer.create (Array.length q.items) in
  let fixed item =
    let c = single item.set in
    if c < 0 || item.min <> 1 || item.max <> 1 then false
    else begin
      Buffer.add_char text (Char.chr c);
      true
    end
  in
  if Array.for_all fixed q.items then Some (Buffer.contents text) else None

(* line_range reads "^.{m,n}$" (and "^.*$", "^.+$", "^.{m}$", "^.{m,}$", each also as "^(....)$"): the bounds on the
   length of a line. *)
let line_range p =
  let ( >>= ) = Option.bind in
  (match cut_prefix p "^." with
    | Some q -> Some (q, false)
    | None -> ( match cut_prefix p "^(." with Some q -> Some (q, true) | None -> None))
  >>= fun (q, grouped) ->
  cut_suffix q "$" >>= fun q ->
  (if grouped then cut_suffix q ")" else Some q) >>= fun q ->
  match parse_quantifier q 0 with
  | Some (min, max, next) when next = String.length q && next <> 0 && not (String.ends_with ~suffix:"?" q) ->
      Some (min, max)
  | _ -> None

(* literal_text reads literal text: characters other than syntax characters, and identity or control escapes. *)
let literal_text p =
  let out = Buffer.create (String.length p) in
  let rec from i =
    if i >= String.length p then Some (Buffer.contents out)
    else
      match p.[i] with
      | '\\' ->
          if i + 1 >= String.length p then None
          else
            begin match p.[i + 1] with
            | 'n' -> add '\n' (i + 2)
            | 'r' -> add '\r' (i + 2)
            | 't' -> add '\t' (i + 2)
            | 'f' -> add '\012' (i + 2)
            | 'v' -> add '\011' (i + 2)
            | ('^' | '$' | '\\' | '.' | '*' | '+' | '?' | '(' | ')' | '[' | ']' | '{' | '}' | '|' | '/' | '-') as e ->
                add e (i + 2)
            | _ -> None
            end
      | '^' | '$' | '.' | '*' | '+' | '?' | '(' | ')' | '[' | ']' | '{' | '}' | '|' -> None
      | c -> add c (i + 1)
  and add c next =
    Buffer.add_char out c;
    from next
  in
  from 0

(* split_alternatives splits at top-level "|"s (none inside a group, a class or after a backslash). *)
let split_alternatives p =
  let n = String.length p in
  let rec from i depth in_class start parts =
    if i >= n then Some (List.rev (slice p (min start n) n :: parts))
    else
      match p.[i] with
      | '\\' -> from (i + 2) depth in_class start parts
      | '[' -> from (i + 1) depth true start parts
      | ']' -> from (i + 1) depth false start parts
      | '(' when not in_class -> from (i + 1) (depth + 1) in_class start parts
      | ')' when not in_class -> if depth = 0 then None else from (i + 1) (depth - 1) in_class start parts
      | '|' when (not in_class) && depth = 0 -> from (i + 1) depth in_class (i + 1) (slice p start i :: parts)
      | _ -> from (i + 1) depth in_class start parts
  in
  from 0 0 false 0 []

(* whole_alternatives reads "^(a|b|...)$" or "^(?:a|b|...)$" over literal alternatives. *)
let whole_alternatives p =
  match Option.bind (cut_prefix p "^(") (fun inner -> cut_suffix inner ")$") with
  | None -> None
  | Some inner -> (
      let inner = match cut_prefix inner "?:" with Some rest -> rest | None -> inner in
      if String.starts_with ~prefix:"?" inner then None
      else
        match split_alternatives inner with
        | Some (_ :: _ :: _ as parts) ->
            let texts = List.filter_map literal_text parts in
            if List.length texts = List.length parts then Some (Array.of_list texts) else None
        | _ -> None)

(* At most this many alternatives after expanding groups. *)
let max_alternatives = 64

(* At most this many alternatives when any is a class sequence: beyond it, trying each in turn is slower than one pass
   of the engine. *)
let max_sequence_alternatives = 4

(* product is every concatenation of one of a and one of b. No shape when there are too many. *)
let product a b =
  if List.length a * List.length b > max_alternatives then raise No_shape;
  List.concat_map (fun x -> List.map (fun y -> x ^ y) b) a

(* expand_alternatives expands the alternatives from i up to an unmatched ")" or the end: groups of alternatives
   multiply out, a group quantified by "?" also contributes the empty alternative, and everything else is copied. The
   alternatives, and the position reached. No shape for lookarounds, other quantified groups, or too many
   alternatives. *)
let rec expand_alternatives c i =
  let n = String.length c in
  let all = ref [] in
  let branch = ref [ "" ] in
  let i = ref i in
  let closed = ref false in
  (* The characters that are copied are added to the branches a run at a time, when something else follows the run.
     Adding them one at a time would copy the branch for each, which takes time quadratic in its length. run is where
     the run that has not been added starts, or -1. *)
  let run = ref (-1) in
  let flush stop =
    if !run >= 0 then begin
      let text = slice c !run stop in
      branch := List.map (fun b -> b ^ text) !branch;
      run := -1
    end
  in
  let copied from = if !run < 0 then run := from in
  while (not !closed) && !i < n do
    (match c.[!i] with
    | '|' ->
        flush !i;
        all := !all @ !branch;
        branch := [ "" ];
        incr i
    | ')' -> closed := true
    | '(' ->
        flush !i;
        incr i;
        if byte_or c !i = Char.code '?' then begin
          if byte_or c (!i + 1) <> Char.code ':' then raise No_shape;
          i := !i + 2
        end;
        let inner, next = expand_alternatives c !i in
        if byte_or c next <> Char.code ')' then raise No_shape;
        i := next + 1;
        let skip_lazy () = if byte_or c !i = Char.code '?' then incr i in
        let inner =
          match byte_or c !i with
          | 0x3F (* ? *) ->
              incr i;
              skip_lazy ();
              inner @ [ "" ]
          | 0x7B (* { *) ->
              (* An exact count repeats the group. Any other bound needs a real regular expression. *)
              let close = match String.index_from_opt c !i '}' with Some close -> close | None -> raise No_shape in
              let count =
                match parse_count (slice c (!i + 1) close) with
                | Some count when count <= 16 -> count
                | _ -> raise No_shape
              in
              let repeated = ref [ "" ] in
              for _ = 1 to count do
                repeated := product !repeated inner
              done;
              i := close + 1;
              skip_lazy ();
              !repeated
          | 0x2A (* * *) | 0x2B (* + *) -> raise No_shape
          | _ -> inner
        in
        branch := product !branch inner
    | '[' ->
        let start = !i in
        incr i;
        if byte_or c !i = Char.code '^' then incr i;
        if byte_or c !i = Char.code ']' then incr i;
        while
          if !i >= n then raise No_shape;
          c.[!i] <> ']'
        do
          if c.[!i] = '\\' then incr i;
          incr i
        done;
        incr i;
        copied start
    | '\\' ->
        if !i + 2 > n then raise No_shape;
        copied !i;
        i := !i + 2
    | _ ->
        copied !i;
        incr i);
    if (not !closed) && List.length !all + List.length !branch > max_alternatives then raise No_shape
  done;
  flush !i;
  (!all @ !branch, !i)

(* An alternative with no "^" before it and no "$" after it is looked for at every position of the text, and each try
   costs up to the length of its literal or the width of its sequence. Such a search has no limit of time, so the
   alternatives of a pattern may cost at most this much for each position between them. A pattern beyond it has no
   faster matcher and runs on the engine, whose matches have the limit. *)
let max_search_width = 256

(* search_width is what an alternative costs at each position of a text it is searched for in. *)
let search_width = function
  | Alt_literal { text; at_start = false; at_end = false } -> String.length text
  | Alt_anywhere { width; _ } -> width
  | _ -> 0

(* parse_alternatives reads two or more alternatives once groups of alternatives (and optional groups) are expanded
   into whole alternatives ("^a|b|c$", "^([a|A]uto)|([n|N]one)$", "^[Ee][Ss]2015(\.([Cc]ore|[Pp]roxy))?$"), each a
   literal or a class sequence anchored where its own "^" and "$" say. *)
let parse_alternatives p =
  let sequences = ref false in
  let alternative part =
    let at_start = String.starts_with ~prefix:"^" part in
    let body = if at_start then slice part 1 (String.length part) else part in
    let body, at_end =
      match cut_suffix body "$" with Some r when not (ends_with_escape r) -> (r, true) | _ -> (body, false)
    in
    match literal_text body with
    | Some text -> Alt_literal { text; at_start; at_end }
    | None -> (
        match parse_sequence ("^" ^ body ^ if at_end then "$" else "") with
        | None -> raise No_shape
        | Some seq ->
            sequences := true;
            if at_start then Alt_start seq
            else begin
              if not (Array.for_all (fun item -> item.min = item.max) seq.items) then raise No_shape;
              let width = Array.fold_left (fun width item -> width + item.min) 0 seq.items in
              if at_end then Alt_end { seq; width } else Alt_anywhere { seq; width }
            end)
  in
  try
    let parts, stop = expand_alternatives p 0 in
    (* A single alternative is only new here when a group was expanded (parse_sequence takes the rest). *)
    if stop <> String.length p || parts = [ p ] then None
    else
      let alts = Array.of_list (List.map alternative parts) in
      if !sequences && Array.length alts > max_sequence_alternatives then None
      else if Array.fold_left (fun width alt -> width + search_width alt) 0 alts > max_search_width then None
      else Some alts
  with No_shape -> None

(* excluded_class_with_word reads "^(?=[^SET]+$)(?=(.*\w)).+$" (with "(?:" or "(" around ".*\w"): the excluded set,
   and whether a run of "!" comes first. *)
let excluded_class_with_word p =
  (* parse_class reads a class body from after "[", here "^SET]". *)
  let body, bangs =
    match cut_prefix p "^(?=!+[" with Some body -> (Some body, true) | None -> (cut_prefix p "^(?=[", false)
  in
  match body with
  | None -> None
  | Some body -> (
      match parse_class body 0 with
      | Some (negated, next) when String.starts_with ~prefix:"^" body -> (
          let excluded = complement_ascii negated in
          (* The run of "!" ends exactly where the class starts only when the class excludes "!". *)
          if bangs && not (contains excluded (Char.code '!')) then None
          else
            match slice body next (String.length body) with
            | "+$)(?=(.*\\w)).+$" | "+$)(?=(?:.*\\w)).+$" | "+$)(?=.*\\w).+$" -> Some (excluded, bangs)
            | _ -> None)
      | _ -> None)

(* strip_group is the inside of "(...)" or "(?:...)". None for other groups. *)
let strip_group g =
  match Option.bind (cut_prefix g "(") (fun inner -> cut_suffix inner ")") with
  | None -> None
  | Some inner -> (
      match cut_prefix inner "?:" with
      | Some rest -> Some rest
      | None -> if String.starts_with ~prefix:"?" inner then None else Some inner)

(* unwrap_group is a text that is one group wrapping everything, unwrapped. Otherwise the text itself. *)
let unwrap_group t =
  if not (String.starts_with ~prefix:"(" t && String.ends_with ~suffix:")" t) then Some t
  else
    match strip_group t with
    | None -> None
    | Some inner ->
        (* Only when the parentheses enclose the whole text ("(a)(b)" is two groups). *)
        let rec balanced i depth escaped =
          i >= String.length inner
          ||
          if escaped then balanced (i + 1) depth false
          else
            match inner.[i] with
            | '\\' -> balanced (i + 1) depth true
            | '(' -> balanced (i + 1) (depth + 1) false
            | ')' -> depth > 0 && balanced (i + 1) (depth - 1) false
            | _ -> balanced (i + 1) depth false
        in
        if balanced 0 0 false then Some inner else None

let parse_separated_list p =
  let ( >>= ) = Option.bind in
  cut_prefix p "^" >>= fun body ->
  cut_suffix body "$" >>= fun body ->
  if ends_with_escape body then None
  else
    let n = String.length body in
    (* Top-level groups: the index of each one's "(" and ")". None for a ")" with no "(". *)
    let rec groups_from i depth in_class opened groups =
      if i >= n then Some (List.rev groups)
      else
        match body.[i] with
        | '\\' -> groups_from (i + 2) depth in_class opened groups
        | '[' when not in_class -> groups_from (i + 1) depth true opened groups
        | ']' when in_class -> groups_from (i + 1) depth false opened groups
        | '(' when not in_class -> groups_from (i + 1) (depth + 1) in_class (if depth = 0 then i else opened) groups
        | ')' when not in_class ->
            if depth = 0 then None
            else groups_from (i + 1) (depth - 1) in_class opened (if depth = 1 then (opened, i) :: groups else groups)
        | _ -> groups_from (i + 1) depth in_class opened groups
    in
    groups_from 0 0 false 0 [] >>= fun groups ->
    let quantified (_, close) = close + 1 < n && (body.[close + 1] = '*' || body.[close + 1] = '+') in
    match List.filter quantified groups with
    | [ (opened, close) ] -> (
        let min_repeats = if body.[close + 1] = '+' then 1 else 0 in
        strip_group (slice body opened (close + 1)) >>= fun inner ->
        let before = slice body 0 opened and after = slice body (close + 2) n in
        (* The items of a text that is a class sequence. A "$" at the end of the text is not one of them, and the list
           would lose it, so such a text has no items. (The Go source drops that "$", and with it takes "^x(,a$)*$" for
           "^x(,a)*$".) *)
        let items t =
          match parse_sequence ("^" ^ t) with Some seq when not seq.to_end -> Some seq.items | _ -> None
        in
        let fixed items =
          Array.length items <> 0 && Array.for_all (fun item -> item.min = item.max && item.min <> 0) items
        in
        let split group_items k = (Array.sub group_items 0 k, Array.sub group_items k (Array.length group_items - k)) in
        match items inner with
        | None | Some [||] -> None
        | Some group_items ->
            (* The first k for which the split is a list, from 1 up. *)
            let rec search k try_split =
              if k >= Array.length group_items then None
              else match try_split k with Some _ as list -> list | None -> search (k + 1) try_split
            in
            if before <> "" && after = "" then
              (* ^I(SR)*$ *)
              unwrap_group before >>= items >>= fun first ->
              search 1 (fun k ->
                  let separator, repeated = split group_items k in
                  let next = separator.(0).set in
                  if fixed separator && greedy_before first next && greedy_before repeated next then
                    Some
                      {
                        first = Some (greedy_sequence first);
                        repeated = greedy_sequence repeated;
                        separator = greedy_sequence separator;
                        final = None;
                        min_repeats;
                      }
                  else None)
            else if before = "" && after <> "" then
              (* ^(RS)*F$ *)
              unwrap_group after >>= fun unwrapped ->
              parse_sequence ("^" ^ unwrapped ^ "$") >>= fun final ->
              search 1 (fun k ->
                  let repeated, separator = split group_items k in
                  if fixed separator && greedy_before repeated separator.(0).set then
                    Some
                      {
                        first = None;
                        repeated = greedy_sequence repeated;
                        separator = greedy_sequence separator;
                        final = Some final;
                        min_repeats;
                      }
                  else None)
            else None)
    | _ -> None

let rec choose_pattern p =
  match p with
  (* Unanchored (or start-anchored) ".*" finds an empty match in any string. "^.*$" does not ("." stops at a line
     terminator), so it is not listed. *)
  | "" | ".*" | "^.*" | ".*$" | "(.*)" | "^(.*)" | "[\\s\\S]*" | "^[\\s\\S]*" | "^[\\s\\S]*$" -> Some Everything
  | ".+" | "." | "(.+)" -> Some (Has_content { start = false })
  | "^.+" | "^." -> Some (Has_content { start = true })
  | _ -> (
      (* "^X.*" (no "$") matches exactly where "^X" does: ".*" can match nothing. *)
      let without_tail =
        match cut_suffix p ".*" with
        | Some rest
          when String.starts_with ~prefix:"^" rest
               && String.length rest > 1
               && (not (ends_with_escape rest))
               && not (String.contains "*+?}|(^" rest.[String.length rest - 1]) ->
            choose_pattern rest
        | _ -> None
      in
      match without_tail with Some _ as m -> m | None -> choose_shape p)

and choose_shape p =
  match line_range p with
  | Some (min, max) -> Some (Line { min; max })
  | None -> (
      match whole_alternatives p with
      | Some texts -> Some (Literals (make_literals texts))
      | None -> (
          match parse_alternatives p with
          | Some alts -> Some (Alternatives alts)
          | None -> (
              match parse_separated_list p with
              | Some list -> Some (Separated_list list)
              | None -> (
                  match excluded_class_with_word p with
                  | Some (set, bangs) -> Some (Excluded_class_with_word { set; bangs })
                  | None -> (
                      match parse_sequence p with
                      | Some seq -> (
                          match literal seq with
                          | Some text -> Some (Literal { text; whole = seq.to_end })
                          | None -> Some (Sequence seq))
                      | None -> None)))))

(* shape is the faster matcher of a pattern, if it has one, with no test of the pattern's validity and no engine. For
   the tests. *)
let shape source =
  match choose_pattern source with Some matcher -> Some { source; matcher; units = None } | None -> None

(* compile_checked compiles a pattern that is under the limits of size and valid, unicode saying whether it is valid
   with the u flag. *)
let compile_checked source unicode =
  (* Validity is ECMA-262's: a pattern the engine rejects is an error, whichever matcher would run it. A pattern with a
     faster matcher that is valid with the u flag needs no engine at all. A pattern that is valid only without the u
     flag is matched by UTF-16 code unit. The faster matchers read code points, which is the same thing unless the text
     has a character beyond the Basic Multilingual Plane, so such a pattern keeps its engine for those texts. When the
     pattern itself has such a character it is two atoms, which no faster matcher reads, and the engine takes every
     text. *)
  match choose_pattern source with
  | Some matcher when unicode -> Ok { source; matcher; units = None }
  | chosen -> (
      match Pattern_engine.compile source with
      | Error e -> Error e
      | Ok engine -> (
          match chosen with
          | Some matcher when not (String.exists (fun c -> c >= '\xF0') source) ->
              Ok { source; matcher; units = Some engine }
          | _ -> Ok { source; matcher = Engine engine; units = None }))

(* compile compiles a pattern. The error says whether it is not a valid ECMA-262 regular expression or is over a
   limit of size. *)
let compile source =
  (* The limits of size come first: nothing below reads a pattern that is over one. *)
  match Pattern_engine.check source with
  | Error e -> Error e
  | Ok unicode -> compile_checked source unicode
