(* Numbers: the correctly rounded conversion of a JSON number's text to a float, and exact numeric comparisons over
   the parser's number representations. Integers stay exact and floats compare by value, so 1 = 1.0 and
   9007199254740993 > 9007199254740992.0. multipleOf is decided exactly on decimal forms, so 0.0075 is a multiple of
   0.0001.

   A port of numbers.go of the Go module. Go's standard library converts decimal text to a float64 and has big
   numbers. The OCaml standard library has neither, so this module has its own, as the Object Pascal port does: the
   Eisel-Lemire algorithm as the Java port wrote it (FastDouble.java), and a small unsigned big integer for the cases
   those algorithms leave.

   The representations are described at the top of document.ml. In short, a number is its flags and one data word:

   - [num_int]: an integer that fits an OCaml [int] (63 bits). The data is the integer.
   - [num_big]: any other integer from -2^63 to 2^64 - 1. Such an integer is (top - 2) * 2^62 + low, where low is in
     [0, 2^62) and top is 0 (below -2^62) or 3, 4 or 5 (2^62 and above). The data is low and top is in the flags
     above the class. An [int] is the same form with top 1 or 2, so the pair (top, low) orders every integer.
   - [num_float]: anything else. The data is the index of the value in the document's array of floats, so that no
     float is ever boxed. *)

let num_int = 0
let num_big = 1
let num_float = 2
let class_of flags = flags land 3
let top_of flags = flags lsr 2
let big_flags top = num_big lor (top lsl 2)
let two62 = 4611686018427387904.0
let two63 = 9223372036854775808.0
let two64 = 18446744073709551616.0
let smallest_power_of_ten = -342
let largest_power_of_ten = 308
let mantissa_explicit_bits = 52
let minimum_exponent = -1023
let infinite_power = 0x7FF
let min_exponent_round_to_even = -4
let max_exponent_round_to_even = 23

(* The bits of positive infinity. The bits of a positive float are 63 bits, which is what an [int] holds: bit 62
   (the top of the exponent) lands on the sign of the [int], and nothing is lost. *)
let infinity_bits = infinite_power lsl mantissa_explicit_bits

(* ---------------------------------------------------------------------------------------------------------------------
   The unsigned big integer: limbs of 28 bits, least significant first, so that a limb times a multiplier below 2^31
   plus a carry fits an [int]. Only what this module needs of one. *)

type big = { mutable limbs : int array; mutable len : int }

let limb_bits = 28
let limb_mask = (1 lsl limb_bits) - 1
let big_create () = { limbs = Array.make 8 0; len = 0 }

let big_push a v =
  if a.len = Array.length a.limbs then begin
    let grown = Array.make ((a.len * 2) + 4) 0 in
    Array.blit a.limbs 0 grown 0 a.len;
    a.limbs <- grown
  end;
  a.limbs.(a.len) <- v;
  a.len <- a.len + 1

(* a := a * m + add, for m and add below 2^31. *)
let big_mul_add a m add =
  let carry = ref add in
  for k = 0 to a.len - 1 do
    let t = (a.limbs.(k) * m) + !carry in
    a.limbs.(k) <- t land limb_mask;
    carry := t lsr limb_bits
  done;
  while !carry <> 0 do
    big_push a (!carry land limb_mask);
    carry := !carry lsr limb_bits
  done

(* a := a * 10^n. *)
let big_mul_pow10 a n =
  let n = ref n in
  while !n >= 9 do
    big_mul_add a 1000000000 0;
    n := !n - 9
  done;
  while !n > 0 do
    big_mul_add a 10 0;
    decr n
  done

(* a := a * 2^n. *)
let big_shift_left a n =
  let n = ref n in
  while !n >= 27 do
    big_mul_add a (1 lsl 27) 0;
    n := !n - 27
  done;
  if !n > 0 then big_mul_add a (1 lsl !n) 0

let big_trim a =
  while a.len > 0 && a.limbs.(a.len - 1) = 0 do
    a.len <- a.len - 1
  done

(* a := a / m, returning the remainder. *)
let big_div_small a m =
  let r = ref 0 in
  for k = a.len - 1 downto 0 do
    let t = (!r lsl limb_bits) lor a.limbs.(k) in
    a.limbs.(k) <- t / m;
    r := t mod m
  done;
  big_trim a;
  !r

