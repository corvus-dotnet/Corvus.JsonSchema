//! The compiled schema graph: one `SchemaNode` per distinct (document, pointer), with pre-digested keyword data.
//! A port of `Corvus.Text.Json.RuntimeEvaluator.Compilation.SchemaNode` (via the TypeScript port's `node.ts`).

use std::sync::Arc;

use serde_json::{Number, Value};

use crate::dialect::Dialect;
use crate::formats::FormatKind;
use crate::pattern::Pattern;

/// A node index in the compiled graph.
pub(crate) type NodeId = u32;

/// JSON types as bits.
pub(crate) mod type_mask {
    pub const NULL: u8 = 1;
    pub const BOOLEAN: u8 = 2;
    pub const OBJECT: u8 = 4;
    pub const ARRAY: u8 = 8;
    pub const NUMBER: u8 = 16;
    pub const STRING: u8 = 32;
    pub const INTEGER: u8 = 64;
}

#[derive(Clone, Copy, PartialEq, Eq, Debug)]
pub(crate) enum ContentKind {
    None,
    Base64,
    Json,
    Base64Json,
}

/// An annotation-producing keyword and its value (reported in verbose results).
#[derive(Clone, Debug)]
pub(crate) struct AnnotationEntry {
    pub keyword: String,
    pub value: Value,
    /// Reported only when the instance is a string (content keywords).
    pub strings_only: bool,
}

#[derive(Clone, Debug)]
pub(crate) struct PatternProperty {
    pub pattern: Arc<Pattern>,
    pub node: NodeId,
}

/// The keyword a dependency entry came from, which names its result rows.
#[derive(Clone, Copy, PartialEq, Eq, Debug)]
pub(crate) enum DependencyKeyword {
    Dependencies,
    DependentSchemas,
    DependentRequired,
}

impl DependencyKeyword {
    pub fn name(self) -> &'static str {
        match self {
            DependencyKeyword::Dependencies => "dependencies",
            DependencyKeyword::DependentSchemas => "dependentSchemas",
            DependencyKeyword::DependentRequired => "dependentRequired",
        }
    }
}

#[derive(Clone, Debug)]
pub(crate) struct DependencyEntry {
    pub keyword: DependencyKeyword,
    pub name: String,
    pub required: Option<Vec<String>>,
    pub schema: Option<NodeId>,
}

/// A `$dynamicRef`/`$recursiveRef` that stays dynamic after compile-time analysis.
#[derive(Clone, Debug)]
pub(crate) struct DynamicRefTarget {
    pub is_recursive: bool,
    pub fallback: NodeId,
    /// (resource id, node id of that resource's matching anchor), for the resources that define it.
    pub by_resource: Vec<(u32, NodeId)>,
}

/// A discriminator value: the JSON scalars `const`/`enum` can name.
#[derive(Clone, Debug, PartialEq)]
pub(crate) enum DiscriminatorValue {
    String(String),
    Number(Number),
    Bool(bool),
    Null,
}

impl DiscriminatorValue {
    /// Whether an instance value equals this one (numbers by value: `1` and `1.0` are the same key).
    pub fn matches<'a, I: crate::instance::Instance<'a>>(&self, v: I) -> bool {
        use crate::instance::View;
        match (self, v.view()) {
            (DiscriminatorValue::String(a), View::String(b)) => a == b,
            (DiscriminatorValue::Number(a), View::Number(b)) => crate::numbers::num_eq(a, &b),
            (DiscriminatorValue::Bool(a), View::Bool(b)) => *a == b,
            (DiscriminatorValue::Null, View::Null) => true,
            _ => false,
        }
    }

    /// Key equality, with numbers by value.
    pub fn same(&self, other: &DiscriminatorValue) -> bool {
        match (self, other) {
            (DiscriminatorValue::Number(a), DiscriminatorValue::Number(b)) => crate::numbers::num_eq(a, b),
            _ => self == other,
        }
    }
}

/// Selects oneOf/anyOf branches by the value of one property (`SchemaCompiler.BuildDiscriminator`).
#[derive(Clone, Debug)]
pub(crate) struct Discriminator {
    pub property: String,
    /// Known discriminator values and the branches (indexes into the keyword's list) each can select.
    pub known: Vec<(DiscriminatorValue, Vec<u32>)>,
    /// Branches that stay candidates for a value not in `known` (negative and wildcard branches).
    pub unknown: Vec<u32>,
    /// Every branch requires the property, so its absence fails the keyword at once.
    pub all_require: bool,
}

#[derive(Clone, Copy, PartialEq, Eq, Debug)]
pub(crate) enum StaticDynamicKeyword {
    DynamicRef,
    RecursiveRef,
}

