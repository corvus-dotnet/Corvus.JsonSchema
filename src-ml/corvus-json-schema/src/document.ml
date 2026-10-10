(* A document is JSON text parsed for evaluation: the UTF-8 text and one flat array of values (a tape) that the
   evaluator reads in place, with no block per value. Strings stay in the text where they have no escapes. A port of
   document.go of the Go module.

   {1 How the 64-bit quantities of the Go code are held}

   Go's tape is two uint64 words for each value. OCaml's [int] is 63 bits, and an [Int64] or a [float] stored in a
   polymorphic structure is a block of its own, which the evaluator must never make while it validates. So:

   - The tape is an [int array], two words for each value. The first word is the header: the kind in bits 0 to 7, the
     flags in bits 8 to 15 and a field from bit 16 up (a string's byte length, a number's offset in the source, a
     container's count). Go keeps a 32-bit field in the high half of a 64-bit word. Here the field has the 47 bits
     that remain. The second word is the data: a string's offset, a container's first child, a boolean's 0 or 1, and
     for a number what its class says.

   - Go classifies a number at parse as an int64, a uint64 beyond an int64, or a float64, and holds any of them in
     the data word. Here a number is one of three classes, in the low two bits of the flags:

     - [Numbers.num_int]: an integer that fits an [int], from -2^62 to 2^62 - 1. The data word is the integer itself.
     - [Numbers.num_big]: any other integer from -2^63 to 2^64 - 1, which are the integers Go keeps exact. Each is
       (top - 2) * 2^62 + low with low in [0, 2^62): top is 0 for an integer below -2^62, and 3, 4 or 5 for one at
       or above 2^62. The data word is low and top is in the flags above the class. An [int] has the same form with
       top 1 or 2, so comparing (top, low) pairs orders every integer exactly, with no arithmetic wider than an
       [int].
     - [Numbers.num_float]: everything else (a fraction, an exponent, an integer beyond 64 bits, and -0). The value
       is in the document's [floats], a [Float.Array.t], which is flat, and the data word is its index. A comparison
       reads the float where it is compared, so no float is boxed.

     Equality, ordering, multipleOf and hashing over these are in numbers.ml and values.ml, and are exact for every
     class against every other: an integer is compared with a float by the float's floor, and floats of 2^62 and
     above are integers, split into the same (top, low) form.

   - The parser reads up to 18 digits into an [int] (Go reads 19 into a uint64). A longer integer is read again into
     two 32-bit halves to classify it. A decimal that Clinger's fast path cannot convert goes to
     [Numbers.decimal_to_float], whose 64-bit words are local [Int64] values that the native compiler keeps in
     registers.

   - Go's hashes are 64 bits. Here they are 63, from a multiplier that fits an [int]. Eight bytes of text are read
     as an [Int64] and folded to an [int] where they are read.

   The text is a [Bytes.t] ([source]) and so are the unescaped strings ([text]), so that one reader serves both.
   Nothing writes to the source. *)

(* The deepest nesting of arrays and objects a document may have. *)
let max_depth = 1000

(* The kinds of a value. They are the evaluator's type bits, so a type test is one mask operation. *)
let kind_null = 1
let kind_bool = 2
let kind_object = 4
let kind_array = 8
let kind_number = 16
let kind_string = 32

(* String flags. *)

(* str_text says the string's bytes are in [text] (it had escapes), not in [source]. *)
let str_text = 1

(* str_wide says the string has bytes outside ASCII. *)
let str_wide = 2

type t = {
  (* Two words per value (see above). The children of a container are consecutive. An object's children are key and
     value pairs. The arrays of a document that a parser fills again may be longer than what is in use. *)
  mutable tape : int array;
  mutable source : Bytes.t;
  (* The unescaped strings. *)
  mutable text : Bytes.t;
  (* The values of the numbers of the float class. *)
  mutable floats : Float.Array.t;
  mutable root : int;
}

type error = { message : string; offset : int }

let error_to_string e = "invalid JSON at offset " ^ string_of_int e.offset ^ ": " ^ e.message

(* ---------------------------------------------------------------------------------------------------------------------
   Reading values (the evaluator's side). A value is the index of its node. *)

let root d = d.root
let kind d n = d.tape.(n lsl 1) land 0xff
let flags d n = (d.tape.(n lsl 1) lsr 8) land 0xff

(* count is a container's item or property count, or a string's byte length. *)
let count d n = d.tape.(n lsl 1) lsr 16
let data d n = d.tape.((n lsl 1) + 1)

(* first is a container's first child. *)
let first d n = d.tape.((n lsl 1) + 1)
let boolean d n = d.tape.((n lsl 1) + 1) <> 0

(* The bytes of a string value are [str_len] bytes of [str_bytes] from [str_off]. *)
let str_bytes d n = if d.tape.(n lsl 1) land (str_text lsl 8) <> 0 then d.text else d.source
let str_off d n = d.tape.((n lsl 1) + 1)
let str_len d n = d.tape.(n lsl 1) lsr 16
let str_ascii d n = d.tape.(n lsl 1) land (str_wide lsl 8) = 0
let floats d = d.floats
let source d = d.source

(* string_value is a copy of a string value. *)
let string_value d n = Bytes.sub_string (str_bytes d n) (str_off d n) (str_len d n)

(* bytes_equal compares two ranges of the same length. *)
let bytes_equal a aoff b boff len =
  let i = ref 0 in
  while !i < len && Bytes.get a (aoff + !i) = Bytes.get b (boff + !i) do
    incr i
  done;
  !i = len

(* bytes_equal_string reports whether a range is the bytes of a string. *)
let bytes_equal_string a aoff len (s : string) =
  len = String.length s
  &&
  let i = ref 0 in
  while !i < len && Bytes.get a (aoff + !i) = String.get s !i do
    incr i
  done;
  !i = len

(* str_equals reports whether a string value is the given string. *)
let str_equals d n (s : string) = bytes_equal_string (str_bytes d n) (str_off d n) (str_len d n) s

(* str_equal reports whether two string values are equal. *)
let str_equal a x b y =
  let len = str_len a x in
  len = str_len b y && bytes_equal (str_bytes a x) (str_off a x) (str_bytes b y) (str_off b y) len

(* property is the value of an object's property, or -1. *)
let property d obj (name : string) =
  let k = ref (first d obj) in
  let i = ref (count d obj) in
  let found = ref (-1) in
  while !found < 0 && !i > 0 do
    if str_equals d !k name then found := !k + 1;
    k := !k + 2;
    decr i
  done;
  !found

(* find_property is the value of the property of an object named by a range of bytes, or -1. *)
let find_property d obj key koff klen =
  let k = ref (first d obj) in
  let i = ref (count d obj) in
  let found = ref (-1) in
  while !found < 0 && !i > 0 do
    if str_len d !k = klen && bytes_equal (str_bytes d !k) (str_off d !k) key koff klen then found := !k + 1;
    k := !k + 2;
    decr i
  done;
  !found

(* float_value is a number's value as a float. *)
let float_value d n = Numbers.to_float (flags d n) (data d n) d.floats
let is_integer d n = Numbers.is_integer (flags d n) (data d n) d.floats

(* compare_numbers compares two number values exactly: negative, zero or positive. *)
let compare_numbers a x b y = Numbers.compare (flags a x) (data a x) a.floats (flags b y) (data b y) b.floats

(* number_end is the end of the number whose text starts at j (already validated). *)
let number_end b j =
  let j = ref j in
  let len = Bytes.length b in
  while
    !j < len
    &&
    let c = Bytes.get b !j in
    (c >= '0' && c <= '9') || c = '-' || c = '+' || c = '.' || c = 'e' || c = 'E'
  do
    incr j
  done;
  !j

(* number_start is the offset in the source of a number's text as written. *)
let number_start d n = count d n

(* number_text is a number's text as written. *)
let number_text d n =
  let start = count d n in
  Bytes.sub_string d.source start (number_end d.source start - start)

let add_quoted out b off len =
  let hex = "0123456789abcdef" in
  Buffer.add_char out '"';
  for i = off to off + len - 1 do
    match Bytes.get b i with
    | '"' -> Buffer.add_string out "\\\""
    | '\\' -> Buffer.add_string out "\\\\"
    | '\n' -> Buffer.add_string out "\\n"
    | '\r' -> Buffer.add_string out "\\r"
    | '\t' -> Buffer.add_string out "\\t"
    | '\b' -> Buffer.add_string out "\\b"
    | '\012' -> Buffer.add_string out "\\f"
    | c when c < ' ' ->
        Buffer.add_string out "\\u00";
        Buffer.add_char out hex.[Char.code c lsr 4];
        Buffer.add_char out hex.[Char.code c land 15]
    | c -> Buffer.add_char out c
  done;
  Buffer.add_char out '"'

(* add_json appends the value as compact JSON text (numbers as written, strings escaped again). *)
let rec add_json d out n =
  let k = kind d n in
  if k = kind_null then Buffer.add_string out "null"
  else if k = kind_bool then Buffer.add_string out (if boolean d n then "true" else "false")
  else if k = kind_number then Buffer.add_string out (number_text d n)
  else if k = kind_string then add_quoted out (str_bytes d n) (str_off d n) (str_len d n)
  else if k = kind_array then begin
    Buffer.add_char out '[';
    let c = first d n in
    for i = 0 to count d n - 1 do
      if i > 0 then Buffer.add_char out ',';
      add_json d out (c + i)
    done;
    Buffer.add_char out ']'
  end
  else begin
    Buffer.add_char out '{';
    let c = first d n in
    for i = 0 to count d n - 1 do
      if i > 0 then Buffer.add_char out ',';
      let key = c + (2 * i) in
      add_quoted out (str_bytes d key) (str_off d key) (str_len d key);
      Buffer.add_char out ':';
      add_json d out (key + 1)
    done;
    Buffer.add_char out '}'
  end

let json_of d n =
  let out = Buffer.create 64 in
  add_json d out n;
  Buffer.contents out

let to_string d = json_of d d.root

(* ---------------------------------------------------------------------------------------------------------------------
   Hashing and sorting, for the parser's duplicate keys and for uniqueItems. *)

(* The multiplier of the hashes: Go's 0x9e3779b97f4a7c15 cut to what an [int] holds. *)
let hash_k = 0x1e3779b97f4a7c15

(* A rotation of the 63 bits of an [int]. *)
let[@inline] rotate5 h = (h lsl 5) lor (h lsr 58)

(* Eight bytes as an [int]: the low 63 bits, with the top bit of the last byte folded in so that every bit counts. *)
let[@inline] word64 b i =
  let w = Bytes.get_int64_le b i in
  Int64.to_int w lxor (Int64.to_int (Int64.shift_right_logical w 63) * 0x2545f4914f6cdd1d)

let[@inline] word32 b i = Int32.to_int (Bytes.get_int32_le b i) land 0xffffffff

(* str_hash hashes a range of bytes, eight bytes at a time. *)
let str_hash b off n =
  let h = ref (n * hash_k) in
  let i = ref 0 in
  while !i + 8 <= n do
    h := rotate5 !h lxor word64 b (off + !i) * hash_k;
    i := !i + 8
  done;
  (* The tail as one word: the last eight bytes when there are that many (overlapping the words already hashed),
     else the overlapping first and last four, else the bytes themselves. *)
  let tail =
    if n >= 8 then word64 b (off + n - 8)
    else if n >= 4 then word32 b off lxor (word32 b (off + n - 4) lsl 31)
    else begin
      let t = ref 0 in
      for k = 0 to n - 1 do
        t := (!t lsl 8) lor Char.code (Bytes.get b (off + k))
      done;
      !t
    end
  in
  rotate5 !h lxor tail * hash_k

let sift (a : int array) root stop =
  let root = ref root in
  let continue = ref true in
  while !continue do
    let child = (2 * !root) + 1 in
    if child >= stop then continue := false
    else begin
      let child = if child + 1 < stop && a.(child) < a.(child + 1) then child + 1 else child in
      if a.(!root) < a.(child) then begin
        let t = a.(!root) in
        a.(!root) <- a.(child);
        a.(child) <- t;
        root := child
      end
      else continue := false
    end
  done

(* sort_ints sorts the first n elements of an array in place, allocating nothing (a heap sort). *)
let sort_ints (a : int array) n =
  for start = (n / 2) - 1 downto 0 do
    sift a start n
  done;
  for stop = n - 1 downto 1 do
    let t = a.(0) in
    a.(0) <- a.(stop);
    a.(stop) <- t;
    sift a 0 stop
  done

(* grow_ints is an array of at least need elements that starts with the used elements of a. *)
let grow_ints (a : int array) used need =
  let grown = Array.make (Int.max need ((2 * Array.length a) + 16)) 0 in
  Array.blit a 0 grown 0 used;
  grown

(* utf8_valid reports whether the bytes from start to stop are well-formed UTF-8 (RFC 3629: no overlong form, no
   surrogate, nothing above U+10FFFF). *)
let utf8_valid b start stop =
  let i = ref start in
  let ok = ref true in
  while !ok && !i < stop do
    let c = Char.code (Bytes.get b !i) in
    if c < 0x80 then incr i
    else if c < 0xc2 then ok := false
    else if c < 0xe0 then
      if !i + 1 < stop && Char.code (Bytes.get b (!i + 1)) land 0xc0 = 0x80 then i := !i + 2 else ok := false
    else if c < 0xf0 then
      if !i + 2 < stop then begin
        let c1 = Char.code (Bytes.get b (!i + 1)) and c2 = Char.code (Bytes.get b (!i + 2)) in
        let lo = if c = 0xe0 then 0xa0 else 0x80 and hi = if c = 0xed then 0x9f else 0xbf in
        if c1 >= lo && c1 <= hi && c2 land 0xc0 = 0x80 then i := !i + 3 else ok := false
      end
      else ok := false
    else if c < 0xf5 then
      if !i + 3 < stop then begin
        let c1 = Char.code (Bytes.get b (!i + 1))
        and c2 = Char.code (Bytes.get b (!i + 2))
        and c3 = Char.code (Bytes.get b (!i + 3)) in
        let lo = if c = 0xf0 then 0x90 else 0x80 and hi = if c = 0xf4 then 0x8f else 0xbf in
        if c1 >= lo && c1 <= hi && c2 land 0xc0 = 0x80 && c3 land 0xc0 = 0x80 then i := !i + 4 else ok := false
      end
      else ok := false
    else ok := false
  done;
  !ok

(* ---------------------------------------------------------------------------------------------------------------------
   The parser *)

(* A parser builds a document. Its buffers are reused from one parse to the next. Each buffer is an array and the
   number of its elements in use. *)
type parser = {
  mutable b : Bytes.t;
  mutable i : int;
  (* The end of the text in b. *)
  mutable stop : int;
  (* Checking syntax only. No document is built and numbers are not converted. *)
  mutable validating : bool;
  (* Finished children of closed containers, each container's consecutive (two words per node). *)
  mutable nodes : int array;
  mutable nnodes : int;
  (* The values of the open containers, innermost last. A container's run moves to nodes when it closes. *)
  mutable scratch : int array;
  mutable nscratch : int;
  (* The scratch index (in nodes) at which each open container's children start, with the object flag above it. *)
  mutable frames : int array;
  mutable nframes : int;
  mutable text : Bytes.t;
  mutable ntext : int;
  mutable floats : Float.Array.t;
  mutable nfloats : int;
  (* Scratch for finding duplicate keys in large objects. *)
  mutable hashes : int array;
  (* Scratch for rebuilding an object with duplicate keys. *)
  mutable pairs : int array;
  mutable err_message : string;
  mutable err_offset : int;
}

(* Raised, with the message and the offset left in the parser, by text that is not JSON. It carries nothing, so
   raising it allocates nothing. *)
exception Failed

let create_parser () =
  {
    b = Bytes.empty;
    i = 0;
    stop = 0;
    validating = false;
    nodes = [||];
    nnodes = 0;
    scratch = [||];
    nscratch = 0;
    frames = [||];
    nframes = 0;
    text = Bytes.empty;
    ntext = 0;
    floats = Float.Array.create 0;
    nfloats = 0;
    hashes = [||];
    pairs = [||];
    err_message = "";
    err_offset = 0;
  }

let fail p message at =
  p.err_message <- message;
  p.err_offset <- at;
  raise_notrace Failed

let reset p b start stop validating =
  p.b <- b;
  p.i <- start;
  p.stop <- stop;
  p.validating <- validating;
  p.nnodes <- 0;
  p.nscratch <- 0;
  p.nframes <- 0;
  p.ntext <- 0;
  p.nfloats <- 0

let skip_ws p =
  let b = p.b in
  let len = p.stop in
  let i = ref p.i in
  while
    !i < len
    &&
    let c = Bytes.get b !i in
    c = ' ' || c = '\n' || c = '\r' || c = '\t'
  do
    incr i
  done;
  p.i <- !i

let peek p = if p.i < p.stop then Char.code (Bytes.get p.b p.i) else -1

let push p header data =
  let n = p.nscratch in
  if n + 2 > Array.length p.scratch then p.scratch <- grow_ints p.scratch n (n + 2);
  p.scratch.(n) <- header;
  p.scratch.(n + 1) <- data;
  p.nscratch <- n + 2

let object_frame = 1 lsl 40

let push_frame p frame =
  let n = p.nframes in
  if n + 1 > Array.length p.frames then p.frames <- grow_ints p.frames n (n + 1);
  p.frames.(n) <- frame;
  p.nframes <- n + 1

let reserve_text p extra =
  let need = p.ntext + extra in
  if need > Bytes.length p.text then begin
    let grown = Bytes.create (Int.max need ((2 * Bytes.length p.text) + 64)) in
    Bytes.blit p.text 0 grown 0 p.ntext;
    p.text <- grown
  end

let append_text p b from stop =
  let n = stop - from in
  if n > 0 then begin
    reserve_text p n;
    Bytes.blit b from p.text p.ntext n;
    p.ntext <- p.ntext + n
  end

let append_byte p c =
  reserve_text p 1;
  Bytes.set p.text p.ntext (Char.chr c);
  p.ntext <- p.ntext + 1

let append_rune p cp =
  if cp < 0x80 then append_byte p cp
  else if cp < 0x800 then begin
    append_byte p (0xc0 lor (cp lsr 6));
    append_byte p (0x80 lor (cp land 0x3f))
  end
  else if cp < 0x10000 then begin
    append_byte p (0xe0 lor (cp lsr 12));
    append_byte p (0x80 lor ((cp lsr 6) land 0x3f));
    append_byte p (0x80 lor (cp land 0x3f))
  end
  else begin
    append_byte p (0xf0 lor (cp lsr 18));
    append_byte p (0x80 lor ((cp lsr 12) land 0x3f));
    append_byte p (0x80 lor ((cp lsr 6) land 0x3f));
    append_byte p (0x80 lor (cp land 0x3f))
  end

(* The bytes of the key with a header and an offset: where they are. *)
let key_bytes p h = if h land (str_text lsl 8) <> 0 then p.text else p.b

(* key_equals compares the keys at scratch value indexes x and y. *)
let key_equals p x y =
  let hx = p.scratch.(x * 2) and hy = p.scratch.(y * 2) in
  hx lsr 16 = hy lsr 16
  && bytes_equal (key_bytes p hx) p.scratch.((x * 2) + 1) (key_bytes p hy) p.scratch.((y * 2) + 1) (hx lsr 16)

(* dedupe keeps, of duplicate property names in the object whose pairs start at start, the last value at the first
   position. *)
let dedupe p start =
  let count = ((p.nscratch / 2) - start) / 2 in
  let duplicate = ref false in
  if count <= 16 then begin
    let j = ref 1 in
    while (not !duplicate) && !j < count do
      let k = ref 0 in
      while (not !duplicate) && !k < !j do
        if key_equals p (start + (2 * !k)) (start + (2 * !j)) then duplicate := true;
        incr k
      done;
      incr j
    done
  end
  else begin
    (* Sorted by hash in reused scratch: only keys with equal hashes are compared. *)
    if Array.length p.hashes < count then p.hashes <- Array.make (Int.max count (2 * Array.length p.hashes)) 0;
    let h = p.hashes in
    for j = 0 to count - 1 do
      let at = (start + (2 * j)) * 2 in
      let header = p.scratch.(at) in
      h.(j) <- str_hash (key_bytes p header) p.scratch.(at + 1) (header lsr 16) land lnot 0xffffffff lor j
    done;
    sort_ints h count;
    let a = ref 0 in
    while (not !duplicate) && !a < count do
      let e = ref (!a + 1) in
      while !e < count && h.(!e) asr 32 = h.(!a) asr 32 do
        incr e
      done;
      let x = ref (!a + 1) in
      while (not !duplicate) && !x < !e do
        let y = ref !a in
        while (not !duplicate) && !y < !x do
          if key_equals p (start + (2 * (h.(!x) land 0xffffffff))) (start + (2 * (h.(!y) land 0xffffffff))) then
            duplicate := true;
          incr y
        done;
        incr x
      done;
      a := !e
    done
  end;
  if !duplicate then begin
    let words = p.nscratch - (start * 2) in
    if Array.length p.pairs < words then p.pairs <- Array.make (Int.max words (2 * Array.length p.pairs)) 0;
    let pairs = p.pairs in
    Array.blit p.scratch (start * 2) pairs 0 words;
    p.nscratch <- start * 2;
    for q = 0 to count - 1 do
      let header = pairs.(q * 4) in
      let name = key_bytes p header and off = pairs.((q * 4) + 1) and len = header lsr 16 in
      let at = ref (-1) in
      let k = ref start in
      while !at < 0 && !k < p.nscratch / 2 do
        let other = p.scratch.(!k * 2) in
        if other lsr 16 = len && bytes_equal (key_bytes p other) p.scratch.((!k * 2) + 1) name off len then at := !k;
        k := !k + 2
      done;
      if !at >= 0 then begin
        p.scratch.((!at + 1) * 2) <- pairs.((q * 4) + 2);
        p.scratch.(((!at + 1) * 2) + 1) <- pairs.((q * 4) + 3)
      end
      else begin
        push p pairs.(q * 4) pairs.((q * 4) + 1);
        push p pairs.((q * 4) + 2) pairs.((q * 4) + 3)
      end
    done
  end

(* close moves the closed container's children to nodes and pushes the container in their place. *)
let close p start is_object =
  p.nframes <- p.nframes - 1;
  if is_object && (p.nscratch / 2) - start > 2 && not p.validating then dedupe p start;
  let children = (p.nscratch / 2) - start in
  let first = p.nnodes / 2 in
  let words = children * 2 in
  if p.nnodes + words > Array.length p.nodes then p.nodes <- grow_ints p.nodes p.nnodes (p.nnodes + words);
  Array.blit p.scratch (start * 2) p.nodes p.nnodes words;
  p.nnodes <- p.nnodes + words;
  p.nscratch <- start * 2;
  if is_object then push p (kind_object lor ((children / 2) lsl 16)) first
  else push p (kind_array lor (children lsl 16)) first

let literal p (word : string) kind data =
  let n = String.length word in
  if p.i + n > p.stop || not (bytes_equal_string p.b p.i n word) then fail p "expected a value" p.i;
  p.i <- p.i + n;
  push p kind data

(* scan is, by byte: 0 for plain ASCII, 1 for a quote, backslash or control character, 2 for a non-ASCII byte. *)
let scan =
  String.init 256 (fun c ->
      if c < 0x20 || c = Char.code '"' || c = Char.code '\\' then '\001' else if c >= 0x80 then '\002' else '\000')

let hex4 p at =
  if at + 4 > p.stop then -1
  else begin
    let v = ref 0 in
    for k = at to at + 3 do
      let c = Bytes.get p.b k in
      let d =
        if c >= '0' && c <= '9' then Char.code c - 48
        else if c >= 'a' && c <= 'f' then Char.code c - 87
        else if c >= 'A' && c <= 'F' then Char.code c - 55
        else -1
      in
      v := if !v < 0 || d < 0 then -1 else (!v * 16) + d
    done;
    !v
  end

(* unicode_escape reads a \u escape at j (and its low surrogate, for a high one): the code point. *)
let unicode_escape p j =
  let u = hex4 p (j + 2) in
  if u < 0 then fail p "invalid \\u escape" j;
  if u >= 0xd800 && u <= 0xdbff then begin
    let low =
      if j + 7 < p.stop && Bytes.get p.b (j + 6) = '\\' && Bytes.get p.b (j + 7) = 'u' then hex4 p (j + 8) else -1
    in
    if low >= 0xdc00 && low <= 0xdfff then 0x10000 + ((u - 0xd800) lsl 10) + (low - 0xdc00)
    else fail p "lone leading surrogate in hex escape" j
  end
  else if u >= 0xdc00 && u <= 0xdfff then fail p "lone trailing surrogate in hex escape" j
  else u

(* escaped reads the rest of a string with escapes, unescaped into the text buffer. j is at the first backslash. *)
let escaped p start j wide =
  let offset = p.ntext in
  let b = p.b in
  let len = p.stop in
  let run = ref start in
  let j = ref j in
  let wide = ref wide in
  let finished = ref false in
  while not !finished do
    if !j >= len then fail p "unterminated string" !j;
    let c = Bytes.get b !j in
    if c = '"' then begin
      if !wide && not (utf8_valid b !run !j) then fail p "invalid UTF-8" !run;
      append_text p b !run !j;
      p.i <- !j + 1;
      let header = kind_string lor (str_text lsl 8) lor ((p.ntext - offset) lsl 16) in
      push p (if !wide then header lor (str_wide lsl 8) else header) offset;
      finished := true
    end
    else if c = '\\' then begin
      if !wide && not (utf8_valid b !run !j) then fail p "invalid UTF-8" !run;
      append_text p b !run !j;
      let e = if !j + 1 < len then Bytes.get b (!j + 1) else '\000' in
      if e = 'u' then begin
        let cp = unicode_escape p !j in
        j := !j + if cp >= 0x10000 then 12 else 6;
        if cp >= 0x80 then wide := true;
        append_rune p cp;
        run := !j
      end
      else begin
        let out =
          match e with
          | '"' | '\\' | '/' -> Char.code e
          | 'b' -> 8
          | 'f' -> 12
          | 'n' -> 10
          | 'r' -> 13
          | 't' -> 9
          | _ -> fail p "invalid escape" !j
        in
        append_byte p out;
        j := !j + 2;
        run := !j
      end
    end
    else if c < ' ' then fail p "control character in a string" !j
    else begin
      if c >= '\128' then wide := true;
      incr j
    end
  done

(* str reads a string, from its opening quote. Go reads eight bytes at a time while there are that many. Here it is
   a byte at a time, through the table. *)
let str p =
  let b = p.b in
  let len = p.stop in
  let start = p.i + 1 in
  let j = ref start in
  let wide = ref 0 in
  while
    !j < len
    &&
    let c = Char.code (String.get scan (Char.code (Bytes.get b !j))) in
    c <> 1
    &&
    (wide := !wide lor c;
     true)
  do
    incr j
  done;
  if !j >= len then fail p "unterminated string" !j;
  let c = Bytes.get b !j in
  if c = '"' then begin
    let header = kind_string lor ((!j - start) lsl 16) in
    let header =
      if !wide <> 0 then begin
        if not (utf8_valid b start !j) then fail p "invalid UTF-8" start;
        header lor (str_wide lsl 8)
      end
      else header
    in
    p.i <- !j + 1;
    push p header start
  end
  else if c = '\\' then escaped p start !j (!wide <> 0)
  else fail p "control character in a string" !j

(* key reads a property name and its colon, leaving the parser at the value. *)
let key p =
  if peek p <> Char.code '"' then fail p "expected a property name" p.i;
  str p;
  skip_ws p;
  if peek p <> Char.code ':' then fail p "expected ':'" p.i;
  p.i <- p.i + 1;
  skip_ws p

let is_digit b len j = j < len && Bytes.get b j >= '0' && Bytes.get b j <= '9'

let powers =
  [|
    1e0;
    1e1;
    1e2;
    1e3;
    1e4;
    1e5;
    1e6;
    1e7;
    1e8;
    1e9;
    1e10;
    1e11;
    1e12;
    1e13;
    1e14;
    1e15;
    1e16;
    1e17;
    1e18;
    1e19;
    1e20;
    1e21;
    1e22;
  |]

(* long_integer classifies an integer of more than 18 digits, from its digits: it pushes the number and returns true
   when the integer is one the tape holds exactly (from -2^63 to 2^64 - 1). The magnitude is read into two 32-bit
   halves. *)
let long_integer p header digits_start digits_stop negative =
  let b = p.b in
  let hi = ref 0 and lo = ref 0 in
  let fits = ref true in
  let k = ref digits_start in
  while !fits && !k < digits_stop do
    let l = (!lo * 10) + (Char.code (Bytes.get b !k) - 48) in
    let h = (!hi * 10) + (l lsr 32) in
    lo := l land 0xffffffff;
    hi := h;
    if h lsr 32 <> 0 then fits := false;
    incr k
  done;
  let hi = !hi and lo = !lo in
  if not !fits then false
  else if not negative then begin
    if hi < 1 lsl 30 then push p header ((hi lsl 32) lor lo)
    else push p (header lor (Numbers.big_flags (2 + (hi lsr 30)) lsl 8)) (((hi land ((1 lsl 30) - 1)) lsl 32) lor lo);
    true
  end
  else if hi < 1 lsl 30 || (hi = 1 lsl 30 && lo = 0) then begin
    (* At most 2^62 in magnitude: an [int] (the shift wraps to the least one for 2^62 itself). *)
    push p header (-((hi lsl 32) lor lo));
    true
  end
  else if hi < 1 lsl 31 || (hi = 1 lsl 31 && lo = 0) then begin
    (* Below -2^62 and not below -2^63: -2 * 2^62 + low. The magnitude less 2^62 is at most 2^62, which wraps. *)
    let above = ((hi - (1 lsl 30)) lsl 32) lor lo in
    push p (header lor (Numbers.big_flags 0 lsl 8)) (max_int - above + 1);
    true
  end
  else false

(* number reads a number: integers of 64 bits as integers, anything else as a float. The header keeps the offset of
   the text. *)
let number p =
  let b = p.b in
  let len = p.stop in
  let start = p.i in
  let j = ref start in
  let negative = Bytes.get b !j = '-' in
  if negative then incr j;
  (* The digits of the integer and the fraction as one integer, while they fit: mantissa. exact says no digit was
     left out of it. *)
  let mantissa = ref 0 in
  let exact = ref true in
  let int_start = !j in
  if !j < len && Bytes.get b !j = '0' then incr j
  else begin
    (* Eighteen digits cannot overflow an [int]. *)
    let limit = Int.min len (!j + 18) in
    while !j < limit && is_digit b len !j do
      mantissa := (!mantissa * 10) + (Char.code (Bytes.get b !j) - 48);
      incr j
    done;
    if !j = int_start then fail p "invalid number" !j;
    while is_digit b len !j do
      exact := false;
      incr j
    done
  end;
  let int_stop = !j in
  let digits = ref (!j - int_start) in
  let floating = ref false in
  let scale = ref 0 in
  if !j < len && Bytes.get b !j = '.' then begin
    incr j;
    let frac_start = !j in
    while is_digit b len !j do
      incr digits;
      if !digits <= 18 then mantissa := (!mantissa * 10) + (Char.code (Bytes.get b !j) - 48) else exact := false;
      incr j
    done;
    if !j = frac_start then fail p "invalid number" !j;
    scale := frac_start - !j;
    floating := true
  end;
  let exponent = ref 0 in
  let exp_overflow = ref false in
  if !j < len && (Bytes.get b !j = 'e' || Bytes.get b !j = 'E') then begin
    incr j;
    let exp_negative = ref false in
    if !j < len && (Bytes.get b !j = '+' || Bytes.get b !j = '-') then begin
      exp_negative := Bytes.get b !j = '-';
      incr j
    end;
    if not (is_digit b len !j) then fail p "invalid number" !j;
    while is_digit b len !j do
      if !exponent < 100000 then exponent := (!exponent * 10) + (Char.code (Bytes.get b !j) - 48)
      else exp_overflow := true;
      incr j
    done;
    if !exp_negative then exponent := - !exponent;
    floating := true
  end;
  p.i <- !j;
  let header = kind_number lor (start lsl 16) in
  let pushed =
    if !floating then false
    else if !exact then
      if not negative then begin
        push p header !mantissa;
        true
      end
      else if !mantissa <> 0 then begin
        push p header (- !mantissa);
        true
      end
      else (* -0 is the float. *) false
    else long_integer p header int_start int_stop negative
  in
  if not pushed then begin
    (* A mantissa that a float holds exactly and a power of ten that one does too: one exact conversion and one
       correctly rounded operation give the correctly rounded value (Clinger's fast path). *)
    let scale = !scale + !exponent in
    if p.validating then begin
      (* Checking syntax only: a number well within the range of a float needs no conversion, since its value is
         not needed. *)
      if !exp_overflow || !digits + !exponent >= 300 then begin
        if Float.Array.length p.floats = 0 then p.floats <- Float.Array.create 16;
        if not (Numbers.decimal_to_float b start !j p.floats 0) then fail p "number out of range" start
      end;
      push p (header lor (Numbers.num_float lsl 8)) 0
    end
    else begin
      let at = p.nfloats in
      if at >= Float.Array.length p.floats then begin
        let grown = Float.Array.create ((2 * at) + 16) in
        Float.Array.blit p.floats 0 grown 0 at;
        p.floats <- grown
      end;
      if !exact && (not !exp_overflow) && !mantissa lsr 53 = 0 && scale >= -22 && scale <= 22 then begin
        let d = Float.of_int !mantissa in
        let d = if scale < 0 then d /. powers.(-scale) else d *. powers.(scale) in
        Float.Array.set p.floats at (if negative then -.d else d)
      end
      else if not (Numbers.decimal_to_float b start !j p.floats at) then fail p "number out of range" start;
      p.nfloats <- at + 1;
      push p (header lor (Numbers.num_float lsl 8)) at
    end
  end

(* max_document_size is the most bytes of text a document may have (2^31 - 1, as in the Object Pascal port). A value
   takes at least one byte, so every count and every index of a member is then below 2^31, which the sort keys of
   dedupe and of Values.all_unique rely on (they keep a position in their low 32 bits). *)
let max_document_size = 0x7fffffff

(* within_size is whether a text of len bytes is within a limit. It is apart from parse so that a test can try it
   with a small limit, a text of 2^31 bytes being too much for one. *)
let within_size len limit = len <= limit

let parse p =
  let len = p.stop in
  if not (within_size (len - p.i) max_document_size) then fail p "document too large" p.i;
  skip_ws p;
  let finished = ref false in
  while not !finished do
    (* A value. *)
    let c = peek p in
    let opened =
      if c = Char.code '{' then begin
        p.i <- p.i + 1;
        skip_ws p;
        if p.nframes >= max_depth then fail p "nesting too deep" p.i;
        if peek p = Char.code '}' then begin
          p.i <- p.i + 1;
          push p kind_object 0;
          false
        end
        else begin
          push_frame p (p.nscratch / 2 lor object_frame);
          key p;
          true
        end
      end
      else if c = Char.code '[' then begin
        p.i <- p.i + 1;
        skip_ws p;
        if p.nframes >= max_depth then fail p "nesting too deep" p.i;
        if peek p = Char.code ']' then begin
          p.i <- p.i + 1;
          push p kind_array 0;
          false
        end
        else begin
          push_frame p (p.nscratch / 2);
          true
        end
      end
      else begin
        if c = Char.code '"' then str p
        else if c = Char.code 't' then literal p "true" kind_bool 1
        else if c = Char.code 'f' then literal p "false" kind_bool 0
        else if c = Char.code 'n' then literal p "null" kind_null 0
        else if c = Char.code '-' || (c >= Char.code '0' && c <= Char.code '9') then number p
        else if c = -1 then fail p "unexpected end of input" p.i
        else fail p "expected a value" p.i;
        false
      end
    in
    (* After a value: separators and closing brackets, until the next value or the end. *)
    let next = ref opened in
    while not !next do
      if p.nframes = 0 then begin
        skip_ws p;
        if p.i <> len then fail p "trailing characters" p.i;
        finished := true;
        next := true
      end
      else begin
        let frame = p.frames.(p.nframes - 1) in
        let is_object = frame land object_frame <> 0 in
        skip_ws p;
        let c = peek p in
        if c = Char.code ',' then begin
          p.i <- p.i + 1;
          skip_ws p;
          if is_object then key p;
          next := true
        end
        else if c = Char.code '}' && is_object then begin
          p.i <- p.i + 1;
          close p (frame land lnot object_frame) true
        end
        else if c = Char.code ']' && not is_object then begin
          p.i <- p.i + 1;
          close p frame false
        end
        else if c = -1 then fail p "unexpected end of input" p.i
        else if is_object then fail p "expected ',' or '}'" p.i
        else fail p "expected ',' or ']'" p.i
      end
    done
  done

let parser_error p = { message = p.err_message; offset = p.err_offset }

(* is_valid_range reports whether the len bytes of b from off are one valid JSON value. It allocates nothing once
   the buffers have grown. *)
let is_valid_range p b off len =
  reset p b off (off + len) true;
  let ok =
    try
      parse p;
      true
    with Failed -> false
  in
  p.b <- Bytes.empty;
  ok

(* is_valid reports whether b is one valid JSON value. *)
let is_valid p b = is_valid_range p b 0 (Bytes.length b)

(* parse_new parses b into a new document, exactly sized. *)
let parse_new p b =
  reset p b 0 (Bytes.length b) false;
  let ok =
    try
      parse p;
      true
    with Failed -> false
  in
  p.b <- Bytes.empty;
  if not ok then Error (parser_error p)
  else begin
    let root = p.nnodes / 2 in
    let tape = Array.make (p.nnodes + 2) 0 in
    Array.blit p.nodes 0 tape 0 p.nnodes;
    tape.(root * 2) <- p.scratch.(0);
    tape.((root * 2) + 1) <- p.scratch.(1);
    let floats = Float.Array.create p.nfloats in
    Float.Array.blit p.floats 0 floats 0 p.nfloats;
    Ok { tape; source = b; text = Bytes.sub p.text 0 p.ntext; floats; root }
  end

(* create_empty is a document for parse_into to fill. It holds the value null. *)
let create_empty () =
  { tape = [| kind_null; 0 |]; source = Bytes.empty; text = Bytes.empty; floats = Float.Array.create 0; root = 0 }

(* parse_into parses the first len bytes of b into d, reusing its arrays and the parser's (no allocation once they
   have grown). The finished values are written straight into the document's tape. The document is valid until the
   next parse into it. When b is longer than len, the byte at len must not be one a number is written with, since a
   number's text is read from the document as far as it goes. *)
let parse_into p d b len =
  let nodes = p.nodes and text = p.text and floats = p.floats in
  p.nodes <- d.tape;
  p.text <- d.text;
  p.floats <- d.floats;
  reset p b 0 len false;
  let ok =
    try
      parse p;
      true
    with Failed -> false
  in
  p.b <- Bytes.empty;
  if ok then begin
    let n = p.nnodes in
    if n + 2 > Array.length p.nodes then p.nodes <- grow_ints p.nodes n (n + 2);
    p.nodes.(n) <- p.scratch.(0);
    p.nodes.(n + 1) <- p.scratch.(1);
    d.root <- n / 2;
    d.source <- b
  end
  else begin
    (* Keep what the buffers have grown to, and leave the document holding null. *)
    if Array.length p.nodes < 2 then p.nodes <- Array.make 16 0;
    p.nodes.(0) <- kind_null;
    p.nodes.(1) <- 0;
    d.root <- 0;
    d.source <- Bytes.empty
  end;
  d.tape <- p.nodes;
  d.text <- p.text;
  d.floats <- p.floats;
  p.nodes <- nodes;
  p.text <- text;
  p.floats <- floats;
  ok

(* parse_bytes parses UTF-8 JSON text. The document keeps the bytes. Do not modify them afterwards. *)
let parse_bytes b = parse_new (create_parser ()) b

(* parse_string parses JSON text. The document holds a copy of it. *)
let parse_string s = parse_bytes (Bytes.of_string s)

(* release_source lets go of the text of a document that a parser fills again, which the caller owns. The document
   holds null until the next parse into it. *)
let release_source d =
  if Array.length d.tape >= 2 then begin
    d.tape.(0) <- kind_null;
    d.tape.(1) <- 0;
    d.root <- 0
  end;
  d.source <- Bytes.empty