(* The remainder of a by m, leaving a as it is. *)
let big_mod_small a m =
  let r = ref 0 in
  for k = a.len - 1 downto 0 do
    r := (!r lsl limb_bits) lor a.limbs.(k) mod m
  done;
  !r

let big_compare a b =
  if a.len <> b.len then if a.len < b.len then -1 else 1
  else begin
    let result = ref 0 in
    let k = ref (a.len - 1) in
    while !result = 0 && !k >= 0 do
      let x = a.limbs.(!k) and y = b.limbs.(!k) in
      if x <> y then result := if x < y then -1 else 1;
      decr k
    done;
    !result
  end

(* a := a - b, for a at least b. *)
let big_sub a b =
  let borrow = ref 0 in
  for k = 0 to a.len - 1 do
    let other = if k < b.len then b.limbs.(k) else 0 in
    let t = a.limbs.(k) - other - !borrow in
    if t < 0 then begin
      a.limbs.(k) <- t + (1 lsl limb_bits);
      borrow := 1
    end
    else begin
      a.limbs.(k) <- t;
      borrow := 0
    end
  done;
  big_trim a

let big_copy a = { limbs = Array.copy a.limbs; len = a.len }
let big_is_zero a = a.len = 0
let big_is_one a = a.len = 1 && a.limbs.(0) = 1

(* ---------------------------------------------------------------------------------------------------------------------
   Decimal to float: the Eisel-Lemire algorithm (Daniel Lemire, "Number Parsing at a Gigabyte per Second", and the
   fast_float library), which is exact for a decimal significand of up to 19 digits (Noble Mushtak and Daniel Lemire,
   "Fast Number Parsing Without Fallback"). A longer significand is truncated and the result accepted when the
   truncated and the next value round to the same float. Otherwise the digits are compared, as big integers, with
   the point halfway between the two.

   The significand is kept to 18 digits here, one fewer than the Go and Pascal ports keep, so that it fits an [int].
   The 64-bit words of the algorithm are [Int64] values that never leave the function that computes them, which the
   native compiler keeps in registers. *)

(* The high 64 bits of the unsigned 128-bit product of a and b. *)
let[@inline] mul_high (a : int64) (b : int64) : int64 =
  let a0 = Int64.logand a 0xFFFFFFFFL and a1 = Int64.shift_right_logical a 32 in
  let b0 = Int64.logand b 0xFFFFFFFFL and b1 = Int64.shift_right_logical b 32 in
  let p00 = Int64.mul a0 b0 and p01 = Int64.mul a0 b1 and p10 = Int64.mul a1 b0 and p11 = Int64.mul a1 b1 in
  let mid =
    Int64.add
      (Int64.add (Int64.shift_right_logical p00 32) (Int64.logand p01 0xFFFFFFFFL))
      (Int64.logand p10 0xFFFFFFFFL)
  in
  Int64.add
    (Int64.add (Int64.add p11 (Int64.shift_right_logical p01 32)) (Int64.shift_right_logical p10 32))
    (Int64.shift_right_logical mid 32)

(* The number of zero bits above the top bit of a positive [int], counted in a 64-bit word. *)
let leading_zeros w =
  let n = ref 0 in
  let x = ref w in
  while !x <> 0 do
    incr n;
    x := !x lsr 1
  done;
  64 - !n

