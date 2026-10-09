package jsonschema

import (
	"errors"
	"fmt"
	"strings"
	"sync"
	"testing"
	"unicode/utf8"
)

// Public API behaviour, ported from the Rust crate's tests/api.rs.

// expectAgreement checks that fail-fast evaluation (through the plans) and collecting evaluation (through the
// general evaluator) give the same answer for each instance.
func expectAgreement(t *testing.T, v *Validator, label string, instances ...string) {
	t.Helper()
	for _, instance := range instances {
		collected, err := v.EvaluateString(instance, NewResultsCollector(Basic))
		if err != nil {
			t.Errorf("%s on %s: %v", label, instance, err)
			continue
		}
		if fast := v.IsValidString(instance); fast != collected {
			t.Errorf("%s on %s: fail-fast %v, collecting %v", label, instance, fast, collected)
		}
	}
}

func expectValid(t *testing.T, v *Validator, instance string, want bool) {
	t.Helper()
	if got := v.IsValidString(instance); got != want {
		t.Errorf("IsValid(%s) = %v, want %v", instance, got, want)
	}
	document := mustParse(t, instance)
	if got := v.IsValid(document); got != want {
		t.Errorf("IsValid(document %s) = %v, want %v", instance, got, want)
	}
}

func TestKeepsTheDynamicScope(t *testing.T) {
	tree := mustParse(t, `{
		"$schema": "https://json-schema.org/draft/2020-12/schema",
		"$id": "https://example.com/tree",
		"$dynamicAnchor": "node",
		"type": "object",
		"properties": { "data": true, "children": { "type": "array", "items": { "$dynamicRef": "#node" } } }
	}`)
	strict := `{
		"$schema": "https://json-schema.org/draft/2020-12/schema",
		"$id": "https://example.com/strict-tree",
		"$dynamicAnchor": "node",
		"$ref": "tree",
		"unevaluatedProperties": false
	}`
	v := mustCompile(t, strict, WithDocumentResolver(func(uri string) *Document {
		if uri == "https://example.com/tree" {
			return tree
		}
		return nil
	}))
	expectValid(t, v, `{ "children": [{ "data": 1, "children": [] }] }`, true)
	expectValid(t, v, `{ "children": [{ "daat": 1 }] }`, false)
}

func TestCustomFormatsAreAssertedWhenFormatAssertionIsOn(t *testing.T) {
	evenLength := func(s string) bool { return utf8.RuneCountInString(s)%2 == 0 }
	v := mustCompile(t, `{ "type": "string", "format": "even-length" }`, WithAssertFormat(true),
		WithFormat("even-length", evenLength))
	expectValid(t, v, `"ab"`, true)
	expectValid(t, v, `"abc"`, false)
	expectAgreement(t, v, "even-length", `"ab"`, `"abc"`, `1`)
	// A custom format on a number receives its JSON text.
	digits := mustCompile(t, `{ "format": "int32" }`, WithAssertFormat(true),
		WithFormat("int32", func(s string) bool { return s == "12" }))
	expectValid(t, digits, `12`, true)
	expectValid(t, digits, `12.0`, false)
	expectAgreement(t, digits, "custom int32", `12`, `13`, `"12"`)
}

func TestFormatIsAnAnnotationByDefaultAndAssertedOnRequest(t *testing.T) {
	schema := `{ "format": "ipv4" }`
	expectValid(t, mustCompile(t, schema), `"not an address"`, true)
	expectValid(t, mustCompile(t, schema, WithAssertFormat(true)), `"not an address"`, false)
	expectValid(t, mustCompile(t, schema, WithAssertFormat(true)), `"10.0.0.1"`, true)
	draft7 := `{ "$schema": "http://json-schema.org/draft-07/schema#", "format": "ipv4" }`
	expectValid(t, mustCompile(t, draft7), `"not an address"`, true)
	expectValid(t, mustCompile(t, draft7, WithAssertFormatInLegacyDrafts(true)), `"not an address"`, false)
	expectValid(t, mustCompile(t, draft7, WithAssertFormatInLegacyDrafts(true), WithAssertFormat(false)), `"x"`, true)
}

