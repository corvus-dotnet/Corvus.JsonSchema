(* The tests of the format checks, ported from TestFormats.pas of the Object Pascal port.

   The first part is the format tests of the JSON-Schema-Test-Suite (tests/<draft>/optional/format/*.json), run as
   suite_test.go of the Go module runs them: for draft 4, 6, 7, 2019-09 and 2020-12, each with the formats and the
   host name rules of its dialect. A value that is not a string is valid for every format, and so is every value for
   a format the dialect does not know. A case that fails and whose description contains "leap second" is counted as
   skipped and not as a failure, exactly as in the Go runner. No case is skipped before it has run.

   The second part is the cases of the Go tests that exercise formats.go directly: TestUnicodeFormatsUseUnicode17 and
   BenchmarkUnicodeFormats of unicode_test.go, and the format and content cases of api_test.go.

   The third part checks that the checks do not allocate, which the Go source gets from slicing its strings.

   The fourth part, which runs only when it is asked for, compares the checks with answers written out from the Go
   source for many more strings than the suite holds.

   Usage: formats_test [suite-root] [--vectors file]

   The suite is at suite-root, or at the path in JSON_SCHEMA_TEST_SUITE, or is the submodule of the repository. Where
   there is none (the published package built on its own), the first part says that it was skipped. Each
   line of a vectors file is a format kind, 1 for the host name rules of draft 4 and 6, the bytes of a string in
   hexadecimal and the expected answer (1 or 0), separated by tabs.

   Two parts of the sources have no port here.

   - TestNumbers and the numbers file of TestFormats.pas test the numeric formats (checkNumber of formats.go). That
     check reads a number of a document, so it is with the document and not in Formats, and its tests are with it.
   - TestNoToolchainUnicodeData of unicode_test.go reads the Go source for uses of the Unicode data of the Go
     toolchain. It has no equivalent: the OCaml standard library has no Unicode data for a module to take by mistake.
     The asciiLower case of TestUnicodeFormatsUseUnicode17 tests a function of uri.go and is with the tests of that
     module. *)

module Check = Corvus_json_schema_test_support.Check

(* The module under test. *)
module Formats = Corvus_json_schema__Formats

(* ------------------------------------------------------------------------------------------------------------------ *)
(* A reader of JSON text, enough for the files of the test suite *)

type json =
  | Null
  | False
  | True
  | Number of string  (** A number as it was written. *)
  | String of string  (** The text of a string, with its escapes read. *)
  | Array of json list
  | Object of (string * json) list

exception Bad_json

(* add_rune appends a code point to the text as UTF-8. *)
let add_rune out c = Buffer.add_utf_8_uchar out (Uchar.of_int c)

let hex_value c =
  match c with
  | '0' .. '9' -> Char.code c - 0x30
  | 'a' .. 'f' -> Char.code c - 0x61 + 10
  | 'A' .. 'F' -> Char.code c - 0x41 + 10
  | _ -> raise Bad_json

let parse_json source =
  let pos = ref 0 in
  let length = String.length source in
  let peek () = if !pos < length then source.[!pos] else '\000' in
  let skip_space () =
    while !pos < length && match source.[!pos] with '\t' | '\n' | '\r' | ' ' -> true | _ -> false do
      incr pos
    done
  in
  let read_hex4 () =
    if !pos + 4 > length then raise Bad_json;
    let v = ref 0 in
    for _ = 0 to 3 do
      v := (!v * 16) + hex_value source.[!pos];
      incr pos
    done;
    !v
  in
  let read_string () =
    let out = Buffer.create 32 in
    (* The opening quote. *)
    incr pos;
    let closed = ref false in
    while not !closed do
      if !pos >= length then raise Bad_json;
      let c = source.[!pos] in
      incr pos;
      if c = '"' then closed := true
      else if c <> '\\' then Buffer.add_char out c
      else begin
        let c = peek () in
        incr pos;
        match c with
        | 'b' -> add_rune out 8
        | 'f' -> add_rune out 12
        | 'n' -> add_rune out 10
        | 'r' -> add_rune out 13
        | 't' -> add_rune out 9
        | 'u' ->
            let u = read_hex4 () in
            if u >= 0xd800 && u <= 0xdbff && !pos + 1 < length && source.[!pos] = '\\' && source.[!pos + 1] = 'u' then begin
              pos := !pos + 2;
              let low = read_hex4 () in
              if low >= 0xdc00 && low <= 0xdfff then add_rune out (0x10000 + ((u - 0xd800) lsl 10) + (low - 0xdc00))
              else raise Bad_json
            end
            else if u >= 0xd800 && u <= 0xdfff then raise Bad_json
            else add_rune out u
        | '"' | '\\' | '/' -> Buffer.add_char out c
        | _ -> raise Bad_json
      end
    done;
    Buffer.contents out
  in
  let literal text value =
    if !pos + String.length text > length || String.sub source !pos (String.length text) <> text then raise Bad_json;
    pos := !pos + String.length text;
    value
  in
  let rec read_value () =
    skip_space ();
    match peek () with
    | '"' -> String (read_string ())
    | '{' ->
        incr pos;
        let members = ref [] in
        let closed = ref false in
        while not !closed do
          skip_space ();
          match peek () with
          | '}' ->
              incr pos;
              closed := true
          | ',' -> incr pos
          | '"' ->
              let key = read_string () in
              skip_space ();
              if peek () <> ':' then raise Bad_json;
              incr pos;
              let value = read_value () in
              members := (key, value) :: !members
          | _ -> raise Bad_json
        done;
        Object (List.rev !members)
    | '[' ->
        incr pos;
        let items = ref [] in
        let closed = ref false in
        while not !closed do
          skip_space ();
          match peek () with
          | ']' ->
              incr pos;
              closed := true
          | ',' -> incr pos
          | _ -> items := read_value () :: !items
        done;
        Array (List.rev !items)
    | 't' -> literal "true" True
    | 'f' -> literal "false" False
    | 'n' -> literal "null" Null
    | '-' | '0' .. '9' ->
        let start = !pos in
        while
          !pos < length && match source.[!pos] with '0' .. '9' | '-' | '+' | '.' | 'e' | 'E' -> true | _ -> false
        do
          incr pos
        done;
        Number (String.sub source start (!pos - start))
    | _ -> raise Bad_json
  in
  let value = read_value () in
  skip_space ();
  if !pos <> length then raise Bad_json;
  value

(* member is the value of a member of an object. *)
let member value name = match value with Object members -> List.assoc_opt name members | _ -> None

(* ------------------------------------------------------------------------------------------------------------------ *)
(* Files and text *)

let read_file path = try Some (In_channel.with_open_bin path In_channel.input_all) with Sys_error _ -> None

(* show is the text with every byte that is not printable ASCII written as \xNN. *)
let show s =
  let out = Buffer.create (String.length s) in
  String.iter
    (fun c ->
      if Char.code c >= 0x20 && Char.code c < 0x7f then Buffer.add_char out c
      else Buffer.add_string out (Printf.sprintf "\\x%02X" (Char.code c)))
    s;
  Buffer.contents out

(* from_hex is the text whose bytes are written in hexadecimal. *)
let from_hex hex =
  String.init (String.length hex / 2) (fun i -> Char.chr ((hex_value hex.[2 * i] * 16) + hex_value hex.[(2 * i) + 1]))

(* u is the UTF-8 text of code points. *)
let u code_points =
  let out = Buffer.create 16 in
  List.iter (add_rune out) code_points;
  Buffer.contents out

(* contains_lower reports whether the text, with its letters A to Z in lower case, contains the other text. *)
let contains_lower s sub =
  let lower = String.lowercase_ascii s in
  let n = String.length sub in
  let found = ref false in
  for i = 0 to String.length lower - n do
    if String.sub lower i n = sub then found := true
  done;
  !found

(* ------------------------------------------------------------------------------------------------------------------ *)
(* Counts *)

type tally = { mutable run : int; mutable passed : int; mutable skipped : int; mutable failed : int }

(* The cases of the suite by format, in the order the formats were first seen. *)
let tallies : (string * tally) list ref = ref []

let tally_of name =
  match List.assoc_opt name !tallies with
  | Some tally -> tally
  | None ->
      let tally = { run = 0; passed = 0; skipped = 0; failed = 0 } in
      tallies := !tallies @ [ (name, tally) ];
      tally

(* The cases of the suite: all of them, those that ran, and how they ended. *)
let suite_total = ref 0
let suite_run = ref 0
let suite_passed = ref 0
let suite_skipped = ref 0
let suite_failed = ref 0
let suite_not_format = ref 0
let suite_files = ref 0
let vectors_run = ref 0
let vectors_failed = ref 0
let check name got want = Check.check (Printf.sprintf "%s: got %b, want %b" name got want) (got = want)

(* The text a check reads is inside a larger buffer, so that a check that read outside its bounds would read other
   bytes. *)
let before = "a.b:/"
let after = "0Z@x:"

(* padded is the text inside a larger buffer. The text starts at the length of [before]. *)
let padded s = Bytes.of_string (before ^ s ^ after)

(* check_kind runs the check of a format kind over the text. *)
let check_kind kind s legacy = Formats.check_string kind (padded s) (String.length before) (String.length s) legacy

(* ------------------------------------------------------------------------------------------------------------------ *)
(* The JSON-Schema-Test-Suite *)

(* The drafts of the suite, each with the rank of its dialect. *)
let suite_drafts = [ ("draft4", 0); ("draft6", 1); ("draft7", 2); ("draft2019-09", 3); ("draft2020-12", 4) ]
let draft6 = 1
let draft2020_12 = 4

let run_suite_file path file_label dialect =
  match read_file path with
  | None -> Check.fail ("cannot read " ^ path)
  | Some source -> (
      match try parse_json source with Bad_json -> Null with
      | Array groups ->
          incr suite_files;
          let legacy = dialect <= draft6 in
          List.iter
            (fun group ->
              let group_name = match member group "description" with Some (String s) -> s | _ -> "" in
              (* A schema that is just a format: an object with a format, and nothing else but the dialect it
                 names. *)
              let format =
                match member group "schema" with
                | Some (Object members as schema)
                  when List.for_all (fun (key, _) -> key = "format" || key = "$schema") members -> (
                    match member schema "format" with Some (String s) -> Some s | _ -> None)
                | _ -> None
              in
              let tests = match member group "tests" with Some (Array tests) -> tests | _ -> [] in
              List.iter
                (fun test ->
                  incr suite_total;
                  let test_name = match member test "description" with Some (String s) -> s | _ -> "" in
                  match (format, member test "data", member test "valid") with
                  | Some format_name, Some data, Some valid ->
                      let kind = Formats.kind_of format_name dialect in
                      let tally = tally_of format_name in
                      let expected = valid = True in
                      (* Data that is not a string is valid for every format, and check_string accepts every string
                         for a format the dialect does not know and for a numeric format. *)
                      let actual = match data with String text -> check_kind kind text legacy | _ -> true in
                      (* The validator by name is the same check, for the formats the latest dialect knows. *)
                      (match data with
                      | String text when dialect = draft2020_12 -> (
                          match Formats.validator format_name with
                          | Some named ->
                              check
                                (Printf.sprintf "validator(%s) agrees for \"%s\"" format_name (show text))
                                (named (Bytes.of_string text) 0 (String.length text))
                                actual
                          | None ->
                              check (Printf.sprintf "validator(%s) is None" format_name) (kind = Formats.unknown) true)
                      | _ -> ());
                      incr suite_run;
                      tally.run <- tally.run + 1;
                      if actual = expected then begin
                        incr suite_passed;
                        tally.passed <- tally.passed + 1;
                        Check.check "" true
                      end
                      else if contains_lower test_name "leap second" then begin
                        (* Leap seconds are skipped in the format run, as in the Go and C# runners. *)
                        incr suite_skipped;
                        tally.skipped <- tally.skipped + 1;
                        Printf.printf "skipped: %s [%s] %s\n" file_label group_name test_name
                      end
                      else begin
                        incr suite_failed;
                        tally.failed <- tally.failed + 1;
                        Check.fail
                          (Printf.sprintf "%s [%s] %s: expected %b, got %b for \"%s\"" file_label group_name test_name
                             expected actual
                             (match data with String text -> show text | _ -> ""))
                      end
                  | _ ->
                      incr suite_not_format;
                      Printf.printf "not a format case: %s [%s] %s\n" file_label group_name test_name)
                tests)
            groups
      | _ -> Check.fail ("cannot parse " ^ path))

let run_suite root =
  List.iter
    (fun (name, dialect) ->
      let dir = Filename.concat root (Filename.concat "tests" (Filename.concat name "optional/format")) in
      let names = try Sys.readdir dir with Sys_error _ -> [||] in
      let names = List.filter (fun file -> Filename.check_suffix file ".json") (Array.to_list names) in
      (* The files in the order of their names. *)
      List.iter
        (fun file -> run_suite_file (Filename.concat dir file) (name ^ "/optional/format/" ^ file) dialect)
        (List.sort String.compare names))
    suite_drafts

(* ------------------------------------------------------------------------------------------------------------------ *)
(* The cases of the Go tests *)

(* test_unicode_formats_use_unicode_17 checks format answers that differ between Unicode 15 and the Unicode 17 of the
   tables. *)
let test_unicode_formats_use_unicode_17 () =
  let host name want =
    check (Printf.sprintf "isIDNHostname(\"%s\")" (show name)) (check_kind Formats.idn_hostname name false) want
  in
  let address name want =
    check (Printf.sprintf "isEmail(\"%s\", idn)" (show name)) (check_kind Formats.idn_email name false) want
  in
  (* U+10D4A and U+10D4B are Garay letters, assigned in Unicode 16. U+1C89 is an uppercase letter of Unicode 16, and
     IDNA2008 disallows uppercase letters. U+2FFFF is a noncharacter in every version. *)
  host (u [ 0x10d4a; 0x10d4b ] ^ ".example") true;
  host (u [ 0x1c8a ] ^ ".example") true;
  host (u [ 0x1c89 ] ^ ".example") false;
  host (u [ 0x2ffff ] ^ ".example") false;
  address (u [ 0x10d4a; 0x10d4b ] ^ "@example.com") true;
  address (u [ 0x16ea0; 0x16ebb ] ^ "@example.com") true;
  address (u [ 0x2ffff ] ^ "@example.com") false;
  (* "dokimi" in Greek letters. *)
  address (u [ 0x3b4; 0x3bf; 0x3ba; 0x3b9; 0x3bc; 0x3ae ] ^ "@example.com") true;
  address "a b@example.com" false

(* The strings BenchmarkUnicodeFormats of the Go source requires to be valid. *)
let test_unicode_format_strings () =
  let greek = u [ 0x3b5; 0x3bb; 0x3bb; 0x3b7; 0x3bd; 0x3b9; 0x3ba; 0x3ac ] in
  let japanese = u [ 0x4f8b; 0x3048 ] in
  let arabic = u [ 0x628; 0x64a; 0x631; 0x648; 0x62a ] in
  check "isIDNHostname of a host name in Greek, Japanese and Arabic"
    (check_kind Formats.idn_hostname (greek ^ "." ^ japanese ^ "." ^ arabic ^ ".example") false)
    true;
  let sample = u [ 0x3c0; 0x3b1; 0x3c1; 0x3ac; 0x3b4; 0x3b5; 0x3b9; 0x3b3; 0x3bc; 0x3b1 ] in
  check "isEmail of an address in Greek and Japanese"
    (check_kind Formats.idn_email
       (u [ 0x3b4; 0x3bf; 0x3ba; 0x3b9; 0x3bc; 0x3ae ] ^ "." ^ japanese ^ "@" ^ sample ^ ".example")
       false)
    true

(* TestFormatIsAnAnnotationByDefaultAndAssertedOnRequest of api_test.go, as far as the checks go. *)
let test_ipv4_cases () =
  check "ipv4 \"not an address\"" (check_kind Formats.ipv4 "not an address" false) false;
  check "ipv4 \"10.0.0.1\"" (check_kind Formats.ipv4 "10.0.0.1" false) true;
  check "unknown format accepts anything" (check_kind Formats.unknown "x" false) true;
  check "a numeric format accepts every string" (check_kind Formats.int32 "x" false) true

(* The content cases of api_test.go, as far as the base64 check goes. *)
let test_content () =
  let decode text want_ok want =
    let buffer = padded text in
    let output = Bytes.create (Formats.base64_decoded_capacity (String.length text)) in
    let length = Formats.base64_decode buffer (String.length before) (String.length text) output in
    check (Printf.sprintf "base64 \"%s\"" (show text)) (length >= 0) want_ok;
    if length >= 0 && want_ok then
      Check.equal_string (Printf.sprintf "base64 \"%s\"" (show text)) ~expected:want (Bytes.sub_string output 0 length)
  in
  decode "eyJhIjogMX0=" true "{\"a\": 1}";
  decode "bm90IGpzb24=" true "not json";
  decode "not base64" false "";
  decode "" true "";
  decode "QQ==" true "A";
  decode "QUI=" true "AB";
  decode "QUJD" true "ABC";
  decode "QUJDRA==" true "ABCD";
  decode "+/+/" true (from_hex "fbffbf");
  decode "QQ" false "";
  decode "QQ=" false "";
  decode "Q===" false "";
  decode "====" false "";
  decode "QQ==QUJD" false "";
  decode "QUJD\nQUJD" false "";
  decode "QU-D" false "";
  decode "QU_D" false "";
  decode "Q=JD" false ""

(* The formats each dialect knows, with their names and messages. *)
let test_format_kinds () =
  (* The first dialect that knows each string format, from date to regex. *)
  let since = [| 2; 2; 0; 3; 3; 0; 0; 0; 2; 0; 2; 0; 1; 2; 2; 1; 1; 2; 2 |] in
  for kind = Formats.unknown to Formats.decimal do
    let name = Formats.name kind in
    let is_string_format = kind >= Formats.date && kind <= Formats.regex in
    check ("isNumeric of " ^ name) (Formats.is_numeric kind) (kind >= Formats.byte);
    for dialect = 0 to draft2020_12 do
      let got = Formats.kind_of name dialect in
      if is_string_format then
        if dialect >= since.(kind - Formats.date) then check ("formatKindOf " ^ name) (got = kind) true
        else check ("formatKindOf " ^ name ^ " before its dialect") (got = Formats.unknown) true
      else check ("formatKindOf " ^ name) (got = kind) true
    done;
    check ("message of " ^ name) (String.length (Formats.message kind) <> 0) is_string_format;
    check ("validator of " ^ name) (Formats.kind_validator kind false <> None) is_string_format
  done;
  Check.equal_int "the number of kinds" ~expected:33 Formats.decimal;
  check "formatKindOf float" (Formats.kind_of "float" 0 = Formats.single) true;
  check "formatKindOf of no format" (Formats.kind_of "no-such-format" draft2020_12 = Formats.unknown) true;
  check "formatKindOf of the empty name" (Formats.kind_of "" draft2020_12 = Formats.unknown) true;
  check "formatKindOf is case sensitive" (Formats.kind_of "Date" draft2020_12 = Formats.unknown) true;
  check "validator of no format" (Formats.validator "no-such-format" <> None) false;
  check "validator of a numeric format" (Formats.validator "int32" <> None) false;
  check "validator of ipv4" (Formats.validator "ipv4" <> None) true;
  Check.equal_string "message of date-time" ~expected:"Expected an ISO8601 Offset DateTime string."
    (Formats.message Formats.date_time);
  check "message of a numeric format" (String.length (Formats.message Formats.int32) = 0) true

(* ------------------------------------------------------------------------------------------------------------------ *)
(* Punycode. The sources have no test of it apart from the host name cases of the suite. These are samples of RFC 3492
   section 7.1 and the round trip the host name check relies on. *)

let test_punycode () =
  let sample name label encoded =
    Check.equal_string ("punycode of " ^ name) ~expected:encoded (Formats.punycode_encode_string label);
    check ("punycode decodes " ^ name) (Formats.punycode_decode_string encoded = Some label) true
  in
  (* (A) Arabic (Egyptian). *)
  sample "Arabic"
    (u
       [
         0x644;
         0x64a;
         0x647;
         0x645;
         0x627;
         0x628;
         0x62a;
         0x643;
         0x644;
         0x645;
         0x648;
         0x634;
         0x639;
         0x631;
         0x628;
         0x64a;
         0x61f;
       ])
    "egbpdaj6bu4bxfgehfvwxn";
  (* (B) Chinese (simplified). *)
  sample "Chinese"
    (u [ 0x4ed6; 0x4eec; 0x4e3a; 0x4ec0; 0x4e48; 0x4e0d; 0x8bf4; 0x4e2d; 0x6587 ])
    "ihqwcrb4cv8a8dqg056pqjye";
  (* (L) Japanese, with basic code points before the delimiter. *)
  sample "Japanese with ASCII"
    ("3" ^ u [ 0x5e74 ] ^ "B" ^ u [ 0x7d44; 0x91d1; 0x516b; 0x5148; 0x751f ])
    "3B-ww4c5e180e575a65lsy2b";
  sample "German" ("b" ^ u [ 0xfc ] ^ "cher") "bcher-kva";
  sample "ASCII alone" "abc" "abc-";
  check "punycode refuses a character that is not a digit" (Formats.punycode_decode_string "a b" = None) true;
  check "punycode refuses a basic code point beyond ASCII"
    (Formats.punycode_decode_string (u [ 0xfc ] ^ "-kva") = None)
    true;
  check "punycode refuses a code point above U+10FFFF" (Formats.punycode_decode_string "999999999" = None) true

(* ------------------------------------------------------------------------------------------------------------------ *)
(* Allocation *)

(* An input of the allocation check. *)
type input = { kind : Formats.kind; buffer : Bytes.t; off : int; len : int; legacy : bool }

(* minor_words is the words the checks of one input allocate on the minor heap in [rounds] calls, with what the
   measurement itself allocates. Gc.minor_words gives a boxed float, so the two readings are taken outside the loop
   and the loop holds nothing but the calls. The caller subtracts the same measurement of the unknown format, which
   does no work, to leave what the check allocates. *)
let minor_words input rounds =
  let hits = ref 0 in
  let words_before = Gc.minor_words () in
  for _ = 1 to rounds do
    if Formats.check_string input.kind input.buffer input.off input.len input.legacy then incr hits
  done;
  let words_after = Gc.minor_words () in
  ignore (Sys.opaque_identity !hits);
  words_after -. words_before

(* test_allocations checks that a check allocates nothing, for valid and invalid text of every format but the ones
   that may allocate: regex, idn-hostname, idn-email, and a host name with a label that starts with "xn--". *)
let test_allocations () =
  let f =
    Formats.
      [
        (date, [ "2024-02-29"; "2023-02-29"; "2024-13-01"; "not a date"; "" ]);
        (time, [ "23:59:60Z"; "12:34:56.789+01:30"; "23:59:60+01:00"; "25:00:00Z"; "12:34:56"; "x" ]);
        ( date_time,
          [ "2024-02-29T12:34:56.5Z"; "2024-02-29t12:34:56-08:00"; "2024-02-30T12:34:56Z"; "2024-02-29 12:34:56Z"; "" ]
        );
        (duration, [ "P1Y2M3DT4H5M6S"; "P4W"; "PT1M"; "P1D"; "P1YT"; "P1S"; "PT"; "P"; "1Y" ]);
        ( uuid,
          [
            "2eb8aa08-aa98-11ea-b4aa-73b441d16380";
            "2EB8AA08-AA98-11EA-B4AA-73B441D16380";
            "2eb8aa08-aa98-11ea-b4aa-73b441d1638";
            "2eb8aa08xaa98-11ea-b4aa-73b441d16380";
          ] );
        (ipv4, [ "192.168.0.1"; "255.255.255.255"; "256.0.0.1"; "1.2.3"; "01.2.3.4"; "1.2.3.4.5"; "" ]);
        ( ipv6,
          [
            "::1";
            "1:2:3:4:5:6:7:8";
            "::ffff:192.168.0.1";
            "1:2:3:4:5:6:192.168.0.1";
            "1::2::3";
            "12345::";
            "1:2:3:4:5:6:7:8:9";
            "::ffff:192.168.0.256";
            "x";
          ] );
        ( hostname,
          [
            "www.example.com";
            "a-b.example";
            "-a.example";
            "ab--cd.example";
            "a..b";
            "";
            "a123456789012345678901234567890123456789012345678901234567890123.example";
          ] );
        ( email,
          [
            "joe.bloggs@example.com";
            "\"joe bloggs\"@example.com";
            "joe@[127.0.0.1]";
            "joe@[IPv6:::1]";
            "joe..bloggs@example.com";
            "joe@";
            "@example.com";
            "joe@[127.0.0.300]";
            "joe";
          ] );
        ( uri,
          [
            "http://user:pw@www.example.com:8080/a/b%20c?q=1&r=%41#frag";
            "urn:isbn:0451450523";
            "http://[::1]:80/";
            "http://[v1.a:b]/";
            "//example.com/a";
            "http://exa mple.com";
            "http://example.com/%zz";
            "/a/b";
            "";
          ] );
        (uri_reference, [ "/a/b?c#d"; "//example.com/a"; "a/b"; "#frag"; ""; "a:b:c"; "\\\\server\\share"; "a b" ]);
        ( iri,
          [
            "http://\xc6\x92\xc3\xb8\xc3\xbc.example/\xc3\xa9?\xee\x80\x80#\xc3\xa9";
            "http://example.com/a";
            "/\xc3\xa9";
            "http://example.com/\xee\x80\x80";
            "http://[2001:db8::7]/";
          ] );
        (iri_reference, [ "/\xc3\xa2\xcf\x80\xcf\x80"; "#\xc6\x92rag"; "\\\\WINDOWS\\file\xc3\xa9"; "a/\xc3\xa9?b" ]);
        ( uri_template,
          [
            "http://example.com/dictionary/{term:1}/{term}";
            "{+path,x}/here{?q*,lang:9999}{&%41.b}";
            "http://example.com/{term";
            "{}";
            "a{b:0}";
            "a b";
            "{a..b}";
          ] );
        (json_pointer, [ "/foo/bar~0/baz~1/%a"; ""; "/"; "/foo/bar~"; "/~2"; "a" ]);
        (relative_json_pointer, [ "1"; "0/foo/bar"; "2/0/baz/1/zip"; "0#"; "/foo/bar"; "01/a"; "0##"; "-1/a" ]);
        (unknown, [ "anything" ]);
        (int32, [ "anything" ]);
      ]
  in
  let inputs =
    List.concat_map
      (fun (kind, texts) ->
        List.concat_map
          (fun text ->
            let input legacy =
              { kind; buffer = padded text; off = String.length before; len = String.length text; legacy }
            in
            if kind = Formats.hostname then [ input false; input true ] else [ input false ])
          texts)
      f
  in
  let rounds = 200 in
  (* The unknown format does no work, so its measurement is what the measurement itself allocates. *)
  let nothing = { kind = Formats.unknown; buffer = padded "x"; off = String.length before; len = 1; legacy = false } in
  let overhead = minor_words nothing rounds in
  Check.check "the measurement of an allocation allocates the same each time" (minor_words nothing rounds = overhead);
  (* The measurement sees an allocation: an internationalized host name is one of the checks that may allocate, and
     it does. *)
  let host = "b\xc3\xbccher.example" in
  let allocating =
    {
      kind = Formats.idn_hostname;
      buffer = padded host;
      off = String.length before;
      len = String.length host;
      legacy = false;
    }
  in
  Check.check "the measurement sees the allocation of an internationalized host name"
    (minor_words allocating rounds -. overhead > 0.0);
  let valid = ref 0 in
  List.iter
    (fun input ->
      if Formats.check_string input.kind input.buffer input.off input.len input.legacy then incr valid;
      let words = minor_words input rounds -. overhead in
      Check.check
        (Printf.sprintf "%s of \"%s\" (legacy %b) allocates %.0f words in %d calls" (Formats.name input.kind)
           (show (Bytes.sub_string input.buffer input.off input.len))
           input.legacy words rounds)
        (words = 0.0))
    inputs;
  (* Both answers are measured: the inputs are neither all valid nor all invalid. *)
  Check.check "the allocation inputs hold valid text" (!valid > 30);
  Check.check "the allocation inputs hold invalid text" (List.length inputs - !valid > 30);
  (* The whole set in one measurement, as an evaluation would run it. *)
  let all = Array.of_list inputs in
  let hits = ref 0 in
  let words_before = Gc.minor_words () in
  for _ = 1 to rounds do
    for i = 0 to Array.length all - 1 do
      let input = all.(i) in
      if Formats.check_string input.kind input.buffer input.off input.len input.legacy then incr hits
    done
  done;
  let words_after = Gc.minor_words () in
  ignore (Sys.opaque_identity !hits);
  let words = words_after -. words_before -. overhead in
  Check.check (Printf.sprintf "%d checks allocate %.0f words" (rounds * Array.length all) words) (words = 0.0);
  Printf.printf "Allocation: %d inputs (%d valid), %d calls of each, 0 words expected of every one\n" (Array.length all)
    !valid rounds

(* ------------------------------------------------------------------------------------------------------------------ *)
(* Answers written out from the Go source *)

let run_vectors path =
  match read_file path with
  | None -> Check.fail ("cannot read " ^ path)
  | Some source ->
      let shown = ref 0 in
      List.iter
        (fun line ->
          match String.split_on_char '\t' line with
          | kind :: legacy :: hex :: want :: _ ->
              let kind = int_of_string kind in
              let legacy = legacy = "1" in
              let text = from_hex hex in
              let want = want = "1" in
              let got = check_kind kind text legacy in
              incr vectors_run;
              if got <> want then begin
                incr vectors_failed;
                incr shown;
                if !shown <= 200 then
                  Check.fail
                    (Printf.sprintf "vector %s legacy=%b \"%s\": got %b, want %b" (Formats.name kind) legacy (show text)
                       got want)
              end
          | _ -> ())
        (String.split_on_char '\n' source);
      Check.check
        (Printf.sprintf "%d of %d answers of the Go source differ" !vectors_failed !vectors_run)
        (!vectors_failed = 0)

(* ------------------------------------------------------------------------------------------------------------------ *)

let () =
  let suite_root =
    ref
      (match Sys.getenv_opt "JSON_SCHEMA_TEST_SUITE" with
      | Some root when root <> "" -> root
      | _ -> Corvus_json_schema_test_helpers.Suite.root ())
  in
  let suite_named = ref false in
  let vectors_path = ref "" in
  let rec arguments = function
    | "--vectors" :: path :: rest ->
        vectors_path := path;
        arguments rest
    | root :: rest ->
        suite_root := root;
        suite_named := true;
        arguments rest
    | [] -> ()
  in
  arguments (List.tl (Array.to_list Sys.argv));
  Check.section "suite";
  (* The published package built on its own does not hold the suite. The other checks still run. *)
  let suite_absent = (not !suite_named) && Corvus_json_schema_test_helpers.Suite.absent !suite_root in
  if suite_absent then Corvus_json_schema_test_helpers.Suite.skipped "formats (the format cases of the suite)"
  else run_suite !suite_root;
  Check.section "kinds";
  test_format_kinds ();
  Check.section "ipv4";
  test_ipv4_cases ();
  Check.section "content";
  test_content ();
  Check.section "unicode";
  test_unicode_formats_use_unicode_17 ();
  test_unicode_format_strings ();
  Check.section "punycode";
  test_punycode ();
  Check.section "allocations";
  test_allocations ();
  if !vectors_path <> "" then begin
    Check.section "vectors";
    run_vectors !vectors_path
  end;
  Check.section "";
  print_newline ();
  print_endline "JSON-Schema-Test-Suite format cases, by format (run, passed, skipped, failed):";
  List.iter
    (fun (name, tally) ->
      Printf.printf "  %s: %d run, %d passed, %d skipped, %d failed\n" name tally.run tally.passed tally.skipped
        tally.failed)
    !tallies;
  Printf.printf
    "JSON-Schema-Test-Suite: %d files, %d cases, %d run, %d passed, %d skipped (leap second), %d failed, %d not format \
     cases\n"
    !suite_files !suite_total !suite_run !suite_passed !suite_skipped !suite_failed !suite_not_format;
  if !vectors_path <> "" then
    Printf.printf "Answers of the Go source: %d run, %d passed, %d failed\n" !vectors_run
      (!vectors_run - !vectors_failed) !vectors_failed;
  if !suite_total = 0 && not suite_absent then
    Check.fail (Printf.sprintf "no JSON-Schema-Test-Suite cases ran (is the suite at %s?)" !suite_root);
  Check.finish "formats"
