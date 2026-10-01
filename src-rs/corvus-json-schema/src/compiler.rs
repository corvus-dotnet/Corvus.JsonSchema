//! Compiles loaded schema documents into a `SchemaNode` graph and runs the compile-time analyses.
//! A port of `Corvus.Text.Json.RuntimeEvaluator.Compilation.SchemaCompiler` (via the TypeScript port's `compiler.ts`).

use std::collections::HashMap;

use serde_json::{Map, Value};

use crate::dialect::{Dialect, vocab};
use crate::formats::FormatKind;
use crate::loader::{SchemaLoader, SchemaTarget, addr};
use crate::node::*;
use crate::options::{CompileOptions, SchemaCompilationError};
use crate::uri::{decode_fragment, escape_pointer_token, split};

/// Keywords `SchemaCompiler.CompileNode` handles itself; anything else is an unknown keyword (an annotation from
/// 2019-09).
const KNOWN_KEYWORDS: &[&str] = &[
    "if",
    "not",
    "type",
    "enum",
    "$ref",
    "then",
    "else",
    "const",
    "items",
    "allOf",
    "anyOf",
    "oneOf",
    "title",
    "$defs",
    "format",
    "pattern",
    "maximum",
    "minimum",
    "default",
    "$schema",
    "$anchor",
    "required",
    "contains",
    "maxItems",
    "minItems",
    "examples",
    "readOnly",
    "$comment",
    "maxLength",
    "minLength",
    "writeOnly",
    "properties",
    "multipleOf",
    "deprecated",
    "uniqueItems",
    "prefixItems",
    "minContains",
    "maxContains",
    "description",
    "$vocabulary",
    "definitions",
    "$dynamicRef",
    "dependencies",
    "propertyNames",
    "maxProperties",
    "minProperties",
    "contentSchema",
    "$recursiveRef",
    "$dynamicAnchor",
    "contentEncoding",
    "additionalItems",
    "exclusiveMaximum",
    "exclusiveMinimum",
    "unevaluatedItems",
    "contentMediaType",
    "$recursiveAnchor",
    "dependentSchemas",
    "patternProperties",
    "dependentRequired",
    "additionalProperties",
    "unevaluatedProperties",
    "id",
    "$id",
];

struct PendingDynamicRef {
    node_id: NodeId,
    anchor: String,
    is_recursive: bool,
    initial_target: SchemaTarget,
    seen_resources: Vec<bool>,
    candidates: Vec<(u32, NodeId)>,
}

/// What a node's annotations are computed from, resolved on the first evaluation with a collector.
#[derive(Clone, Copy)]
pub(crate) struct AnnotationSource {
    pub document: u32,
    pub vocab: u32,
    pub content: bool,
}

/// The compiled program: the node graph, its entry node and whether it maintains a dynamic scope.
pub(crate) struct CompiledSchema {
    pub nodes: Vec<SchemaNode>,
    pub root: NodeId,
    pub uses_dynamic_scope: bool,
    /// The loaded documents (kept for the annotation keywords, which only results collection needs). Boxed so
    /// each root keeps its address: schema values are identified by address.
    #[allow(clippy::vec_box)]
    pub documents: Vec<Box<Value>>,
    pub annotation_sources: Vec<Option<AnnotationSource>>,
}

pub(crate) struct SchemaCompiler<'l, 'o> {
    loader: &'l mut SchemaLoader<'o>,
    options: &'o CompileOptions,
    nodes: Vec<SchemaNode>,
    targets: Vec<SchemaTarget>,
    /// The compiled node of each schema value, by (document, address).
    node_of: HashMap<(u32, usize), NodeId>,
    worklist_head: usize,
    pending_dynamic_refs: Vec<PendingDynamicRef>,
    annotation_sources: Vec<Option<AnnotationSource>>,
    entry_node: NodeId,
}

fn get_uint(v: Option<&Value>) -> Option<u64> {
    match v {
        Some(Value::Number(n)) => {
            if let Some(u) = n.as_u64() {
                Some(u)
            } else {
                let f = n.as_f64()?;
                (f >= 0.0 && f.fract() == 0.0 && f < 1.8e19).then_some(f as u64)
            }
        }
        _ => None,
    }
}

fn type_mask_of(name: &Value) -> u8 {
    match name.as_str() {
        Some("null") => type_mask::NULL,
        Some("boolean") => type_mask::BOOLEAN,
        Some("object") => type_mask::OBJECT,
        Some("array") => type_mask::ARRAY,
        Some("number") => type_mask::NUMBER,
        Some("string") => type_mask::STRING,
        Some("integer") => type_mask::INTEGER,
        _ => 0,
    }
}

fn strings(v: &[Value]) -> Vec<String> {
    v.iter().filter_map(|x| x.as_str().map(String::from)).collect()
}

impl<'l, 'o> SchemaCompiler<'l, 'o> {
    pub fn compile(schema: Value, options: &'o CompileOptions) -> Result<CompiledSchema, SchemaCompilationError> {
        let mut loader = SchemaLoader::new(options);
        let root_resource = loader.load_root(schema, options.base_uri.as_deref())?;
        Self::compile_loaded(loader, root_resource, options)
    }

