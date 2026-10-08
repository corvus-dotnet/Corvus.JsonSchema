package jsonschema

import (
	"strings"
)

// Results collection. The evaluator opens a context per subschema application and writes keyword rows into the open
// context. Closing a context either commits it (a summary row, then its own rows newest first, after its committed
// descendants) or pops it (everything it and its descendants wrote is discarded). Levels decide which rows exist and
// which carry message text.

// ResultsLevel says how much a ResultsCollector records.
type ResultsLevel int

const (
	// Basic records failures only, without message text (the lowest overhead).
	Basic ResultsLevel = iota
	// Detailed records failures only, with message text.
	Detailed
	// Verbose records every evaluation, passing and failing, with message text, including annotations.
	Verbose
)

// String returns the level's name.
func (l ResultsLevel) String() string {
	switch l {
	case Basic:
		return "Basic"
	case Detailed:
		return "Detailed"
	case Verbose:
		return "Verbose"
	}
	return "unknown"
}

// SchemaResult is one result row.
type SchemaResult struct {
	// IsMatch says whether the keyword or subschema matched.
	IsMatch bool
	// Message is the message, or "" when the level records none or the keyword has none. Annotation rows carry raw
	// JSON.
	Message string
	// EvaluationLocation is the path of keywords from the root schema (for example /properties/name/type).
	EvaluationLocation string
	// SchemaEvaluationLocation is the JSON pointer of the evaluated schema (or keyword) within its document.
	SchemaEvaluationLocation string
	// DocumentEvaluationLocation is the JSON pointer of the instance location (for example /name).
	DocumentEvaluationLocation string
}

type resultsFrame struct {
	evalLength  int
	schemaPath  string
	docLength   int
	commitIndex int
	rowsStart   int
}

// ResultsCollector collects the results of an evaluation. A collector is used by one evaluation at a time.
type ResultsCollector struct {
	level     ResultsLevel
	committed []SchemaResult
	frames    []resultsFrame
	// The rows written into open frames (each frame owns the tail from its rowsStart).
	pending    []SchemaResult
	evalPath   []byte
	schemaPath string
	docPath    []byte
}

// NewResultsCollector creates a collector at the given level.
func NewResultsCollector(level ResultsLevel) *ResultsCollector {
	return &ResultsCollector{level: level}
}

// Level returns the level.
func (c *ResultsCollector) Level() ResultsLevel {
	return c.level
}

// Results returns the results, in commit order. The slice is valid until the collector is used again.
func (c *ResultsCollector) Results() []SchemaResult {
	return c.committed
}

// Reset discards the results, so that the collector can be used for another evaluation.
func (c *ResultsCollector) Reset() {
	c.committed = nil
	c.frames = c.frames[:0]
	c.pending = c.pending[:0]
	c.evalPath = c.evalPath[:0]
	c.schemaPath = ""
	c.docPath = c.docPath[:0]
}

// verbose reports whether passing rows are recorded.
func (c *ResultsCollector) verbose() bool {
	return c.level == Verbose
}

// withText reports whether a row with the given result carries its message.
func (c *ResultsCollector) withText(isMatch bool) bool {
	return c.level == Verbose || (!isMatch && c.level >= Detailed)
}

// records reports whether a keyword row with the given result is recorded at all.
func (c *ResultsCollector) records(isMatch bool) bool {
	return !isMatch || c.level == Verbose
}

// beginChildContext opens a child context. The evaluation path is extended by evalSegment (verbatim), the schema
// path is replaced by schemaLocation, and the document path is extended by docSegment (already pointer-encoded).
// hasEval and hasDoc say whether there is a segment at all.
func (c *ResultsCollector) beginChildContext(hasEval bool, evalSegment, schemaLocation string, hasDoc bool, docSegment string) {
	c.frames = append(c.frames, resultsFrame{
		evalLength: len(c.evalPath), schemaPath: c.schemaPath, docLength: len(c.docPath),
		commitIndex: len(c.committed), rowsStart: len(c.pending),
	})
	if hasEval {
		c.evalPath = append(append(c.evalPath, '/'), evalSegment...)
	}
	c.schemaPath = schemaLocation
	if hasDoc {
		c.docPath = append(append(c.docPath, '/'), docSegment...)
	}
}

// commitChildContext closes a child context. When the parent does not need the child's results (parentIsMatch) they
// are discarded below Verbose. Otherwise the context's summary row is written and its rows are committed.
func (c *ResultsCollector) commitChildContext(parentIsMatch, childIsMatch bool, message string) {
	if parentIsMatch && c.level != Verbose {
		c.popChildContext()
		return
	}
	if !c.withText(childIsMatch) {
		message = ""
	}
	c.pending = append(c.pending, SchemaResult{
		IsMatch: childIsMatch, Message: message, EvaluationLocation: string(c.evalPath),
		SchemaEvaluationLocation: c.schemaPath, DocumentEvaluationLocation: string(c.docPath),
	})
	frame := c.frames[len(c.frames)-1]
	c.frames = c.frames[:len(c.frames)-1]
	for i := len(c.pending) - 1; i >= frame.rowsStart; i-- {
		c.committed = append(c.committed, c.pending[i])
	}
	c.pending = c.pending[:frame.rowsStart]
	c.restore(frame)
}

