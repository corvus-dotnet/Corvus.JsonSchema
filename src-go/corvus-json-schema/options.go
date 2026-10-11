package jsonschema

import (
	"errors"
	"strconv"
	"time"
)

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
	matchLimit                 time.Duration
}

func defaultOptions() compileOptions {
	return compileOptions{defaultDialect: Draft202012, assertContent: true, maxDepth: 128, matchLimit: DefaultPatternMatchLimit}
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

// DefaultPatternMatchLimit is the time one match of a pattern or patternProperties regular expression against one
// string may take, unless WithPatternMatchLimit says otherwise.
const DefaultPatternMatchLimit = time.Second

// NoPatternMatchLimit, given to WithPatternMatchLimit, turns the limit on a match off.
const NoPatternMatchLimit time.Duration = 0

// WithPatternMatchLimit sets the time one match of a pattern or patternProperties regular expression against one
// string may take. The default is DefaultPatternMatchLimit, one second. A match that takes longer is abandoned, and
// the validation it belongs to returns a *PatternMatchLimitError. Zero (NoPatternMatchLimit) or less turns the
// limit off, for a caller who trusts both the schema and the instances. Without the limit a match also has no
// limit on its memory.
//
// The limit is on each match, not on the validation. Only the backtracking matcher can take long, and it reads the
// clock every 4,096 steps, so a match is abandoned within tens of microseconds of the limit. The matchers that
// take time proportional to the text are not stopped.
func WithPatternMatchLimit(limit time.Duration) Option {
	return func(o *compileOptions) {
		if limit < 0 {
			limit = 0
		}
		o.matchLimit = limit
	}
}

// ErrPatternMatchLimit is the error every *PatternMatchLimitError matches with errors.Is.
var ErrPatternMatchLimit = errors.New("a regular expression match reached its limit")

// PatternMatchLimitError reports a validation that was abandoned because one match of a pattern or
// patternProperties regular expression reached its limit. The instance is then neither valid nor not valid.
type PatternMatchLimitError struct {
	// Pattern is the regular expression.
	Pattern string
	// Limit is the time the match was allowed (see WithPatternMatchLimit).
	Limit time.Duration
	// Memory says the match was abandoned for the memory of its backtracking, not for its time: it held 1,048,576
	// choices, which is 24 MB.
	Memory bool
}

// Error implements the error interface.
func (e *PatternMatchLimitError) Error() string {
	if e.Memory {
		return "A match of the regular expression " + shownPattern(e.Pattern) + " was abandoned: it needed more than the limit of 1,048,576 backtracking entries."
	}
	return "A match of the regular expression " + shownPattern(e.Pattern) + " was abandoned: it took longer than the limit of " + e.Limit.String() + "."
}

// Is reports whether target is ErrPatternMatchLimit.
func (e *PatternMatchLimitError) Is(target error) bool { return target == ErrPatternMatchLimit }

// shownPattern quotes a pattern for a message, or its first 120 bytes and its length when it is longer.
func shownPattern(pattern string) string {
	const shown = 120
	if len(pattern) <= shown {
		return "'" + pattern + "'"
	}
	cut := shown
	for cut > 0 && pattern[cut]&0xC0 == 0x80 {
		cut--
	}
	return "'" + pattern[:cut] + "...' (" + strconv.Itoa(len(pattern)) + " bytes)"
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
