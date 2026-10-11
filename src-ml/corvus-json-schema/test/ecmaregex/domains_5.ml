(* One compiled pattern matched from several domains at once, which run in parallel. It is the domain form of
   TestConcurrentUse of the Go tests, and it needs OCaml 5. The test with threads is in test_ecmaregex.ml. *)

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
  if !failures > 0 then exit 1;
  Printf.printf
    "ecmaregex domains: 2 patterns, %d domains each, with the memory of the pattern and with a scratch for each domain\n"
    domains
