(* The limit on the time and the memory of one match of a pattern or patternProperties regular expression, and the
   limits on the size of one pattern. A port of pattern_match_limit_test.go, pattern_size_test.go and
   pattern_compile_cost_test.go of the Go module. An instance may come from outside, and so may a schema, so no text
   and no pattern may make a validation run without end or hold memory without bound, and no pattern may take long or
   much memory to read and to compile.

   No test here depends on a quiet machine. A match that must reach its limit runs a pattern that no backtracking
   matcher answers in the life of the machine, and the bounds on the time it is given are many times what it needs. *)

open Corvus_json_schema
open Corvus_json_schema_test_support
module Pattern = Corvus_json_schema__Pattern

(* What a test allows for something that takes milliseconds, in seconds. *)
let generous_time = 20.0
let repeat n s = String.concat "" (List.init n (fun _ -> s))

(* A pattern, as it is written in JSON, and a string it never answers for: a group read back inside a repetition, on a
   text that fails at its end. *)
let never_answers = {|^(?:(a+)\\1*)+$|}
let never_answers_source = {|^(?:(a+)\1*)+$|}
let never_answered = repeat 60 "a" ^ "b"

let compile ?options schema =
  match Validator.compile_string ?options schema with
  | Ok v -> v
  | Error e -> failwith (String.sub schema 0 (min 80 (String.length schema)) ^ ": " ^ error_to_string e)

let parse instance =
  match Document.parse_string instance with Ok d -> d | Error e -> failwith (Document.error_to_string e)

(* The pattern in each keyword that matches a regular expression, with an instance that makes it match the text. The
   plans run pattern and patternProperties in several ways, so each is here in the shapes that take a different one. *)
let limit_schemas pattern text =
  [
    ("pattern", Printf.sprintf {|{"pattern": "%s"}|} pattern, Printf.sprintf {|"%s"|} text);
    ( "pattern in a property",
      Printf.sprintf {|{"type": "object", "properties": {"s": {"type": "string", "pattern": "%s"}}}|} pattern,
      Printf.sprintf {|{"s": "%s"}|} text );
    ("pattern in items", Printf.sprintf {|{"items": {"pattern": "%s"}}|} pattern, Printf.sprintf {|["%s"]|} text);
    ("pattern under not", Printf.sprintf {|{"not": {"pattern": "%s"}}|} pattern, Printf.sprintf {|"%s"|} text);
    ( "pattern under anyOf",
      Printf.sprintf {|{"anyOf": [{"pattern": "%s"}, {"type": "string"}]}|} pattern,
      Printf.sprintf {|"%s"|} text );
    ( "patternProperties",
      Printf.sprintf {|{"patternProperties": {"%s": {"type": "integer"}}}|} pattern,
      Printf.sprintf {|{"%s": 1}|} text );
    ( "patternProperties with properties",
      Printf.sprintf {|{"properties": {"id": true}, "patternProperties": {"%s": true}, "additionalProperties": false}|}
        pattern,
      Printf.sprintf {|{"id": 1, "%s": 1}|} text );
    ( "patternProperties and unevaluatedProperties",
      Printf.sprintf {|{"patternProperties": {"%s": true}, "unevaluatedProperties": false}|} pattern,
      Printf.sprintf {|{"%s": 1}|} text );
    ( "patternProperties under a condition",
      Printf.sprintf {|{"if": {"patternProperties": {"%s": {"const": 1}}}, "then": {"minProperties": 1}}|} pattern,
      Printf.sprintf {|{"%s": 1}|} text );
    ("propertyNames", Printf.sprintf {|{"propertyNames": {"pattern": "%s"}}|} pattern, Printf.sprintf {|{"%s": 1}|} text);
  ]

let contains text part =
  let n = String.length part in
  let rec from i = i + n <= String.length text && (String.sub text i n = part || from (i + 1)) in
  from 0