func TestContentIsAssertedInDraft7Only(t *testing.T) {
	schema := `{ "contentEncoding": "base64", "contentMediaType": "application/json" }`
	draft7 := mustCompile(t, schema, WithDefaultDialect(Draft7))
	expectValid(t, draft7, `"eyJhIjogMX0="`, true)
	expectValid(t, draft7, `"bm90IGpzb24="`, false)
	expectValid(t, draft7, `"not base64"`, false)
	expectValid(t, draft7, `12`, true)
	expectAgreement(t, draft7, "content", `"eyJhIjogMX0="`, `"bm90IGpzb24="`, `"not base64"`, `12`)
	expectValid(t, mustCompile(t, schema, WithDefaultDialect(Draft7), WithAssertContent(false)), `"not base64"`, true)
	expectValid(t, mustCompile(t, schema), `"not base64"`, true)
	json := mustCompile(t, `{ "contentMediaType": "application/json" }`, WithDefaultDialect(Draft7))
	expectValid(t, json, `"[1, 2]"`, true)
	expectValid(t, json, `"[1, 2"`, false)
}

func TestEntryPointEvaluatesFromASubschema(t *testing.T) {
	schema := `{ "$defs": { "positive": { "type": "number", "exclusiveMinimum": 0 } }, "type": "string" }`
	v := mustCompile(t, schema, WithEntryPoint("#/$defs/positive"))
	expectValid(t, v, `3`, true)
	expectValid(t, v, `-3`, false)
	expectValid(t, v, `"s"`, false)
	if _, err := CompileString(schema, WithEntryPoint("#/$defs/missing")); err == nil {
		t.Error("a missing entry point compiled")
	}
}

func TestBaseURIResolvesRelativeReferences(t *testing.T) {
	item := mustParse(t, `{ "type": "integer" }`)
	resolver := func(uri string) *Document {
		if uri == "https://example.com/schemas/item.json" {
			return item
		}
		return nil
	}
	v := mustCompile(t, `{ "items": { "$ref": "item.json" } }`, WithBaseURI("https://example.com/schemas/root.json"),
		WithDocumentResolver(resolver))
	expectValid(t, v, `[1, 2]`, true)
	expectValid(t, v, `[1, "2"]`, false)
	fromURI, err := CompileURI("https://example.com/schemas/item.json", WithDocumentResolver(resolver))
	if err != nil {
		t.Fatal(err)
	}
	expectValid(t, fromURI, `1`, true)
	expectValid(t, fromURI, `"1"`, false)
	if _, err := CompileURI("https://example.com/schemas/other.json", WithDocumentResolver(resolver)); err == nil {
		t.Error("an unknown URI compiled")
	}
}

func TestDefaultDialectAppliesToSchemasWithoutSchema(t *testing.T) {
	schema := `{ "items": [{ "type": "string" }], "additionalItems": false }`
	expectValid(t, mustCompile(t, schema, WithDefaultDialect(Draft7)), `["a", "b"]`, false)
	// In 2020-12 an array-valued "items" is not the tuple form, so neither keyword applies.
	expectValid(t, mustCompile(t, schema), `["a", "b"]`, true)
}

func TestCompilationErrors(t *testing.T) {
	var compileErr *CompileError
	if _, err := CompileString(`{ "$ref": "https://example.com/missing.json" }`); !errors.As(err, &compileErr) {
		t.Errorf("an unresolvable reference: %v", err)
	}
	if _, err := CompileString(`{ "pattern": "a(" }`); !errors.As(err, &compileErr) {
		t.Errorf("an invalid pattern: %v", err)
	}
	var parseErr *ParseError
	if _, err := CompileString(`{ "type": `); !errors.As(err, &parseErr) {
		t.Errorf("schema text that is not JSON: %v", err)
	}
}

