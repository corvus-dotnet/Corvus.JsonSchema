package jsonschema

import (
	"fmt"
	"strings"
	"testing"
)

// Validation allocates nothing in the steady state: the evaluator's buffers are pooled by the validator, JSON text is
// parsed into reused buffers, and the document is evaluated where it was parsed.
//
// Four asserted formats are outside this: regex (the pattern is parsed), idn-hostname and idn-email (the labels are
// decoded), and hostname for a label that starts with "xn--". A custom format receives a copy of the string.

type allocationCase struct {
	name      string
	schema    string
	options   []Option
	instances []string
}

func allocationCases() []allocationCase {
	var large []string
	for i := 0; i < 300; i++ {
		large = append(large, fmt.Sprintf(`"p%d": %d`, i, i))
	}
	var unique []string
	for i := 0; i < 100; i++ {
		unique = append(unique, fmt.Sprintf(`{"id": %d, "name": "n%d"}`, i, i))
	}
	return []allocationCase{
		{
			name: "objects and arrays",
			schema: `{
				"type": "object",
				"properties": { "name": { "type": "string", "minLength": 1 }, "tags": { "type": "array", "items": { "type": "string" } } },
				"required": ["name"]
			}`,
			instances: []string{
				`{"name": "a", "tags": ["x", "y\nz"]}`,
				`{"name": "", "tags": []}`,
				`{"name": "café", "tags": ["1", "2", "3", "4", "5", "6", "7", "8"], "other": {"deep": [1, [2, [3]]]}}`,
			},
		},
		{
			name: "keywords of every kind",
			schema: `{
				"type": "object",
				"properties": {
					"n": { "type": "number", "minimum": 0, "exclusiveMaximum": 100, "multipleOf": 0.25 },
					"s": { "type": "string", "maxLength": 5, "pattern": "^[a-z]+$" },
					"e": { "enum": ["a", "b", 1, null] },
					"c": { "const": { "k": [1, 2] } },
					"u": { "type": "array", "uniqueItems": true, "contains": { "type": "integer" }, "minContains": 1 },
					"o": { "oneOf": [{ "type": "string" }, { "type": "integer" }, { "required": ["x"] }] },
					"a": { "anyOf": [{ "minimum": 5 }, { "maxLength": 2 }] },
					"i": { "if": { "type": "integer" }, "then": { "minimum": 1 }, "else": { "type": "string" } },
					"x": { "not": { "type": "null" } }
				},
				"patternProperties": { "^x-": { "type": "boolean" } },
				"additionalProperties": { "type": "integer" },
				"propertyNames": { "maxLength": 10 },
				"dependentRequired": { "n": ["s"] },
				"minProperties": 1
			}`,
			instances: []string{
				`{"n": 1.5, "s": "abc", "e": "b", "c": {"k": [1, 2.0]}, "u": [1, "a", [2], {"b": 1}], "o": 3, "a": "ab", "i": 2, "x": 0, "x-flag": true, "extra": 7}`,
				`{"n": 1.3, "s": "abc"}`,
				`{"u": [` + strings.Join(unique, ",") + `, 5]}`,
				`{"u": [` + strings.Join(unique, ",") + `, {"name": "n3", "id": 3}]}`,
				`{"o": {"x": 1}, "a": 3, "i": "s", "x": null}`,
				`{"a-name-that-is-too-long": 1}`,
			},
		},
		{
			name: "unevaluated properties and items",
			schema: `{
				"$defs": { "base": { "properties": { "a": { "type": "integer" } }, "patternProperties": { "^x-": true } } },
				"allOf": [{ "$ref": "#/$defs/base" }],
				"anyOf": [{ "properties": { "b": true }, "required": ["b"] }, { "properties": { "c": true } }],
				"oneOf": [{ "properties": { "d": { "type": "string" } } }, { "properties": { "d": { "type": "integer" } } }],
				"properties": { "list": { "prefixItems": [true], "contains": { "type": "string" }, "unevaluatedItems": false } },
				"unevaluatedProperties": false
			}`,
			instances: []string{
				`{"a": 1, "b": 2, "d": "s", "x-y": null, "list": [1, "a", "b"]}`,
				`{"a": 1, "c": 2, "d": 3, "list": [1, "a", 2]}`,
				`{"a": 1, "e": 2}`,
				`{` + strings.Join(large, ",") + `}`,
			},
		},
		{
			name: "fused objects with conditions",
			schema: `{
				"type": "object",
				"properties": { "kind": { "enum": ["a", "b"] }, "value": true, "other": { "type": "string" } },
				"allOf": [{ "$ref": "#/$defs/ext" }, { "properties": { "c": { "type": "integer" } } }],
				"if": { "properties": { "kind": { "const": "a" } }, "required": ["kind"] },
				"then": { "required": ["value"], "properties": { "extra": { "type": "integer" } } },
				"else": { "not": { "required": ["value", "other"] } },
				"dependentSchemas": { "c": { "properties": { "d": true } } },
				"unevaluatedProperties": false,
				"$defs": { "ext": { "patternProperties": { "^x-": true } } }
			}`,
			instances: []string{
				`{"kind": "a", "value": 1, "extra": 2, "x-a": 1}`,
				`{"kind": "b", "value": 1, "c": 3, "d": 4}`,
				`{"kind": "b", "value": 1, "other": "x"}`,
				`{"kind": "a"}`,
				`{"kind": "a", "value": 1, ` + strings.Join(large, ",") + `}`,
			},
		},
		{
			name: "a dynamic scope",
			schema: `{
				"$schema": "https://json-schema.org/draft/2020-12/schema",
				"$id": "https://example.com/strict-tree",
				"$dynamicAnchor": "node",
				"$ref": "tree",
				"unevaluatedProperties": false,
				"$defs": {
					"tree": {
						"$id": "https://example.com/tree",
						"$dynamicAnchor": "node",
						"type": "object",
						"properties": { "data": true, "children": { "type": "array", "items": { "$dynamicRef": "#node" } } }
					}
				}
			}`,
			instances: []string{
				`{"children": [{"data": 1, "children": [{"data": [1, 2, 3]}]}]}`,
				`{"children": [{"daat": 1}]}`,
			},
		},
		{
			name:    "asserted formats and content",
			options: []Option{WithDefaultDialect(Draft7), WithAssertFormat(true)},
			schema: `{
				"properties": {
					"date": { "format": "date-time" }, "ip": { "format": "ipv6" }, "host": { "format": "hostname" },
					"id": { "format": "uuid" }, "n": { "format": "int32" }, "pointer": { "format": "json-pointer" },
					"uri": { "format": "uri" }, "ref": { "format": "uri-reference" }, "iri": { "format": "iri" },
					"template": { "format": "uri-template" }, "email": { "format": "email" },
					"duration": { "format": "duration" }, "time": { "format": "time" }, "v4": { "format": "ipv4" },
					"json": { "contentMediaType": "application/json", "contentEncoding": "base64" }
				}
			}`,
			instances: []string{
				`{"date": "2020-01-02T03:04:05.678Z", "ip": "::ffff:192.168.0.1", "host": "example.com", "id": "2eb8aa08-aa98-11ea-b4aa-73b441d16380", "n": 12, "pointer": "/a/~0b", "json": "eyJhIjogWzEsIDIsIDNdfQ=="}`,
				`{"uri": "http://example.com/a/b?c=d#e", "ref": "../a/b?c#d", "iri": "http://\u00e9xample.com/\u00fc", "template": "http://example.com/{id}/x{?q,r}", "email": "joe.bloggs@example.com", "duration": "P4DT12H30M5S", "time": "08:30:06.283185+01:00", "v4": "1.2.3.4"}`,
				`{"n": 1e30}`,
				`{"json": "bm90IGpzb24="}`,
			},
		},
	}
}