// popChildContext closes a child context and discards everything it and its descendants wrote.
func (c *ResultsCollector) popChildContext() {
	frame := c.frames[len(c.frames)-1]
	c.frames = c.frames[:len(c.frames)-1]
	c.committed = c.committed[:frame.commitIndex]
	c.pending = c.pending[:frame.rowsStart]
	c.restore(frame)
}

// evaluatedKeyword records a keyword's result. The message is only looked at when withText says so.
func (c *ResultsCollector) evaluatedKeyword(isMatch bool, message, keyword string) {
	if c.records(isMatch) {
		k := "/" + escapePointerToken(keyword)
		c.pending = append(c.pending, SchemaResult{
			IsMatch: isMatch, Message: c.text(isMatch, message), EvaluationLocation: string(c.evalPath) + k,
			SchemaEvaluationLocation: c.schemaPath + k, DocumentEvaluationLocation: string(c.docPath),
		})
	}
}

func (c *ResultsCollector) evaluatedKeywordForProperty(isMatch bool, message, propertyName, keyword string) {
	if c.records(isMatch) {
		k := "/" + escapePointerToken(keyword)
		c.pending = append(c.pending, SchemaResult{
			IsMatch: isMatch, Message: c.text(isMatch, message), EvaluationLocation: string(c.evalPath) + k,
			SchemaEvaluationLocation:   c.schemaPath + k,
			DocumentEvaluationLocation: string(c.docPath) + "/" + escapePointerToken(propertyName),
		})
	}
}

// ignoredKeyword records an annotation: Verbose only. The keyword extends the evaluation path but not the schema
// path.
func (c *ResultsCollector) ignoredKeyword(message, keyword string) {
	if c.level == Verbose {
		c.pending = append(c.pending, SchemaResult{
			IsMatch: true, Message: message, EvaluationLocation: string(c.evalPath) + "/" + escapePointerToken(keyword),
			SchemaEvaluationLocation: c.schemaPath, DocumentEvaluationLocation: string(c.docPath),
		})
	}
}

func (c *ResultsCollector) evaluatedBooleanSchema(isMatch bool) {
	if c.records(isMatch) {
		c.pending = append(c.pending, SchemaResult{
			IsMatch: isMatch, EvaluationLocation: string(c.evalPath), SchemaEvaluationLocation: c.schemaPath,
			DocumentEvaluationLocation: string(c.docPath),
		})
	}
}

func (c *ResultsCollector) text(isMatch bool, message string) string {
	if c.withText(isMatch) {
		return message
	}
	return ""
}

func (c *ResultsCollector) restore(frame resultsFrame) {
	c.evalPath = c.evalPath[:frame.evalLength]
	c.schemaPath = frame.schemaPath
	c.docPath = c.docPath[:frame.docLength]
}

// Annotation is an annotation extracted from verbose results.
type Annotation struct {
	// InstanceLocation is the instance location (a JSON pointer).
	InstanceLocation string
	// Keyword is the annotating keyword.
	Keyword string
	// SchemaLocation is the JSON pointer of the schema object that holds the keyword.
	SchemaLocation string
	// Value is the annotation value as JSON text.
	Value string
}

// Annotations returns the annotations in a verbose collector's results.
func (c *ResultsCollector) Annotations() []Annotation {
	var out []Annotation
	for i := range c.committed {
		r := &c.committed[i]
		if !r.IsMatch || r.Message == "" {
			continue
		}
		slash := strings.LastIndexByte(r.EvaluationLocation, '/')
		if slash < 0 || r.EvaluationLocation == r.SchemaEvaluationLocation {
			continue
		}
		keyword := r.EvaluationLocation[slash+1:]
		first := r.Message[0]
		if keyword == "" || !(strings.IndexByte(`"{[tfn-`, first) >= 0 || isASCIIDigit(first)) {
			continue
		}
		out = append(out, Annotation{
			InstanceLocation: r.DocumentEvaluationLocation, Keyword: keyword,
			SchemaLocation: r.SchemaEvaluationLocation, Value: r.Message,
		})
	}
	return out
}

// SchemaLocationFragment returns "#" followed by the schema location, percent-encoded as a URI fragment (upper-case
// hex, UTF-8).
func SchemaLocationFragment(schemaLocation string) string {
	const hex = "0123456789ABCDEF"
	out := make([]byte, 1, len(schemaLocation)+1)
	out[0] = '#'
	for i := 0; i < len(schemaLocation); i++ {
		c := schemaLocation[i]
		if isASCIIAlphanumeric(c) || strings.IndexByte("-._~!$&'()*+,;=:@/?", c) >= 0 {
			out = append(out, c)
		} else {
			out = append(out, '%', hex[c>>4], hex[c&15])
		}
	}
	return string(out)
}

// CollectAnnotations returns the annotations grouped by instance location, then keyword, then schema location
// fragment, with the values as JSON text: {"/name": {"title": {"#/properties/name": "\"Name\""}}}.
func (c *ResultsCollector) CollectAnnotations() map[string]map[string]map[string]string {
	out := make(map[string]map[string]map[string]string)
	for _, a := range c.Annotations() {
		byKeyword := out[a.InstanceLocation]
		if byKeyword == nil {
			byKeyword = make(map[string]map[string]string)
			out[a.InstanceLocation] = byKeyword
		}
		byLocation := byKeyword[a.Keyword]
		if byLocation == nil {
			byLocation = make(map[string]string)
			byKeyword[a.Keyword] = byLocation
		}
		byLocation[SchemaLocationFragment(a.SchemaLocation)] = a.Value
	}
	return out
}