    pub fn compile_from_uri(uri: &str, options: &'o CompileOptions) -> Result<CompiledSchema, SchemaCompilationError> {
        let mut loader = SchemaLoader::new(options);
        let root_resource = loader.load_root_from_uri(uri)?;
        Self::compile_loaded(loader, root_resource, options)
    }

    fn compile_loaded(
        mut loader: SchemaLoader<'o>,
        root_resource: u32,
        options: &'o CompileOptions,
    ) -> Result<CompiledSchema, SchemaCompilationError> {
        let mut target = loader.root_target(root_resource);
        if let Some(entry) = &options.entry_point {
            target = loader
                .try_resolve_reference(root_resource, entry)?
                .ok_or_else(|| SchemaCompilationError::new(format!("Unable to resolve the entry point '{entry}'.")))?;
        }
        let mut compiler = SchemaCompiler {
            loader: &mut loader,
            options,
            nodes: Vec::new(),
            targets: Vec::new(),
            node_of: HashMap::new(),
            worklist_head: 0,
            pending_dynamic_refs: Vec::new(),
            annotation_sources: Vec::new(),
            entry_node: 0,
        };
        compiler.entry_node = compiler.get_node(&target);
        compiler.compile_all()?;
        let uses_dynamic_scope = compiler.nodes.iter().any(|n| n.dynamic_ref.is_some());
        compiler.analyse();
        let root = compiler.entry_node;
        let nodes = std::mem::take(&mut compiler.nodes);
        let annotation_sources = std::mem::take(&mut compiler.annotation_sources);
        drop(compiler);
        let documents = loader.documents.into_iter().map(|d| d.root).collect();
        Ok(CompiledSchema { nodes, root, uses_dynamic_scope, documents, annotation_sources })
    }

    fn get_node(&mut self, target: &SchemaTarget) -> NodeId {
        let key = (target.document, target.value as usize);
        if let Some(&id) = self.node_of.get(&key) {
            return id;
        }
        let id = self.nodes.len() as NodeId;
        let resource = &self.loader.resources[target.resource as usize];
        self.nodes.push(SchemaNode::new(target.resource, resource.dialect, target.pointer.clone()));
        self.targets.push(target.clone());
        self.annotation_sources.push(None);
        self.node_of.insert(key, id);
        id
    }

    fn compile_all(&mut self) -> Result<(), SchemaCompilationError> {
        loop {
            while self.worklist_head < self.nodes.len() {
                let id = self.worklist_head;
                self.worklist_head += 1;
                let target = self.targets[id].clone();
                self.compile_node(id as NodeId, &target)?;
            }
            if self.pending_dynamic_refs.is_empty()
                || (!self.expand_dynamic_refs() && self.worklist_head == self.nodes.len())
            {
                break;
            }
        }
        // Most schemas have no dynamic reference: skip (and so never compile) the finalisation.
        if !self.pending_dynamic_refs.is_empty() {
            self.finalize_dynamic_refs()?;
        }
        Ok(())
    }

    fn child(&mut self, parent: &SchemaTarget, value: &Value, relative: &str) -> NodeId {
        let pointer = format!("{}{}", parent.pointer, relative);
        let resource = self.loader.resource_of(parent.document, value).unwrap_or(parent.resource);
        self.get_node(&SchemaTarget { document: parent.document, pointer, value: value as *const Value, resource })
    }

    fn child_array(&mut self, parent: &SchemaTarget, value: Option<&Value>, keyword: &str) -> Option<Vec<NodeId>> {
        let Some(Value::Array(items)) = value else {
            return None;
        };
        Some(items.iter().enumerate().map(|(i, v)| self.child(parent, v, &format!("/{keyword}/{i}"))).collect())
    }