(* Checks a result is the error of a match that reached its limit, for the pattern and the limit. *)
let expect_match_limit what result limit =
  match result with
  | Error (Pattern_match_limit { pattern; limit = found; memory } as e) ->
      Check.checkf
        (pattern = never_answers_source && found = limit && not memory)
        "%s: the error holds the pattern %S, the limit %g and memory %b" what pattern found memory;
      let message = error_to_string e in
      Check.checkf
        (contains message (Printf.sprintf "it took longer than the limit of %gs." limit) && contains message pattern)
        "%s: the message %S does not name the pattern and the limit" what message
  | Ok ok -> Check.fail (Printf.sprintf "%s: the result is %b, want the error Pattern_match_limit" what ok)
  | Error e -> Check.fail (Printf.sprintf "%s: the error is %s, want Pattern_match_limit" what (error_to_string e))

(* Checks every entry point, for pattern and patternProperties, failing fast and collecting: a match that reaches its
   limit ends the validation in the error Pattern_match_limit, no sooner than the limit and not long after it. It is
   never an answer. The functions that return a bool alone raise Pattern_match_limit_reached. *)
let test_a_match_that_reaches_its_limit_is_an_error () =
  Check.section "TestAMatchThatReachesItsLimitIsAnError";
  let limit = 0.05 in
  let options = options ~pattern_match_limit:limit () in
  List.iter
    (fun (name, schema, instance) ->
      let v = compile ~options schema in
      let document = parse instance in
      let bytes = Bytes.of_string instance in
      let scratch = Scratch.create () in
      let timed what run =
        let start = Unix.gettimeofday () in
        let result = run () in
        let took = Unix.gettimeofday () -. start in
        expect_match_limit (name ^ ", " ^ what) result limit;
        Check.checkf
          (took >= limit && took <= limit +. generous_time)
          "%s, %s: abandoned after %gs with a limit of %gs" name what took limit
      in
      timed "validate_document" (fun () -> Validator.validate_document v document);
      timed "validate_bytes" (fun () -> Validator.validate_bytes v bytes);
      timed "validate_string" (fun () -> Validator.validate_string v instance);
      timed "validate_string with a scratch" (fun () -> Validator.validate_string ~scratch v instance);
      List.iter
        (fun level ->
          timed
            ("evaluate at level " ^ Results.level_to_string level)
            (fun () -> Validator.evaluate v ~results:(Results.create level) document))
        [ Results.Basic; Results.Detailed; Results.Verbose ];
      timed "evaluate_bytes" (fun () -> Validator.evaluate_bytes v ~results:(Results.create Results.Basic) bytes);
      timed "evaluate_string" (fun () -> Validator.evaluate_string v ~results:(Results.create Results.Basic) instance);
      (* The functions that return a bool alone raise, and the result is the same error. *)
      let raised what run =
        timed what (fun () ->
            match run () with
            | (answer : bool) -> Ok answer
            | exception Pattern_match_limit_reached { pattern; limit; memory } ->
                Error (Pattern_match_limit { pattern; limit; memory }))
      in
      raised "is_valid_document" (fun () -> Validator.is_valid_document v document);
      raised "is_valid_bytes" (fun () -> Validator.is_valid_bytes v bytes);
      raised "is_valid_string" (fun () -> Validator.is_valid_string v instance);
      raised "is_valid_string with a scratch" (fun () -> Validator.is_valid_string ~scratch v instance);
      (* The validator and the scratch are given back: the next validation runs, and answers. *)
      let answer = Validator.validate_string v "1" in
      let again =
        match Validator.is_valid_string ~scratch v "1" with ok -> Ok ok | exception _ -> Error Depth_exceeded
      in
      Check.checkf
        (Result.is_ok answer && again = answer)
        "%s: a validation after one that was abandoned does not answer" name)
    (limit_schemas never_answers never_answered)