func TestInPlaceRecursionBeyondMaxDepthIsAnError(t *testing.T) {
	schema := `{ "$defs": { "loop": { "allOf": [{ "$ref": "#/$defs/loop" }] } }, "$ref": "#/$defs/loop" }`
	v := mustCompile(t, schema, WithMaxDepth(16))
	if _, err := v.ValidateString(`1`); !errors.Is(err, ErrDepthExceeded) {
		t.Errorf("ValidateString: %v", err)
	}
	if _, err := v.Validate(mustParse(t, `1`)); !errors.Is(err, ErrDepthExceeded) {
		t.Errorf("Validate: %v", err)
	}
	if v.IsValidString(`1`) {
		t.Error("IsValidString reports a runaway schema as valid")
	}
	if _, err := v.EvaluateString(`1`, NewResultsCollector(Detailed)); !errors.Is(err, ErrDepthExceeded) {
		t.Errorf("EvaluateString: %v", err)
	}
	// The evaluator that gave up is fit for the next validation.
	if _, err := v.ValidateString(`2`); !errors.Is(err, ErrDepthExceeded) {
		t.Errorf("ValidateString again: %v", err)
	}
}

func TestNumbersAreComparedExactlyForMultipleOf(t *testing.T) {
	v := mustCompile(t, `{ "multipleOf": 0.01 }`)
	expectValid(t, v, `0.07`, true)
	expectValid(t, v, `19.99`, true)
	expectValid(t, v, `0.075`, false)
	expectValid(t, mustCompile(t, `{ "multipleOf": 0.0001 }`), `0.0075`, true)
}

func TestValidatorsAreSafeForConcurrentUse(t *testing.T) {
	v := mustCompile(t, `{ "type": "array", "items": { "type": "integer", "minimum": 0 }, "uniqueItems": true }`)
	var wg sync.WaitGroup
	failures := make(chan string, 64)
	for g := 0; g < 8; g++ {
		wg.Add(1)
		go func(g int) {
			defer wg.Done()
			var items []string
			for i := 0; i < 100; i++ {
				items = append(items, fmt.Sprint(g*1000+i))
			}
			valid := "[" + strings.Join(items, ",") + "]"
			invalid := "[" + strings.Join(items, ",") + ",-1]"
			for i := 0; i < 200; i++ {
				if !v.IsValidString(valid) || v.IsValidString(invalid) {
					failures <- valid
					return
				}
			}
		}(g)
	}
	wg.Wait()
	close(failures)
	for range failures {
		t.Error("a concurrent validation gave the wrong answer")
	}
}

func TestDiscriminatedOneOfAndAnyOfAgreeWithExhaustiveEvaluation(t *testing.T) {
	shape := func(kind, extra string) string {
		return fmt.Sprintf(`{ "type": "object", "properties": { "kind": { "const": %q }, %q: { "type": "number" } }, "required": ["kind", %q] }`, kind, extra, extra)
	}
	branches := "[" + shape("circle", "r") + "," + shape("square", "side") + `, { "properties": { "kind": { "enum": [1, true] } } }]`
	for _, keyword := range []string{"oneOf", "anyOf"} {
		v := mustCompile(t, fmt.Sprintf(`{ %q: %s }`, keyword, branches))
		expectAgreement(t, v, keyword,
			`{ "kind": "circle", "r": 1 }`, `{ "kind": "circle", "side": 1 }`, `{ "kind": "square", "side": 1 }`,
			`{ "kind": "triangle" }`, `{ "kind": 1.0 }`, `{ "kind": true }`, `{ "kind": false }`, `{}`, `"circle"`)
	}
}

func TestDiscriminatorsKeyNullAndNumbersByValue(t *testing.T) {
	branch := func(kind, extra string) string {
		return fmt.Sprintf(`{ "type": "object", "properties": { "kind": { "const": %s }, %q: { "type": "string" } }, "required": ["kind", %q] }`, kind, extra, extra)
	}
	schema := `{ "oneOf": [` + branch("null", "a") + "," + branch("1", "b") + "," + branch("1.0", "c") + "," + branch(`"x"`, "d") + `] }`
	v := mustCompile(t, schema)
	expectAgreement(t, v, "discriminator",
		`{ "kind": null, "a": "s" }`, `{ "kind": null, "b": "s" }`, `{ "kind": 1, "b": "s" }`,
		`{ "kind": 1, "b": "s", "c": "t" }`, `{ "kind": 1.0, "c": "t" }`, `{ "kind": "x", "d": "s" }`,
		`{ "kind": "y", "d": "s" }`, `{ "d": "s" }`)
	// Both numeric branches match 1 when it has both properties: oneOf fails.
	expectValid(t, v, `{ "kind": 1, "b": "s", "c": "t" }`, false)
}

