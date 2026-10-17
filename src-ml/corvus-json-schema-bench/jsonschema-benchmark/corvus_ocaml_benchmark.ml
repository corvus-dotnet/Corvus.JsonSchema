(* corvus-json-schema's implementation of the jsonschema-benchmark protocol
   (https://github.com/sourcemeta-research/jsonschema-benchmark):

     corvus_ocaml_benchmark <schema.json> <instances.jsonl>

   It parses every instance, compiles the schema, validates every instance once cold, warms up, and validates once
   warm. It prints one line, cold,warm,compile,parse in nanoseconds, and exits 1 if an instance is invalid.

   The benchmark defines warm as steady state. The warm-up follows a rule that is the same for every engine whatever
   its runtime: validation passes for a fixed time (warmup_time), and at least min_warmup_passes passes. The warm
   figure is the last of those passes. *)

open Corvus_json_schema

(* The monotonic clock in nanoseconds (clock_stubs.c). The OCaml standard library has no monotonic clock. *)
external now : unit -> (int[@untagged]) = "corvus_benchmark_now_byte" "corvus_benchmark_now" [@@noalloc]

let warmup_time = 2_000_000_000
let min_warmup_passes = 100
let read_file name = In_channel.with_open_bin name In_channel.input_all
let is_blank line = String.for_all (fun c -> c = ' ' || c = '\t' || c = '\r' || c = '\n') line

(* validate_all validates every document, and does not stop at one that is not valid. *)
let validate_all validator (documents : Document.t array) =
  let valid = ref true in
  for i = 0 to Array.length documents - 1 do
    if not (Validator.is_valid_document validator documents.(i)) then valid := false
  done;
  !valid

let stop message =
  prerr_endline message;
  exit 2

let () =
  if Array.length Sys.argv <> 3 then stop "Usage: corvus_ocaml_benchmark <schema> <instances>";
  let schema, contents =
    try (read_file Sys.argv.(1), read_file Sys.argv.(2)) with Sys_error message -> stop message
  in
  let texts =
    Array.of_list
      (List.filter_map
         (fun line -> if is_blank line then None else Some (Bytes.of_string line))
         (String.split_on_char '\n' contents))
  in

  let parse_start = now () in
  let documents =
    Array.map
      (fun text -> match Document.parse_bytes text with Ok d -> d | Error e -> stop (Document.error_to_string e))
      texts
  in
  let parse = now () - parse_start in

  (* The benchmark's schema-noformat.json has no format keywords, and the defaults leave format as an annotation. *)
  let compile_start = now () in
  let validator = match Validator.compile_string schema with Ok v -> v | Error e -> stop (error_to_string e) in
  let compile = now () - compile_start in

  let cold_start = now () in
  let valid = validate_all validator documents in
  let cold = now () - cold_start in
  if not valid then exit 1;

  (* The warm pass is the last pass of the warm-up loop, timed at the same call site as the passes before it, as in
     the harnesses of the runtimes that compile while they run. *)
  let deadline = now () + warmup_time in
  let warm = ref 0 and valid = ref valid and pass = ref 0 in
  while !pass < min_warmup_passes || now () < deadline do
    let start = now () in
    valid := validate_all validator documents && !valid;
    warm := now () - start;
    incr pass
  done;
  if not !valid then exit 1;

  Printf.printf "%d,%d,%d,%d\n" cold !warm compile parse
