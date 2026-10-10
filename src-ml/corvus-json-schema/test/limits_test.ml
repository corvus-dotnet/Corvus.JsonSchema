(* The places where this port holds less in a mask than the Go module does. Go's masks are 64 bits and an OCaml int
   is 63, so a mask here has at most 62 members: the names an object plan sees, the conditions and the branches of a
   fused plan, the tests of one property, the properties whose pending applications a mask remembers. A set that
   needs more takes the path Go takes beyond 64. These tests cross each of those edges, and the edge of the name
   table, and check the fail-fast plans against the collecting evaluator (which has no such limit) and against the
   answer that is known.

   This file has no counterpart in the Go module. *)

open Corvus_json_schema
open Corvus_json_schema_test_support

let compile schema =
  match Validator.compile_string schema with Ok v -> v | Error e -> failwith (schema ^ ": " ^ error_to_string e)

(* expect checks an instance every way: fail-fast as a string and as a document, and collecting. *)
let expect what v instance want =
  let fast = Validator.is_valid_string v instance in
  let document =
    match Document.parse_string instance with Ok d -> d | Error e -> failwith (Document.error_to_string e)
  in
  let as_document = Validator.is_valid_document v document in
  let collected = Validator.evaluate_string v ~results:(Results.create Results.Basic) instance in
  Check.checkf
    (fast = want && as_document = want && collected = Ok want)
    "%s: fail-fast %b, as a document %b, collecting %s, want %b" what fast as_document
    (match collected with Ok ok -> string_of_bool ok | Error e -> error_to_string e)
    want

let join = String.concat ","
let range n f = List.init n f
let sizes = [ 1; 2; 31; 32; 33; 60; 61; 62; 63; 64; 65; 66; 130 ]

(* An object of the properties p<i> for the i that keep says, each with the value that value gives. *)
let object_of n keep value =
  "{"
  ^ join
      (List.filter_map
         (fun i -> if keep i then Some (Printf.sprintf {|"p%d":%s|} i (value i)) else None)
         (range n Fun.id))
  ^ "}"

let test_required_names_of_an_object_plan () =
  Check.section "the required names of an object plan";
  List.iter
    (fun n ->
      let properties = join (range n (fun i -> Printf.sprintf {|"p%d":{"type":"integer"}|} i)) in
      let required = join (range n (fun i -> Printf.sprintf {|"p%d"|} i)) in
      let v =
        compile
          (Printf.sprintf {|{"properties":{%s},"required":[%s],"additionalProperties":false}|} properties required)
      in
      let what = Printf.sprintf "%d names" n in
      expect what v (object_of n (fun _ -> true) string_of_int) true;
      (* In the reverse order. *)
      expect what v ("{" ^ join (List.rev (range n (fun i -> Printf.sprintf {|"p%d":%d|} i i))) ^ "}") true;
      List.iter
        (fun missing ->
          if missing < n then
            expect
              (Printf.sprintf "%s without p%d" what missing)
              v
              (object_of n (fun i -> i <> missing) string_of_int)
              false)
        [ 0; 1; 30; 31; 32; 60; 61; 62; 63; 64; n - 1 ];
      List.iter
        (fun wrong ->
          if wrong < n then
            expect
              (Printf.sprintf "%s with a string for p%d" what wrong)
              v
              (object_of n (fun _ -> true) (fun i -> if i = wrong then {|"s"|} else string_of_int i))
              false)
        [ 0; 61; 62; 63; n - 1 ];
      expect (what ^ " and one more") v (object_of (n + 1) (fun _ -> true) string_of_int) false;
      expect (what ^ ", not an object") v "[]" true)
    sizes

