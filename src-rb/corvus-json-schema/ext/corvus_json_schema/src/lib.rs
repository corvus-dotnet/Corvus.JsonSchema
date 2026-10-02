//! The native extension of the `corvus_json_schema` gem: the corvus-json-schema crate, with Ruby values read in place
//! (see src-rs/BINDINGS.md). The Ruby layer (lib/corvus_json_schema.rb) gives the public API.

use std::cell::{Cell, RefCell};

use corvus::{
    ArrayView, CompileOptions, Dialect, Instance, JsonSchemaResultsCollector, Kind, ObjectView, ResultsLevel, View,
};
use magnus::prelude::*;
use magnus::rb_sys::AsRawValue;
use magnus::{
    Error, ExceptionClass, Float, Integer, RArray, RHash, RModule, RString, Ruby, Symbol, Value, function, method,
    r_hash::ForEach, value::BoxValue,
};
use serde_json::{Map, Number, Value as Json};

/// Arrays and objects nested deeper than this are converted (with a depth check) rather than read in place.
const MAX_NESTING: u32 = 256;

// ---------------------------------------------------------------------------------------------------------------------
// Errors

thread_local! {
    /// The first exception a Ruby callback raised during the current call (format validators run inside the
    /// evaluator, which cannot carry a Ruby exception out).
    static CALLBACK_ERROR: RefCell<Option<Error>> = const { RefCell::new(None) };
}

fn take_callback_error() -> Result<(), Error> {
    CALLBACK_ERROR.with(|e| e.borrow_mut().take()).map_or(Ok(()), Err)
}

fn error_class(ruby: &Ruby, name: &str) -> ExceptionClass {
    let module: RModule = ruby.class_object().const_get("CorvusJsonSchema").expect("the CorvusJsonSchema module");
    module.const_get(name).expect("an error class")
}

fn raise(ruby: &Ruby, class: &str, message: impl Into<String>) -> Error {
    Error::new(error_class(ruby, class), message.into())
}

// ---------------------------------------------------------------------------------------------------------------------
// Ruby values read in place

/// The state of one evaluation: whether a value could not be read in place (so the caller converts the instance and
/// evaluates that instead), and the pairs of the hashes read, gathered when the evaluator first looks into one (Ruby
/// has no public lazy hash iterator).
struct Context {
    fallback: Cell<bool>,
    gathered: RefCell<Vec<Pairs>>,
}

