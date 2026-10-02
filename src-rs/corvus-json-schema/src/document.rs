//! A parsed JSON document that the evaluator reads in place: JSON text parsed once into one flat array of nodes, with
//! strings borrowed from the text where they have no escapes. Parsing allocates a few buffers per document, not one
//! per value as `serde_json::Value` does, so it is several times faster to build.
//!
//! ```
//! let validator = corvus_json_schema::compile(&serde_json::json!({ "type": "array", "items": { "type": "integer" } }))
//!     .unwrap();
//! let document = corvus_json_schema::JsonDocument::parse("[1, 2, 3]").unwrap();
//! assert!(validator.validate_instance(document.root()).unwrap());
//! ```

use std::cell::Cell;
use std::fmt;

use serde_json::{Map, Number, Value};

use crate::instance::{ArrayView, Instance, Kind, ObjectView, View, str_eq};

/// The deepest nesting of arrays and objects accepted: serde_json's default recursion limit admits 127 levels.
const MAX_DEPTH: usize = 127;

/// A value: its kind, flags (the number representation, or where a string's bytes are), a count (array items, object
/// properties) or byte length (strings), and its data (a number's bits, a string's offset, the index of a
/// container's first child). The children of an array or object are consecutive; an object's are key and value
/// pairs.
#[derive(Clone, Copy)]
struct Node {
    kind: Kind,
    flags: u8,
    len: u32,
    data: u64,
}

const NUM_U64: u8 = 0;
const NUM_I64: u8 = 1;
const NUM_F64: u8 = 2;

/// The string's bytes are in the source text.
const STR_SOURCE: u8 = 0;
/// The string's bytes are in the document's buffer of unescaped strings.
const STR_TEXT: u8 = 1;

impl Node {
    #[inline(always)]
    const fn new(kind: Kind, flags: u8, len: u32, data: u64) -> Node {
        Node { kind, flags, len, data }
    }
}

/// JSON text parsed for evaluation. It borrows the text, for the strings that need no unescaping.
pub struct JsonDocument<'s> {
    source: &'s str,
    nodes: Box<[Node]>,
    root: Node,
    /// The unescaped strings.
    text: Box<str>,
}

/// The text is not valid JSON, or nests arrays and objects too deeply.
#[derive(Clone, Debug, PartialEq, Eq)]
pub struct JsonParseError {
    offset: usize,
    message: &'static str,
}

impl JsonParseError {
    /// The byte offset in the text at which the error was found.
    pub fn offset(&self) -> usize {
        self.offset
    }

    /// What is wrong.
    pub fn message(&self) -> &str {
        self.message
    }
}

impl fmt::Display for JsonParseError {
    fn fmt(&self, f: &mut fmt::Formatter<'_>) -> fmt::Result {
        write!(f, "{} at byte {}", self.message, self.offset)
    }
}

impl std::error::Error for JsonParseError {}

impl<'s> JsonDocument<'s> {
    /// Parses JSON text. As serde_json does, it rejects lone surrogates in `\u` escapes, numbers out of the range of a
    /// double, nesting deeper than 128 and anything but whitespace after the value; of duplicate property names, the
    /// last value is kept, at the position of the first.
    pub fn parse(source: &'s str) -> Result<JsonDocument<'s>, JsonParseError> {
        let mut parser = Parser::new(source, BUFFERS.take().unwrap_or_default());
        let root = parser.parse();
        // One allocation for the document's nodes, exactly sized.
        let document = root.map(|root| JsonDocument {
            source,
            nodes: parser.nodes.as_slice().into(),
            root,
            text: parser.text.as_str().into(),
        });
        let Parser { mut nodes, mut scratch, mut frames, mut text, .. } = parser;
        if nodes.capacity() <= MAX_KEPT_NODES && text.capacity() <= MAX_KEPT_TEXT {
            nodes.clear();
            scratch.clear();
            frames.clear();
            text.clear();
            BUFFERS.set(Some(Buffers { nodes, scratch, frames, text }));
        }
        document
    }

    /// The root value.
    #[inline]
    pub fn root(&self) -> JsonDocumentValue<'_> {
        JsonDocumentValue { doc: self.erase(), node: &self.root }
    }

    /// The document as a `serde_json::Value`.
    pub fn to_value(&self) -> Value {
        self.root().to_value()
    }

