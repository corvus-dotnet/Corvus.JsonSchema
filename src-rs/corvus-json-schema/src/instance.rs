//! The instance values the evaluator reads: anything that can show itself as one of the six JSON kinds, with arrays and
//! objects read in place. `&serde_json::Value` is one; a host language's own values (for example Python objects, read
//! through its C API) can be another, so that evaluation needs no conversion to `serde_json::Value` first.

use serde_json::{Map, Number, Value};

/// A JSON value the evaluator can read in place. Cheap to copy (a reference or a handle); values reached through it
/// (items, property values, strings) live as long as `'a`.
pub trait Instance<'a>: Copy {
    /// An array of instances.
    type Array: ArrayView<'a, Item = Self>;
    /// An object of named instances.
    type Object: ObjectView<'a, Item = Self>;

    /// The value's kind and content.
    fn view(self) -> View<'a, Self>;
}

/// What an instance is.
pub enum View<'a, I: Instance<'a>> {
    Null,
    Bool(bool),
    Number(Number),
    String(&'a str),
    Array(I::Array),
    Object(I::Object),
}

/// The items of an array.
pub trait ArrayView<'a>: Copy {
    type Item: Instance<'a>;

    fn len(self) -> usize;

    fn is_empty(self) -> bool {
        self.len() == 0
    }

    /// The item at an index (which must be below `len`).
    fn get(self, index: usize) -> Self::Item;

    fn iter(self) -> impl Iterator<Item = Self::Item>;
}

/// The properties of an object, in their order.
pub trait ObjectView<'a>: Copy {
    type Item: Instance<'a>;

    fn len(self) -> usize;

    fn is_empty(self) -> bool {
        self.len() == 0
    }

    /// The value of a property.
    fn get(self, name: &str) -> Option<Self::Item>;

    fn contains_key(self, name: &str) -> bool {
        self.get(name).is_some()
    }

    fn iter(self) -> impl Iterator<Item = (&'a str, Self::Item)>;

    fn values(self) -> impl Iterator<Item = Self::Item> {
        self.iter().map(|(_, v)| v)
    }
}

impl<'a> Instance<'a> for &'a Value {
    type Array = &'a [Value];
    type Object = &'a Map<String, Value>;

    #[inline(always)]
    fn view(self) -> View<'a, Self> {
        match self {
            Value::Null => View::Null,
            Value::Bool(b) => View::Bool(*b),
            Value::Number(n) => View::Number(n.clone()),
            Value::String(s) => View::String(s),
            Value::Array(a) => View::Array(a.as_slice()),
            Value::Object(o) => View::Object(o),
        }
    }
}

impl<'a> ArrayView<'a> for &'a [Value] {
    type Item = &'a Value;

    #[inline(always)]
    fn len(self) -> usize {
        <[Value]>::len(self)
    }

    #[inline(always)]
    fn get(self, index: usize) -> &'a Value {
        &self[index]
    }

    #[inline(always)]
    fn iter(self) -> impl Iterator<Item = &'a Value> {
        <[Value]>::iter(self)
    }
}

/// Objects up to this size are searched by scanning their keys: comparing lengths first, that is cheaper than hashing
/// the name with serde_json's SipHash.
const LINEAR_KEYS: usize = 32;

impl<'a> ObjectView<'a> for &'a Map<String, Value> {
    type Item = &'a Value;

    #[inline(always)]
    fn len(self) -> usize {
        Map::len(self)
    }

    #[inline]
    fn get(self, name: &str) -> Option<&'a Value> {
        if Map::len(self) <= LINEAR_KEYS {
            Map::iter(self).find(|(k, _)| str_eq(k, name)).map(|(_, v)| v)
        } else {
            Map::get(self, name)
        }
    }

    #[inline]
    fn contains_key(self, name: &str) -> bool {
        if Map::len(self) <= LINEAR_KEYS { self.keys().any(|k| str_eq(k, name)) } else { Map::contains_key(self, name) }
    }

    #[inline(always)]
    fn iter(self) -> impl Iterator<Item = (&'a str, &'a Value)> {
        Map::iter(self).map(|(k, v)| (k.as_str(), v))
    }

    #[inline(always)]
    fn values(self) -> impl Iterator<Item = &'a Value> {
        Map::values(self)
    }
}

/// String equality with the length test and short comparisons inline (property names are short).
#[inline(always)]
pub(crate) fn str_eq(a: &str, b: &str) -> bool {
    let (a, b) = (a.as_bytes(), b.as_bytes());
    if a.len() != b.len() {
        return false;
    }
    let n = a.len();
    if n <= 8 {
        if n >= 4 {
            // Two overlapping four-byte words cover every byte (one load each, no per-byte bounds checks).
            let word = |s: &[u8], i: usize| u32::from_le_bytes(s[i..i + 4].try_into().unwrap());
            return word(a, 0) == word(b, 0) && word(a, n - 4) == word(b, n - 4);
        }
        return a == b;
    }
    // Eight-byte words from the start, then one ending at the last byte (overlapping the previous one): property names
    // are short enough that a call to memcmp costs more than the compare.
    let word = |s: &[u8], i: usize| u64::from_le_bytes(s[i..i + 8].try_into().unwrap());
    let mut i = 0;
    while i + 8 < n {
        if word(a, i) != word(b, i) {
            return false;
        }
        i += 8;
    }
    word(a, n - 8) == word(b, n - 8)
}
