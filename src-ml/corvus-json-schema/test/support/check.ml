let passes = ref 0
let failures = ref 0
let current = ref ""

let section name = current := name

let fail what =
  incr failures;
  if !current = "" then Printf.printf "FAIL: %s\n%!" what else Printf.printf "FAIL: %s: %s\n%!" !current what

let check what ok = if ok then incr passes else fail what
let checkf ok fmt = Printf.ksprintf (fun what -> check what ok) fmt

let equal_string what ~expected actual =
  if String.equal expected actual then incr passes
  else fail (Printf.sprintf "%s: expected %S, got %S" what expected actual)

let equal_int what ~expected actual =
  if expected = actual then incr passes else fail (Printf.sprintf "%s: expected %d, got %d" what expected actual)

let equal_bool what ~expected actual =
  if expected = actual then incr passes else fail (Printf.sprintf "%s: expected %b, got %b" what expected actual)

let passed () = !passes
let failed () = !failures

let finish name =
  Printf.printf "%s: %d passed, %d failed\n%!" name !passes !failures;
  exit (if !failures = 0 then 0 else 1)
