(* One compiled pattern matched from several domains at once, which run in parallel. It is the domain form of
   TestConcurrentUse of the Go tests, and it needs OCaml 5. The test with threads is in test_ecmaregex.ml. Then the
   limit of a match while other domains compute. *)

module Re = Corvus_json_schema_ecmaregex

let () =
  let failures = ref 0 in
  let domains = 8 in
  List.iter
    (fun p ->
      match Re.compile p with
      | Error error ->
          let message = Re.error_message error in
          prerr_endline ("FAIL: " ^ message);
          incr failures
      | Ok re ->
          List.iter
            (fun own_scratch ->
              let run g () =
                let scratch = Re.create_scratch () in
                let yes = Bytes.of_string (String.concat "" (List.init (50 + g) (fun _ -> "aa1b2"))) in
                let no = Bytes.cat yes (Bytes.of_string "!") in
                let is_match text =
                  if own_scratch then Re.is_match_with scratch re text 0 (Bytes.length text)
                  else Re.is_match re text 0 (Bytes.length text)
                in
                let wrong = ref false in
                for _ = 1 to 20000 do
                  if (not (is_match yes)) || is_match no then wrong := true
                done;
                !wrong
              in
              let running = List.init domains (fun g -> Domain.spawn (run g)) in
              if List.exists Fun.id (List.map Domain.join running) then begin
                Printf.eprintf "FAIL: %S gave a wrong answer under concurrent use (%s)\n" p
                  (if own_scratch then "a scratch for each domain" else "the memory of the pattern");
                incr failures
              end)
            [ false; true ])
    [ {|^(?:[a-z]+\d)+$|}; {|^(?:([a-z])\1?\d)+(?<=\d)$|} ];
  (* The limit of a match is of the time that passes, whatever the other domains compute. The processor time of the
     process runs as many times faster as there are domains computing, so a limit counted on it would end the match
     here in a part of its time on a machine with more than one processor. The least is asked of the time that passed,
     and the most is many times what the match needs. *)
  let limit = 0.5 and computing = 4 in
  let stop = Atomic.make false in
  let others =
    List.init computing (fun _ ->
        Domain.spawn (fun () ->
            let n = ref 0 in
            while not (Atomic.get stop) do
              incr n
            done;
            !n))
  in
  let took, outcome =
    match Re.compile_backtrack {|^(?:(a+)\1*)+$|} with
    | Error error -> (0., "the error " ^ Re.error_message error)
    | Ok re -> (
        let scratch = Re.create_scratch () in
        let text = String.make 60 'a' ^ "b" in
        Re.set_limit scratch limit;
        let start = Unix.gettimeofday () in
        match Re.is_match_sub_with scratch re text 0 (String.length text) with
        | matched -> (Unix.gettimeofday () -. start, string_of_bool matched)
        | exception Re.Match_time -> (Unix.gettimeofday () -. start, "Match_time")
        | exception Re.Match_memory -> (Unix.gettimeofday () -. start, "Match_memory"))
  in
  Atomic.set stop true;
  List.iter (fun d -> ignore (Domain.join d)) others;
  if outcome <> "Match_time" || took < limit || took > limit +. 20. then begin
    Printf.eprintf "FAIL: with %d other domains computing and a limit of %gs: %s after %gs\n" computing limit outcome
      took;
    incr failures
  end;
  Printf.printf "  with %d other domains computing and a limit of %gs: %s after %.4fs\n" computing limit outcome took;
  if !failures > 0 then exit 1;
  Printf.printf
    "ecmaregex domains: 2 patterns, %d domains each, with the memory of the pattern and with a scratch for each domain\n"
    domains