    fn compile_node(&mut self, id: NodeId, target: &SchemaTarget) -> Result<(), SchemaCompilationError> {
        let element = target.value();
        let e = match element {
            Value::Bool(true) => {
                self.nodes[id as usize].always_true = true;
                return Ok(());
            }
            Value::Bool(false) => {
                self.nodes[id as usize].always_false = true;
                return Ok(());
            }
            Value::Object(map) => map,
            _ => {
                self.nodes[id as usize].always_true = true;
                return Ok(());
            }
        };

        let (dialect, voc) = {
            let r = &self.loader.resources[target.resource as usize];
            (r.dialect, r.vocabularies)
        };
        let legacy = dialect.is_legacy();

        if legacy {
            if let Some(Value::String(r)) = e.get("$ref") {
                // In draft 7 and earlier, $ref replaces every sibling keyword.
                return self.compile_ref(id, target, r);
            }
        }

        let applicator = legacy || voc & vocab::APPLICATOR != 0;
        let validation = legacy || voc & vocab::VALIDATION != 0;
        let unevaluated = if dialect == Dialect::Draft201909 { applicator } else { voc & vocab::UNEVALUATED != 0 };
        let content = legacy || voc & vocab::CONTENT != 0;
        let format_assert = self
            .options
            .assert_format
            .unwrap_or((legacy && self.options.assert_format_in_legacy_drafts) || voc & vocab::FORMAT_ASSERTION != 0);
        let kw = |name: &str| format!("/{}", escape_pointer_token(name));

        let mut dependencies: Option<Vec<DependencyEntry>> = None;

        // References.
        if let Some(Value::String(r)) = e.get("$ref") {
            self.compile_ref(id, target, r)?;
        }
        if dialect >= Dialect::Draft202012 {
            if let Some(Value::String(r)) = e.get("$dynamicRef") {
                self.compile_dynamic_ref(id, target, r, false)?;
            }
        }
        if dialect == Dialect::Draft201909 {
            if let Some(Value::String(r)) = e.get("$recursiveRef") {
                self.compile_dynamic_ref(id, target, r, true)?;
            }
        }

        // Applicators.
        if applicator {
            if e.contains_key("allOf") {
                self.nodes[id as usize].all_of = self.child_array(target, e.get("allOf"), "allOf");
            }
            if e.contains_key("anyOf") {
                self.nodes[id as usize].any_of = self.child_array(target, e.get("anyOf"), "anyOf");
            }
            if e.contains_key("oneOf") {
                self.nodes[id as usize].one_of = self.child_array(target, e.get("oneOf"), "oneOf");
            }
            if let Some(v) = e.get("not") {
                let c = self.child(target, v, "/not");
                self.nodes[id as usize].not = Some(c);
            }
            if dialect >= Dialect::Draft7 {
                if let Some(v) = e.get("if") {
                    let c = self.child(target, v, "/if");
                    self.nodes[id as usize].if_ = Some(c);
                    if let Some(v) = e.get("then") {
                        let c = self.child(target, v, "/then");
                        self.nodes[id as usize].then = Some(c);
                    }
                    if let Some(v) = e.get("else") {
                        let c = self.child(target, v, "/else");
                        self.nodes[id as usize].else_ = Some(c);
                    }
                }
            }
            if let Some(Value::Object(props)) = e.get("properties") {
                let mut list = Vec::with_capacity(props.len());
                for (name, v) in props {
                    let c = self.child(target, v, &format!("/properties/{}", escape_pointer_token(name)));
                    list.push((name.clone(), c));
                }
                self.nodes[id as usize].properties = Some(list);
            }
            if let Some(Value::Object(pp)) = e.get("patternProperties") {
                let mut list = Vec::with_capacity(pp.len());
                for (pattern, v) in pp {
                    let compiled = crate::pattern::compile(pattern).ok_or_else(|| {
                        SchemaCompilationError::new(format!(
                            "Invalid regular expression '{pattern}' in patternProperties."
                        ))
                    })?;
                    let c = self.child(target, v, &format!("/patternProperties/{}", escape_pointer_token(pattern)));
                    list.push(PatternProperty { pattern: compiled, node: c });
                }
                self.nodes[id as usize].pattern_properties = Some(list);
            }
            if let Some(v) = e.get("additionalProperties") {
                let c = self.child(target, v, &kw("additionalProperties"));
                self.nodes[id as usize].additional_properties = Some(c);
            }
            if dialect >= Dialect::Draft6 {
                if let Some(v) = e.get("propertyNames") {
                    let c = self.child(target, v, &kw("propertyNames"));
                    self.nodes[id as usize].property_names = Some(c);
                }
                if let Some(v) = e.get("contains") {
                    let c = self.child(target, v, &kw("contains"));
                    self.nodes[id as usize].contains = Some(c);
                }
            }

            // "dependencies" is honoured in every dialect: in 2019-09+ it is an optional compatibility keyword.
            if matches!(e.get("dependencies"), Some(Value::Object(_)))
                || (dialect >= Dialect::Draft201909 && matches!(e.get("dependentSchemas"), Some(Value::Object(_))))
            {
                dependencies = Some(self.compile_dependency_schemas(target, e, dialect));
            }

            // Array applicators.
            if dialect >= Dialect::Draft202012 {
                if let Some(Value::Array(_)) = e.get("prefixItems") {
                    self.nodes[id as usize].prefix_items =
                        self.child_array(target, e.get("prefixItems"), "prefixItems");
                }
                if let Some(v) = e.get("items") {
                    if !v.is_array() {
                        let c = self.child(target, v, &kw("items"));
                        self.nodes[id as usize].items = Some(c);
                    }
                }
            } else if let Some(v) = e.get("items") {
                if v.is_array() {
                    let p = self.child_array(target, Some(v), "items");
                    let n = &mut self.nodes[id as usize];
                    n.prefix_items = p;
                    n.prefix_keyword = "items";
                    if let Some(a) = e.get("additionalItems") {
                        let c = self.child(target, a, "/additionalItems");
                        let n = &mut self.nodes[id as usize];
                        n.items = Some(c);
                        n.items_keyword = "additionalItems";
                    }
                } else {
                    let c = self.child(target, v, &kw("items"));
                    self.nodes[id as usize].items = Some(c);
                }
            }
            self.nodes[id as usize].contains_marks_evaluated = dialect >= Dialect::Draft202012;
        }

        if unevaluated && dialect >= Dialect::Draft201909 {
            if let Some(v) = e.get("unevaluatedProperties") {
                let c = self.child(target, v, &kw("unevaluatedProperties"));
                self.nodes[id as usize].unevaluated_properties = Some(c);
            }
            if let Some(v) = e.get("unevaluatedItems") {
                let c = self.child(target, v, &kw("unevaluatedItems"));
                self.nodes[id as usize].unevaluated_items = Some(c);
            }
        }

        let n = &mut self.nodes[id as usize];
        if validation {
            if let Some(t) = e.get("type") {
                let mask = match t {
                    Value::Array(list) => list.iter().fold(0, |m, x| m | type_mask_of(x)),
                    other => type_mask_of(other),
                };
                n.type_mask = mask;
                n.has_type = true;
            }
            if dialect >= Dialect::Draft6 {
                if let Some(c) = e.get("const") {
                    n.const_value = Some(c.clone());
                }
            }
            if let Some(Value::Array(values)) = e.get("enum") {
                n.enum_values = Some(values.clone());
            }
            if let Some(Value::Array(req)) = e.get("required") {
                let list = strings(req);
                let mut dedup: Vec<String> = Vec::with_capacity(list.len());
                for r in &list {
                    if !dedup.contains(r) {
                        dedup.push(r.clone());
                    }
                }
                n.required_list = Some(list);
                n.required = Some(dedup);
            }
            if dialect >= Dialect::Draft201909 {
                if let Some(Value::Object(dr)) = e.get("dependentRequired") {
                    let deps = dependencies.get_or_insert_with(Vec::new);
                    for (name, v) in dr {
                        if let Value::Array(list) = v {
                            deps.push(DependencyEntry {
                                keyword: DependencyKeyword::DependentRequired,
                                name: name.clone(),
                                required: Some(strings(list)),
                                schema: None,
                            });
                        }
                    }
                }
            }
            n.min_properties = get_uint(e.get("minProperties"));
            n.max_properties = get_uint(e.get("maxProperties"));
            n.min_items = get_uint(e.get("minItems"));
            n.max_items = get_uint(e.get("maxItems"));
            n.unique_items = e.get("uniqueItems") == Some(&Value::Bool(true));
            n.min_length = get_uint(e.get("minLength"));
            n.max_length = get_uint(e.get("maxLength"));
            if let Some(Value::String(p)) = e.get("pattern") {
                n.pattern = Some(crate::pattern::compile(p).ok_or_else(|| {
                    SchemaCompilationError::new(format!("Invalid regular expression '{p}' in pattern."))
                })?);
            }
            if let Some(Value::Number(m)) = e.get("multipleOf") {
                n.multiple_of = Some(m.clone());
            }
            let num = |k: &str| match e.get(k) {
                Some(Value::Number(x)) => Some(x.clone()),
                _ => None,
            };
            if dialect == Dialect::Draft4 {
                // Draft 4: exclusiveMaximum/exclusiveMinimum are booleans that make maximum/minimum exclusive.
                if let Some(max) = num("maximum") {
                    if e.get("exclusiveMaximum") == Some(&Value::Bool(true)) {
                        n.exclusive_maximum = Some(max);
                    } else {
                        n.maximum = Some(max);
                    }
                }
                if let Some(min) = num("minimum") {
                    if e.get("exclusiveMinimum") == Some(&Value::Bool(true)) {
                        n.exclusive_minimum = Some(min);
                    } else {
                        n.minimum = Some(min);
                    }
                }
            } else {
                n.maximum = num("maximum");
                n.minimum = num("minimum");
                n.exclusive_maximum = num("exclusiveMaximum");
                n.exclusive_minimum = num("exclusiveMinimum");
            }
            if dialect >= Dialect::Draft201909 && n.contains.is_some() {
                if e.contains_key("minContains") {
                    n.min_contains = get_uint(e.get("minContains")).unwrap_or(1);
                }
                if e.contains_key("maxContains") {
                    n.max_contains = get_uint(e.get("maxContains"));
                }
            }
        }

        if let Some(Value::String(f)) = e.get("format") {
            n.format = Some(f.clone());
            n.format_kind = FormatKind::of(f, dialect);
            n.assert_format = format_assert;
        }

        // Content keywords are asserted only in draft 7 (and annotations elsewhere).
        if content
            && dialect >= Dialect::Draft7
            && (e.contains_key("contentEncoding") || e.contains_key("contentMediaType"))
        {
            let base64 = e.get("contentEncoding").and_then(Value::as_str) == Some("base64");
            let json = e.get("contentMediaType").and_then(Value::as_str) == Some("application/json");
            n.content = match (base64, json) {
                (true, true) => ContentKind::Base64Json,
                (true, false) => ContentKind::Base64,
                (false, true) => ContentKind::Json,
                (false, false) => ContentKind::None,
            };
            n.assert_content =
                dialect == Dialect::Draft7 && self.options.assert_content && n.content != ContentKind::None;
        }

        if let Some(deps) = &mut dependencies {
            // In the order the three keywords appear in the schema, as SchemaCompiler.CompileNode meets them.
            let order = |k: DependencyKeyword| e.keys().position(|x| x == k.name()).unwrap_or(usize::MAX);
            deps.sort_by_key(|d| order(d.keyword));
        }
        n.dependencies = dependencies;
        self.annotation_sources[id as usize] =
            Some(AnnotationSource { document: target.document, vocab: voc, content });
        Ok(())
    }

