(* The compiled program an evaluator runs: the node graph and the fail-fast plans. The types of plan.go, fused.go and
   eval.go of the Go module, gathered here because the plans, the fused object plans and the program refer to each
   other. plan.ml and fused.ml build them, and eval.ml runs them.

   Fail-fast evaluation plans: each node compiled to only the keywords it has, as a flat list of operations, with
   the object keywords fused into one pass over the instance's properties (declared names resolved through a lookup
   table, required checked as a bit mask of the declared properties seen) and children that are nothing but a type
   check tested inline.

   A node that tracks evaluated properties or items (unevaluatedProperties, unevaluatedItems) runs through the
   general evaluator, whose children come back to their plans.

   Go's masks are 64 bits and an OCaml [int] is 63, so every mask here holds at most [mask_bits] members: the names
   of an object plan that a mask of seen names covers, the conditions and the branches of a fused plan, the tests
   of one property, and the properties whose pending applications a mask remembers. A set that would need more
   takes the path Go takes beyond 64. *)

open Node

(* The most members of a mask (Go has 64). *)
let mask_bits = 62

(* any_type is every JSON type (a node without type). *)
let any_type = 0x7f

(* The shape of a node's plan, as its callers enter it. *)

(* shape_general is anything else, or on an in-place cycle (entered through the guarded path). *)
let shape_general = 0

(* shape_trivial has nothing to check beyond the types. *)
let shape_trivial = 1

(* shape_leaf checks only its own value (const, enum, number and string keywords): no children, no calls. *)
let shape_leaf = 2

(* shape_string_enum is only an enum of strings. *)
let shape_string_enum = 3

(* shape_strings is only string keywords (length, pattern, format). *)
let shape_strings = 4

(* shape_object is nothing but an object plan: an object value enters its loop directly. *)
let shape_object = 5

(* shape_array is nothing but an array plan: an array value enters its loop directly. *)
let shape_array = 6

(* shape_apply is nothing but in-place applicators: straight to them. *)
let shape_apply = 7

(* shape_fused has a fused object plan (see fused.ml): an object value goes to it directly, and a value of any other
   kind to the node's keywords. *)
let shape_fused = 8

(* A child is a child application, with its type check hoisted so that a child that is only a type check needs no
   call. The compilation of the plans sets the fields once every plan is known. Nothing writes to them afterwards. *)
type child = {
  id : int;
  (* The types the child accepts (0 for false). *)
  mutable types : int;
  (* What the child's keywords come to, so that entering it skips the dispatch it does not need. *)
  mutable shape : int;
  (* For a child that is only a type test (shape_trivial), the types again, and 0 for any other child: a value whose
     kind is in pass needs nothing more, which is one test where the child is applied. *)
  mutable pass : int;
}

(* set_shape sets a child's types and shape together, with what follows from them. *)
let set_shape c types shape =
  c.types <- types;
  c.shape <- shape;
  c.pass <- (if shape = shape_trivial then types else 0)

(* no_child is a child that accepts anything and is never entered (an undeclared name without
   additionalProperties). It is never written to: set_shape is only applied to a child with a node. *)
let no_child = { id = no_node; types = any_type; shape = shape_trivial; pass = any_type }
let number_minimum = 0
let number_maximum = 1
let number_exclusive_minimum = 2
let number_exclusive_maximum = 3
let number_multiple_of = 4
let number_format = 5

type format_check = { custom : (string -> bool) option; fc_kind : Formats.kind; legacy : bool }

let no_format = { custom = None; fc_kind = Formats.unknown; legacy = false }

type number_op = {
  n_kind : int;
  (* The bound's representation: its flags, its data word and the floats of its document. *)
  n_flag : int;
  n_data : int;
  n_floats : Float.Array.t;
  n_divisor : Numbers.divisor option;
  n_format : format_check;
}

let string_length = 0
let string_pattern = 1
let string_format = 2
let string_content = 3

type string_op = {
  s_kind : int;
  s_min : int;
  s_max : int;
  s_pattern : Pattern.t option;
  s_format : format_check;
  s_content : int;
}

let op_const = 0

(* op_enum_strings is an enum of strings only. *)
let op_enum_strings = 1
let op_enum = 2
let op_ref = 3
let op_all_of = 4
let op_any_of = 5
let op_one_of = 6
let op_not = 7

(* op_dynamic_ref is a $dynamicRef/$recursiveRef resolved against the dynamic scope at run time. *)
let op_dynamic_ref = 8
let op_if = 9

(* A discriminator's known values by lookup: string values through a name table, the few others (numbers, booleans,
   null) by scan. *)
type discriminator_index = {
  strings : Names.t;
  (* For each string in strings, its entry in the discriminator's known values. *)
  string_entries : int array;
  others : int array;
}

(* Branches are anyOf/oneOf branches, dispatched by the instance's type: only the branches whose type admits it are
   tried, and a branch that is only a type check is decided without a call. *)
type branches = {
  b_children : child array;
  (* For each instance kind (by the position of its bit), the branches that can accept it. *)
  by_kind : int array array;
  (* The discriminator, and its known values by lookup. *)
  discriminator : discriminator option;
  index : discriminator_index option;
}

type op = {
  kind : int;
  value : value_ref;
  names : Names.t option;
  values : value_ref array;
  node : int;
  children : child array;
  branches : branches option;
  dynamic : dynamic_ref_target option;
  then_ : int;
  else_ : int;
}

let new_op kind =
  {
    kind;
    value = no_value;
    names = None;
    values = [||];
    node = no_node;
    children = [||];
    branches = None;
    dynamic = None;
    then_ = no_node;
    else_ = no_node;
  }

type pattern_child = { pc_pattern : Pattern.t; pc_child : child }

type plan_dependency = {
  pd_name : string;
  pd_required : string array;
  pd_schema : int;
  (* The seen bits of the name and of the names it requires, when every one is a known name. *)
  has_bits : bool;
  name_bit : int;
  required_bits : int;
}

(* Plans with at most this many names may look them up in the instance instead of visiting its properties... *)
let lookup_names = 4

(* ...when the instance's properties times the names are at most this (each lookup scans the properties). *)
let lookup_budget = 24

(* The property loop, specialised by which keywords apply. *)

(* visit_none: no keyword looks at the properties. *)
let visit_none = 0

(* visit_values: only additionalProperties (a map): every value against one child. *)
let visit_values = 1

(* visit_names: declared properties, and additionalProperties for the rest. *)
let visit_names = 2

(* visit_pattern: one patternProperties entry and nothing declared: each name against the pattern, else
   additionalProperties. *)
let visit_pattern = 3

(* visit_general: anything else (patternProperties, propertyNames). *)
let visit_general = 4

type object_plan = {
  mutable o_min : int;
  mutable o_max : int;
  (* How the properties are visited (for properties, patternProperties, additionalProperties, propertyNames). *)
  visit : int;
  (* The declared names, then the names only required and dependencies mention (so that the pass sees them too):
     the first declared have a schema. *)
  o_names : Names.t;
  declared : int;
  (* Per name: the declared schema, or for the others the additionalProperties child (or nothing, no_child). *)
  o_children : child array;
  (* Bit i set: name i is required (the names checked by the mask: at most mask_bits names in all). *)
  mutable required_mask : int;
  (* Required names checked by lookup (when the mask cannot cover them). *)
  o_required : string array;
  patterns : pattern_child array;
  (* For each declared name, the patterns (indexes into patterns) it matches, worked out at compile time: only
     undeclared names are tested against the patterns at run time. *)
  name_patterns : int array array;
  has_additional : bool;
  additional : child;
  property_names : int;
  o_dependencies : plan_dependency array;
  (* No required names by lookup and no dependencies: nothing after the property loop but the required mask. *)
  rest_free : bool;
  (* visit_names and rest_free: the strict loop decides it. *)
  strict : bool;
  (* visit_names with at most lookup_names names and no additionalProperties: undeclared properties need no visit,
     so a small object is decided by looking each name up in it (see visit_lookup). *)
  lookup : bool;
}

type array_plan = {
  a_min : int;
  a_max : int;
  prefix : child array;
  has_items : bool;
  items : child;
  (* contains: the node, and the bounds on the count. *)
  a_contains : int;
  a_min_contains : int;
  a_max_contains : int;
  unique : bool;
  (* Bounds and type-only items, nothing else: the items' type mask (any_type: no test). *)
  mutable is_simple : bool;
  mutable simple : int;
  (* Items that are themselves simple arrays (GeoJSON's positions): their bounds and item types, checked inline. *)
  mutable has_nested : bool;
  mutable nested_min : int;
  mutable nested_max : int;
  mutable nested_types : int;
}

(* ---------------------------------------------------------------------------------------------------------------------
   The fused object plan (see fused.ml). *)

let max_fused_names = 256
let max_fused_conditions = mask_bits
let max_fused_contributors = 64
let max_fused_alt_groups = 8

(* A gate is a condition and the polarity under which something applies. Without g_set, it always applies. *)
type gate = { g_set : bool; g_condition : int; polarity : bool }

let no_gate = { g_set = false; g_condition = 0; polarity = false }

type fused_forbidden = { ff_gate : gate; ff_names : int array }
type fused_absent = { fa_condition : int; fa_pattern : Pattern.t }

(* A condition's test on a property's value: absent passes, present must hold. *)
type value_test = {
  vt_condition : int;
  (* One of these constants (strings, integers, booleans, null). None: the property must be absent. *)
  is_pattern : bool;
  allowed : value_ref array;
  vt_pattern : Pattern.t option;
  requires_string : bool;
}

type masked_value = { mv_value : value_ref; mutable mv_mask : int }

(* Each constant any of an entry's constant tests allows, with the mask of the tests (by index in tests) that allow
   it. Strings are looked up by name. Other constants are compared in turn. *)
type merged_tests = {
  m_strings : Names.t;
  string_masks : int array;
  m_others : masked_value array;
  (* The tests that are constant sets. The others (patterns) are tested one by one. *)
  keyed : int;
}

(* One child schema applying to a property on behalf of a contributor. *)
type fused_app = {
  contributor : int;
  (* Not set: true, which covers without a test. *)
  has_child : bool;
  app_child : child;
  (* Other contributors whose resolution of the property is the same test: applied once when any is active. *)
  app_others : int array;
  (* For an application under conditions: the conditions that apply it when they hold, and when they do not, over
     its contributors. *)
  mutable app_then : int;
  mutable app_els : int;
}

type fused_entry = {
  apps : fused_app array;
  (* The conditions' tests on this property's value. *)
  tests : value_test array;
  (* The constant tests merged: one lookup decides them all. *)
  merged : merged_tests option;
}

type alt_branch = { alt_set : bool; group : int; branch : int }

let no_alt = { alt_set = false; group = 0; branch = 0 }

(* A child schema, or true (not set), which covers without a test. *)
type opt_child = { oc_set : bool; oc_child : child }

let no_opt_child = { oc_set = false; oc_child = no_child }

type fused_pattern = { fp_pattern : Pattern.t; fp_child : opt_child }

type fused_contributor = {
  (* The condition and the polarity under which it applies. *)
  condition : gate;
  (* The alternative group and the branch in it. *)
  alt : alt_branch;
  c_patterns : fused_pattern array;
  (* additionalProperties. *)
  c_has_additional : bool;
  c_additional : opt_child;
  c_required : int array;
  c_min : int;
  c_max : int;
  (* The condition as a mask: the bit of the condition in c_then when it applies the contributor by holding, in
     c_els when by not holding. *)
  mutable c_then : int;
  mutable c_els : int;
}

(* An if the pass decides (required names and value tests), or the presence of a dependency's property. *)
type fused_condition = {
  fc_required : int array;
  (* The enclosing condition and the polarity under which this one is reached. *)
  fc_gate : gate;
}

type fused_alternative = { al_condition : gate; al_exactly_one : bool; al_branches : int array array }
type fused_alt_group = { exactly_one : bool; count : int }

type fused_object = {
  (* The branches merge into one strict object loop: every one applies unconditionally with declared properties and
     required names only (and count bounds), and each name resolves to one schema. *)
  flat : object_plan option;
  (* Every property name any branch or condition knows, to its entry. *)
  f_names : Names.t;
  entries : fused_entry array;
  (* The branches, the node itself first, then the required lists of dependencies. *)
  contributors : fused_contributor array;
  conditions : fused_condition array;
  (* Required-only oneOf/anyOf keywords, decided from the seen names after the pass. *)
  alternatives : fused_alternative array;
  (* oneOf/anyOf groups whose branches carry object keywords (each branch is a contributor). *)
  alt_groups : fused_alt_group array;
  (* not: {required: [...]}: names that must not all be present, under a condition. *)
  forbidden : fused_forbidden array;
  (* Conditions that fail when some property name matches a pattern (an if with patternProperties: {P: false}), for
     names no entry knows. A known name that matches carries a test that never holds. *)
  absent : fused_absent array;
  (* The contributors with something to check after the pass (required names or count bounds). *)
  finals : int array;
  (* Some branch has pattern properties or additional properties, or some condition absent patterns, so names no
     entry knows need resolving. *)
  resolves_unknown : bool;
  has_count_bounds : bool;
  has_unevaluated : bool;
  unevaluated : child;
}

(* A node's keywords, grouped so that only the ones for the instance's type are looked at. *)
type body = {
  (* For an object instance, the whole node in one pass (see fused.ml), in place of everything below. *)
  mutable fused : fused_object option;
  (* Instance kinds (type_object, type_array) whose evaluated properties or items the general evaluator must track
     (unevaluatedProperties, unevaluatedItems), and the node it runs. *)
  general : int;
  b_node : int;
  (* unevaluatedItems with a static coverage: the items from this index on, against the child. *)
  has_unevaluated_items : bool;
  unevaluated_from : int;
  unevaluated_child : child;
  (* const and enum. *)
  b_values : op array;
  number : number_op array;
  str : string_op array;
  object_ : object_plan option;
  array : array_plan option;
  (* In-place applicators (children resolved through pure-$ref hops), in the general evaluator's order. *)
  apply : op array;
}

type plan = {
  p_types : int;
  (* Evaluated in place under the depth guard (part of an in-place cycle). *)
  guard : bool;
  (* Everything beyond the type check (None for true, false and type-only schemas). *)
  mutable body : body option;
  (* The body's shape, for entering it directly. *)
  mutable p_shape : int;
  (* The node as a child, for entering it by its id. *)
  mutable self : child;
}

type t = {
  nodes : Node.t array;
  root : int;
  (* The entry for fail-fast evaluation: the root's target. *)
  entry : int;
  uses_dynamic_scope : bool;
  max_depth : int;
  (* The time in seconds one match of a pattern may take, or zero for no limit. *)
  pattern_match_limit : float;
  (* The clock that limit is counted on, or None for the time that passes. *)
  pattern_match_clock : (unit -> float) option;
  (* The custom format of each node whose format names one. Go looks the name up in a map where it is asserted. *)
  custom_formats : (string -> bool) option array;
  (* For fail-fast evaluation: each node's target after following pure $ref hops. *)
  fast_target : int array;
  (* Name lookup tables for nodes with many properties. *)
  property_maps : Names.t option array;
  annotation_sources : Compiler.annotation_source option array;
  (* The annotation keywords of every node, computed on the first evaluation with a collector. Two evaluations that
     start together may each compute them, and they compute the same. *)
  annotations : annotation_entry array array option Atomic.t;
  assert_format_set : bool;
  (* Fail-fast plans. *)
  mutable plans : plan array;
}
