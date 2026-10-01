//! Results collection: a port of Corvus.Text.Json's `JsonSchemaResultsCollector` and `JsonSchemaAnnotationProducer`.
//!
//! The evaluator opens a context per subschema application and writes keyword rows into the open context. Closing a
//! context either commits it (a summary row, then its own rows newest first, after its committed descendants) or
//! pops it (everything it and its descendants wrote is discarded). Levels decide which rows exist and which carry
//! message text: Basic has failures without text, Detailed adds text to failures, Verbose keeps every row with text,
//! annotations included.

use std::collections::BTreeMap;

use serde_json::Value;

/// How much a results collector records.
#[derive(Clone, Copy, Debug, PartialEq, Eq, PartialOrd, Ord)]
pub enum ResultsLevel {
    /// Failures only, without message text (the lowest overhead).
    Basic,
    /// Failures only, with message text.
    Detailed,
    /// Every evaluation, passing and failing, with message text, including annotations.
    Verbose,
}

/// One result row.
#[derive(Clone, Debug, PartialEq, Eq)]
pub struct SchemaResult {
    pub is_match: bool,
    /// The message, or `""` when the level records none or the keyword has none. Annotation rows carry raw JSON.
    pub message: String,
    /// The path of keywords from the root schema (e.g. `/properties/name/type`).
    pub evaluation_location: String,
    /// The JSON pointer of the evaluated schema (or keyword) within its document (e.g. `/properties/name/type`).
    pub schema_evaluation_location: String,
    /// The JSON pointer of the instance location (e.g. `/name`).
    pub document_evaluation_location: String,
}

/// A message for a result row, produced only when the level records message text.
pub(crate) enum Message<'a> {
    None,
    Static(&'static str),
    Lazy(&'a dyn Fn() -> String),
}

struct Frame {
    eval_len: usize,
    schema_path: String,
    doc_len: usize,
    commit_index: usize,
    rows_start: usize,
}

/// Collects the results of an evaluation (`JsonSchemaResultsCollector`).
pub struct JsonSchemaResultsCollector {
    level: ResultsLevel,
    committed: Vec<SchemaResult>,
    frames: Vec<Frame>,
    /// The rows written into open frames (each frame owns the tail from its `rows_start`).
    pending: Vec<SchemaResult>,
    eval_path: String,
    schema_path: String,
    doc_path: String,
}

/// Encodes a JSON pointer segment (`~` as `~0`, `/` as `~1`).
pub fn encode_pointer_segment(segment: &str) -> std::borrow::Cow<'_, str> {
    crate::uri::escape_pointer_token(segment)
}

impl JsonSchemaResultsCollector {
    /// Creates a collector at the given level.
    pub fn new(level: ResultsLevel) -> Self {
        JsonSchemaResultsCollector {
            level,
            committed: Vec::new(),
            frames: Vec::new(),
            pending: Vec::new(),
            eval_path: String::new(),
            schema_path: String::new(),
            doc_path: String::new(),
        }
    }

    pub fn level(&self) -> ResultsLevel {
        self.level
    }

    /// The results, in commit order.
    pub fn results(&self) -> &[SchemaResult] {
        &self.committed
    }

    /// Removes and returns the results.
    pub fn take_results(&mut self) -> Vec<SchemaResult> {
        std::mem::take(&mut self.committed)
    }

    // ------------------------------------------------------------------------------------------------------------
    // The evaluator's side

    /// Opens a child context. The evaluation path is extended by `eval_segment` (verbatim), the schema path is
    /// replaced by `schema_location`, and the document path is extended by `doc_segment` (already pointer-encoded).
    pub(crate) fn begin_child_context(
        &mut self,
        eval_segment: Option<&str>,
        schema_location: Option<&str>,
        doc_segment: Option<&str>,
    ) {
        self.frames.push(Frame {
            eval_len: self.eval_path.len(),
            schema_path: self.schema_path.clone(),
            doc_len: self.doc_path.len(),
            commit_index: self.committed.len(),
            rows_start: self.pending.len(),
        });
        if let Some(s) = eval_segment {
            self.eval_path.push('/');
            self.eval_path.push_str(s);
        }
        if let Some(s) = schema_location {
            self.schema_path.clear();
            self.schema_path.push_str(s);
        }
        if let Some(s) = doc_segment {
            self.doc_path.push('/');
            self.doc_path.push_str(s);
        }
    }

    /// Closes a child context. When the parent does not need the child's results (`parent_is_match`) they are
    /// discarded below Verbose; otherwise the context's summary row is written and its rows are committed.
    pub(crate) fn commit_child_context(&mut self, parent_is_match: bool, child_is_match: bool, message: Message<'_>) {
        if parent_is_match && self.level != ResultsLevel::Verbose {
            self.pop_child_context();
            return;
        }
        let row =
            self.row(child_is_match, message, self.eval_path.clone(), self.schema_path.clone(), self.doc_path.clone());
        self.pending.push(row);
        let frame = self.frames.pop().unwrap();
        let rows = self.pending.split_off(frame.rows_start);
        self.committed.extend(rows.into_iter().rev());
        self.restore(frame);
    }

    /// Closes a child context and discards everything it and its descendants wrote.
    pub(crate) fn pop_child_context(&mut self) {
        let frame = self.frames.pop().unwrap();
        self.committed.truncate(frame.commit_index);
        self.pending.truncate(frame.rows_start);
        self.restore(frame);
    }

