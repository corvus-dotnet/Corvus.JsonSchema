(* The JSON parser, the number conversions, equality, hashing and uniqueness. A port of document_test.go of the Go
   module and of TestDocument, TestDoubleCases and TestStringCases of the Object Pascal port.

   Go checks its parser against encoding/json and strconv. Here the reference for a float is the C library's strtod,
   through [float_of_string], which is correctly rounded, and the reference for a string is the decoded form of each
   piece the test builds the string from. *)

open Corvus_json_schema_test_support
module Document = Corvus_json_schema__Document
module Numbers = Corvus_json_schema__Numbers
module Values = Corvus_json_schema__Values

let must_parse json =
  match Document.parse_string json with
  | Ok d -> d
  | Error e -> failwith (Printf.sprintf "parse %S: %s" json (Document.error_to_string e))

let root_equal a b = Values.values_equal a (Document.root a) b (Document.root b)

(* The classes of Go and Pascal: an int64, a uint64 beyond an int64, a float64. *)
let go_int = 0
let go_uint = 1
let go_float = 2

(* The class Go gives the root number of a document, and the 64 bits Go holds for it. *)
let go_number d =
  let n = Document.root d in
  let flags = Document.flags d n and data = Document.data d n in
  let c = Numbers.class_of flags in
  if c = Numbers.num_int then (go_int, Int64.of_int data)
  else if c = Numbers.num_float then (go_float, Int64.bits_of_float (Float.Array.get (Document.floats d) data))
  else begin
    let top = Numbers.top_of flags in
    let bits = Int64.add (Int64.shift_left (Int64.of_int (top - 2)) 62) (Int64.of_int data) in
    ((if top >= 4 then go_uint else go_int), bits)
  end

let number json = go_number (must_parse json)

let compare_json a b =
  let da = must_parse a and db = must_parse b in
  Document.compare_numbers da (Document.root da) db (Document.root db)

let divides x d =
  let dx = must_parse x and dd = must_parse d in
  let rd = Document.root dd and rx = Document.root dx in
  let v =
    Numbers.new_divisor (Document.flags dd rd) (Document.data dd rd) (Document.source dd) (Document.number_start dd rd)
  in
  Numbers.divides v (Document.flags dx rx) (Document.data dx rx) (Document.source dx) (Document.number_start dx rx)

let is_valid_text json = Document.is_valid (Document.create_parser ()) (Bytes.of_string json)

(* ---------------------------------------------------------------------------------------------------------------------
   document_test.go *)

let test_parse_round_trips () =
  Check.section "TestParseRoundTrips";
  List.iter
    (fun json ->
      let d = must_parse json in
      let again = must_parse (Document.to_string d) in
      Check.checkf (root_equal d again) "%s did not round trip: %s" json (Document.to_string d))
    [
      "null";
      "true";
      "false";
      "0";
      "-0";
      "1.5e3";
      {|""|};
      {|"a"|};
      "[]";
      "{}";
      {|[1,[2,[3]],{"a":null}]|};
      {|{"a":1,"b":[true,false],"c":{"d":"e"}}|};
      "\"caf\xc3\xa9 \\n \\\"q\\\" \\\\ \xf0\x9f\x98\x80\"";
      "18446744073709551615";
      "-9223372036854775808";
      "123456789012345678901234567890";
    ];
  Check.equal_string "compact text" ~expected:{|{"a":[1,2]}|}
    (Document.to_string (must_parse " { \"a\" : [ 1 , 2 ] } "));
  Check.equal_string "escapes" ~expected:{|"\u0001\t/"|} (Document.to_string (must_parse {|"\u0001\t\/"|}))