    /// The document with its source lifetime shortened to the borrow (`JsonDocument` is covariant in it).
    #[inline(always)]
    fn erase<'d>(&'d self) -> &'d JsonDocument<'d> {
        self
    }

    #[inline(always)]
    fn str_of(&self, n: &Node) -> &str {
        let (offset, len) = (n.data as usize, n.len as usize);
        let buffer = if n.flags == STR_TEXT { &*self.text } else { self.source };
        debug_assert!(buffer.is_char_boundary(offset) && buffer.is_char_boundary(offset + len));
        // SAFETY: the parser records string nodes at character boundaries within their buffer.
        unsafe { buffer.get_unchecked(offset..offset + len) }
    }

    #[inline(always)]
    fn children(&self, n: &Node, count: usize) -> &[Node] {
        let start = n.data as usize;
        debug_assert!(start + count <= self.nodes.len());
        // SAFETY: the parser records a container's children as a range within `nodes`.
        unsafe { self.nodes.get_unchecked(start..start + count) }
    }

    /// An object's key and value nodes, as pairs (iterating pairs, not two-node chunks, needs no per-step checks).
    #[inline(always)]
    fn pairs(&self, n: &Node) -> &[[Node; 2]] {
        let items = self.children(n, 2 * n.len as usize);
        // SAFETY: `[Node; 2]` has the layout of two consecutive `Node`s, and `items` holds exactly `n.len` pairs.
        unsafe { std::slice::from_raw_parts(items.as_ptr().cast::<[Node; 2]>(), n.len as usize) }
    }
}

impl fmt::Debug for JsonDocument<'_> {
    fn fmt(&self, f: &mut fmt::Formatter<'_>) -> fmt::Result {
        f.debug_struct("JsonDocument").field("nodes", &self.nodes.len()).finish()
    }
}

/// A value in a [`JsonDocument`]: an [`Instance`] for [`crate::Validator::validate_instance`].
#[derive(Clone, Copy)]
pub struct JsonDocumentValue<'d> {
    doc: &'d JsonDocument<'d>,
    node: &'d Node,
}

/// The items of an array in a [`JsonDocument`].
#[derive(Clone, Copy)]
pub struct JsonDocumentArray<'d> {
    doc: &'d JsonDocument<'d>,
    items: &'d [Node],
}

/// The properties of an object in a [`JsonDocument`].
#[derive(Clone, Copy)]
pub struct JsonDocumentObject<'d> {
    doc: &'d JsonDocument<'d>,
    /// Key and value nodes.
    pairs: &'d [[Node; 2]],
}

impl<'d> JsonDocumentValue<'d> {
    #[inline(always)]
    fn number(n: &Node) -> Number {
        match n.flags {
            NUM_U64 => Number::from(n.data),
            NUM_I64 => Number::from(n.data as i64),
            // The parser only records finite doubles.
            _ => Number::from_f64(f64::from_bits(n.data)).unwrap_or_else(|| Number::from(0)),
        }
    }

    /// The value as a `serde_json::Value`.
    pub fn to_value(self) -> Value {
        match self.view() {
            View::Null => Value::Null,
            View::Bool(b) => Value::Bool(b),
            View::Number(n) => Value::Number(n),
            View::String(s) => Value::String(s.to_owned()),
            View::Array(a) => Value::Array(a.iter().map(JsonDocumentValue::to_value).collect()),
            View::Object(o) => {
                let mut map = Map::with_capacity(o.len());
                for (k, v) in o.iter() {
                    map.insert(k.to_owned(), v.to_value());
                }
                Value::Object(map)
            }
        }
    }
}

impl<'d> Instance<'d> for JsonDocumentValue<'d> {
    type Array = JsonDocumentArray<'d>;
    type Object = JsonDocumentObject<'d>;

    #[inline(always)]
    fn view(self) -> View<'d, Self> {
        let (doc, n) = (self.doc, self.node);
        match n.kind {
            Kind::Null => View::Null,
            Kind::Bool => View::Bool(n.data != 0),
            Kind::Number => View::Number(Self::number(n)),
            Kind::String => View::String(doc.str_of(n)),
            Kind::Array => View::Array(JsonDocumentArray { doc, items: doc.children(n, n.len as usize) }),
            Kind::Object => View::Object(JsonDocumentObject { doc, pairs: doc.pairs(n) }),
        }
    }

    #[inline(always)]
    fn kind(self) -> Kind {
        self.node.kind
    }
}