    pub(crate) fn evaluated_keyword(&mut self, is_match: bool, message: Message<'_>, keyword: &str) {
        if !is_match || self.level == ResultsLevel::Verbose {
            let k = encode_pointer_segment(keyword);
            let row = self.row(
                is_match,
                message,
                format!("{}/{k}", self.eval_path),
                format!("{}/{k}", self.schema_path),
                self.doc_path.clone(),
            );
            self.pending.push(row);
        }
    }

    pub(crate) fn evaluated_keyword_for_property(
        &mut self,
        is_match: bool,
        message: Message<'_>,
        property_name: &str,
        keyword: &str,
    ) {
        if !is_match || self.level == ResultsLevel::Verbose {
            let k = encode_pointer_segment(keyword);
            let row = self.row(
                is_match,
                message,
                format!("{}/{k}", self.eval_path),
                format!("{}/{k}", self.schema_path),
                format!("{}/{}", self.doc_path, encode_pointer_segment(property_name)),
            );
            self.pending.push(row);
        }
    }

    /// An annotation: Verbose only; the keyword extends the evaluation path but not the schema path.
    pub(crate) fn ignored_keyword(&mut self, message: Message<'_>, keyword: &str) {
        if self.level == ResultsLevel::Verbose {
            let row = self.row(
                true,
                message,
                format!("{}/{}", self.eval_path, encode_pointer_segment(keyword)),
                self.schema_path.clone(),
                self.doc_path.clone(),
            );
            self.pending.push(row);
        }
    }

    pub(crate) fn evaluated_boolean_schema(&mut self, is_match: bool) {
        if !is_match || self.level == ResultsLevel::Verbose {
            let row = self.row(
                is_match,
                Message::None,
                self.eval_path.clone(),
                self.schema_path.clone(),
                self.doc_path.clone(),
            );
            self.pending.push(row);
        }
    }

    fn row(
        &self,
        is_match: bool,
        message: Message<'_>,
        evaluation_location: String,
        schema_evaluation_location: String,
        document_evaluation_location: String,
    ) -> SchemaResult {
        let with_text = self.level == ResultsLevel::Verbose || (!is_match && self.level >= ResultsLevel::Detailed);
        let message = if with_text {
            match message {
                Message::None => String::new(),
                Message::Static(s) => s.to_string(),
                Message::Lazy(f) => f(),
            }
        } else {
            String::new()
        };
        SchemaResult {
            is_match,
            message,
            evaluation_location,
            schema_evaluation_location,
            document_evaluation_location,
        }
    }

    fn restore(&mut self, frame: Frame) {
        self.eval_path.truncate(frame.eval_len);
        self.schema_path = frame.schema_path;
        self.doc_path.truncate(frame.doc_len);
    }
}

/// An annotation extracted from verbose results.
#[derive(Clone, Debug, PartialEq, Eq)]
pub struct Annotation {
    /// The instance location (JSON pointer).
    pub instance_location: String,
    pub keyword: String,
    /// The JSON pointer of the schema object that holds the keyword.
    pub schema_location: String,
    /// The annotation value as JSON text.
    pub value: String,
}

/// The annotations in a verbose collector's results (`JsonSchemaAnnotationProducer.EnumerateAnnotations`).
pub fn enumerate_annotations(collector: &JsonSchemaResultsCollector) -> impl Iterator<Item = Annotation> + '_ {
    collector.results().iter().filter_map(|r| {
        if !r.is_match || r.message.is_empty() {
            return None;
        }
        let slash = r.evaluation_location.rfind('/')?;
        if r.evaluation_location == r.schema_evaluation_location {
            return None;
        }
        let keyword = &r.evaluation_location[slash + 1..];
        let first = r.message.as_bytes()[0];
        if keyword.is_empty()
            || !(matches!(first, b'"' | b'{' | b'[' | b't' | b'f' | b'n' | b'-') || first.is_ascii_digit())
        {
            return None;
        }
        Some(Annotation {
            instance_location: r.document_evaluation_location.clone(),
            keyword: keyword.to_string(),
            schema_location: r.schema_evaluation_location.clone(),
            value: r.message.clone(),
        })
    })
}

/// `#` followed by the schema location, percent-encoded as a URI fragment (upper-case hex, UTF-8).
pub fn schema_location_fragment(schema_location: &str) -> String {
    let mut out = String::from("#");
    for &byte in schema_location.as_bytes() {
        let c = byte as char;
        if byte < 128 && (c.is_ascii_alphanumeric() || "-._~!$&'()*+,;=:@/?".contains(c)) {
            out.push(c);
        } else {
            out.push_str(&format!("%{byte:02X}"));
        }
    }
    out
}

/// Annotations grouped by instance location, then keyword, then schema location fragment, with parsed values
/// (`JsonSchemaAnnotationProducer.WriteAnnotationsTo`): `{ "/name": { "title": { "#/properties/name": "Name" } } }`.
pub fn collect_annotations(
    collector: &JsonSchemaResultsCollector,
) -> BTreeMap<String, BTreeMap<String, BTreeMap<String, Value>>> {
    let mut out: BTreeMap<String, BTreeMap<String, BTreeMap<String, Value>>> = BTreeMap::new();
    for a in enumerate_annotations(collector) {
        let value = serde_json::from_str(&a.value).unwrap_or(Value::String(a.value.clone()));
        out.entry(a.instance_location)
            .or_default()
            .entry(a.keyword)
            .or_default()
            .insert(schema_location_fragment(&a.schema_location), value);
    }
    out
}
