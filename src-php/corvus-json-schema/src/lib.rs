//! The `corvus_json_schema` PHP extension: the corvus-json-schema crate, with PHP values read in place (see
//! src-rs/BINDINGS.md). Namespace `Corvus\JsonSchema`: `Validator`, `Collector`, the `Dialect` and `ResultsLevel`
//! enums, and the exceptions.
#![cfg_attr(windows, feature(abi_vectorcall))]

use std::cell::{Cell, RefCell};
use std::sync::{Arc, Mutex};

use corvus::{
    ArrayView, CompileOptions, Instance, JsonSchemaResultsCollector, Kind, ObjectView, ResultsLevel as Level, View,
};
use ext_php_rs::boxed::ZBox;
use ext_php_rs::exception::PhpException;
use ext_php_rs::ffi::{Bucket, zend_hash_index_find, zend_hash_str_find, zend_standard_class_def};
use ext_php_rs::prelude::*;
use ext_php_rs::types::{ZendCallable, ZendHashTable, ZendObject, Zval};
use ext_php_rs::zend::{ClassEntry, ce};
use serde_json::{Map, Number, Value as Json};

/// Arrays and objects nested deeper than this are converted (with a depth check) rather than read in place.
const MAX_NESTING: u32 = 256;

// zval type bytes (Zend/zend_types.h).
const IS_UNDEF: u8 = 0;
const IS_NULL: u8 = 1;
const IS_FALSE: u8 = 2;
const IS_TRUE: u8 = 3;
const IS_LONG: u8 = 4;
const IS_DOUBLE: u8 = 5;
const IS_STRING: u8 = 6;
const IS_ARRAY: u8 = 7;
const IS_OBJECT: u8 = 8;
const IS_REFERENCE: u8 = 10;
const IS_INDIRECT: u8 = 12;

/// HASH_FLAG_PACKED (Zend/zend_hash.h): the table holds plain zvals indexed by integer key, with holes as IS_UNDEF.
const HASH_FLAG_PACKED: u32 = 1 << 2;
/// ZEND_ACC_ENUM (Zend/zend_compile.h).
const ZEND_ACC_ENUM: u32 = 1 << 28;

#[inline(always)]
fn type_of(z: &Zval) -> u8 {
    // SAFETY: every zval has its type in the low byte of u1.type_info.
    (unsafe { z.u1.type_info } & 0xff) as u8
}

/// The value a reference or an indirect slot (in an object's property table) stands for.
#[inline(always)]
fn deref(mut z: &Zval) -> &Zval {
    loop {
        match type_of(z) {
            // SAFETY: the type says which member of the value is set; both point at a live zval.
            IS_REFERENCE => z = unsafe { &(*z.value.ref_).val },
            IS_INDIRECT => z = unsafe { &*z.value.zv },
            _ => return z,
        }
    }
}

// ---------------------------------------------------------------------------------------------------------------------
// Exceptions

#[php_class]
#[php(name = "Corvus\\JsonSchema\\JsonSchemaException")]
#[php(extends(ce = ce::exception, stub = "\\Exception"))]
#[derive(Default)]
pub struct JsonSchemaException;

#[php_class]
#[php(name = "Corvus\\JsonSchema\\CompilationException")]
#[php(extends(JsonSchemaException))]
#[derive(Default)]
pub struct CompilationException;

#[php_class]
#[php(name = "Corvus\\JsonSchema\\DepthException")]
#[php(extends(JsonSchemaException))]
#[derive(Default)]
pub struct DepthException;

#[php_class]
#[php(name = "Corvus\\JsonSchema\\InvalidJsonException")]
#[php(extends(JsonSchemaException))]
#[derive(Default)]
pub struct InvalidJsonException;

fn depth_error(e: corvus::SchemaEvaluationDepthError) -> PhpException {
    PhpException::from_class::<DepthException>(e.to_string())
}

fn type_error(message: impl Into<String>) -> PhpException {
    PhpException::new(message.into(), 0, ce::type_error())
}

fn value_error(message: impl Into<String>) -> PhpException {
    PhpException::new(message.into(), 0, ce::value_error())
}

// ---------------------------------------------------------------------------------------------------------------------
// PHP values read in place

/// The state of one evaluation: whether a value could not be read in place (so the caller converts the instance and
/// evaluates that instead), and the text of integer keys, made when the evaluator reads an array as an object.
struct Context {
    fallback: Cell<bool>,
    keys: RefCell<Vec<Box<str>>>,
}

