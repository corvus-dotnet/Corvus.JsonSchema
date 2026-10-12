(* The tests of the limits of the regular expression engine: the limit on a match, and the limits on the size of one
   pattern. They are match_limit_test.go, size_limit_test.go and size_cases_test.go of the ecmaregex package of the Go
   module. A schema and an instance may both come from outside, so neither a pattern nor a text may make a match run
   without end or hold memory without bound, and no pattern may take long or much memory to read and to compile.

   No test here depends on a quiet machine. A match that must reach its limit runs a pattern that would take longer
   than the age of the universe, and the bounds on the time it is given are many times what it needs.

   The tests of the Go package that are about the regexp package of Go are not here: this engine has no such back
   end. *)

module Re = Corvus_json_schema_ecmaregex

let failures = ref 0

let fail fmt =
  Printf.ksprintf
    (fun message ->
      incr failures;
      if !failures <= 300 then prerr_endline ("FAIL: " ^ message))
    fmt

let log fmt = Printf.ksprintf (fun message -> print_endline ("  " ^ message)) fmt

(* The time a test allows for something that takes milliseconds, in seconds. *)
let generous = 20.0
let repeat n s = String.concat "" (List.init n (fun _ -> s))

let compiled pattern =
  match Re.compile_backtrack pattern with
  | Ok re -> re
  | Error e -> failwith (Printf.sprintf "compile(%S): %s" pattern (Re.error_message e))

(* What a match came to. *)
type outcome = Matched of bool | Time | Memory

let outcome_name = function Matched m -> string_of_bool m | Time -> "Match_time" | Memory -> "Match_memory"

(* Matches with a scratch that has the limit given, in seconds. *)
let match_limit scratch re text limit =
  Re.set_limit scratch limit;
  match Re.is_match_sub_with scratch re text 0 (String.length text) with
  | matched -> Matched matched
  | exception Re.Match_time -> Time
  | exception Re.Match_memory -> Memory

(* Patterns with a text that no backtracking matcher answers, and that no rewrite of this engine makes easy: each reads
   a group back inside a repetition, so the ways through the text cannot be told apart. *)
let forever =
  [
    ({|^(?:(a+)\1*)+$|}, repeat 60 "a" ^ "b"); ({|(a*)*\1b|}, repeat 60 "a");
    ({|^(?:(\w+)\s?\1?)+$|}, repeat 30 "ab " ^ "!"); ({|(?=(?:(a+)\1*)+b)a|}, repeat 60 "a");
  ]
  [@ocamlformat "disable"]

(* Runs each pattern that never answers with a limit, and expects Match_time no sooner than the limit and not long
   after it. The limit is counted on the time that passes, which is the clock of a scratch that has been given no
   other, so both bounds are asked of that time. The clock starts at the first check of a match, so a match is never
   abandoned before its limit has passed. *)
let test_a_match_is_abandoned_at_its_limit () =
  let scratch = Re.create_scratch () in
  List.iter
    (fun limit ->
      List.iter
        (fun (pattern, text) ->
          let re = compiled pattern in
          let start = Unix.gettimeofday () in
          let outcome = match_limit scratch re text limit in
          let took = Unix.gettimeofday () -. start in
          if outcome <> Time then
            fail "%S with a limit of %gs: %s, want Match_time" pattern limit (outcome_name outcome);
          if took < limit || took > limit +. generous then
            fail "%S with a limit of %gs was abandoned after %gs" pattern limit took;
          log "%S with a limit of %gs: abandoned after %.4fs" pattern limit took)
        forever)
    [ 0.02; 0.3 ]

(* Checks the clock of a scratch that has been given no other is the time that passes, and not the processor time of
   the process. The process sleeps, which takes time and no processor time, and the clock must have moved by the time
   the sleep took: the two readings of it are outside the two of the time of day. A fifth of a second is asked of a
   sleep of three tenths, and no most, so a busy machine changes nothing. *)