    fn compile_dependency_schemas(
        &mut self,
        target: &SchemaTarget,
        e: &Map<String, Value>,
        dialect: Dialect,
    ) -> Vec<DependencyEntry> {
        let mut out = Vec::new();
        if let Some(Value::Object(deps)) = e.get("dependencies") {
            for (name, v) in deps {
                if let Value::Array(list) = v {
                    out.push(DependencyEntry {
                        keyword: DependencyKeyword::Dependencies,
                        name: name.clone(),
                        required: Some(strings(list)),
                        schema: None,
                    });
                } else {
                    let c = self.child(target, v, &format!("/dependencies/{}", escape_pointer_token(name)));
                    out.push(DependencyEntry {
                        keyword: DependencyKeyword::Dependencies,
                        name: name.clone(),
                        required: None,
                        schema: Some(c),
                    });
                }
            }
        }
        if dialect >= Dialect::Draft201909 {
            if let Some(Value::Object(ds)) = e.get("dependentSchemas") {
                for (name, v) in ds {
                    let c = self.child(target, v, &format!("/dependentSchemas/{}", escape_pointer_token(name)));
                    out.push(DependencyEntry {
                        keyword: DependencyKeyword::DependentSchemas,
                        name: name.clone(),
                        required: None,
                        schema: Some(c),
                    });
                }
            }
        }
        out
    }