impl Context {
    fn new() -> Context {
        Context { fallback: Cell::new(false), keys: RefCell::new(Vec::new()) }
    }

    /// An integer key's decimal text, kept for the evaluation.
    fn key_text(&self, h: u64) -> &str {
        let text: Box<str> = (h as i64).to_string().into_boxed_str();
        let ptr: *const str = &*text;
        self.keys.borrow_mut().push(text);
        // SAFETY: the box lives in the context and is never removed or changed while the context lives (a box's
        // contents do not move when the vector holding it grows).
        unsafe { &*ptr }
    }
}

/// A PHP value read as a JSON value: a list (`array_is_list`, the empty array included) is an array, any other array an
/// object (integer keys read as their decimal text), a `stdClass` an object, and strings (UTF-8), integers, floats,
/// booleans and null themselves. Anything else (other objects, invalid UTF-8, NaN and infinities, nesting deeper than
/// `MAX_NESTING`) sets the context's fallback.
///
/// PHP never moves values, and the instance passed in holds a reference to everything reachable from it for the whole
/// call. Arrays are copied on write, so PHP code cannot change them under the evaluator; objects and references could
/// be changed in place, but no PHP code runs during the evaluation (with PHP format validators the caller converts the
/// instance first).
#[derive(Clone, Copy)]
struct PhpInstance<'a> {
    z: &'a Zval,
    ctx: &'a Context,
    depth: u32,
}

impl<'a> PhpInstance<'a> {
    fn root(z: &'a Zval, ctx: &'a Context) -> Self {
        PhpInstance { z: deref(z), ctx, depth: 0 }
    }

    #[inline(always)]
    fn child(self, z: &'a Zval) -> Self {
        PhpInstance { z: deref(z), ctx: self.ctx, depth: self.depth + 1 }
    }

    #[cold]
    fn unsupported(self) -> View<'a, Self> {
        self.ctx.fallback.set(true);
        View::Null
    }
}

/// A string's text when it is valid UTF-8.
#[inline(always)]
fn str_of(z: &Zval) -> Option<&str> {
    // SAFETY: the zval is a string (checked by the caller); its bytes live as long as the zval.
    let s = unsafe { &*z.value.str_ };
    let bytes = unsafe { std::slice::from_raw_parts(s.val.as_ptr().cast::<u8>(), s.len) };
    std::str::from_utf8(bytes).ok()
}

fn key_of(key: *mut ext_php_rs::ffi::zend_string) -> Option<&'static [u8]> {
    // SAFETY: a bucket's key is null or a live string, which outlives the evaluation (see `PhpInstance`).
    unsafe { key.as_ref().map(|s| std::slice::from_raw_parts(s.val.as_ptr().cast::<u8>(), s.len)) }
}