let test_names_that_only_dependencies_mention () =
  Check.section "the names only required and dependencies mention";
  List.iter
    (fun n ->
      let properties = join (range n (fun i -> Printf.sprintf {|"p%d":{"type":"integer"}|} i)) in
      let v =
        compile
          (Printf.sprintf
             {|{"properties":{%s},"required":["p0","extra"],"dependentRequired":{"p0":["x1","x2"],"x3":["p1"]},"dependentSchemas":{"x1":{"required":["x4"]}}}|}
             properties)
      in
      let what = Printf.sprintf "%d declared names" n in
      let base = join (range n (fun i -> Printf.sprintf {|"p%d":%d|} i i)) in
      expect what v (Printf.sprintf {|{%s,"extra":1,"x1":1,"x2":1,"x4":1}|} base) true;
      expect (what ^ " without x2") v (Printf.sprintf {|{%s,"extra":1,"x1":1,"x4":1}|} base) false;
      expect (what ^ " without x4") v (Printf.sprintf {|{%s,"extra":1,"x1":1,"x2":1}|} base) false;
      expect (what ^ " without extra") v (Printf.sprintf {|{%s,"x1":1,"x2":1,"x4":1}|} base) false;
      expect (what ^ " with x3") v (Printf.sprintf {|{%s,"extra":1,"x1":1,"x2":1,"x4":1,"x3":1}|} base) (n >= 2);
      expect (what ^ " with x3 and no p1") v {|{"p0":1,"extra":1,"x1":1,"x2":1,"x4":1,"x3":1}|} false)
    sizes

let test_properties_beyond_the_mask_of_a_fused_pass () =
  Check.section "the properties of a fused pass";
  List.iter
    (fun n ->
      let numbers = join (range n (fun i -> Printf.sprintf {|"p%d":{"type":"integer"}|} i)) in
      let strings = join (range n (fun i -> Printf.sprintf {|"p%d":{"type":"string"}|} i)) in
      let v =
        compile
          (Printf.sprintf
             {|{
                "properties": {"kind": {"enum": ["a", "b"]}},
                "allOf": [{"patternProperties": {"^x-": true}}],
                "if": {"properties": {"kind": {"const": "a"}}, "required": ["kind"]},
                "then": {"properties": {%s}},
                "else": {"properties": {%s}},
                "unevaluatedProperties": false
              }|}
             numbers strings)
      in
      let what = Printf.sprintf "%d conditional properties" n in
      let with_kind kind value =
        Printf.sprintf {|{"kind":"%s",%s}|} kind (join (range n (fun i -> Printf.sprintf {|"p%d":%s|} i (value i))))
      in
      expect (what ^ ", then") v (with_kind "a" string_of_int) true;
      expect (what ^ ", else") v (with_kind "b" (fun _ -> {|"s"|})) true;
      expect (what ^ ", then with strings") v (with_kind "a" (fun _ -> {|"s"|})) false;
      expect (what ^ ", else with numbers") v (with_kind "b" string_of_int) false;
      List.iter
        (fun wrong ->
          if wrong < n then begin
            expect
              (Printf.sprintf "%s, then with a string for p%d" what wrong)
              v
              (with_kind "a" (fun i -> if i = wrong then {|"s"|} else string_of_int i))
              false;
            expect
              (Printf.sprintf "%s, else with a number for p%d" what wrong)
              v
              (with_kind "b" (fun i -> if i = wrong then "1" else {|"s"|}))
              false
          end)
        [ 0; 59; 60; 61; 62; 63; 64; n - 1 ];
      (* The kind last, so that every property before it waits for the condition. *)
      expect (what ^ ", the kind last") v
        (Printf.sprintf {|{%s,"x-a":1,"kind":"a"}|} (join (range n (fun i -> Printf.sprintf {|"p%d":%d|} i i))))
        true;
      expect (what ^ ", an unevaluated property") v
        (Printf.sprintf {|{%s,"other":1,"kind":"a"}|} (join (range n (fun i -> Printf.sprintf {|"p%d":%d|} i i))))
        false)
    sizes

let test_conditions_of_a_fused_plan () =
  Check.section "the conditions of a fused plan";
  List.iter
    (fun n ->
      let branches =
        join
          (range n (fun i ->
               Printf.sprintf
                 {|{"if":{"required":["a%d"]},"then":{"required":["b%d"]},"else":{"properties":{"c%d":{"type":"integer"}}}}|}
                 i i i))
      in
      let v = compile (Printf.sprintf {|{"type":"object","properties":{"z":true},"allOf":[%s]}|} branches) in
      let what = Printf.sprintf "%d conditions" n in
      expect what v "{}" true;
      expect (what ^ ", every a and b") v
        ("{" ^ join (range n (fun i -> Printf.sprintf {|"a%d":1,"b%d":1|} i i)) ^ "}")
        true;
      List.iter
        (fun i ->
          if i < n then begin
            expect (Printf.sprintf "%s, a%d without b%d" what i i) v (Printf.sprintf {|{"a%d":1}|} i) false;
            expect (Printf.sprintf "%s, a%d with b%d" what i i) v (Printf.sprintf {|{"a%d":1,"b%d":2}|} i i) true;
            expect (Printf.sprintf "%s, a string for c%d" what i) v (Printf.sprintf {|{"c%d":"s"}|} i) false;
            expect
              (Printf.sprintf "%s, a string for c%d under a%d" what i i)
              v
              (Printf.sprintf {|{"c%d":"s","a%d":1,"b%d":1}|} i i i)
              true
          end)
        [ 0; 1; 60; 61; 62; 63; 64; n - 1 ])
    sizes

