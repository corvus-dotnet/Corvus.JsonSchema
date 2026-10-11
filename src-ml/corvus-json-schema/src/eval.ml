(* Evaluation. One general evaluator serves two modes. Without a collector it fails fast at the first violation and
   reports nothing. With a collector it is exhaustive and reports every keyword with its evaluation path, schema
   location and instance location, reproducing the C# collecting mode (keyword order, paths, messages, which
   subschema results are committed or discarded). Fail-fast evaluation mostly runs through plans and comes to the
   general evaluator only for nodes that track evaluated properties or items.

   A port of eval.go and of the evaluation halves of plan.go and fused.go of the Go module. They are one file here
   because the functions of the three call each other.

   Nothing on the fail-fast path allocates. Where Go returns two values, a function here returns one [int] that
   holds both (a mask of seen names, or -1 for failure), and where Go keeps a small structure on the stack (a set of
   evaluated properties, the state of a fused pass) it is a run of words in the evaluator's arena. No function on
   that path is a closure, and no float leaves the array it is in. (The collecting path does allocate, as it builds
   its results.) *)

open Document
open Node
open Program

(* The state of an evaluation, and the buffers it needs, which are reused from one evaluation to the next. Go keeps
   the state on the stack and the buffers in a pool of the validator. Here both are in one value that one evaluation
   at a time uses. *)