let test_the_default_clock_is_the_time_that_passes () =
  let before = Re.default_clock () in
  let start = Unix.gettimeofday () in
  Unix.sleepf 0.3;
  let slept = Unix.gettimeofday () -. start in
  let moved = Re.default_clock () -. before in
  if slept < 0.2 then fail "the sleep took %gs" slept
  else if moved < 0.2 then fail "the default clock moved %gs over a sleep of %gs" moved slept;
  log "the default clock moved %.4fs over a sleep of %.4fs" moved slept;
  (* A match that never answers is abandoned for the time that passed while the process slept as well as computed.
     The clock given here is the default one with a sleep in each reading, so the process computes for a small part of
     the time: four readings of a tenth of a second pass a limit of a quarter of a second. A clock that counted the
     processor time would take thousands, so after ten readings the clock given says the limit has passed. *)
  let scratch = Re.create_scratch () in
  let pattern, text = List.hd forever in
  let reads = ref 0 in
  Re.set_clock scratch
    (Some
       (fun () ->
         incr reads;
         Unix.sleepf 0.1;
         if !reads > 10 then infinity else Re.default_clock ()));
  let outcome = match_limit scratch (compiled pattern) text 0.25 in
  if outcome <> Time || !reads < 2 || !reads > 6 then
    fail "with a sleep in each reading of the default clock: %s after %d readings, want Match_time after 2 to 6"
      (outcome_name outcome) !reads;
  log "with a sleep of 0.1s in each reading: abandoned after %d readings for a limit of 0.25s" !reads

(* Checks the clock a caller gives a scratch. With the time of day the limit is of the time that passes. With a clock
   that the test moves, the match is abandoned at the first check after the limit, which is a count of checks that no
   machine changes: the first check starts the clock, and the match goes on while the clock is at or before the
   deadline. A clock that is set back counts the limit again from the new time, and a clock set forward ends the match
   at once. *)
