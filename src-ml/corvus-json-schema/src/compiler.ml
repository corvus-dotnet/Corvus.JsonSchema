(* Compiles loaded schema documents into a graph of nodes and runs the compile-time analyses. A port of compiler.go of
   the Go module. *)

open Document
open Node

(* is_known_keyword reports the keywords compile_node handles itself. Anything else is an unknown keyword (an
   annotation from 2019-09). *)
let is_known_keyword = function
  | "if" | "not" | "type" | "enum" | "$ref" | "then" | "else" | "const" | "items" | "allOf" | "anyOf" | "oneOf"
  | "title" | "$defs" | "format" | "pattern" | "maximum" | "minimum" | "default" | "$schema" | "$anchor" | "required"
  | "contains" | "maxItems" | "minItems" | "examples" | "readOnly" | "$comment" | "maxLength" | "minLength"
  | "writeOnly" | "properties" | "multipleOf" | "deprecated" | "uniqueItems" | "prefixItems" | "minContains"
  | "maxContains" | "description" | "$vocabulary" | "definitions" | "$dynamicRef" | "dependencies" | "propertyNames"
  | "maxProperties" | "minProperties" | "contentSchema" | "$recursiveRef" | "$dynamicAnchor" | "contentEncoding"
  | "additionalItems" | "exclusiveMaximum" | "exclusiveMinimum" | "unevaluatedItems" | "contentMediaType"
  | "$recursiveAnchor" | "dependentSchemas" | "patternProperties" | "dependentRequired" | "additionalProperties"
  | "unevaluatedProperties" | "id" | "$id" ->
      true
  | _ -> false

type pending_dynamic_ref = {
  p_node : int;
  anchor : string;
  p_is_recursive : bool;
  initial_target : Loader.target;
  mutable seen_resources : int;
  mutable candidates : resource_node list;
}

