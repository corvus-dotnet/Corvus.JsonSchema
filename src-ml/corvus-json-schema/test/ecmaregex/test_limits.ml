(* The tests of the limit on a match of the regular expression engine. They are match_limit_test.go of the ecmaregex
   package of the Go module. A schema and an instance may both come from outside, so neither a pattern nor a text may
   make a match run without end or hold memory without bound.

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
  | Error message -> failwith (Printf.sprintf "compile(%S): %s" pattern message)

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
   after it. *)
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
            else match Re.compile pattern with Ok re -> re | Error message -> failwith message
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
      ({|^(?:(a+)\1*)+$|}, repeat 60 "a" ^ "b", 0.001, Time);
    ]
    [@ocamlformat "disable"]

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
      ("a match with no limit is not abandoned", test_a_match_with_no_limit_is_not_abandoned);
      ("a match is abandoned at its memory limit", test_a_match_is_abandoned_at_its_memory_limit);
      ("a limit does not change an answer", test_a_limit_does_not_change_an_answer);
      ("a limited match allocates nothing", test_a_limited_match_allocates_nothing);
    ];
  if !failures <> 0 then begin
    Printf.printf "FAILED: %d failures\n" !failures;
    exit 1
  end;
  Printf.printf "ecmaregex limits: %d tests, 0 failures\n" !ran