impl StaticDynamicKeyword {
    pub fn name(self) -> &'static str {
        match self {
            StaticDynamicKeyword::DynamicRef => "$dynamicRef",
            StaticDynamicKeyword::RecursiveRef => "$recursiveRef",
        }
    }
}

#[derive(Debug)]
pub(crate) struct SchemaNode {
    pub resource_id: u32,
    pub dialect: Dialect,
    /// The JSON pointer of the schema within its document (C#'s SchemaLocation).
    pub pointer: String,

    pub always_true: bool,
    pub always_false: bool,

    // Assertions.
    pub type_mask: u8,
    pub has_type: bool,
    pub const_value: Option<Value>,
    pub enum_values: Option<Vec<Value>>,

    // References.
    pub ref_: Option<NodeId>,
    /// A `$dynamicRef`/`$recursiveRef` that compile-time analysis resolved statically, and its keyword.
    pub static_dynamic_ref: Option<NodeId>,
    pub static_dynamic_keyword: StaticDynamicKeyword,
    pub dynamic_ref: Option<Box<DynamicRefTarget>>,

    // In-place applicators.
    pub all_of: Option<Vec<NodeId>>,
    pub any_of: Option<Vec<NodeId>>,
    pub one_of: Option<Vec<NodeId>>,
    pub not: Option<NodeId>,
    pub if_: Option<NodeId>,
    pub then: Option<NodeId>,
    pub else_: Option<NodeId>,

    // Objects.
    pub properties: Option<Vec<(String, NodeId)>>,
    pub pattern_properties: Option<Vec<PatternProperty>>,
    pub additional_properties: Option<NodeId>,
    pub property_names: Option<NodeId>,
    /// `required` without duplicates.
    pub required: Option<Vec<String>>,
    /// `required` as written (duplicates kept), for results.
    pub required_list: Option<Vec<String>>,
    pub dependencies: Option<Vec<DependencyEntry>>,
    pub min_properties: Option<u64>,
    pub max_properties: Option<u64>,
    pub unevaluated_properties: Option<NodeId>,

    // Arrays.
    pub prefix_items: Option<Vec<NodeId>>,
    /// The keywords behind prefix_items/items: `prefixItems`/`items` (2020-12) or `items`/`additionalItems` (legacy).
    pub prefix_keyword: &'static str,
    pub items_keyword: &'static str,
    pub items: Option<NodeId>,
    pub contains: Option<NodeId>,
    pub min_contains: u64,
    pub max_contains: Option<u64>,
    pub contains_marks_evaluated: bool,
    pub min_items: Option<u64>,
    pub max_items: Option<u64>,
    pub unique_items: bool,
    pub unevaluated_items: Option<NodeId>,

    // Strings.
    pub min_length: Option<u64>,
    pub max_length: Option<u64>,
    pub pattern: Option<Arc<Pattern>>,
    pub format: Option<String>,
    pub format_kind: FormatKind,
    pub assert_format: bool,
    pub content: ContentKind,
    pub assert_content: bool,

    // Numbers.
    pub minimum: Option<Number>,
    pub maximum: Option<Number>,
    pub exclusive_minimum: Option<Number>,
    pub exclusive_maximum: Option<Number>,
    pub multiple_of: Option<Number>,

    // Analysis.
    pub marks_properties: bool,
    pub marks_items: bool,
    pub in_place_cycle: bool,
    pub one_of_discriminator: Option<Box<Discriminator>>,
    pub any_of_discriminator: Option<Box<Discriminator>>,
}

impl SchemaNode {
    pub fn new(resource_id: u32, dialect: Dialect, pointer: String) -> SchemaNode {
        SchemaNode {
            resource_id,
            dialect,
            pointer,
            always_true: false,
            always_false: false,
            type_mask: 0,
            has_type: false,
            const_value: None,
            enum_values: None,
            ref_: None,
            static_dynamic_ref: None,
            static_dynamic_keyword: StaticDynamicKeyword::DynamicRef,
            dynamic_ref: None,
            all_of: None,
            any_of: None,
            one_of: None,
            not: None,
            if_: None,
            then: None,
            else_: None,
            properties: None,
            pattern_properties: None,
            additional_properties: None,
            property_names: None,
            required: None,
            required_list: None,
            dependencies: None,
            min_properties: None,
            max_properties: None,
            unevaluated_properties: None,
            prefix_items: None,
            prefix_keyword: "prefixItems",
            items_keyword: "items",
            items: None,
            contains: None,
            min_contains: 1,
            max_contains: None,
            contains_marks_evaluated: false,
            min_items: None,
            max_items: None,
            unique_items: false,
            unevaluated_items: None,
            min_length: None,
            max_length: None,
            pattern: None,
            format: None,
            format_kind: FormatKind::Unknown,
            assert_format: false,
            content: ContentKind::None,
            assert_content: false,
            minimum: None,
            maximum: None,
            exclusive_minimum: None,
            exclusive_maximum: None,
            multiple_of: None,
            marks_properties: false,
            marks_items: false,
            in_place_cycle: false,
            one_of_discriminator: None,
            any_of_discriminator: None,
        }
    }