/// A hash table's entries, as the evaluator reads them.
#[derive(Clone, Copy)]
enum Table<'a> {
    /// Plain zvals, the key being the index; holes are IS_UNDEF.
    Packed(&'a [Zval]),
    /// Buckets with integer or string keys; holes are IS_UNDEF.
    Hash(&'a [Bucket]),
}

impl<'a> Table<'a> {
    fn of(ht: &'a ZendHashTable) -> Table<'a> {
        let used = ht.nNumUsed as usize;
        // SAFETY: the flags say which member of the data union is set, and nNumUsed counts its slots.
        unsafe {
            if ht.u.flags & HASH_FLAG_PACKED != 0 {
                Table::Packed(slice(ht.__bindgen_anon_1.arPacked, used))
            } else {
                Table::Hash(slice(ht.__bindgen_anon_1.arData, used))
            }
        }
    }
}

/// A slice over a hash table's slots (a table with no slots may have a dangling data pointer).
unsafe fn slice<'a, T>(ptr: *const T, len: usize) -> &'a [T] {
    if len == 0 { &[] } else { unsafe { std::slice::from_raw_parts(ptr, len) } }
}

impl<'a> Instance<'a> for PhpInstance<'a> {
    type Array = PhpArray<'a>;
    type Object = PhpObject<'a>;

    fn view(self) -> View<'a, Self> {
        let z = self.z;
        match type_of(z) {
            IS_NULL => View::Null,
            IS_FALSE => View::Bool(false),
            IS_TRUE => View::Bool(true),
            // SAFETY (each arm): the type says which member of the value is set.
            IS_LONG => View::Number(unsafe { z.value.lval }.into()),
            IS_DOUBLE => Number::from_f64(unsafe { z.value.dval }).map_or_else(|| self.unsupported(), View::Number),
            IS_STRING => str_of(z).map_or_else(|| self.unsupported(), View::String),
            IS_ARRAY => {
                if self.depth >= MAX_NESTING {
                    return self.unsupported();
                }
                let ht = unsafe { &*z.value.arr };
                let count = ht.nNumOfElements as usize;
                match Table::of(ht) {
                    // No holes: the keys are 0 to n - 1 in order.
                    Table::Packed(slots) if slots.len() == count => {
                        View::Array(PhpArray { items: Items::Packed(slots), parent: self })
                    }
                    Table::Hash(buckets) if is_list(buckets, count) => {
                        View::Array(PhpArray { items: Items::Hash(buckets), parent: self })
                    }
                    table => View::Object(PhpObject { table, count, symtable: true, parent: self }),
                }
            }
            IS_OBJECT => {
                if self.depth >= MAX_NESTING {
                    return self.unsupported();
                }
                let obj = unsafe { &*z.value.obj };
                // Only a stdClass is read in place: its properties are all in its property table (which is null for
                // one that never had any).
                if obj.ce != unsafe { zend_standard_class_def } {
                    return self.unsupported();
                }
                match unsafe { obj.properties.as_ref() } {
                    None => {
                        View::Object(PhpObject { table: Table::Hash(&[]), count: 0, symtable: false, parent: self })
                    }
                    Some(ht) => View::Object(PhpObject {
                        table: Table::of(ht),
                        count: ht.nNumOfElements as usize,
                        symtable: false,
                        parent: self,
                    }),
                }
            }
            _ => self.unsupported(),
        }
    }

    fn kind(self) -> Kind {
        match type_of(self.z) {
            IS_NULL => Kind::Null,
            IS_FALSE | IS_TRUE => Kind::Bool,
            IS_LONG => Kind::Number,
            _ => match self.view() {
                View::Null => Kind::Null,
                View::Bool(_) => Kind::Bool,
                View::Number(_) => Kind::Number,
                View::String(_) => Kind::String,
                View::Array(_) => Kind::Array,
                View::Object(_) => Kind::Object,
            },
        }
    }
}

/// Whether a hash-form array is a list with no holes: integer keys 0 to n - 1, in order.
fn is_list(buckets: &[Bucket], count: usize) -> bool {
    buckets.len() == count && buckets.iter().enumerate().all(|(i, b)| b.key.is_null() && b.h == i as u64)
}

#[derive(Clone, Copy)]
enum Items<'a> {
    Packed(&'a [Zval]),
    Hash(&'a [Bucket]),
}

#[derive(Clone, Copy)]
struct PhpArray<'a> {
    items: Items<'a>,
    parent: PhpInstance<'a>,
}

impl<'a> ArrayView<'a> for PhpArray<'a> {
    type Item = PhpInstance<'a>;

    fn len(self) -> usize {
        match self.items {
            Items::Packed(s) => s.len(),
            Items::Hash(s) => s.len(),
        }
    }

    fn get(self, index: usize) -> PhpInstance<'a> {
        match self.items {
            Items::Packed(s) => self.parent.child(&s[index]),
            Items::Hash(s) => self.parent.child(&s[index].val),
        }
    }

    fn iter(self) -> impl Iterator<Item = PhpInstance<'a>> {
        let parent = self.parent;
        let (packed, hash): (&'a [Zval], &'a [Bucket]) = match self.items {
            Items::Packed(s) => (s, &[]),
            Items::Hash(s) => (&[], s),
        };
        packed.iter().chain(hash.iter().map(|b| &b.val)).map(move |z| parent.child(z))
    }
}

#[derive(Clone, Copy)]
struct PhpObject<'a> {
    table: Table<'a>,
    count: usize,
    /// An array's table, whose keys that are canonical decimal integers are stored as integers (a property table's keys
    /// are all strings).
    symtable: bool,
    parent: PhpInstance<'a>,
}

/// Scanned rather than looked up by hash below this size.
const SCAN_LIMIT: usize = 8;