impl<'d> ArrayView<'d> for JsonDocumentArray<'d> {
    type Item = JsonDocumentValue<'d>;

    #[inline(always)]
    fn len(self) -> usize {
        self.items.len()
    }

    #[inline(always)]
    fn get(self, index: usize) -> JsonDocumentValue<'d> {
        JsonDocumentValue { doc: self.doc, node: &self.items[index] }
    }

    #[inline(always)]
    fn iter(self) -> impl Iterator<Item = JsonDocumentValue<'d>> {
        let doc = self.doc;
        self.items.iter().map(move |node| JsonDocumentValue { doc, node })
    }

    #[inline(always)]
    fn tail(self, start: usize) -> impl Iterator<Item = JsonDocumentValue<'d>> {
        let doc = self.doc;
        self.items.get(start..).unwrap_or_default().iter().map(move |node| JsonDocumentValue { doc, node })
    }
}

impl<'d> ObjectView<'d> for JsonDocumentObject<'d> {
    type Item = JsonDocumentValue<'d>;

    #[inline(always)]
    fn len(self) -> usize {
        self.pairs.len()
    }

    #[inline]
    fn get(self, name: &str) -> Option<JsonDocumentValue<'d>> {
        let doc = self.doc;
        self.pairs.iter().find(|[k, _]| str_eq(doc.str_of(k), name)).map(|[_, v]| JsonDocumentValue { doc, node: v })
    }

    #[inline]
    fn contains_key(self, name: &str) -> bool {
        let doc = self.doc;
        self.pairs.iter().any(|[k, _]| str_eq(doc.str_of(k), name))
    }

    #[inline(always)]
    fn iter(self) -> impl Iterator<Item = (&'d str, JsonDocumentValue<'d>)> {
        let doc = self.doc;
        self.pairs.iter().map(move |[k, v]| (doc.str_of(k), JsonDocumentValue { doc, node: v }))
    }

    #[inline(always)]
    fn values(self) -> impl Iterator<Item = JsonDocumentValue<'d>> {
        let doc = self.doc;
        self.pairs.iter().map(move |[_, v]| JsonDocumentValue { doc, node: v })
    }
}

/// The parser's working buffers, kept between parses on a thread so that a document costs one allocation (two when
/// strings need unescaping): an allocator's cost per call, which musl's makes high, otherwise dominates small
/// documents.
#[derive(Default)]
struct Buffers {
    nodes: Vec<Node>,
    scratch: Vec<Node>,
    frames: Vec<Frame>,
    text: String,
}

thread_local! {
    /// Taken for the length of a parse, so a parse that a parse somehow re-entered would start with new buffers.
    static BUFFERS: Cell<Option<Buffers>> = const { Cell::new(None) };
}

/// Buffers that grew beyond this many nodes (16 MiB) for a large document are released, not kept.
const MAX_KEPT_NODES: usize = 1 << 20;
/// Nor is an unescaped-string buffer beyond 16 MiB.
const MAX_KEPT_TEXT: usize = 1 << 24;

/// An open array or object: where its children start in the scratch stack.
#[derive(Clone, Copy)]
struct Frame {
    start: u32,
    object: bool,
}

struct Parser<'s> {
    source: &'s str,
    b: &'s [u8],
    i: usize,
    /// Finished children of closed containers, each container's consecutive.
    nodes: Vec<Node>,
    /// The values of the open containers, innermost last; a container's run moves to `nodes` when it closes.
    scratch: Vec<Node>,
    frames: Vec<Frame>,
    text: String,
}

/// Bytes equal to `n` in a word (exact at the lowest set bit, which is all the scanner uses).
#[inline(always)]
const fn eq_bytes(w: u64, n: u8) -> u64 {
    let x = w ^ (0x0101_0101_0101_0101 * n as u64);
    x.wrapping_sub(0x0101_0101_0101_0101) & !x & 0x8080_8080_8080_8080
}

/// Bytes below 0x20 in a word (exact at the lowest set bit).
#[inline(always)]
const fn control_bytes(w: u64) -> u64 {
    w.wrapping_sub(0x2020_2020_2020_2020) & !w & 0x8080_8080_8080_8080
}

