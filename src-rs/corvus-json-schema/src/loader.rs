//! Loads schema documents, identifies resources and anchors, and resolves references.
//! A port of `SchemaLoader` (via the TypeScript port's `loader.ts`): elements are identified by (document, JSON
//! pointer), and a value's address identifies it within the compile (documents are boxed and never mutated).

use std::collections::HashMap;

use serde_json::Value;

use crate::dialect::{Dialect, SubschemaKind, known_dialect, subschema_kind, vocab, vocabulary_flag};
use crate::metaschemas::metaschema;
use crate::options::{CompileOptions, SchemaCompilationError};
use crate::uri::{decode_fragment, escape_pointer_token, normalize, resolve, resolve_pointer, split};

const DEFAULT_ROOT_URI: &str = "https://corvus-oss.org/runtime-evaluator/root.json";

/// The identity of a JSON value within a loaded document.
#[inline]
pub(crate) fn addr(v: &Value) -> usize {
    v as *const Value as usize
}

pub(crate) struct SchemaDocument {
    pub root: Box<Value>,
    /// The resource that owns each visited schema value.
    pub resource_of: HashMap<usize, u32>,
}

pub(crate) struct SchemaResource {
    pub document: u32,
    pub root_pointer: String,
    pub uri: String,
    pub dialect: Dialect,
    pub vocabularies: u32,
    pub recursive_anchor: bool,
    pub anchors: HashMap<String, String>,
    pub dynamic_anchors: HashMap<String, String>,
}

/// The target of a reference: a value in a document, and the resource it belongs to.
#[derive(Clone)]
pub(crate) struct SchemaTarget {
    pub document: u32,
    pub pointer: String,
    pub value: *const Value,
    pub resource: u32,
}

impl SchemaTarget {
    /// The target value. Valid while the loader that produced the target lives (documents are boxed and
    /// never mutated or dropped before the loader).
    #[inline]
    pub fn value<'a>(&self) -> &'a Value {
        // SAFETY: see above; the compiler never lets a target outlive its loader.
        unsafe { &*self.value }
    }
}

pub(crate) struct SchemaLoader<'o> {
    pub documents: Vec<SchemaDocument>,
    pub resources: Vec<SchemaResource>,
    resources_by_uri: HashMap<String, u32>,
    documents_by_uri: HashMap<String, u32>,
    metaschema_info: HashMap<String, (Dialect, u32)>,
    metaschema_loading: Vec<String>,
    reference_cache: HashMap<(u32, String), Option<SchemaTarget>>,
    options: &'o CompileOptions,
}

fn is_schema_value(v: &Value) -> bool {
    v.is_boolean() || v.is_object()
}