impl<'a> PhpObject<'a> {
    #[inline(always)]
    fn entry(self, key: Option<&'a [u8]>, h: u64) -> &'a str {
        match key {
            Some(bytes) => std::str::from_utf8(bytes).unwrap_or_else(|_| {
                self.parent.ctx.fallback.set(true);
                ""
            }),
            None => self.parent.ctx.key_text(h),
        }
    }
}

impl<'a> ObjectView<'a> for PhpObject<'a> {
    type Item = PhpInstance<'a>;

    fn len(self) -> usize {
        self.count
    }

    fn get(self, name: &str) -> Option<PhpInstance<'a>> {
        match self.table {
            Table::Hash(buckets) if buckets.len() <= SCAN_LIMIT => {
                let index = if self.symtable { integer_key(name) } else { None };
                buckets
                    .iter()
                    .find(|b| {
                        type_of(&b.val) != IS_UNDEF
                            && match key_of(b.key) {
                                Some(k) => k == name.as_bytes(),
                                None => index == Some(b.h),
                            }
                    })
                    .map(|b| self.parent.child(&b.val))
            }
            Table::Packed(slots) => {
                let i = integer_key(name)? as usize;
                slots.get(i).filter(|z| type_of(z) != IS_UNDEF).map(|z| self.parent.child(z))
            }
            Table::Hash(_) => {
                let table = self.hashtable();
                // SAFETY: the table is live (see `PhpInstance`); the lookups only read it.
                let found = unsafe {
                    match if self.symtable { integer_key(name) } else { None } {
                        Some(h) => zend_hash_index_find(table, h),
                        None => zend_hash_str_find(table, name.as_ptr().cast(), name.len()),
                    }
                };
                // SAFETY: a found slot is a live zval in the table.
                unsafe { found.as_ref() }.filter(|z| type_of(z) != IS_UNDEF).map(|z| self.parent.child(z))
            }
        }
    }

    fn iter(self) -> impl Iterator<Item = (&'a str, PhpInstance<'a>)> {
        let (packed, hash): (&'a [Zval], &'a [Bucket]) = match self.table {
            Table::Packed(s) => (s, &[]),
            Table::Hash(s) => (&[], s),
        };
        let packed = packed
            .iter()
            .enumerate()
            .filter(|(_, z)| type_of(z) != IS_UNDEF)
            .map(move |(i, z)| (self.parent.ctx.key_text(i as u64) as &'a str, self.parent.child(z)));
        let hash = hash
            .iter()
            .filter(|b| type_of(&b.val) != IS_UNDEF)
            .map(move |b| (self.entry(key_of(b.key), b.h), self.parent.child(&b.val)));
        packed.chain(hash)
    }
}

impl PhpObject<'_> {
    /// The hash table the buckets belong to (the instance's array, or its object's property table).
    fn hashtable(self) -> *const ZendHashTable {
        let z = self.parent.z;
        // SAFETY: the parent's type says which member of its value is set.
        unsafe {
            if type_of(z) == IS_ARRAY { z.value.arr.cast_const() } else { (*z.value.obj).properties.cast_const() }
        }
    }
}

/// The integer a PHP array stores a key as, when the key is a canonical decimal integer ("0", or an optional minus sign
/// then digits without a leading zero, within a long's range: ZEND_HANDLE_NUMERIC_STR).
fn integer_key(name: &str) -> Option<u64> {
    let b = name.as_bytes();
    let digits = b.strip_prefix(b"-").unwrap_or(b);
    if digits.is_empty() || digits.len() > 20 || !digits.iter().all(u8::is_ascii_digit) {
        return None;
    }
    if digits[0] == b'0' && (digits.len() > 1 || b.len() > 1) {
        return None;
    }
    name.parse::<i64>().ok().map(|n| n as u64)
}

// ---------------------------------------------------------------------------------------------------------------------
// Conversions

/// A PHP value as JSON (for schemas, and for instances that cannot be read in place), as `json_encode` reads it: lists
/// as arrays and other arrays as objects; a `JsonSerializable` as what its `jsonSerialize` returns; a backed enum as
/// its value; any other object as its public properties.
fn to_json(z: &Zval, depth: u32) -> PhpResult<Json> {
    if depth > 1024 {
        return Err(value_error("the value nests too deeply"));
    }
    let z = deref(z);
    Ok(match type_of(z) {
        IS_NULL => Json::Null,
        IS_FALSE => Json::Bool(false),
        IS_TRUE => Json::Bool(true),
        IS_LONG => Json::Number(unsafe { z.value.lval }.into()),
        IS_DOUBLE => Json::Number(
            Number::from_f64(unsafe { z.value.dval })
                .ok_or_else(|| value_error("NaN and infinities are not JSON numbers"))?,
        ),
        IS_STRING => Json::String(str_of(z).ok_or_else(|| value_error("a string is not valid UTF-8"))?.to_owned()),
        IS_ARRAY => table_to_json(unsafe { &*z.value.arr }, true, depth)?,
        IS_OBJECT => object_to_json(unsafe { &*z.value.obj }, depth)?,
        _ => return Err(type_error(format!("a {} is not a JSON value", z.get_type()))),
    })
}

