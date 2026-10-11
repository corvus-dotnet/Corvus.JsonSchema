(* The compilation of the fail-fast plans (see program.ml for what a plan is). A port of the compilation half of
   plan.go of the Go module, and of the making of the program in eval.go. The evaluation of a plan is in eval.ml. *)

open Node
open Program

let new_discriminator_index (d : discriminator) =
  let list = ref [] and string_entries = ref [] and others = ref [] in
  Array.iteri
    (fun i entry ->
      let v = entry.de_value in
      if v.dv_kind = Document.kind_string then begin
        list := v.text :: !list;
        string_entries := i :: !string_entries
      end
      else others := i :: !others)
    d.known;
  {
    strings = Names.create (Array.of_list (List.rev !list));
    string_entries = Array.of_list (List.rev !string_entries);
    others = Array.of_list (List.rev !others);
  }

let new_branches children disc =
  {
    b_children = children;
    by_kind = Array.make 6 [||];
    discriminator = disc;
    index = (match disc with Some d -> Some (new_discriminator_index d) | None -> None);
  }

(* kind_index is the index of a kind in a dispatch table: the position of its bit. *)
let kind_index kind =
  if kind = 1 then 0
  else if kind = 2 then 1
  else if kind = 4 then 2
  else if kind = 8 then 3
  else if kind = 16 then 4
  else 5