func TestArraysOfSimpleArraysMatchTheGeneralPath(t *testing.T) {
	position := `{ "type": "array", "minItems": 2, "maxItems": 3, "items": { "type": "number" } }`
	for _, schema := range []string{
		`{ "type": "array", "items": ` + position + ` }`,
		`{ "type": "array", "items": { "type": ["array", "string"], "minItems": 2, "items": { "type": "integer" } } }`,
		`{ "type": "array", "items": { "type": "object", "minItems": 2 } }`,
		`{ "type": "array", "items": { "type": "array", "items": { "type": "array", "items": { "type": "number" } } } }`,
	} {
		expectAgreement(t, mustCompile(t, schema), schema,
			`[]`, `[[1, 2]]`, `[[1, 2], [3, 4, 5]]`, `[[1]]`, `[[1, 2, 3, 4]]`, `[[1, "a"]]`, `[[1.5, 2]]`,
			`[["x", "y"]]`, `["s", [1, 2]]`, `[{}, [1, 2]]`, `[[[1, 2]], [[3]]]`, `[[[1, "a"]]]`)
	}
}

func TestFusedNotRequiredAndAbsentPatternConditionsMatchTheGeneralPath(t *testing.T) {
	const extensions = `{ "patternProperties": { "^x-": true } }`
	for _, schema := range []string{
		// not: {required} alongside unevaluatedProperties (the OpenAPI example object).
		`{
			"type": "object",
			"properties": { "value": true, "externalValue": { "type": "string" }, "summary": { "type": "string" } },
			"not": { "required": ["value", "externalValue"] },
			"$ref": "#/$defs/ext",
			"unevaluatedProperties": false,
			"$defs": { "ext": ` + extensions + ` }
		}`,
		// An if deciding on no name matching a pattern (the OpenAPI responses object).
		`{
			"type": "object",
			"properties": { "default": { "type": "integer" } },
			"patternProperties": { "^[1-5](?:[0-9]{2}|XX)$": { "type": "integer" } },
			"$ref": "#/$defs/ext",
			"unevaluatedProperties": false,
			"if": { "patternProperties": { "^[1-5](?:[0-9]{2}|XX)$": false } },
			"then": { "required": ["default"] },
			"$defs": { "ext": ` + extensions + ` }
		}`,
		// Both, gated by another condition, with a name the pattern also matches.
		`{
			"type": "object",
			"properties": { "kind": true, "a": true, "b": true, "x-a": true },
			"allOf": [{ "$ref": "#/$defs/ext" }, { "properties": { "c": true } }],
			"if": { "properties": { "kind": { "const": "k" } }, "required": ["kind"] },
			"then": {
				"not": { "required": ["a", "b"] },
				"if": { "patternProperties": { "^x-": false } },
				"then": { "required": ["c"] },
				"else": { "properties": { "d": true } }
			},
			"unevaluatedProperties": false,
			"$defs": { "ext": ` + extensions + ` }
		}`,
	} {
		v := mustCompile(t, schema)
		if b := v.program.plans[v.program.root].body; b == nil || b.fused == nil {
			t.Errorf("the schema did not take a fused plan: %s", schema)
		}
		expectAgreement(t, v, schema,
			`{}`, `{ "value": 1 }`, `{ "value": 1, "externalValue": "u" }`, `{ "externalValue": 2 }`,
			`{ "x-y": 1, "summary": "s" }`, `{ "other": 1 }`, `{ "default": 1 }`, `{ "200": 1 }`,
			`{ "2XX": 1, "x-a": 1 }`, `{ "600": 1 }`, `{ "default": "a", "404": 1 }`, `{ "kind": "k" }`,
			`{ "kind": "k", "c": 1 }`, `{ "kind": "k", "a": 1, "b": 1, "c": 1 }`, `{ "kind": "k", "a": 1, "c": 1 }`,
			`{ "kind": "k", "x-a": 1, "d": 1 }`, `{ "kind": "k", "x-z": 1, "d": 1 }`, `{ "kind": "k", "d": 1, "c": 1 }`,
			`{ "kind": "j", "a": 1, "b": 1 }`)
	}
}