fn table_to_json(ht: &ZendHashTable, symtable: bool, depth: u32) -> PhpResult<Json> {
    let count = ht.nNumOfElements as usize;
    let table = Table::of(ht);
    let list = match table {
        Table::Packed(slots) => symtable && slots.len() == count,
        Table::Hash(buckets) => symtable && is_list(buckets, count),
    };
    if list {
        let mut out = Vec::with_capacity(count);
        match table {
            Table::Packed(slots) => {
                for z in slots {
                    out.push(to_json(z, depth + 1)?);
                }
            }
            Table::Hash(buckets) => {
                for b in buckets {
                    out.push(to_json(&b.val, depth + 1)?);
                }
            }
        }
        return Ok(Json::Array(out));
    }
    let mut out = Map::with_capacity(count);
    match table {
        Table::Packed(slots) => {
            for (i, z) in slots.iter().enumerate().filter(|(_, z)| type_of(z) != IS_UNDEF) {
                out.insert(i.to_string(), to_json(z, depth + 1)?);
            }
        }
        Table::Hash(buckets) => {
            for b in buckets.iter().filter(|b| type_of(&b.val) != IS_UNDEF) {
                let key = match key_of(b.key) {
                    // A property table's private and protected names start with NUL ("\0Class\0name"): skipped, as
                    // json_encode skips them.
                    Some(k) if !symtable && k.first() == Some(&0) => continue,
                    Some(k) => {
                        std::str::from_utf8(k).map_err(|_| value_error("an object key is not valid UTF-8"))?.to_owned()
                    }
                    None => (b.h as i64).to_string(),
                };
                out.insert(key, to_json(&b.val, depth + 1)?);
            }
        }
    }
    Ok(Json::Object(out))
}

fn object_to_json(obj: &ZendObject, depth: u32) -> PhpResult<Json> {
    if let Some(serializable) = ClassEntry::try_find("JsonSerializable")
        && obj.instance_of(serializable)
    {
        let value = obj.try_call_method("jsonSerialize", vec![])?;
        return to_json(&value, depth + 1);
    }
    let class = obj.get_class_entry();
    if class.ce_flags & ZEND_ACC_ENUM != 0 {
        let properties = obj.get_properties()?;
        return match properties.get("value") {
            Some(value) => to_json(value, depth + 1),
            None => {
                Err(type_error(format!("the non-backed enum {} is not a JSON value", class.name().unwrap_or_default())))
            }
        };
    }
    table_to_json(obj.get_properties()?, false, depth)
}

/// JSON as a PHP value (for results and annotations): objects as associative arrays.
fn to_php(j: &Json) -> PhpResult<Zval> {
    let mut z = Zval::new();
    match j {
        Json::Null => z.set_null(),
        Json::Bool(b) => z.set_bool(*b),
        Json::Number(n) => match n.as_i64() {
            Some(i) => z.set_long(i),
            None => z.set_double(n.as_f64().unwrap_or(f64::NAN)),
        },
        Json::String(s) => z.set_string(s, false)?,
        Json::Array(a) => {
            let mut ht = ZendHashTable::with_capacity(a.len() as u32);
            for item in a {
                ht.push(to_php(item)?)?;
            }
            z.set_hashtable(ht);
        }
        Json::Object(o) => {
            let mut ht = ZendHashTable::with_capacity(o.len() as u32);
            for (k, v) in o {
                ht.insert(k.as_str(), to_php(v)?)?;
            }
            z.set_hashtable(ht);
        }
    }
    Ok(z)
}

// ---------------------------------------------------------------------------------------------------------------------
// Callbacks

/// A PHP callable kept for the validator (the validator holds a reference to it).
struct Callback(Zval);

// SAFETY: callbacks run only on the PHP thread that evaluates (a PHP object is used on the thread that made it); the
// crate requires Send + Sync of the closures that hold them.
unsafe impl Send for Callback {}
unsafe impl Sync for Callback {}