let test_tests_of_one_property () =
  Check.section "the tests of one property";
  List.iter
    (fun n ->
      let branches =
        join
          (range n (fun i ->
               Printf.sprintf
                 {|{"if":{"properties":{"kind":{"const":%d}},"required":["kind"]},"then":{"required":["r%d"]}}|} i i))
      in
      let v =
        compile (Printf.sprintf {|{"type":"object","properties":{"kind":{"type":"integer"}},"allOf":[%s]}|} branches)
      in
      let what = Printf.sprintf "%d tests of a property" n in
      expect what v "{}" true;
      expect (what ^ ", another kind") v {|{"kind":100000}|} true;
      List.iter
        (fun i ->
          if i < n then begin
            expect (Printf.sprintf "%s, kind %d" what i) v (Printf.sprintf {|{"kind":%d}|} i) false;
            expect
              (Printf.sprintf "%s, kind %d.0 with r%d" what i i)
              v
              (Printf.sprintf {|{"kind":%d.0,"r%d":1}|} i i)
              true;
            expect
              (Printf.sprintf "%s, kind %d with another r" what i)
              v
              (Printf.sprintf {|{"kind":%d,"r%d":1}|} i (i + 1))
              false
          end)
        [ 0; 1; 60; 61; 62; 63; 64; n - 1 ])
    sizes

let test_branches_of_an_alternative () =
  Check.section "the branches of an alternative";
  List.iter
    (fun n ->
      List.iter
        (fun keyword ->
          let branches =
            join
              (range n (fun i -> Printf.sprintf {|{"properties":{"k%d":{"type":"integer"}},"required":["k%d"]}|} i i))
          in
          let v = compile (Printf.sprintf {|{"type":"object","properties":{"z":true},"%s":[%s]}|} keyword branches) in
          let what = Printf.sprintf "%s of %d branches" keyword n in
          expect what v "{}" false;
          List.iter
            (fun i ->
              if i < n then begin
                expect (Printf.sprintf "%s, k%d" what i) v (Printf.sprintf {|{"k%d":1}|} i) true;
                expect (Printf.sprintf "%s, a string for k%d" what i) v (Printf.sprintf {|{"k%d":"s"}|} i) false;
                if i + 1 < n then
                  expect
                    (Printf.sprintf "%s, k%d and k%d" what i (i + 1))
                    v
                    (Printf.sprintf {|{"k%d":1,"k%d":2}|} i (i + 1))
                    (keyword = "anyOf")
              end)
            [ 0; 1; 60; 61; 62; 63; 64; n - 1 ])
        [ "oneOf"; "anyOf" ])
    sizes

(* A set of names too large for the table is a map, which Go has beyond 32768 names too. *)
let test_names_beyond_the_table () =
  Check.section "names beyond the table";
  let n = 40000 in
  let v = compile (Printf.sprintf {|{"enum":[%s]}|} (join (range n (fun i -> Printf.sprintf {|"name-%d"|} i)))) in
  List.iter
    (fun i -> expect (Printf.sprintf "name-%d" i) v (Printf.sprintf {|"name-%d"|} i) true)
    [ 0; 1; 32767; 32768; n - 1 ];
  List.iter
    (fun name -> expect name v (Printf.sprintf {|"%s"|} name) false)
    [ "name-40000"; "name"; ""; "a-name-that-is-longer-than-sixteen-bytes" ];
  expect "a number" v "1" false

let () =
  test_required_names_of_an_object_plan ();
  test_names_that_only_dependencies_mention ();
  test_properties_beyond_the_mask_of_a_fused_pass ();
  test_conditions_of_a_fused_plan ();
  test_tests_of_one_property ();
  test_branches_of_an_alternative ();
  test_names_beyond_the_table ();
  Check.finish "limits"