func TestFlatFusedObjectsMatchTheGeneralPath(t *testing.T) {
	const shared = `{ "properties": { "a": { "type": "string" }, "b": true }, "required": ["a"], "maxProperties": 3 }`
	for i, schema := range []string{
		`{ "allOf": [{ "$ref": "#/$defs/s" }], "properties": { "c": { "type": "integer" } }, "$defs": { "s": ` + shared + ` } }`,
		`{
			"allOf": [{ "$ref": "#/$defs/s" }, { "properties": { "a": { "type": "string" } }, "required": ["c"] }],
			"properties": { "c": { "type": "integer" } },
			"minProperties": 2,
			"$defs": { "s": ` + shared + ` }
		}`,
		// The same name with different schemas stays a fused plan.
		`{ "allOf": [{ "$ref": "#/$defs/s" }], "properties": { "a": { "minLength": 2 } }, "$defs": { "s": ` + shared + ` } }`,
	} {
		v := mustCompile(t, schema)
		b := v.program.plans[v.program.root].body
		if b == nil || b.fused == nil || (b.fused.flat != nil) != (i < 2) {
			t.Errorf("schema %d: unexpected plan (fused %v)", i, b != nil && b.fused != nil)
		}
		expectAgreement(t, v, schema,
			`{}`, `{ "a": "x" }`, `{ "a": "xy", "c": 1 }`, `{ "a": 1, "c": 1 }`, `{ "a": "x", "c": "1" }`,
			`{ "a": "x", "b": null, "c": 1 }`, `{ "a": "x", "b": 1, "c": 1, "d": 1 }`, `{ "c": 1 }`, `[]`)
	}
}

func TestFusedObjectsBelowADynamicReferenceMatchTheGeneralPath(t *testing.T) {
	// The items' $dynamicRef resolves (through the scope) to "strict", whose allOf contributor is in its own
	// resource.
	v := mustCompile(t, `{
		"$schema": "https://json-schema.org/draft/2020-12/schema",
		"$id": "https://example.com/root",
		"$ref": "strict",
		"$defs": {
			"strict": {
				"$id": "https://example.com/strict",
				"$dynamicAnchor": "node",
				"type": "object",
				"properties": { "data": true, "y": true, "children": { "type": "array", "items": { "$ref": "tree#/$defs/kids" } } },
				"allOf": [{ "$ref": "#/$defs/extra" }],
				"unevaluatedProperties": false,
				"$defs": { "extra": { "properties": { "x": { "type": "integer" } } } }
			},
			"tree": {
				"$id": "https://example.com/tree",
				"$dynamicAnchor": "node",
				"type": "object",
				"$defs": { "kids": { "$dynamicRef": "#node" } }
			}
		}
	}`)
	expectAgreement(t, v, "dynamic",
		`{}`, `{ "data": 1, "x": 2 }`, `{ "x": "a" }`, `{ "y": 1, "children": [{ "y": 1 }] }`,
		`{ "children": [{ "z": 1 }] }`, `{ "children": [{ "x": 1, "children": [{ "y": 2, "data": 3 }] }] }`,
		`{ "children": [{ "children": [{ "x": "no" }] }] }`, `{ "z": 1 }`)
	expectValid(t, v, `{ "children": [{ "x": 1, "children": [{ "y": 2 }] }] }`, true)
	expectValid(t, v, `{ "children": [{ "z": 1 }] }`, false)
}

// Small objects are decided by looking the few declared and required names up in them, large ones by visiting their
// properties. Both agree, top-level and nested.
func TestFewNamesAreLookedUpInSmallAndLargeObjects(t *testing.T) {
	v := mustCompile(t, `{
		"properties": { "a": { "type": "string" }, "n": { "properties": { "x": { "type": "integer" } } } },
		"required": ["b"]
	}`)
	for _, pad := range []int{0, 3, 40, 300} {
		var sb strings.Builder
		for i := 0; i < pad; i++ {
			fmt.Fprintf(&sb, `,"p%d": %d`, i, i)
		}
		p := sb.String()
		expectValid(t, v, `{"b": 1, "a": "x"`+p+`}`, true)
		expectValid(t, v, `{"a": "x"`+p+`}`, false)
		expectValid(t, v, `{"a": 1, "b": 1`+p+`}`, false)
		expectValid(t, v, `{"b": null`+p+`, "n": {"x": 2`+p+`}}`, true)
		expectValid(t, v, `{"b": null`+p+`, "n": {"x": "2"`+p+`}}`, false)
	}
}