(* Checks the limit is on when no option names it, and is a second. *)
let test_the_default_match_limit_is_one_second () =
  Check.section "TestTheDefaultMatchLimitIsOneSecond";
  Check.checkf
    (default_pattern_match_limit = 1.0
    && default_options.pattern_match_limit = 1.0
    && (options ()).pattern_match_limit = 1.0)
    "the default limit is %g" default_pattern_match_limit;
  let v = compile (Printf.sprintf {|{"pattern": "%s"}|} never_answers) in
  let start = Unix.gettimeofday () in
  let result = Validator.validate_string v (Printf.sprintf {|"%s"|} never_answered) in
  let took = Unix.gettimeofday () -. start in
  expect_match_limit "the default limit" result 1.0;
  Check.checkf (took >= 1.0 && took <= 1.0 +. generous_time) "abandoned after %gs with the default limit" took

(* Checks the limit is on one match and not on the validation: an array of strings that each take a part of the limit
   is validated, however long the whole takes. *)
let test_the_match_limit_is_for_each_match () =
  Check.section "TestTheMatchLimitIsForEachMatch";
  (* Some hundred thousand steps for each string, which is a few milliseconds. *)
  let item = Printf.sprintf {|"%sb"|} (repeat 14 "a") in
  let instance = "[" ^ String.concat "," (List.init 40 (fun _ -> item)) ^ "]" in
  let v =
    compile
      ~options:(options ~pattern_match_limit:generous_time ())
      (Printf.sprintf {|{"items": {"not": {"pattern": "%s"}}}|} never_answers)
  in
  Check.check "40 strings that each take a part of the limit are valid" (Validator.validate_string v instance = Ok true)

(* Checks that no_pattern_match_limit, zero, or less restores a match with no limit: a match that takes far more steps
   than a small limit allows runs to its answer. *)
let test_the_match_limit_can_be_turned_off () =
  Check.section "TestTheMatchLimitCanBeTurnedOff";
  let schema = Printf.sprintf {|{"pattern": "%s"}|} never_answers in
  (* Some hundred thousand steps: far beyond a limit of a microsecond, and a few milliseconds of work. *)
  let instance = Printf.sprintf {|"%sb"|} (repeat 14 "a") in
  let small = 1e-6 in
  expect_match_limit "a limit of a microsecond"
    (Validator.validate_string (compile ~options:(options ~pattern_match_limit:small ()) schema) instance)
    small;
  List.iter
    (fun off ->
      let v = compile ~options:(options ~pattern_match_limit:off ()) schema in
      Check.checkf
        (Validator.validate_string v instance = Ok false && not (Validator.is_valid_string v instance))
        "with the limit %g, a string that does not match is not answered false" off;
      Check.checkf
        (Validator.validate_string v {|"aaaa"|} = Ok true)
        "with the limit %g, a string that matches is not answered true" off)
    [ no_pattern_match_limit; 0.; -1.; Float.nan; Float.neg_infinity ];
  Check.check "no_pattern_match_limit is zero" (no_pattern_match_limit = 0.)

(* Runs a pattern that pushes on the backtracking stack without end. With a limit on the match the stack is capped,
   and the error says the memory was the limit. The time limit here is long, so only the memory can end the match. *)
let test_a_match_that_reaches_its_memory_limit_is_an_error () =
  Check.section "TestAMatchThatReachesItsMemoryLimitIsAnError";
  let v = compile ~options:(options ~pattern_match_limit:3600. ()) {|{"pattern": "(?:a?){2147483647}"}|} in
  let start = Unix.gettimeofday () in
  let result = Validator.validate_string v {|""|} in
  let took = Unix.gettimeofday () -. start in
  (match result with
  | Error (Pattern_match_limit { pattern; limit; memory } as e) ->
      Check.checkf
        (pattern = "(?:a?){2147483647}" && limit = 3600. && memory)
        "the error holds the pattern %S, the limit %g and memory %b" pattern limit memory;
      Check.checkf
        (contains (error_to_string e) "it needed more than the limit of 1,048,576 backtracking entries.")
        "the message %S does not name the limit" (error_to_string e)
  | Ok ok -> Check.fail (Printf.sprintf "the result is %b, want Pattern_match_limit for memory" ok)
  | Error e -> Check.fail (Printf.sprintf "the error is %s, want Pattern_match_limit for memory" (error_to_string e)));
  Check.checkf (took <= generous_time) "abandoned after %gs" took;
  match Validator.is_valid_string v {|""|} with
  | answer -> Check.fail (Printf.sprintf "is_valid_string answers %b" answer)
  | exception Pattern_match_limit_reached { memory; _ } -> Check.check "is_valid_string raises for memory" memory