let test_parse_rejects_invalid_json () =
  Check.section "TestParseRejectsInvalidJSON";
  List.iter
    (fun json ->
      Check.checkf (Result.is_error (Document.parse_string json)) "%S parsed" json;
      Check.checkf (not (is_valid_text json)) "%S is valid to the syntax check" json)
    [
      "";
      " ";
      "{";
      "[";
      "[1,]";
      {|{"a":1,}|};
      {|{"a"}|};
      "{a:1}";
      "01";
      "1.";
      ".5";
      "-";
      "1e";
      "+1";
      "tru";
      "nul";
      {|"abc|};
      {|"\x"|};
      {|"\u12"|};
      {|"\ud800"|};
      {|"\udc00"|};
      {|"\ud800A"|};
      "\"a\nb\"";
      "1 2";
      "[1] x";
      "1e999";
      "\"\xff\"";
      "\"\xc0\x80\"";
      "\"\xed\xa0\x80\"";
      {|{"a":1 "b":2}|};
      "[1 2]";
      "]";
    ];
  let deep = String.make Document.max_depth '[' ^ String.make Document.max_depth ']' in
  Check.checkf (Result.is_ok (Document.parse_string deep)) "depth %d" Document.max_depth;
  Check.checkf (Result.is_error (Document.parse_string ("[" ^ deep ^ "]"))) "depth %d parsed" (Document.max_depth + 1);
  match Document.parse_string "[1, x]" with
  | Error e -> Check.equal_int "the offset of the error" ~expected:4 e.Document.offset
  | Ok _ -> Check.fail "[1, x] parsed"

let test_duplicate_keys () =
  Check.section "TestDuplicateKeysKeepTheLastValueAtTheFirstPosition";
  Check.equal_string "small object" ~expected:{|{"a":3,"b":5,"c":4}|}
    (Document.to_string (must_parse {|{"a":1,"b":2,"a":3,"c":4,"b":5}|}));
  let sb = Buffer.create 256 in
  Buffer.add_string sb "{";
  for i = 0 to 39 do
    Buffer.add_string sb (Printf.sprintf {|"k%d":%d,|} i i)
  done;
  Buffer.add_string sb {|"k7":"seven","k3":"three"}|};
  let d = must_parse (Buffer.contents sb) in
  let root = Document.root d in
  Check.equal_int "count" ~expected:40 (Document.count d root);
  Check.equal_string "k7" ~expected:"seven" (Document.string_value d (Document.property d root "k7"));
  Check.equal_string "k3" ~expected:"three" (Document.string_value d (Document.property d root "k3"));
  Check.equal_string "position 7" ~expected:"k7" (Document.string_value d (Document.first d root + (2 * 7)))

let test_numbers () =
  Check.section "TestNumbers";
  List.iter
    (fun (json, cls) ->
      let got, _ = number json in
      Check.checkf (got = cls) "%s has representation %d, want %d" json got cls)
    [
      ("0", go_int);
      ("-1", go_int);
      ("9223372036854775807", go_int);
      ("-9223372036854775808", go_int);
      ("9223372036854775808", go_uint);
      ("18446744073709551615", go_uint);
      ("18446744073709551616", go_float);
      ("-9223372036854775809", go_float);
      ("-0", go_float);
      ("1.0", go_float);
      ("1e2", go_float);
    ];
  List.iter
    (fun json ->
      let _, v = number json in
      let want = float_of_string json in
      Check.checkf
        (Int64.equal v (Int64.bits_of_float want))
        "%s parsed as %h, want %h" json (Int64.float_of_bits v) want)
    [
      "0.1";
      "1e22";
      "1e23";
      "123456789012345678.0";
      "0.000001";
      "5e-324";
      "1.7976931348623157e308";
      "2.2250738585072014e-308";
      "9007199254740993.0";
      "0.30000000000000004";
      "1e-400";
      "123456789012345678901234567890.123456789";
    ];
  List.iter
    (fun (a, b, want) ->
      Check.checkf (compare_json a b = want) "compare(%s, %s) = %d, want %d" a b (compare_json a b) want;
      Check.checkf (compare_json b a = -want) "compare(%s, %s) = %d, want %d" b a (compare_json b a) (-want))
    [
      ("1", "1.0", 0);
      ("-1", "-0.5", -1);
      ("9007199254740993", "9007199254740992.0", 1);
      ("0", "-0", 0);
      ("18446744073709551615", "1.8446744073709552e19", -1);
      ("9223372036854775808", "9223372036854775808.0", 0);
      ("9223372036854775807", "9223372036854775808", -1);
      ("1e30", "18446744073709551615", 1);
      ("-1e30", "-9223372036854775808", -1);
      ("2", "1.5", 1);
      ("1", "1.5", -1);
    ]

let test_multiple_of_is_exact () =
  Check.section "TestMultipleOfIsExact";
  List.iter
    (fun (x, d, want) -> Check.checkf (divides x d = want) "%s multipleOf %s = %b, want %b" x d (divides x d) want)
    [
      ("0.0075", "0.0001", true);
      ("0.00751", "0.0001", false);
      ("4.5", "1.5", true);
      ("35", "1.5", false);
      ("10", "5", true);
      ("1e308", "0.123456789", false);
      ("1e308", "0.5", true);
      ("0", "0.3", true);
      ("1e-300", "1e-7", false);
      ("7", "2", false);
      ("-8", "2", true);
      ("1.0", "1", true);
      ("3.0", "1.5", true);
      ("12391239123", "0.01", true);
      ("1.23456789012345678901234567890", "0.00000000000000000000000000001", true);
      ("1.5", "0.1234567890123456789012", false);
      ("0.2469135780246913578024", "0.1234567890123456789012", true);
      ("18446744073709551615", "5", true);
      ("1", "0", false);
      ("0.0", "0.0", false);
    ]