impl<'s> Parser<'s> {
    fn new(source: &'s str, buffers: Buffers) -> Parser<'s> {
        Parser {
            source,
            b: source.as_bytes(),
            i: 0,
            nodes: buffers.nodes,
            scratch: buffers.scratch,
            frames: buffers.frames,
            text: buffers.text,
        }
    }

    #[cold]
    fn error<T>(&self, message: &'static str) -> Result<T, JsonParseError> {
        Err(JsonParseError { offset: self.i, message })
    }

    #[inline(always)]
    fn skip_ws(&mut self) {
        while let Some(&c) = self.b.get(self.i) {
            if !matches!(c, b' ' | b'\n' | b'\r' | b'\t') {
                break;
            }
            self.i += 1;
        }
    }

    #[inline(always)]
    fn peek(&self) -> Option<u8> {
        self.b.get(self.i).copied()
    }

    /// Parses the text into `nodes`, returning the root.
    fn parse(&mut self) -> Result<Node, JsonParseError> {
        if self.source.len() > u32::MAX as usize {
            return self.error("document too large");
        }
        self.skip_ws();
        'value: loop {
            // A value.
            match self.peek() {
                Some(b'{') => {
                    self.i += 1;
                    self.skip_ws();
                    if self.peek() == Some(b'}') {
                        self.i += 1;
                        self.check_depth()?;
                        self.scratch.push(Node::new(Kind::Object, 0, 0, 0));
                    } else {
                        self.open(true)?;
                        self.key()?;
                        continue 'value;
                    }
                }
                Some(b'[') => {
                    self.i += 1;
                    self.skip_ws();
                    if self.peek() == Some(b']') {
                        self.i += 1;
                        self.check_depth()?;
                        self.scratch.push(Node::new(Kind::Array, 0, 0, 0));
                    } else {
                        self.open(false)?;
                        continue 'value;
                    }
                }
                Some(b'"') => {
                    let n = self.string()?;
                    self.scratch.push(n);
                }
                Some(b't') => self.literal(b"true", Node::new(Kind::Bool, 0, 0, 1))?,
                Some(b'f') => self.literal(b"false", Node::new(Kind::Bool, 0, 0, 0))?,
                Some(b'n') => self.literal(b"null", Node::new(Kind::Null, 0, 0, 0))?,
                Some(b'-' | b'0'..=b'9') => {
                    let n = self.number()?;
                    self.scratch.push(n);
                }
                Some(_) => return self.error("expected a value"),
                None => return self.error("unexpected end of input"),
            }
            // After a value: separators and closing brackets, until the next value or the end.
            loop {
                let Some(&frame) = self.frames.last() else {
                    self.skip_ws();
                    if self.i != self.b.len() {
                        return self.error("trailing characters");
                    }
                    return Ok(self.scratch[0]);
                };
                self.skip_ws();
                match self.peek() {
                    Some(b',') => {
                        self.i += 1;
                        self.skip_ws();
                        if frame.object {
                            self.key()?;
                        }
                        continue 'value;
                    }
                    Some(b'}') if frame.object => {
                        self.i += 1;
                        self.close(frame);
                    }
                    Some(b']') if !frame.object => {
                        self.i += 1;
                        self.close(frame);
                    }
                    Some(_) => {
                        return self.error(if frame.object { "expected ',' or '}'" } else { "expected ',' or ']'" });
                    }
                    None => return self.error("unexpected end of input"),
                }
            }
        }
    }

    #[inline(always)]
    fn open(&mut self, object: bool) -> Result<(), JsonParseError> {
        self.check_depth()?;
        self.frames.push(Frame { start: self.scratch.len() as u32, object });
        Ok(())
    }

    /// An array or object may open at the current depth (an empty one counts, as in serde_json).
    #[inline(always)]
    fn check_depth(&self) -> Result<(), JsonParseError> {
        if self.frames.len() >= MAX_DEPTH { self.error("recursion limit exceeded") } else { Ok(()) }
    }

    /// A property name and its colon, leaving the parser at the value.
    #[inline(always)]
    fn key(&mut self) -> Result<(), JsonParseError> {
        if self.peek() != Some(b'"') {
            return self.error("expected a property name");
        }
        let k = self.string()?;
        self.scratch.push(k);
        self.skip_ws();
        if self.peek() != Some(b':') {
            return self.error("expected ':'");
        }
        self.i += 1;
        self.skip_ws();
        Ok(())
    }