/// A hash's pairs: the keys' text and the values.
type Pairs = Box<[(&'static str, Value)]>;

impl Context {
    fn new() -> Context {
        Context { fallback: Cell::new(false), gathered: RefCell::new(Vec::new()) }
    }
}

/// A Ruby value read as a JSON value: `Hash` (String or Symbol keys), `Array`, `String` (UTF-8), `Symbol` (as its
/// name), `Integer`, `Float`, `true`, `false` and `nil`. Anything else, or nesting deeper than `MAX_NESTING`, sets the
/// context's fallback.
///
/// The values stay reachable from the instance the caller passed (on the stack) for the whole evaluation, and no Ruby
/// code runs during it (with Ruby format validators the caller converts the instance first), so no garbage collection
/// can free or move them.
#[derive(Clone, Copy)]
struct RbInstance<'a> {
    v: Value,
    ctx: &'a Context,
    depth: u32,
}

impl<'a> RbInstance<'a> {
    fn root(v: Value, ctx: &'a Context) -> Self {
        RbInstance { v, ctx, depth: 0 }
    }

    #[inline(always)]
    fn child(self, v: Value) -> Self {
        RbInstance { v, ctx: self.ctx, depth: self.depth + 1 }
    }

    #[cold]
    fn unsupported(self) -> View<'a, Self> {
        self.ctx.fallback.set(true);
        View::Null
    }

    /// A hash's pairs, gathered once into the context (which outlives `'a`).
    fn gather(self, h: RHash) -> Option<&'a [(&'a str, Value)]> {
        let mut pairs: Vec<(&'static str, Value)> = Vec::with_capacity(h.len());
        let mut ok = true;
        let result = h.foreach(|k: Value, v: Value| match key_str(k) {
            Some(k) => {
                pairs.push((k, v));
                Ok(ForEach::Continue)
            }
            None => {
                ok = false;
                Ok(ForEach::Stop)
            }
        });
        if result.is_err() || !ok {
            return None;
        }
        let boxed: Pairs = pairs.into_boxed_slice();
        let slice: *const [(&'static str, Value)] = &*boxed;
        self.ctx.gathered.borrow_mut().push(boxed);
        // SAFETY: the box lives in the context, which outlives 'a, and is never removed or changed while the context
        // lives (a box's contents do not move when the vector holding it grows).
        Some(unsafe { &*slice })
    }
}

/// A string's text when it is valid UTF-8. The borrow lasts while the string is neither changed nor moved (see
/// `RbInstance`).
fn str_of<'a>(s: RString) -> Option<&'a str> {
    // SAFETY: as above.
    let bytes: &'a [u8] = unsafe { std::mem::transmute::<&[u8], &'a [u8]>(s.as_slice()) };
    std::str::from_utf8(bytes).ok()
}

fn key_str<'a>(k: Value) -> Option<&'a str> {
    if let Some(s) = RString::from_value(k) {
        return str_of(s);
    }
    Symbol::from_value(k).and_then(|s| s.to_r_string().ok()).and_then(str_of)
}

impl<'a> Instance<'a> for RbInstance<'a> {
    type Array = RbArray<'a>;
    type Object = RbObject<'a>;

    fn view(self) -> View<'a, Self> {
        let v = self.v;
        if v.is_nil() {
            return View::Null;
        }
        if let Some(s) = RString::from_value(v) {
            return str_of(s).map_or_else(|| self.unsupported(), View::String);
        }
        if let Some(h) = RHash::from_value(v) {
            if self.depth >= MAX_NESTING {
                return self.unsupported();
            }
            return match self.gather(h) {
                Some(pairs) => View::Object(RbObject { pairs, parent: self }),
                None => self.unsupported(),
            };
        }
        if let Some(a) = RArray::from_value(v) {
            if self.depth >= MAX_NESTING {
                return self.unsupported();
            }
            // SAFETY: the array is neither changed nor moved during the evaluation (see `RbInstance`).
            let items: &'a [Value] = unsafe { std::mem::transmute::<&[Value], &'a [Value]>(a.as_slice()) };
            return View::Array(RbArray { items, parent: self });
        }
        let ruby = Ruby::get_with(v);
        if v.as_raw() == ruby.qtrue().as_raw() {
            return View::Bool(true);
        }
        if v.as_raw() == ruby.qfalse().as_raw() {
            return View::Bool(false);
        }
        if let Some(i) = Integer::from_value(v) {
            if let Ok(n) = i.to_i64() {
                return View::Number(n.into());
            }
            if let Ok(n) = i.to_u64() {
                return View::Number(n.into());
            }
            // Beyond 64 bits, the nearest double (as for a JSON parser without arbitrary precision).
            return Float::from_value(i.as_value()).map_or_else(
                || integer_to_f64(i).and_then(Number::from_f64).map_or_else(|| self.unsupported(), View::Number),
                |f| Number::from_f64(f.to_f64()).map_or_else(|| self.unsupported(), View::Number),
            );
        }
        if let Some(f) = Float::from_value(v) {
            return Number::from_f64(f.to_f64()).map_or_else(|| self.unsupported(), View::Number);
        }
        if let Some(s) = Symbol::from_value(v) {
            return s.to_r_string().ok().and_then(str_of).map_or_else(|| self.unsupported(), View::String);
        }
        self.unsupported()
    }

    fn kind(self) -> Kind {
        let v = self.v;
        if v.is_nil() {
            return Kind::Null;
        }
        match self.view() {
            View::Null => Kind::Null,
            View::Bool(_) => Kind::Bool,
            View::Number(_) => Kind::Number,
            View::String(_) => Kind::String,
            View::Array(_) => Kind::Array,
            View::Object(_) => Kind::Object,
        }
    }
}

/// An integer beyond 64 bits as the nearest double.
fn integer_to_f64(i: Integer) -> Option<f64> {
    // SAFETY: rb_big2dbl does not raise (it warns and returns an infinity beyond a double's range).
    let f = unsafe { rb_sys::rb_big2dbl(i.as_raw()) };
    f.is_finite().then_some(f)
}