let test_equality_and_hashing () =
  Check.section "TestEqualityAndHashing";
  let hash d = Values.value_hash d (Document.root d) in
  List.iter
    (fun group ->
      let first = List.hd group in
      let a = must_parse first in
      List.iter
        (fun other ->
          let b = must_parse other in
          Check.checkf (root_equal a b && root_equal b a) "%s != %s" first other;
          Check.checkf (hash a = hash b) "hash(%s) != hash(%s)" first other)
        (List.tl group))
    [
      [ "1"; "1.0"; "1e0"; "10e-1" ];
      [ {|{"a":1,"b":[1,2]}|}; {|{"b":[1.0,2],"a":1}|} ];
      [ {|"ab"|}; {|"ab"|} ];
      [ "[]"; "[ ]" ];
      [ "0"; "-0"; "0.0" ];
      [ "18446744073709551615"; "18446744073709551615" ];
      [ "9223372036854775808"; "9223372036854775808.0" ];
      (* The same for the integers that an int64 holds and an OCaml int does not, and the floats they equal. *)
      [ "4611686018427387904"; "4611686018427387904.0"; "4.611686018427387904e18" ];
      [ "-4611686018427387905"; "-4611686018427387905" ];
      [ "-9223372036854775808"; "-9223372036854775808.0" ];
      [ "13835058055282163712"; "13835058055282163712.0" ];
      [ "-4611686018427387904"; "-4611686018427387904.0" ];
    ];
  let distinct =
    [ "1"; {|"1"|}; "[1]"; {|{"a":1}|}; {|{"a":2}|}; {|{"b":1}|}; "null"; "false"; "true"; "1.5"; "[1,2]"; "[2,1]" ]
  in
  List.iteri
    (fun i x ->
      List.iteri
        (fun j y ->
          let a = must_parse x and b = must_parse y in
          Check.checkf (root_equal a b = (i = j)) "equal(%s, %s) = %b" x y (root_equal a b))
        distinct)
    distinct

let test_all_unique () =
  Check.section "TestAllUnique";
  let scratch = Values.create_scratch () in
  let unique json =
    let d = must_parse json in
    Values.all_unique d (Document.root d) scratch
  in
  List.iter
    (fun (json, want) -> Check.checkf (unique json = want) "unique(%s) = %b" json (unique json))
    [
      ("[]", true);
      ("[1]", true);
      ("[1,2]", true);
      ("[1,1.0]", false);
      ({|["a","b","a"]|}, false);
      ({|["a","b","ab"]|}, true);
      ({|[{"a":1,"b":2},{"b":2,"a":1}]|}, false);
      ("[[1],[1,2],[2,1]]", true);
      ({|[1,"1",true,null,[1],{"1":1}]|}, true);
    ];
  let items = ref [] in
  for i = 99 downto 0 do
    items := Printf.sprintf {|{"n":%d,"s":"%d"}|} i (i mod 7) :: string_of_int i :: Printf.sprintf {|"%d"|} i :: !items
  done;
  let joined = String.concat "," !items in
  Check.check "300 distinct items are unique" (unique ("[" ^ joined ^ "]"));
  Check.check "a duplicate among 301 items is found" (not (unique ("[" ^ joined ^ {|,{"s":"3","n":3.0}]|})))

let test_str_hash_reads_every_byte () =
  Check.section "TestStrHashReadsEveryByte";
  let base = "abcdefghijklmnopqrstuvwxyz" in
  for n = 0 to String.length base do
    let h = Document.str_hash (Bytes.of_string base) 0 n in
    for i = 0 to n - 1 do
      for bit = 0 to 7 do
        let changed = Bytes.of_string (String.sub base 0 n) in
        Bytes.set changed i (Char.chr (Char.code (Bytes.get changed i) lxor (1 lsl bit)));
        Check.checkf
          (Document.str_hash changed 0 n <> h)
          "length %d: bit %d of byte %d does not affect the hash" n bit i
      done
    done
  done

(* xorshift is a small deterministic generator for the tests that compare against a reference. The state is 64 bits,
   as in Go. *)
let xorshift_next (state : int64 ref) n =
  let x = !state in
  let x = Int64.logxor x (Int64.shift_left x 13) in
  let x = Int64.logxor x (Int64.shift_right_logical x 7) in
  let x = Int64.logxor x (Int64.shift_left x 17) in
  state := x;
  Int64.to_int (Int64.unsigned_rem x (Int64.of_int n))

let is_ascii s =
  let ok = ref true in
  String.iter (fun c -> if c >= '\128' then ok := false) s;
  !ok