    /// Moves the closed container's children to `nodes` and pushes the container in their place.
    #[inline(always)]
    fn close(&mut self, frame: Frame) {
        self.frames.pop();
        let start = frame.start as usize;
        if frame.object && self.scratch.len() - start > 2 {
            self.dedupe(start);
        }
        let first = self.nodes.len() as u64;
        let children = self.scratch.len() - start;
        self.nodes.extend_from_slice(&self.scratch[start..]);
        self.scratch.truncate(start);
        let (kind, count) = if frame.object { (Kind::Object, children / 2) } else { (Kind::Array, children) };
        self.scratch.push(Node::new(kind, 0, count as u32, first));
    }

    fn key_str(&self, n: &Node) -> &str {
        let (offset, len) = (n.data as usize, n.len as usize);
        if n.flags == STR_TEXT { &self.text[offset..offset + len] } else { &self.source[offset..offset + len] }
    }

    /// Of duplicate property names in the object whose pairs start at `start`, keeps the last value at the first
    /// position (as `serde_json::Map` with `preserve_order` does).
    fn dedupe(&mut self, start: usize) {
        let pairs = &self.scratch[start..];
        let count = pairs.len() / 2;
        let duplicate = if count <= 16 {
            (1..count).any(|j| (0..j).any(|i| str_eq(self.key_str(&pairs[2 * i]), self.key_str(&pairs[2 * j]))))
        } else {
            let mut seen = std::collections::HashSet::with_capacity(count);
            (0..count).any(|j| !seen.insert(self.key_str(&pairs[2 * j])))
        };
        if !duplicate {
            return;
        }
        let mut kept: Vec<Node> = Vec::with_capacity(pairs.len());
        for p in pairs.chunks_exact(2) {
            let name = self.key_str(&p[0]);
            match kept.chunks_exact(2).position(|q| self.key_str(&q[0]) == name) {
                Some(at) => kept[2 * at + 1] = p[1],
                None => kept.extend_from_slice(p),
            }
        }
        self.scratch.truncate(start);
        self.scratch.extend_from_slice(&kept);
    }

    #[inline(always)]
    fn literal(&mut self, word: &[u8], node: Node) -> Result<(), JsonParseError> {
        if self.b.get(self.i..self.i + word.len()) != Some(word) {
            return self.error("expected a value");
        }
        self.i += word.len();
        self.scratch.push(node);
        Ok(())
    }

    /// A string, from its opening quote.
    #[inline(always)]
    fn string(&mut self) -> Result<Node, JsonParseError> {
        let start = self.i + 1;
        let mut j = start;
        let b = self.b;
        // Eight bytes at a time to the first quote, backslash or control character.
        while j + 8 <= b.len() {
            let w = u64::from_le_bytes(b[j..j + 8].try_into().unwrap());
            let special = eq_bytes(w, b'"') | eq_bytes(w, b'\\') | control_bytes(w);
            if special != 0 {
                j += (special.trailing_zeros() / 8) as usize;
                return self.string_at(start, j);
            }
            j += 8;
        }
        while j < b.len() && !matches!(b[j], b'"' | b'\\' | 0..0x20) {
            j += 1;
        }
        self.string_at(start, j)
    }

    #[inline(always)]
    fn string_at(&mut self, start: usize, j: usize) -> Result<Node, JsonParseError> {
        match self.b.get(j) {
            Some(b'"') => {
                self.i = j + 1;
                Ok(Node::new(Kind::String, STR_SOURCE, (j - start) as u32, start as u64))
            }
            Some(b'\\') => self.escaped(start, j),
            Some(_) => {
                self.i = j;
                self.error("control character in a string")
            }
            None => {
                self.i = j;
                self.error("unterminated string")
            }
        }
    }

    /// The rest of a string with escapes, unescaped into the text buffer: `j` is at the first backslash.
    #[cold]
    fn escaped(&mut self, start: usize, mut j: usize) -> Result<Node, JsonParseError> {
        let offset = self.text.len();
        let b = self.b;
        let mut run = start;
        loop {
            match b.get(j) {
                Some(b'"') => {
                    self.text.push_str(&self.source[run..j]);
                    self.i = j + 1;
                    let len = self.text.len() - offset;
                    return Ok(Node::new(Kind::String, STR_TEXT, len as u32, offset as u64));
                }
                Some(b'\\') => {
                    self.text.push_str(&self.source[run..j]);
                    let c = match b.get(j + 1) {
                        Some(b'"') => '"',
                        Some(b'\\') => '\\',
                        Some(b'/') => '/',
                        Some(b'b') => '\u{8}',
                        Some(b'f') => '\u{c}',
                        Some(b'n') => '\n',
                        Some(b'r') => '\r',
                        Some(b't') => '\t',
                        Some(b'u') => {
                            let (c, next) = self.unicode_escape(j)?;
                            self.text.push(c);
                            j = next;
                            run = j;
                            continue;
                        }
                        _ => {
                            self.i = j;
                            return self.error("invalid escape");
                        }
                    };
                    self.text.push(c);
                    j += 2;
                    run = j;
                }
                Some(0..0x20) => {
                    self.i = j;
                    return self.error("control character in a string");
                }
                Some(_) => j += 1,
                None => {
                    self.i = j;
                    return self.error("unterminated string");
                }
            }
        }
    }