let test_the_clock_of_a_match_can_be_given () =
  let scratch = Re.create_scratch () in
  let pattern, text = List.hd forever in
  let re = compiled pattern in
  Re.set_clock scratch (Some Unix.gettimeofday);
  let start = Unix.gettimeofday () in
  let outcome = match_limit scratch re text 0.1 in
  let took = Unix.gettimeofday () -. start in
  if outcome <> Time || took < 0.1 || took > 0.1 +. generous then
    fail "with the time of day and a limit of 0.1s: %s after %gs" (outcome_name outcome) took;
  (* A clock that is a second later at each read, and the readings a match made of it. *)
  let reads = ref 0 and now = ref 1000. in
  let expect what clock limit want =
    reads := 0;
    Re.set_clock scratch (Some clock);
    let outcome = match_limit scratch re text limit in
    if outcome <> Time || !reads <> want then
      fail "%s: %s after %d readings of the clock, want Match_time after %d" what (outcome_name outcome) !reads want
  in
  let tick () =
    incr reads;
    now := !now +. 1.;
    !now
  in
  (* The first reading sets the deadline five seconds on, the next five are within it and the seventh is past it. *)
  expect "a clock of a second a reading, with a limit of 5s" tick 5. 7;
  expect "the same with a limit of half a second" tick 0.5 2;
  (* Set back an hour at its fourth reading: the limit is counted again from there, so six more readings follow. *)
  expect "a clock set back at its fourth reading"
    (fun () ->
      if !reads = 3 then now := !now -. 3600.;
      tick ())
    5. 10;
  (* Set forward an hour at its third reading: the match is abandoned there. *)
  expect "a clock set forward at its third reading"
    (fun () ->
      if !reads = 2 then now := !now +. 3600.;
      tick ())
    5. 3;
  (* With no clock of the caller's again, the scratch is back on the time that passes. *)
  Re.set_clock scratch None;
  reads := 0;
  let outcome = match_limit scratch re text 0.02 in
  if outcome <> Time || !reads <> 0 then
    fail "with the clock taken away: %s after %d readings of it" (outcome_name outcome) !reads

(* Checks that a limit of zero is no limit: a match that takes far more steps than a small limit allows runs to its
   answer, as it does through the entry points that take no scratch. *)
let test_a_match_with_no_limit_is_not_abandoned () =
  let scratch = Re.create_scratch () in
  let re = compiled {|^(?:(a+)\1*)+$|} in
  (* Some hundred thousand steps: far beyond a limit of a nanosecond, and a few milliseconds of work. *)
  let text = repeat 14 "a" ^ "b" in
  let expect what limit want =
    let outcome = match_limit scratch re text limit in
    if outcome <> want then fail "%s: %s, want %s" what (outcome_name outcome) (outcome_name want)
  in
  expect "with a limit of 1ns" 1e-9 Time;
  expect "with no limit" 0. (Matched false);
  expect "with a limit below zero" (-1.) (Matched false);
  expect "with a limit that is not a number" Float.nan (Matched false);
  if Re.limit scratch <> 0. then fail "a limit that is not a number is kept as %g" (Re.limit scratch);
  if Re.is_match_string re text then fail "is_match_string: true, want false";
  let outcome = match_limit scratch re (repeat 14 "a") generous in
  if outcome <> Matched true then fail "a text that matches, with a limit: %s, want true" (outcome_name outcome);
  if Re.limit scratch <> generous then fail "the limit of the scratch is %g, want %g" (Re.limit scratch) generous

(* Runs patterns that push on the backtracking stack without end. With a limit on the match the stack is capped, and
   the match ends in Match_memory long before the clock says anything. Without the cap the first of these ran out of
   memory at several gigabytes. *)
let test_a_match_is_abandoned_at_its_memory_limit () =
  let scratch = Re.create_scratch () in
  List.iter
    (fun (pattern, text) ->
      let re = compiled pattern in
      let before = Gc.allocated_bytes () in
      let start = Unix.gettimeofday () in
      let outcome = match_limit scratch re text 3600. in
      let took = Unix.gettimeofday () -. start in
      let grew = Gc.allocated_bytes () -. before in
      if outcome <> Memory then fail "%S: %s, want Match_memory" pattern (outcome_name outcome);
      if took > generous then fail "%S was abandoned after %gs" pattern took;
      (* Every stack the match grew through, which is twice the largest: four words an entry. *)
      if grew > 16. *. 32. *. float_of_int Re.max_stack then
        fail "%S allocated %.0f MB before it was abandoned" pattern (grew /. 1048576.);
      log "%S: abandoned after %.4fs and %.0f MB allocated" pattern took (grew /. 1048576.))
    [
      ({|(?:a?){2147483647}|}, ""); ({|(?:a?){100000000}|}, repeat 100 "a");
      ({|(?:(?:(?:a?){1000}){1000}){1000}|}, repeat 20 "a"); ({|(?:(a)|b|){2147483647}\1|}, "");
    ]
    [@ocamlformat "disable"]

(* Runs patterns of both back ends with a limit they do not reach and expects the answer they give without one. *)
let test_a_limit_does_not_change_an_answer () =
  let scratch = Re.create_scratch () in
  let long = repeat 50000 "ab" in
  List.iter
    (fun (pattern, text, want) ->
      List.iter
        (fun force ->
          let re =
            if force then compiled pattern
            else match Re.compile pattern with Ok re -> re | Error e -> failwith (Re.error_message e)
          in
          List.iter
            (fun limit ->
              let outcome = match_limit scratch re text limit in
              if outcome <> Matched want then
                fail "%S on %S with a limit of %gs: %s, want %b" pattern
                  (String.sub text 0 (min 20 (String.length text)))
                  limit (outcome_name outcome) want)
            [ 0.; 60. ])
        [ false; true ])
    [
      ({|^[a-z]+$|}, "abc", true); ({|^[a-z]+$|}, "abc1", false); ({|^\p{L}+$|}, "caf\u{e9}", true);
      ({|^\p{L}+$|}, "caf\u{e9} ", false); ({|^(?=.*\d)\w+$|}, "abc1", true); ({|^(?=.*\d)\w+$|}, "abcd", false);
      ({|^(a+)\1$|}, "aaaa", true); ({|^(a+)\1$|}, "aaa", false); ({|(?<=\$)\d+|}, "cost $42", true);
      ({|^(?=.)(?:a|b)*$|}, long, true); ({|^(?=.)(?:a|b)*c$|}, long, false); ({|^[ab]*$|}, long, true);
      ({|^[ab]*c$|}, long, false); ({|^[abé]*$|}, long ^ "\u{e9}", true);
      ({|^[abé]*c$|}, long ^ "\u{e9}", false);
    ]
    [@ocamlformat "disable"]

(* The words the run allocates on the heap of OCaml, small and large, beyond what reading the counters allocates. *)
let allocated_words run =
  let measure run =
    let minor = Gc.minor_words () in
    let major = (Gc.quick_stat ()).Gc.major_words -. (Gc.quick_stat ()).Gc.promoted_words in
    run ();
    let minor = Gc.minor_words () -. minor in
    let major = (Gc.quick_stat ()).Gc.major_words -. (Gc.quick_stat ()).Gc.promoted_words -. major in
    minor +. major
  in
  let nothing = measure (fun () -> ()) in
  measure run -. nothing

(* Checks the steady state with a limit on the match, on patterns that reach the first check of the clock and on one
   that is abandoned there. The limit and the deadline are in an array of floats of the scratch, the clock is read
   into it, and the exceptions of a match that is abandoned carry nothing. *)
let test_a_limited_match_allocates_nothing () =
  let long = repeat 20000 "ab" in
  let sink = ref 0 in
  let seen = allocated_words (fun () -> sink := !sink + !(Sys.opaque_identity (ref 1))) in
  if seen < 2. then fail "the measure of allocations saw %.0f words where two were allocated" seen;
  List.iter
    (fun (pattern, text, limit, want) ->
      let re = compiled pattern in
      let scratch = Re.create_scratch () in
      Re.set_limit scratch limit;
      let length = String.length text in
      let bytes = Bytes.of_string text in
      let run () =
        match Re.is_match_sub_with scratch re text 0 length with
        | matched -> if matched then incr sink
        | exception Re.Match_time -> sink := !sink + 2
      in
      let run_bytes () =
        match Re.is_match_with scratch re bytes 0 length with
        | matched -> if matched then incr sink
        | exception Re.Match_time -> sink := !sink + 2
      in
      (* The first matches grow the scratch to what the text needs. *)
      for _ = 1 to 3 do
        let outcome = match_limit scratch re text limit in
        if outcome <> want then fail "%S: %s, want %s" pattern (outcome_name outcome) (outcome_name want)
      done;
      let words =
        allocated_words (fun () ->
            for _ = 1 to 20 do
              run ();
              run_bytes ()
            done)
      in
      if words <> 0. then fail "%S with a limit of %gs: %.0f words allocated in 40 matches, want 0" pattern limit words)
    [
      ({|^(?=.)(?:a|b)*c$|}, long, 60., Matched false); ({|^(?:(a|b)\1?)+$|}, String.sub long 0 4000, 60., Matched true);
      ({|^[abé]{0,60000}c$|}, "\u{e9}" ^ long, 60., Matched false);
      (* Matches that take the memory of failed places into use, which stays in the scratch. *)
      ({|^(?=.)(?:a|aa)+$|}, String.make 1000 'a' ^ "b", 60., Matched false); ({|(?=.)(?:a|b)*c|}, long, 0., Matched false);
      ({|(a*)*b|}, "\u{e9}" ^ String.make 1000 'a', 60., Matched false);
      ({|^(?:(a+)\1*)+$|}, repeat 60 "a" ^ "b", 0.001, Time);
    ]
    [@ocamlformat "disable"]

(* Checks the search for the literal text a match starts with. The search looks for it at every position of the text,
   outside the steps the matcher counts, so it looks for the first bytes alone: a literal of 16,000 characters that
   nearly matches at every position of a text of 100,000 took 2.5 seconds, which no limit ended. The matcher compares
   the rest of the literal when it runs, and counts that. *)
let test_a_long_literal_is_searched_for_by_its_first_bytes () =
  List.iter
    (fun (pattern, want) ->
      let found = Re.Internal.prefix (compiled pattern) in
      if found <> want then fail "the prefix of %S is %S, want %S" (String.sub pattern 0 (min 30 (String.length pattern))) found want)
    [
      ("abc", "abc"); ("abcdefghijklmnop", "abcdefghijklmnop"); ("abcdefghijklmnopq", "abcdefghijklmnop");
      (repeat 16000 "a" ^ "b", repeat 16 "a"); ("(abcdefghijklmnopqrstuvwxyz)", "abcdefghijklmnop"); ("^abc", "");
      ("[ab]c", "");
    ]
    [@ocamlformat "disable"];
  let scratch = Re.create_scratch () in
  let text = String.make 100000 'a' in
  List.iter
    (fun (pattern, text, want) ->
      let start = Unix.gettimeofday () in
      let outcome = match_limit scratch (compiled pattern) text 0.2 in
      let took = Unix.gettimeofday () -. start in
      (* The match is abandoned at its limit or answers. It does not run on outside the limit. *)
      if outcome <> Time && outcome <> Matched want then
        fail "%S: %s, want %b or Match_time" (String.sub pattern 0 20) (outcome_name outcome) want;
      if took > 0.2 +. generous then fail "%S took %gs with a limit of 0.2s" (String.sub pattern 0 20) took;
      log "a literal of %d characters: %s after %.3fs" (String.length pattern) (outcome_name outcome) took)
    [
      (repeat 16000 "a" ^ "b", text, false); (repeat 16000 "a" ^ "[bc]", text, false);
      (repeat 16000 "a" ^ "b", String.sub text 0 20000 ^ "b", true);
    ]
    [@ocamlformat "disable"]

(* The limits of size. *)

let node_size = 32
let range_size = 8

(* Patterns of each kind that costs something to read or to compile, each a little under max_size. *)
let at_the_limit () =
  let fit each = (Re.max_size - 4096) / each in
  let l_ranges = match Re.Internal.property_pairs "L" with Some pairs -> Array.length pairs / 2 | None -> 0 in
  let nest opening body closing depth = repeat depth opening ^ body ^ repeat depth closing in
  (* Groups of one name: each is charged for the groups before it, so 400 of them cost most of the limit. *)
  let named = String.concat "|" (List.init 400 (fun k -> Printf.sprintf "(?<n>%d)" k)) in
  let alternatives = repeat (fit (3 * node_size)) "a|" in
  [
    ("literal", repeat (fit (2 * node_size)) "a");
    ("alternatives", String.sub alternatives 0 (String.length alternatives - 1));
    ("groups", repeat (fit (4 * node_size)) "(a)");
    ("groups read back", repeat 4000 "(a)" ^ repeat 4000 {|\1|} ^ repeat 300 {|\4000|});
    ("nested groups", nest "(" (repeat (fit (2 * node_size) - (2 * Re.max_nesting)) "a") ")" (Re.max_nesting - 1));
    ("nested counted loops", nest "(?:" (repeat 2000 "a?b") "){2,3}" (Re.max_nesting - 1));
    ("nested lookbehinds", nest "(?<=" (repeat 2000 "a") ")" (Re.max_nesting - 1));
    ("counted loops", repeat (fit (7 * node_size)) "(?:a|){2}");
    ("lazy loops", repeat (fit (7 * node_size)) "(?:a|b)*?");
    ("properties", repeat (fit (2 * node_size)) {|\p{L}|});
    ("negated properties", repeat (fit ((2 * node_size) + (range_size * (l_ranges + 1)))) {|\P{L}|});
    ("classes of properties", repeat (fit ((2 * node_size) + (range_size * (l_ranges + 200)))) {|[\p{L}\p{N}]|});
    ("folded negated classes", "(?i:" ^ repeat (fit ((2 * node_size) + 32)) {|[^a]|} ^ ")");
    ("folded dots", "(?i:" ^ repeat (fit (2 * node_size)) "." ^ ")");
    ("folded wide classes", "(?i:" ^ repeat (fit 3200) {|[\u0000-\u{10FFFF}]|} ^ ")");
    ("folded wide negations", "(?i:" ^ repeat (fit 3300) {|[^Ā-\u{10FFFF}]|} ^ ")");
    ("folded properties", "(?i:" ^ repeat (fit 8600) {|\p{L}|} ^ ")");
    ("folded literal", "(?i:" ^ repeat (fit (2 * node_size)) "k" ^ ")");
    ("one large class", "[" ^ repeat (fit (2 * range_size)) {|ĀĂ|} ^ "]");
    ("groups of one name", named);
    ("named groups, nested", nest "(?:x|" (repeat 40 "(?<a>b)|(?<c>d)") ")" 600);
    ("counted, for regexp", repeat (fit (4 * node_size)) "(?:a?){1000}");
    ("quantified everything", repeat (fit (2 * node_size)) "a{2,5}?");
    ("long escapes", repeat (fit (2 * node_size)) {|\u{0000000000000000000000000000000000000000000041}|});
  ]

(* Compiles the patterns of at_the_limit and matches each once. The limit of size is what bounds the time and the
   memory of compiling, so the largest pattern of every kind must take little of both. The bounds asserted are
   generous. The figures measured are logged. *)
let test_a_pattern_at_the_limit_of_size_is_cheap () =
  let scratch = Re.create_scratch () in
  List.iter
    (fun (name, pattern) ->
      Gc.full_major ();
      let live = (Gc.stat ()).Gc.live_words in
      let before = Gc.allocated_bytes () in
      let start = Unix.gettimeofday () in
      match Re.compile pattern with
      | Error e -> fail "%s (%d bytes): %s" name (String.length pattern) (Re.error_message e)
      | Ok re ->
          let took = Unix.gettimeofday () -. start in
          (* The match may reach a limit, which is an outcome. What matters here is that the first match is cheap. *)
          let start = Unix.gettimeofday () in
          let outcome = match_limit scratch re "abc" 60. in
          let matched = Unix.gettimeofday () -. start in
          let grew = Gc.allocated_bytes () -. before in
          Gc.full_major ();
          let retained = (Gc.stat ()).Gc.live_words - live in
          log
            "%-24s %7d bytes, %s: compile %.1f ms, first match %.1f ms (%s), %.0f MB allocated by both, %d KB retained"
            name (String.length pattern)
            (Re.engine_name (Re.engine re))
            (took *. 1000.) (matched *. 1000.) (outcome_name outcome) (grew /. 1048576.)
            (retained * 8 / 1024);
          if took > generous || matched > generous then
            fail "%s took %gs to compile and %gs for its first match" name took matched;
          if grew > 512. *. 1048576. || retained * 8 > 64 * 1048576 then
            fail "%s allocated %.0f MB and retained %d MB" name (grew /. 1048576.) (retained * 8 / 1048576);
          ignore (Sys.opaque_identity re))
    (at_the_limit ())

(* Checks each limit: the pattern at the limit compiles and the pattern over it is refused with Too_large, which names
   the limit, in both grammars and by check, and a pattern that is over a limit and also not valid is refused for its
   size. *)
let test_a_pattern_over_a_limit_of_size_is_too_large () =
  let nest depth = repeat depth "(?:" ^ "a" ^ repeat depth ")" in
  let terms = Re.max_size / (2 * node_size) in
  List.iter
    (fun (name, at, over, limit, value) ->
      (match Re.compile at with
      | Ok _ -> ()
      | Error e -> fail "%s: the pattern at the limit (%d bytes): %s" name (String.length at) (Re.error_message e));
      (match Re.check at with
      | Ok _ -> ()
      | Error e -> fail "%s: check of the pattern at the limit: %s" name (Re.error_message e));
      let expect what = function
        | Error (Re.Too_large (l, v)) when l = limit && v = value -> ()
        | Error e ->
            fail "%s: %s of the pattern over the limit (%d bytes): %s, want Too_large for %s of %d" name what
              (String.length over) (Re.error_message e) limit value
        | Ok _ ->
            fail "%s: %s of the pattern over the limit (%d bytes) gives no error, want Too_large for %s of %d" name what
              (String.length over) limit value
      in
      expect "compile" (Result.map ignore (Re.compile over));
      expect "check" (Result.map ignore (Re.check over));
      if Re.valid over then fail "%s: valid of the pattern over the limit: true" name)
    [
      ( "length",
        {|\u{|} ^ repeat (Re.max_pattern_length - 6) "0" ^ "41}",
        {|\u{|} ^ repeat (Re.max_pattern_length - 5) "0" ^ "41}",
        "length",
        Re.max_pattern_length );
      ("nesting", nest (Re.max_nesting - 1), nest Re.max_nesting, "nesting", Re.max_nesting);
      ("size of terms", repeat terms "a", repeat (terms + 1) "a", "size", Re.max_size);
      ("size of classes", "[" ^ repeat 60000 {|ĀĂ|} ^ "]", "[" ^ repeat 70000 {|ĀĂ|} ^ "]", "size", Re.max_size);
      ("size without the u flag", repeat terms {|\-|}, repeat (terms + 1) {|\-|}, "size", Re.max_size);
      ( "nesting without the u flag",
        {|\-|} ^ nest (Re.max_nesting - 1),
        {|\-|} ^ nest Re.max_nesting,
        "nesting",
        Re.max_nesting );
      ("nesting, and not valid", nest (Re.max_nesting - 1) ^ "x", nest Re.max_nesting ^ "*+)", "nesting", Re.max_nesting);
      ("size, and not valid", repeat terms "a", repeat (terms + 1) "a" ^ "(", "size", Re.max_size);
    ];
  (* The messages are those of the Go package. *)
  List.iter
    (fun (error, want) ->
      if Re.error_message error <> want then fail "the message is %S, want %S" (Re.error_message error) want)
    [
      (Re.Too_large ("length", 1048576), "the pattern is longer than 1048576 bytes");
      (Re.Too_large ("nesting", 1000), "the groups of the pattern nest more than 1000 deep");
      (Re.Too_large ("size", 1048576), "the pattern has a size of more than 1048576 bytes when it is read");
    ];
  (* A pattern that is not valid and under every limit is not too large. *)
  List.iter
    (fun pattern ->
      match Re.compile pattern with
      | Error (Re.Invalid _) -> ()
      | Error (Re.Too_large _) | Ok _ ->
          fail "compile(%S): want the error of a pattern that is not valid"
            (String.sub pattern 0 (min 20 (String.length pattern))))
    [ "("; "a**"; {|(?<n>a)\k<m>|}; "[b-a]"; nest (Re.max_nesting - 1) ^ ")" ]

(* Checks the size the parser counts for each pattern of size_cases.json, under each grammar, against the size the Go
   package counts, which is in the file. The size of a pattern decides whether it is refused as too large, so the
   engines must count the same figure for every pattern, or a schema one of them compiles would be refused by another.
   The file is the one of the Go package (testdata/size_cases.json there) and is not changed here. *)
let test_the_size_of_a_pattern () =
  let data = In_channel.with_open_bin "size_cases.json" In_channel.input_all in
  let cases =
    match Json.parse data with Json.Array cases -> cases | _ -> failwith "size_cases.json is not an array"
  in
  let valid = ref 0 and legacy = ref 0 and invalid = ref 0 in
  let number name c =
    match Json.member name c with Some (Json.Number n) -> int_of_float n | _ -> failwith ("no number " ^ name)
  in
  List.iter
    (fun c ->
      let p = Json.text "p" c in
      let want_u = number "u" c and want_l = number "l" c in
      let u = Re.Internal.size p true and l = Re.Internal.size p false in
      if u <> want_u || l <> want_l then
        fail "%S: size %d with the u flag and %d without, the Go package counts %d and %d" p u l want_u want_l;
      if want_u >= 0 then incr valid else if want_l >= 0 then incr legacy else incr invalid)
    cases;
  log "%d patterns: %d valid with the u flag, %d only without it, %d not valid" (List.length cases) !valid !legacy
    !invalid;
  if List.length cases <> 122 || !valid < 90 || !legacy < 10 || !invalid < 5 then
    fail "the cases are not the ones this test is about"

let () =
  let ran = ref 0 in
  List.iter
    (fun (name, test) ->
      Printf.printf "%s\n%!" name;
      incr ran;
      let before = !failures in
      (try test () with e -> fail "%s raised %s" name (Printexc.to_string e));
      if !failures > before then Printf.printf "  FAILED: %d failures\n%!" (!failures - before))
    [
      ("a match is abandoned at its limit", test_a_match_is_abandoned_at_its_limit);
      ("the default clock is the time that passes", test_the_default_clock_is_the_time_that_passes);
      ("the clock of a match can be given", test_the_clock_of_a_match_can_be_given);
      ("a match with no limit is not abandoned", test_a_match_with_no_limit_is_not_abandoned);
      ("a match is abandoned at its memory limit", test_a_match_is_abandoned_at_its_memory_limit);
      ("a limit does not change an answer", test_a_limit_does_not_change_an_answer);
      ("a limited match allocates nothing", test_a_limited_match_allocates_nothing);
      ("a long literal is searched for by its first bytes", test_a_long_literal_is_searched_for_by_its_first_bytes);
      ("a pattern at the limit of size is cheap", test_a_pattern_at_the_limit_of_size_is_cheap);
      ("a pattern over a limit of size is too large", test_a_pattern_over_a_limit_of_size_is_too_large);
      ("the size of a pattern", test_the_size_of_a_pattern);
    ];
  if !failures <> 0 then begin
    Printf.printf "FAILED: %d failures\n" !failures;
    exit 1
  end;
  Printf.printf "ecmaregex limits: %d tests, 0 failures\n" !ran