thread_local! {
    /// Whether a PHP callback threw during the current call. Its exception stays pending in the engine, so later
    /// callbacks are not called, and the call returns to PHP, which throws it.
    static CALLBACK_THREW: Cell<bool> = const { Cell::new(false) };
}

impl Callback {
    fn call(&self, arg: &str) -> Option<Zval> {
        if CALLBACK_THREW.get() {
            return None;
        }
        let result = ZendCallable::new(&self.0).and_then(|c| c.try_call(vec![&arg]));
        match result {
            Ok(v) => Some(v),
            Err(_) => {
                CALLBACK_THREW.set(true);
                None
            }
        }
    }
}

/// Ends a call that ran PHP callbacks: an exception one threw is thrown.
fn callbacks_done() -> PhpResult<()> {
    if CALLBACK_THREW.replace(false) {
        // The pending exception propagates; this one is dropped (throw does nothing while one is pending).
        return Err(PhpException::from_message("a callback threw".into()));
    }
    Ok(())
}

// ---------------------------------------------------------------------------------------------------------------------
// Enums

/// The dialect of schemas without `$schema`.
#[php_enum]
#[php(name = "Corvus\\JsonSchema\\Dialect")]
pub enum Dialect {
    Draft4,
    Draft6,
    Draft7,
    Draft201909,
    Draft202012,
}

/// How much a `Collector` records.
#[php_enum]
#[php(name = "Corvus\\JsonSchema\\ResultsLevel")]
pub enum ResultsLevel {
    /// The failures, without messages (and the root's own result, as at every level).
    Basic,
    /// The failures, with messages.
    Detailed,
    /// Every result, and annotations.
    Verbose,
}

// ---------------------------------------------------------------------------------------------------------------------
// Validator

/// A compiled schema.
#[php_class]
#[php(name = "Corvus\\JsonSchema\\Validator")]
#[php(flags = ext_php_rs::flags::ClassFlags::Final)]
pub struct Validator {
    inner: corvus::Validator,
    /// Format validators written in PHP run PHP code during an evaluation, so instances are converted first.
    php_formats: bool,
}

