(* JSON equality, hashing and uniqueness over values in documents (an instance against a schema's constant). A port
   of values.go of the Go module. *)

open Document

(* values_equal is JSON equality: numbers by value, objects by their property sets, arrays element by element. *)
let rec values_equal a x b y =
  let kind = kind a x in
  kind = Document.kind b y
  &&
  if kind = kind_null then true
  else if kind = kind_bool then boolean a x = boolean b y
  else if kind = kind_number then compare_numbers a x b y = 0
  else if kind = kind_string then str_equal a x b y
  else if kind = kind_array then begin
    let n = count a x in
    n = count b y
    &&
    let p = first a x and q = first b y in
    let i = ref 0 in
    while !i < n && values_equal a (p + !i) b (q + !i) do
      incr i
    done;
    !i = n
  end
  else begin
    let n = count a x in
    n = count b y
    &&
    (* Objects usually list their members in the same order: compare position by position, and look names up only
       from the first position where the names differ. *)
    let p = first a x and q = first b y in
    let i = ref 0 in
    let equal = ref true in
    let ordered = ref true in
    while !equal && !ordered && !i < n do
      let k = p + (2 * !i) and l = q + (2 * !i) in
      if not (str_equal a k b l) then ordered := false
      else if not (values_equal a (k + 1) b (l + 1)) then equal := false
      else incr i
    done;
    if !equal && not !ordered then begin
      let j = ref !i in
      while !equal && !j < n do
        let key = p + (2 * !j) in
        let w = find_property b y (str_bytes a key) (str_off a key) (str_len a key) in
        if w < 0 || not (values_equal a (key + 1) b w) then equal := false;
        incr j
      done
    end;
    !equal
  end

(* value_hash is a hash that agrees with JSON equality (object hashing is independent of the member order). *)
let rec value_hash d v =
  let kind = kind d v in
  if kind = kind_null then 0x53
  else if kind = kind_bool then if boolean d v then 0x52 else 0x51
  else if kind = kind_number then begin
    let flags = flags d v and data = data d v in
    let c = Numbers.class_of flags in
    if c = Numbers.num_float then begin
      let f = Float.Array.get (floats d) data in
      (* Integral floats hash as the integer they equal. *)
      if f = Float.floor f && f >= -.Numbers.two62 && f < Numbers.two62 then Float.to_int f * hash_k lxor 0x1234
      else Int64.to_int (Int64.bits_of_float f) * hash_k lxor 0x4321
    end
    else if c = Numbers.num_big then begin
      (* Floats of 2^62 and above in magnitude are integers, and never equal an integer of this class exactly unless
         the integer is a float: hash both by the float. *)
      let f = (Float.of_int (Numbers.top_of flags - 2) *. Numbers.two62) +. Float.of_int data in
      Int64.to_int (Int64.bits_of_float f) * hash_k lxor 0x4321
    end
    else data * hash_k lxor 0x1234
  end
  else if kind = kind_string then str_hash (str_bytes d v) (str_off d v) (str_len d v)
  else if kind = kind_array then begin
    let n = count d v in
    let h = ref (0x54 + n) in
    let c = first d v in
    for i = 0 to n - 1 do
      h := (!h * 31) + value_hash d (c + i)
    done;
    !h
  end
  else begin
    (* Equal objects have the same member values, so a sum of the values' hashes (whatever the order) agrees with
       equality. *)
    let n = count d v in
    let h = ref (0x55 + n) in
    let c = first d v in
    for i = 0 to n - 1 do
      h := !h + (value_hash d (c + (2 * i) + 1) * 0x2c1b3c6d)
    done;
    !h
  end

let all_strings d first n =
  let i = ref 0 in
  while !i < n && kind d (first + !i) = kind_string do
    incr i
  done;
  !i = n

(* Scratch for all_unique, which grows to the longest array it has sorted. *)
type scratch = { mutable hashes : int array }

let create_scratch () = { hashes = [||] }

(* all_unique decides uniqueItems: pairwise for short arrays, otherwise sorted by hash in scratch, each entry the
   hash's high half and the item's index, so that only items with equal hashes are compared. It allocates nothing
   once scratch has grown. *)
let all_unique d array scratch =
  let n = count d array in
  n < 2
  ||
  let c = first d array in
  let unique = ref true in
  if n <= 32 && all_strings d c n then begin
    (* Lists of names, the common case: pairwise, comparing lengths first. *)
    let i = ref 1 in
    while !unique && !i < n do
      let j = ref 0 in
      while !unique && !j < !i do
        if str_equal d (c + !i) d (c + !j) then unique := false;
        incr j
      done;
      incr i
    done
  end
  else if n <= 16 then begin
    let i = ref 1 in
    while !unique && !i < n do
      let j = ref 0 in
      while !unique && !j < !i do
        if values_equal d (c + !i) d (c + !j) then unique := false;
        incr j
      done;
      incr i
    done
  end
  else begin
    if Array.length scratch.hashes < n then scratch.hashes <- Array.make (Int.max n (2 * Array.length scratch.hashes)) 0;
    let s = scratch.hashes in
    for i = 0 to n - 1 do
      s.(i) <- value_hash d (c + i) land lnot 0xffffffff lor i
    done;
    sort_ints s n;
    let start = ref 0 in
    let stop = ref 1 in
    while !unique && !stop <= n do
      if !stop = n || s.(!stop) asr 32 <> s.(!start) asr 32 then begin
        let i = ref (!start + 1) in
        while !unique && !i < !stop do
          let j = ref !start in
          while !unique && !j < !i do
            if values_equal d (c + (s.(!i) land 0xffffffff)) d (c + (s.(!j) land 0xffffffff)) then unique := false;
            incr j
          done;
          incr i
        done;
        start := !stop
      end;
      incr stop
    done
  end;
  !unique