(* dispatch computes the dispatch table once the children's types are known. *)
let dispatch (b : branches) =
  let kind_types = [| type_null; type_boolean; type_object; type_array; type_number lor type_integer; type_string |] in
  Array.iteri
    (fun k types ->
      let list = ref [] in
      Array.iteri (fun i (c : child) -> if c.types land types <> 0 then list := i :: !list) b.b_children;
      b.by_kind.(k) <- Array.of_list (List.rev !list))
    kind_types

(* expand_types is a type mask with integer made explicit wherever number is (every integer is a number). *)
let expand_types mask = if mask land type_number <> 0 then mask lor type_integer else mask

(* meet_types is the types both masks admit. *)
let meet_types a b =
  let m = expand_types a land expand_types b in
  (* number in both keeps number. integer alone stays integer. *)
  if a land b land type_number = 0 && m land type_number <> 0 then m land lnot type_number else m

(* type_only_mask is the mask of a schema that tests only the type (true admits everything, false nothing), or -1
   for a schema that tests more. *)
let type_only_mask (n : Node.t) =
  if n.always_true then any_type
  else if n.always_false then 0
  else if
    n.has_type && (not n.in_place_cycle)
    && (not (is_set n.const_value))
    && (not n.has_enum)
    && (not (has_number_keywords n))
    && (not (has_string_keywords n))
    && (not (has_object_keywords n))
    && (not (has_array_keywords n))
    && (not (has_in_place_applicators n))
    && n.dynamic_ref = None
  then n.type_mask
  else -1

(* type_union is the union of anyOf/oneOf branches that all test only the type, or -1. For oneOf, only when no two
   branches admit a common value (so that "exactly one" is "any"). *)
let type_union (p : Program.t) list exactly_one =
  let masks = Array.map (fun c -> type_only_mask p.nodes.(p.fast_target.(c))) list in
  if Array.exists (fun m -> m < 0) masks then -1
  else begin
    let ok = ref true in
    let union = ref 0 in
    Array.iteri
      (fun i a ->
        if exactly_one then
          for j = i + 1 to Array.length masks - 1 do
            if expand_types a land expand_types masks.(j) <> 0 then ok := false
          done;
        union := !union lor a)
      masks;
    if !ok then !union else -1
  end

(* static_item_coverage is the items a node's evaluation always marks evaluated, when that is static: the longest
   prefixItems of the node and the contributors it always applies ($ref and allOf chains) as from, or all of them
   when one has items (or, below the node, unevaluatedItems). It returns None when an in-place child that applies
   conditionally (anyOf, oneOf, if/then/else, a dependent schema, a dynamic reference) can mark items, or contains
   marks them, and otherwise whether all the items are covered and the index the rest start from. *)
let static_item_coverage (p : Program.t) id =
  let stack = ref [ (id, true) ] in
  let visited = ref [ id ] in
  let all = ref false and from = ref 0 and ok = ref true in
  while !ok && !stack <> [] do
    let at, is_root = List.hd !stack in
    stack := List.tl !stack;
    let n = p.nodes.(at) in
    if not (n.always_true || n.always_false) then
      if n.in_place_cycle || n.dynamic_ref <> None || (n.contains >= 0 && n.contains_marks_evaluated) then ok := false
      else begin
        from := Int.max !from (Array.length n.prefix_items);
        all := !all || n.items >= 0 || ((not is_root) && n.unevaluated_items >= 0);
        let marks c = c >= 0 && p.nodes.(c).marks_items in
        if
          Array.exists marks n.any_of || Array.exists marks n.one_of || marks n.if_ || marks n.then_ || marks n.else_
          || Array.exists (fun (dep : dependency_entry) -> marks dep.schema) n.dependencies
        then ok := false
        else begin
          let follow c =
            if c >= 0 && not (List.mem c !visited) then begin
              visited := c :: !visited;
              stack := (c, false) :: !stack
            end
          in
          (* The stack is last in, first out: pushed in Go's order, they are taken in Go's order. *)
          follow n.ref_;
          follow n.static_dynamic_ref;
          Array.iter follow n.all_of
        end
      end
  done;
  if !ok then Some (!all, !from) else None

let plan_object (p : Program.t) (n : Node.t) (child_of : int -> child) =
  (* Names only required or dependencies mention join the declared ones, up to mask_bits names in all (one mask
     word). *)
  let declared = Array.map (fun (property : named_node) -> property.name) n.properties in
  let extras = ref [] in
  let extra name =
    if not (Array.exists (String.equal name) declared || List.mem name !extras) then extras := name :: !extras
  in
  Array.iter extra n.required;
  Array.iter
    (fun (dep : dependency_entry) ->
      extra dep.dep_name;
      Array.iter extra dep.dep_required)
    n.dependencies;
  let extras = Array.of_list (List.rev !extras) in
  let known =
    if Array.length declared + Array.length extras <= mask_bits then Array.append declared extras else declared
  in
  let names = Names.create known in
  let has_additional = n.additional_properties >= 0 in
  let undeclared = if has_additional then child_of n.additional_properties else no_child in
  (* A child for each use, as each takes its shape separately. *)
  let children =
    Array.mapi (fun i _ -> if i < Array.length n.properties then child_of n.properties.(i).node else undeclared) known
  in
  let has_names = Array.length known <> 0 in
  let visit =
    if n.property_names < 0 && (not has_names) && Array.length n.pattern_properties = 1 then visit_pattern
    else if n.has_pattern_properties || n.property_names >= 0 then visit_general
    else if has_names then visit_names
    else if has_additional then visit_values
    else visit_none
  in
  let visited = (visit = visit_names || visit = visit_general) && Array.length known <= mask_bits in
  (* The seen bit of a name, or 0 when the pass does not see it. *)
  let bit name =
    if not visited then 0
    else begin
      let i = Names.find_string names name in
      if i >= 0 then 1 lsl i else 0
    end
  in
  let required_mask = ref 0 in
  let required = ref [] in
  Array.iter
    (fun r ->
      let b = bit r in
      if b <> 0 then required_mask := !required_mask lor b else required := r :: !required)
    n.required;
  let dependencies =
    Array.map
      (fun (dep : dependency_entry) ->
        let name_bit = bit dep.dep_name in
        let ok = ref (name_bit <> 0) in
        let required_bits = ref 0 in
        Array.iter
          (fun r ->
            let b = bit r in
            if b = 0 then ok := false;
            required_bits := !required_bits lor b)
          dep.dep_required;
        {
          pd_name = dep.dep_name;
          pd_required = dep.dep_required;
          pd_schema = (if dep.schema >= 0 then p.fast_target.(dep.schema) else no_node);
          has_bits = !ok;
          name_bit;
          required_bits = !required_bits;
        })
      n.dependencies
  in
  let patterns =
    Array.map (fun pp -> { pc_pattern = pp.pp_pattern; pc_child = child_of pp.pp_node }) n.pattern_properties
  in
  (* The matches made while a schema is compiled have the limit of a match too. *)
  let scratch = Pattern_engine.create_limited_scratch p.pattern_match_limit p.pattern_match_clock in
  let name_patterns =
    Array.map
      (fun name ->
        let matching = ref [] in
        Array.iteri
          (fun j pp -> if Pattern.match_string pp.pp_pattern scratch name then matching := j :: !matching)
          n.pattern_properties;
        Array.of_list (List.rev !matching))
      known
  in
  let rest_free = !required = [] && not n.has_dependencies in
  {
    o_min = (if n.min_properties >= 0 then n.min_properties else 0);
    o_max = (if n.max_properties >= 0 then n.max_properties else max_int);
    visit;
    o_names = names;
    declared = Array.length n.properties;
    o_children = children;
    required_mask = !required_mask;
    o_required = Array.of_list (List.rev !required);
    patterns;
    name_patterns;
    has_additional;
    additional = undeclared;
    property_names = (if n.property_names >= 0 then p.fast_target.(n.property_names) else no_node);
    o_dependencies = dependencies;
    rest_free;
    strict = visit = visit_names && rest_free;
    lookup = visit = visit_names && (not has_additional) && Array.length known <= lookup_names;
  }

let empty_plan types guard = { p_types = types; guard; body = None; p_shape = shape_general; self = no_child }

let plan_node (p : Program.t) id (n : Node.t) =
  let guard = n.in_place_cycle in
  if n.always_true then empty_plan any_type guard
  else if n.always_false then empty_plan 0 guard
  else begin
    let target id = p.fast_target.(id) in
    let child_of id = { id = target id; types = any_type; shape = shape_general; pass = 0 } in
    let values = ref [] in
    if is_set n.const_value then values := { (new_op op_const) with value = n.const_value } :: !values;
    if n.has_enum then
      if Array.for_all (fun v -> Document.kind v.d v.n = Document.kind_string) n.enum_values then
        values :=
          {
            (new_op op_enum_strings) with
            names = Some (Names.create (Array.map (fun v -> Document.string_value v.d v.n) n.enum_values));
          }
          :: !values
      else values := { (new_op op_enum) with values = n.enum_values } :: !values;

    let format_check_for numeric =
      if (not n.assert_format) || (not n.has_format) || Formats.is_numeric n.format_kind <> numeric then None
      else
        match p.custom_formats.(id) with
        | Some custom -> Some { custom = Some custom; fc_kind = Formats.unknown; legacy = false }
        | None ->
            if n.format_kind = Formats.unknown then None
            else Some { custom = None; fc_kind = n.format_kind; legacy = Dialect.rank n.dialect <= 1 }
    in
    let number_op kind =
      { n_kind = kind; n_flag = 0; n_data = 0; n_floats = Float.Array.create 0; n_divisor = None; n_format = no_format }
    in

    (* Numbers. *)
    let number = ref [] in
    (match format_check_for true with
    | Some f -> number := { (number_op number_format) with n_format = f } :: !number
    | None -> ());
    let bound kind v =
      if is_set v then
        number :=
          {
            (number_op kind) with
            n_flag = Document.flags v.d v.n;
            n_data = Document.data v.d v.n;
            n_floats = Document.floats v.d;
          }
          :: !number
    in
    bound number_minimum n.minimum;
    bound number_maximum n.maximum;
    bound number_exclusive_minimum n.exclusive_minimum;
    bound number_exclusive_maximum n.exclusive_maximum;
    if is_set n.multiple_of then number := { (number_op number_multiple_of) with n_divisor = n.divisor } :: !number;

    (* Strings. *)
    let string_op kind =
      { s_kind = kind; s_min = 0; s_max = max_int; s_pattern = None; s_format = no_format; s_content = content_none }
    in
    let str = ref [] in
    if n.min_length >= 0 || n.max_length >= 0 then
      str :=
        {
          (string_op string_length) with
          s_min = (if n.min_length >= 0 then n.min_length else 0);
          s_max = (if n.max_length >= 0 then n.max_length else max_int);
        }
        :: !str;
    if n.pattern <> None then str := { (string_op string_pattern) with s_pattern = n.pattern } :: !str;
    (match format_check_for false with
    | Some f -> str := { (string_op string_format) with s_format = f } :: !str
    | None -> ());
    if n.assert_content then str := { (string_op string_content) with s_content = n.content } :: !str;

    (* Objects (unless the type excludes objects: then the keywords apply to nothing and must not cost the node its
       shape). *)
    let own_types = if n.has_type then n.type_mask else any_type in
    let object_ =
      if has_object_keywords n && own_types land type_object <> 0 then Some (plan_object p n child_of) else None
    in

    (* Arrays. *)
    let array =
      if has_array_keywords n && own_types land type_array <> 0 then
        Some
          {
            a_min = (if n.min_items >= 0 then n.min_items else 0);
            a_max = (if n.max_items >= 0 then n.max_items else max_int);
            prefix = Array.map child_of n.prefix_items;
            has_items = n.items >= 0;
            items = (if n.items >= 0 then child_of n.items else no_child);
            a_contains = (if n.contains >= 0 then target n.contains else no_node);
            a_min_contains = (if n.contains >= 0 then n.min_contains else 0);
            a_max_contains = (if n.contains >= 0 then n.max_contains else no_count);
            unique = n.unique_items;
            is_simple = false;
            simple = any_type;
            has_nested = false;
            nested_min = 0;
            nested_max = 0;
            nested_types = 0;
          }
      else None
    in

    (* In-place applicators, in the general evaluator's order. *)
    let apply = ref [] in
    if n.ref_ >= 0 then apply := { (new_op op_ref) with node = target n.ref_ } :: !apply;
    if n.static_dynamic_ref >= 0 then apply := { (new_op op_ref) with node = target n.static_dynamic_ref } :: !apply;
    (match n.dynamic_ref with
    | Some d ->
        if p.uses_dynamic_scope then apply := { (new_op op_dynamic_ref) with dynamic = Some d } :: !apply
        else
          (* Without a dynamic scope the reference always takes its fallback. *)
          apply := { (new_op op_ref) with node = target d.fallback } :: !apply
    | None -> ());
    let children list = Array.map child_of list in
    if n.has_all_of then apply := { (new_op op_all_of) with children = children n.all_of } :: !apply;
    (* An anyOf of type-only branches, or a oneOf of type-only branches with no type in common, is one type test: it
       narrows the node's own types instead of adding a keyword. *)
    let union = ref any_type in
    if n.has_any_of then begin
      let mask = type_union p n.any_of false in
      if mask >= 0 then union := meet_types !union mask
      else
        apply :=
          { (new_op op_any_of) with branches = Some (new_branches (children n.any_of) n.any_of_discriminator) }
          :: !apply
    end;
    if n.has_one_of then begin
      let mask = type_union p n.one_of true in
      if mask >= 0 then union := meet_types !union mask
      else
        apply :=
          { (new_op op_one_of) with branches = Some (new_branches (children n.one_of) n.one_of_discriminator) }
          :: !apply
    end;
    if n.not_ >= 0 then apply := { (new_op op_not) with node = target n.not_ } :: !apply;
    if n.if_ >= 0 && (n.then_ >= 0 || n.else_ >= 0) then
      apply :=
        {
          (new_op op_if) with
          node = target n.if_;
          then_ = (if n.then_ >= 0 then target n.then_ else no_node);
          else_ = (if n.else_ >= 0 then target n.else_ else no_node);
        }
        :: !apply;

    (* unevaluatedProperties is left to the general evaluator (or a fused object plan). unevaluatedItems takes the
       items after a static prefix when every contribution to the evaluated items is unconditional. *)
    let general = ref 0 in
    if n.unevaluated_properties >= 0 then general := !general lor type_object;
    let has_unevaluated_items = ref false and unevaluated_from = ref 0 and unevaluated_child = ref no_child in
    if n.unevaluated_items >= 0 then
      begin match static_item_coverage p id with
      | None -> general := !general lor type_array
      | Some (all, from) ->
          if not all then begin
            has_unevaluated_items := true;
            unevaluated_from := from;
            unevaluated_child := child_of n.unevaluated_items
          end
      end;

    let types = meet_types own_types !union in
    if
      !values = [] && !number = [] && !str = [] && object_ = None && array = None && !apply = [] && !general = 0
      && not !has_unevaluated_items
    then empty_plan types guard
    else
      {
        (empty_plan types guard) with
        body =
          Some
            {
              fused = None;
              general = !general;
              b_node = id;
              has_unevaluated_items = !has_unevaluated_items;
              unevaluated_from = !unevaluated_from;
              unevaluated_child = !unevaluated_child;
              b_values = Array.of_list (List.rev !values);
              number = Array.of_list (List.rev !number);
              str = Array.of_list (List.rev !str);
              object_;
              array;
              apply = Array.of_list (List.rev !apply);
            };
      }
  end

(* shape_of says how callers can enter a plan (see the shapes). The shortcuts skip the scope push, so a program that
   keeps a dynamic scope takes them only for leaves. A node on an in-place cycle is entered through its guard. *)
let shape_of (pl : plan) dynamic_scope =
  if pl.guard then shape_general
  else
    match pl.body with
    | None -> shape_trivial
    | Some b ->
        if b.general <> 0 || b.has_unevaluated_items then shape_general
        else if b.fused <> None then
          (* Without the scope push of the general entry, so not in a program that keeps a dynamic scope. *)
          if dynamic_scope then shape_general else shape_fused
        else begin
          let values = Array.length b.b_values <> 0 || Array.length b.number <> 0 || Array.length b.str <> 0 in
          let has_object = b.object_ <> None
          and has_array = b.array <> None
          and has_apply = Array.length b.apply <> 0 in
          if (not has_object) && (not has_array) && not has_apply then
            if
              values
              && Array.length b.number = 0
              && Array.length b.str = 0
              && Array.length b.b_values = 1
              && b.b_values.(0).kind = op_enum_strings
            then shape_string_enum
            else if values && Array.length b.number = 0 && Array.length b.b_values = 0 then shape_strings
            else shape_leaf
          else if values || dynamic_scope then shape_general
          else if has_object && (not has_array) && not has_apply then shape_object
          else if (not has_object) && has_array && not has_apply then shape_array
          else if (not has_object) && (not has_array) && has_apply then shape_apply
          else shape_general
        end

(* reaches_dynamic_reference marks every node from which a live dynamic reference is reachable through any child. *)
let reaches_dynamic_reference (p : Program.t) =
  let reaches = Array.make (Array.length p.nodes) false in
  if p.uses_dynamic_scope then begin
    let children = Array.map Node.children p.nodes in
    Array.iteri (fun i (n : Node.t) -> reaches.(i) <- n.dynamic_ref <> None) p.nodes;
    let changed = ref true in
    while !changed do
      changed := false;
      Array.iteri
        (fun i list ->
          if (not reaches.(i)) && List.exists (fun c -> reaches.(c)) list then begin
            reaches.(i) <- true;
            changed := true
          end)
        children
    done
  end;
  reaches

let compile_plans (p : Program.t) =
  let plans = Array.mapi (fun id n -> plan_node p id n) p.nodes in
  (* Hoist the children's type checks now that every plan is known. *)
  let summary_types = Array.map (fun (pl : plan) -> pl.p_types) plans in
  let summary_shape = Array.map (fun pl -> shape_of pl p.uses_dynamic_scope) plans in
  let resolved id =
    (* Not shape_object: a node may still take a fused plan below. *)
    let shape = if summary_shape.(id) = shape_object then shape_general else summary_shape.(id) in
    let c = { id; types = any_type; shape = shape_general; pass = 0 } in
    set_shape c summary_types.(id) shape;
    c
  in
  (* Fused object plans, for nodes whose object semantics span in-place applicators. A fused plan applies its
     contributors' keywords without entering them as nodes, so the dynamic scope below it would differ from the
     general path's where a contributor is in another resource: nodes that can reach a live dynamic reference fuse
     only contributors in their own resource (which the general path would not push again). *)
  let reaches_dynamic = reaches_dynamic_reference p in
  let fused_nodes = Array.make (Array.length plans) false in
  Array.iteri
    (fun id (pl : plan) ->
      match Fused.try_fuse p id reaches_dynamic.(id) resolved with
      | None -> ()
      | Some f ->
          (match pl.body with
          | Some b -> b.fused <- Some f
          | None ->
              pl.body <-
                Some
                  {
                    fused = Some f;
                    general = 0;
                    b_node = id;
                    has_unevaluated_items = false;
                    unevaluated_from = 0;
                    unevaluated_child = no_child;
                    b_values = [||];
                    number = [||];
                    str = [||];
                    object_ = None;
                    array = None;
                    apply = [||];
                  });
          fused_nodes.(id) <- true)
    plans;
  let fix (c : child) =
    if c.id >= 0 then
      if summary_shape.(c.id) = shape_object && fused_nodes.(c.id) then set_shape c summary_types.(c.id) shape_general
      else set_shape c summary_types.(c.id) summary_shape.(c.id)
  in
  Array.iter
    (fun (pl : plan) ->
      match pl.body with
      | None -> ()
      | Some b ->
          (match b.object_ with
          | Some o ->
              Array.iter fix o.o_children;
              Array.iter (fun pc -> fix pc.pc_child) o.patterns;
              if o.has_additional then fix o.additional
          | None -> ());
          (match b.array with
          | Some a ->
              Array.iter fix a.prefix;
              let item_types, type_only =
                if a.has_items then begin
                  fix a.items;
                  (a.items.types, a.items.shape = shape_trivial)
                end
                else (any_type, true)
              in
              a.is_simple <- type_only && Array.length a.prefix = 0 && a.a_contains < 0 && not a.unique;
              a.simple <- item_types
          | None -> ());
          if b.has_unevaluated_items then fix b.unevaluated_child;
          Array.iter
            (fun (o : op) ->
              if o.kind = op_all_of then Array.iter fix o.children
              else if o.kind = op_any_of || o.kind = op_one_of then
                match o.branches with
                | Some branches ->
                    Array.iter fix branches.b_children;
                    dispatch branches
                | None -> ())
            b.apply)
    plans;
  (* Arrays whose items are simple arrays check them inline, without entering each one. *)
  let simple =
    Array.map
      (fun (pl : plan) ->
        match pl.body with
        | Some { array = Some a; _ } when a.is_simple && shape_of pl p.uses_dynamic_scope = shape_array -> Some a
        | _ -> None)
      plans
  in
  Array.iter
    (fun (pl : plan) ->
      match pl.body with
      | Some { array = Some a; _ } when a.has_items && a.items.shape = shape_array -> (
          match simple.(a.items.id) with
          | Some nested ->
              a.has_nested <- true;
              a.nested_min <- nested.a_min;
              a.nested_max <- nested.a_max;
              a.nested_types <- nested.simple
          | None -> ())
      | _ -> ())
    plans;
  Array.iteri
    (fun i (pl : plan) ->
      pl.p_shape <- shape_of pl p.uses_dynamic_scope;
      let self = { id = i; types = any_type; shape = shape_general; pass = 0 } in
      set_shape self pl.p_types pl.p_shape;
      pl.self <- self)
    plans;
  (* Every child now takes the final shape of its node, which a fused plan may have changed since the child was made:
     a child that kept the shape its node had before it was fused would enter the node's applicators one by one where
     the fused plan decides them in one pass. *)
  let final (c : child) = if c.id >= 0 then set_shape c plans.(c.id).p_types plans.(c.id).p_shape in
  let final_object (o : object_plan) =
    Array.iter final o.o_children;
    Array.iter (fun pc -> final pc.pc_child) o.patterns;
    if o.has_additional then final o.additional
  in
  Array.iter
    (fun (pl : plan) ->
      match pl.body with
      | None -> ()
      | Some b -> (
          (match b.object_ with Some o -> final_object o | None -> ());
          (match b.array with
          | Some a ->
              Array.iter final a.prefix;
              if a.has_items then final a.items
          | None -> ());
          if b.has_unevaluated_items then final b.unevaluated_child;
          Array.iter
            (fun (o : op) ->
              if o.kind = op_all_of then Array.iter final o.children
              else if o.kind = op_any_of || o.kind = op_one_of then
                match o.branches with Some branches -> Array.iter final branches.b_children | None -> ())
            b.apply;
          match b.fused with
          | None -> ()
          | Some f ->
              (match f.flat with Some flat -> final_object flat | None -> ());
              Array.iter (fun e -> Array.iter (fun app -> if app.has_child then final app.app_child) e.apps) f.entries;
              Array.iter
                (fun c ->
                  Array.iter (fun fp -> if fp.fp_child.oc_set then final fp.fp_child.oc_child) c.c_patterns;
                  if c.c_has_additional && c.c_additional.oc_set then final c.c_additional.oc_child)
                f.contributors;
              if f.has_unevaluated then final f.unevaluated))
    plans;
  plans

(* ---------------------------------------------------------------------------------------------------------------------
   The program *)

let property_map_threshold = 8

(* has_no_assertions_besides_references reports a node with no keyword but references and allOf. *)
let has_no_assertions_besides_references (n : Node.t) =
  not
    (n.has_type || is_set n.const_value || n.has_enum || has_number_keywords n || has_string_keywords n
   || has_object_keywords n || has_array_keywords n || n.dynamic_ref <> None || n.has_any_of || n.has_one_of
   || n.not_ >= 0 || n.if_ >= 0)

(* pure_ref_target is the single reference of a node that is nothing but $ref (or a static $dynamicRef), for
   elision. *)
let pure_ref_target (n : Node.t) =
  if n.ref_ >= 0 = (n.static_dynamic_ref >= 0) || n.always_true || n.always_false then no_node
  else if n.has_all_of || not (has_no_assertions_besides_references n) then no_node
  else if n.ref_ >= 0 then n.ref_
  else n.static_dynamic_ref

(* forward_target is the branch of a node that is nothing but a one-branch allOf, for fail-fast forwarding. *)
let forward_target (n : Node.t) =
  if
    Array.length n.all_of <> 1
    || n.always_true || n.always_false || n.ref_ >= 0 || n.static_dynamic_ref >= 0
    || not (has_no_assertions_besides_references n)
  then no_node
  else n.all_of.(0)

let new_program (c : Compiler.compiled) (options : Options.t) =
  let nodes = c.nodes in
  let fast_target =
    Array.mapi
      (fun id _ ->
        let current = ref id in
        let hop = ref 0 in
        while !hop < 16 do
          let n = nodes.(!current) in
          (* Pure $ref hops, and forwards: a node whose only assertion is one allOf branch, unless either end is on an
             in-place cycle, which keeps its guard. *)
          let next = pure_ref_target n in
          let next =
            if next >= 0 then next
            else begin
              let next = forward_target n in
              if next < 0 || n.in_place_cycle || nodes.(next).in_place_cycle || next = !current then no_node else next
            end
          in
          if next < 0 || (c.uses_dynamic_scope && nodes.(next).resource_id <> n.resource_id) then hop := 16
          else begin
            current := next;
            incr hop
          end
        done;
        !current)
      nodes
  in
  let property_maps =
    Array.map
      (fun (n : Node.t) ->
        if Array.length n.properties > property_map_threshold then
          Some (Names.create (Array.map (fun (property : named_node) -> property.name) n.properties))
        else None)
      nodes
  in
  let custom_formats =
    Array.map (fun (n : Node.t) -> if n.has_format then List.assoc_opt n.format options.formats else None) nodes
  in
  let p =
    {
      nodes;
      root = c.root;
      entry = fast_target.(c.root);
      uses_dynamic_scope = c.uses_dynamic_scope;
      max_depth = options.max_depth;
      pattern_match_limit = options.pattern_match_limit;
      pattern_match_clock = options.pattern_match_clock;
      custom_formats;
      fast_target;
      property_maps;
      annotation_sources = c.annotation_sources;
      annotations = Atomic.make None;
      assert_format_set = c.assert_format_set;
      plans = [||];
    }
  in
  p.plans <- compile_plans p;
  p

(* node_annotations are the annotation keywords of every node (computed on the first evaluation with a
   collector). *)
let node_annotations (p : Program.t) =
  match Atomic.get p.annotations with
  | Some annotations -> annotations
  | None ->
      let annotations =
        Array.mapi
          (fun id (n : Node.t) ->
            match p.annotation_sources.(id) with
            | Some src ->
                Compiler.collect_annotations src.a_doc src.a_value n.dialect src.a_vocab src.a_content
                  p.assert_format_set
            | None -> [||])
          p.nodes
      in
      Atomic.set p.annotations (Some annotations);
      annotations
