(* The compiled schema graph: one node per distinct (document, pointer), with keyword data digested in advance. A
   port of node.go of the Go module.

   A node is filled in by the compiler and by the analyses, which is why its fields are mutable. Nothing writes to
   one after compilation. *)

(* A node index in the compiled graph, or no_node. *)
let no_node = -1

(* JSON types as bits. The first six are the kinds of a document's values. *)
let type_null = Document.kind_null
let type_boolean = Document.kind_bool
let type_object = Document.kind_object
let type_array = Document.kind_array
let type_number = Document.kind_number
let type_string = Document.kind_string
let type_integer = 64

(* What contentEncoding and contentMediaType ask of a string. *)
let content_none = 0
let content_base64 = 1
let content_json = 2
let content_base64_json = 3

(* A value in a schema document (a constant, a bound). [n] is negative when there is none. *)
type value_ref = { d : Document.t; n : int }

let no_value = { d = Document.create_empty (); n = -1 }
let is_set v = v.n >= 0

(* An optional non-negative integer keyword value is an [int], negative when the keyword is absent. Go holds a
   uint64. A value of 2^62 or more is held as the greatest [int], which no count of a document reaches either. *)
let no_count = -1

(* An annotation-producing keyword and its value (reported in verbose results). *)
type annotation_entry = {
  keyword : string;
  (* The value as JSON text. *)
  value : string;
  (* Reported only when the instance is a string (content keywords). *)
  strings_only : bool;
}

type named_node = { name : string; node : int }
type pattern_property = { pp_pattern : Pattern.t; pp_node : int }

(* The keyword a dependency entry came from, which names its result rows. *)
let keyword_dependencies = "dependencies"
let keyword_dependent_schemas = "dependentSchemas"
let keyword_dependent_required = "dependentRequired"

type dependency_entry = {
  dep_keyword : string;
  dep_name : string;
  has_required : bool;
  dep_required : string array;
  schema : int;
}

type resource_node = { rn_resource : int; rn_node : int }

(* A $dynamicRef/$recursiveRef that stays dynamic after compile-time analysis. *)
type dynamic_ref_target = {
  is_recursive : bool;
  fallback : int;
  (* The node of the matching anchor in each resource that defines it. *)
  by_resource : resource_node array;
}

(* A discriminator value: one of the JSON scalars const and enum can name. *)
type discriminator_value = {
  dv_kind : int;
  (* A string's text. *)
  text : string;
  (* A number, or a boolean. *)
  dv_value : value_ref;
}

(* dv_matches reports whether an instance value equals this one (numbers by value: 1 and 1.0 are the same key). *)
let dv_matches v d x =
  Document.kind d x = v.dv_kind
  &&
  if v.dv_kind = Document.kind_string then Document.str_equals d x v.text
  else if v.dv_kind = Document.kind_null then true
  else Values.values_equal d x v.dv_value.d v.dv_value.n

(* dv_same is key equality, with numbers by value. *)
let dv_same v other =
  v.dv_kind = other.dv_kind
  &&
  if v.dv_kind = Document.kind_string then String.equal v.text other.text
  else if v.dv_kind = Document.kind_null then true
  else Values.values_equal v.dv_value.d v.dv_value.n other.dv_value.d other.dv_value.n

type discriminator_entry = {
  de_value : discriminator_value;
  (* The branches (indexes into the keyword's list) the value can select. *)
  branches : int array;
}

(* A discriminator selects oneOf/anyOf branches by the value of one property. *)
type discriminator = {
  property : string;
  (* Known discriminator values and the branches each can select. *)
  known : discriminator_entry array;
  (* Branches that stay candidates for a value not in known (negative and wildcard branches). *)
  unknown : int array;
  (* Every branch requires the property, so its absence fails the keyword at once. *)
  all_require : bool;
}

type t = {
  resource_id : int;
  dialect : Dialect.t;
  (* The JSON pointer of the schema within its document. *)
  pointer : string;
  mutable always_true : bool;
  mutable always_false : bool;
  (* Assertions. *)
  mutable type_mask : int;
  mutable has_type : bool;
  mutable const_value : value_ref;
  mutable has_enum : bool;
  mutable enum_values : value_ref array;
  (* References. *)
  mutable ref_ : int;
  (* A $dynamicRef/$recursiveRef that compile-time analysis resolved statically, and its keyword. *)
  mutable static_dynamic_ref : int;
  mutable static_dynamic_keyword : string;
  mutable dynamic_ref : dynamic_ref_target option;
  (* In-place applicators. *)
  mutable has_all_of : bool;
  mutable has_any_of : bool;
  mutable has_one_of : bool;
  mutable all_of : int array;
  mutable any_of : int array;
  mutable one_of : int array;
  mutable not_ : int;
  mutable if_ : int;
  mutable then_ : int;
  mutable else_ : int;
  (* Objects. *)
  mutable has_properties : bool;
  mutable properties : named_node array;
  mutable has_pattern_properties : bool;
  mutable pattern_properties : pattern_property array;
  mutable additional_properties : int;
  mutable property_names : int;
  mutable has_required : bool;
  (* Without duplicates. *)
  mutable required : string array;
  (* As written (duplicates kept), for results. *)
  mutable required_list : string array;
  mutable has_dependencies : bool;
  mutable dependencies : dependency_entry array;
  mutable min_properties : int;
  mutable max_properties : int;
  mutable unevaluated_properties : int;
  (* Arrays. *)
  mutable has_prefix_items : bool;
  mutable prefix_items : int array;
  (* The keywords behind prefixItems/items: prefixItems/items (2020-12) or items/additionalItems (legacy). *)
  mutable prefix_keyword : string;
  mutable items_keyword : string;
  mutable items : int;
  mutable contains : int;
  mutable min_contains : int;
  mutable max_contains : int;
  mutable contains_marks_evaluated : bool;
  mutable min_items : int;
  mutable max_items : int;
  mutable unique_items : bool;
  mutable unevaluated_items : int;
  (* Strings. *)
  mutable min_length : int;
  mutable max_length : int;
  mutable pattern : Pattern.t option;
  mutable has_format : bool;
  mutable format : string;
  mutable format_kind : Formats.kind;
  mutable assert_format : bool;
  mutable content : int;
  mutable assert_content : bool;
  (* Numbers. *)
  mutable minimum : value_ref;
  mutable maximum : value_ref;
  mutable exclusive_minimum : value_ref;
  mutable exclusive_maximum : value_ref;
  mutable multiple_of : value_ref;
  (* The multipleOf divisor, digested. *)
  mutable divisor : Numbers.divisor option;
  (* Analysis. *)
  mutable marks_properties : bool;
  mutable marks_items : bool;
  mutable in_place_cycle : bool;
  mutable one_of_discriminator : discriminator option;
  mutable any_of_discriminator : discriminator option;
}

let create resource dialect pointer =
  {
    resource_id = resource;
    dialect;
    pointer;
    always_true = false;
    always_false = false;
    type_mask = 0;
    has_type = false;
    const_value = no_value;
    has_enum = false;
    enum_values = [||];
    ref_ = no_node;
    static_dynamic_ref = no_node;
    static_dynamic_keyword = "$dynamicRef";
    dynamic_ref = None;
    has_all_of = false;
    has_any_of = false;
    has_one_of = false;
    all_of = [||];
    any_of = [||];
    one_of = [||];
    not_ = no_node;
    if_ = no_node;
    then_ = no_node;
    else_ = no_node;
    has_properties = false;
    properties = [||];
    has_pattern_properties = false;
    pattern_properties = [||];
    additional_properties = no_node;
    property_names = no_node;
    has_required = false;
    required = [||];
    required_list = [||];
    has_dependencies = false;
    dependencies = [||];
    min_properties = no_count;
    max_properties = no_count;
    unevaluated_properties = no_node;
    has_prefix_items = false;
    prefix_items = [||];
    prefix_keyword = "prefixItems";
    items_keyword = "items";
    items = no_node;
    contains = no_node;
    min_contains = 1;
    max_contains = no_count;
    contains_marks_evaluated = false;
    min_items = no_count;
    max_items = no_count;
    unique_items = false;
    unevaluated_items = no_node;
    min_length = no_count;
    max_length = no_count;
    pattern = None;
    has_format = false;
    format = "";
    format_kind = Formats.unknown;
    assert_format = false;
    content = content_none;
    assert_content = false;
    minimum = no_value;
    maximum = no_value;
    exclusive_minimum = no_value;
    exclusive_maximum = no_value;
    multiple_of = no_value;
    divisor = None;
    marks_properties = false;
    marks_items = false;
    in_place_cycle = false;
    one_of_discriminator = None;
    any_of_discriminator = None;
  }

(* has_object_keywords reports keywords that apply only to objects. *)
let has_object_keywords n =
  n.has_properties || n.has_pattern_properties || n.additional_properties >= 0 || n.property_names >= 0
  || n.has_required || n.has_dependencies || n.min_properties >= 0 || n.max_properties >= 0
  || n.unevaluated_properties >= 0

let has_array_keywords n =
  n.has_prefix_items || n.items >= 0 || n.contains >= 0 || n.min_items >= 0 || n.max_items >= 0 || n.unique_items
  || n.unevaluated_items >= 0

let has_string_keywords n =
  n.min_length >= 0 || n.max_length >= 0 || n.pattern <> None
  || (n.assert_format && n.has_format && not (Formats.is_numeric n.format_kind))
  || n.assert_content

let has_number_keywords n =
  is_set n.minimum || is_set n.maximum || is_set n.exclusive_minimum || is_set n.exclusive_maximum
  || is_set n.multiple_of
  || (n.assert_format && n.has_format && Formats.is_numeric n.format_kind)

let has_dependency_schema n = Array.exists (fun dep -> dep.schema >= 0) n.dependencies

let has_in_place_applicators n =
  n.ref_ >= 0 || n.static_dynamic_ref >= 0 || n.dynamic_ref <> None || n.has_all_of || n.has_any_of || n.has_one_of
  || n.not_ >= 0 || n.if_ >= 0 || has_dependency_schema n

(* is_pure_ref reports a node that is nothing but $ref: no other keyword that asserts. *)
let is_pure_ref n =
  n.ref_ >= 0 && (not n.has_type)
  && (not (is_set n.const_value))
  && (not n.has_enum)
  && (not (has_object_keywords n))
  && (not (has_array_keywords n))
  && (not (has_string_keywords n))
  && (not (has_number_keywords n))
  && n.dynamic_ref = None && n.static_dynamic_ref < 0 && (not n.has_all_of) && (not n.has_any_of) && (not n.has_one_of)
  && n.not_ < 0 && n.if_ < 0 && not n.has_dependencies

(* in_place_children are the in-place children (the instance is evaluated at the same location). *)
let in_place_children n include_not =
  let out = ref [] in
  let add id = if id >= 0 then out := id :: !out in
  add n.ref_;
  add n.static_dynamic_ref;
  (match n.dynamic_ref with
  | Some d ->
      out := d.fallback :: !out;
      Array.iter (fun r -> out := r.rn_node :: !out) d.by_resource
  | None -> ());
  Array.iter (fun id -> out := id :: !out) n.all_of;
  Array.iter (fun id -> out := id :: !out) n.any_of;
  Array.iter (fun id -> out := id :: !out) n.one_of;
  if include_not then add n.not_;
  add n.if_;
  add n.then_;
  add n.else_;
  Array.iter (fun dep -> add dep.schema) n.dependencies;
  List.rev !out

(* children are every child node. *)
let children n =
  let out = ref (List.rev (in_place_children n true)) in
  let add id = if id >= 0 then out := id :: !out in
  Array.iter (fun p -> out := p.node :: !out) n.properties;
  Array.iter (fun p -> out := p.pp_node :: !out) n.pattern_properties;
  add n.additional_properties;
  add n.property_names;
  add n.unevaluated_properties;
  add n.items;
  add n.contains;
  add n.unevaluated_items;
  Array.iter (fun id -> out := id :: !out) n.prefix_items;
  List.rev !out