    fn resolve_or_fail(
        &mut self,
        target: &SchemaTarget,
        reference: &str,
    ) -> Result<SchemaTarget, SchemaCompilationError> {
        self.loader.try_resolve_reference(target.resource, reference)?.ok_or_else(|| {
            SchemaCompilationError::new(format!(
                "Unable to resolve reference '{reference}' from '{}'.",
                self.loader.resources[target.resource as usize].uri
            ))
        })
    }

    fn compile_ref(
        &mut self,
        id: NodeId,
        target: &SchemaTarget,
        reference: &str,
    ) -> Result<(), SchemaCompilationError> {
        let resolved = self.resolve_or_fail(target, reference)?;
        let r = self.get_node(&resolved);
        self.nodes[id as usize].ref_ = Some(r);
        Ok(())
    }

    fn compile_dynamic_ref(
        &mut self,
        id: NodeId,
        target: &SchemaTarget,
        reference: &str,
        is_recursive: bool,
    ) -> Result<(), SchemaCompilationError> {
        let resolved = self.resolve_or_fail(target, reference)?;
        let fragment = decode_fragment(split(reference).1);
        let res = &self.loader.resources[resolved.resource as usize];
        let dynamic = if is_recursive {
            res.recursive_anchor && resolved.pointer == res.root_pointer
        } else {
            !fragment.is_empty()
                && !fragment.starts_with('/')
                && res.dynamic_anchors.get(&fragment) == Some(&resolved.pointer)
        };
        let keyword = if is_recursive { StaticDynamicKeyword::RecursiveRef } else { StaticDynamicKeyword::DynamicRef };
        if !dynamic {
            // A static reference, kept apart from any sibling $ref (both apply).
            let t = self.get_node(&resolved);
            let n = &mut self.nodes[id as usize];
            n.static_dynamic_ref = Some(t);
            n.static_dynamic_keyword = keyword;
            return Ok(());
        }
        self.pending_dynamic_refs.push(PendingDynamicRef {
            node_id: id,
            anchor: fragment,
            is_recursive,
            initial_target: resolved,
            seen_resources: Vec::new(),
            candidates: Vec::new(),
        });
        Ok(())
    }

    fn expand_dynamic_refs(&mut self) -> bool {
        let mut added = false;
        for p in 0..self.pending_dynamic_refs.len() {
            let resource_count = self.loader.resources.len();
            for resource in 0..resource_count {
                let pending = &mut self.pending_dynamic_refs[p];
                if pending.seen_resources.len() <= resource {
                    pending.seen_resources.resize(resource + 1, false);
                }
                if pending.seen_resources[resource] {
                    continue;
                }
                pending.seen_resources[resource] = true;
                let r = &self.loader.resources[resource];
                let pointer = if pending.is_recursive {
                    if !r.recursive_anchor {
                        continue;
                    }
                    r.root_pointer.clone()
                } else {
                    match r.dynamic_anchors.get(&pending.anchor) {
                        Some(p) => p.clone(),
                        None => continue,
                    }
                };
                let t = self.target_in(resource as u32, &pointer);
                let before = self.nodes.len();
                let node_id = self.get_node(&t);
                added |= self.nodes.len() != before;
                self.pending_dynamic_refs[p].candidates.push((resource as u32, node_id));
            }
        }
        added
    }