    /// A `\u` escape at `j` (and its low surrogate, for a high one): the character and the index after it.
    fn unicode_escape(&mut self, j: usize) -> Result<(char, usize), JsonParseError> {
        let hex = |p: &Self, at: usize| -> Option<u32> {
            let digits = p.b.get(at..at + 4)?;
            let mut v = 0u32;
            for &d in digits {
                v = v * 16 + (d as char).to_digit(16)?;
            }
            Some(v)
        };
        let Some(u) = hex(self, j + 2) else {
            self.i = j;
            return self.error("invalid \\u escape");
        };
        match u {
            0xD800..=0xDBFF => {
                let low = (self.b.get(j + 6) == Some(&b'\\') && self.b.get(j + 7) == Some(&b'u'))
                    .then(|| hex(self, j + 8))
                    .flatten();
                match low {
                    Some(l @ 0xDC00..=0xDFFF) => {
                        let c = 0x10000 + ((u - 0xD800) << 10) + (l - 0xDC00);
                        Ok((char::from_u32(c).unwrap(), j + 12))
                    }
                    _ => {
                        self.i = j;
                        self.error("lone leading surrogate in hex escape")
                    }
                }
            }
            0xDC00..=0xDFFF => {
                self.i = j;
                self.error("lone trailing surrogate in hex escape")
            }
            _ => Ok((char::from_u32(u).unwrap(), j + 6)),
        }
    }

    /// A number: unsigned or negative integers that fit 64 bits as integers, anything else as a double (as serde_json
    /// classifies them).
    #[inline(always)]
    fn number(&mut self) -> Result<Node, JsonParseError> {
        let b = self.b;
        let start = self.i;
        let mut j = start;
        let negative = b[j] == b'-';
        if negative {
            j += 1;
        }
        let mut value: u64 = 0;
        let mut overflow = false;
        match b.get(j) {
            Some(b'0') => j += 1,
            Some(b'1'..=b'9') => {
                while let Some(&d @ b'0'..=b'9') = b.get(j) {
                    match value.checked_mul(10).and_then(|v| v.checked_add((d - b'0') as u64)) {
                        Some(v) => value = v,
                        None => overflow = true,
                    }
                    j += 1;
                }
            }
            _ => {
                self.i = j;
                return self.error("invalid number");
            }
        }
        let mut float = false;
        if b.get(j) == Some(&b'.') {
            j += 1;
            if !matches!(b.get(j), Some(b'0'..=b'9')) {
                self.i = j;
                return self.error("invalid number");
            }
            while matches!(b.get(j), Some(b'0'..=b'9')) {
                j += 1;
            }
            float = true;
        }
        if matches!(b.get(j), Some(b'e' | b'E')) {
            j += 1;
            if matches!(b.get(j), Some(b'+' | b'-')) {
                j += 1;
            }
            if !matches!(b.get(j), Some(b'0'..=b'9')) {
                self.i = j;
                return self.error("invalid number");
            }
            while matches!(b.get(j), Some(b'0'..=b'9')) {
                j += 1;
            }
            float = true;
        }
        self.i = j;
        if !float && !overflow {
            if !negative {
                return Ok(Node::new(Kind::Number, NUM_U64, 0, value));
            }
            // serde_json reads -0 as the double.
            if value != 0 && value <= i64::MAX as u64 + 1 {
                return Ok(Node::new(Kind::Number, NUM_I64, 0, (value as i64).wrapping_neg() as u64));
            }
        }
        let f: f64 = self.source[start..j].parse().unwrap_or(f64::INFINITY);
        if !f.is_finite() {
            self.i = start;
            return self.error("number out of range");
        }
        Ok(Node::new(Kind::Number, NUM_F64, 0, f.to_bits()))
    }
}