(* Checks the matches made when a schema is compiled. The plans match each pattern of patternProperties against the
   property names the schema declares, and that match has the limit too: compiling returns the error. *)
let test_a_match_while_compiling_has_the_limit () =
  Check.section "TestAMatchWhileCompilingHasTheLimit";
  let limit = 0.05 in
  let options = options ~pattern_match_limit:limit () in
  let compiled_one = ref false in
  List.iter
    (fun schema ->
      let start = Unix.gettimeofday () in
      match Validator.compile_string ~options schema with
      | Ok v ->
          (* The schema compiled with no match made, so the match is made when an instance has the property. *)
          expect_match_limit ("validating " ^ schema)
            (Validator.validate_string v (Printf.sprintf {|{"%s": 1}|} never_answered))
            limit
      | Error e ->
          let took = Unix.gettimeofday () -. start in
          compiled_one := true;
          expect_match_limit ("compiling " ^ schema) (Error e) limit;
          Check.checkf (took <= limit +. generous_time) "compiling was abandoned after %gs" took)
    [
      Printf.sprintf {|{"properties": {"%s": true}, "patternProperties": {"%s": {"type": "integer"}}}|} never_answered
        never_answers;
      Printf.sprintf {|{"properties": {"%s": true}, "patternProperties": {"%s": true}, "additionalProperties": false}|}
        never_answered never_answers;
      Printf.sprintf
        {|{"properties": {"%s": true}, "allOf": [{"patternProperties": {"%s": true}}], "unevaluatedProperties": false}|}
        never_answered never_answers;
    ];
  Check.check "one schema at least makes the match when it is compiled" !compiled_one

(* Checks patterns of every matcher with the default limit, a long one and none: the answers are the same. *)
let test_an_ordinary_pattern_is_not_affected_by_the_limit () =
  Check.section "TestAnOrdinaryPatternIsNotAffectedByTheLimit";
  List.iter
    (fun (pattern, text, want) ->
      List.iter
        (fun options ->
          let schema = Printf.sprintf {|{"pattern": "%s"}|} pattern in
          let instance = Printf.sprintf {|"%s"|} text in
          let v = compile ~options schema in
          let fast = Validator.validate_string v instance in
          let collected = Validator.evaluate_string v ~results:(Results.create Results.Basic) instance in
          let plain = match Validator.is_valid_string v instance with ok -> Ok ok | exception _ -> Error Depth_exceeded in
          Check.checkf
            (fast = Ok want && collected = Ok want && plain = Ok want)
            "%s on %S with the limit %g: want %b" pattern
            (String.sub text 0 (min 20 (String.length text)))
            options.pattern_match_limit want)
        [ default_options; options ~pattern_match_limit:10. (); options ~pattern_match_limit:no_pattern_match_limit () ])
    [
      ({|^[a-z]+$|}, "abc", true); ({|^[a-z]+$|}, "abc1", false); ({|^x-|}, "x-a", true);
      ({|^\\p{L}+$|}, "caf\u{e9}", true); ({|^\\p{L}+$|}, "caf\u{e9} ", false); ({|^(?=.*\\d)\\w+$|}, "abc1", true);
      ({|^(?=.*\\d)\\w+$|}, "abcd", false); ({|^(a+)\\1$|}, "aaaa", true); ({|^(a+)\\1$|}, "aaa", false);
      ({|(?<=\\$)\\d+|}, "cost $42", true); ({|^(?:[a-z]+\\.)+[a-z]+$|}, "a.b.c", true);
      ({|^[abé]*c$|}, repeat 20000 "ab" ^ "\u{e9}", false);
    ]
    [@ocamlformat "disable"]

