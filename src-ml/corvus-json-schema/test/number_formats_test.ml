(* The numeric formats, for numbers as a document holds them. A port of TestNumbers of TestFormats.pas of the Object
   Pascal port, which tests checkNumber of formats.go. Pascal gives the check a representation and its bits. Here
   each number is parsed from its text, which is how a number reaches the check, so the infinities of the Pascal test
   have no case: no document holds one. *)

open Corvus_json_schema_test_support
module Document = Corvus_json_schema__Document
module Formats = Corvus_json_schema__Formats
module Number_formats = Corvus_json_schema__Number_formats

let check_number kind text =
  match Document.parse_string text with
  | Ok d -> Number_formats.check kind d (Document.root d)
  | Error e -> failwith (text ^ ": " ^ Document.error_to_string e)

let expect kind text want =
  Check.checkf
    (check_number kind text = want)
    "%s of %s: %b, want %b" (Formats.name kind) text (check_number kind text) want

let test_numbers () =
  Check.section "TestNumbers";
  let open Formats in
  expect byte "0" true;
  expect byte "255" true;
  expect byte "256" false;
  expect byte "-1" false;
  expect sbyte "-128" true;
  expect sbyte "-129" false;
  expect sbyte "127" true;
  expect sbyte "128" false;
  expect int16 "-32768" true;
  expect int16 "32768" false;
  expect uint16 "65535" true;
  expect uint16 "65536" false;
  expect int32 "-2147483648" true;
  expect int32 "2147483648" false;
  expect uint32 "4294967295" true;
  expect uint32 "4294967296" false;
  expect int64 "-9223372036854775808" true;
  expect int64 "9223372036854775807" true;
  expect uint64 "9223372036854775807" true;
  expect uint64 "-1" false;
  expect uint128 "-1" false;
  expect int128 "-9223372036854775808" true;
  (* An integer of 2^63 or more is not an int64. *)
  expect uint64 "18446744073709551615" true;
  expect int64 "9223372036854775808" false;
  expect uint32 "9223372036854775808" false;
  expect uint128 "18446744073709551615" true;
  (* 1.5, 2^63, 2^64, 65504 and 65505. *)
  expect int32 "1.5" false;
  expect double "1.5" true;
  expect int64 "9223372036854775808.0" true;
  expect uint64 "18446744073709551616" true;
  expect uint32 "18446744073709551616" false;
  expect half "65504.0" true;
  expect half "65505.0" false;
  (* The integers that an int64 holds and an OCaml int does not, which have a representation of their own here. *)
  expect int64 "4611686018427387904" true;
  expect int64 "-4611686018427387905" true;
  expect int32 "4611686018427387904" false;
  expect int32 "-4611686018427387905" false;
  expect uint64 "4611686018427387904" true;
  expect uint64 "-4611686018427387905" false;
  expect uint128 "-4611686018427387905" false;
  expect int128 "-4611686018427387905" true;
  expect uint64 "13835058055282163712" true;
  expect int64 "13835058055282163712" false;
  expect single "4611686018427387904" true;
  expect half "4611686018427387904" false;
  expect half "-4611686018427387905" false;
  expect decimal "18446744073709551615" true;
  expect double "1e308" true;
  expect single "1e39" false;
  expect decimal "1e29" false;
  expect int128 "1e38" true;
  expect int128 "1e39" false;
  expect uint128 "1e38" true;
  expect uint128 "-1e3" false;
  expect uint128 "1.5" false;
  expect byte "255.0" true;
  expect byte "2.55e2" true;
  expect byte "255.5" false

