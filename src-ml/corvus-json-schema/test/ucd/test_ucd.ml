(* The tests of ucd_test.go of the Go module, as TestUcd.pas of the Object Pascal port has them.

   TestAgreesWithTheToolchainOfTheSameVersion of the Go source compares the tables with the unicode package of the Go
   toolchain. OCaml has no Unicode properties of its own to compare with, so that test has no port, as in Pascal. The
   generator checks the tables against their source, and the oracle of the regular expression tests checks them
   against V8. *)

module Ucd = Corvus_json_schema_ucd

let failures = ref 0
let checks = ref 0

let fail fmt =
  Printf.ksprintf
    (fun message ->
      incr failures;
      if !failures <= 50 then prerr_endline ("FAIL: " ^ message))
    fmt

exception Stop
(** Raised to leave a test at its first failure, where the Go test calls Fatalf. *)

let fatal fmt =
  Printf.ksprintf
    (fun message ->
      incr failures;
      prerr_endline ("FAIL: " ^ message);
      raise Stop)
    fmt

let get = function Some table -> table | None -> failwith "no table"

(* Returns every table the package can name, by a label. *)
let every_table () =
  let tables = ref [] in
  let add label table = tables := (label, table) :: !tables in
  List.iter
    (fun name -> add ("gc=" ^ name) (Ucd.category name))
    [
      "Lu"; "Ll"; "Lt"; "Lm"; "Lo"; "Mn"; "Mc"; "Me"; "Nd"; "Nl"; "No"; "Pc"; "Pd"; "Ps"; "Pe"; "Pi"; "Pf"; "Po"; "Sm";
      "Sc"; "Sk"; "So"; "Zs"; "Zl"; "Zp"; "Cc"; "Cf"; "Cs"; "Co"; "Cn"; "L"; "LC"; "M"; "N"; "P"; "S"; "Z"; "C";
    ]
  [@ocamlformat "disable"];
  Array.iter (fun name -> add name (Ucd.binary name)) Ucd.binary_names;
  add "Assigned" (Ucd.binary "Assigned");
  Array.iter
    (Array.iter (fun name ->
         add ("sc=" ^ name) (Ucd.script name);
         add ("scx=" ^ name) (Ucd.script_extensions name)))
    Ucd.script_names;
  List.rev !tables

(* Checks the invariants contains and ranges rely on. *)
let test_tables_are_well_formed () =
  let tables = every_table () in
  List.iter
    (fun (label, table) ->
      incr checks;
      match table with
      | None -> fail "%s has no table" label
      | Some table ->
          let r16 = Ucd.bounds16 table and r32 = Ucd.bounds32 table in
          if Array.length r16 mod 2 <> 0 || Array.length r32 mod 2 <> 0 then fail "%s has an odd number of bounds" label
          else begin
            let i = ref 0 in
            while !i < Array.length r16 do
              if r16.(!i) > r16.(!i + 1) || (!i > 0 && r16.(!i) <= r16.(!i - 1) + 1) then
                fail "%s: the 16-bit range at %d is out of order or touches the one before" label !i;
              if r16.(!i + 1) > 0xFFFF then fail "%s: the 16-bit range at %d is out of bounds" label !i;
              i := !i + 2
            done;
            i := 0;
            while !i < Array.length r32 do
              if
                r32.(!i) < 0x10000
                || r32.(!i) > r32.(!i + 1)
                || r32.(!i + 1) > Ucd.max_rune
                || (!i > 0 && r32.(!i) <= r32.(!i - 1) + 1)
              then fail "%s: the 32-bit range at %d is out of order, out of bounds or touches the one before" label !i;
              i := !i + 2
            done;
            let pairs = Ucd.ranges table in
            i := 0;
            while !i < Array.length pairs do
              if pairs.(!i) > pairs.(!i + 1) || (!i > 0 && pairs.(!i) <= pairs.(!i - 1) + 1) then
                fail "%s: ranges gives a range at %d that is out of order or touches the one before" label !i;
              i := !i + 2
            done
          end)
    tables;
  Printf.printf "  %d names resolve\n" (List.length tables)

(* Walks every code point for tables of each shape. *)
let test_contains_agrees_with_the_ranges () =
  List.iter
    (fun table ->
      incr checks;
      let pairs = Ucd.ranges table in
      let next = ref 0 in
      for r = 0 to Ucd.max_rune do
        while !next < Array.length pairs && pairs.(!next + 1) < r do
          next := !next + 2
        done;
        let want = !next < Array.length pairs && pairs.(!next) <= r in
        if Ucd.contains table r <> want then fatal "contains(U+%04X) = %b, and the ranges say %b" r (not want) want
      done;
      if Ucd.contains table (-1) || Ucd.contains table (Ucd.max_rune + 1) then
        fail "contains accepts a value that is not a code point")
    [
      get (Ucd.category "Lu");
      get (Ucd.category "Cn");
      get (Ucd.category "Co");
      Ucd.letter;
      Ucd.assigned;
      Ucd.unknown_script;
      get (Ucd.binary "Alphabetic");
      get (Ucd.binary "Noncharacter_Code_Point");
      get (Ucd.script "Han");
      get (Ucd.script_extensions "Latin");
      get (Ucd.script "Adlam");
      Ucd.union [];
    ]