    /// Keywords that apply only to objects.
    pub fn has_object_keywords(&self) -> bool {
        self.properties.is_some()
            || self.pattern_properties.is_some()
            || self.additional_properties.is_some()
            || self.property_names.is_some()
            || self.required.is_some()
            || self.dependencies.is_some()
            || self.min_properties.is_some()
            || self.max_properties.is_some()
            || self.unevaluated_properties.is_some()
    }

    pub fn has_array_keywords(&self) -> bool {
        self.prefix_items.is_some()
            || self.items.is_some()
            || self.contains.is_some()
            || self.min_items.is_some()
            || self.max_items.is_some()
            || self.unique_items
            || self.unevaluated_items.is_some()
    }

    pub fn has_string_keywords(&self) -> bool {
        self.min_length.is_some()
            || self.max_length.is_some()
            || self.pattern.is_some()
            || (self.assert_format && self.format.is_some() && !self.format_kind.is_numeric())
            || self.assert_content
    }

    pub fn has_number_keywords(&self) -> bool {
        self.minimum.is_some()
            || self.maximum.is_some()
            || self.exclusive_minimum.is_some()
            || self.exclusive_maximum.is_some()
            || self.multiple_of.is_some()
            || (self.assert_format && self.format.is_some() && self.format_kind.is_numeric())
    }

    pub fn has_in_place_applicators(&self) -> bool {
        self.ref_.is_some()
            || self.static_dynamic_ref.is_some()
            || self.dynamic_ref.is_some()
            || self.all_of.is_some()
            || self.any_of.is_some()
            || self.one_of.is_some()
            || self.not.is_some()
            || self.if_.is_some()
            || self.dependencies.as_ref().is_some_and(|d| d.iter().any(|e| e.schema.is_some()))
    }

    /// Nothing but `$ref`: no other keyword that asserts.
    pub fn is_pure_ref(&self) -> bool {
        self.ref_.is_some()
            && !self.has_type
            && self.const_value.is_none()
            && self.enum_values.is_none()
            && !self.has_object_keywords()
            && !self.has_array_keywords()
            && !self.has_string_keywords()
            && !self.has_number_keywords()
            && self.dynamic_ref.is_none()
            && self.static_dynamic_ref.is_none()
            && self.all_of.is_none()
            && self.any_of.is_none()
            && self.one_of.is_none()
            && self.not.is_none()
            && self.if_.is_none()
            && self.dependencies.is_none()
    }

    /// In-place children (the instance is evaluated at the same location).
    pub fn in_place_children(&self, include_not: bool) -> Vec<NodeId> {
        let mut out = Vec::new();
        out.extend(self.ref_);
        out.extend(self.static_dynamic_ref);
        if let Some(d) = &self.dynamic_ref {
            out.push(d.fallback);
            out.extend(d.by_resource.iter().map(|&(_, n)| n));
        }
        for list in [&self.all_of, &self.any_of, &self.one_of].into_iter().flatten() {
            out.extend_from_slice(list);
        }
        if include_not {
            out.extend(self.not);
        }
        out.extend(self.if_);
        out.extend(self.then);
        out.extend(self.else_);
        if let Some(deps) = &self.dependencies {
            out.extend(deps.iter().filter_map(|d| d.schema));
        }
        out
    }

    /// Every child node.
    pub fn children(&self) -> Vec<NodeId> {
        let mut out = self.in_place_children(true);
        if let Some(p) = &self.properties {
            out.extend(p.iter().map(|&(_, n)| n));
        }
        if let Some(p) = &self.pattern_properties {
            out.extend(p.iter().map(|pp| pp.node));
        }
        for c in [
            self.additional_properties,
            self.property_names,
            self.unevaluated_properties,
            self.items,
            self.contains,
            self.unevaluated_items,
        ]
        .into_iter()
        .flatten()
        {
            out.push(c);
        }
        if let Some(p) = &self.prefix_items {
            out.extend_from_slice(p);
        }
        out
    }
}