impl<'o> SchemaLoader<'o> {
    pub fn new(options: &'o CompileOptions) -> SchemaLoader<'o> {
        SchemaLoader {
            documents: Vec::new(),
            resources: Vec::new(),
            resources_by_uri: HashMap::new(),
            documents_by_uri: HashMap::new(),
            metaschema_info: HashMap::new(),
            metaschema_loading: Vec::new(),
            reference_cache: HashMap::new(),
            options,
        }
    }

    pub fn load_root(&mut self, schema: Value, base_uri: Option<&str>) -> Result<u32, SchemaCompilationError> {
        let uri = base_uri.map(normalize).unwrap_or_else(|| DEFAULT_ROOT_URI.to_string());
        let doc = self.add_document(uri, schema)?;
        let d = &self.documents[doc as usize];
        Ok(d.resource_of[&addr(&d.root)])
    }

    pub fn load_root_from_uri(&mut self, uri: &str) -> Result<u32, SchemaCompilationError> {
        let normalized = normalize(uri);
        if !self.try_load_document(&normalized)? {
            return Err(SchemaCompilationError::new(format!("Unable to resolve the schema document '{uri}'.")));
        }
        let doc = self.documents_by_uri[&normalized];
        let d = &self.documents[doc as usize];
        Ok(d.resource_of[&addr(&d.root)])
    }

    /// The root value of a resource.
    pub fn resource_root(&self, resource: u32) -> &Value {
        let r = &self.resources[resource as usize];
        let doc = &self.documents[r.document as usize];
        resolve_pointer(&doc.root, &r.root_pointer).map(|(v, _)| v).unwrap_or(&doc.root)
    }

    pub fn root_target(&self, resource: u32) -> SchemaTarget {
        let r = &self.resources[resource as usize];
        SchemaTarget {
            document: r.document,
            pointer: r.root_pointer.clone(),
            value: self.resource_root(resource) as *const Value,
            resource,
        }
    }

    pub fn resource_of(&self, document: u32, value: &Value) -> Option<u32> {
        self.documents[document as usize].resource_of.get(&addr(value)).copied()
    }

    pub fn try_resolve_reference(
        &mut self,
        from: u32,
        reference: &str,
    ) -> Result<Option<SchemaTarget>, SchemaCompilationError> {
        let key = (from, reference.to_string());
        if let Some(t) = self.reference_cache.get(&key) {
            return Ok(t.clone());
        }
        let target = self.resolve_reference(from, reference)?;
        self.reference_cache.insert(key, target.clone());
        Ok(target)
    }

    fn resolve_reference(
        &mut self,
        from: u32,
        reference: &str,
    ) -> Result<Option<SchemaTarget>, SchemaCompilationError> {
        let (uri_part, fragment) = split(reference);
        let absolute = resolve(&self.resources[from as usize].uri, uri_part);
        let resource = match self.resources_by_uri.get(&absolute) {
            Some(&r) => r,
            None => {
                if !self.try_load_document(&absolute)? {
                    return Ok(None);
                }
                match self.resources_by_uri.get(&absolute) {
                    Some(&r) => r,
                    None => return Ok(None),
                }
            }
        };
        Ok(self.try_resolve_fragment(resource, &decode_fragment(fragment)))
    }

    pub fn try_resolve_fragment(&self, resource: u32, fragment: &str) -> Option<SchemaTarget> {
        if fragment.is_empty() {
            return Some(self.root_target(resource));
        }
        let r = &self.resources[resource as usize];
        if fragment.starts_with('/') {
            let root = self.resource_root(resource);
            let (value, path) = resolve_pointer(root, fragment)?;
            let owner = self.resource_of(r.document, value).unwrap_or(resource);
            return Some(SchemaTarget {
                document: r.document,
                pointer: format!("{}{}", r.root_pointer, path),
                value: value as *const Value,
                resource: owner,
            });
        }
        let anchor = r.anchors.get(fragment)?;
        let doc = &self.documents[r.document as usize];
        let (value, _) = resolve_pointer(&doc.root, anchor)?;
        Some(SchemaTarget { document: r.document, pointer: anchor.clone(), value: value as *const Value, resource })
    }

    pub fn get_dialect_info(&mut self, schema_uri: &str) -> Result<(Dialect, u32), SchemaCompilationError> {
        // The standard metaschema URIs, as usually written, need no URI normalisation.
        if let Some(d) = known_dialect(schema_uri.strip_suffix('#').unwrap_or(schema_uri)) {
            return Ok((d, vocab::ALL_ANNOTATING_FORMAT));
        }
        let normalized = normalize(schema_uri);
        if let Some(d) = known_dialect(&normalized) {
            return Ok((d, vocab::ALL_ANNOTATING_FORMAT));
        }
        if let Some(&info) = self.metaschema_info.get(&normalized) {
            return Ok(info);
        }
        if self.metaschema_loading.contains(&normalized) {
            return Ok((self.options.default_dialect, vocab::ALL_ANNOTATING_FORMAT));
        }
        self.metaschema_loading.push(normalized.clone());
        let result = self.load_metaschema_info(&normalized);
        self.metaschema_loading.retain(|u| u != &normalized);
        let info = result?;
        self.metaschema_info.insert(normalized, info);
        Ok(info)
    }

    fn load_metaschema_info(&mut self, normalized: &str) -> Result<(Dialect, u32), SchemaCompilationError> {
        let meta_resource = match self.resources_by_uri.get(normalized) {
            Some(&r) => r,
            None => {
                if !self.try_load_document(normalized)? {
                    return Ok((self.options.default_dialect, vocab::ALL_ANNOTATING_FORMAT));
                }
                match self.resources_by_uri.get(normalized) {
                    Some(&r) => r,
                    None => return Ok((self.options.default_dialect, vocab::ALL_ANNOTATING_FORMAT)),
                }
            }
        };
        let mut vocabularies = vocab::ALL_ANNOTATING_FORMAT;
        if let Some(Value::Object(v)) = self.resource_root(meta_resource).get("$vocabulary") {
            vocabularies = vocab::NONE;
            for name in v.keys() {
                vocabularies |= vocabulary_flag(name);
            }
            vocabularies |= vocab::CORE;
        }
        Ok((self.resources[meta_resource as usize].dialect, vocabularies))
    }

    fn try_load_document(&mut self, absolute_uri: &str) -> Result<bool, SchemaCompilationError> {
        if self.documents_by_uri.contains_key(absolute_uri) {
            return Ok(true);
        }
        if let Some(resolver) = &self.options.resolve_document {
            if let Some(doc) = resolver(absolute_uri) {
                self.add_document(absolute_uri.to_string(), doc)?;
                return Ok(true);
            }
        }
        if let Some(text) = metaschema(absolute_uri) {
            let doc: Value = serde_json::from_str(text).expect("embedded metaschemas are valid JSON");
            self.add_document(absolute_uri.to_string(), doc)?;
            return Ok(true);
        }
        Ok(false)
    }

    fn add_document(&mut self, uri: String, root: Value) -> Result<u32, SchemaCompilationError> {
        let id = self.documents.len() as u32;
        self.documents.push(SchemaDocument { root: Box::new(root), resource_of: HashMap::new() });
        self.documents_by_uri.insert(uri.clone(), id);
        // SAFETY: the boxed root is never moved or dropped while the loader lives.
        let root: &Value = unsafe { &*(self.documents[id as usize].root.as_ref() as *const Value) };
        let (dialect, vocabularies) = match root.get("$schema") {
            Some(Value::String(s)) if root.is_object() => self.get_dialect_info(s)?,
            _ => (self.options.default_dialect, vocab::ALL_ANNOTATING_FORMAT),
        };
        let resource = self.create_resource(id, String::new(), uri, dialect, vocabularies);
        self.walk(id, root, String::new(), resource, true)?;
        Ok(id)
    }

    fn create_resource(
        &mut self,
        document: u32,
        pointer: String,
        uri: String,
        dialect: Dialect,
        vocabularies: u32,
    ) -> u32 {
        let id = self.resources.len() as u32;
        self.resources.push(SchemaResource {
            document,
            root_pointer: pointer,
            uri: uri.clone(),
            dialect,
            vocabularies,
            recursive_anchor: false,
            anchors: HashMap::new(),
            dynamic_anchors: HashMap::new(),
        });
        self.resources_by_uri.entry(uri).or_insert(id);
        id
    }

    fn add_anchor(&mut self, resource: u32, name: &str, pointer: &str) {
        self.resources[resource as usize].anchors.entry(name.to_string()).or_insert_with(|| pointer.to_string());
    }

    fn walk(
        &mut self,
        doc: u32,
        element: &Value,
        pointer: String,
        mut resource: u32,
        mut is_resource_root: bool,
    ) -> Result<(), SchemaCompilationError> {
        let Value::Object(map) = element else {
            self.documents[doc as usize].resource_of.insert(addr(element), resource);
            return Ok(());
        };

        let (mut dialect, mut vocabularies) = {
            let r = &self.resources[resource as usize];
            (r.dialect, r.vocabularies)
        };
        if !is_resource_root {
            if let Some(Value::String(s)) = map.get("$schema") {
                (dialect, vocabularies) = self.get_dialect_info(s)?;
            }
        }

        let legacy_ref_overrides_siblings = dialect.is_legacy() && matches!(map.get("$ref"), Some(Value::String(_)));
        if !legacy_ref_overrides_siblings {
            let id_value = if dialect == Dialect::Draft4 { map.get("id") } else { map.get("$id") };
            if let Some(Value::String(id)) = id_value {
                let (uri_part, fragment) = split(id);
                if uri_part.is_empty() {
                    if !fragment.is_empty() && dialect.is_legacy() {
                        self.add_anchor(resource, fragment, &pointer);
                    }
                } else {
                    let absolute = resolve(&self.resources[resource as usize].uri, uri_part);
                    if !is_resource_root || absolute != self.resources[resource as usize].uri {
                        if is_resource_root {
                            self.resources_by_uri.entry(absolute.clone()).or_insert(resource);
                            self.resources[resource as usize].uri = absolute;
                        } else {
                            resource = self.create_resource(doc, pointer.clone(), absolute, dialect, vocabularies);
                            is_resource_root = true;
                        }
                    }
                    if !fragment.is_empty() && dialect.is_legacy() {
                        self.add_anchor(resource, fragment, &pointer);
                    }
                }
            }
            if dialect >= Dialect::Draft201909 {
                if let Some(Value::String(a)) = map.get("$anchor") {
                    self.add_anchor(resource, a, &pointer);
                }
            }
            if dialect >= Dialect::Draft202012 {
                if let Some(Value::String(name)) = map.get("$dynamicAnchor") {
                    self.resources[resource as usize]
                        .dynamic_anchors
                        .entry(name.clone())
                        .or_insert_with(|| pointer.clone());
                    self.add_anchor(resource, name, &pointer);
                }
            }
            if dialect == Dialect::Draft201909
                && is_resource_root
                && map.get("$recursiveAnchor") == Some(&Value::Bool(true))
            {
                self.resources[resource as usize].recursive_anchor = true;
            }
        }

        self.documents[doc as usize].resource_of.insert(addr(element), resource);

        for (name, value) in map {
            let base = || format!("{pointer}/{}", escape_pointer_token(name));
            match subschema_kind(name, dialect, legacy_ref_overrides_siblings) {
                SubschemaKind::Single => {
                    if is_schema_value(value) {
                        self.walk(doc, value, base(), resource, false)?;
                    }
                }
                SubschemaKind::SingleOrArray => {
                    if let Value::Array(items) = value {
                        let b = base();
                        for (i, v) in items.iter().enumerate() {
                            self.walk(doc, v, format!("{b}/{i}"), resource, false)?;
                        }
                    } else if is_schema_value(value) {
                        self.walk(doc, value, base(), resource, false)?;
                    }
                }
                SubschemaKind::Array => {
                    if let Value::Array(items) = value {
                        let b = base();
                        for (i, v) in items.iter().enumerate() {
                            self.walk(doc, v, format!("{b}/{i}"), resource, false)?;
                        }
                    }
                }
                SubschemaKind::Map => {
                    if let Value::Object(entries) = value {
                        let b = base();
                        for (entry, v) in entries {
                            if is_schema_value(v) {
                                self.walk(doc, v, format!("{b}/{}", escape_pointer_token(entry)), resource, false)?;
                            }
                        }
                    }
                }
                SubschemaKind::None => {}
            }
        }
        Ok(())
    }
}
