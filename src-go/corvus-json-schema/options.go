package jsonschema

import "errors"

// DocumentResolver resolves a schema document by absolute URI. It returns nil if the document is unknown.
type DocumentResolver func(uri string) *Document

// FormatValidator is a custom format assertion over the string value (or the number's JSON text, for numbers).
type FormatValidator func(value string) bool

// compileOptions are the options for compiling a schema.
type compileOptions struct {
	defaultDialect Dialect
	// Whether format is asserted: 1 always, -1 never, 0 as the vocabularies say.
	assertFormat               int8
	assertFormatInLegacyDrafts bool
	assertContent              bool
	formats                    map[string]FormatValidator
	resolver                   DocumentResolver
	baseURI                    string
	entryPoint                 string
	hasEntryPoint              bool
	maxDepth                   int
}

func defaultOptions() compileOptions {
	return compileOptions{defaultDialect: Draft202012, assertContent: true, maxDepth: 128}
}

// Option configures the compilation of a schema.
type Option func(*compileOptions)

// WithDefaultDialect sets the dialect of documents without $schema. The default is Draft202012.
func WithDefaultDialect(dialect Dialect) Option {
	return func(o *compileOptions) { o.defaultDialect = dialect }
}

// WithAssertFormat says whether format is asserted: true always, false never. Without this option the vocabularies
// decide (the 2020-12 format-assertion vocabulary asserts, anything else annotates).
func WithAssertFormat(assert bool) Option {
	return func(o *compileOptions) {
		if assert {
			o.assertFormat = 1
		} else {
			o.assertFormat = -1
		}
	}
}

// WithAssertFormatInLegacyDrafts also asserts format in drafts 4 to 7, when WithAssertFormat is not given.
func WithAssertFormatInLegacyDrafts(assert bool) Option {
	return func(o *compileOptions) { o.assertFormatInLegacyDrafts = assert }
}

// WithAssertContent says whether contentEncoding and contentMediaType are asserted in draft 7, the only draft that
// asserts them. The default is true.
func WithAssertContent(assert bool) Option {
	return func(o *compileOptions) { o.assertContent = assert }
}

// WithFormat adds a custom format assertion, which takes precedence over a built-in one of the same name. It
// receives a copy of the string value, or a number's JSON text.
func WithFormat(name string, validator FormatValidator) Option {
	return func(o *compileOptions) {
		if o.formats == nil {
			o.formats = make(map[string]FormatValidator)
		}
		o.formats[name] = validator
	}
}

// WithDocumentResolver sets the function that resolves remote documents. The standard metaschemas are always
// available.
func WithDocumentResolver(resolver DocumentResolver) Option {
	return func(o *compileOptions) { o.resolver = resolver }
}

// WithBaseURI sets the base URI of the root document.
func WithBaseURI(uri string) Option {
	return func(o *compileOptions) { o.baseURI = uri }
}

// WithEntryPoint sets a reference, relative to the root, to evaluate from (for example #/$defs/item). The default is
// the root.
func WithEntryPoint(reference string) Option {
	return func(o *compileOptions) { o.entryPoint, o.hasEntryPoint = reference, true }
}

// WithMaxDepth sets the maximum depth of in-place recursion on a cycle before evaluation is abandoned. The default
// is 128. Values below 1 are ignored.
func WithMaxDepth(depth int) Option {
	return func(o *compileOptions) {
		if depth >= 1 {
			o.maxDepth = depth
		}
	}
}

// CompileError reports a schema that could not be compiled (an unresolvable reference, an invalid pattern).
type CompileError struct {
	// Message is the reason compilation failed.
	Message string
}

// Error implements the error interface.
func (e *CompileError) Error() string {
	return e.Message
}

func compileError(message string) error {
	return &CompileError{Message: message}
}

// ErrDepthExceeded reports that evaluation recursed in place beyond the maximum depth (a schema that loops without
// consuming the instance).
var ErrDepthExceeded = errors.New("the schema recursed in place beyond the maximum depth")
