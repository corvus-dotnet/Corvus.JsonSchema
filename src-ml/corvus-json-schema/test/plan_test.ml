(* The plans' type algebra, the name lookup and the length test. A port of plan_test.go of the Go module. *)

open Corvus_json_schema_test_support
module Document = Corvus_json_schema__Document
module Names = Corvus_json_schema__Names
module Node = Corvus_json_schema__Node
module Program = Corvus_json_schema__Program
module Plan = Corvus_json_schema__Plan
module Eval = Corvus_json_schema__Eval
module Compiler = Corvus_json_schema__Compiler
module Options = Corvus_json_schema__Options

let must_parse json =
  match Document.parse_string json with
  | Ok d -> d
  | Error e -> failwith (Printf.sprintf "parse %S: %s" json (Document.error_to_string e))

let index_of list name =
  let rec at i = function [] -> -1 | x :: rest -> if String.equal x name then i else at (i + 1) rest in
  at 0 list

let test_meet_treats_integers_as_numbers () =
  Check.section "TestMeetTreatsIntegersAsNumbers";
  let open Node in
  let any_type = Program.any_type in
  List.iter
    (fun (a, b, want) ->
      Check.checkf (Plan.meet_types a b = want) "meet(%#x, %#x) = %#x, want %#x" a b (Plan.meet_types a b) want)
    [
      (type_integer, type_number, type_integer);
      (type_number, type_number lor type_string, type_number lor type_integer);
      (any_type, type_string lor type_array, type_string lor type_array);
      (type_string, type_integer, 0);
    ];
  List.iter
    (fun json ->
      let d = must_parse json in
      let root = Document.root d in
      List.iter
        (fun (a, b) ->
          let got = Eval.type_ok (Plan.meet_types a b) d root in
          let want = Eval.type_ok a d root && Eval.type_ok b d root in
          Check.checkf (got = want) "%#x and %#x on %s: %b, want %b" a b json got want)
        [
          (type_integer, type_number);
          (type_number, type_integer);
          (type_number lor type_string, type_integer lor type_string);
          (any_type, type_number);
        ])
    [ "1"; "1.5"; {|"a"|} ]

let test_name_map_finds_every_name () =
  Check.section "TestNameMapFindsEveryName";
  let list =
    List.init 200 (fun i -> Printf.sprintf "p%d%s" i (String.make (i mod 13) 'x'))
    @ [
        "";
        "a";
        "ab";
        "abc";
        "abcd";
        "abcdefgh";
        "abcdefghi";
        "\xc3\xa9";
        "\xe6\x97\xa5\xe6\x9c\xac";
        String.make 300 'y';
        "abcdefghijkl";
        "abcdefghijkm";
        "abcdefghXjkl";
        (* Longer than sixteen bytes with the same first and last eight: only the text tells them apart. *)
        "aaaaaaaa-1-bbbbbbbb";
        "aaaaaaaa-2-bbbbbbbb";
        "aaaaaaaa-3-bbbbbbbb";
      ]
  in
  let find m name =
    let b = Bytes.of_string name in
    let n = Bytes.length b in
    Names.map_find m b 0 n (Names.name_word b 0 n)
  in
  let m = Names.new_name_map (Array.of_list list) in
  List.iteri (fun i n -> Check.checkf (find m n = i) "find(%S) = %d, want %d" n (find m n) i) list;
  List.iter
    (fun miss -> Check.checkf (find m miss = -1) "find(%S) = %d" miss (find m miss))
    [
      "p";
      "q1";
      "p1000";
      "p0x";
      "b";
      "abce";
      "abcdefgj";
      "abcdefghj";
      "\xc3\xa8";
      "y";
      String.make 299 'y';
      "abcdefghijkn";
      "abcdefghXjkm";
      "aaaaaaaa-4-bbbbbbbb";
      String.make 150 'y' ^ "z" ^ String.make 149 'y';
    ];
  List.iter
    (fun few ->
      let m = Names.new_name_map (Array.of_list few) in
      List.iter
        (fun n -> Check.checkf (find m n = index_of few n) "find(%S) = %d, want %d" n (find m n) (index_of few n))
        few;
      Check.checkf (find m "zeta!" = -1) "find(zeta!) = %d" (find m "zeta!"))
    [
      [];
      [ "a" ];
      [ "ab"; "ba" ];
      [ "alpha"; "gamma"; "delta"; "omega" ];
      [ "abcdefghij"; "abcdefghik" ];
      [ "twice"; "twice"; "once" ];
    ]

(* find_from as Go has it: the index, and the hint for the next call. *)
let find_from ns name hint =
  let b = Bytes.of_string name in
  let i = Names.find_from ns b 0 (Bytes.length b) hint in
  (i, if i >= 0 then i + 1 else hint)

let test_names_follow_declared_or_sorted_order () =
  Check.section "TestNamesFollowDeclaredOrSortedOrder";
  let declared = [ "name"; "version"; "repository"; "alias" ] in
  let ns = Names.create (Array.of_list declared) in
  (* Every order of the names, from every starting hint, finds each one. *)
  List.iter
    (fun order ->
      for start = 0 to List.length declared do
        let hint = ref start in
        List.iter
          (fun n ->
            let got, next = find_from ns n !hint in
            hint := next;
            Check.checkf (got = index_of declared n) "%s in [%s] from %d: %d" n (String.concat "; " order) start got)
          order
      done)
    [
      [ "name"; "version"; "repository"; "alias" ];
      [ "alias"; "name"; "repository"; "version" ];
      [ "version"; "alias"; "name"; "repository" ];
      [ "repository"; "repository"; "name" ];
    ];
  Check.checkf (fst (find_from ns "other" 0) = -1) "other: %d" (fst (find_from ns "other" 0));
  Check.checkf (fst (find_from ns "names" 2) = -1) "names: %d" (fst (find_from ns "names" 2));
  (* Sorted successors: after "name" (index 0) comes "repository" (2), after it "version" (1), then none. *)
  Check.check "sorted_next" (ns.Names.sorted_next = [| 3; 2; Names.no_name; 1; 0 |]);
  let many = Array.init 40 (fun i -> Printf.sprintf "property-number-%02d" (i * 7 mod 40)) in
  let large = Names.create many in
  let start = ref 0 in
  while !start <= Array.length many do
    Array.iteri
      (fun i n ->
        let got = fst (find_from large n !start) in
        Check.checkf (got = i) "%s from %d: %d, want %d" n !start got i)
      many;
    let got = fst (find_from large "property-number-40" !start) in
    Check.checkf (got = -1) "property-number-40 from %d: %d" !start got;
    start := !start + 13
  done

let test_linear_names_find_from_any_hint () =
  Check.section "TestLinearNamesFindFromAnyHint";
  let ns = Names.create [| "a"; "b"; "c" |] in
  for start = 0 to 2 do
    List.iteri
      (fun i n -> Check.checkf (fst (find_from ns n start) = i) "%s from %d: %d" n start (fst (find_from ns n start)))
      [ "a"; "b"; "c" ];
    Check.checkf (fst (find_from ns "d" start) = -1) "d from %d: %d" start (fst (find_from ns "d" start))
  done

let quote name =
  let out = Buffer.create 16 in
  Document.add_quoted out (Bytes.of_string name) 0 (String.length name);
  Buffer.contents out

(* The object plan of the entry of a schema's program. *)
let entry_object_plan schema =
  let compiled = Compiler.compile_document (must_parse schema) Options.default in
  let program = Plan.new_program compiled Options.default in
  match program.Program.plans.(program.Program.entry).Program.body with
  | Some body -> body.Program.object_
  | None -> None

(* A plan of a few names looks each one up in a small object (walk_lookup): every name is found by its length and
   word, the text deciding beyond eight bytes, and a name that is not there is not. *)
let test_property_lookup_by_name () =
  Check.section "TestPropertyLookupByName";
  let instance =
    "{\"a\": 1, \"ab\": 2, \"abcdefgh\": 3, \"abcdefghi\": 4, \"abcdefghj\": 5, \"\xc3\xa9\": 6, \"\": 7, \"a\\nb\": 8}"
  in
  let d = must_parse instance in
  let root = Document.root d in
  let public = match Corvus_json_schema.Document.parse_string instance with Ok d -> d | Error _ -> failwith "parse" in
  let is_valid schema =
    match Corvus_json_schema.Validator.compile_string schema with
    | Ok v -> Corvus_json_schema.Validator.is_valid_document v public
    | Error e -> failwith (schema ^ ": " ^ Corvus_json_schema.error_to_string e)
  in
  List.iter
    (fun (name, want) ->
      let v = Document.property d root name in
      Check.checkf (v >= 0 && Document.number_text d v = want) "property %S: %d" name v;
      List.iter
        (fun (value, valid) ->
          let schema =
            {|{"properties": {|} ^ quote name ^ {|: {"const": |} ^ value ^ {|}}, "required": [|} ^ quote name ^ "]}"
          in
          (match entry_object_plan schema with
          | Some pl when pl.Program.lookup -> Check.check "a lookup plan" true
          | _ -> Check.fail (schema ^ ": not a lookup plan"));
          Check.checkf (is_valid schema = valid) "%s: %b, want %b" schema (is_valid schema) valid)
        [ (want, true); ("0", false) ])
    [
      ("a", "1");
      ("ab", "2");
      ("abcdefgh", "3");
      ("abcdefghi", "4");
      ("abcdefghj", "5");
      ("\xc3\xa9", "6");
      ("", "7");
      ("a\nb", "8");
    ];
  List.iter
    (fun miss ->
      Check.checkf (Document.property d root miss = -1) "property %S found" miss;
      (* Found, the property would fail its schema. Required, it is missed. *)
      List.iter
        (fun (schema, valid) -> Check.checkf (is_valid schema = valid) "%s: %b, want %b" schema (is_valid schema) valid)
        [
          ({|{"properties": {|} ^ quote miss ^ {|: false}}|}, true);
          ({|{"properties": {|} ^ quote miss ^ {|: true}, "required": [|} ^ quote miss ^ "]}", false);
        ])
    [ "b"; "ba"; "abcdefgi"; "abcdefghk"; "abcdefgh "; "\xc3\xa8" ]

let test_length_ok_agrees_with_counting () =
  Check.section "TestLengthOKAgreesWithCounting";
  List.iter
    (fun (s, chars) ->
      let d = must_parse ("\"" ^ s ^ "\"") in
      for min = 0 to 5 do
        for max = 0 to 5 do
          let got = Eval.length_ok d (Document.root d) min max in
          Check.checkf (got = (chars >= min && chars <= max)) "%S in [%d, %d]: %b" s min max got
        done
      done)
    [
      ("", 0);
      ("a", 1);
      ("abcd", 4);
      ("\xc3\xa9", 1);
      ("\xc3\xa9\xc3\xa9", 2);
      ("\xf0\x9f\x98\x80\xf0\x9f\x98\x80", 2);
      ("a\xf0\x9f\x98\x80b", 3);
    ]

let () =
  test_meet_treats_integers_as_numbers ();
  test_name_map_finds_every_name ();
  test_names_follow_declared_or_sorted_order ();
  test_linear_names_find_from_any_hint ();
  test_property_lookup_by_name ();
  test_length_ok_agrees_with_counting ();
  Check.finish "plan"