#[cfg(test)]
mod tests {
    use super::*;

    fn same(text: &str) {
        let expected: Value = serde_json::from_str(text).unwrap();
        let document = JsonDocument::parse(text).unwrap_or_else(|e| panic!("{text}: {e}"));
        assert_eq!(document.to_value(), expected, "{text}");
        // The number representations match too (serde_json's Value equality compares them).
        assert_eq!(serde_json::to_string(&document.to_value()).unwrap(), serde_json::to_string(&expected).unwrap());
    }

    fn both_reject(text: &str) {
        assert!(serde_json::from_str::<Value>(text).is_err(), "serde_json accepts {text}");
        assert!(JsonDocument::parse(text).is_err(), "accepted {text}");
    }

    #[test]
    fn values_match_serde_json() {
        for text in [
            "null",
            " true ",
            "false",
            "0",
            "-0",
            "-0.0",
            "1",
            "-1",
            "18446744073709551615",
            "18446744073709551616",
            "-9223372036854775808",
            "-9223372036854775809",
            "1.5",
            "1e3",
            "1E+3",
            "1.0e-3",
            "123456789012345678901234567890",
            "2.2250738585072014e-308",
            "\"\"",
            "\"plain text, longer than eight bytes\"",
            "\"caf\u{e9} \u{1f600}\"",
            r#""esc \" \\ \/ \b \f \n \r \t é 😀 end""#,
            r#""\u0000""#,
            "[]",
            "{}",
            "[1, [2, [3, []]], {}]",
            r#"{"a": 1, "b": [true, null], "c": {"d": "e"}}"#,
            r#"{"a": 1, "b": 2, "a": 3}"#,
            r#"{"x": 1, "y": 2, "x": {"z": 1}, "y": 4, "w": 5}"#,
        ] {
            same(text);
        }
        let many: String =
            format!("{{{}}}", (0..40).map(|i| format!("\"k{}\": {i}", i % 25)).collect::<Vec<_>>().join(","));
        same(&many);
    }

    #[test]
    fn doubles_are_correctly_rounded() {
        // serde_json (without float_roundtrip) reads this one ulp off.
        let text = "-90.50242899999999";
        let d = JsonDocument::parse(text).unwrap();
        assert_eq!(d.to_value().as_f64().unwrap().to_bits(), text.parse::<f64>().unwrap().to_bits());
    }

    #[test]
    fn rejects_what_serde_json_rejects() {
        for text in [
            "",
            " ",
            "nul",
            "tru",
            "01",
            "-",
            "1.",
            ".5",
            "1e",
            "+1",
            "1e400",
            "[1,]",
            "[1 2]",
            "{\"a\" 1}",
            "{\"a\": 1,}",
            "{a: 1}",
            "\"unterminated",
            "\"tab\there\"",
            r#""\x""#,
            r#""\ud800""#,
            r#""\udc00""#,
            r#""\ud800A""#,
            "[1] 2",
            "[",
            "{",
        ] {
            both_reject(text);
        }
    }

    #[test]
    fn depth_limit() {
        for inner in ["", "1"] {
            let nest = |n: usize| format!("{}{inner}{}", "[".repeat(n), "]".repeat(n));
            assert!(JsonDocument::parse(&nest(MAX_DEPTH)).is_ok());
            assert!(JsonDocument::parse(&nest(MAX_DEPTH + 1)).is_err());
            assert!(serde_json::from_str::<Value>(&nest(MAX_DEPTH)).is_ok());
            assert!(serde_json::from_str::<Value>(&nest(MAX_DEPTH + 1)).is_err());
        }
    }

    #[test]
    fn reads_in_place() {
        let d = JsonDocument::parse(r#"{"a": [1, "x\n"], "b": {"c": null}}"#).unwrap();
        let View::Object(o) = d.root().view() else { panic!() };
        assert_eq!(o.len(), 2);
        assert!(o.contains_key("b") && !o.contains_key("c"));
        let View::Array(a) = o.get("a").unwrap().view() else { panic!() };
        assert_eq!(a.len(), 2);
        assert!(matches!(a.get(1).view(), View::String("x\n")));
        assert_eq!(a.tail(1).count(), 1);
        assert_eq!(a.tail(5).count(), 0);
        assert_eq!(o.iter().map(|(k, _)| k).collect::<Vec<_>>(), ["a", "b"]);
    }
}
