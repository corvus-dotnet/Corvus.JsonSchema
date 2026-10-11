(* The fused object plan: one pass over an object's properties for a schema whose object semantics are spread over
   in-place applicators ($ref, allOf, if/then/else, dependencies, required-only oneOf/anyOf) and possibly finished by
   unevaluatedProperties. A port of the construction half of fused.go of the Go module. The pass itself is in
   eval.ml.

   The general path evaluates each applicator branch as a separate pass over the instance, each with its own
   property lookups, and then walks the instance once more for unevaluatedProperties. The fused plan resolves every
   property name known to any branch at compile time to the list of child schemas that apply to it (a branch's own
   property schema, its matching pattern-property schemas, or its additionalProperties when neither matched), so
   evaluation is one lookup per instance property, and it tracks which properties some branch covered so that the
   unevaluated check needs no second analysis.

   Branches under then/else (or a dependency's schema) apply only when their condition holds. The plan supports
   conditions the pass itself decides (required names, property values tested against constants or a pattern, and
   names no property may match), and defers those branches' applications to a second step over the properties they
   touch.

   Fail-fast evaluation only. The fused pass does not enter its contributors as nodes (and so would not push their
   resources on the dynamic scope): below a live dynamic reference, only contributors in the node's own resource
   fuse. *)

open Node
open Program

(* ---------------------------------------------------------------------------------------------------------------------
   Construction *)

(* A contributor as collected: the branch, its condition, its alternative group and branch. *)
type collected_contributor = { cc_node : int; cc_condition : gate; cc_alt : alt_branch }

type pending_condition = {
  (* The if schema, or the dependency's property name. *)
  is_test : bool;
  test : int;
  dependency : string;
  pc_gate : gate;
}

type collected_extra = { ce_condition : int; ce_names : string array }
type collected_alternative = { ca_condition : gate; ca_exactly_one : bool; ca_branches : string array array }
type collected_forbidden = { cf_gate : gate; cf_names : string array }

(* What a collection gathers. *)
type collector = {
  p : Program.t;
  contributors : collected_contributor Vec.t;
  conditions : pending_condition Vec.t;
  (* Dependencies' required names. *)
  extras : collected_extra Vec.t;
  alternatives : collected_alternative Vec.t;
  alt_groups : fused_alt_group Vec.t;
  forbidden : collected_forbidden Vec.t;
  (* Alternative groups with object keywords only where coverage is not tracked (a failed branch must not cover). *)
  allow_alt_groups : bool;
  (* Every contributor must belong to this resource (below a live dynamic reference, where the general path would
     push a contributor's own resource on the dynamic scope and the fused pass does not enter contributors). *)
  same_resource : bool;
  resource : int;
}

let node c id = c.p.nodes.(id)
let target c id = c.p.fast_target.(id)
let other_resource c (n : Node.t) = c.same_resource && n.resource_id <> c.resource

let is_required_list_only (n : Node.t) =
  not
    (is_set n.const_value || n.has_enum || has_number_keywords n || has_string_keywords n || has_array_keywords n
   || has_in_place_applicators n || n.dynamic_ref <> None || n.unevaluated_properties >= 0 || n.unevaluated_items >= 0
    || (n.has_type && n.type_mask land type_object = 0)
    || n.has_pattern_properties || n.additional_properties >= 0 || n.property_names >= 0 || n.has_dependencies
    || n.min_properties >= 0 || n.max_properties >= 0
    || Array.length n.properties <> 0)

(* forbidden_names are the names of a not whose schema is a non-empty required list: an object fails when all are
   present. *)
let forbidden_names (p : Program.t) not_ =
  let n = p.nodes.(p.fast_target.(not_)) in
  if n.always_true || n.always_false || (not (is_required_list_only n)) || Array.length n.required = 0 then None
  else Some n.required

(* is_object_branch reports a branch whose only effect on an object instance is through object keywords and fusable
   in-place applicators. *)
let is_object_branch p (n : Node.t) allow_unevaluated =
  (n.not_ < 0 || forbidden_names p n.not_ <> None)
  && not
       (is_set n.const_value || n.has_enum || has_number_keywords n || has_string_keywords n
       || (n.has_type && n.type_mask land type_object = 0)
       || n.property_names >= 0 || n.dynamic_ref <> None
       || ((not allow_unevaluated) && n.unevaluated_properties >= 0))

(* is_leaf reports only local keywords (type, const, enum, number and string keywords). *)
let is_leaf (n : Node.t) =
  (not n.always_true) && (not n.always_false)
  && (not (has_object_keywords n))
  && (not (has_array_keywords n))
  && (not (has_in_place_applicators n))
  && n.dynamic_ref = None

let is_type_only (n : Node.t) =
  is_leaf n
  && (not (is_set n.const_value))
  && (not n.has_enum)
  && (not (has_number_keywords n))
  && not (has_string_keywords n)

(* type_ok reports whether a value is of one of the types in a mask. *)
let type_ok mask d x =
  let kind = Document.kind d x in
  mask land kind <> 0 || (kind = Document.kind_number && mask land type_integer <> 0 && Document.is_integer d x)

let no_test = { vt_condition = 0; is_pattern = false; allowed = [||]; vt_pattern = None; requires_string = false }

(* value_test_of is the value test for a property schema inside an if: const/enum of scalars (the values the
   schema's type admits), or a pattern with at most type: string. None for anything else. *)
let value_test_of (n : Node.t) =
  if
    n.always_true || n.always_false || has_number_keywords n || has_object_keywords n || has_array_keywords n
    || has_in_place_applicators n || n.dynamic_ref <> None
  then None
  else
    match n.pattern with
    | Some pattern ->
        let only_pattern =
          (not (is_set n.const_value))
          && (not n.has_enum) && n.min_length < 0 && n.max_length < 0
          && (not (n.assert_format && n.has_format))
          && (not n.assert_content)
          && ((not n.has_type) || n.type_mask = type_string)
        in
        if only_pattern then
          Some { no_test with is_pattern = true; vt_pattern = Some pattern; requires_string = n.has_type }
        else None
    | None ->
        if has_string_keywords n then None
        else begin
          let values =
            if is_set n.const_value then Some [| n.const_value |]
            else if Array.length n.enum_values <> 0 then Some n.enum_values
            else None
          in
          match values with
          | None -> None
          | Some values ->
              let scalar v =
                let kind = Document.kind v.d v.n in
                if kind = Document.kind_number then Document.is_integer v.d v.n
                else kind = Document.kind_string || kind = Document.kind_bool || kind = Document.kind_null
              in
              if not (Array.for_all scalar values) then None
              else
                (* A value the schema's type rejects never passes, so it is not allowed. *)
                Some
                  {
                    no_test with
                    allowed =
                      Array.of_list
                        (List.filter (fun v -> (not n.has_type) || type_ok n.type_mask v.d v.n) (Array.to_list values));
                  }
        end

(* type_union reports that every branch only tests the type (the general path decides the keyword by one mask
   test). *)
let type_union c list =
  Array.for_all
    (fun id ->
      let b = node c (target c id) in
      b.has_type && is_type_only b)
    list

(* is_simple_array reports an array of leaf items with at most size bounds: cheap to apply more than once. *)
let is_simple_array c (n : Node.t) =
  if
    (not (has_array_keywords n))
    || has_object_keywords n || has_in_place_applicators n || n.dynamic_ref <> None || is_set n.const_value
    || n.has_enum || n.has_prefix_items || n.contains >= 0 || n.unique_items || n.unevaluated_items >= 0 || n.items < 0
  then false
  else begin
    let item = node c (target c n.items) in
    item.always_true || is_leaf item
  end

(* expensive_children_disjoint reports whether no property name gets an expensive child (anything but a leaf or a
   boolean schema) from more than one branch. Pattern and additional properties count as every name. *)
let expensive_children_disjoint c branches =
  let cheap id =
    let n = node c (target c id) in
    n.always_true || n.always_false || is_leaf n || is_simple_array c n
  in
  let expensive = ref [] in
  let wildcards = ref 0 in
  let disjoint = ref true in
  Array.iter
    (fun b ->
      let n = node c b in
      Array.iter
        (fun (property : named_node) ->
          if not (cheap property.node) then
            if List.mem property.name !expensive then disjoint := false else expensive := property.name :: !expensive)
        n.properties;
      let wildcard =
        (n.additional_properties >= 0 && not (cheap n.additional_properties))
        || Array.exists (fun pp -> not (cheap pp.pp_node)) n.pattern_properties
      in
      if wildcard then begin
        incr wildcards;
        if !wildcards > 1 then disjoint := false
      end)
    branches;
  !disjoint && (!wildcards = 0 || !expensive = [])

(* supported_condition reports a condition the pass decides alone: required names, and properties whose schemas are
   value tests, optionally with type: object (which the object plan has established). At least one of the two. *)
let supported_condition c (test : Node.t) =
  if
    is_set test.const_value || test.has_enum || has_number_keywords test || has_string_keywords test
    || has_array_keywords test || has_in_place_applicators test || test.dynamic_ref <> None
    || (test.has_type && test.type_mask land type_object = 0)
    || test.additional_properties >= 0 || test.property_names >= 0 || test.unevaluated_properties >= 0
    || test.has_dependencies || test.min_properties >= 0 || test.max_properties >= 0
  then false
  else
    Array.for_all
      (fun (property : named_node) -> value_test_of (node c (target c property.node)) <> None)
      test.properties
    (* patternProperties: {P: false}: no property name may match P. *)
    && Array.for_all (fun pp -> (node c (target c pp.pp_node)).always_false) test.pattern_properties
    && (Array.length test.required <> 0
       || Array.length test.properties <> 0
       || Array.length test.pattern_properties <> 0)

(* collect_alternative fuses a oneOf/anyOf when every branch is a plain required list (decided from the seen names
   after the pass), or as an alternative group of object branches where coverage is not tracked. *)
let collect_alternative c list condition exactly_one discriminated =
  let branches = Array.map (target c) list in
  let required_only =
    Array.for_all
      (fun b ->
        let n = node c b in
        Array.length n.required <> 0 && is_required_list_only n)
      branches
  in
  if required_only then begin
    Vec.push c.alternatives
      {
        ca_condition = condition;
        ca_exactly_one = exactly_one;
        ca_branches = Array.map (fun b -> (node c b).required) branches;
      };
    true
  end
  else if
    (* Branches with object keywords: each a contributor whose failure marks the branch rather than the object. The
       pass applies every branch that knows a name to that property, where the general path stops at the first
       branch that passes, so a name with an expensive child in more than one branch is refused. A keyword the
       general path decides by discriminator or type stays with it. *)
    (not c.allow_alt_groups) || discriminated || condition.g_set
    || Vec.length c.alt_groups >= max_fused_alt_groups
    || Array.length branches > mask_bits
    || not (expensive_children_disjoint c branches)
  then false
  else begin
    let group = Vec.length c.alt_groups in
    let ok = ref true in
    Array.iteri
      (fun i b ->
        if !ok then begin
          let n = node c b in
          if
            n.always_true || n.always_false || n.in_place_cycle || has_in_place_applicators n || n.has_dependencies
            || n.not_ >= 0
            || (not (is_object_branch c.p n false))
            || other_resource c n
            || Vec.length c.contributors >= max_fused_contributors
          then ok := false
          else
            Vec.push c.contributors
              { cc_node = b; cc_condition = no_gate; cc_alt = { alt_set = true; group; branch = i } }
        end)
      branches;
    if !ok then Vec.push c.alt_groups { exactly_one; count = Array.length branches };
    !ok
  end

(* collect walks the in-place applicators of a branch, adding every object branch with its condition. It fails when
   a branch cannot be fused. *)
let rec collect c id condition =
  let n = node c id in
  if n.always_true then true
  else if n.always_false || n.in_place_cycle || not (is_object_branch c.p n (Vec.length c.contributors = 0)) then false
  else if Vec.length c.contributors >= max_fused_contributors || other_resource c n then false
  else begin
    Vec.push c.contributors { cc_node = id; cc_condition = condition; cc_alt = no_alt };
    if n.not_ >= 0 then
      Vec.push c.forbidden
        { cf_gate = condition; cf_names = (match forbidden_names c.p n.not_ with Some names -> names | None -> [||]) };
    (n.ref_ < 0 || collect c (target c n.ref_) condition)
    && (n.static_dynamic_ref < 0 || collect c (target c n.static_dynamic_ref) condition)
    && Array.for_all (fun child -> collect c (target c child) condition) n.all_of
    && ((not n.has_one_of)
       || collect_alternative c n.one_of condition true (n.one_of_discriminator <> None || type_union c n.one_of))
    && ((not n.has_any_of)
       || collect_alternative c n.any_of condition false (n.any_of_discriminator <> None || type_union c n.any_of))
    && Array.for_all
         (fun (dep : dependency_entry) ->
           if Vec.length c.conditions >= max_fused_conditions then false
           else begin
             let at = Vec.length c.conditions in
             Vec.push c.conditions { is_test = false; test = no_node; dependency = dep.dep_name; pc_gate = condition };
             if Array.length dep.dep_required <> 0 then
               Vec.push c.extras { ce_condition = at; ce_names = dep.dep_required };
             dep.schema < 0 || collect c (target c dep.schema) { g_set = true; g_condition = at; polarity = true }
           end)
         n.dependencies
    &&
    if n.if_ < 0 then true
    else begin
      let test_id = target c n.if_ in
      let test = node c test_id in
      let then_ = if n.then_ >= 0 then target c n.then_ else no_node in
      let else_ = if n.else_ >= 0 then target c n.else_ else no_node in
      if test.always_true then then_ < 0 || collect c then_ condition
      else if test.always_false then else_ < 0 || collect c else_ condition
      else if (not (supported_condition c test)) || Vec.length c.conditions >= max_fused_conditions then false
      else begin
        let at = Vec.length c.conditions in
        Vec.push c.conditions { is_test = true; test = test_id; dependency = ""; pc_gate = condition };
        (* When the condition holds, the if schema's own properties count as evaluated, so it contributes under the
           same condition as then. *)
        ((not test.has_properties) || collect c test_id { g_set = true; g_condition = at; polarity = true })
        && (then_ < 0 || collect c then_ { g_set = true; g_condition = at; polarity = true })
        && (else_ < 0 || collect c else_ { g_set = true; g_condition = at; polarity = false })
      end
    end
  end

(* One resolution of a property by a contributor, before identical ones are merged. *)
type collected_app = { ca_contributor : int; ca_child : opt_child; ca_target : int }

(* merge_value_tests is the merged constants of an entry's value tests, when some (of at most mask_bits) are
   constant sets and there is more than one constant to look for. *)
let merge_value_tests (tests : value_test array) =
  let constants =
    Array.fold_left (fun k (t : value_test) -> if t.is_pattern then k else k + Array.length t.allowed) 0 tests
  in
  if Array.length tests > mask_bits || constants < 2 then None
  else begin
    let keyed = ref 0 in
    (* Last first. *)
    let strings = ref [] in
    let others = ref [] in
    Array.iteri
      (fun t (test : value_test) ->
        if not test.is_pattern then begin
          keyed := !keyed lor (1 lsl t);
          Array.iter
            (fun v ->
              if Document.kind v.d v.n = Document.kind_string then begin
                let s = Document.string_value v.d v.n in
                match List.find_opt (fun (text, _) -> String.equal text s) !strings with
                | Some (_, mask) -> mask := !mask lor (1 lsl t)
                | None -> strings := (s, ref (1 lsl t)) :: !strings
              end
              else
                match List.find_opt (fun o -> Values.values_equal o.mv_value.d o.mv_value.n v.d v.n) !others with
                | Some o -> o.mv_mask <- o.mv_mask lor (1 lsl t)
                | None -> others := { mv_value = v; mv_mask = 1 lsl t } :: !others)
            test.allowed
        end)
      tests;
    let strings = Array.of_list (List.rev !strings) in
    Some
      {
        m_strings = Names.create (Array.map fst strings);
        string_masks = Array.map (fun (_, mask) -> !mask) strings;
        m_others = Array.of_list (List.rev !others);
        keyed = !keyed;
      }
  end

(* coalesce_apps merges identical resolutions of a property from several branches (the same child, or both true)
   into one application listing every branch, applied once when any of them is active. Branches of an alternative
   group merge only within the same branch. The primary contributor is an unconditional one when there is one, so
   the pass applies it at once. *)
let coalesce_apps (apps : collected_app array) (contributors : fused_contributor array) =
  let same a b =
    if (not a.ca_child.oc_set) || not b.ca_child.oc_set then a.ca_child.oc_set = b.ca_child.oc_set
    else begin
      let x = a.ca_child.oc_child and y = b.ca_child.oc_child in
      a.ca_target = b.ca_target || (x.shape = shape_trivial && y.shape = shape_trivial && x.types = y.types)
    end
  in
  let used = Array.make (Array.length apps) false in
  let out = ref [] in
  Array.iteri
    (fun i _ ->
      if not used.(i) then begin
        let primary = ref i in
        let others = ref [] in
        for j = i + 1 to Array.length apps - 1 do
          let a = contributors.(apps.(!primary).ca_contributor) and b = contributors.(apps.(j).ca_contributor) in
          if (not used.(j)) && same apps.(!primary) apps.(j) && a.alt = b.alt then begin
            used.(j) <- true;
            if a.condition.g_set && not b.condition.g_set then begin
              others := apps.(!primary).ca_contributor :: !others;
              primary := j
            end
            else others := apps.(j).ca_contributor :: !others
          end
        done;
        let app = apps.(!primary) in
        out :=
          {
            contributor = app.ca_contributor;
            has_child = app.ca_child.oc_set;
            app_child = app.ca_child.oc_child;
            app_others = Array.of_list (List.rev !others);
            app_then = 0;
            app_els = 0;
          }
          :: !out
      end)
    apps;
  Array.of_list (List.rev !out)

(* try_fuse builds the fused plan for a node, or None when it cannot be fused or fusing does not pay. *)
let try_fuse (p : Program.t) id same_resource (child_of : int -> child) =
  let n = p.nodes.(id) in
  if (not (is_object_branch p n true)) || n.in_place_cycle || n.always_true || n.always_false then None
  else begin
    let ctx =
      {
        p;
        contributors = Vec.create ();
        conditions = Vec.create ();
        extras = Vec.create ();
        alternatives = Vec.create ();
        alt_groups = Vec.create ();
        forbidden = Vec.create ();
        allow_alt_groups = n.unevaluated_properties < 0;
        same_resource;
        resource = n.resource_id;
      }
    in
    if not (collect ctx id no_gate) then None
    else if
      Vec.length ctx.contributors + Vec.length ctx.extras > max_fused_contributors
      || Vec.length ctx.conditions > max_fused_conditions
    then None
    else begin
      let contributors = Vec.to_array ctx.contributors in
      let conditions = Vec.to_array ctx.conditions in
      let collected_alternatives = Vec.to_array ctx.alternatives in
      let alt_groups = Vec.to_array ctx.alt_groups in
      (* Fusing pays when a pass over every property is unavoidable (unevaluatedProperties) or when it replaces
         several passes: two or more branches with object keywords, an if the seen names decide, or alternatives. A
         node whose object keywords are all its own keeps its object plan. *)
      let has_if = Array.exists (fun c -> c.is_test) conditions in
      let effective =
        Array.fold_left
          (fun k contributor ->
            let b = p.nodes.(contributor.cc_node) in
            if
              b.has_properties || b.has_pattern_properties || b.additional_properties >= 0
              || Array.length b.required <> 0
              || b.min_properties >= 0 || b.max_properties >= 0
            then k + 1
            else k)
          0 contributors
      in
      if
        n.unevaluated_properties < 0 && (not has_if)
        && Array.length collected_alternatives = 0
        && Array.length alt_groups = 0
        && effective < 2
      then None
      else begin
        (* Every name any branch or condition knows gets an index. *)
        let known = Vec.create () in
        let bit name =
          let found = ref (-1) in
          for i = Vec.length known - 1 downto 0 do
            if String.equal (Vec.get known i) name then found := i
          done;
          if !found >= 0 then !found
          else begin
            Vec.push known name;
            Vec.length known - 1
          end
        in
        (* In order, so that the names are numbered as Go numbers them. *)
        let bits_of list =
          let out = Array.make (Array.length list) 0 in
          for i = 0 to Array.length list - 1 do
            out.(i) <- bit list.(i)
          done;
          out
        in
        Array.iter
          (fun contributor ->
            let b = p.nodes.(contributor.cc_node) in
            Array.iter (fun (property : named_node) -> ignore (bit property.name : int)) b.properties;
            Array.iter (fun name -> ignore (bit name : int)) b.required)
          contributors;
        let absent = ref [] in
        (* The tests of each condition, with the entry each is for. *)
        let tests_by_entry = ref [] in
        let f_conditions =
          Array.mapi
            (fun i pending ->
              let required =
                if pending.is_test then begin
                  let test = p.nodes.(pending.test) in
                  Array.iter
                    (fun pp -> absent := { fa_condition = i; fa_pattern = pp.pp_pattern } :: !absent)
                    test.pattern_properties;
                  Array.iter
                    (fun (property : named_node) ->
                      let vt =
                        match value_test_of p.nodes.(p.fast_target.(property.node)) with
                        | Some vt -> vt
                        | None -> no_test
                      in
                      let entry = bit property.name in
                      tests_by_entry := (entry, { vt with vt_condition = i }) :: !tests_by_entry)
                    test.properties;
                  bits_of test.required
                end
                else [| bit pending.dependency |]
              in
              { fc_required = required; fc_gate = pending.pc_gate })
            conditions
        in
        let absent = Array.of_list (List.rev !absent) in
        let tests_by_entry = List.rev !tests_by_entry in
        let forbidden =
          Array.map (fun fb -> { ff_gate = fb.cf_gate; ff_names = bits_of fb.cf_names }) (Vec.to_array ctx.forbidden)
        in
        let extras = Array.map (fun extra -> (extra.ce_condition, bits_of extra.ce_names)) (Vec.to_array ctx.extras) in
        let alternatives =
          Array.map
            (fun a ->
              let branches = Array.make (Array.length a.ca_branches) [||] in
              for i = 0 to Array.length branches - 1 do
                branches.(i) <- bits_of a.ca_branches.(i)
              done;
              { al_condition = a.ca_condition; al_exactly_one = a.ca_exactly_one; al_branches = branches })
            collected_alternatives
        in
        if Vec.length known > max_fused_names then None
        else begin
          let app_child c =
            let t = p.fast_target.(c) in
            if p.nodes.(t).always_true then no_opt_child else { oc_set = true; oc_child = child_of t }
          in
          let branch_contributors =
            Array.map
              (fun contributor ->
                let b = p.nodes.(contributor.cc_node) in
                let required = bits_of b.required in
                let patterns =
                  Array.map
                    (fun pp -> { fp_pattern = pp.pp_pattern; fp_child = app_child pp.pp_node })
                    b.pattern_properties
                in
                {
                  condition = contributor.cc_condition;
                  alt = contributor.cc_alt;
                  c_patterns = patterns;
                  c_has_additional = b.additional_properties >= 0;
                  c_additional =
                    (if b.additional_properties >= 0 then app_child b.additional_properties else no_opt_child);
                  c_required = required;
                  c_min = b.min_properties;
                  c_max = b.max_properties;
                  c_then = 0;
                  c_els = 0;
                })
              contributors
          in
          let extra_contributors =
            Array.map
              (fun (condition, names) ->
                {
                  condition = { g_set = true; g_condition = condition; polarity = true };
                  alt = no_alt;
                  c_patterns = [||];
                  c_has_additional = false;
                  c_additional = no_opt_child;
                  c_required = names;
                  c_min = no_count;
                  c_max = no_count;
                  c_then = 0;
                  c_els = 0;
                })
              extras
          in
          let f_contributors = Array.append branch_contributors extra_contributors in
          let known = Vec.to_array known in
          let scratch = Pattern_engine.create_limited_scratch p.pattern_match_limit p.pattern_match_clock in
          let tests =
            Array.mapi
              (fun e name ->
                let own = List.filter_map (fun (entry, t) -> if entry = e then Some t else None) tests_by_entry in
                (* A known name matching an absent pattern fails its condition whatever its value: no constant is
                   allowed. *)
                let never =
                  List.filter_map
                    (fun a ->
                      if Pattern.match_string a.fa_pattern scratch name then
                        Some { no_test with vt_condition = a.fa_condition }
                      else None)
                    (Array.to_list absent)
                in
                Array.of_list (own @ never))
              known
          in
          (* Resolve every known name against every branch now. *)
          let entries =
            Array.mapi
              (fun i name ->
                let apps = ref [] in
                Array.iteri
                  (fun c contributor ->
                    let b = p.nodes.(contributor.cc_node) in
                    let matched = ref false in
                    (match
                       Array.find_opt (fun (property : named_node) -> String.equal property.name name) b.properties
                     with
                    | Some property ->
                        matched := true;
                        apps :=
                          {
                            ca_contributor = c;
                            ca_child = app_child property.node;
                            ca_target = p.fast_target.(property.node);
                          }
                          :: !apps
                    | None -> ());
                    Array.iter
                      (fun pp ->
                        if Pattern.match_string pp.pp_pattern scratch name then begin
                          matched := true;
                          apps :=
                            {
                              ca_contributor = c;
                              ca_child = app_child pp.pp_node;
                              ca_target = p.fast_target.(pp.pp_node);
                            }
                            :: !apps
                        end)
                      b.pattern_properties;
                    if (not !matched) && b.additional_properties >= 0 then begin
                      let a = b.additional_properties in
                      apps := { ca_contributor = c; ca_child = app_child a; ca_target = p.fast_target.(a) } :: !apps
                    end)
                  contributors;
                {
                  apps = coalesce_apps (Array.of_list (List.rev !apps)) f_contributors;
                  tests = tests.(i);
                  merged = merge_value_tests tests.(i);
                })
              known
          in
          let flat =
            Array.length f_conditions = 0
            && Array.length alternatives = 0
            && Array.length alt_groups = 0
            && Array.length forbidden = 0
            && n.unevaluated_properties < 0
            && Array.length known <= mask_bits
            && Array.for_all
                 (fun c -> (not c.condition.g_set) && Array.length c.c_patterns = 0 && not c.c_has_additional)
                 f_contributors
            && Array.for_all (fun e -> Array.length e.apps <= 1 && Array.length e.tests = 0) entries
          in
          let resolves_unknown =
            Array.exists (fun c -> Array.length c.c_patterns <> 0 || c.c_has_additional) f_contributors
            || Array.length absent <> 0
          in
          let has_count_bounds = Array.exists (fun c -> c.c_min >= 0 || c.c_max >= 0) f_contributors in
          let names = Names.create known in
          let finals = ref [] in
          Array.iteri
            (fun i c ->
              if c.condition.g_set && c.condition.polarity then c.c_then <- 1 lsl c.condition.g_condition
              else if c.condition.g_set then c.c_els <- 1 lsl c.condition.g_condition;
              if Array.length c.c_required <> 0 || c.c_min >= 0 || c.c_max >= 0 then finals := i :: !finals)
            f_contributors;
          Array.iter
            (fun e ->
              Array.iter
                (fun app ->
                  app.app_then <- f_contributors.(app.contributor).c_then;
                  app.app_els <- f_contributors.(app.contributor).c_els;
                  Array.iter
                    (fun other ->
                      app.app_then <- app.app_then lor f_contributors.(other).c_then;
                      app.app_els <- app.app_els lor f_contributors.(other).c_els)
                    app.app_others)
                e.apps)
            entries;
          let flat_plan =
            if not flat then None
            else begin
              let o =
                {
                  o_min = 0;
                  o_max = max_int;
                  visit = visit_names;
                  o_names = names;
                  declared = Array.length known;
                  o_children =
                    Array.map
                      (fun e ->
                        if Array.length e.apps <> 0 && e.apps.(0).has_child then e.apps.(0).app_child else no_child)
                      entries;
                  required_mask = 0;
                  o_required = [||];
                  patterns = [||];
                  name_patterns = Array.make (Array.length known) [||];
                  has_additional = false;
                  additional = no_child;
                  property_names = no_node;
                  o_dependencies = [||];
                  rest_free = true;
                  strict = true;
                  (* No contributor has additionalProperties (flat requires it). *)
                  lookup = Array.length known <= lookup_names;
                }
              in
              Array.iter
                (fun c ->
                  Array.iter (fun r -> o.required_mask <- o.required_mask lor (1 lsl r)) c.c_required;
                  if c.c_min >= 0 && c.c_min > o.o_min then o.o_min <- c.c_min;
                  if c.c_max >= 0 && c.c_max < o.o_max then o.o_max <- c.c_max)
                f_contributors;
              Some o
            end
          in
          Some
            {
              flat = flat_plan;
              f_names = names;
              entries;
              contributors = f_contributors;
              conditions = f_conditions;
              alternatives;
              alt_groups;
              forbidden;
              absent;
              finals = Array.of_list (List.rev !finals);
              resolves_unknown;
              has_count_bounds;
              has_unevaluated = n.unevaluated_properties >= 0;
              unevaluated =
                (if n.unevaluated_properties >= 0 then child_of p.fast_target.(n.unevaluated_properties) else no_child);
            }
        end
      end
    end
  end