    fn target_in(&self, resource: u32, pointer: &str) -> SchemaTarget {
        let r = &self.loader.resources[resource as usize];
        let fragment = if pointer == r.root_pointer { "" } else { &pointer[r.root_pointer.len()..] };
        self.loader.try_resolve_fragment(resource, fragment).unwrap_or_else(|| self.loader.root_target(resource))
    }

    fn finalize_dynamic_refs(&mut self) -> Result<(), SchemaCompilationError> {
        let mut reachable: Option<Vec<bool>> = None;
        let pending = std::mem::take(&mut self.pending_dynamic_refs);
        for p in &pending {
            let fallback = self.get_node(&p.initial_target);
            let keyword =
                if p.is_recursive { StaticDynamicKeyword::RecursiveRef } else { StaticDynamicKeyword::DynamicRef };
            let set_static = |this: &mut Self, t: NodeId| {
                let n = &mut this.nodes[p.node_id as usize];
                n.static_dynamic_ref = Some(t);
                n.static_dynamic_keyword = keyword;
            };
            if p.candidates.len() <= 1 {
                // Only the initial target's resource defines the anchor: resolution is static.
                set_static(self, fallback);
                continue;
            }
            // The dynamic scope is searched outermost-first and its outermost entry is always the resource
            // evaluation started in. When the entry resource defines the anchor, that target is the answer on every
            // path.
            if reachable.is_none() {
                reachable = Some(self.compute_reachability(&pending));
            }
            if !reachable.as_ref().unwrap()[p.node_id as usize] {
                set_static(self, fallback);
                continue;
            }
            let entry_resource = self.nodes[self.entry_node as usize].resource_id;
            if let Some(&(_, uniform)) = p.candidates.iter().find(|(r, _)| *r == entry_resource) {
                set_static(self, uniform);
                continue;
            }
            self.nodes[p.node_id as usize].dynamic_ref = Some(Box::new(DynamicRefTarget {
                is_recursive: p.is_recursive,
                fallback,
                by_resource: p.candidates.clone(),
            }));
        }
        while self.worklist_head < self.nodes.len() {
            let id = self.worklist_head;
            self.worklist_head += 1;
            let target = self.targets[id].clone();
            self.compile_node(id as NodeId, &target)?;
        }
        Ok(())
    }

    /// Nodes reachable from the entry, counting every candidate of a pending dynamic reference as a child.
    fn compute_reachability(&mut self, pending: &[PendingDynamicRef]) -> Vec<bool> {
        let mut extra: HashMap<NodeId, Vec<NodeId>> = HashMap::new();
        for p in pending {
            let init = self.get_node(&p.initial_target);
            let list = extra.entry(p.node_id).or_default();
            list.push(init);
            list.extend(p.candidates.iter().map(|&(_, n)| n));
        }
        let mut reached = vec![false; self.nodes.len()];
        let mut stack = vec![self.entry_node];
        reached[self.entry_node as usize] = true;
        while let Some(id) = stack.pop() {
            let mut children = self.nodes[id as usize].children();
            if let Some(e) = extra.get(&id) {
                children.extend_from_slice(e);
            }
            for c in children {
                if (c as usize) < reached.len() && !reached[c as usize] {
                    reached[c as usize] = true;
                    stack.push(c);
                }
            }
        }
        reached
    }

    // ------------------------------------------------------------------------------------------------------------
    // Analyses

    fn analyse(&mut self) {
        let count = self.nodes.len();
        let mut edges: Vec<Vec<NodeId>> = Vec::with_capacity(count);
        let mut in_place = false;
        let mut branches = false;
        for n in &self.nodes {
            let e = n.in_place_children(true);
            in_place |= !e.is_empty();
            branches |= n.one_of.is_some() || n.any_of.is_some();
            edges.push(e);
        }
        self.compute_marking(if in_place { Some(&edges) } else { None });
        if in_place {
            self.compute_in_place_cycles(&edges);
        }
        if branches {
            self.compute_discriminators();
        }
    }