#[php_impl]
impl Validator {
    /// Compiles a schema (an array, an object, a bool, or JSON text) into a `Validator`. Options (an array): `defaultDialect`
    /// (a `Dialect`, for schemas without `$schema`), `assertFormat` (null follows the vocabularies),
    /// `assertFormatInLegacyDrafts`, `assertContent`, `formats` (format name => callable(string): bool), `resolver`
    /// (callable(string $uri) returning the document, as an array, an object or JSON text, or null when unknown),
    /// `baseUri`, `entryPoint` (a reference such as "#/$defs/item") and `maxDepth` (of in-place recursion, default 128).
    pub fn compile(schema: &Zval, options: Option<&ZendHashTable>) -> PhpResult<Validator> {
        let schema = deref(schema);
        let schema: Json = if type_of(schema) == IS_STRING {
            let text = str_of(schema).ok_or_else(|| {
                PhpException::from_class::<InvalidJsonException>("the schema is not valid UTF-8".into())
            })?;
            serde_json::from_str(text).map_err(|e| {
                PhpException::from_class::<InvalidJsonException>(format!("the schema is not valid JSON: {e}"))
            })?
        } else {
            to_json(schema, 0)?
        };
        let mut compile = CompileOptions::default();
        let mut php_formats = false;
        let resolver_error: Arc<Mutex<Option<String>>> = Arc::default();
        if let Some(options) = options {
            for (key, value) in options {
                let key = String::try_from(key)?;
                let value = deref(value);
                let optional_bool = || -> PhpResult<Option<bool>> {
                    match type_of(value) {
                        IS_NULL => Ok(None),
                        IS_TRUE => Ok(Some(true)),
                        IS_FALSE => Ok(Some(false)),
                        _ => Err(type_error(format!("the {key} option must be a bool"))),
                    }
                };
                let optional_string = || -> PhpResult<Option<String>> {
                    match type_of(value) {
                        IS_NULL => Ok(None),
                        IS_STRING => Ok(Some(
                            value
                                .string()
                                .ok_or_else(|| value_error(format!("the {key} option is not valid UTF-8")))?,
                        )),
                        _ => Err(type_error(format!("the {key} option must be a string"))),
                    }
                };
                match key.as_str() {
                    "defaultDialect" => {
                        compile.default_dialect = match value.extract::<Dialect>() {
                            Some(Dialect::Draft4) => corvus::Dialect::Draft4,
                            Some(Dialect::Draft6) => corvus::Dialect::Draft6,
                            Some(Dialect::Draft7) => corvus::Dialect::Draft7,
                            Some(Dialect::Draft201909) => corvus::Dialect::Draft201909,
                            Some(Dialect::Draft202012) => corvus::Dialect::Draft202012,
                            None => {
                                return Err(type_error(
                                    "the defaultDialect option must be a Corvus\\JsonSchema\\Dialect",
                                ));
                            }
                        }
                    }
                    "assertFormat" => compile.assert_format = optional_bool()?,
                    "assertFormatInLegacyDrafts" => {
                        compile.assert_format_in_legacy_drafts = optional_bool()?.unwrap_or(false)
                    }
                    "assertContent" => compile.assert_content = optional_bool()?.unwrap_or(true),
                    "baseUri" => compile.base_uri = optional_string()?,
                    "entryPoint" => compile.entry_point = optional_string()?,
                    "maxDepth" => {
                        compile.max_depth = value
                            .long()
                            .and_then(|d| u32::try_from(d).ok())
                            .ok_or_else(|| type_error("the maxDepth option must be a non-negative int"))?;
                    }
                    "formats" => {
                        let Some(formats) = value.array() else {
                            return Err(type_error("the formats option must be an array of format name => callable"));
                        };
                        for (name, f) in formats {
                            let name = String::try_from(name)?;
                            if !f.is_callable() {
                                return Err(type_error(format!("the validator of the {name} format is not callable")));
                            }
                            let callback = Callback(f.shallow_clone());
                            compile.formats.insert(
                                name,
                                Arc::new(move |s: &str| callback.call(s).is_some_and(|v| v.coerce_to_bool())),
                            );
                            php_formats = true;
                        }
                    }
                    "resolver" => {
                        if type_of(value) == IS_NULL {
                            continue;
                        }
                        if !value.is_callable() {
                            return Err(type_error("the resolver option must be callable"));
                        }
                        let callback = Callback(value.shallow_clone());
                        let failure = resolver_error.clone();
                        compile.resolve_document = Some(Arc::new(move |uri: &str| {
                            let fail = |message: String| {
                                failure.lock().unwrap().get_or_insert(message);
                                None
                            };
                            let v = callback.call(uri)?;
                            let v = deref(&v);
                            match type_of(v) {
                                IS_NULL => None,
                                IS_STRING => match str_of(v).map(serde_json::from_str::<Json>) {
                                    Some(Ok(json)) => Some(json),
                                    Some(Err(e)) => fail(format!("the resolver returned invalid JSON for {uri}: {e}")),
                                    None => fail(format!("the resolver returned invalid UTF-8 for {uri}")),
                                },
                                _ => to_json(v, 0)
                                    .map_or_else(|e| fail(format!("the resolver failed for {uri}: {e:?}")), Some),
                            }
                        }));
                    }
                    other => return Err(value_error(format!("{other} is not an option"))),
                }
            }
        }
        let compiled = corvus::compile_with(&schema, &compile);
        callbacks_done()?;
        match compiled {
            Ok(inner) => Ok(Validator { inner, php_formats }),
            Err(e) => {
                let message = match resolver_error.lock().unwrap().take() {
                    Some(r) => format!("{} ({r})", e.message()),
                    None => e.message().to_owned(),
                };
                Err(PhpException::from_class::<CompilationException>(message))
            }
        }
    }

    /// Whether the value is valid, evaluating only as far as the answer needs.
    pub fn is_valid(&self, value: &Zval) -> PhpResult<bool> {
        if !self.php_formats {
            let ctx = Context::new();
            let result = self.inner.validate_instance(PhpInstance::root(value, &ctx));
            if !ctx.fallback.get() {
                return result.map_err(depth_error);
            }
        }
        let json = to_json(value, 0)?;
        let result = self.inner.validate(&json);
        callbacks_done()?;
        result.map_err(depth_error)
    }