let test_strings_agree_with_the_reference () =
  Check.section "TestStringsAgreeWithEncodingJSON";
  (* Each piece as it is written in JSON text, and what it reads as. *)
  let pieces =
    [|
      ("a", "a");
      ("b", "b");
      ("z", "z");
      (" ", " ");
      ("0", "0");
      ("/", "/");
      ("\xc3\xa9", "\xc3\xa9");
      ("\xe6\x97\xa5", "\xe6\x97\xa5");
      ("\xf0\x9f\x98\x80", "\xf0\x9f\x98\x80");
      ({|\n|}, "\n");
      ({|\t|}, "\t");
      ({|\"|}, "\"");
      ({|\\|}, "\\");
      ({|\/|}, "/");
      ({|A|}, "A");
      ({|é|}, "\xc3\xa9");
      ({|😀|}, "\xf0\x9f\x98\x80");
      ({|\b|}, "\b");
      ({|\f|}, "\012");
      ({|\r|}, "\r");
      ("abcdefgh", "abcdefgh");
      ("ABCDEFGHIJKLMNOP", "ABCDEFGHIJKLMNOP");
      ("\x7f", "\x7f");
    |]
  in
  let breakers = [| "\n"; "\x00"; "\x1f"; {|\x|}; {|\u12G|}; "\xff"; "\xc3"; {|\ud800|}; {|\udc00x|} |] in
  let seed = ref 0x2545f4914f6cdd1dL in
  let parser = Document.create_parser () in
  for _ = 1 to 20000 do
    let sb = Buffer.create 64 and want = Buffer.create 64 in
    Buffer.add_char sb '"';
    for _ = 1 to xorshift_next seed 24 do
      let text, read = pieces.(xorshift_next seed (Array.length pieces)) in
      Buffer.add_string sb text;
      Buffer.add_string want read
    done;
    let broken = xorshift_next seed 8 = 0 in
    if broken then begin
      Buffer.add_string sb breakers.(xorshift_next seed (Array.length breakers));
      for _ = 1 to xorshift_next seed 12 do
        Buffer.add_string sb (fst pieces.(xorshift_next seed (Array.length pieces)))
      done
    end;
    Buffer.add_char sb '"';
    let text = Buffer.contents sb and want = Buffer.contents want in
    match Document.parse_string text with
    | Ok _ when broken -> Check.fail (Printf.sprintf "%S parsed" text)
    | Error _ when broken ->
        Check.checkf (not (Document.is_valid parser (Bytes.of_string text))) "%S is valid to the syntax check" text
    | Error e -> Check.fail (Printf.sprintf "%S: %s" text (Document.error_to_string e))
    | Ok d ->
        let root = Document.root d in
        let got = Document.string_value d root in
        if got <> want then Check.fail (Printf.sprintf "%S read as %S, want %S" text got want)
        else if Document.str_ascii d root <> is_ascii want then
          Check.fail (Printf.sprintf "%S: ASCII flag %b" text (Document.str_ascii d root))
        else Check.check "string" (Document.is_valid parser (Bytes.of_string text))
  done

let test_numbers_agree_with_the_reference () =
  Check.section "TestNumbersAgreeWithStrconv";
  let seed = ref 0x9e3779b97f4a7c15L in
  let digits n = String.init n (fun _ -> Char.chr (48 + xorshift_next seed 10)) in
  for _ = 1 to 50000 do
    let sb = Buffer.create 64 in
    if xorshift_next seed 3 = 0 then Buffer.add_char sb '-';
    if xorshift_next seed 6 = 0 then Buffer.add_char sb '0'
    else begin
      Buffer.add_char sb (Char.chr (49 + xorshift_next seed 9));
      Buffer.add_string sb (digits (xorshift_next seed 24))
    end;
    let floating = ref false in
    if xorshift_next seed 2 = 0 then begin
      floating := true;
      Buffer.add_char sb '.';
      Buffer.add_string sb (digits (1 + xorshift_next seed 24))
    end;
    if xorshift_next seed 3 = 0 then begin
      floating := true;
      Buffer.add_char sb "eE".[xorshift_next seed 2];
      Buffer.add_string sb [| ""; "+"; "-" |].(xorshift_next seed 3);
      Buffer.add_string sb (string_of_int (xorshift_next seed 40))
    end;
    let text = Buffer.contents sb in
    let d = must_parse (" " ^ text ^ " ") in
    let cls, data = go_number d in
    Check.equal_string "the text of the number" ~expected:text (Document.number_text d (Document.root d));
    let signed = if !floating || text = "-0" then None else Int64.of_string_opt text in
    let unsigned = if !floating || text.[0] = '-' || signed <> None then None else Int64.of_string_opt ("0u" ^ text) in
    match (signed, unsigned) with
    | Some v, _ -> Check.checkf (cls = go_int && Int64.equal data v) "%s: representation %d, value %Ld" text cls data
    | None, Some v ->
        Check.checkf (cls = go_uint && Int64.equal data v) "%s: representation %d, value %Lu" text cls data
    | None, None ->
        let want = float_of_string text in
        Check.checkf
          (cls = go_float && Int64.equal data (Int64.bits_of_float want))
          "%s: representation %d, value %h, want %h" text cls (Int64.float_of_bits data) want
  done

(* ---------------------------------------------------------------------------------------------------------------------
   TestDocument.pas *)

let parse_into p d text = Document.parse_into p d (Bytes.of_string text) (String.length text)

let round_trip json expected =
  match Document.parse_string json with
  | Error e -> Check.fail (json ^ " -> " ^ Document.error_to_string e)
  | Ok d -> Check.equal_string json ~expected (Document.to_string d)

let refused json message offset =
  match Document.parse_string json with
  | Ok _ -> Check.fail (json ^ " was accepted")
  | Error e ->
      Check.checkf
        (e.Document.message = message && e.Document.offset = offset)
        "%s -> %s"
        (String.sub json 0 (min 40 (String.length json)))
        (Document.error_to_string e);
      (* The validating parser agrees. *)
      Check.checkf
        (not (is_valid_text json))
        "%s was valid to the validating parser"
        (String.sub json 0 (min 40 (String.length json)))

let bits json cls (expected : int64) =
  let got_cls, got = number json in
  Check.checkf (got_cls = cls && Int64.equal got expected) "%s -> class %d data %Lx" json got_cls got

let multiple x d expected = Check.checkf (divides x d = expected) "%s multipleOf %s -> %b" x d (divides x d)

let compare a b expected =
  let actual = compare_json a b in
  let actual = if actual < 0 then -1 else if actual > 0 then 1 else 0 in
  Check.checkf (actual = expected) "%s compared with %s -> %d" a b actual

let test_pascal_document () =
  Check.section "TestDocument";
  (* Values, whitespace, strings and escapes. *)
  round_trip " null " "null";
  round_trip "true" "true";
  round_trip "false" "false";
  round_trip "[]" "[]";
  round_trip "{}" "{}";
  round_trip {| [ 1 , 2.50 , -3e2 , "a" , null , [ ] , { } ] |} {|[1,2.50,-3e2,"a",null,[],{}]|};
  round_trip {|{"a": {"b": [1, {"c": true}]}, "d": "e"}|} {|{"a":{"b":[1,{"c":true}]},"d":"e"}|};
  round_trip {|"a\"b\\c\/d\b\f\n\r\t"|} {|"a\"b\\c/d\b\f\n\r\t"|};
  round_trip "\"A\xc3\xa9\xe2\x82\xac\xf0\x9f\x98\x80\"" "\"A\xc3\xa9\xe2\x82\xac\xf0\x9f\x98\x80\"";
  round_trip {|"\u0000\u001f"|} {|"\u0000\u001f"|};
  round_trip "\"\xc3\xa9caf\xc3\xa9 plain text longer than eight bytes\""
    "\"\xc3\xa9caf\xc3\xa9 plain text longer than eight bytes\"";
  round_trip {|"exactly8"|} {|"exactly8"|};
  round_trip {|"1234567\""|} {|"1234567\""|};

  (* Of duplicate names the last value is kept, at the position of the first. *)
  round_trip {|{"a": 1, "b": 2, "a": 3}|} {|{"a":3,"b":2}|};
  round_trip {|{"a": 1, "b": 2, "c": 3, "a": 4, "b": 5}|} {|{"a":4,"b":5,"c":3}|};
  let big = Buffer.create 256 in
  Buffer.add_string big "{";
  for i = 0 to 39 do
    Buffer.add_string big (Printf.sprintf {|"k%d": %d, |} i i)
  done;
  Buffer.add_string big {|"k7": 700, "k39": 3900}|};
  (match Document.parse_string (Buffer.contents big) with
  | Ok d ->
      let root = Document.root d in
      Check.check "forty names after duplicates in a large object" (Document.count d root = 40);
      Check.check "the last value of k7" (Document.data d (Document.property d root "k7") = 700);
      Check.check "the last value of k39" (Document.data d (Document.property d root "k39") = 3900);
      Check.check "the value of k8" (Document.data d (Document.property d root "k8") = 8);
      Check.check "no k40" (Document.property d root "k40" = -1)
  | Error e -> Check.fail ("a large object with duplicates: " ^ Document.error_to_string e));

  (* Errors, with the offsets the Go parser reports. *)
  refused "" "unexpected end of input" 0;
  refused "nul" "expected a value" 0;
  refused "[1,]" "expected a value" 3;
  refused "[1 2]" "expected ',' or ']'" 3;
  refused {|{"a" 1}|} "expected ':'" 5;
  refused {|{"a": 1,}|} "expected a property name" 8;
  refused {|{"a": 1 "b": 2}|} "expected ',' or '}'" 8;
  refused "{1: 2}" "expected a property name" 1;
  refused "1 2" "trailing characters" 2;
  refused {|"abc|} "unterminated string" 4;
  refused "\"a\nb\"" "control character in a string" 2;
  refused {|"\x"|} "invalid escape" 1;
  refused {|"\u12g4"|} "invalid \\u escape" 1;
  refused {|"\ud800"|} "lone leading surrogate in hex escape" 1;
  refused {|"\udc00"|} "lone trailing surrogate in hex escape" 1;
  refused "\"\xc3\"" "invalid UTF-8" 1;
  refused "\"\xed\xa0\x80\"" "invalid UTF-8" 1;
  refused "\"\xc0\x80\"" "invalid UTF-8" 1;
  refused "01" "trailing characters" 1;
  refused "-" "invalid number" 1;
  refused "1." "invalid number" 2;
  refused "1e" "invalid number" 2;
  refused "1e400" "number out of range" 0;
  refused "-1e400" "number out of range" 0;
  refused (String.make (Document.max_depth + 1) '[') "nesting too deep" (Document.max_depth + 1);
  Check.check "nesting of the maximum depth is accepted"
    (Result.is_ok (Document.parse_string (String.make Document.max_depth '[' ^ String.make Document.max_depth ']')));
  Check.check "text of the maximum size is within it"
    (Document.within_size Document.max_document_size Document.max_document_size);
  Check.check "text beyond the maximum size is not"
    (not (Document.within_size (Document.max_document_size + 1) Document.max_document_size));
  Check.check "the maximum size is 2^31 - 1" (Document.max_document_size = (1 lsl 31) - 1);

  (* Numbers: integers stay integers, and a float is the correctly rounded one. *)
  bits "0" go_int 0L;
  bits "-0" go_float 0x8000000000000000L;
  bits "42" go_int 42L;
  bits "-42" go_int (-42L);
  bits "9223372036854775807" go_int 0x7FFFFFFFFFFFFFFFL;
  bits "-9223372036854775808" go_int 0x8000000000000000L;
  bits "9223372036854775808" go_uint 0x8000000000000000L;
  bits "18446744073709551615" go_uint 0xFFFFFFFFFFFFFFFFL;
  bits "18446744073709551616" go_float 0x43F0000000000000L;
  bits "-9223372036854775809" go_float 0xC3E0000000000000L;
  bits "1.0" go_float 0x3FF0000000000000L;
  bits "0.1" go_float 0x3FB999999999999AL;
  bits "1e22" go_float 0x4480F0CF064DD592L;
  bits "1e23" go_float 0x44B52D02C7E14AF6L;
  bits "123456789012345678901234567890" go_float 0x45F8EE90FF6C373EL;
  bits "2.2250738585072014e-308" go_float 0x0010000000000000L;
  bits "2.2250738585072011e-308" go_float 0x000FFFFFFFFFFFFFL;
  bits "5e-324" go_float 1L;
  bits "4.9406564584124654e-324" go_float 1L;
  bits "2.4703282292062328e-324" go_float 1L;
  bits "2.4703282292062327e-324" go_float 0L;
  bits "1e-400" go_float 0L;
  bits "1.7976931348623157e308" go_float 0x7FEFFFFFFFFFFFFFL;
  bits "1.7976931348623158e308" go_float 0x7FEFFFFFFFFFFFFFL;
  bits "9007199254740993" go_int 9007199254740993L;
  bits "9007199254740993.0" go_float 0x4340000000000000L;
  bits "9007199254740993.00000000000000000001" go_float 0x4340000000000001L;
  bits "9007199254740995.0" go_float 0x4340000000000002L;
  (* The suite's bignum.json: the long text and the shortest text of its float read as the same float. *)
  bits "972783798187987123879878123.18878137" go_float 0x45892557DAF10FBEL;
  bits "9.727837981879871e+26" go_float 0x45892557DAF10FBEL;
  (* More than nineteen digits, where the digits beyond them decide the rounding. *)
  bits "1.00000000000000011102230246251565404236316680908203125" go_float 0x3FF0000000000000L;
  bits "1.00000000000000011102230246251565404236316680908203126" go_float 0x3FF0000000000001L;
  bits "1.00000000000000033306690738754696212708950042724609375" go_float 0x3FF0000000000002L;
  bits "1.00000000000000033306690738754696212708950042724609374" go_float 0x3FF0000000000001L;
  (* The edges of an OCaml int, which Go and Pascal do not have: every integer of 64 bits keeps its value. *)
  bits "4611686018427387903" go_int 0x3FFFFFFFFFFFFFFFL;
  bits "4611686018427387904" go_int 0x4000000000000000L;
  bits "4611686018427387905" go_int 0x4000000000000001L;
  bits "-4611686018427387904" go_int 0xC000000000000000L;
  bits "-4611686018427387905" go_int 0xBFFFFFFFFFFFFFFFL;
  bits "-9223372036854775807" go_int 0x8000000000000001L;
  bits "13835058055282163711" go_uint 0xBFFFFFFFFFFFFFFFL;
  bits "13835058055282163712" go_uint 0xC000000000000000L;
  bits "9999999999999999999" go_uint 0x8AC7230489E7FFFFL;
  bits "1000000000000000000" go_int 1000000000000000000L;
  bits "999999999999999999" go_int 999999999999999999L;

  (* Comparisons are exact across the representations. *)
  compare "1" "1.0" 0;
  compare "1" "1.5" (-1);
  compare "-1" "-1.5" 1;
  compare "9007199254740993" "9007199254740992.0" 1;
  compare "9223372036854775807" "9223372036854775808" (-1);
  compare "9223372036854775808" "9223372036854775808.0" 0;
  compare "18446744073709551615" "18446744073709551616" (-1);
  compare "18446744073709551615" "1.8446744073709552e19" (-1);
  compare "9223372036854775807" "9223372036854775808.0" (-1);
  compare "-9223372036854775808" "-9223372036854775808.0" 0;
  compare "-9223372036854775808" "-9223372036854775809" 0;
  compare "1e300" "18446744073709551615" 1;
  compare "-1e300" "-9223372036854775808" (-1);
  compare "0.5" "0" 1;
  compare "-0.5" "0" (-1);
  compare "-0" "0" 0;
  (* The same across the edge of an OCaml int, in every pairing of the classes. *)
  let ordered =
    [|
      "-1e300";
      "-9223372036854775809.5e3";
      "-9223372036854775808";
      "-9223372036854775807";
      "-4611686018427388928.0";
      "-4611686018427387905";
      "-4611686018427387904";
      "-4611686018427387903";
      "-4611686018427387392.0";
      "-1.5";
      "-1";
      "0";
      "0.5";
      "1";
      "4611686018427387392.0";
      "4611686018427387903";
      "4611686018427387904";
      "4611686018427387905";
      "4611686018427388928.0";
      "9223372036854775807";
      "9223372036854775808";
      "9223372036854775809";
      "9223372036854777856.0";
      "13835058055282163711";
      "13835058055282163712";
      "13835058055282163713";
      "13835058055282165760.0";
      "18446744073709551615";
      "18446744073709551616";
      "1e300";
    |]
  in
  Array.iteri
    (fun i a -> Array.iteri (fun j b -> compare a b (if i < j then -1 else if i > j then 1 else 0)) ordered)
    ordered;
  compare "4611686018427387904" "4611686018427387904.0" 0;
  compare "-4611686018427387904" "-4611686018427387904.0" 0;
  compare "9223372036854775808" "9.223372036854775808e18" 0;
  compare "13835058055282163712" "13835058055282163712.0" 0;
  compare "-9223372036854775808" "-9.223372036854775808e18" 0;

  (* multipleOf is decided on the decimal digits. *)
  multiple "10" "2" true;
  multiple "7" "2" false;
  multiple "0" "3" true;
  multiple "0.0" "0.1" true;
  multiple "0.0075" "0.0001" true;
  multiple "0.00751" "0.0001" false;
  multiple "4.5" "1.5" true;
  multiple "35" "1.5" false;
  multiple "1e308" "1e-8" true;
  multiple "12391239123" "0.01" true;
  multiple "1e2" "100" true;
  multiple "100" "1e2" true;
  multiple "150" "1e2" false;
  multiple "1.0" "1" true;
  multiple "3" "0.5" true;
  multiple "3" "0" false;
  multiple "-9223372036854775808" "-1" true;
  multiple "9223372036854775808" "2" true;
  multiple "18446744073709551615" "5" true;
  multiple "18446744073709551615" "2" false;
  multiple "246913578246913578246913578" "123456789123456789123456789" true;
  multiple "246913578246913578246913579" "123456789123456789123456789" false;
  multiple "1234567891234567891.23456789" "0.000000000123456789123456789123456789" true;
  multiple "1234567891234567891234567890" "12345678912345678912345678900" false;
  multiple "123456789123456789123456789000" "12345678912345678912345678900" true;
  multiple "1e300" "1e299" true;
  multiple "1e-300" "1e-299" false;
  multiple "1e-299" "1e-300" true;
  (* Divisors of 17 and of 18 digits, on either side of what the streamed remainder takes here. *)
  multiple "24691357802469135" "12345678901234567" false;
  multiple "24691357802469134" "12345678901234567" true;
  multiple "246913578024691356" "123456789012345678" true;
  multiple "246913578024691357" "123456789012345678" false;
  multiple "-4611686018427387904" "-1" true;
  multiple "-4611686018427387904" "2" true;
  multiple "4611686018427387904" "4611686018427387904" true;
  multiple "9223372036854775808" "4611686018427387904" true;
  multiple "9223372036854775809" "4611686018427387904" false;

  (* Parsing into reused buffers gives the same document, and allocates no new arrays once they have grown. *)
  let p = Document.create_parser () in
  let d = Document.create_empty () in
  Check.check "parse into" (parse_into p d {|{"a": [1, 2, {"b": "c\n"}], "d": 1.5}|});
  Check.equal_string "parse into" ~expected:{|{"a":[1,2,{"b":"c\n"}],"d":1.5}|} (Document.to_string d);
  Check.check "parse into refuses bad text" (not (parse_into p d "[true"));
  Check.equal_string "parse into reports the error" ~expected:"unexpected end of input"
    (Document.parser_error p).Document.message;
  Check.check "parse into after a failure" (parse_into p d {|[false, "x"]|} && Document.to_string d = {|[false,"x"]|});
  Check.check "the validating parser accepts valid text" (Document.is_valid p (Bytes.of_string {|[false, "x"]|}))

(* Words allocated on the minor heap by f, beyond what the measurement itself allocates. *)
let minor_words f =
  let before = Gc.minor_words () in
  let middle = Gc.minor_words () in
  f ();
  let after = Gc.minor_words () in
  int_of_float (after -. middle -. (middle -. before))

let test_parsing_into_reused_buffers_allocates_nothing () =
  Check.section "parsing into reused buffers";
  let p = Document.create_parser () in
  let d = Document.create_empty () in
  let texts =
    Array.map Bytes.of_string
      [|
        {|{"a": [1, 2, {"b": "c\n"}], "d": 1.5, "e": "café", "a": null}|};
        {|[0.1, 1e22, 1e23, 123456789012345678.25, 9007199254740993.0, 18446744073709551615, -9223372036854775808]|};
        {|[972783798187987123879878123.18878137, 2.2250738585072011e-308, 1.7976931348623157e308, 4.35, 1e-7]|};
        {|{"k0":0,"k1":1,"k2":2,"k3":3,"k4":4,"k5":5,"k6":6,"k7":7,"k8":8,"k9":9,"k10":10,"k11":11,"k12":12,"k13":13,
          "k14":14,"k15":15,"k16":16,"k17":17,"k3":"three"}|};
        {|[1, 2|};
      |]
  in
  let run () =
    for i = 0 to Array.length texts - 1 do
      ignore (Document.parse_into p d texts.(i) (Bytes.length texts.(i)) : bool);
      ignore (Document.is_valid p texts.(i) : bool)
    done
  in
  run ();
  run ();
  Check.equal_int "words allocated by a pass over the texts" ~expected:0 (minor_words run)

(* TestDoubleCases and TestStringCases of the Object Pascal port read files of cases that a reference implementation
   wrote. They run here when the environment names the files. *)

let read_lines path =
  let ic = open_in_bin path in
  let lines = ref [] in
  (try
     while true do
       lines := input_line ic :: !lines
     done
   with End_of_file -> close_in ic);
  List.rev !lines

let test_double_cases path =
  Check.section "TestDoubleCases";
  List.iter
    (fun line ->
      match String.index_opt line ' ' with
      | None -> ()
      | Some space -> (
          let text = String.sub line 0 space in
          let expected = String.sub line (space + 1) 16 in
          match Document.parse_string text with
          | Error e -> Check.fail (String.sub text 0 (min 60 (String.length text)) ^ " did not parse: " ^ e.message)
          | Ok d ->
              let cls, data = go_number d in
              (* An integer that fits 64 bits is kept as one: its bits are not a float's. *)
              if cls = go_float then Check.equal_string text ~expected (Printf.sprintf "%016LX" data)))
    (read_lines path)

let from_hex s = String.init (String.length s / 2) (fun i -> Char.chr (int_of_string ("0x" ^ String.sub s (2 * i) 2)))

let test_string_cases path =
  Check.section "TestStringCases";
  let p = Document.create_parser () in
  List.iter
    (fun line ->
      match String.index_opt line ' ' with
      | Some space when space < String.length line - 1 -> (
          let text = from_hex (String.sub line 0 space) in
          let flag = line.[space + 1] in
          let want = from_hex (String.sub line (space + 2) (String.length line - space - 2)) in
          let valid = Document.is_valid p (Bytes.of_string text) in
          match Document.parse_string text with
          | Ok d when flag <> 'B' ->
              let root = Document.root d in
              Check.checkf
                (valid && Document.string_value d root = want && Document.str_ascii d root = (flag = 'A'))
                "%s read differently" line
          | Ok _ -> Check.fail (line ^ " parsed")
          | Error _ when flag = 'B' -> Check.checkf (not valid) "%s is valid to the syntax check" line
          | Error e -> Check.fail (line ^ " did not parse: " ^ Document.error_to_string e))
      | _ -> ())
    (read_lines path)

let () =
  test_parse_round_trips ();
  test_parse_rejects_invalid_json ();
  test_duplicate_keys ();
  test_numbers ();
  test_multiple_of_is_exact ();
  test_equality_and_hashing ();
  test_all_unique ();
  test_str_hash_reads_every_byte ();
  test_strings_agree_with_the_reference ();
  test_numbers_agree_with_the_reference ();
  test_pascal_document ();
  test_parsing_into_reused_buffers_allocates_nothing ();
  (match Sys.getenv_opt "CORVUS_DOUBLE_CASES" with Some path -> test_double_cases path | None -> ());
  (match Sys.getenv_opt "CORVUS_STRING_CASES" with Some path -> test_string_cases path | None -> ());
  Check.finish "document"