    /// Which nodes can contribute evaluated-property/item annotations: a node marks if it has the keywords itself or
    /// any in-place child (not counting `not`) marks.
    fn compute_marking(&mut self, edges: Option<&Vec<Vec<NodeId>>>) {
        for n in &mut self.nodes {
            n.marks_properties = n.properties.is_some()
                || n.pattern_properties.is_some()
                || n.additional_properties.is_some()
                || n.unevaluated_properties.is_some();
            n.marks_items = n.prefix_items.is_some()
                || n.items.is_some()
                || (n.contains.is_some() && n.contains_marks_evaluated)
                || n.unevaluated_items.is_some();
        }
        let Some(edges) = edges else {
            return;
        };
        let count = self.nodes.len();
        let mut parents: Vec<Vec<NodeId>> = vec![Vec::new(); count];
        for (i, edge) in edges.iter().enumerate() {
            // `not` does not contribute annotations: nodes with one use the edges without it.
            let children =
                if self.nodes[i].not.is_some() { self.nodes[i].in_place_children(false) } else { edge.clone() };
            for c in children {
                parents[c as usize].push(i as NodeId);
            }
        }
        for which in 0..2 {
            let marks = |n: &SchemaNode| if which == 0 { n.marks_properties } else { n.marks_items };
            let mut work: Vec<NodeId> = (0..count).filter(|&i| marks(&self.nodes[i])).map(|i| i as NodeId).collect();
            while let Some(w) = work.pop() {
                for &p in &parents[w as usize] {
                    let n = &mut self.nodes[p as usize];
                    let flag = if which == 0 { &mut n.marks_properties } else { &mut n.marks_items };
                    if !*flag {
                        *flag = true;
                        work.push(p);
                    }
                }
            }
        }
    }

    /// Marks nodes on a cycle of in-place applicators (iterative Tarjan), the only ones that need a depth guard.
    fn compute_in_place_cycles(&mut self, edges: &[Vec<NodeId>]) {
        let count = self.nodes.len();
        let mut index = vec![-1i32; count];
        let mut low = vec![0i32; count];
        let mut on_stack = vec![false; count];
        let mut stack: Vec<usize> = Vec::new();
        let mut next = 0i32;
        for start in 0..count {
            if index[start] >= 0 {
                continue;
            }
            let mut work: Vec<(usize, usize)> = vec![(start, 0)];
            index[start] = next;
            low[start] = next;
            next += 1;
            stack.push(start);
            on_stack[start] = true;
            while let Some(&mut (v, ref mut ei)) = work.last_mut() {
                if *ei < edges[v].len() {
                    let w = edges[v][*ei] as usize;
                    *ei += 1;
                    if index[w] < 0 {
                        index[w] = next;
                        low[w] = next;
                        next += 1;
                        stack.push(w);
                        on_stack[w] = true;
                        work.push((w, 0));
                    } else if on_stack[w] {
                        low[v] = low[v].min(index[w]);
                    }
                } else {
                    work.pop();
                    if let Some(&(parent, _)) = work.last() {
                        low[parent] = low[parent].min(low[v]);
                    }
                    if low[v] == index[v] {
                        let mut component = Vec::new();
                        loop {
                            let w = stack.pop().unwrap();
                            on_stack[w] = false;
                            component.push(w);
                            if w == v {
                                break;
                            }
                        }
                        if component.len() > 1 || edges[v].contains(&(v as NodeId)) {
                            for c in component {
                                self.nodes[c].in_place_cycle = true;
                            }
                        }
                    }
                }
            }
        }
    }

    /// Follows pure `$ref` nodes to the node that carries constraints.
    fn effective_node(&self, id: NodeId) -> &SchemaNode {
        let mut node = &self.nodes[id as usize];
        let mut hops = 0;
        while hops < 16 && node.is_pure_ref() {
            node = &self.nodes[node.ref_.unwrap() as usize];
            hops += 1;
        }
        node
    }

    fn compute_discriminators(&mut self) {
        for i in 0..self.nodes.len() {
            if let Some(branches) = self.nodes[i].one_of.clone() {
                if branches.len() > 1 {
                    self.nodes[i].one_of_discriminator = self.build_discriminator(&branches).map(Box::new);
                }
            }
            if let Some(branches) = self.nodes[i].any_of.clone() {
                if branches.len() > 1 {
                    self.nodes[i].any_of_discriminator = self.build_discriminator(&branches).map(Box::new);
                }
            }
        }
    }

    /// Classifies branches by the constraint their `properties[X]` places on the value (positive: const/enum of
    /// primitives; negative: string not in an enum; wildcard: anything else), and builds the value -> branches table.
    fn build_discriminator(&self, branches: &[NodeId]) -> Option<Discriminator> {
        let mut candidates: Vec<String> = Vec::new();
        for &b in branches {
            let eff = self.effective_node(b);
            let Some(props) = &eff.properties else {
                continue;
            };
            for (name, _) in props {
                if !matches!(self.classify(eff, name), Class::Wildcard) {
                    candidates.push(name.clone());
                }
            }
            break;
        }
        for name in candidates {
            let classes: Vec<Class> = branches.iter().map(|&b| self.classify(self.effective_node(b), &name)).collect();
            if classes.iter().filter(|c| !matches!(c, Class::Wildcard)).count() < 2 {
                continue;
            }
            let mut values: Vec<DiscriminatorValue> = Vec::new();
            for c in &classes {
                for v in c.set() {
                    if !values.iter().any(|x| x.same(v)) {
                        values.push(v.clone());
                    }
                }
            }
            let known = values
                .into_iter()
                .map(|value| {
                    let selected: Vec<u32> = classes
                        .iter()
                        .enumerate()
                        .filter(|(_, c)| {
                            let contains = c.set().iter().any(|x| x.same(&value));
                            match c {
                                Class::Positive(_) => contains,
                                Class::Negative(_) => !contains,
                                Class::Wildcard => true,
                            }
                        })
                        .map(|(i, _)| i as u32)
                        .collect();
                    (value, selected)
                })
                .collect();
            let unknown = classes
                .iter()
                .enumerate()
                .filter(|(_, c)| !matches!(c, Class::Positive(_)))
                .map(|(i, _)| i as u32)
                .collect();
            let all_require =
                branches.iter().all(|&b| self.effective_node(b).required.as_ref().is_some_and(|r| r.contains(&name)));
            return Some(Discriminator { property: name, known, unknown, all_require });
        }
        None
    }

