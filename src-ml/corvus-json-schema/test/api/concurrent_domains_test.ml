(* TestValidatorsAreSafeForConcurrentUse of api_test.go of the Go module, with domains where Go has goroutines. Several
   domains, which run in parallel, validate with one shared validator at once: first with the working memory of the
   validator, which one evaluation at a time holds while the others make their own, then with a scratch for each
   domain. Domains need OCaml 5. The same test with threads is in api_test.ml. *)

open Corvus_json_schema
module Check = Corvus_json_schema_test_support.Check

let domains = 8

(* Go validates 200 times in each goroutine. Domains start more slowly than goroutines, so each validates more often
   here, to be sure that the evaluations overlap. *)
let rounds = 2000

(* run is the work of one domain: its own valid and invalid instance, validated in turn. It is the number of wrong
   answers (it stops at the first, as Go does). *)
let run v own_scratch g () =
  let scratch = if own_scratch then Some (Scratch.create ()) else None in
  let items = String.concat "," (List.init 100 (fun i -> string_of_int ((g * 1000) + i))) in
  let valid = "[" ^ items ^ "]" in
  let invalid = "[" ^ items ^ ",-1]" in
  let wrong = ref 0 in
  (try
     for _ = 1 to rounds do
       if (not (Validator.is_valid_string ?scratch v valid)) || Validator.is_valid_string ?scratch v invalid then begin
         incr wrong;
         raise Exit
       end
     done
   with Exit -> ());
  !wrong

let () =
  Check.section "validators_are_safe_for_concurrent_use";
  (match
     Validator.compile_string {|{ "type": "array", "items": { "type": "integer", "minimum": 0 }, "uniqueItems": true }|}
   with
  | Error e -> Check.fail ("compile: " ^ error_to_string e)
  | Ok v ->
      List.iter
        (fun own_scratch ->
          let running = List.init domains (fun g -> Domain.spawn (run v own_scratch g)) in
          let wrong = List.fold_left (fun sum d -> sum + Domain.join d) 0 running in
          Check.equal_int
            (if own_scratch then "domains that gave a wrong answer, with a scratch for each domain"
             else "domains that gave a wrong answer, with the working memory of the validator")
            ~expected:0 wrong)
        [ false; true ]);
  Check.finish "concurrent_domains"