// JSON text is validated in place. Invalid JSON and runaway recursion are told apart.
func TestValidatesJSONText(t *testing.T) {
	v := mustCompile(t, `{ "type": "array", "items": { "type": "integer" } }`)
	if ok, err := v.ValidateString("[1, 2, 3]"); !ok || err != nil {
		t.Errorf("[1, 2, 3]: %v, %v", ok, err)
	}
	if ok, err := v.ValidateBytes([]byte(`[1, "2"]`)); ok || err != nil {
		t.Errorf(`[1, "2"]: %v, %v`, ok, err)
	}
	var parseErr *ParseError
	if _, err := v.ValidateString("[1, 2"); !errors.As(err, &parseErr) || parseErr.Offset != 5 {
		t.Errorf("[1, 2: %v", err)
	}
	if v.IsValidString("[1, 2") || v.IsValidBytes(nil) {
		t.Error("text that is not JSON is valid")
	}
	c := NewResultsCollector(Detailed)
	if ok, err := v.EvaluateString(`["x"]`, c); ok || err != nil {
		t.Errorf(`["x"]: %v, %v`, ok, err)
	}
	found := false
	for _, r := range c.Results() {
		found = found || (!r.IsMatch && r.DocumentEvaluationLocation == "/0")
	}
	if !found {
		t.Errorf("no failure at /0: %v", c.Results())
	}
	if _, err := v.EvaluateString(`[`, c); !errors.As(err, &parseErr) {
		t.Errorf("EvaluateString of text that is not JSON: %v", err)
	}
}

// A format callback that validates JSON text itself, during a validation of JSON text on the same goroutine, gets
// buffers of its own.
func TestValidatingJSONFromAFormatCallbackWorks(t *testing.T) {
	inner := mustCompile(t, `{ "type": "object", "required": ["a"] }`)
	var v *Validator
	v = mustCompile(t, `{ "type": "array", "items": { "format": "embedded-json" } }`, WithAssertFormat(true),
		WithFormat("embedded-json", func(s string) bool {
			// The same validator too: an evaluation nested in its own callback.
			return inner.IsValidString(s) && v.IsValidString(`[]`)
		}))
	expectValid(t, v, `["{\"a\": 1}", "{\"a\": 2}"]`, true)
	expectValid(t, v, `["{\"a\": 1}", "{\"b\": 2}"]`, false)
	expectValid(t, v, `["{\"a\": 1}", "not json"]`, false)
}

// A number in a schema and the same number in an instance are the same float64, however it is written.
func TestSchemaAndInstanceNumbersParseAlike(t *testing.T) {
	for keyword, literal := range map[string]string{
		"exclusiveMaximum": "972783798187987123879878123.18878137",
		"exclusiveMinimum": "-972783798187987123879878123.18878137",
	} {
		for _, text := range []string{literal, strings.Replace(literal, "972783798187987123879878123.18878137", "9.727837981879871e+26", 1)} {
			v := mustCompile(t, fmt.Sprintf(`{%q: %s}`, keyword, text))
			expectValid(t, v, text, false)
			expectValid(t, v, literal, false)
		}
	}
}

func TestUnevaluatedItemsWithAStaticPrefixMatchTheGeneralPath(t *testing.T) {
	for _, schema := range []string{
		`{ "prefixItems": [{ "type": "integer" }], "unevaluatedItems": { "type": "string" } }`,
		`{ "allOf": [{ "prefixItems": [true, { "type": "integer" }] }], "prefixItems": [{ "type": "integer" }], "unevaluatedItems": false }`,
		`{ "allOf": [{ "items": { "type": "integer" } }], "unevaluatedItems": false }`,
		`{ "anyOf": [{ "prefixItems": [true, true] }, { "prefixItems": [{ "type": "integer" }] }], "unevaluatedItems": false }`,
		`{ "contains": { "type": "integer" }, "unevaluatedItems": { "type": "string" } }`,
		`{ "if": { "prefixItems": [{ "const": 1 }] }, "then": { "prefixItems": [true, true] }, "unevaluatedItems": false }`,
	} {
		expectAgreement(t, mustCompile(t, schema), schema,
			`[]`, `[1]`, `[1, 2]`, `[1, "a"]`, `["a"]`, `[1, 2, 3]`, `[1, "a", "b"]`, `["a", 1, "b"]`, `{}`, `[2, 2]`)
	}
}

