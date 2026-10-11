(* The limit on the time and the memory of one match of a pattern or patternProperties regular expression, and the
   limits on the size of one pattern. A port of pattern_match_limit_test.go, pattern_size_test.go and
   pattern_compile_cost_test.go of the Go module. An instance may come from outside, and so may a schema, so no text
   and no pattern may make a validation run without end or hold memory without bound, and no pattern may take long or
   much memory to read and to compile.

   No test here depends on a quiet machine. A match that must reach its limit runs a pattern that no backtracking
   matcher answers in the life of the machine, and the bounds on the time it is given are many times what it needs. *)

open Corvus_json_schema
open Corvus_json_schema_test_support

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

let () =
  test_a_match_that_reaches_its_limit_is_an_error ();
  test_the_default_match_limit_is_one_second ();
  test_the_match_limit_is_for_each_match ();
  test_the_match_limit_can_be_turned_off ();
  test_a_match_that_reaches_its_memory_limit_is_an_error ();
  test_a_match_while_compiling_has_the_limit ();
  test_an_ordinary_pattern_is_not_affected_by_the_limit ();
  Check.finish "pattern limits"