type t = {
  mutable p : Program.t;
  (* The instance document. *)
  mutable d : Document.t;
  (* Whether results are collected, and the collector (one of the evaluator's own when they are not). *)
  mutable collect : bool;
  mutable c : Results.t;
  idle : Results.t;
  mutable annotations : annotation_entry array array;
  mutable depth : int;
  (* Evaluation recursed in place beyond the maximum depth. *)
  mutable depth_exceeded : bool;
  (* The dynamic scope: the resources entered, outermost first. *)
  mutable scope : int array;
  mutable nscope : int;
  (* The evaluated-property and evaluated-item sets in use, and the states of the fused passes in progress. *)
  mutable arena : int array;
  mutable narena : int;
  (* Scratch for uniqueItems. *)
  unique : Values.scratch;
  (* For validating JSON text: the document it is parsed into, the parser, and the copy of a string's text. *)
  text : Document.t;
  parser : Document.parser;
  mutable copy : Bytes.t;
  (* For content assertions: the decoded bytes, and the parser that checks them. *)
  mutable content : Bytes.t;
  content_parser : Document.parser;
  (* The working memory of the regular expression engine. *)
  regex : Pattern_engine.scratch;
  (* An evaluation is using this evaluator. One that finds it in use makes its own. *)
  mutable in_use : bool;
}

(* A program with no node, which an evaluator holds between evaluations so that it keeps nothing of a validator
   alive. *)
let no_program : Program.t =
  {
    nodes = [||];
    root = no_node;
    entry = no_node;
    uses_dynamic_scope = false;
    max_depth = 0;
    pattern_match_limit = 0.;
    custom_formats = [||];
    fast_target = [||];
    property_maps = [||];
    annotation_sources = [||];
    annotations = Atomic.make (Some [||]);
    assert_format_set = false;
    plans = [||];
  }

let no_document = Document.create_empty ()

let create () =
  let idle = Results.create Results.Basic in
  {
    p = no_program;
    d = no_document;
    collect = false;
    c = idle;
    idle;
    annotations = [||];
    depth = 0;
    depth_exceeded = false;
    scope = Array.make 8 0;
    nscope = 0;
    arena = Array.make 64 0;
    narena = 0;
    unique = Values.create_scratch ();
    text = Document.create_empty ();
    parser = Document.create_parser ();
    copy = Bytes.empty;
    content = Bytes.empty;
    content_parser = Document.create_parser ();
    regex = Pattern_engine.create_scratch ();
    in_use = false;
  }

(* Messages (the C# evaluator's text). *)
let msg_evaluated_subschema = "The value was expected to match the subschema."
let msg_matched_all = "The value matched all subschema."
let msg_did_not_match_all = "The value did not match all subschema."
let msg_matched_at_least_one = "The value matched at least one subschema."
let msg_did_not_match_at_least_one = "The value did not match at least one subschema."
let msg_matched_no_schema = "The instance matched no schema."
let msg_matched_exactly_one = "The value matched exactly one subschema."
let msg_matched_more_than_one = "The instance matched more than one schema."

let msg_matched_not =
  "The value matched the subschema in a not composition, which means the evaluation was not a match."

let msg_did_not_match_not =
  "The value did not match the subschema in a not composition, which means the evaluation was a match."

let msg_matched_if_for_then =
  "The value matched the subschema in a binary or ternay if, which means the evaluation will go on to match the then \
   subschema."

let msg_matched_if_for_else =
  "The value did not match the subschema in a ternary if, which means the evaluation will go on to match the else \
   subschema."

let msg_matched_then = "The value matched the then subschema corresponding to a binary or ternary if."
let msg_did_not_match_then = "The value did not match the then subschema corresponding to a binary or ternary if."
let msg_matched_else = "The value matched the else subschema corresponding to a ternary if."
let msg_did_not_match_else = "The value did not match the else subschema corresponding to a ternary if."
let msg_unique_items = "The array was expected to contain unique items."
let msg_property_name_failed = "The property name did not match the schema."

(* quoted is " 'v'", or nothing for an empty value. *)
let quoted v = if v = "" then "" else " '" ^ v ^ "'"

let type_order =
  [
    (type_array, "array");
    (type_object, "object");
    (type_null, "null");
    (type_boolean, "boolean");
    (type_number, "number");
    (type_integer, "integer");
    (type_string, "string");
  ]

let type_message mask =
  match List.filter_map (fun (m, name) -> if mask land m <> 0 then Some name else None) type_order with
  | [] -> ""
  | [ name ] -> "The value was expected to be of type '" ^ name ^ "'"
  | names -> "The value was expected to be of type '[\"" ^ String.concat "\", \"" names ^ "\"]'"

let const_message (v : value_ref) =
  let kind = kind v.d v.n in
  if kind = kind_string then "Expected the value to be the string" ^ quoted (Document.string_value v.d v.n)
  else if kind = kind_number then "The value was expected to be equal to" ^ quoted (number_text v.d v.n)
  else if kind = kind_bool then "Expected the value to be '" ^ (if boolean v.d v.n then "true" else "false") ^ "'"
  else if kind = kind_null then "Expected the value to be 'null'"
  else ""

(* integer_ok reports a number that the mask does not accept as a number but accepts as an integer. *)
let integer_ok mask d x = kind d x = kind_number && mask land type_integer <> 0 && is_integer d x

(* type_ok reports whether a value is of one of the types in a mask. A kind is its type bit. *)
let type_ok mask d x = mask land kind d x <> 0 || integer_ok mask d x

(* code_points is the length of a string value in code points (what minLength and maxLength count). *)
let code_points d x =
  if str_ascii d x then str_len d x
  else begin
    let b = str_bytes d x and off = str_off d x in
    let n = ref 0 in
    for i = off to off + str_len d x - 1 do
      if Char.code (Bytes.get b i) land 0xc0 <> 0x80 then incr n
    done;
    !n
  end

(* length_ok decides minLength/maxLength, counting code points only when the byte length cannot decide (a code point
   is one to four bytes). *)
let length_ok d x min max =
  let length = str_len d x in
  if length < min then false
  else if str_ascii d x then length <= max
  else begin
    let quarter = (length + 3) / 4 in
    if length <= max && quarter >= min then true
    else if quarter > max then (* Every code point is at most four bytes: more than max of them for sure. *) false
    else begin
      let chars = code_points d x in
      chars >= min && chars <= max
    end
  end

(* ---------------------------------------------------------------------------------------------------------------------
   Evaluated properties and items

   A set of evaluated properties (by index in the instance object) or items is a run of words in the evaluator's
   arena: its number of words, then the words, of 32 members each. A set is named by where it starts, and "no set" is
   a negative offset. *)

let no_bits = -1

(* take takes n words from the arena, giving where they start. Runs are released in the reverse order. *)
let take e n =
  let off = e.narena in
  let stop = off + n in
  if stop > Array.length e.arena then begin
    let grown = Array.make ((2 * stop) + 16) 0 in
    Array.blit e.arena 0 grown 0 off;
    e.arena <- grown
  end;
  e.narena <- stop;
  off

(* new_bits takes a cleared set for length members from the arena. *)
let new_bits e length =
  let words = (length + 31) lsr 5 in
  let words = if words = 0 then 1 else words in
  let off = take e (words + 1) in
  e.arena.(off) <- words;
  Array.fill e.arena (off + 1) words 0;
  off

(* The functions below are only given sets that new_bits returned. *)

let free_bits e b = e.narena <- b

let set_bit e b i =
  let at = b + 1 + (i lsr 5) in
  e.arena.(at) <- e.arena.(at) lor (1 lsl (i land 31))

let get_bit e b i = e.arena.(b + 1 + (i lsr 5)) land (1 lsl (i land 31)) <> 0

let merge_bits e into from =
  for i = 1 to e.arena.(from) do
    e.arena.(into + i) <- e.arena.(into + i) lor e.arena.(from + i)
  done

let clear_bits e b = Array.fill e.arena (b + 1) e.arena.(b) 0
let copy_bits e into from = Array.blit e.arena (from + 1) e.arena (into + 1) e.arena.(from)

(* push_scope enters a resource in the dynamic scope, unless it is the innermost one already. It reports whether it
   did. *)
let push_scope e resource =
  let n = e.nscope in
  if n <> 0 && e.scope.(n - 1) = resource then false
  else begin
    if n = Array.length e.scope then e.scope <- Document.grow_ints e.scope n (n + 1);
    e.scope.(n) <- resource;
    e.nscope <- n + 1;
    true
  end

let pop_scope e = e.nscope <- e.nscope - 1

let resolve_dynamic e (d : dynamic_ref_target) =
  let found = ref no_node in
  let s = ref 0 in
  while !found < 0 && !s < e.nscope do
    let resource = e.scope.(!s) in
    let i = ref 0 in
    while !found < 0 && !i < Array.length d.by_resource do
      if d.by_resource.(!i).rn_resource = resource then found := d.by_resource.(!i).rn_node;
      incr i
    done;
    incr s
  done;
  if !found >= 0 then !found else d.fallback

(* content_ok is the draft 7 content assertion, of the len bytes of b from off. *)
let content_ok e b off len kind =
  if kind = content_none then true
  else if kind <> content_json then begin
    let capacity = Formats.base64_decoded_capacity len in
    if Bytes.length e.content < capacity then e.content <- Bytes.create (Int.max capacity (2 * Bytes.length e.content));
    let n = Formats.base64_decode b off len e.content in
    n >= 0 && (kind = content_base64 || Document.is_valid_range e.content_parser e.content 0 n)
  end
  else Document.is_valid_range e.content_parser b off len

(* A format assertion of a string value. A custom format receives a copy of the string. *)
let check_string_format (f : format_check) e x =
  let d = e.d in
  match f.custom with
  | Some custom -> custom (Document.string_value d x)
  | None -> Formats.check_string f.fc_kind (str_bytes d x) (str_off d x) (str_len d x) f.legacy

(* A format assertion of a number value. A custom format receives the number's text. *)
let check_number_format (f : format_check) e x =
  match f.custom with Some custom -> custom (number_text e.d x) | None -> Number_formats.check f.fc_kind e.d x

(* all_of_type reports whether each of count consecutive values is of the types in a mask. *)
let all_of_type d first count types =
  types = any_type
  ||
  let i = ref first in
  let stop = first + count in
  if types land type_integer <> 0 && types land type_number = 0 then
    (* Integers that are not numbers in general: the number's value decides. *)
    while
      !i < stop
      &&
      let bit = kind d !i in
      types land bit <> 0 || (bit = kind_number && is_integer d !i)
    do
      incr i
    done
  else
    (* A kind is its type bit. *)
    while !i < stop && kind d !i land types <> 0 do
      incr i
    done;
  !i = stop

let run_number e (ops : number_op array) x =
  let d = e.d in
  let flag = flags d x and data = data d x in
  let floats = floats d in
  let i = ref 0 in
  let ok = ref true in
  while !ok && !i < Array.length ops do
    let o = ops.(!i) in
    let k = o.n_kind in
    ok :=
      if k = number_minimum then Numbers.compare flag data floats o.n_flag o.n_data o.n_floats >= 0
      else if k = number_maximum then Numbers.compare flag data floats o.n_flag o.n_data o.n_floats <= 0
      else if k = number_exclusive_minimum then Numbers.compare flag data floats o.n_flag o.n_data o.n_floats > 0
      else if k = number_exclusive_maximum then Numbers.compare flag data floats o.n_flag o.n_data o.n_floats < 0
      else if k = number_multiple_of then
        match o.n_divisor with
        | Some divisor -> Numbers.divides divisor flag data (source d) (number_start d x)
        | None -> true
      else check_number_format o.n_format e x;
    incr i
  done;
  !ok

let run_string e (ops : string_op array) x =
  let d = e.d in
  let i = ref 0 in
  let ok = ref true in
  while !ok && !i < Array.length ops do
    let o = ops.(!i) in
    let k = o.s_kind in
    ok :=
      if k = string_length then length_ok d x o.s_min o.s_max
      else if k = string_pattern then
        match o.s_pattern with
        | Some pattern -> Pattern.matches pattern e.regex (str_bytes d x) (str_off d x) (str_len d x) (str_ascii d x)
        | None -> true
      else if k = string_format then check_string_format o.s_format e x
      else content_ok e (str_bytes d x) (str_off d x) (str_len d x) o.s_content;
    incr i
  done;
  !ok

let enum_contains e (values : value_ref array) x =
  let i = ref 0 in
  while !i < Array.length values && not (Values.values_equal e.d x values.(!i).d values.(!i).n) do
    incr i
  done;
  !i < Array.length values

(* names_has reports whether a string value is one of a set of names. *)
let names_has (names : Names.t option) d x =
  match names with Some ns -> Names.find ns (str_bytes d x) (str_off d x) (str_len d x) >= 0 | None -> false

(* run_leaf evaluates a leaf's keywords: its value constraints, then those for the instance's type. *)
let run_leaf e (b : body) x =
  let d = e.d in
  let i = ref 0 in
  let ok = ref true in
  while !ok && !i < Array.length b.b_values do
    let o = b.b_values.(!i) in
    ok :=
      if o.kind = op_const then Values.values_equal d x o.value.d o.value.n
      else if o.kind = op_enum_strings then kind d x = kind_string && names_has o.names d x
      else enum_contains e o.values x;
    incr i
  done;
  !ok
  &&
  let kind = kind d x in
  if kind = kind_number then Array.length b.number = 0 || run_number e b.number x
  else if kind = kind_string then Array.length b.str = 0 || run_string e b.str x
  else true

(* select_index is the branches a discriminator value selects. *)
let select_index (index : discriminator_index) (disc : discriminator) d v =
  if kind d v = kind_string then begin
    let i = Names.find index.strings (str_bytes d v) (str_off d v) (str_len d v) in
    if i >= 0 then disc.known.(index.string_entries.(i)).branches else disc.unknown
  end
  else begin
    let found = ref (-1) in
    let i = ref 0 in
    while !found < 0 && !i < Array.length index.others do
      if dv_matches disc.known.(index.others.(!i)).de_value d v then found := index.others.(!i);
      incr i
    done;
    if !found >= 0 then disc.known.(!found).branches else disc.unknown
  end

(* candidates are the anyOf/oneOf branches that can match: those a discriminator selects, or those admitting the
   instance type. *)
let candidates e (b : branches) x =
  let d = e.d in
  let kind = kind d x in
  match b.discriminator with
  | Some disc when kind = kind_object ->
      let v = property d x disc.property in
      if v >= 0 then match b.index with Some index -> select_index index disc d v | None -> disc.unknown
      else if disc.all_require then [||]
      else b.by_kind.(Plan.kind_index kind)
  | _ -> b.by_kind.(Plan.kind_index kind)

(* Every branch, where select_branches does not narrow them. It is told apart by its identity. *)
let all_branches = [| -1 |]

(* select_branches is the oneOf/anyOf branches a discriminator leaves as candidates for an instance: all of them
   (all_branches), or the listed ones. *)
let select_branches e (disc : discriminator option) x =
  let d = e.d in
  match disc with
  | Some disc when kind d x = kind_object ->
      let value = property d x disc.property in
      if value < 0 then if disc.all_require then [||] else all_branches
      else begin
        let found = ref (-1) in
        let i = ref 0 in
        while !found < 0 && !i < Array.length disc.known do
          if dv_matches disc.known.(!i).de_value d value then found := !i;
          incr i
        done;
        if !found >= 0 then disc.known.(!found).branches else disc.unknown
      end
  | _ -> all_branches

(* ---------------------------------------------------------------------------------------------------------------------
   The state of a fused pass: a run of words in the arena, named by where it starts. Go keeps it on the stack. *)

(* The entries seen: eight words of 32 names each. *)
let pass_seen = 0

(* Bit i: condition i's value test failed. *)
let pass_failed = 8

(* For each alternative group, the branches that failed. *)
let pass_alt_failed = 9
let pass_holds = 9 + max_fused_alt_groups
let pass_gate_ok = pass_holds + 1

(* Once the conditions are decided: those that apply and hold, and those that apply and do not. *)
let pass_then = pass_holds + 2
let pass_els = pass_holds + 3
let pass_words = pass_holds + 4

(* applies reports whether one of the conditions in the masks applies what they guard: one of then that holds, or
   one of els that does not. *)
let applies e pass then_ els = e.arena.(pass + pass_then) land then_ lor (e.arena.(pass + pass_els) land els) <> 0

let all_seen e pass (names : int array) =
  let i = ref 0 in
  while
    !i < Array.length names
    &&
    let name = names.(!i) in
    e.arena.(pass + (name lsr 5)) land (1 lsl (name land 31)) <> 0
  do
    incr i
  done;
  !i = Array.length names

let active e pass (g : gate) =
  (not g.g_set)
  ||
  let bit = 1 lsl g.g_condition in
  e.arena.(pass + pass_gate_ok) land bit <> 0 && e.arena.(pass + pass_holds) land bit <> 0 = g.polarity

let fail_alt e pass (alt : alt_branch) =
  let at = pass + pass_alt_failed + alt.group in
  e.arena.(at) <- e.arena.(at) lor (1 lsl alt.branch)

(* The outcome of a property in the first step. *)

(* The object fails. *)
let fused_failed = 1

(* Some branch covered the property. *)
let fused_cover = 2

(* Conditional applications are pending. *)
let fused_defer = 4
let count_ok (c : fused_contributor) count = (c.c_min < 0 || count >= c.c_min) && (c.c_max < 0 || count <= c.c_max)

(* holds reports whether a condition's test on a property's value holds. *)
let test_holds e (t : value_test) v =
  let d = e.d in
  if t.is_pattern then
    if kind d v = kind_string then
      match t.vt_pattern with
      | Some pattern -> Pattern.matches pattern e.regex (str_bytes d v) (str_off d v) (str_len d v) (str_ascii d v)
      | None -> false
    else not t.requires_string
  else enum_contains e t.allowed v

(* allowed is the mask of the constant tests that allow the value. *)
let merged_allowed (m : merged_tests) d v =
  if kind d v = kind_string then begin
    let i = Names.find m.m_strings (str_bytes d v) (str_off d v) (str_len d v) in
    if i >= 0 then m.string_masks.(i) else 0
  end
  else begin
    let mask = ref 0 in
    let i = ref 0 in
    while !i < Array.length m.m_others do
      let o = m.m_others.(!i) in
      if Values.values_equal d v o.mv_value.d o.mv_value.n then begin
        mask := o.mv_mask;
        i := Array.length m.m_others
      end
      else incr i
    done;
    !mask
  end

(* The node a declared property of a node applies, or no_node. *)
let property_node e id (n : Node.t) b off len =
  match e.p.property_maps.(id) with
  | Some names ->
      let i = Names.find names b off len in
      if i >= 0 then n.properties.(i).node else no_node
  | None ->
      let found = ref no_node in
      let i = ref 0 in
      while !found < 0 && !i < Array.length n.properties do
        if bytes_equal_string b off len n.properties.(!i).name then found := n.properties.(!i).node;
        incr i
      done;
      !found

(* wants reports whether a keyword result with the given outcome carries message text. *)
let wants e is_match = e.collect && Results.with_text e.c is_match

(* keyword records a keyword's result when collecting. It reports whether evaluation stops here (failing fast). *)
let keyword e is_match message name =
  if e.collect then begin
    Results.evaluated_keyword e.c is_match message name;
    false
  end
  else not is_match

(* count_keyword records a keyword whose message ends in a quoted count: the value of a count keyword of the node,
   which is named by key (the keyword that holds the value, where that is not the keyword of the row). *)
let count_keyword e is_match prefix (n : Node.t) key value name =
  if not e.collect then not is_match
  else begin
    let message = if Results.with_text e.c is_match then prefix ^ " '" ^ count_text n key value ^ "'" else "" in
    Results.evaluated_keyword e.c is_match message name;
    false
  end

(* required_property records whether a required property is present. It reports whether evaluation stops here. *)
let required_property e present name keyword =
  if not e.collect then not present
  else begin
    let message =
      if Results.with_text e.c present then
        "Required property " ^ (if present then "" else "not ") ^ "present '" ^ name ^ "'"
      else ""
    in
    Results.evaluated_keyword_for_property e.c present message name keyword;
    false
  end

(* bound records a number keyword whose message ends in the quoted bound. *)
let bound e (b : value_ref) is_match text name =
  let message = if wants e is_match then text ^ quoted (number_text b.d b.n) else "" in
  keyword e is_match message name

(* collect_pure_ref is the single reference of a pure-$ref node, for collecting (a node with annotations is not
   elided). *)
let collect_pure_ref e id =
  if id < Array.length e.annotations && Array.length e.annotations.(id) <> 0 then no_node
  else Plan.pure_ref_target e.p.nodes.(id)

(* resolve follows pure-reference hops (at most 16, and not across resources when a dynamic scope is kept),
   returning the target and the evaluation-path suffix of the hops (/$ref, /$dynamicRef, /$recursiveRef). It is for
   collecting, and allocates. *)
let resolve e id =
  let current = ref id in
  let suffix = ref "" in
  let hop = ref 0 in
  while !hop < 16 do
    let n = e.p.nodes.(!current) in
    let next = collect_pure_ref e !current in
    if next < 0 || (e.p.uses_dynamic_scope && e.p.nodes.(next).resource_id <> n.resource_id) then hop := 16
    else begin
      suffix := !suffix ^ if n.ref_ >= 0 then "/$ref" else "/" ^ n.static_dynamic_keyword;
      current := next;
      incr hop
    end
  done;
  (!current, !suffix)

let can_mark e id x =
  let n = e.p.nodes.(id) in
  if kind e.d x = kind_object then n.marks_properties else n.marks_items

(* ---------------------------------------------------------------------------------------------------------------------
   Plans *)

(* run evaluates a node's plan (at a new instance location, or where no depth guard applies). *)
let rec run e id x = enter_child e e.p.plans.(id).self x

(* run_child evaluates a child at a new instance location. A child that is only a type test the value passes is
   decided here, and anything else is one call. *)
and run_child e (c : child) x = c.pass land kind e.d x <> 0 || enter_child e c x

(* enter_child is run_child past its first test: the type test in full, then the child's keywords by its shape. *)
and enter_child e (c : child) x =
  let d = e.d in
  let kind = kind d x in
  if c.types land kind = 0 && not (integer_ok c.types d x) then false
  else if c.shape = shape_trivial then true
  else match e.p.plans.(c.id).body with None -> true | Some b -> enter_kind e c.shape b x kind

(* enter evaluates a body by its shape (its types already tested, and not on an in-place cycle unless general). *)
and enter e shape (b : body) x = enter_kind e shape b x (kind e.d x)

and enter_kind e shape (b : body) x kind =
  let d = e.d in
  if shape = shape_leaf then run_leaf e b x
  else if shape = shape_string_enum then kind = kind_string && names_has b.b_values.(0).names d x
  else if shape = shape_strings then kind <> kind_string || run_string e b.str x
  else if shape = shape_object then
    kind <> kind_object
    ||
    match b.object_ with
    | Some pl -> if pl.strict then run_strict_object e pl x else run_object e pl x
    | None -> true
  else if shape = shape_fused then
    if kind <> kind_object then run_keywords e b x else match b.fused with Some f -> run_fused e f x | None -> true
  else if shape = shape_array then kind <> kind_array || match b.array with Some pl -> run_array e pl x | None -> true
  else if shape = shape_apply then run_apply e b.apply x
  else run_body e b x

(* run_in_place evaluates an in-place child under the depth guard. *)
and run_in_place e id x =
  if not e.p.plans.(id).guard then run e id x
  else begin
    e.depth <- e.depth + 1;
    if e.depth > e.p.max_depth then begin
      e.depth_exceeded <- true;
      e.depth <- e.depth - 1;
      false
    end
    else begin
      let ok = run e id x in
      e.depth <- e.depth - 1;
      ok
    end
  end

(* run_branch evaluates an in-place child with its type check inline, then its keywords under the depth guard
   (without testing the type again). *)
and run_branch e (c : child) x =
  if c.types <> any_type && not (type_ok c.types e.d x) then false
  else if c.shape = shape_trivial then true
  else begin
    let pl = e.p.plans.(c.id) in
    match pl.body with Some b when not pl.guard -> enter e c.shape b x | _ -> run_in_place e c.id x
  end

(* run_body evaluates a node's keywords. Where the program keeps a dynamic scope, entering a node of another
   resource pushes that resource (as the general evaluator does), for the dynamic references below it. *)
and run_body e (b : body) x =
  if not e.p.uses_dynamic_scope then run_keywords e b x
  else begin
    let pushed = push_scope e e.p.nodes.(b.b_node).resource_id in
    let ok = run_keywords e b x in
    if pushed then pop_scope e;
    ok
  end

and run_keywords e (b : body) x =
  let d = e.d in
  let kind = kind d x in
  match if kind = kind_object then b.fused else None with
  | Some f -> run_fused e f x
  | None ->
      if (kind = kind_object && b.general land type_object <> 0) || (kind = kind_array && b.general land type_array <> 0)
      then eval_node e b.b_node x no_bits
      else
        run_apply e b.b_values x
        && (if kind = kind_number then Array.length b.number = 0 || run_number e b.number x
            else if kind = kind_string then Array.length b.str = 0 || run_string e b.str x
            else if kind = kind_object then match b.object_ with Some pl -> run_object e pl x | None -> true
            else if kind = kind_array then
              (match b.array with Some pl -> run_array e pl x | None -> true)
              && ((not b.has_unevaluated_items) || run_unevaluated_items e b x)
            else true)
        && (Array.length b.apply = 0 || run_apply e b.apply x)

(* The items after a static prefix, against the unevaluatedItems child. *)
and run_unevaluated_items e (b : body) x =
  let d = e.d in
  let first = first d x in
  let count = count d x in
  let i = ref b.unevaluated_from in
  while !i < count && run_child e b.unevaluated_child (first + !i) do
    incr i
  done;
  !i >= count

and run_apply e (ops : op array) x =
  let i = ref 0 in
  while !i < Array.length ops && run_op e ops.(!i) x do
    incr i
  done;
  !i = Array.length ops

and run_op e (o : op) x =
  let d = e.d in
  let k = o.kind in
  if k = op_const then Values.values_equal d x o.value.d o.value.n
  else if k = op_enum_strings then kind d x = kind_string && names_has o.names d x
  else if k = op_enum then enum_contains e o.values x
  else if k = op_ref then run_in_place e o.node x
  else if k = op_all_of then begin
    let i = ref 0 in
    while !i < Array.length o.children && run_branch e o.children.(!i) x do
      incr i
    done;
    !i = Array.length o.children
  end
  else if k = op_any_of then (
    match o.branches with
    | None -> false
    | Some branches ->
        let list = candidates e branches x in
        let i = ref 0 in
        while !i < Array.length list && not (run_branch e branches.b_children.(list.(!i)) x) do
          incr i
        done;
        !i < Array.length list)
  else if k = op_one_of then
    match o.branches with
    | None -> false
    | Some branches ->
        let list = candidates e branches x in
        (* The instance's type (or the discriminator) leaves one branch: that branch decides. *)
        if Array.length list = 1 then run_branch e branches.b_children.(list.(0)) x
        else begin
          let matched = ref 0 in
          let i = ref 0 in
          while !matched <= 1 && !i < Array.length list do
            if run_branch e branches.b_children.(list.(!i)) x then incr matched;
            incr i
          done;
          !matched = 1
        end
  else if k = op_not then
    (* Under the depth guard, like every other in-place applicator: a not can be part of a cycle too. *)
    not (run_in_place e o.node x)
  else if k = op_dynamic_ref then
    match o.dynamic with Some dynamic -> run_in_place e e.p.fast_target.(resolve_dynamic e dynamic) x | None -> true
  else begin
    let next = if run_in_place e o.node x then o.then_ else o.else_ in
    next < 0 || run_in_place e next x
  end

(* run_object evaluates an object plan: the size bounds, the property loop for its shape, then the required names
   and dependencies. *)
and run_object e (pl : object_plan) x =
  let d = e.d in
  let count = count d x in
  if count < pl.o_min || count > pl.o_max then false
  else begin
    let visit = pl.visit in
    (* The names seen, or -1 when a property failed. *)
    let seen =
      if visit = visit_names then
        if pl.lookup && count * Names.length pl.o_names <= lookup_budget then walk_lookup e pl (first d x) count
        else walk_names e pl (first d x) count
      else if visit = visit_values then if walk_values e pl x then 0 else -1
      else if visit = visit_pattern then if walk_pattern e pl x then 0 else -1
      else if visit = visit_general then walk_general e pl x
      else 0
    in
    seen >= 0 && seen land pl.required_mask = pl.required_mask && (pl.rest_free || object_rest e pl x seen)
  end

(* run_strict_object is the strict loop: bounds, declared names (additionalProperties for the rest), and the
   required mask. A small function of its own, since nested objects enter it directly. *)
and run_strict_object e (pl : object_plan) x =
  let d = e.d in
  let count = count d x in
  if count < pl.o_min || count > pl.o_max then false
  else begin
    let seen =
      if pl.lookup && count * Names.length pl.o_names <= lookup_budget then walk_lookup e pl (first d x) count
      else walk_names e pl (first d x) count
    in
    seen >= 0 && seen land pl.required_mask = pl.required_mask
  end

(* walk_lookup looks each name up in the object, whose count properties start at first (a plan with lookup: the
   other properties need no visit). It returns the names seen, or -1 when a property failed. *)
and walk_lookup e (pl : object_plan) first count =
  let d = e.d in
  let ns = pl.o_names in
  let seen = ref 0 in
  let i = ref 0 in
  let n = Names.length ns in
  while !seen >= 0 && !i < n do
    let name = Names.name ns !i in
    let length = String.length name in
    let word = Names.word ns !i in
    let j = ref 0 in
    while !j < count do
      let k = first + (2 * !j) in
      if
        str_len d k = length
        &&
        let b = str_bytes d k and off = str_off d k in
        Names.name_word b off length = word && (length <= 8 || bytes_equal_string b off length name)
      then begin
        let c = pl.o_children.(!i) in
        if c.pass land kind d (k + 1) = 0 && not (enter_child e c (k + 1)) then seen := -1
        else seen := !seen lor (1 lsl !i);
        j := count
      end
      else incr j
    done;
    incr i
  done;
  !seen

(* object_rest checks the required names checked by lookup, and the dependencies. *)
and object_rest e (pl : object_plan) x seen =
  let d = e.d in
  let ok = ref true in
  let i = ref 0 in
  while !ok && !i < Array.length pl.o_required do
    if property d x pl.o_required.(!i) < 0 then ok := false;
    incr i
  done;
  let i = ref 0 in
  while !ok && !i < Array.length pl.o_dependencies do
    let dep = pl.o_dependencies.(!i) in
    let present =
      if dep.has_bits then begin
        let present = seen land dep.name_bit <> 0 in
        if present && seen land dep.required_bits <> dep.required_bits then ok := false;
        present
      end
      else begin
        let present = property d x dep.pd_name >= 0 in
        if present then begin
          let r = ref 0 in
          while !ok && !r < Array.length dep.pd_required do
            if property d x dep.pd_required.(!r) < 0 then ok := false;
            incr r
          done
        end;
        present
      end
    in
    if !ok && present && dep.pd_schema >= 0 && not (run_in_place e dep.pd_schema x) then ok := false;
    incr i
  done;
  !ok

(* walk_values is for only additionalProperties: every value against one child. *)
and walk_values e (pl : object_plan) x =
  let d = e.d in
  let c = pl.additional in
  let first = first d x and count = count d x in
  if c.shape = shape_trivial then (
    c.types = any_type
    ||
    let i = ref 0 in
    while
      !i < count
      &&
      let v = first + (2 * !i) + 1 in
      c.types land kind d v <> 0 || integer_ok c.types d v
    do
      incr i
    done;
    !i = count)
  else begin
    let i = ref 0 in
    while !i < count && run_child e c (first + (2 * !i) + 1) do
      incr i
    done;
    !i = count
  end

(* walk_names is for declared properties, and additionalProperties for the rest, over the count properties that
   start at first. It returns the declared names seen, or -1 when a property failed. *)
and walk_names e (pl : object_plan) first count =
  let d = e.d in
  let ns = pl.o_names in
  let seen = ref 0 in
  let hint = ref 0 in
  let k = ref first in
  let stop = first + (2 * count) in
  while !seen >= 0 && !k < stop do
    let key = !k in
    let i = Names.find_from ns (str_bytes d key) (str_off d key) (str_len d key) !hint in
    if i >= 0 then begin
      hint := i + 1;
      if not (run_child e pl.o_children.(i) (key + 1)) then seen := -1
      else if i < mask_bits then seen := !seen lor (1 lsl i)
    end
    else if pl.has_additional && not (run_child e pl.additional (key + 1)) then seen := -1;
    k := key + 2
  done;
  !seen

and walk_pattern e (pl : object_plan) x =
  let d = e.d in
  let p = pl.patterns.(0) in
  let k = ref (first d x) in
  let stop = !k + (2 * count d x) in
  let ok = ref true in
  while !ok && !k < stop do
    let key = !k in
    if Pattern.matches p.pc_pattern e.regex (str_bytes d key) (str_off d key) (str_len d key) (str_ascii d key) then
      begin if not (run_child e p.pc_child (key + 1)) then ok := false
      end
    else if pl.has_additional && not (run_child e pl.additional (key + 1)) then ok := false;
    k := key + 2
  done;
  !ok

and walk_general e (pl : object_plan) x =
  let d = e.d in
  let ns = pl.o_names in
  let seen = ref 0 in
  let hint = ref 0 in
  let k = ref (first d x) in
  let stop = !k + (2 * count d x) in
  while !seen >= 0 && !k < stop do
    let key = !k in
    let b = str_bytes d key and off = str_off d key and len = str_len d key in
    let matched = ref false in
    let ok = ref true in
    let i = Names.find_from ns b off len !hint in
    if i >= 0 then begin
      hint := i + 1;
      (* A name only required (or a dependency) mentions is undeclared: patterns, else additionalProperties. *)
      if i < pl.declared then begin
        matched := true;
        if not (run_child e pl.o_children.(i) (key + 1)) then ok := false
      end;
      let list = pl.name_patterns.(i) in
      let j = ref 0 in
      while !ok && !j < Array.length list do
        matched := true;
        if not (run_child e pl.patterns.(list.(!j)).pc_child (key + 1)) then ok := false;
        incr j
      done
    end
    else begin
      let ascii = str_ascii d key in
      let j = ref 0 in
      while !ok && !j < Array.length pl.patterns do
        let p = pl.patterns.(!j) in
        if Pattern.matches p.pc_pattern e.regex b off len ascii then begin
          matched := true;
          if not (run_child e p.pc_child (key + 1)) then ok := false
        end;
        incr j
      done
    end;
    if !ok && (not !matched) && pl.has_additional && not (run_child e pl.additional (key + 1)) then ok := false;
    (* The name is a string value of the document: it is evaluated where it is. *)
    if !ok && pl.property_names >= 0 && not (run e pl.property_names key) then ok := false;
    if not !ok then seen := -1 else if i >= 0 && i < mask_bits then seen := !seen lor (1 lsl i);
    k := key + 2
  done;
  !seen

and run_array e (pl : array_plan) x =
  let d = e.d in
  let count = count d x in
  if count < pl.a_min || count > pl.a_max then false
  else begin
    let first = first d x in
    if pl.is_simple then all_of_type d first count pl.simple
    else begin
      let prefix = Int.min (Array.length pl.prefix) count in
      let i = ref 0 in
      while !i < prefix && run_child e pl.prefix.(!i) (first + !i) do
        incr i
      done;
      !i = prefix
      && ((not pl.has_items)
         ||
         let items = pl.items in
         if pl.has_nested then begin
           let item = ref (first + prefix) in
           let stop = first + count in
           while
             !item < stop
             &&
             let it = !item in
             if kind d it <> kind_array then (* Not an array: only the items' type test applies. *)
               type_ok items.types d it
             else begin
               let length = Document.count d it in
               items.types land type_array <> 0
               && length >= pl.nested_min && length <= pl.nested_max
               && all_of_type d (Document.first d it) length pl.nested_types
             end
           do
             incr item
           done;
           !item = stop
         end
         else if items.shape = shape_trivial then
           (* A type-only items schema: one tight loop, or none for true. *)
           all_of_type d (first + prefix) (count - prefix) items.types
         else begin
           let item = ref (first + prefix) in
           let stop = first + count in
           while !item < stop && run_child e items !item do
             incr item
           done;
           !item = stop
         end)
      && (pl.a_contains < 0
         ||
         let matches = ref 0 in
         let i = ref 0 in
         while !i < count do
           if run e pl.a_contains (first + !i) then begin
             incr matches;
             if pl.a_max_contains < 0 && !matches >= pl.a_min_contains then i := count
           end;
           incr i
         done;
         !matches >= pl.a_min_contains && (pl.a_max_contains < 0 || !matches <= pl.a_max_contains))
      && ((not pl.unique) || Values.all_unique d x e.unique)
    end
  end

(* ---------------------------------------------------------------------------------------------------------------------
   The fused pass (see fused.ml) *)

and apply_opt e (c : opt_child) v = (not c.oc_set) || run_child e c.oc_child v

(* resolve_unknown resolves a name no entry knows against one branch's pattern and additional properties. It
   returns 2 when it matched (the property is covered), 1 when nothing matched, and 0 when the application failed. *)
and resolve_unknown e (c : fused_contributor) b off len ascii v =
  let matched = ref false in
  let ok = ref true in
  let i = ref 0 in
  while !ok && !i < Array.length c.c_patterns do
    let p = c.c_patterns.(!i) in
    if Pattern.matches p.fp_pattern e.regex b off len ascii then begin
      matched := true;
      if not (apply_opt e p.fp_child v) then ok := false
    end;
    incr i
  done;
  if !ok && (not !matched) && c.c_has_additional then begin
    matched := true;
    if not (apply_opt e c.c_additional v) then ok := false
  end;
  if not !ok then 0 else if !matched then 2 else 1

and fused_entry e (f : fused_object) index v pass =
  let d = e.d in
  let entry = f.entries.(index) in
  (let at = pass + pass_seen + (index lsr 5) in
   e.arena.(at) <- e.arena.(at) lor (1 lsl (index land 31)));
  let tests = entry.tests in
  if Array.length tests <> 0 then begin
    let keyed = match entry.merged with Some m -> m.keyed | None -> 0 in
    let allowed = match entry.merged with Some m -> merged_allowed m d v | None -> 0 in
    for t = 0 to Array.length tests - 1 do
      let test = tests.(t) in
      let holds =
        if t < mask_bits && keyed land (1 lsl t) <> 0 then allowed land (1 lsl t) <> 0 else test_holds e test v
      in
      if not holds then e.arena.(pass + pass_failed) <- e.arena.(pass + pass_failed) lor (1 lsl test.vt_condition)
    done
  end;
  let outcome = ref 0 in
  let i = ref 0 in
  while !i < Array.length entry.apps do
    let app = entry.apps.(!i) in
    let c = f.contributors.(app.contributor) in
    if c.condition.g_set then outcome := !outcome lor fused_defer
    else if app.has_child && not (run_child e app.app_child v) then
      if not c.alt.alt_set then begin
        outcome := fused_failed;
        i := Array.length entry.apps
      end
      else fail_alt e pass c.alt
    else outcome := !outcome lor fused_cover;
    incr i
  done;
  !outcome

and fused_unknown e (f : fused_object) b off len ascii v pass =
  if not f.resolves_unknown then 0
  else begin
    for i = 0 to Array.length f.absent - 1 do
      let a = f.absent.(i) in
      let bit = 1 lsl a.fa_condition in
      if e.arena.(pass + pass_failed) land bit = 0 && Pattern.matches a.fa_pattern e.regex b off len ascii then
        e.arena.(pass + pass_failed) <- e.arena.(pass + pass_failed) lor bit
    done;
    let outcome = ref 0 in
    let i = ref 0 in
    while !i < Array.length f.contributors do
      let c = f.contributors.(!i) in
      if c.condition.g_set then
        begin if Array.length c.c_patterns <> 0 || c.c_has_additional then outcome := !outcome lor fused_defer
        end
      else begin
        let resolved = resolve_unknown e c b off len ascii v in
        if resolved = 0 then
          if not c.alt.alt_set then begin
            outcome := fused_failed;
            i := Array.length f.contributors
          end
          else fail_alt e pass c.alt
        else if resolved = 2 then outcome := !outcome lor fused_cover
      end;
      incr i
    done;
    !outcome
  end

and run_fused e (f : fused_object) x =
  match f.flat with
  | Some flat -> run_strict_object e flat x
  | None ->
      (* The state of the pass, and the sets it takes after it, are released together. *)
      let mark = e.narena in
      let pass = take e pass_words in
      Array.fill e.arena pass pass_words 0;
      let ok = run_fused_pass e f x pass in
      e.narena <- mark;
      ok

and run_fused_pass e (f : fused_object) x pass =
  let d = e.d in
  let count = count d x in
  let ok = ref true in
  if f.has_count_bounds then
    for i = 0 to Array.length f.contributors - 1 do
      let c = f.contributors.(i) in
      if (not c.condition.g_set) && (not c.alt.alt_set) && not (count_ok c count) then ok := false
    done;
  if not !ok then false
  else begin
    (* The covered properties (for unevaluatedProperties) and, beyond the first mask_bits (which a mask holds), the
       properties with conditional applications pending, by ordinal. *)
    let covered = if f.has_unevaluated then new_bits e count else no_bits in
    let deferred_beyond = if count > mask_bits then new_bits e count else no_bits in
    let deferred = ref 0 in
    let pending = ref 0 in
    let hint = ref 0 in
    let ns = f.f_names in
    let first = first d x in
    let ordinal = ref 0 in
    while !ok && !ordinal < count do
      let k = first + (2 * !ordinal) in
      let b = str_bytes d k and off = str_off d k and len = str_len d k in
      let index = Names.find_from ns b off len !hint in
      let outcome =
        if index >= 0 then begin
          hint := index + 1;
          fused_entry e f index (k + 1) pass
        end
        else fused_unknown e f b off len (str_ascii d k) (k + 1) pass
      in
      if outcome land fused_failed <> 0 then ok := false
      else begin
        if outcome land fused_cover <> 0 && covered >= 0 then set_bit e covered !ordinal;
        if outcome land fused_defer <> 0 then begin
          incr pending;
          if !ordinal < mask_bits then deferred := !deferred lor (1 lsl !ordinal)
          else set_bit e deferred_beyond !ordinal
        end
      end;
      incr ordinal
    done;
    !ok
    && begin
      (* Decide the conditions, then which apply along their gates (a gate precedes the conditions under it). *)
      for i = 0 to Array.length f.conditions - 1 do
        if e.arena.(pass + pass_failed) land (1 lsl i) = 0 && all_seen e pass f.conditions.(i).fc_required then
          e.arena.(pass + pass_holds) <- e.arena.(pass + pass_holds) lor (1 lsl i)
      done;
      for i = 0 to Array.length f.conditions - 1 do
        if active e pass f.conditions.(i).fc_gate then
          e.arena.(pass + pass_gate_ok) <- e.arena.(pass + pass_gate_ok) lor (1 lsl i)
      done;
      let gate_ok = e.arena.(pass + pass_gate_ok) and holds = e.arena.(pass + pass_holds) in
      e.arena.(pass + pass_then) <- gate_ok land holds;
      e.arena.(pass + pass_els) <- gate_ok land lnot holds;

      let ordinal = ref 0 in
      while !ok && !ordinal < count && !pending > 0 do
        let o = !ordinal in
        if if o < mask_bits then !deferred land (1 lsl o) <> 0 else get_bit e deferred_beyond o then begin
          decr pending;
          let k = first + (2 * o) in
          let b = str_bytes d k and off = str_off d k and len = str_len d k in
          let v = k + 1 in
          let cover = ref false in
          let index = Names.find f.f_names b off len in
          if index >= 0 then begin
            let apps = f.entries.(index).apps in
            let i = ref 0 in
            while !ok && !i < Array.length apps do
              (* An application whose first contributor has no condition was made in the first step. *)
              let app = apps.(!i) in
              if f.contributors.(app.contributor).condition.g_set && applies e pass app.app_then app.app_els then
                if app.has_child && not (run_child e app.app_child v) then ok := false else cover := true;
              incr i
            done
          end
          else begin
            let ascii = str_ascii d k in
            let i = ref 0 in
            while !ok && !i < Array.length f.contributors do
              let c = f.contributors.(!i) in
              if c.condition.g_set && applies e pass c.c_then c.c_els then begin
                let resolved = resolve_unknown e c b off len ascii v in
                if resolved = 0 then ok := false else if resolved = 2 then cover := true
              end;
              incr i
            done
          end;
          if !ok && !cover && covered >= 0 then set_bit e covered o
        end;
        incr ordinal
      done;
      !ok
    end
    && begin
      let i = ref 0 in
      while !ok && !i < Array.length f.finals do
        let c = f.contributors.(f.finals.(!i)) in
        if (not c.condition.g_set) || applies e pass c.c_then c.c_els then
          if c.condition.g_set && not (count_ok c count) then ok := false
          else if (c.alt.alt_set && not (count_ok c count)) || not (all_seen e pass c.c_required) then
            if not c.alt.alt_set then ok := false else fail_alt e pass c.alt;
        incr i
      done;
      !ok
    end
    && begin
      let g = ref 0 in
      while !ok && !g < Array.length f.alt_groups do
        let group = f.alt_groups.(!g) in
        let all = (1 lsl group.count) - 1 in
        let survivors = lnot e.arena.(pass + pass_alt_failed + !g) land all in
        (* None, or for exactly one, any number but one. *)
        if survivors = 0 || (group.exactly_one && survivors land (survivors - 1) <> 0) then ok := false;
        incr g
      done;
      !ok
    end
    && begin
      let i = ref 0 in
      while !ok && !i < Array.length f.forbidden do
        let fb = f.forbidden.(!i) in
        if active e pass fb.ff_gate && all_seen e pass fb.ff_names then ok := false;
        incr i
      done;
      !ok
    end
    && begin
      let i = ref 0 in
      while !ok && !i < Array.length f.alternatives do
        let a = f.alternatives.(!i) in
        if active e pass a.al_condition then begin
          let matches = ref 0 in
          for j = 0 to Array.length a.al_branches - 1 do
            if all_seen e pass a.al_branches.(j) then incr matches
          done;
          if !matches = 0 || (a.al_exactly_one && !matches <> 1) then ok := false
        end;
        incr i
      done;
      !ok
    end
    && ((not f.has_unevaluated)
       ||
       let ordinal = ref 0 in
       while
         !ordinal < count && (get_bit e covered !ordinal || run_child e f.unevaluated (first + (2 * !ordinal) + 1))
       do
         incr ordinal
       done;
       !ordinal = count)
  end

(* ---------------------------------------------------------------------------------------------------------------------
   The general evaluator *)

and eval_node e id x bits =
  let n = e.p.nodes.(id) in
  if n.always_true || n.always_false then begin
    if e.collect then Results.evaluated_boolean_schema e.c n.always_true;
    n.always_true
  end
  else begin
    let pushed = e.p.uses_dynamic_scope && push_scope e n.resource_id in
    let kind = kind e.d x in
    let ok =
      if
        bits < 0
        && ((n.unevaluated_properties >= 0 && kind = kind_object) || (n.unevaluated_items >= 0 && kind = kind_array))
      then begin
        let own = new_bits e (count e.d x) in
        let ok = eval_core e id n x own in
        free_bits e own;
        ok
      end
      else eval_core e id n x bits
    in
    if pushed then pop_scope e;
    ok
  end

and eval_core e id (n : Node.t) x bits =
  let d = e.d in
  let collect = e.collect in
  let ok = ref true in
  (* Whether evaluation goes on: it stops at the first failure when nothing is collected. *)
  let go = ref true in
  if n.has_type then begin
    let m = type_ok n.type_mask d x in
    let message = if wants e m then type_message n.type_mask else "" in
    if keyword e m message "type" then go := false;
    ok := !ok && m
  end;
  if !go && is_set n.const_value then begin
    let m = Values.values_equal d x n.const_value.d n.const_value.n in
    let message = if wants e m then const_message n.const_value else "" in
    if keyword e m message "const" then go := false;
    ok := !ok && m
  end;
  if !go && n.has_enum then begin
    let m = enum_contains e n.enum_values x in
    if keyword e m (if m then msg_matched_at_least_one else msg_did_not_match_at_least_one) "enum" then go := false;
    ok := !ok && m
  end;
  if not !go then false
  else begin
    let kind = kind d x in
    let m =
      if kind = kind_number && has_number_keywords n then eval_number e id n x
      else if kind = kind_string && has_string_keywords n then eval_string e id n x
      else if kind = kind_object && has_object_keywords n then eval_object e id n x bits
      else if kind = kind_array && has_array_keywords n then eval_array e n x bits
      else true
    in
    if (not m) && not collect then false
    else begin
      ok := !ok && m;
      let m = eval_in_place e n x bits in
      if (not m) && not collect then false
      else begin
        ok := !ok && m;
        let m =
          if kind = kind_object && n.unevaluated_properties >= 0 then eval_unevaluated_properties e n x bits
          else if kind = kind_array && n.unevaluated_items >= 0 then eval_unevaluated_items e n x bits
          else true
        in
        if (not m) && not collect then false
        else begin
          ok := !ok && m;
          if collect then
            Array.iter
              (fun (a : annotation_entry) ->
                if not (a.strings_only && kind <> kind_string) then Results.ignored_keyword e.c a.value a.keyword)
              e.annotations.(id);
          !ok
        end
      end
    end
  end

(* -- Numbers and strings *)

and eval_number e id (n : Node.t) x =
  let d = e.d in
  let ok = ref true in
  let go = ref true in
  if n.assert_format && Formats.is_numeric n.format_kind && n.has_format then begin
    let m =
      match e.p.custom_formats.(id) with
      | Some custom -> custom (number_text d x)
      | None -> Number_formats.check n.format_kind d x
    in
    let message =
      if wants e m then
        "The value was expected to be in a supported format, and within bounds for '" ^ Formats.name n.format_kind ^ "'"
      else ""
    in
    if keyword e m message "format" then go := false;
    ok := !ok && m
  end;
  if !go && is_set n.minimum then begin
    let m = compare_numbers d x n.minimum.d n.minimum.n >= 0 in
    if bound e n.minimum m "The value was expected to be greater than or equal to" "minimum" then go := false;
    ok := !ok && m
  end;
  if !go && is_set n.maximum then begin
    let m = compare_numbers d x n.maximum.d n.maximum.n <= 0 in
    if bound e n.maximum m "The value was expected to be less than or equal to" "maximum" then go := false;
    ok := !ok && m
  end;
  if !go && is_set n.exclusive_minimum then begin
    let m = compare_numbers d x n.exclusive_minimum.d n.exclusive_minimum.n > 0 in
    if bound e n.exclusive_minimum m "The value was expected to be greater than" "exclusiveMinimum" then go := false;
    ok := !ok && m
  end;
  if !go && is_set n.exclusive_maximum then begin
    let m = compare_numbers d x n.exclusive_maximum.d n.exclusive_maximum.n < 0 in
    if bound e n.exclusive_maximum m "The value was expected to be less than" "exclusiveMaximum" then go := false;
    ok := !ok && m
  end;
  if !go && is_set n.multiple_of then begin
    let m =
      match n.divisor with
      | Some divisor -> Numbers.divides divisor (flags d x) (data d x) (source d) (number_start d x)
      | None -> true
    in
    if bound e n.multiple_of m "The value was expected to be a multiple of" "multipleOf" then go := false;
    ok := !ok && m
  end;
  !go && !ok

and eval_string e id (n : Node.t) x =
  let d = e.d in
  let ok = ref true in
  let go = ref true in
  if n.min_length >= 0 || n.max_length >= 0 then begin
    let length = code_points d x in
    if n.min_length >= 0 then begin
      let m = length >= n.min_length in
      if
        count_keyword e m "Expected the length of the value to be greater than or equal to" n "minLength" n.min_length
          "minLength"
      then go := false;
      ok := !ok && m
    end;
    if !go && n.max_length >= 0 then begin
      let m = length <= n.max_length in
      if
        count_keyword e m "Expected the length of the value to be less than or equal to" n "maxLength" n.max_length
          "maxLength"
      then go := false;
      ok := !ok && m
    end
  end;
  (if !go then
     match n.pattern with
     | Some p ->
         let m = Pattern.matches p e.regex (str_bytes d x) (str_off d x) (str_len d x) (str_ascii d x) in
         let message =
           if wants e m then "Expected the value to match the regular expression" ^ quoted (Pattern.source p) else ""
         in
         if keyword e m message "pattern" then go := false;
         ok := !ok && m
     | None -> ());
  if !go && n.assert_format && (not (Formats.is_numeric n.format_kind)) && n.has_format then begin
    let custom = e.p.custom_formats.(id) in
    let m =
      match custom with
      | Some custom -> custom (Document.string_value d x)
      | None ->
          n.format_kind = Formats.unknown
          || Formats.check_string n.format_kind (str_bytes d x) (str_off d x) (str_len d x) (Dialect.rank n.dialect <= 1)
    in
    let message =
      match custom with
      | Some _ -> if wants e m then "Expected a string in the '" ^ n.format ^ "' format." else ""
      | None -> if n.format_kind <> Formats.unknown then Formats.message n.format_kind else ""
    in
    if keyword e m message "format" then go := false;
    ok := !ok && m
  end;
  if !go && n.assert_content then begin
    let m = content_ok e (str_bytes d x) (str_off d x) (str_len d x) n.content in
    let message =
      if n.content = content_base64 then "Expected a valid Base64-encoded string."
      else if n.content = content_json then "Expected valid JSON content."
      else "Expected valid Base64-encoded JSON content."
    in
    let name = if n.content = content_base64 then "contentEncoding" else "contentMediaType" in
    if keyword e m message name then go := false;
    ok := !ok && m
  end;
  !go && !ok

(* -- Objects *)

(* eval_at applies a child at a new instance location (a property value or an array item), when collecting. *)
and eval_at e child path value doc_segment =
  let target, suffix = resolve e child in
  Results.begin_child_context e.c true (path ^ suffix) e.p.nodes.(target).pointer true doc_segment;
  let ok = eval_node e target value no_bits in
  Results.commit_child_context e.c ok ok msg_evaluated_subschema;
  ok

(* apply_at applies a child to the value of the property at an index of the object. *)
and apply_at e bits i v child keyword entry segment =
  if bits >= 0 then set_bit e bits i;
  if not e.collect then run e e.p.fast_target.(child) v
  else begin
    let path = if entry <> "" || keyword <> "additionalProperties" then keyword ^ "/" ^ entry else keyword in
    eval_at e child path v segment
  end

and eval_object e id (n : Node.t) x bits =
  let d = e.d in
  let collect = e.collect in
  let ok = ref true in
  let go = ref true in
  let count = count d x in
  if n.min_properties >= 0 then begin
    let m = count >= n.min_properties in
    if
      count_keyword e m "Expected the property count to be greater than or equal to" n "minProperties" n.min_properties
        "minProperties"
    then go := false;
    ok := !ok && m
  end;
  if !go && n.max_properties >= 0 then begin
    let m = count <= n.max_properties in
    if
      count_keyword e m "Expected the property count to be less than or equal to" n "maxProperties" n.max_properties
        "maxProperties"
    then go := false;
    ok := !ok && m
  end;
  let first = first d x in
  if !go && (n.has_properties || n.has_pattern_properties || n.additional_properties >= 0 || n.property_names >= 0) then begin
    let i = ref 0 in
    while !go && !i < count do
      let k = first + (2 * !i) in
      let v = k + 1 in
      let b = str_bytes d k and off = str_off d k and len = str_len d k in
      let matched = ref false in
      let segment = if collect then Uri.escape_pointer_token (Document.string_value d k) else "" in
      let p = property_node e id n b off len in
      if p >= 0 then begin
        matched := true;
        if not (apply_at e bits !i v p "properties" segment segment) then if collect then ok := false else go := false
      end;
      let j = ref 0 in
      while !go && !j < Array.length n.pattern_properties do
        let pp = n.pattern_properties.(!j) in
        if Pattern.matches pp.pp_pattern e.regex b off len (str_ascii d k) then begin
          matched := true;
          let entry = if collect then Uri.escape_pointer_token (Pattern.source pp.pp_pattern) else "" in
          if not (apply_at e bits !i v pp.pp_node "patternProperties" entry segment) then
            if collect then ok := false else go := false
        end;
        incr j
      done;
      if !go && n.additional_properties >= 0 && not !matched then
        if not (apply_at e bits !i v n.additional_properties "additionalProperties" "" segment) then
          if collect then ok := false else go := false;
      let pn = n.property_names in
      if !go && pn >= 0 then
        (* The name is a string value of the document: it is evaluated where it is. *)
        if collect then begin
          (* Not elided. The document path stays the object's. *)
          Results.begin_child_context e.c true "propertyNames" e.p.nodes.(pn).pointer false "";
          let m = eval_node e pn k no_bits in
          Results.commit_child_context e.c m m msg_evaluated_subschema;
          if not m then begin
            Results.evaluated_keyword e.c false msg_property_name_failed "propertyNames";
            ok := false
          end
        end
        else if not (run e e.p.fast_target.(pn) k) then go := false;
      incr i
    done
  end;
  let i = ref 0 in
  while !go && !i < Array.length n.required_list do
    let r = n.required_list.(!i) in
    let present = property d x r >= 0 in
    if required_property e present r "required" then go := false;
    ok := !ok && present;
    incr i
  done;
  (* Rows are reported under the keyword the schema used (dependencies, dependentRequired, dependentSchemas). *)
  let i = ref 0 in
  while !go && !i < Array.length n.dependencies do
    let dep = n.dependencies.(!i) in
    if property d x dep.dep_name >= 0 then begin
      let r = ref 0 in
      while !go && !r < Array.length dep.dep_required do
        let name = dep.dep_required.(!r) in
        let present = property d x name >= 0 in
        if required_property e present name dep.dep_keyword then go := false;
        ok := !ok && present;
        incr r
      done;
      if !go && dep.schema >= 0 then begin
        let path = if collect then dep.dep_keyword ^ "/" ^ Uri.escape_pointer_token dep.dep_name else "" in
        let m = eval_in_place_child e dep.schema path x bits true true in
        if not collect then
          begin if not m then go := false
          end
        else begin
          let message =
            if Results.with_text e.c m then
              "The value did match the schema applied because it contained the property '" ^ dep.dep_name ^ "'"
            else ""
          in
          Results.evaluated_keyword_for_property e.c m message dep.dep_name dep.dep_keyword;
          ok := !ok && m
        end
      end
    end;
    incr i
  done;
  !go && !ok

and eval_unevaluated_properties e (n : Node.t) x bits =
  let d = e.d in
  let child = n.unevaluated_properties in
  let ok = ref true in
  let go = ref true in
  let first = first d x in
  let i = ref 0 in
  let count = count d x in
  while !go && !i < count do
    if not (get_bit e bits !i) then begin
      set_bit e bits !i;
      let v = first + (2 * !i) + 1 in
      if not e.collect then
        begin if not (run e e.p.fast_target.(child) v) then go := false
        end
      else if
        not
          (eval_at e child "unevaluatedProperties" v
             (Uri.escape_pointer_token (Document.string_value d (first + (2 * !i)))))
      then ok := false
    end;
    incr i
  done;
  if e.collect then Results.evaluated_keyword e.c !ok "" "unevaluatedProperties";
  !go && !ok

(* -- Arrays *)

and eval_array e (n : Node.t) x bits =
  let d = e.d in
  let collect = e.collect in
  let ok = ref true in
  let go = ref true in
  let count = count d x in
  if n.min_items >= 0 then begin
    let m = count >= n.min_items in
    if count_keyword e m "Expected the item count to be greater than or equal to" n "minItems" n.min_items "minItems"
    then go := false;
    ok := !ok && m
  end;
  if !go && n.max_items >= 0 then begin
    let m = count <= n.max_items in
    if count_keyword e m "Expected the item count to be less than or equal to" n "maxItems" n.max_items "maxItems" then
      go := false;
    ok := !ok && m
  end;
  if (not !go) || ((not n.has_prefix_items) && n.items < 0 && n.contains < 0 && not n.unique_items) then !go && !ok
  else begin
    let matches = ref 0 in
    let first = first d x in
    let i = ref 0 in
    while !go && !i < count do
      let item = first + !i in
      let child =
        if !i < Array.length n.prefix_items then n.prefix_items.(!i) else if n.items >= 0 then n.items else no_node
      in
      if child >= 0 then begin
        if bits >= 0 then set_bit e bits !i;
        if not collect then
          begin if not (run e e.p.fast_target.(child) item) then go := false
          end
        else begin
          let path =
            if !i < Array.length n.prefix_items then n.prefix_keyword ^ "/" ^ string_of_int !i else n.items_keyword
          in
          if not (eval_at e child path item (string_of_int !i)) then ok := false
        end
      end;
      if !go && n.contains >= 0 then begin
        let matched =
          if collect then begin
            let target, suffix = resolve e n.contains in
            Results.begin_child_context e.c true ("contains" ^ suffix) e.p.nodes.(target).pointer true
              (string_of_int !i);
            let matched = eval_node e target item no_bits in
            if matched then Results.commit_child_context e.c true true msg_evaluated_subschema
            else Results.pop_child_context e.c;
            matched
          end
          else run e e.p.fast_target.(n.contains) item
        in
        if matched then begin
          incr matches;
          if n.contains_marks_evaluated && bits >= 0 then set_bit e bits !i
        end
      end;
      incr i
    done;
    if !go && n.unique_items then begin
      let m = Values.all_unique d x e.unique in
      if keyword e m msg_unique_items "uniqueItems" then go := false;
      ok := !ok && m
    end;
    if !go && n.contains >= 0 then begin
      let over = n.max_contains >= 0 && !matches > n.max_contains in
      let m = !matches >= n.min_contains && not over in
      let stop =
        if over then
          count_keyword e m "Expected the contains count to be less than or equal to" n "maxContains" n.max_contains
            "contains"
        else
          count_keyword e m "Expected the contains count to be greater than or equal to" n "minContains" n.min_contains
            "contains"
      in
      if stop then go := false;
      ok := !ok && m
    end;
    !go && !ok
  end

and eval_unevaluated_items e (n : Node.t) x bits =
  let d = e.d in
  let child = n.unevaluated_items in
  let ok = ref true in
  let go = ref true in
  let first = first d x in
  let i = ref 0 in
  let count = count d x in
  while !go && !i < count do
    if not (get_bit e bits !i) then begin
      set_bit e bits !i;
      if not e.collect then
        begin if not (run e e.p.fast_target.(child) (first + !i)) then go := false
        end
      else if not (eval_at e child "unevaluatedItems" (first + !i) (string_of_int !i)) then ok := false
    end;
    incr i
  done;
  if e.collect then Results.evaluated_keyword e.c !ok "" "unevaluatedItems";
  !go && !ok

(* -- In-place applicators *)

(* eval_in_place_child evaluates an in-place child: a new context at the same instance location, on a fresh scratch
   set of evaluated properties or items merged into the parent's on success. A failing child is committed or
   popped. *)
and eval_in_place_child e child path x bits commit_on_failure elide =
  let target = if not elide then child else if e.collect then fst (resolve e child) else e.p.fast_target.(child) in
  let scratch = if bits >= 0 && can_mark e child x then new_bits e (count e.d x) else no_bits in
  let guarded = e.p.nodes.(target).in_place_cycle in
  if guarded then e.depth <- e.depth + 1;
  if guarded && e.depth > e.p.max_depth then begin
    e.depth_exceeded <- true;
    e.depth <- e.depth - 1;
    if scratch >= 0 then free_bits e scratch;
    false
  end
  else begin
    let ok =
      if e.collect then begin
        let suffix = if elide then snd (resolve e child) else "" in
        Results.begin_child_context e.c true (path ^ suffix) e.p.nodes.(target).pointer false "";
        let ok = eval_node e target x scratch in
        if ok || commit_on_failure then Results.commit_child_context e.c ok ok msg_evaluated_subschema
        else Results.pop_child_context e.c;
        ok
      end
      else if scratch >= 0 then eval_node e target x scratch
      else run e target x
    in
    if guarded then e.depth <- e.depth - 1;
    if scratch >= 0 then begin
      if ok then merge_bits e bits scratch;
      free_bits e scratch
    end;
    ok
  end

and eval_in_place e (n : Node.t) x bits =
  let collect = e.collect in
  let ok = ref true in
  let go = ref true in
  if n.ref_ >= 0 then begin
    let m = eval_in_place_child e n.ref_ "$ref" x bits true true in
    if keyword e m (if m then msg_matched_all else msg_did_not_match_all) "$ref" then go := false;
    ok := !ok && m
  end;
  if !go && n.static_dynamic_ref >= 0 then begin
    let name = n.static_dynamic_keyword in
    let m = eval_in_place_child e n.static_dynamic_ref name x bits true true in
    if keyword e m (if m then msg_matched_all else msg_did_not_match_all) name then go := false;
    ok := !ok && m
  end;
  (if !go then
     match n.dynamic_ref with
     | Some dr ->
         let name = Compiler.dynamic_keyword dr.is_recursive in
         (* The resolved target is elided, with no hops in the path. *)
         let target = resolve_dynamic e dr in
         let target = if collect then fst (resolve e target) else e.p.fast_target.(target) in
         let m = eval_in_place_child e target name x bits true false in
         if keyword e m (if m then msg_matched_all else msg_did_not_match_all) name then go := false;
         ok := !ok && m
     | None -> ());
  if !go && n.has_all_of then begin
    let all = ref true in
    let i = ref 0 in
    while !go && !i < Array.length n.all_of do
      let path = if collect then "allOf/" ^ string_of_int !i else "" in
      if not (eval_in_place_child e n.all_of.(!i) path x bits true true) then
        if collect then all := false else go := false;
      incr i
    done;
    if !go then begin
      if keyword e !all (if !all then msg_matched_all else msg_did_not_match_all) "allOf" then go := false;
      ok := !ok && !all
    end
  end;
  if !go && n.has_any_of then begin
    let any = ref false in
    (* Every branch runs when results are collected or evaluated properties or items are tracked. *)
    let exhaustive = collect || bits >= 0 in
    (* Fail-fast evaluation only tries the branches a discriminator property can select. *)
    let subset = if exhaustive then all_branches else select_branches e n.any_of_discriminator x in
    let all = subset == all_branches in
    let count = if all then Array.length n.any_of else Array.length subset in
    let j = ref 0 in
    while !j < count do
      let i = if all then !j else subset.(!j) in
      let path = if collect then "anyOf/" ^ string_of_int i else "" in
      if eval_in_place_child e n.any_of.(i) path x bits false true then begin
        any := true;
        if not exhaustive then j := count
      end;
      incr j
    done;
    if keyword e !any (if !any then msg_matched_at_least_one else msg_did_not_match_at_least_one) "anyOf" then
      go := false;
    ok := !ok && !any
  end;
  if !go && n.has_one_of then begin
    let matched = ref 0 in
    let track = bits >= 0 in
    (* Evaluated properties or items are merged only when exactly one branch matched, so they are collected aside,
       and the matching branch's kept. *)
    let only = if track then new_bits e (count e.d x) else no_bits in
    let aside = if track then new_bits e (count e.d x) else no_bits in
    (* Branches a discriminator rules out cannot match, so fail-fast evaluation skips them. *)
    let subset = if collect || track then all_branches else select_branches e n.one_of_discriminator x in
    let all = subset == all_branches in
    let count = if all then Array.length n.one_of else Array.length subset in
    let j = ref 0 in
    while !j < count do
      let i = if all then !j else subset.(!j) in
      let path = if collect then "oneOf/" ^ string_of_int i else "" in
      if track then clear_bits e aside;
      if eval_in_place_child e n.one_of.(i) path x aside false true then begin
        incr matched;
        if track then copy_bits e only aside;
        if (not collect) && !matched > 1 then j := count
      end;
      incr j
    done;
    if track then begin
      if !matched = 1 then merge_bits e bits only;
      free_bits e only
    end;
    let message =
      if !matched = 0 then msg_matched_no_schema
      else if !matched = 1 then msg_matched_exactly_one
      else msg_matched_more_than_one
    in
    if keyword e (!matched = 1) message "oneOf" then go := false;
    ok := !ok && !matched = 1
  end;
  if !go && n.not_ >= 0 then begin
    (* Not elided, never contributes results or evaluated properties or items. A not on an in-place cycle is under
       the depth guard, like every other in-place applicator. *)
    let inner =
      if collect then begin
        let guarded = e.p.nodes.(n.not_).in_place_cycle in
        if guarded then e.depth <- e.depth + 1;
        let inner =
          if guarded && e.depth > e.p.max_depth then begin
            e.depth_exceeded <- true;
            false
          end
          else begin
            Results.begin_child_context e.c true "not" e.p.nodes.(n.not_).pointer false "";
            let inner = eval_node e n.not_ x no_bits in
            Results.pop_child_context e.c;
            inner
          end
        in
        if guarded then e.depth <- e.depth - 1;
        inner
      end
      else run_in_place e e.p.fast_target.(n.not_) x
    in
    if keyword e (not inner) (if inner then msg_matched_not else msg_did_not_match_not) "not" then go := false;
    ok := !ok && not inner
  end;
  if !go && n.if_ >= 0 then begin
    let cond = eval_in_place_child e n.if_ "if" x bits false true in
    if collect then
      Results.evaluated_keyword e.c true (if cond then msg_matched_if_for_then else msg_matched_if_for_else) "if";
    if cond then
      begin if n.then_ >= 0 then begin
        let m = eval_in_place_child e n.then_ "then" x bits true true in
        if keyword e m (if m then msg_matched_then else msg_did_not_match_then) "then" then go := false;
        ok := !ok && m
      end
      end
    else if n.else_ >= 0 then begin
      let m = eval_in_place_child e n.else_ "else" x bits true true in
      if keyword e m (if m then msg_matched_else else msg_did_not_match_else) "else" then go := false;
      ok := !ok && m
    end
  end;
  !go && !ok

(* ---------------------------------------------------------------------------------------------------------------------
   Entry points *)

(* start readies an evaluator for an evaluation of a document by a program. *)
let start e p d =
  e.p <- p;
  e.d <- d;
  (* Every match of this evaluation has the limit of the program. *)
  Pattern_engine.set_limit e.regex p.pattern_match_limit;
  e.collect <- false;
  e.depth <- 0;
  e.depth_exceeded <- false;
  e.nscope <- 0;
  e.narena <- 0

(* finish lets go of the program and the instance, which the caller owns. *)
let finish e =
  e.p <- no_program;
  e.d <- no_document;
  e.collect <- false;
  e.c <- e.idle;
  e.annotations <- [||]

(* validate evaluates the program's entry, failing fast. An evaluation that recursed in place beyond the maximum
   depth is not valid, whatever it came to: the branch that was abandoned counts as false, which a not above it
   turns into true. The caller reads depth_exceeded to tell the two apart. *)
let validate e p d =
  start e p d;
  let ok = run e p.entry (root d) in
  let ok = ok && not e.depth_exceeded in
  finish e;
  ok

(* evaluate evaluates the program's entry, reporting to the collector. *)
let evaluate e p d (collector : Results.t) =
  start e p d;
  e.collect <- true;
  e.c <- collector;
  e.annotations <- Plan.node_annotations p;
  (* A root that is nothing but a $ref reports against its target, with no $ref in the evaluation path. *)
  let root_node, _ = resolve e p.root in
  Results.begin_child_context collector false "" p.nodes.(root_node).pointer false "";
  let ok = eval_node e root_node (root d) no_bits in
  Results.commit_child_context collector false ok msg_evaluated_subschema;
  finish e;
  ok