(* What a node's annotations are computed from, on the first evaluation with a collector. *)
type annotation_source = { a_doc : Document.t; a_value : int; a_vocab : int; a_content : bool }

(* The compiled program: the node graph, its entry node and whether it keeps a dynamic scope. *)
type compiled = {
  nodes : Node.t array;
  root : int;
  uses_dynamic_scope : bool;
  annotation_sources : annotation_source option array;
  (* Whether the assert format option was given, which makes format an annotation in every dialect. *)
  assert_format_set : bool;
}

type t = {
  loader : Loader.t;
  options : Options.t;
  nodes : Node.t Vec.t;
  targets : Loader.target Vec.t;
  node_of : (int * int, int) Hashtbl.t;
  mutable worklist_head : int;
  mutable pending_dynamic_refs : pending_dynamic_ref list;
  annotation_sources : annotation_source option Vec.t;
  mutable entry_node : int;
  (* Each pattern of the schema, compiled once. Go keeps one cache for the process, and this library has no global
     state, so the cache is the compilation's. *)
  patterns : (string, Pattern.t option) Hashtbl.t;
}

let compile_pattern c source =
  match Hashtbl.find_opt c.patterns source with
  | Some p -> p
  | None ->
      let p = Pattern.compile source in
      Hashtbl.replace c.patterns source p;
      p

(* uint_value is a non-negative integer keyword value, or no_count. *)
let uint_value d n =
  if n < 0 || kind d n <> kind_number then no_count
  else begin
    let flags = flags d n in
    let c = Numbers.class_of flags in
    if c = Numbers.num_int then if data d n >= 0 then data d n else no_count
    else if c = Numbers.num_big then if Numbers.top_of flags >= 3 then max_int else no_count
    else begin
      let f = float_value d n in
      if f >= 0. && f = Float.floor f && f < 1.8e19 then if f >= Numbers.two62 then max_int else Float.to_int f
      else no_count
    end
  end

let type_mask_of d n =
  if kind d n <> kind_string then 0
  else
    match Document.string_value d n with
    | "null" -> type_null
    | "boolean" -> type_boolean
    | "object" -> type_object
    | "array" -> type_array
    | "number" -> type_number
    | "string" -> type_string
    | "integer" -> type_integer
    | _ -> 0

(* string_items are the strings of an array (other items are skipped). *)
let string_items d array =
  let out = ref [] in
  let c = first d array in
  for i = count d array - 1 downto 0 do
    if kind d (c + i) = kind_string then out := Document.string_value d (c + i) :: !out
  done;
  Array.of_list !out

let contains_string list s = Array.exists (String.equal s) list

let get_node c (target : Loader.target) =
  let key = (target.t_document, target.t_value) in
  match Hashtbl.find_opt c.node_of key with
  | Some id -> id
  | None ->
      let id = Vec.length c.nodes in
      let resource = Loader.resource c.loader target.t_resource in
      Vec.push c.nodes (Node.create target.t_resource resource.Loader.dialect target.t_pointer);
      Vec.push c.targets target;
      Vec.push c.annotation_sources None;
      Hashtbl.replace c.node_of key id;
      id

let child c (parent : Loader.target) value relative =
  let resource =
    match Loader.resource_of c.loader parent.t_document value with Some r -> r | None -> parent.t_resource
  in
  get_node c
    {
      Loader.t_document = parent.t_document;
      t_pointer = parent.t_pointer ^ relative;
      t_value = value;
      t_resource = resource;
    }

(* child_array is the nodes of an array of subschemas, or None when the value is not an array. *)
let child_array c parent d value keyword =
  if not (Loader.is_kind d value kind_array) then None
  else begin
    let first = first d value in
    let out = Array.make (count d value) no_node in
    (* In order, so that the nodes are numbered as Go numbers them. *)
    for i = 0 to Array.length out - 1 do
      out.(i) <- child c parent (first + i) ("/" ^ keyword ^ "/" ^ string_of_int i)
    done;
    Some out
  end

let resolve_or_fail c (target : Loader.target) reference =
  match Loader.try_resolve_reference c.loader target.t_resource reference with
  | Some t -> t
  | None ->
      Options.compile_error
        ("Unable to resolve reference '" ^ reference ^ "' from '"
       ^ (Loader.resource c.loader target.t_resource).Loader.uri ^ "'.")

let compile_ref c id target reference =
  let resolved = resolve_or_fail c target reference in
  (Vec.get c.nodes id).ref_ <- get_node c resolved

let dynamic_keyword is_recursive = if is_recursive then "$recursiveRef" else "$dynamicRef"

let compile_dynamic_ref c id target reference is_recursive =
  let resolved = resolve_or_fail c target reference in
  let _, raw_fragment = Uri.split_fragment reference in
  let fragment = Uri.decode_fragment raw_fragment in
  let res = Loader.resource c.loader resolved.t_resource in
  let dynamic =
    if is_recursive then res.Loader.recursive_anchor && resolved.t_pointer = res.Loader.root_pointer
    else if fragment <> "" && fragment.[0] <> '/' then
      match Hashtbl.find_opt res.Loader.dynamic_anchors fragment with
      | Some pointer -> pointer = resolved.t_pointer
      | None -> false
    else false
  in
  if not dynamic then begin
    (* A static reference, kept apart from any sibling $ref (both apply). *)
    let n = Vec.get c.nodes id in
    n.static_dynamic_ref <- get_node c resolved;
    n.static_dynamic_keyword <- dynamic_keyword is_recursive
  end
  else
    c.pending_dynamic_refs <-
      c.pending_dynamic_refs
      @ [
          {
            p_node = id;
            anchor = fragment;
            p_is_recursive = is_recursive;
            initial_target = resolved;
            seen_resources = 0;
            candidates = [];
          };
        ]

let compile_dependency_schemas c target d e dialect =
  let out = ref [] in
  let deps = property d e "dependencies" in
  if Loader.is_kind d deps kind_object then begin
    let k = first d deps in
    for i = 0 to count d deps - 1 do
      let name = Document.string_value d (k + (2 * i)) in
      let v = k + (2 * i) + 1 in
      if kind d v = kind_array then
        out :=
          {
            dep_keyword = keyword_dependencies;
            dep_name = name;
            has_required = true;
            dep_required = string_items d v;
            schema = no_node;
          }
          :: !out
      else begin
        let node = child c target v ("/dependencies/" ^ Uri.escape_pointer_token name) in
        out :=
          {
            dep_keyword = keyword_dependencies;
            dep_name = name;
            has_required = false;
            dep_required = [||];
            schema = node;
          }
          :: !out
      end
    done
  end;
  if Dialect.rank dialect >= 3 then begin
    let ds = property d e "dependentSchemas" in
    if Loader.is_kind d ds kind_object then begin
      let k = first d ds in
      for i = 0 to count d ds - 1 do
        let name = Document.string_value d (k + (2 * i)) in
        let node = child c target (k + (2 * i) + 1) ("/dependentSchemas/" ^ Uri.escape_pointer_token name) in
        out :=
          {
            dep_keyword = keyword_dependent_schemas;
            dep_name = name;
            has_required = false;
            dep_required = [||];
            schema = node;
          }
          :: !out
      done
    end
  end;
  List.rev !out

let compile_node c id (target : Loader.target) =
  let d = Loader.document c.loader target.t_document in
  let e = target.t_value in
  let n = Vec.get c.nodes id in
  let k = kind d e in
  if k = kind_bool then begin
    n.always_true <- boolean d e;
    n.always_false <- not n.always_true
  end
  else if k <> kind_object then n.always_true <- true
  else begin
    let get name = property d e name in
    let has name = property d e name >= 0 in
    let resource = Loader.resource c.loader target.t_resource in
    let dialect = resource.Loader.dialect and voc = resource.Loader.vocabularies in
    let rank = Dialect.rank dialect in
    let legacy = Dialect.is_legacy dialect in
    match if legacy then Loader.string_value d (get "$ref") else None with
    | Some r ->
        (* In draft 7 and earlier, $ref replaces every sibling keyword. *)
        compile_ref c id target r
    | None ->
        let applicator = legacy || voc land Dialect.vocab_applicator <> 0 in
        let validation = legacy || voc land Dialect.vocab_validation <> 0 in
        let unevaluated =
          if dialect = Dialect.Draft2019_09 then applicator else voc land Dialect.vocab_unevaluated <> 0
        in
        let content = legacy || voc land Dialect.vocab_content <> 0 in
        let format_assert =
          if c.options.Options.assert_format <> 0 then c.options.Options.assert_format > 0
          else
            (legacy && c.options.Options.assert_format_in_legacy_drafts) || voc land Dialect.vocab_format_assertion <> 0
        in
        let single name =
          let v = get name in
          if v >= 0 then child c target v ("/" ^ Uri.escape_pointer_token name) else no_node
        in
        let dependencies = ref [] in
        let has_dependencies = ref false in

        (* References. *)
        (match Loader.string_value d (get "$ref") with Some r -> compile_ref c id target r | None -> ());
        if rank >= 4 then
          begin match Loader.string_value d (get "$dynamicRef") with
          | Some r -> compile_dynamic_ref c id target r false
          | None -> ()
          end;
        if dialect = Dialect.Draft2019_09 then
          begin match Loader.string_value d (get "$recursiveRef") with
          | Some r -> compile_dynamic_ref c id target r true
          | None -> ()
          end;

        (* Applicators. *)
        if applicator then begin
          (match child_array c target d (get "allOf") "allOf" with
          | Some a ->
              n.all_of <- a;
              n.has_all_of <- true
          | None -> ());
          (match child_array c target d (get "anyOf") "anyOf" with
          | Some a ->
              n.any_of <- a;
              n.has_any_of <- true
          | None -> ());
          (match child_array c target d (get "oneOf") "oneOf" with
          | Some a ->
              n.one_of <- a;
              n.has_one_of <- true
          | None -> ());
          n.not_ <- single "not";
          if rank >= 2 then begin
            n.if_ <- single "if";
            if n.if_ >= 0 then begin
              n.then_ <- single "then";
              n.else_ <- single "else"
            end
          end;
          let props = get "properties" in
          if Loader.is_kind d props kind_object then begin
            n.has_properties <- true;
            let k = first d props in
            let out = Array.make (count d props) { name = ""; node = no_node } in
            for i = 0 to Array.length out - 1 do
              let name = Document.string_value d (k + (2 * i)) in
              let node = child c target (k + (2 * i) + 1) ("/properties/" ^ Uri.escape_pointer_token name) in
              out.(i) <- { name; node }
            done;
            n.properties <- out
          end;
          let pp = get "patternProperties" in
          if Loader.is_kind d pp kind_object then begin
            n.has_pattern_properties <- true;
            let k = first d pp in
            let out = ref [] in
            for i = 0 to count d pp - 1 do
              let source = Document.string_value d (k + (2 * i)) in
              match compile_pattern c source with
              | None -> Options.compile_error ("Invalid regular expression '" ^ source ^ "' in patternProperties.")
              | Some pattern ->
                  let node =
                    child c target (k + (2 * i) + 1) ("/patternProperties/" ^ Uri.escape_pointer_token source)
                  in
                  out := { pp_pattern = pattern; pp_node = node } :: !out
            done;
            n.pattern_properties <- Array.of_list (List.rev !out)
          end;
          n.additional_properties <- single "additionalProperties";
          if rank >= 1 then begin
            n.property_names <- single "propertyNames";
            n.contains <- single "contains"
          end;

          (* "dependencies" is honoured in every dialect: in 2019-09 and later it is an optional compatibility
             keyword. *)
          if
            Loader.is_kind d (get "dependencies") kind_object
            || (rank >= 3 && Loader.is_kind d (get "dependentSchemas") kind_object)
          then begin
            dependencies := compile_dependency_schemas c target d e dialect;
            has_dependencies := true
          end;

          (* Array applicators. *)
          let set_prefix v keyword =
            match child_array c target d v keyword with
            | Some a ->
                n.prefix_items <- a;
                n.has_prefix_items <- true
            | None -> ()
          in
          if rank >= 4 then begin
            set_prefix (get "prefixItems") "prefixItems";
            let v = get "items" in
            if v >= 0 && kind d v <> kind_array then n.items <- single "items"
          end
          else begin
            let v = get "items" in
            if v >= 0 then
              if kind d v = kind_array then begin
                set_prefix v "items";
                n.prefix_keyword <- "items";
                n.items <- single "additionalItems";
                if n.items >= 0 then n.items_keyword <- "additionalItems"
              end
              else n.items <- single "items"
          end;
          n.contains_marks_evaluated <- rank >= 4
        end;

        if unevaluated && rank >= 3 then begin
          n.unevaluated_properties <- single "unevaluatedProperties";
          n.unevaluated_items <- single "unevaluatedItems"
        end;

        if validation then begin
          let t = get "type" in
          if t >= 0 then begin
            if kind d t = kind_array then begin
              let first = first d t in
              for i = 0 to count d t - 1 do
                n.type_mask <- n.type_mask lor type_mask_of d (first + i)
              done
            end
            else n.type_mask <- type_mask_of d t;
            n.has_type <- true
          end;
          if rank >= 1 then begin
            let v = get "const" in
            if v >= 0 then n.const_value <- { d; n = v }
          end;
          let values = get "enum" in
          if Loader.is_kind d values kind_array then begin
            n.has_enum <- true;
            let first = first d values in
            n.enum_values <- Array.init (count d values) (fun i -> { d; n = first + i })
          end;
          let req = get "required" in
          if Loader.is_kind d req kind_array then begin
            n.has_required <- true;
            n.required_list <- string_items d req;
            let unique = ref [] in
            Array.iter (fun r -> if not (List.mem r !unique) then unique := r :: !unique) n.required_list;
            n.required <- Array.of_list (List.rev !unique)
          end;
          if rank >= 3 then begin
            let dr = get "dependentRequired" in
            if Loader.is_kind d dr kind_object then begin
              has_dependencies := true;
              let k = first d dr in
              for i = 0 to count d dr - 1 do
                let v = k + (2 * i) + 1 in
                if kind d v = kind_array then
                  dependencies :=
                    !dependencies
                    @ [
                        {
                          dep_keyword = keyword_dependent_required;
                          dep_name = Document.string_value d (k + (2 * i));
                          has_required = true;
                          dep_required = string_items d v;
                          schema = no_node;
                        };
                      ]
              done
            end
          end;
          n.min_properties <- uint_value d (get "minProperties");
          n.max_properties <- uint_value d (get "maxProperties");
          n.min_items <- uint_value d (get "minItems");
          n.max_items <- uint_value d (get "maxItems");
          (let v = get "uniqueItems" in
           if Loader.is_kind d v kind_bool then n.unique_items <- boolean d v);
          n.min_length <- uint_value d (get "minLength");
          n.max_length <- uint_value d (get "maxLength");
          (match Loader.string_value d (get "pattern") with
          | Some p -> (
              match compile_pattern c p with
              | None -> Options.compile_error ("Invalid regular expression '" ^ p ^ "' in pattern.")
              | Some compiled -> n.pattern <- Some compiled)
          | None -> ());
          let num name =
            let v = get name in
            if Loader.is_kind d v kind_number then { d; n = v } else no_value
          in
          n.multiple_of <- num "multipleOf";
          if is_set n.multiple_of then begin
            let m = n.multiple_of.n in
            n.divisor <- Some (Numbers.new_divisor (flags d m) (data d m) (source d) (number_start d m))
          end;
          let is_true name =
            let v = get name in
            Loader.is_kind d v kind_bool && boolean d v
          in
          if dialect = Dialect.Draft4 then begin
            (* Draft 4: exclusiveMaximum/exclusiveMinimum are booleans that make maximum/minimum exclusive. *)
            if is_true "exclusiveMaximum" then n.exclusive_maximum <- num "maximum" else n.maximum <- num "maximum";
            if is_true "exclusiveMinimum" then n.exclusive_minimum <- num "minimum" else n.minimum <- num "minimum"
          end
          else begin
            n.maximum <- num "maximum";
            n.minimum <- num "minimum";
            n.exclusive_maximum <- num "exclusiveMaximum";
            n.exclusive_minimum <- num "exclusiveMinimum"
          end;
          if rank >= 3 && n.contains >= 0 then begin
            if has "minContains" then begin
              let v = uint_value d (get "minContains") in
              n.min_contains <- (if v >= 0 then v else 1)
            end;
            if has "maxContains" then n.max_contains <- uint_value d (get "maxContains")
          end
        end;

        (match Loader.string_value d (get "format") with
        | Some f ->
            n.has_format <- true;
            n.format <- f;
            n.format_kind <- Formats.kind_of f rank;
            n.assert_format <- format_assert
        | None -> ());

        (* Content keywords are asserted only in draft 7 (and annotations elsewhere). *)
        if content && rank >= 2 && (has "contentEncoding" || has "contentMediaType") then begin
          let base64 = Loader.string_value d (get "contentEncoding") = Some "base64" in
          let json = Loader.string_value d (get "contentMediaType") = Some "application/json" in
          n.content <-
            (if base64 && json then content_base64_json
             else if base64 then content_base64
             else if json then content_json
             else content_none);
          n.assert_content <- dialect = Dialect.Draft7 && c.options.Options.assert_content && n.content <> content_none
        end;

        if !has_dependencies then begin
          (* In the order the three keywords appear in the schema. *)
          let order keyword =
            let k = first d e in
            let found = ref max_int in
            for i = count d e - 1 downto 0 do
              if str_equals d (k + (2 * i)) keyword then found := i
            done;
            !found
          in
          dependencies :=
            List.stable_sort (fun a b -> Int.compare (order a.dep_keyword) (order b.dep_keyword)) !dependencies
        end;
        n.dependencies <- Array.of_list !dependencies;
        n.has_dependencies <- !has_dependencies;
        Vec.set c.annotation_sources id (Some { a_doc = d; a_value = e; a_vocab = voc; a_content = content })
  end

let drain_worklist c =
  while c.worklist_head < Vec.length c.nodes do
    let id = c.worklist_head in
    c.worklist_head <- id + 1;
    compile_node c id (Vec.get c.targets id)
  done

let target_in c resource pointer =
  let r = Loader.resource c.loader resource in
  let root_pointer = r.Loader.root_pointer in
  let fragment =
    if pointer <> root_pointer then
      String.sub pointer (String.length root_pointer) (String.length pointer - String.length root_pointer)
    else ""
  in
  match Loader.try_resolve_fragment c.loader resource fragment with
  | Some t -> t
  | None -> Loader.root_target c.loader resource

let expand_dynamic_refs c =
  let added = ref false in
  List.iter
    (fun pending ->
      (* The resources are only ever added to, so those seen are the first seen_resources of them. *)
      for resource = pending.seen_resources to Vec.length c.loader.Loader.resources - 1 do
        pending.seen_resources <- resource + 1;
        let r = Loader.resource c.loader resource in
        let pointer =
          if pending.p_is_recursive then if r.Loader.recursive_anchor then Some r.Loader.root_pointer else None
          else Hashtbl.find_opt r.Loader.dynamic_anchors pending.anchor
        in
        match pointer with
        | None -> ()
        | Some pointer ->
            let before = Vec.length c.nodes in
            let node = get_node c (target_in c resource pointer) in
            added := !added || Vec.length c.nodes <> before;
            pending.candidates <- pending.candidates @ [ { rn_resource = resource; rn_node = node } ]
      done)
    c.pending_dynamic_refs;
  !added

(* compute_reachability marks the nodes reachable from the entry, counting every candidate of a pending dynamic
   reference as a child. *)
let compute_reachability c pending =
  let extra = Hashtbl.create 16 in
  List.iter
    (fun p ->
      let list = try Hashtbl.find extra p.p_node with Not_found -> [] in
      let list = list @ (get_node c p.initial_target :: List.map (fun candidate -> candidate.rn_node) p.candidates) in
      Hashtbl.replace extra p.p_node list)
    pending;
  let reached = Array.make (Vec.length c.nodes) false in
  let stack = ref [ c.entry_node ] in
  reached.(c.entry_node) <- true;
  while !stack <> [] do
    let id = List.hd !stack in
    stack := List.tl !stack;
    let extra_children = try Hashtbl.find extra id with Not_found -> [] in
    List.iter
      (fun child ->
        if child < Array.length reached && not reached.(child) then begin
          reached.(child) <- true;
          stack := child :: !stack
        end)
      (Node.children (Vec.get c.nodes id) @ extra_children)
  done;
  reached

let finalize_dynamic_refs c =
  let reachable = ref None in
  let pending = c.pending_dynamic_refs in
  c.pending_dynamic_refs <- [];
  List.iter
    (fun p ->
      let fallback = get_node c p.initial_target in
      let set_static t =
        let n = Vec.get c.nodes p.p_node in
        n.static_dynamic_ref <- t;
        n.static_dynamic_keyword <- dynamic_keyword p.p_is_recursive
      in
      if List.length p.candidates <= 1 then
        (* Only the initial target's resource defines the anchor: resolution is static. *)
        set_static fallback
      else begin
        (* The dynamic scope is searched outermost first and its outermost entry is always the resource evaluation
           started in. When the entry resource defines the anchor, that target is the answer on every path. *)
        let reached =
          match !reachable with
          | Some r -> r
          | None ->
              let r = compute_reachability c pending in
              reachable := Some r;
              r
        in
        if not (p.p_node < Array.length reached && reached.(p.p_node)) then set_static fallback
        else begin
          let entry_resource = (Vec.get c.nodes c.entry_node).resource_id in
          match List.find_opt (fun candidate -> candidate.rn_resource = entry_resource) p.candidates with
          | Some candidate -> set_static candidate.rn_node
          | None ->
              (Vec.get c.nodes p.p_node).dynamic_ref <-
                Some { is_recursive = p.p_is_recursive; fallback; by_resource = Array.of_list p.candidates }
        end
      end)
    pending;
  drain_worklist c

let compile_all c =
  let continue = ref true in
  while !continue do
    drain_worklist c;
    if c.pending_dynamic_refs = [] || ((not (expand_dynamic_refs c)) && c.worklist_head = Vec.length c.nodes) then
      continue := false
  done;
  (* Most schemas have no dynamic reference: skip the finalisation. *)
  if c.pending_dynamic_refs <> [] then finalize_dynamic_refs c

(* ---------------------------------------------------------------------------------------------------------------------
   Analyses *)

(* compute_marking works out which nodes can contribute evaluated-property/item annotations: a node marks if it has
   the keywords itself or any in-place child (not counting not) marks. *)
let compute_marking (nodes : Node.t array) (edges : int list array option) =
  Array.iter
    (fun n ->
      n.marks_properties <-
        n.has_properties || n.has_pattern_properties || n.additional_properties >= 0 || n.unevaluated_properties >= 0;
      n.marks_items <-
        n.has_prefix_items || n.items >= 0
        || (n.contains >= 0 && n.contains_marks_evaluated)
        || n.unevaluated_items >= 0)
    nodes;
  match edges with
  | None -> ()
  | Some edges ->
      let parents = Array.make (Array.length nodes) [] in
      Array.iteri
        (fun i edge ->
          (* not does not contribute annotations: nodes with one use the edges without it. *)
          let children = if nodes.(i).not_ >= 0 then Node.in_place_children nodes.(i) false else edge in
          List.iter (fun child -> parents.(child) <- i :: parents.(child)) children)
        edges;
      for which = 0 to 1 do
        let get n = if which = 0 then n.marks_properties else n.marks_items in
        let set n = if which = 0 then n.marks_properties <- true else n.marks_items <- true in
        let work = ref [] in
        Array.iteri (fun i n -> if get n then work := i :: !work) nodes;
        while !work <> [] do
          let w = List.hd !work in
          work := List.tl !work;
          List.iter
            (fun p ->
              if not (get nodes.(p)) then begin
                set nodes.(p);
                work := p :: !work
              end)
            parents.(w)
        done
      done

(* compute_in_place_cycles marks nodes on a cycle of in-place applicators (iterative Tarjan), the only ones that
   need a depth guard. *)
let compute_in_place_cycles (nodes : Node.t array) (edges : int array array) =
  let count = Array.length nodes in
  let index = Array.make count (-1) in
  let low = Array.make count 0 in
  let on_stack = Array.make count false in
  let stack = ref [] in
  let next = ref 0 in
  for start = 0 to count - 1 do
    if index.(start) < 0 then begin
      (* The frames of the search: a node and the next of its edges to follow. *)
      let work = ref [ (start, ref 0) ] in
      index.(start) <- !next;
      low.(start) <- !next;
      incr next;
      stack := start :: !stack;
      on_stack.(start) <- true;
      while !work <> [] do
        let v, edge = List.hd !work in
        if !edge < Array.length edges.(v) then begin
          let w = edges.(v).(!edge) in
          incr edge;
          if index.(w) < 0 then begin
            index.(w) <- !next;
            low.(w) <- !next;
            incr next;
            stack := w :: !stack;
            on_stack.(w) <- true;
            work := (w, ref 0) :: !work
          end
          else if on_stack.(w) then low.(v) <- Int.min low.(v) index.(w)
        end
        else begin
          work := List.tl !work;
          (match !work with (parent, _) :: _ -> low.(parent) <- Int.min low.(parent) low.(v) | [] -> ());
          if low.(v) = index.(v) then begin
            let component = ref [] in
            let popping = ref true in
            while !popping do
              let w = List.hd !stack in
              stack := List.tl !stack;
              on_stack.(w) <- false;
              component := w :: !component;
              if w = v then popping := false
            done;
            let self_loop = Array.exists (fun w -> w = v) edges.(v) in
            if List.length !component > 1 || self_loop then
              List.iter (fun w -> nodes.(w).in_place_cycle <- true) !component
          end
        end
      done
    end
  done

(* effective_node follows pure $ref nodes to the node that carries constraints. *)
let effective_node (nodes : Node.t array) id =
  let node = ref nodes.(id) in
  let hops = ref 0 in
  while !hops < 16 && is_pure_ref !node do
    node := nodes.(!node.ref_);
    incr hops
  done;
  !node

(* The constraint a branch's properties[X] places on the value: positive (const/enum of primitives), negative (a
   string not in an enum) or wildcard (anything else). *)
let class_wildcard = 0
let class_positive = 1
let class_negative = 2

type value_class = { vc_kind : int; set : discriminator_value list }

let class_contains c v = List.exists (fun x -> dv_same x v) c.set

let primitive v =
  let kind = kind v.d v.n in
  if kind = kind_string then Some { dv_kind = kind; text = Document.string_value v.d v.n; dv_value = no_value }
  else if kind = kind_number || kind = kind_bool || kind = kind_null then
    Some { dv_kind = kind; text = ""; dv_value = v }
  else None

let primitives values = List.filter_map primitive (Array.to_list values)

let classify nodes (branch : Node.t) name =
  let wildcard = { vc_kind = class_wildcard; set = [] } in
  let child = ref no_node in
  Array.iter (fun (p : named_node) -> if !child < 0 && String.equal p.name name then child := p.node) branch.properties;
  if !child < 0 then wildcard
  else begin
    let p = effective_node nodes !child in
    let from_const = if is_set p.const_value then primitive p.const_value else None in
    match from_const with
    | Some v -> { vc_kind = class_positive; set = [ v ] }
    | None ->
        let prims = if p.has_enum && Array.length p.enum_values <> 0 then primitives p.enum_values else [] in
        if prims <> [] && List.length prims = Array.length p.enum_values then { vc_kind = class_positive; set = prims }
        else if p.not_ >= 0 && p.has_type && p.type_mask = type_string then begin
          let not_ = effective_node nodes p.not_ in
          if
            not_.has_enum && (not not_.has_type)
            && (not (is_set not_.const_value))
            && (not (has_string_keywords not_))
            && (not (has_in_place_applicators not_))
            && Array.for_all (fun v -> kind v.d v.n = kind_string) not_.enum_values
          then { vc_kind = class_negative; set = primitives not_.enum_values }
          else wildcard
        end
        else wildcard
  end

(* build_discriminator classifies branches by the constraint their properties[X] places on the value, and builds
   the table from values to branches. *)
let build_discriminator nodes (branches : int array) =
  let candidates =
    let rec first i =
      if i >= Array.length branches then []
      else begin
        let eff = effective_node nodes branches.(i) in
        if not eff.has_properties then first (i + 1)
        else
          List.filter_map
            (fun (p : named_node) ->
              if (classify nodes eff p.name).vc_kind <> class_wildcard then Some p.name else None)
            (Array.to_list eff.properties)
      end
    in
    first 0
  in
  let rec attempt = function
    | [] -> None
    | name :: rest ->
        let classes = Array.map (fun b -> classify nodes (effective_node nodes b) name) branches in
        let constrained = Array.fold_left (fun k c -> if c.vc_kind <> class_wildcard then k + 1 else k) 0 classes in
        if constrained < 2 then attempt rest
        else begin
          let values = ref [] in
          Array.iter
            (fun c ->
              List.iter
                (fun v -> if not (List.exists (fun x -> dv_same x v) !values) then values := !values @ [ v ])
                c.set)
            classes;
          let known =
            List.map
              (fun value ->
                let selected = ref [] in
                Array.iteri
                  (fun b c ->
                    let contains = class_contains c value in
                    let is_selected =
                      if c.vc_kind = class_positive then contains
                      else if c.vc_kind = class_negative then not contains
                      else true
                    in
                    if is_selected then selected := b :: !selected)
                  classes;
                { de_value = value; branches = Array.of_list (List.rev !selected) })
              !values
          in
          let unknown = ref [] in
          Array.iteri (fun b c -> if c.vc_kind <> class_positive then unknown := b :: !unknown) classes;
          let all_require = Array.for_all (fun b -> contains_string (effective_node nodes b).required name) branches in
          Some
            { property = name; known = Array.of_list known; unknown = Array.of_list (List.rev !unknown); all_require }
        end
  in
  attempt candidates

let compute_discriminators (nodes : Node.t array) =
  Array.iter
    (fun n ->
      if Array.length n.one_of > 1 then n.one_of_discriminator <- build_discriminator nodes n.one_of;
      if Array.length n.any_of > 1 then n.any_of_discriminator <- build_discriminator nodes n.any_of)
    nodes

let analyse (nodes : Node.t array) =
  let edges = Array.map (fun n -> in_place_children n true) nodes in
  let in_place = Array.exists (fun e -> e <> []) edges in
  let branches = Array.exists (fun n -> n.has_one_of || n.has_any_of) nodes in
  if in_place then begin
    compute_marking nodes (Some edges);
    compute_in_place_cycles nodes (Array.map Array.of_list edges)
  end
  else compute_marking nodes None;
  if branches then compute_discriminators nodes

let compile_loaded loader root_resource (options : Options.t) =
  let target =
    match options.entry_point with
    | None -> Loader.root_target loader root_resource
    | Some entry_point -> (
        match Loader.try_resolve_reference loader root_resource entry_point with
        | Some t -> t
        | None -> Options.compile_error ("Unable to resolve the entry point '" ^ entry_point ^ "'."))
  in
  let c =
    {
      loader;
      options;
      nodes = Vec.create ();
      targets = Vec.create ();
      node_of = Hashtbl.create 64;
      worklist_head = 0;
      pending_dynamic_refs = [];
      annotation_sources = Vec.create ();
      entry_node = no_node;
      patterns = Hashtbl.create 8;
    }
  in
  c.entry_node <- get_node c target;
  compile_all c;
  let nodes = Vec.to_array c.nodes in
  let uses_dynamic_scope = Array.exists (fun n -> n.dynamic_ref <> None) nodes in
  analyse nodes;
  {
    nodes;
    root = c.entry_node;
    uses_dynamic_scope;
    annotation_sources = Vec.to_array c.annotation_sources;
    assert_format_set = options.assert_format <> 0;
  }

let compile_document schema options =
  let loader = Loader.create options in
  let root = Loader.load_root loader schema in
  compile_loaded loader root options

let compile_from_uri uri options =
  let loader = Loader.create options in
  let root = Loader.load_root_from_uri loader uri in
  compile_loaded loader root options

(* collect_annotations lists the annotation keywords of a schema object, in the order the C# compiler records
   them. *)
let collect_annotations d e dialect voc content assert_format_set =
  let rank = Dialect.rank dialect in
  let legacy = Dialect.is_legacy dialect in
  let meta_data = legacy || voc land Dialect.vocab_meta_data <> 0 in
  let format_annotate =
    legacy || voc land (Dialect.vocab_format_annotation lor Dialect.vocab_format_assertion) <> 0 || assert_format_set
  in
  let out = ref [] in
  let add keyword strings_only = out := { keyword; value = json_of d (property d e keyword); strings_only } :: !out in
  let has name = property d e name >= 0 in
  let k = first d e in
  for i = 0 to count d e - 1 do
    let name = Document.string_value d (k + (2 * i)) in
    match name with
    | "title" | "description" | "default" -> if meta_data then add name false
    | "examples" -> if meta_data && rank >= 1 then add name false
    | "readOnly" | "writeOnly" -> if meta_data && rank >= 2 then add name false
    | "deprecated" -> if meta_data && rank >= 3 then add name false
    | "format" -> if kind d (k + (2 * i) + 1) = kind_string && format_annotate then add name false
    | _ ->
        (* Unknown keywords are collected as annotations from 2019-09 onwards. *)
        if rank >= 3 && not (is_known_keyword name) then add name false
  done;
  if content && rank >= 2 then begin
    if has "contentEncoding" then add "contentEncoding" true;
    if has "contentMediaType" then begin
      add "contentMediaType" true;
      (* contentSchema is only meaningful alongside contentMediaType. *)
      if has "contentSchema" && rank >= 3 then add "contentSchema" true
    end
  end;
  Array.of_list (List.rev !out)