(* The same through a schema, with the format asserted, and a custom format, which is given the number's text. *)
let test_numbers_through_a_schema () =
  Check.section "numeric formats through a schema";
  let open Corvus_json_schema in
  let compile ?(formats = []) schema =
    match Validator.compile_string ~options:(options ~assert_format:true ~formats ()) schema with
    | Ok v -> v
    | Error e -> failwith (error_to_string e)
  in
  let v = compile {|{"format": "int32"}|} in
  List.iter
    (fun (instance, want) ->
      Check.checkf (Validator.is_valid_string v instance = want) "int32 of %s: want %b" instance want;
      let results = Results.create Results.Basic in
      Check.checkf
        (Validator.evaluate_string v ~results instance = Ok want)
        "int32 of %s, collecting: want %b" instance want)
    [
      ("12", true); ("2147483647", true); ("2147483648", false); ("1e30", false); ("1.5", false); ({|"a string"|}, true);
    ];
  let seen = ref [] in
  let v =
    compile
      ~formats:
        [
          ( "int32",
            fun text ->
              seen := text :: !seen;
              text = "1.50" );
        ]
      {|{"format": "int32"}|}
  in
  Check.check "a custom numeric format is asked" (Validator.is_valid_string v "1.50");
  Check.check "a custom numeric format decides" (not (Validator.is_valid_string v "12"));
  Check.check "a custom numeric format is given the text of the number" (!seen = [ "12"; "1.50" ])

(* A count keyword holds a non-negative integer of up to 64 bits in Go. Here one beyond an OCaml int is held as the
   greatest int, which decides the same, and its text is kept for the messages. *)
let test_counts_beyond_an_int () =
  Check.section "counts beyond an int";
  let open Corvus_json_schema in
  let messages schema instance =
    match Validator.compile_string schema with
    | Error e -> failwith (error_to_string e)
    | Ok v ->
        let results = Results.create Results.Verbose in
        let valid = Validator.evaluate_string v ~results instance in
        Check.checkf (Ok (Validator.is_valid_string v instance) = valid) "%s: fail-fast and collecting agree" schema;
        ( valid,
          List.filter_map
            (fun (r : Results.result) -> if r.evaluation_location <> "" then Some r.message else None)
            (Results.results results) )
  in
  List.iter
    (fun (schema, instance, valid, message) ->
      let got_valid, got = messages schema instance in
      Check.checkf (got_valid = Ok valid) "%s on %s: want %b" schema instance valid;
      Check.checkf (List.mem message got) "%s on %s: %s" schema instance (String.concat " | " got))
    [
      ( {|{"maxLength": 18446744073709551615}|},
        {|"a"|},
        true,
        "Expected the length of the value to be less than or equal to '18446744073709551615'" );
      ( {|{"minLength": 4611686018427387904}|},
        {|"a"|},
        false,
        "Expected the length of the value to be greater than or equal to '4611686018427387904'" );
      ( {|{"minLength": 4611686018427387903}|},
        {|"a"|},
        false,
        "Expected the length of the value to be greater than or equal to '4611686018427387903'" );
      ( {|{"minItems": 1e19}|},
        "[1]",
        false,
        "Expected the item count to be greater than or equal to '10000000000000000000'" );
      ({|{"maxItems": 1.0e2}|}, "[1]", true, "Expected the item count to be less than or equal to '100'");
      ( {|{"maxProperties": 9223372036854775808.0}|},
        "{}",
        true,
        "Expected the property count to be less than or equal to '9223372036854775808'" );
      (* The nearest double is 12345678901234567168, whose digits a printf of 17 significant digits does not keep. *)
      ( {|{"maxItems": 12345678901234567890.0}|},
        "[1]",
        true,
        "Expected the item count to be less than or equal to '12345678901234567168'" );
      ( {|{"minItems": 4611686018427387904.0}|},
        "[1]",
        false,
        "Expected the item count to be greater than or equal to '4611686018427387904'" );
      ( {|{"contains": true, "maxContains": 18446744073709551615, "minContains": 9223372036854775807}|},
        "[1]",
        false,
        "Expected the contains count to be greater than or equal to '9223372036854775807'" );
    ]

let () =
  test_numbers ();
  test_numbers_through_a_schema ();
  test_counts_beyond_an_int ();
  Check.finish "number_formats"