(* Checks that every code point has exactly one General_Category, that the groups hold what their members hold, and
   that Assigned and the Unknown script follow from the categories. *)
let test_general_categories_partition_the_code_points () =
  let groups =
    List.map
      (fun (group, members) ->
        (group, get (Ucd.category group), List.map (fun member -> get (Ucd.category member)) members))
      [
        ("L", [ "Lu"; "Ll"; "Lt"; "Lm"; "Lo" ]);
        ("LC", [ "Lu"; "Ll"; "Lt" ]);
        ("M", [ "Mn"; "Mc"; "Me" ]);
        ("N", [ "Nd"; "Nl"; "No" ]);
        ("P", [ "Pc"; "Pd"; "Ps"; "Pe"; "Pi"; "Pf"; "Po" ]);
        ("S", [ "Sm"; "Sc"; "Sk"; "So" ]);
        ("Z", [ "Zs"; "Zl"; "Zp" ]);
        ("C", [ "Cc"; "Cf"; "Cs"; "Co"; "Cn" ]);
      ]
  in
  let scripts =
    Array.to_list Ucd.script_names
    |> List.filter (fun names -> names.(0) <> "Unknown")
    |> List.map (fun names -> get (Ucd.script names.(0)))
  in
  let cn = get (Ucd.category "Cn") in
  for r = 0 to Ucd.max_rune do
    incr checks;
    let count = ref 0 in
    List.iter
      (fun (group, table, members) ->
        let inside = List.exists (fun member -> Ucd.contains member r) members in
        if group <> "LC" && inside then incr count;
        if Ucd.contains table r <> inside then fatal "U+%04X: the group %s and its members disagree" r group)
      groups;
    if !count <> 1 then fatal "U+%04X is in %d General_Category groups" r !count;
    if Ucd.contains Ucd.assigned r = Ucd.contains cn r then fatal "U+%04X: Assigned is not the complement of Cn" r;
    let in_script = List.fold_left (fun n script -> if Ucd.contains script r then n + 1 else n) 0 scripts in
    let unknown = Ucd.contains Ucd.unknown_script r in
    if in_script > 1 || in_script = 0 <> unknown then
      fatal "U+%04X is in %d scripts, and Unknown says %b" r in_script unknown
  done

(* Checks code points whose properties older versions of Unicode do not have. *)
let test_the_data_is_unicode_17 () =
  if Ucd.version <> "17.0.0" then fatal "version = %S. Update this test for the new data." Ucd.version;
  List.iter
    (fun (table, r, want, what) ->
      incr checks;
      match table with
      | None -> fail "no table: %s" what
      | Some table ->
          let got = Ucd.contains table r in
          if got <> want then fail "contains(U+%04X) = %b: %s" r got what)
    [
      (Ucd.script "Garay", 0x10D40, true, "Garay is a script of Unicode 16");
      (Ucd.script "Gara", 0x10D40, true, "Gara is the code of Garay");
      (Ucd.script "Sidetic", 0x10940, true, "Sidetic is a script of Unicode 17");
      (Ucd.script "Beria_Erfe", 0x16EA0, true, "Beria Erfe is a script of Unicode 17");
      (Ucd.category "Lu", 0x16EA0, true, "U+16EA0 is an uppercase letter of Unicode 17");
      (Ucd.category "Cn", 0x16EA0, false, "U+16EA0 is assigned in Unicode 17");
      (Ucd.category "Cn", 0x1FAE9, false, "U+1FAE9 is assigned in Unicode 16");
      (Ucd.binary "Emoji", 0x1FAE9, true, "U+1FAE9 is an emoji of Unicode 16");
      (Ucd.script "Han", 0x323B0, true, "U+323B0 starts CJK extension J of Unicode 17");
      (Some Ucd.unknown_script, 0x10D40, false, "U+10D40 has a script");
      (* Katakana_Or_Hiragana is a Script value of PropertyValueAliases.txt that no code point has. *)
      (Ucd.script "Katakana_Or_Hiragana", 0x3042, false, "Katakana_Or_Hiragana is a script with no code point");
      (Ucd.script "Hrkt", 0x30FC, false, "Hrkt is a script with no code point");
      (Ucd.script_extensions "Katakana_Or_Hiragana", 0x30FC, false, "Katakana_Or_Hiragana has no code point");
      (Ucd.script_extensions "Hrkt", 0x3042, false, "Hrkt has no code point");
      (Some Ucd.assigned, 0x10FFFF, false, "U+10FFFF is a noncharacter");
    ];
  (* Case pairs of Unicode 16 and 17. *)
  List.iter
    (fun (upper, lower) ->
      incr checks;
      if Ucd.fold upper <> lower then fail "fold(U+%04X) = U+%04X, want U+%04X" upper (Ucd.fold upper) lower;
      if Ucd.to_upper lower <> upper then fail "to_upper(U+%04X) = U+%04X, want U+%04X" lower (Ucd.to_upper lower) upper)
    [ (0x10D50, 0x10D70); (0x16EA0, 0x16EBB); (0xA7CB, 0x264); (0x1C89, 0x1C8A) ];
  List.iter
    (fun name ->
      incr checks;
      if
        Ucd.category name <> None
        || Ucd.script name <> None
        || Ucd.script_extensions name <> None
        || Ucd.binary name <> None
      then fail "%S names a table" name)
    [ "Nope"; ""; "l"; "Latn="; "Any"; "ASCII"; "hrkt"; "Katakana_or_Hiragana" ]