    fn classify(&self, branch: &SchemaNode, name: &str) -> Class {
        let Some(child) = branch.properties.as_ref().and_then(|p| p.iter().find(|(n, _)| n == name).map(|&(_, c)| c))
        else {
            return Class::Wildcard;
        };
        let p = self.effective_node(child);
        if let Some(c) = &p.const_value {
            if let Some(v) = primitive(c) {
                return Class::Positive(vec![v]);
            }
        }
        if let Some(values) = &p.enum_values {
            if !values.is_empty() {
                let prims: Vec<DiscriminatorValue> = values.iter().filter_map(primitive).collect();
                if prims.len() == values.len() {
                    return Class::Positive(prims);
                }
            }
        }
        if let Some(not) = p.not {
            if p.has_type && p.type_mask == type_mask::STRING {
                let not = self.effective_node(not);
                if let Some(values) = &not.enum_values {
                    if values.iter().all(Value::is_string)
                        && !not.has_type
                        && not.const_value.is_none()
                        && !not.has_string_keywords()
                        && !not.has_in_place_applicators()
                    {
                        return Class::Negative(values.iter().filter_map(primitive).collect());
                    }
                }
            }
        }
        Class::Wildcard
    }
}

enum Class {
    Positive(Vec<DiscriminatorValue>),
    Negative(Vec<DiscriminatorValue>),
    Wildcard,
}

impl Class {
    fn set(&self) -> &[DiscriminatorValue] {
        match self {
            Class::Positive(s) | Class::Negative(s) => s,
            Class::Wildcard => &[],
        }
    }
}

fn primitive(v: &Value) -> Option<DiscriminatorValue> {
    match v {
        Value::String(s) => Some(DiscriminatorValue::String(s.clone())),
        Value::Number(n) => Some(DiscriminatorValue::Number(n.clone())),
        Value::Bool(b) => Some(DiscriminatorValue::Bool(*b)),
        Value::Null => Some(DiscriminatorValue::Null),
        _ => None,
    }
}

/// The annotation keywords of a schema object, in the order `SchemaCompiler.CompileNode` records them.
pub(crate) fn collect_annotations(
    e: &Map<String, Value>,
    dialect: Dialect,
    voc: u32,
    content: bool,
    assert_format_set: bool,
) -> Option<Vec<AnnotationEntry>> {
    let legacy = dialect.is_legacy();
    let meta_data = legacy || voc & vocab::META_DATA != 0;
    let format_annotate =
        legacy || voc & (vocab::FORMAT_ANNOTATION | vocab::FORMAT_ASSERTION) != 0 || assert_format_set;
    let mut out = Vec::new();
    let mut add = |keyword: &str, strings_only: bool| {
        out.push(AnnotationEntry { keyword: keyword.to_string(), value: e[keyword].clone(), strings_only });
    };
    for name in e.keys() {
        match name.as_str() {
            "title" | "description" | "default" => {
                if meta_data {
                    add(name, false)
                }
            }
            "examples" => {
                if meta_data && dialect >= Dialect::Draft6 {
                    add(name, false)
                }
            }
            "readOnly" | "writeOnly" => {
                if meta_data && dialect >= Dialect::Draft7 {
                    add(name, false)
                }
            }
            "deprecated" => {
                if meta_data && dialect >= Dialect::Draft201909 {
                    add(name, false)
                }
            }
            "format" => {
                if e["format"].is_string() && format_annotate {
                    add(name, false)
                }
            }
            _ => {
                // Unknown keywords are collected as annotations from 2019-09 onwards.
                if dialect >= Dialect::Draft201909 && !KNOWN_KEYWORDS.contains(&name.as_str()) {
                    add(name, false)
                }
            }
        }
    }
    if content
        && dialect >= Dialect::Draft7
        && (e.contains_key("contentEncoding") || e.contains_key("contentMediaType") || e.contains_key("contentSchema"))
    {
        if e.contains_key("contentEncoding") {
            add("contentEncoding", true);
        }
        if e.contains_key("contentMediaType") {
            add("contentMediaType", true);
            // contentSchema is only meaningful alongside contentMediaType.
            if e.contains_key("contentSchema") && dialect >= Dialect::Draft201909 {
                add("contentSchema", true);
            }
        }
    }
    (!out.is_empty()).then_some(out)
}

#[allow(dead_code)]
pub(crate) fn value_addr(v: &Value) -> usize {
    addr(v)
}