(* Checks the matchers that are not the engine's and so have no limit. An alternative with no anchor is looked for at
   every position of the text, at a cost of its length for each, so a pattern whose alternatives cost more than 256
   between them has no such matcher and runs on the engine, whose matches have the limit. A sequence of 50,000
   characters and one more, looked for in a text of 100,000, took 4 to 10 seconds that way. *)
let test_a_search_with_no_limit_is_narrow () =
  Check.section "TestASearchWithNoLimitIsNarrow";
  let kind source = match Pattern.compile source with Ok p -> Pattern.kind_name p | Error _ -> "not compiled" in
  List.iter
    (fun (source, want) ->
      Check.equal_string
        (Printf.sprintf "the matcher of %S" (String.sub source 0 (min 40 (String.length source))))
        ~expected:want (kind source))
    [
      ("[a-z]{3}[0-9]|x", "alternatives"); ("[a-z]{254}[0-9]|x", "alternatives"); ("[a-z]{255}[0-9]|x", "engine");
      ("[a-z]{50000}[0-9]|x", "engine"); ("^[a-z]{50000}[0-9]|x", "alternatives"); ("[a-z]{50000}[0-9]$|^x", "alternatives");
      ("^x|" ^ repeat 256 "a", "alternatives"); ("^x|" ^ repeat 257 "a", "engine"); ("^x|^" ^ repeat 8000 "a", "alternatives");
      ("^x|" ^ repeat 8000 "a" ^ "$", "alternatives"); (repeat 100 "a" ^ "|" ^ repeat 100 "b" ^ "|" ^ repeat 56 "c", "alternatives");
      (repeat 100 "a" ^ "|" ^ repeat 100 "b" ^ "|" ^ repeat 57 "c", "engine");
    ]
    [@ocamlformat "disable"];
  (* The answers are the same whichever matcher runs. *)
  let text = String.make 100000 'a' in
  List.iter
    (fun (pattern, instance, want) ->
      let v = compile (Printf.sprintf {|{"pattern": "%s"}|} pattern) in
      let start = Unix.gettimeofday () in
      let result = Validator.validate_string v (Printf.sprintf {|"%s"|} instance) in
      let took = Unix.gettimeofday () -. start in
      Check.checkf
        (match result with Ok ok -> ok = want | Error (Pattern_match_limit _) -> true | Error _ -> false)
        "%S: want %b or the limit of a match" (String.sub pattern 0 (min 20 (String.length pattern))) want;
      Check.checkf (took <= 1.0 +. generous_time) "%S took %gs" (String.sub pattern 0 (min 20 (String.length pattern))) took)
    [
      ("[a-z]{50000}[0-9]|x", text, false); ("[a-z]{50000}[0-9]|x", text ^ "1", true);
      ("^x|" ^ repeat 8000 "a" ^ "b", text, false); ("^x|" ^ repeat 8000 "a" ^ "b", text ^ "b", true);
    ]
    [@ocamlformat "disable"]

(* The limits on the size of one pattern. *)

(* Checks each limit in both keywords: the error is Pattern_too_large, which names the keyword, the limit and its
   value, and is neither a Compile_error nor the error of a match. *)