(* eisel_lemire is the bits of the float nearest w * 10^q (w a significand of at most 18 digits). *)
let eisel_lemire (w : int) (q : int) : int =
  if w = 0 || q < smallest_power_of_ten then 0
  else if q > largest_power_of_ten then infinity_bits
  else begin
    let lz = leading_zeros w in
    let w = Int64.shift_left (Int64.of_int w) lz in
    let index = 2 * (q - smallest_power_of_ten) in
    let power = String.get_int64_le Powers_of_five.table (index * 8) in
    let high = mul_high w power in
    (* The low half of the product: the multiplication wraps on purpose. *)
    let low = Int64.mul w power in
    let precision_mask = Int64.shift_right_logical (-1L) (mantissa_explicit_bits + 3) in
    let more = Int64.equal (Int64.logand high precision_mask) precision_mask in
    let second_high = if more then mul_high w (String.get_int64_le Powers_of_five.table ((index + 1) * 8)) else 0L in
    let low = Int64.add low second_high in
    (* An unsigned comparison: second_high > low. *)
    let high =
      if more && Int64.compare (Int64.add second_high Int64.min_int) (Int64.add low Int64.min_int) > 0 then
        Int64.succ high
      else high
    in
    let upper_bit = Int64.to_int (Int64.shift_right_logical high 63) in
    let shift = upper_bit + 64 - mantissa_explicit_bits - 3 in
    (* At most 55 bits from here on, so an [int]. *)
    let mantissa = Int64.to_int (Int64.shift_right_logical high shift) in
    let power2 = (((152170 + 65536) * q) asr 16) + 63 + upper_bit - lz - minimum_exponent in
    if power2 <= 0 then
      (* Subnormal. *)
      if -power2 + 1 >= 64 then 0
      else begin
        let mantissa = mantissa lsr (-power2 + 1) in
        let mantissa = mantissa + (mantissa land 1) in
        let mantissa = mantissa lsr 1 in
        let power2 = if mantissa < 1 lsl mantissa_explicit_bits then 0 else 1 in
        (power2 lsl mantissa_explicit_bits) lor (mantissa land ((1 lsl mantissa_explicit_bits) - 1))
      end
    else begin
      (* A product exactly halfway between two floats rounds to even. *)
      let low_is_small = Int64.equal low 0L || Int64.equal low 1L in
      let mantissa =
        if
          low_is_small && q >= min_exponent_round_to_even && q <= max_exponent_round_to_even
          && mantissa land 3 = 1
          && Int64.equal (Int64.shift_left (Int64.of_int mantissa) shift) high
        then mantissa land lnot 1
        else mantissa
      in
      let mantissa = mantissa + (mantissa land 1) in
      let mantissa = mantissa lsr 1 in
      let carried = mantissa >= 2 lsl mantissa_explicit_bits in
      let mantissa = if carried then 1 lsl mantissa_explicit_bits else mantissa in
      let power2 = if carried then power2 + 1 else power2 in
      let mantissa = mantissa land lnot (1 lsl mantissa_explicit_bits) in
      if power2 >= infinite_power then infinity_bits else (power2 lsl mantissa_explicit_bits) lor mantissa
    end
  end

let is_digit c = c >= '0' && c <= '9'

