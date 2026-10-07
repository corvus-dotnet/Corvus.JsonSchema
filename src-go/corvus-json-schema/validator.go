// Package jsonschema is a JSON Schema evaluator for draft 4, 6, 7, 2019-09 and 2020-12, ported from the
// Corvus.Text.Json runtime evaluator.
//
// A schema is compiled once into a node graph with fail-fast plans, then any number of instances are validated
// against it. IsValid and its variants fail fast and report nothing. Evaluate is exhaustive and reports to a
// ResultsCollector at the Basic, Detailed or Verbose level, with the same rows (paths, messages, order) as the
// other Corvus implementations, annotations included.
//
//	validator, err := jsonschema.CompileString(`{
//	    "type": "object",
//	    "properties": {"id": {"type": "integer", "minimum": 1}},
//	    "required": ["id"]
//	}`)
//	if err != nil {
//	    return err
//	}
//	validator.IsValidString(`{"id": 3}`) // true
//	validator.IsValidString(`{"id": 0}`) // false
//
// In the steady state, validating a parsed Document, or JSON text, allocates nothing.
package jsonschema

import (
	"sync"
	"unsafe"
)

// Validator is a compiled schema. It is immutable and safe for concurrent use.
type Validator struct {
	program *program
	// Evaluators not in use, with the buffers they have grown.
	evaluators sync.Pool
}

func newValidator(compiled *compiledSchema, options *compileOptions) *Validator {
	v := &Validator{program: newProgram(compiled, options)}
	v.evaluators.New = func() any { return newEvaluator(v.program, nil) }
	return v
}

func applyOptions(options []Option) *compileOptions {
	o := defaultOptions()
	for _, option := range options {
		option(&o)
	}
	return &o
}

// Compile compiles a schema given as UTF-8 JSON text. The validator keeps the slice. Do not modify it afterwards.
// The error is a *ParseError when the text is not JSON, and a *CompileError when the schema cannot be compiled.
func Compile(schema []byte, options ...Option) (*Validator, error) {
	doc, err := ParseDocument(schema)
	if err != nil {
		return nil, err
	}
	return CompileDocument(doc, options...)
}

// CompileString compiles a schema given as JSON text.
func CompileString(schema string, options ...Option) (*Validator, error) {
	return Compile([]byte(schema), options...)
}

// CompileDocument compiles a schema document.
func CompileDocument(schema *Document, options ...Option) (*Validator, error) {
	o := applyOptions(options)
	compiled, err := compileDocument(schema, o)
	if err != nil {
		return nil, err
	}
	return newValidator(compiled, o), nil
}

// CompileURI compiles the schema document at a URI, fetched through the document resolver given in the options (or
// one of the standard metaschemas).
func CompileURI(uri string, options ...Option) (*Validator, error) {
	o := applyOptions(options)
	compiled, err := compileFromURI(uri, o)
	if err != nil {
		return nil, err
	}
	return newValidator(compiled, o), nil
}

func (v *Validator) acquire() *evaluator {
	return v.evaluators.Get().(*evaluator)
}

// The buffers an evaluator keeps between validations, in bytes, beyond which it is dropped instead of pooled.
const evaluatorRetainedLimit = 4 << 20

func (v *Validator) release(e *evaluator) {
	// Let go of the instance, which the caller owns.
	e.d = nil
	e.text.source = nil
	if e.parser.retainsTooMuch() || 8*(cap(e.text.tape)+cap(e.arena)+cap(e.unique)) > evaluatorRetainedLimit {
		return
	}
	v.evaluators.Put(e)
}

// stringBytes views a string's bytes, which are only read.
func stringBytes(s string) []byte {
	return unsafe.Slice(unsafe.StringData(s), len(s))
}

// IsValid reports whether an instance is valid. A schema that recursed in place beyond the maximum depth is
// reported as invalid. Use Validate to tell the two apart.
func (v *Validator) IsValid(instance *Document) bool {
	e := v.acquire()
	ok := e.validate(instance)
	v.release(e)
	return ok
}

// IsValidBytes reports whether UTF-8 JSON text is a valid instance. Text that is not JSON is not valid. The text is
// parsed into buffers the validator reuses, so a validation allocates nothing in the steady state.
func (v *Validator) IsValidBytes(json []byte) bool {
	e := v.acquire()
	ok := e.parser.parseInto(&e.text, json) && e.validate(&e.text)
	v.release(e)
	return ok
}

// IsValidString is IsValidBytes for a string.
func (v *Validator) IsValidString(json string) bool {
	return v.IsValidBytes(stringBytes(json))
}

// Validate reports whether an instance is valid. The error is ErrDepthExceeded when evaluation recursed in place
// beyond the maximum depth.
func (v *Validator) Validate(instance *Document) (bool, error) {
	e := v.acquire()
	ok := e.validate(instance)
	exceeded := e.depthExceeded
	v.release(e)
	if exceeded {
		return false, ErrDepthExceeded
	}
	return ok, nil
}

// ValidateBytes reports whether UTF-8 JSON text is a valid instance. The error is a *ParseError when the text is
// not JSON, and ErrDepthExceeded when evaluation recursed in place beyond the maximum depth. The text is parsed into
// buffers the validator reuses, so a validation of JSON text allocates nothing in the steady state.
func (v *Validator) ValidateBytes(json []byte) (bool, error) {
	e := v.acquire()
	if !e.parser.parseInto(&e.text, json) {
		err := e.parser.err()
		v.release(e)
		return false, err
	}
	ok := e.validate(&e.text)
	exceeded := e.depthExceeded
	v.release(e)
	if exceeded {
		return false, ErrDepthExceeded
	}
	return ok, nil
}

// ValidateString is ValidateBytes for a string.
func (v *Validator) ValidateString(json string) (bool, error) {
	return v.ValidateBytes(stringBytes(json))
}

// Evaluate evaluates an instance exhaustively, reporting to the collector, and reports whether the instance is
// valid. The error is ErrDepthExceeded when evaluation recursed in place beyond the maximum depth.
func (v *Validator) Evaluate(instance *Document, collector *ResultsCollector) (bool, error) {
	e := newEvaluator(v.program, collector)
	ok := e.evaluate(instance)
	if e.depthExceeded {
		return false, ErrDepthExceeded
	}
	return ok, nil
}

// EvaluateBytes is Evaluate for UTF-8 JSON text. The error is a *ParseError when the text is not JSON.
func (v *Validator) EvaluateBytes(json []byte, collector *ResultsCollector) (bool, error) {
	instance, err := ParseDocument(json)
	if err != nil {
		return false, err
	}
	return v.Evaluate(instance, collector)
}

// EvaluateString is Evaluate for JSON text.
func (v *Validator) EvaluateString(json string, collector *ResultsCollector) (bool, error) {
	return v.EvaluateBytes(stringBytes(json), collector)
}