#[derive(Clone, Copy)]
struct RbArray<'a> {
    items: &'a [Value],
    parent: RbInstance<'a>,
}

impl<'a> ArrayView<'a> for RbArray<'a> {
    type Item = RbInstance<'a>;

    fn len(self) -> usize {
        self.items.len()
    }

    fn get(self, index: usize) -> RbInstance<'a> {
        self.parent.child(self.items[index])
    }

    fn iter(self) -> impl Iterator<Item = RbInstance<'a>> {
        let parent = self.parent;
        self.items.iter().map(move |&v| parent.child(v))
    }
}

#[derive(Clone, Copy)]
struct RbObject<'a> {
    pairs: &'a [(&'a str, Value)],
    parent: RbInstance<'a>,
}

impl<'a> ObjectView<'a> for RbObject<'a> {
    type Item = RbInstance<'a>;

    fn len(self) -> usize {
        self.pairs.len()
    }

    fn get(self, name: &str) -> Option<RbInstance<'a>> {
        self.pairs.iter().find(|(k, _)| *k == name).map(|&(_, v)| self.parent.child(v))
    }

    fn iter(self) -> impl Iterator<Item = (&'a str, RbInstance<'a>)> {
        let parent = self.parent;
        self.pairs.iter().map(move |&(k, v)| (k, parent.child(v)))
    }
}

// ---------------------------------------------------------------------------------------------------------------------
// Conversions

/// A Ruby value as JSON (for schemas, and for instances that cannot be read in place).
fn to_json(ruby: &Ruby, v: Value, depth: u32) -> Result<Json, Error> {
    if depth > 1024 {
        return Err(Error::new(ruby.exception_arg_error(), "the value nests too deeply"));
    }
    if v.is_nil() {
        return Ok(Json::Null);
    }
    if v.as_raw() == ruby.qtrue().as_raw() {
        return Ok(Json::Bool(true));
    }
    if v.as_raw() == ruby.qfalse().as_raw() {
        return Ok(Json::Bool(false));
    }
    if let Some(s) = RString::from_value(v) {
        return Ok(Json::String(s.to_string()?));
    }
    if let Some(s) = Symbol::from_value(v) {
        return Ok(Json::String(s.name()?.into_owned()));
    }
    if let Some(i) = Integer::from_value(v) {
        if let Ok(n) = i.to_i64() {
            return Ok(Json::Number(n.into()));
        }
        if let Ok(n) = i.to_u64() {
            return Ok(Json::Number(n.into()));
        }
        return integer_to_f64(i)
            .and_then(Number::from_f64)
            .map(Json::Number)
            .ok_or_else(|| Error::new(ruby.exception_range_error(), "an integer beyond the range of a double"));
    }
    if let Some(f) = Float::from_value(v) {
        return Number::from_f64(f.to_f64())
            .map(Json::Number)
            .ok_or_else(|| Error::new(ruby.exception_arg_error(), "NaN and infinities are not JSON numbers"));
    }
    if let Some(a) = RArray::from_value(v) {
        let mut out = Vec::with_capacity(a.len());
        for item in a.into_iter() {
            out.push(to_json(ruby, item, depth + 1)?);
        }
        return Ok(Json::Array(out));
    }
    if let Some(h) = RHash::from_value(v) {
        let mut out = Map::with_capacity(h.len());
        let mut failure: Option<Error> = None;
        h.foreach(|k: Value, item: Value| {
            let key = if let Some(s) = RString::from_value(k) {
                s.to_string()
            } else if let Some(s) = Symbol::from_value(k) {
                s.name().map(|n| n.into_owned())
            } else {
                Err(Error::new(ruby.exception_type_error(), "object keys must be Strings or Symbols"))
            };
            match key.and_then(|k| Ok((k, to_json(ruby, item, depth + 1)?))) {
                Ok((k, j)) => {
                    out.insert(k, j);
                    Ok(ForEach::Continue)
                }
                Err(e) => {
                    failure = Some(e);
                    Ok(ForEach::Stop)
                }
            }
        })?;
        return match failure {
            Some(e) => Err(e),
            None => Ok(Json::Object(out)),
        };
    }
    Err(Error::new(ruby.exception_type_error(), format!("a {} is not a JSON value", unsafe { v.classname() })))
}