(* read_exponent reads the exponent part of a number's text at j, if there is one, as far as stop. *)
let read_exponent b j stop =
  if j >= stop || (Bytes.get b j <> 'e' && Bytes.get b j <> 'E') then 0
  else begin
    let j = ref (j + 1) in
    let negative = ref false in
    if !j < stop && (Bytes.get b !j = '+' || Bytes.get b !j = '-') then begin
      negative := Bytes.get b !j = '-';
      incr j
    end;
    let e = ref 0 in
    while !j < stop && is_digit (Bytes.get b !j) do
      if !e < 1_000_000_000 then e := (!e * 10) + (Char.code (Bytes.get b !j) - 48);
      incr j
    done;
    if !negative then - !e else !e
  end

(* The end of the digits and the point of the number text from j, as far as stop. *)
let digits_end b j stop =
  let j = ref j in
  while !j < stop && (is_digit (Bytes.get b !j) || Bytes.get b !j = '.') do
    incr j
  done;
  !j

(* compare_with_halfway says whether the positive decimal of the digits of b from digits_start to stop is above (1),
   at (0) or below (-1) the point halfway between the float with these bits and the next float up. *)
let compare_with_halfway b digits_start stop (low : int) =
  (* The decimal: its digits as an integer, and its power of ten. *)
  let digits = big_create () in
  let exponent = ref 0 in
  let fraction = ref false in
  let j = ref digits_start in
  let scanning = ref true in
  while !scanning && !j < stop do
    let c = Bytes.get b !j in
    if c = '.' then begin
      fraction := true;
      incr j
    end
    else if is_digit c then begin
      big_mul_add digits 10 (Char.code c - 48);
      if !fraction then decr exponent;
      incr j
    end
    else scanning := false
  done;
  exponent := !exponent + read_exponent b !j stop;
  (* The halfway point: (2 * mantissa + 1) * 2^(exponent - 1), where the float is mantissa * 2^exponent. *)
  let mantissa = low land ((1 lsl mantissa_explicit_bits) - 1) in
  let binary_exponent = (low lsr mantissa_explicit_bits) land 0x7FF in
  let mantissa = if binary_exponent = 0 then mantissa else mantissa lor (1 lsl mantissa_explicit_bits) in
  let binary_exponent = if binary_exponent = 0 then 1 else binary_exponent in
  let binary_exponent = binary_exponent - 1075 - 1 in
  let mantissa = (mantissa * 2) + 1 in
  let half = big_create () in
  big_push half (mantissa land limb_mask);
  big_push half (mantissa lsr limb_bits);
  big_trim half;
  (* Both sides as integers: the powers with negative exponents move to the other side. *)
  if !exponent >= 0 then big_mul_pow10 digits !exponent else big_mul_pow10 half (- !exponent);
  if binary_exponent >= 0 then big_shift_left half binary_exponent else big_shift_left digits (-binary_exponent);
  big_compare digits half

let decimal_to_float b start stop (out : Float.Array.t) at =
  let j = ref start in
  let negative = Bytes.get b !j = '-' in
  if negative then incr j;
  let digits_start = !j in
  let w = ref 0 in
  let digits = ref 0 in
  let exponent = ref 0 in
  let truncated = ref false in
  let fraction = ref false in
  let scanning = ref true in
  while !scanning && !j < stop do
    let c = Bytes.get b !j in
    if c = '.' then begin
      fraction := true;
      incr j
    end
    else if not (is_digit c) then scanning := false
    else begin
      if !digits = 0 && c = '0' then
        (* Leading zeros count only as a fraction's places. *)
        begin if !fraction then decr exponent
        end
      else if !digits < 18 then begin
        w := (!w * 10) + (Char.code c - 48);
        incr digits;
        if !fraction then decr exponent
      end
      else begin
        (* Beyond 18 digits: dropped, but an integer part's dropped digits still scale the value. *)
        if c <> '0' then truncated := true;
        if not !fraction then incr exponent
      end;
      incr j
    end
  done;
  exponent := !exponent + read_exponent b !j stop;
  let q = if !exponent > 100000 then 100000 else if !exponent < -100000 then -100000 else !exponent in
  let bits = ref (eisel_lemire !w q) in
  if !truncated then begin
    let next = eisel_lemire (!w + 1) q in
    if next <> !bits then begin
      (* The digits that were dropped decide between the two: the value is above the truncated one, so it rounds to
         the lower float, to the next one up, or (exactly halfway) to whichever of them is even. *)
      let order = if !bits = infinity_bits then -1 else compare_with_halfway b digits_start stop !bits in
      if order > 0 || (order = 0 && !bits land 1 = 1) then incr bits
    end
  end;
  if (!bits lsr mantissa_explicit_bits) land 0x7FF = infinite_power then false
  else begin
    let d = Int64.float_of_bits (Int64.logand (Int64.of_int !bits) Int64.max_int) in
    Float.Array.set out at (if negative then -.d else d);
    true
  end

(* ---------------------------------------------------------------------------------------------------------------------
   Comparisons. A float operand is named by its array and index, and read where it is compared, so that it is never
   boxed. *)

let compare_int (a : int) (b : int) = if a < b then -1 else if a > b then 1 else 0

(* compare_int_float compares an [int] with a (finite) float exactly. *)
let compare_int_float (a : int) floats i =
  let d = Float.Array.get floats i in
  if d >= two62 then -1
  else if d < -.two62 then 1
  else begin
    let floor = Float.floor d in
    let c = compare_int a (Float.to_int floor) in
    if c <> 0 then c else if d > floor then -1 else 0
  end

(* compare_big_float compares an integer beyond an [int] with a (finite) float exactly. Floats of that size are
   integers. *)
let compare_big_float top low floats i =
  let d = Float.Array.get floats i in
  if top = 0 then if d >= -.two62 then -1 else if d < -.two63 then 1 else compare_int low (Float.to_int (d +. two63))
  else if d >= two64 then -1
  else if d < two62 then 1
  else begin
    let float_top = if d < two63 then 3 else if d < two63 +. two62 then 4 else 5 in
    if top <> float_top then compare_int top float_top
    else compare_int low (Float.to_int (d -. (Float.of_int (float_top - 2) *. two62)))
  end

let compare fa a floats_a fb b floats_b =
  let ca = fa land 3 and cb = fb land 3 in
  if ca = num_int then
    if cb = num_int then compare_int a b
    else if cb = num_float then compare_int_float a floats_b b
    else if fb lsr 2 = 0 then 1
    else -1
  else if ca = num_float then
    if cb = num_float then begin
      let x = Float.Array.get floats_a a and y = Float.Array.get floats_b b in
      if x < y then -1 else if x > y then 1 else 0
    end
    else if cb = num_int then -compare_int_float b floats_a a
    else -compare_big_float (fb lsr 2) b floats_a a
  else if cb = num_big then begin
    let ta = fa lsr 2 and tb = fb lsr 2 in
    if ta <> tb then compare_int ta tb else compare_int a b
  end
  else if cb = num_int then if fa lsr 2 = 0 then -1 else 1
  else compare_big_float (fa lsr 2) a floats_b b

let is_integer flags data floats =
  flags land 3 <> num_float
  ||
  let d = Float.Array.get floats data in
  d = Float.floor d

let to_float flags data floats =
  let c = flags land 3 in
  if c = num_int then Float.of_int data
  else if c = num_float then Float.Array.get floats data
  else (Float.of_int (top_of flags - 2) *. two62) +. Float.of_int data

(* ---------------------------------------------------------------------------------------------------------------------
   multipleOf *)

type divisor = {
  is_int : bool;
  value : int;
  (* The significand (at most 17 digits), or -1 when the divisor's digits do not fit. *)
  significand : int;
  (* The exponent of the significand, whichever of the two holds it. *)
  exponent : int;
  (* The significand of a divisor of more than 17 significant digits. *)
  big : big;
}

(* The most digits of a significand that the streamed remainder can take: the remainder is below the significand, and
   ten times it plus nine must fit an [int]. Go and Pascal have 18 here, with a 64-bit remainder. *)
let max_significand_digits = 17

(* text_exponent reads the exponent part of a number's text at j, if there is one. *)
let text_exponent b j = read_exponent b j (Bytes.length b)

(* decimal_of reads the decimal of the number text at start: the significand without trailing zeros and the exponent.
   It returns the exponent. The significand is left in [significand] when it has at most 17 digits (and -1 is left
   there when it has more), and in [big] whenever one is given. *)
let decimal_of b start (big : big option) (significand : int ref) =
  let m = ref 0 in
  let fits = ref true in
  let len = Bytes.length b in
  let j = ref start in
  if Bytes.get b !j = '-' then incr j;
  let digits = ref 0 in
  let exponent = ref 0 in
  let pending_zeros = ref 0 in
  let fraction = ref false in
  let scanning = ref true in
  let push digit =
    if !digits >= max_significand_digits then fits := false else m := (!m * 10) + digit;
    (match big with Some big -> big_mul_add big 10 digit | None -> ());
    incr digits
  in
  while !scanning && !j < len do
    let c = Bytes.get b !j in
    if c = '.' then begin
      fraction := true;
      incr j
    end
    else if not (is_digit c) then scanning := false
    else begin
      if !fraction then decr exponent;
      if c = '0' then
        (* Trailing zeros are held back, so that they move into the exponent if no other digit follows. *)
        begin if !digits > 0 then incr pending_zeros
        end
      else begin
        while !pending_zeros > 0 do
          push 0;
          decr pending_zeros
        done;
        push (Char.code c - 48)
      end;
      incr j
    end
  done;
  significand := if !fits then !m else -1;
  !exponent + !pending_zeros + text_exponent b !j

let new_divisor flags data b start =
  let significand = ref 0 in
  let exponent = decimal_of b start None significand in
  let big = big_create () in
  if !significand < 0 then ignore (decimal_of b start (Some big) (ref 0));
  { is_int = class_of flags = num_int; value = data; significand = !significand; exponent; big }

(* The digits of the number text at start are read by two functions, since a pair of results would be a block. The
   digits start at start, or after its sign.

   last_non_zero_digit is the position of the last digit that is not zero, or -1 for zero. *)
let last_non_zero_digit b start =
  let len = Bytes.length b in
  let j = ref (if Bytes.get b start = '-' then start + 1 else start) in
  let last = ref (-1) in
  while
    !j < len
    &&
    let c = Bytes.get b !j in
    c = '.' || is_digit c
  do
    if Bytes.get b !j <> '.' && Bytes.get b !j <> '0' then last := !j;
    incr j
  done;
  !last

(* digits_exponent is the power of ten of the digit string up to its last digit that is not zero (which is given):
   the digits after it are trailing zeros, and move into the exponent. *)
let digits_exponent b start last_non_zero =
  let len = Bytes.length b in
  let j = ref (if Bytes.get b start = '-' then start + 1 else start) in
  let exponent = ref 0 in
  let fraction = ref false in
  while
    !j < len
    &&
    let c = Bytes.get b !j in
    c = '.' || is_digit c
  do
    if Bytes.get b !j = '.' then fraction := true
    else if !fraction then
      (* A digit of the fraction is a place, unless it is a trailing zero, which is no digit of the string. *)
      begin if !j <= last_non_zero then decr exponent
      end
    else if !j > last_non_zero then incr exponent;
    incr j
  done;
  !exponent + text_exponent b !j

(* divides_text reports whether the number text at start is a multiple of dm * 10^de (dm positive, no trailing
   zeros). The text's digits are streamed modulo what remains of the divisor, so the text may have any number of
   digits. It allocates nothing. *)
let divides_text b start dm de =
  (* x = xm * 10^xe with xm the digit string (trailing zeros moved into xe). x / d is an integer exactly when dm
     divides xm * 10^(xe - de). *)
  let digits_start = if Bytes.get b start = '-' then start + 1 else start in
  let last_non_zero = last_non_zero_digit b start in
  if last_non_zero < 0 then (* Zero is a multiple of everything. *) true
  else begin
    let shift = digits_exponent b start last_non_zero - de in
    if shift < 0 then
      (* dm * 10^-shift must divide xm, which has no trailing zero, so is not a multiple of 10. *)
      false
    else begin
      (* Remove from dm the factors of 2 and 5 that 10^shift supplies. What remains must divide xm. *)
      let rest = ref dm in
      let twos = ref 0 in
      while !twos < shift && !rest land 1 = 0 do
        rest := !rest lsr 1;
        incr twos
      done;
      let fives = ref 0 in
      while !fives < shift && !rest mod 5 = 0 do
        rest := !rest / 5;
        incr fives
      done;
      !rest = 1
      ||
      let r = ref 0 in
      for t = digits_start to last_non_zero do
        let c = Bytes.get b t in
        (* r < rest < 10^17, so r * 10 + 9 fits an [int]. *)
        if c <> '.' then r := ((!r * 10) + (Char.code c - 48)) mod !rest
      done;
      !r = 0
    end
  end

(* divides_text_big is divides_text for a divisor whose significand has more than 17 digits. The remainder is kept
   below the divisor by subtraction, at most nine times for each digit. *)
let divides_text_big b start dm de =
  let digits_start = if Bytes.get b start = '-' then start + 1 else start in
  let last_non_zero = last_non_zero_digit b start in
  if last_non_zero < 0 then true
  else begin
    let shift = digits_exponent b start last_non_zero - de in
    if shift < 0 then false
    else begin
      let rest = big_copy dm in
      let twos = ref 0 in
      while !twos < shift && rest.len > 0 && rest.limbs.(0) land 1 = 0 do
        ignore (big_div_small rest 2);
        incr twos
      done;
      let fives = ref 0 in
      while !fives < shift && rest.len > 0 && big_mod_small rest 5 = 0 do
        ignore (big_div_small rest 5);
        incr fives
      done;
      big_is_one rest
      ||
      let r = big_create () in
      for t = digits_start to last_non_zero do
        let c = Bytes.get b t in
        if c <> '.' then begin
          big_mul_add r 10 (Char.code c - 48);
          while big_compare r rest >= 0 do
            big_sub r rest
          done
        end
      done;
      big_is_zero r
    end
  end

let divides v flags data b start =
  if v.is_int && flags land 3 = num_int then
    (* The least integer divided by -1 overflows, and is a multiple of it. *)
    v.value = -1 || (v.value <> 0 && data mod v.value = 0)
  else if v.significand = 0 then false
  else if v.significand > 0 then divides_text b start v.significand v.exponent
  else if big_is_zero v.big then false
  else (* A divisor of more than 17 significant digits. *)
    divides_text_big b start v.big v.exponent