    /// Whether the JSON text is valid: it is parsed by the crate, without creating PHP values for it.
    pub fn is_valid_json(&self, json: &Zval) -> PhpResult<bool> {
        let json = deref(json);
        if type_of(json) != IS_STRING {
            return Err(type_error("the JSON text must be a string"));
        }
        let text = str_of(json).ok_or_else(|| {
            PhpException::from_class::<InvalidJsonException>("the JSON text is not valid UTF-8".into())
        })?;
        let result = self.inner.validate_json(text);
        callbacks_done()?;
        match result {
            Ok(valid) => Ok(valid),
            Err(corvus::JsonValidationError::InvalidJson(e)) => {
                Err(PhpException::from_class::<InvalidJsonException>(e.to_string()))
            }
            Err(corvus::JsonValidationError::DepthExceeded(e)) => Err(depth_error(e)),
        }
    }

    /// Evaluates the value into the collector, replacing its results (every keyword is evaluated and reported at the
    /// collector's level). Returns whether the value is valid.
    pub fn evaluate(&self, value: &Zval, collector: &Collector) -> PhpResult<bool> {
        let mut c = JsonSchemaResultsCollector::new(collector.level);
        if !self.php_formats {
            let ctx = Context::new();
            let result = self.inner.evaluate_instance(PhpInstance::root(value, &ctx), &mut c);
            if !ctx.fallback.get() {
                *collector.inner.borrow_mut() = c;
                return result.map_err(depth_error);
            }
            c = JsonSchemaResultsCollector::new(collector.level);
        }
        let json = to_json(value, 0)?;
        let result = self.inner.evaluate(&json, &mut c);
        callbacks_done()?;
        *collector.inner.borrow_mut() = c;
        result.map_err(depth_error)
    }
}

// ---------------------------------------------------------------------------------------------------------------------
// Collector

/// The results of the latest evaluation into it.
#[php_class]
#[php(name = "Corvus\\JsonSchema\\Collector")]
#[php(flags = ext_php_rs::flags::ClassFlags::Final)]
pub struct Collector {
    level: Level,
    inner: RefCell<JsonSchemaResultsCollector>,
}

#[php_impl]
impl Collector {
    pub fn __construct(level: Option<ResultsLevel>) -> Collector {
        let level = match level {
            None | Some(ResultsLevel::Basic) => Level::Basic,
            Some(ResultsLevel::Detailed) => Level::Detailed,
            Some(ResultsLevel::Verbose) => Level::Verbose,
        };
        Collector { level, inner: RefCell::new(JsonSchemaResultsCollector::new(level)) }
    }

    /// The rows, as arrays: `isMatch`, `message`, `evaluationLocation`, `schemaLocation`, `instanceLocation`.
    pub fn results(&self) -> PhpResult<ZBox<ZendHashTable>> {
        let c = self.inner.borrow();
        let mut out = ZendHashTable::with_capacity(c.results().len() as u32);
        for r in c.results() {
            let mut row = ZendHashTable::with_capacity(5);
            row.insert("isMatch", r.is_match)?;
            row.insert("message", r.message.as_str())?;
            row.insert("evaluationLocation", r.evaluation_location.as_str())?;
            row.insert("schemaLocation", r.schema_evaluation_location.as_str())?;
            row.insert("instanceLocation", r.document_evaluation_location.as_str())?;
            out.push(row)?;
        }
        Ok(out)
    }

    /// The annotations of a verbose evaluation, grouped by instance location, keyword and schema location.
    pub fn annotations(&self) -> PhpResult<Zval> {
        let grouped = corvus::collect_annotations(&self.inner.borrow());
        let json = serde_json::to_value(grouped).map_err(|e| PhpException::from_message(e.to_string()))?;
        to_php(&json)
    }

    /// Clears the results.
    pub fn clear(&self) {
        *self.inner.borrow_mut() = JsonSchemaResultsCollector::new(self.level);
    }
}

/// The version of the corvus-json-schema crate the extension was built from.
#[php_function]
#[php(name = "Corvus\\JsonSchema\\crate_version")]
pub fn crate_version() -> &'static str {
    corvus::VERSION
}

#[php_module]
pub fn get_module(module: ModuleBuilder) -> ModuleBuilder {
    // The extension's name (as `extension_loaded`, `phpversion` and PIE know it) and version, rather than the crate's.
    module
        .name("corvus_json_schema")
        .version(env!("CARGO_PKG_VERSION"))
        .class::<JsonSchemaException>()
        .class::<CompilationException>()
        .class::<DepthException>()
        .class::<InvalidJsonException>()
        .enumeration::<Dialect>()
        .enumeration::<ResultsLevel>()
        .class::<Validator>()
        .class::<Collector>()
        .function(wrap_function!(crate_version))
}