/// JSON as a Ruby value (for results and annotations): objects as Hashes with String keys.
fn to_ruby(ruby: &Ruby, j: &Json) -> Result<Value, Error> {
    Ok(match j {
        Json::Null => ruby.qnil().as_value(),
        Json::Bool(b) => {
            if *b {
                ruby.qtrue().as_value()
            } else {
                ruby.qfalse().as_value()
            }
        }
        Json::Number(n) => {
            if let Some(i) = n.as_i64() {
                ruby.integer_from_i64(i).as_value()
            } else if let Some(u) = n.as_u64() {
                ruby.integer_from_u64(u).as_value()
            } else {
                ruby.float_from_f64(n.as_f64().unwrap_or(f64::NAN)).as_value()
            }
        }
        Json::String(s) => ruby.str_new(s).as_value(),
        Json::Array(a) => {
            let out = ruby.ary_new_capa(a.len());
            for item in a {
                out.push(to_ruby(ruby, item)?)?;
            }
            out.as_value()
        }
        Json::Object(o) => {
            let out = ruby.hash_new();
            for (k, v) in o {
                out.aset(ruby.str_new(k), to_ruby(ruby, v)?)?;
            }
            out.as_value()
        }
    })
}

// ---------------------------------------------------------------------------------------------------------------------
// Callbacks

/// A Ruby Proc kept alive for the validator (a boxed value is marked by the garbage collector).
struct Callback(BoxValue<Value>);

// SAFETY: callbacks run only on the Ruby thread that evaluates (validators hold the GVL while they evaluate); the crate
// requires Send + Sync of the closures that hold them.
unsafe impl Send for Callback {}
unsafe impl Sync for Callback {}

impl Callback {
    fn call(&self, arg: &str) -> Result<Value, Error> {
        // Callbacks run on the Ruby thread evaluating (see above).
        let ruby = Ruby::get().expect("a Ruby thread");
        self.0.funcall("call", (ruby.str_new(arg),))
    }
}

// ---------------------------------------------------------------------------------------------------------------------
// Validator

#[magnus::wrap(class = "CorvusJsonSchema::Validator", free_immediately, size)]
struct Validator {
    inner: corvus::Validator,
    /// Format validators written in Ruby run Ruby code during an evaluation, so instances are converted first.
    ruby_formats: bool,
}

fn depth_error(ruby: &Ruby, e: corvus::SchemaEvaluationDepthError) -> Error {
    raise(ruby, "DepthError", e.to_string())
}

impl Validator {
    /// Evaluates a Ruby value: read in place, or converted when it holds something the reader does not (or when Ruby
    /// format validators could run Ruby code during the evaluation).
    fn check(&self, ruby: &Ruby, value: Value) -> Result<bool, Error> {
        if !self.ruby_formats {
            let ctx = Context::new();
            let result = self.inner.validate_instance(RbInstance::root(value, &ctx));
            if !ctx.fallback.get() {
                return result.map_err(|e| depth_error(ruby, e));
            }
        }
        let json = to_json(ruby, value, 0)?;
        let result = self.inner.validate(&json);
        take_callback_error()?;
        result.map_err(|e| depth_error(ruby, e))
    }

    fn valid(ruby: &Ruby, rb_self: &Self, value: Value) -> Result<bool, Error> {
        rb_self.check(ruby, value)
    }

    fn valid_json(ruby: &Ruby, rb_self: &Self, text: RString) -> Result<bool, Error> {
        // With Ruby format validators the text is copied first: Ruby code run during the evaluation could move it.
        let owned;
        let json: &str = if rb_self.ruby_formats {
            owned = text.to_string()?;
            &owned
        } else {
            str_of(text).ok_or_else(|| raise(ruby, "InvalidJsonError", "the JSON text is not valid UTF-8"))?
        };
        let result = rb_self.inner.validate_json(json);
        take_callback_error()?;
        match result {
            Ok(valid) => Ok(valid),
            Err(corvus::JsonValidationError::InvalidJson(e)) => Err(raise(ruby, "InvalidJsonError", e.to_string())),
            Err(corvus::JsonValidationError::DepthExceeded(e)) => Err(depth_error(ruby, e)),
        }
    }