(* Checks fold and to_upper over every code point. *)
let test_case_mappings () =
  let folding = Ucd.folding () in
  let next = ref 0 in
  for r = 0 to Ucd.max_rune do
    incr checks;
    let folded = Ucd.fold r in
    if Ucd.fold folded <> folded then fatal "fold is not idempotent at U+%04X" r;
    let changes = !next < Array.length folding && folding.(!next) = r in
    if changes then incr next;
    if changes <> (folded <> r) then fatal "folding and fold disagree at U+%04X" r;
    if r < 0x80 then
      if folded <> Ucd.fold_by_table r || Ucd.to_upper r <> Ucd.to_upper_by_table r then
        fatal "the ASCII path and the table disagree at U+%04X" r
  done;
  if !next <> Array.length folding then fail "folding lists a code point that is out of order";
  List.iter
    (fun (from, want) ->
      incr checks;
      if Ucd.fold from <> want then fail "fold(U+%04X) = U+%04X, want U+%04X" from (Ucd.fold from) want)
    [
      (0x41, 0x61);
      (0x7A, 0x7A);
      (0xB5, 0x3BC);
      (0x17F, 0x73);
      (0x212A, 0x6B);
      (0x1E9E, 0xDF);
      (0x3C2, 0x3C3);
      (0x130, 0x130);
      (0x1F88, 0x1F80);
      (0x1E921, 0x1E943);
    ];
  List.iter
    (fun (from, want) ->
      incr checks;
      if Ucd.to_upper from <> want then fail "to_upper(U+%04X) = U+%04X, want U+%04X" from (Ucd.to_upper from) want)
    [ (0x61, 0x41); (0xDF, 0xDF); (0xFF, 0x178); (0x131, 0x49); (0x17F, 0x53); (0x1C5, 0x1C4) ]

(* The lookups do not allocate. BenchmarkContains of the Go source reports the same of Contains. *)
let test_lookups_do_not_allocate () =
  let text = [| 0x395; 0x43A; 0x65E5; 0x627; 0xEF; 0xC9; 0x9EA; 0x31; 0x1F432; 0x10FFFF; 0x41 |] in
  let sink = ref 0 in
  let run () =
    for _ = 1 to 1000 do
      for i = 0 to Array.length text - 1 do
        let r = text.(i) in
        if Ucd.contains Ucd.letter r then incr sink;
        sink := !sink + Ucd.fold r + Ucd.to_upper r
      done
    done
  in
  run ();
  let before = Gc.minor_words () in
  run ();
  let words = Gc.minor_words () -. before in
  incr checks;
  if words > 0. then fail "contains, fold and to_upper allocated %.0f words over 11000 lookups" words

let () =
  let ran = ref 0 in
  List.iter
    (fun (name, test) ->
      Printf.printf "%s\n%!" name;
      incr ran;
      try test () with Stop -> ())
    [
      ("tables are well formed", test_tables_are_well_formed);
      ("contains agrees with the ranges", test_contains_agrees_with_the_ranges);
      ("general categories partition the code points", test_general_categories_partition_the_code_points);
      ("the data is Unicode 17", test_the_data_is_unicode_17);
      ("case mappings", test_case_mappings);
      ("lookups do not allocate", test_lookups_do_not_allocate);
    ];
  Printf.printf "ucd: %d tests, %d checks, %d failures\n" !ran !checks !failures;
  if !failures > 0 then exit 1