let test_a_pattern_over_a_limit_of_size_is_refused () =
  Check.section "TestAPatternOverALimitOfSizeIsRefused";
  let nest depth = repeat depth "(?:" ^ "a" ^ repeat depth ")" in
  let size_message = "the pattern has a size of more than 1048576 bytes when it is read" in
  List.iter
    (fun (name, at, over, limit, value, message) ->
      List.iter
        (fun keyword ->
          let schema pattern =
            if keyword = "pattern" then Printf.sprintf {|{"pattern": "%s"}|} pattern
            else Printf.sprintf {|{"patternProperties": {"%s": true}}|} pattern
          in
          let start = Unix.gettimeofday () in
          (match Validator.compile_string (schema at) with
          | Ok _ -> ()
          | Error e ->
              Check.fail (Printf.sprintf "%s in %s: the pattern at the limit: %s" name keyword (error_to_string e)));
          let result = Validator.compile_string (schema over) in
          let took = Unix.gettimeofday () -. start in
          Check.checkf (took <= generous_time) "%s in %s: compiling took %gs" name keyword took;
          match result with
          | Error (Pattern_too_large { pattern; keyword = found_keyword; limit = found_limit; value = found } as e) ->
              Check.checkf
                (found_keyword = keyword && found_limit = limit && found = value)
                "%s in %s: the error holds the keyword %S, the limit %S and the value %d" name keyword found_keyword
                found_limit found;
              let text = error_to_string e in
              let want =
                Printf.sprintf "' (%d bytes) in %s is too large to read as an ECMA-262 regular expression: %s."
                  (String.length pattern) keyword message
              in
              Check.checkf
                (String.starts_with ~prefix:"The regular expression '" text
                && String.ends_with ~suffix:want text
                && String.length text <= 400)
                "%s in %s: the message is %S" name keyword
                (String.sub text 0 (min 300 (String.length text)));
              Check.checkf
                (not (contains (String.lowercase_ascii text) "invalid"))
                "%s in %s: the message says the pattern is not valid" name keyword
          | Ok _ -> Check.fail (Printf.sprintf "%s in %s: the pattern over the limit compiled" name keyword)
          | Error e ->
              let text = error_to_string e in
              Check.fail
                (Printf.sprintf "%s in %s: the error is %s, want Pattern_too_large" name keyword
                   (String.sub text 0 (min 200 (String.length text)))))
        [ "pattern"; "patternProperties" ])
    [
      ( "length",
        {|\\u{|} ^ repeat ((1 lsl 20) - 6) "0" ^ "41}",
        {|\\u{|} ^ repeat ((1 lsl 20) - 5) "0" ^ "41}",
        "length",
        1 lsl 20,
        "the pattern is longer than 1048576 bytes" );
      ("nesting", nest 999, nest 1000, "nesting", 1000, "the groups of the pattern nest more than 1000 deep");
      ("size", repeat 16384 "a", repeat 16385 "a", "size", 1 lsl 20, size_message);
      ( "size of a class",
        "[" ^ repeat 60000 {|\\u0100\\u0102|} ^ "]",
        "[" ^ repeat 70000 {|\\u0100\\u0102|} ^ "]",
        "size",
        1 lsl 20,
        size_message );
      ( "nesting, and not valid",
        nest 999,
        nest 1000 ^ "*+)",
        "nesting",
        1000,
        "the groups of the pattern nest more than 1000 deep" );
    ]

(* Checks the two errors stay apart: a pattern under every limit that is not valid is a Compile_error. *)
let test_a_pattern_that_is_not_valid_is_not_too_large () =
  Check.section "TestAPatternThatIsNotValidIsNotTooLarge";
  List.iter
    (fun pattern ->
      match Validator.compile_string (Printf.sprintf {|{"pattern": "%s"}|} pattern) with
      | Error (Compile_error message) ->
          Check.checkf
            (String.starts_with ~prefix:"Invalid regular expression '" message)
            "%S: the message is %S"
            (String.sub pattern 0 (min 20 (String.length pattern)))
            message
      | Ok _ | Error _ ->
          Check.fail
            (Printf.sprintf "%S: want a Compile_error for a pattern that is not valid"
               (String.sub pattern 0 (min 20 (String.length pattern)))))
    [ "("; "a**"; "[b-a]"; {|(?<n>a)\\k<m>|}; repeat 999 "(?:" ^ "a" ^ repeat 1000 ")" ]

(* Compiles literal patterns of growing length through every matcher a literal can take, and counts the bytes
   allocated, which no load on the machine changes. A pattern of 100,000 characters took half a second when the
   alternatives of a pattern were built a character at a time, each step copying what was built so far. A long pattern
   must allocate no more than a few times its share of what a short one does. *)
let test_a_long_pattern_compiles_in_memory_proportional_to_its_length () =
  Check.section "TestALongPatternCompilesInMemoryProportionalToItsLength";
  let round = ref 0 in
  let compile_bytes n shape =
    incr round;
    let pattern = shape (repeat (n - 8) "a" ^ Printf.sprintf "%08d" !round) in
    let schema = Printf.sprintf {|{"pattern": "%s"}|} pattern in
    let before = Gc.allocated_bytes () in
    (match Validator.compile_string schema with
    | Ok _ -> ()
    | Error e -> failwith (Printf.sprintf "a pattern of %d characters: %s" n (error_to_string e)));
    Gc.allocated_bytes () -. before
  in
  List.iter
    (fun (name, shape) ->
      let short = compile_bytes 1000 shape and long = compile_bytes 16000 shape in
      Printf.printf "  %s: 1,000 characters allocate %.0f KB, 16,000 allocate %.0f KB\n" name (short /. 1024.)
        (long /. 1024.);
      (* Sixteen times the length. Quadratic growth is 256 times. *)
      Check.checkf
        (long <= 64. *. short)
        "%s: 16,000 characters allocate %.0f KB and 1,000 allocate %.0f KB" name (long /. 1024.) (short /. 1024.))
    [
      ("a literal", fun s -> s);
      ("anchored", fun s -> "^" ^ s ^ "$");
      ( "alternatives",
        fun s ->
          let n = String.length s in
          "^(?:" ^ String.sub s 0 (n - 200) ^ "|x" ^ String.sub s (n - 100) 100 ^ ")$" );
      ( "with an escape",
        fun s ->
          let n = String.length s in
          String.sub s 0 (n / 2) ^ {|\\.|} ^ String.sub s ((n / 2) + 2) (n - (n / 2) - 2) );
      ("in a group", fun s -> "(" ^ String.sub s 0 (String.length s - 4) ^ ")");
    ]

(* Checks the regex format, which reads a string of the instance as a pattern: a string over a limit of size is not
   read to its end and is not a valid regular expression. *)
let test_the_regex_format_refuses_a_pattern_over_a_limit () =
  Check.section "TestTheRegexFormatRefusesAPatternOverALimit";
  let v = compile ~options:(options ~assert_format:true ()) {|{"format": "regex"}|} in
  List.iter
    (fun (text, want) ->
      let start = Unix.gettimeofday () in
      let result = Validator.validate_string v (Printf.sprintf {|"%s"|} text) in
      let took = Unix.gettimeofday () -. start in
      Check.checkf (result = Ok want) "a string of %d bytes: want %b" (String.length text) want;
      Check.checkf (took <= generous_time) "a string of %d bytes took %gs" (String.length text) took)
    [
      ("^[a-z]+$", true); ("(", false); (repeat 16384 "a", true); (repeat 16385 "a", false);
      (repeat 999 "(?:" ^ "a" ^ repeat 999 ")", true); (repeat 1000 "(?:" ^ "a" ^ repeat 1000 ")", false);
    ]
    [@ocamlformat "disable"]

let () =
  test_a_match_that_reaches_its_limit_is_an_error ();
  test_the_default_match_limit_is_one_second ();
  test_the_match_limit_is_for_each_match ();
  test_the_match_limit_can_be_turned_off ();
  test_a_match_that_reaches_its_memory_limit_is_an_error ();
  test_a_match_while_compiling_has_the_limit ();
  test_an_ordinary_pattern_is_not_affected_by_the_limit ();
  test_a_search_with_no_limit_is_narrow ();
  test_a_pattern_over_a_limit_of_size_is_refused ();
  test_a_pattern_that_is_not_valid_is_not_too_large ();
  test_a_long_pattern_compiles_in_memory_proportional_to_its_length ();
  test_the_regex_format_refuses_a_pattern_over_a_limit ();
  Check.finish "pattern limits"