    /// Evaluates into the collector (replacing its results), reporting every keyword at its level.
    fn evaluate(ruby: &Ruby, rb_self: &Self, value: Value, collector: &Collector) -> Result<bool, Error> {
        let mut c = JsonSchemaResultsCollector::new(collector.level);
        if !rb_self.ruby_formats {
            let ctx = Context::new();
            let result = rb_self.inner.evaluate_instance(RbInstance::root(value, &ctx), &mut c);
            if !ctx.fallback.get() {
                *collector.inner.borrow_mut() = c;
                return result.map_err(|e| depth_error(ruby, e));
            }
            c = JsonSchemaResultsCollector::new(collector.level);
        }
        let json = to_json(ruby, value, 0)?;
        let result = rb_self.inner.evaluate(&json, &mut c);
        take_callback_error()?;
        *collector.inner.borrow_mut() = c;
        result.map_err(|e| depth_error(ruby, e))
    }
}

/// Compiles a schema (a Ruby value, or JSON text) with the options the Ruby layer passes.
#[allow(clippy::too_many_arguments)]
fn compile(
    ruby: &Ruby,
    schema: Value,
    dialect: Option<i64>,
    assert_format: Option<bool>,
    assert_format_in_legacy_drafts: bool,
    assert_content: bool,
    formats: Option<RHash>,
    resolver: Option<Value>,
    base_uri: Option<String>,
    entry_point: Option<String>,
    max_depth: u32,
) -> Result<Validator, Error> {
    let schema: Json = match RString::from_value(schema) {
        Some(text) => serde_json::from_str(&text.to_string()?)
            .map_err(|e| raise(ruby, "InvalidJsonError", format!("the schema is not valid JSON: {e}")))?,
        None => to_json(ruby, schema, 0)?,
    };
    let mut options = CompileOptions {
        default_dialect: match dialect {
            None | Some(2020) => Dialect::Draft202012,
            Some(4) => Dialect::Draft4,
            Some(6) => Dialect::Draft6,
            Some(7) => Dialect::Draft7,
            Some(2019) => Dialect::Draft201909,
            Some(d) => return Err(Error::new(ruby.exception_arg_error(), format!("{d} is not a dialect"))),
        },
        assert_format,
        assert_format_in_legacy_drafts,
        assert_content,
        base_uri,
        entry_point,
        max_depth,
        ..CompileOptions::default()
    };
    let mut ruby_formats = false;
    if let Some(formats) = formats {
        formats.foreach(|name: Value, f: Value| {
            let name = match Symbol::from_value(name) {
                Some(s) => s.name()?.into_owned(),
                None => RString::try_convert(name)?.to_string()?,
            };
            let callback = Callback(BoxValue::new(f));
            options.formats.insert(
                name,
                std::sync::Arc::new(move |s: &str| match callback.call(s) {
                    Ok(v) => v.to_bool(),
                    Err(e) => {
                        CALLBACK_ERROR.with(|c| {
                            c.borrow_mut().get_or_insert(e);
                        });
                        false
                    }
                }),
            );
            ruby_formats = true;
            Ok(ForEach::Continue)
        })?;
    }
    // The first failure of the resolver (a Ruby exception, or a value that is not JSON), as text: the crate's resolver
    // can only say a document is unknown.
    let resolver_error: std::sync::Arc<std::sync::Mutex<Option<String>>> = Default::default();
    if let Some(r) = resolver.filter(|r| !r.is_nil()) {
        let callback = Callback(BoxValue::new(r));
        let failure = resolver_error.clone();
        options.resolve_document = Some(std::sync::Arc::new(move |uri: &str| {
            let fail = |message: String| {
                failure.lock().unwrap().get_or_insert(message);
                None
            };
            let ruby = Ruby::get().expect("a Ruby thread");
            match callback.call(uri) {
                Ok(v) if v.is_nil() => None,
                Ok(v) => match RString::from_value(v) {
                    Some(text) => match text.to_string().map(|t| serde_json::from_str::<Json>(&t)) {
                        Ok(Ok(json)) => Some(json),
                        Ok(Err(e)) => fail(format!("the resolver returned invalid JSON for {uri}: {e}")),
                        Err(e) => fail(format!("the resolver failed for {uri}: {e}")),
                    },
                    None => {
                        to_json(&ruby, v, 0).map_or_else(|e| fail(format!("the resolver failed for {uri}: {e}")), Some)
                    }
                },
                Err(e) => fail(format!("the resolver failed for {uri}: {e}")),
            }
        }));
    }
    match corvus::compile_with(&schema, &options) {
        Ok(inner) => Ok(Validator { inner, ruby_formats }),
        Err(e) => {
            let message = match resolver_error.lock().unwrap().take() {
                Some(r) => format!("{} ({r})", e.message()),
                None => e.message().to_owned(),
            };
            Err(raise(ruby, "CompilationError", message))
        }
    }
}