func TestUniqueItemsOverLargeArrays(t *testing.T) {
	v := mustCompile(t, `{ "uniqueItems": true }`)
	var items []string
	for i := 0; i < 200; i++ {
		items = append(items, fmt.Sprintf(`{"id": %d, "tags": ["a", %d]}`, i, i%3))
	}
	expectValid(t, v, "["+strings.Join(items, ",")+"]", true)
	expectValid(t, v, "["+strings.Join(items, ",")+`,{"tags": ["a", 1.0], "id": 4e0}]`, false)
}

// A not whose subschema leads back to the schema it is in recurses in place like any other applicator. It stops at
// the maximum depth: the validation is an error, and IsValid reports the instance as invalid. (Evaluating not went
// around the depth guard, so the first of these schemas overflowed the stack, and for the others the not turned the
// abandoned evaluation's false into true.)
func TestNotOnAnInPlaceCycleStopsAtMaxDepth(t *testing.T) {
	schemas := []string{
		`{"not": {"$ref": "#"}}`,
		`{"not": {"not": {"$ref": "#"}}}`,
		`{"type": "integer", "not": {"$ref": "#"}}`,
		`{"allOf": [{"not": {"$ref": "#"}}]}`,
		`{"$defs": {"a": {"not": {"$ref": "#/$defs/b"}}, "b": {"not": {"$ref": "#/$defs/a"}}}, "$ref": "#/$defs/a"}`,
		`{"$defs": {"loop": {"allOf": [{"$ref": "#/$defs/loop"}]}}, "not": {"$ref": "#/$defs/loop"}}`,
		`{"$defs": {"loop": {"allOf": [{"$ref": "#/$defs/loop"}]}}, "not": {"not": {"$ref": "#/$defs/loop"}}}`,
		`{"$defs": {"loop": {"allOf": [{"$ref": "#/$defs/loop"}]}}, "properties": {"a": {"not": {"$ref": "#/$defs/loop"}}}}`,
		`{"unevaluatedProperties": false, "not": {"$ref": "#"}}`,
	}
	instances := []string{`1`, `"a"`, `{"a": 1}`, `[1]`}
	for _, schema := range schemas {
		v := mustCompile(t, schema, WithMaxDepth(16))
		for _, instance := range instances {
			if schema == schemas[7] && instance != `{"a": 1}` {
				// Only an object with the property reaches the loop.
				continue
			}
			if schema == schemas[2] && instance != `1` {
				// Anything but an integer fails the type before the not is reached, when failing fast.
				continue
			}
			doc := mustParse(t, instance)
			if v.IsValid(doc) || v.IsValidString(instance) || v.IsValidBytes([]byte(instance)) {
				t.Errorf("%s: IsValid reported %s as valid", schema, instance)
			}
			if _, err := v.Validate(doc); !errors.Is(err, ErrDepthExceeded) {
				t.Errorf("%s: Validate(%s) gave %v, not ErrDepthExceeded", schema, instance, err)
			}
			if _, err := v.ValidateString(instance); !errors.Is(err, ErrDepthExceeded) {
				t.Errorf("%s: ValidateString(%s) gave %v, not ErrDepthExceeded", schema, instance, err)
			}
			for _, level := range []ResultsLevel{Basic, Detailed, Verbose} {
				if _, err := v.Evaluate(doc, NewResultsCollector(level)); !errors.Is(err, ErrDepthExceeded) {
					t.Errorf("%s: Evaluate(%s) at level %v gave %v, not ErrDepthExceeded", schema, instance, level, err)
				}
			}
		}
	}
}