func TestValidationAllocatesNothingInTheSteadyState(t *testing.T) {
	if raceEnabled {
		t.Skip("the race detector makes sync.Pool drop values")
	}
	for _, c := range allocationCases() {
		v := mustCompile(t, c.schema, c.options...)
		documents := make([]*Document, len(c.instances))
		texts := make([][]byte, len(c.instances))
		for i, instance := range c.instances {
			documents[i] = mustParse(t, instance)
			texts[i] = []byte(instance)
		}
		// The first validations size the buffers.
		for i := range c.instances {
			v.IsValid(documents[i])
			v.IsValidBytes(texts[i])
		}
		sink := false
		check := func(what string, run func()) {
			t.Helper()
			if allocations := testing.AllocsPerRun(100, run); allocations != 0 {
				t.Errorf("%s: %v allocations for each pass over %d %s", c.name, allocations, len(c.instances), what)
			}
		}
		check("documents", func() {
			for _, d := range documents {
				sink = v.IsValid(d) != sink
			}
		})
		check("documents (Validate)", func() {
			for _, d := range documents {
				ok, _ := v.Validate(d)
				sink = ok != sink
			}
		})
		check("texts as bytes", func() {
			for _, b := range texts {
				sink = v.IsValidBytes(b) != sink
			}
		})
		check("texts as bytes (ValidateBytes)", func() {
			for _, b := range texts {
				ok, _ := v.ValidateBytes(b)
				sink = ok != sink
			}
		})
		check("texts as strings", func() {
			for _, s := range c.instances {
				sink = v.IsValidString(s) != sink
			}
		})
		check("texts as strings (ValidateString)", func() {
			for _, s := range c.instances {
				ok, _ := v.ValidateString(s)
				sink = ok != sink
			}
		})
		_ = sink
	}
}

func TestTextThatIsNotJSONAllocatesNothingForIsValid(t *testing.T) {
	if raceEnabled {
		t.Skip("the race detector makes sync.Pool drop values")
	}
	v := mustCompile(t, `{"type": "object"}`)
	text := []byte(`{"a": [1, 2, {"b": "c"}`)
	v.IsValidBytes(text)
	if allocations := testing.AllocsPerRun(100, func() { v.IsValidBytes(text) }); allocations != 0 {
		t.Errorf("%v allocations for text that is not JSON", allocations)
	}
}