// ---------------------------------------------------------------------------------------------------------------------
// Collector

#[magnus::wrap(class = "CorvusJsonSchema::Collector", free_immediately, size)]
struct Collector {
    level: ResultsLevel,
    inner: RefCell<JsonSchemaResultsCollector>,
}

impl Collector {
    fn new(ruby: &Ruby, level: i64) -> Result<Collector, Error> {
        let level = match level {
            0 => ResultsLevel::Basic,
            1 => ResultsLevel::Detailed,
            2 => ResultsLevel::Verbose,
            l => return Err(Error::new(ruby.exception_arg_error(), format!("{l} is not a results level"))),
        };
        Ok(Collector { level, inner: RefCell::new(JsonSchemaResultsCollector::new(level)) })
    }

    /// The rows as Hashes: `is_match`, `message`, `evaluation_location`, `schema_location`, `instance_location`.
    fn results(ruby: &Ruby, rb_self: &Self) -> Result<RArray, Error> {
        let c = rb_self.inner.borrow();
        let out = ruby.ary_new_capa(c.results().len());
        for r in c.results() {
            let row = ruby.hash_new();
            row.aset(ruby.to_symbol("is_match"), r.is_match)?;
            row.aset(ruby.to_symbol("message"), ruby.str_new(&r.message))?;
            row.aset(ruby.to_symbol("evaluation_location"), ruby.str_new(&r.evaluation_location))?;
            row.aset(ruby.to_symbol("schema_location"), ruby.str_new(&r.schema_evaluation_location))?;
            row.aset(ruby.to_symbol("instance_location"), ruby.str_new(&r.document_evaluation_location))?;
            out.push(row)?;
        }
        Ok(out)
    }

    /// The annotations of a verbose evaluation, grouped by instance location, keyword and schema location.
    fn annotations(ruby: &Ruby, rb_self: &Self) -> Result<Value, Error> {
        let grouped = corvus::collect_annotations(&rb_self.inner.borrow());
        let json =
            serde_json::to_value(grouped).map_err(|e| Error::new(ruby.exception_runtime_error(), e.to_string()))?;
        to_ruby(ruby, &json)
    }

    fn clear(&self) {
        *self.inner.borrow_mut() = JsonSchemaResultsCollector::new(self.level);
    }
}

// ---------------------------------------------------------------------------------------------------------------------
// Module

#[magnus::init]
fn init(ruby: &Ruby) -> Result<(), Error> {
    let module = ruby.define_module("CorvusJsonSchema")?;
    let error = module.define_error("Error", ruby.exception_standard_error())?;
    module.define_error("CompilationError", error)?;
    module.define_error("DepthError", error)?;
    module.define_error("InvalidJsonError", error)?;

    let native = module.define_module("Native")?;
    native.define_singleton_method("compile", function!(compile, 10))?;
    native.define_singleton_method("crate_version", function!(|| corvus::VERSION, 0))?;

    let validator = module.define_class("Validator", ruby.class_object())?;
    validator.define_method("valid?", method!(Validator::valid, 1))?;
    validator.define_method("valid_json?", method!(Validator::valid_json, 1))?;
    validator.define_method("native_evaluate", method!(Validator::evaluate, 2))?;

    let collector = module.define_class("Collector", ruby.class_object())?;
    collector.define_singleton_method("native_new", function!(Collector::new, 1))?;
    collector.define_method("results", method!(Collector::results, 0))?;
    collector.define_method("annotations", method!(Collector::annotations, 0))?;
    collector.define_method("clear", method!(Collector::clear, 0))?;
    Ok(())
}
