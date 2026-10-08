package jsonschema

import (
	"strings"
	"testing"
)

// Results collection: the expectations of the C# evaluator's ResultsTests and ResultPathTests, plus worked examples
// traced through the C# collecting path (row order, locations, messages, levels). A port of the Rust crate's
// tests/results.rs.

func mustCompile(t testing.TB, schema string, options ...Option) *Validator {
	t.Helper()
	v, err := CompileString(schema, options...)
	if err != nil {
		t.Fatalf("compile %s: %v", schema, err)
	}
	return v
}

func dumpResults(t testing.TB, v *Validator, instance string, level ResultsLevel) string {
	t.Helper()
	c := NewResultsCollector(level)
	if _, err := v.EvaluateString(instance, c); err != nil {
		t.Fatalf("evaluate %s: %v", instance, err)
	}
	var rows []string
	for _, r := range c.Results() {
		outcome := "fail"
		if r.IsMatch {
			outcome = "match"
		}
		rows = append(rows, strings.Join([]string{
			outcome, r.SchemaEvaluationLocation, r.EvaluationLocation, r.DocumentEvaluationLocation, r.Message,
		}, "|"))
	}
	return strings.Join(rows, "\n")
}

func expectRows(t *testing.T, got string, want ...string) {
	t.Helper()
	if expected := strings.Join(want, "\n"); got != expected {
		t.Errorf("rows:\n%s\nwant:\n%s", got, expected)
	}
}

const personSchema = `{
	"$schema": "https://json-schema.org/draft/2020-12/schema",
	"type": "object",
	"title": "Person",
	"properties": {
		"name": { "type": "string", "minLength": 1, "description": "The name" },
		"age": { "type": "integer", "minimum": 0 }
	},
	"required": ["name"],
	"additionalProperties": false
}`

const refsSchema = `{
	"$schema": "https://json-schema.org/draft/2020-12/schema",
	"$defs": {
		"fooId": { "type": "integer", "minimum": 0 },
		"holder": { "type": "object", "properties": { "fooId": { "$ref": "#/$defs/fooId" } } },
		"viaRef": { "$ref": "#/$defs/fooId" }
	}
}`

const exampleSchema = `{
	"type": "object",
	"properties": { "a": { "type": "string" } },
	"required": ["b"],
	"anyOf": [{ "required": ["a"] }, { "minProperties": 5 }]
}`

func TestFlagAndCollectingEvaluationAgreeAtEveryLevel(t *testing.T) {
	v := mustCompile(t, personSchema)
	for instance, expected := range map[string]bool{
		`{ "name": "a", "age": 3 }`: true, `{ "name": "", "age": 3 }`: false, `{ "age": 3 }`: false,
		`{ "name": "a", "extra": 1 }`: false, `{ "name": "a", "age": -1 }`: false, `[]`: false,
	} {
		if got := v.IsValidString(instance); got != expected {
			t.Errorf("IsValid(%s) = %v", instance, got)
		}
		for _, level := range resultsLevels {
			if got, err := v.EvaluateString(instance, NewResultsCollector(level)); err != nil || got != expected {
				t.Errorf("Evaluate(%s) at %v = %v, %v", instance, level, got, err)
			}
		}
	}
}

func TestBasicResultsReportFailingKeywordsWithLocations(t *testing.T) {
	c := NewResultsCollector(Basic)
	if ok, _ := mustCompile(t, personSchema).EvaluateString(`{ "name": "", "age": -1 }`, c); ok {
		t.Fatal("valid")
	}
	var minLength, minimum, schemaLocation bool
	for _, r := range c.Results() {
		if r.Message != "" {
			t.Errorf("a Basic row has a message: %q", r.Message)
		}
		if r.IsMatch {
			continue
		}
		minLength = minLength || (strings.HasSuffix(r.EvaluationLocation, "/minLength") && r.DocumentEvaluationLocation == "/name")
		minimum = minimum || (strings.HasSuffix(r.EvaluationLocation, "/minimum") && r.DocumentEvaluationLocation == "/age")
		schemaLocation = schemaLocation || r.SchemaEvaluationLocation == "/properties/name"
	}
	if !minLength || !minimum || !schemaLocation {
		t.Errorf("missing rows: minLength %v, minimum %v, schema location %v", minLength, minimum, schemaLocation)
	}
}

func TestVerboseAnnotationsAreProduced(t *testing.T) {
	c := NewResultsCollector(Verbose)
	if ok, _ := mustCompile(t, personSchema).EvaluateString(`{ "name": "a" }`, c); !ok {
		t.Fatal("invalid")
	}
	annotations := c.CollectAnnotations()
	if got := annotations[""]["title"]["#"]; got != `"Person"` || len(annotations[""]["title"]) != 1 {
		t.Errorf("title: %v", annotations[""]["title"])
	}
	if got := annotations["/name"]["description"]["#/properties/name"]; got != `"The name"` {
		t.Errorf("description: %v", annotations["/name"])
	}
}

func TestVerboseOutputFollowsTheCSharpRowOrder(t *testing.T) {
	expectRows(t, dumpResults(t, mustCompile(t, personSchema), `{ "name": "a" }`, Verbose),
		"match|/properties/name|/properties/name|/name|The value was expected to match the subschema.",
		"match|/properties/name|/properties/name/description|/name|\"The name\"",
		"match|/properties/name/minLength|/properties/name/minLength|/name|Expected the length of the value to be greater than or equal to '1'",
		"match|/properties/name/type|/properties/name/type|/name|The value was expected to be of type 'string'",
		"match||||The value was expected to match the subschema.",
		"match||/title||\"Person\"",
		"match|/required|/required|/name|Required property present 'name'",
		"match|/type|/type||The value was expected to be of type 'object'",
	)
}

func TestMatchingKeywordsCarryTheirMessageInVerboseOutput(t *testing.T) {
	v := mustCompile(t, `{
		"$schema": "https://json-schema.org/draft/2020-12/schema",
		"type": ["integer", "array"],
		"uniqueItems": true,
		"properties": { "n": { "type": "integer" } }
	}`)
	rows := func(instance string) (bool, []SchemaResult) {
		c := NewResultsCollector(Verbose)
		valid, _ := v.EvaluateString(instance, c)
		return valid, c.Results()
	}
	has := func(rows []SchemaResult, isMatch bool, location, message string) bool {
		for _, r := range rows {
			if r.IsMatch == isMatch && r.EvaluationLocation == location && r.Message != "" &&
				(message == "" || r.Message == message) {
				return true
			}
		}
		return false
	}
	valid, r := rows(`[1, 2]`)
	if !valid || !has(r, true, "/type", `The value was expected to be of type '["array", "integer"]'`) ||
		!has(r, true, "/uniqueItems", "") {
		t.Errorf("[1, 2]: %v %v", valid, r)
	}
	valid, r = rows(`[1, 1]`)
	if valid || !has(r, false, "/uniqueItems", "") {
		t.Errorf("[1, 1]: %v %v", valid, r)
	}
	c := NewResultsCollector(Verbose)
	if ok, _ := mustCompile(t, `{ "type": "integer" }`).EvaluateString(`3`, c); !ok ||
		!has(c.Results(), true, "/type", "The value was expected to be of type 'integer'") {
		t.Errorf("integer: %v", c.Results())
	}
}

func TestAnEntryPointReportsItsOwnSchemaLocation(t *testing.T) {
	v := mustCompile(t, refsSchema, WithEntryPoint("#/$defs/fooId"))
	expectRows(t, dumpResults(t, v, `"notAnInteger"`, Detailed),
		"fail|/$defs/fooId|||The value was expected to match the subschema.",
		"fail|/$defs/fooId/type|/type||The value was expected to be of type 'integer'",
	)
}

func TestAPureRefPropertyIsElidedWithRefInTheEvaluationPath(t *testing.T) {
	v := mustCompile(t, refsSchema, WithEntryPoint("#/$defs/holder"))
	expectRows(t, dumpResults(t, v, `{ "fooId": "notAnInteger" }`, Detailed),
		"fail|/$defs/fooId|/properties/fooId/$ref|/fooId|The value was expected to match the subschema.",
		"fail|/$defs/fooId/type|/properties/fooId/$ref/type|/fooId|The value was expected to be of type 'integer'",
		"fail|/$defs/holder|||The value was expected to match the subschema.",
	)
}

func TestAPureRefRootReportsAgainstItsTarget(t *testing.T) {
	v := mustCompile(t, refsSchema, WithEntryPoint("#/$defs/viaRef"))
	expectRows(t, dumpResults(t, v, `"notAnInteger"`, Detailed),
		"fail|/$defs/fooId|||The value was expected to match the subschema.",
		"fail|/$defs/fooId/type|/type||The value was expected to be of type 'integer'",
	)
}

func TestARequiredFailureCarriesThePropertyName(t *testing.T) {
	v := mustCompile(t, `{ "type": "object", "required": ["name"] }`)
	expectRows(t, dumpResults(t, v, `{}`, Detailed),
		"fail||||The value was expected to match the subschema.",
		"fail|/required|/required|/name|Required property not present 'name'",
	)
}

func TestDetailedOutputKeepsFailuresOnlyWithMessages(t *testing.T) {
	expected := []string{
		"fail|/properties/a|/properties/a|/a|The value was expected to match the subschema.",
		"fail|/properties/a/type|/properties/a/type|/a|The value was expected to be of type 'string'",
		"fail||||The value was expected to match the subschema.",
		"fail|/required|/required|/b|Required property not present 'b'",
	}
	v := mustCompile(t, exampleSchema)
	expectRows(t, dumpResults(t, v, `{ "a": 1 }`, Detailed), expected...)
	basic := make([]string, len(expected))
	for i, line := range expected {
		basic[i] = line[:strings.LastIndexByte(line, '|')+1]
	}
	expectRows(t, dumpResults(t, v, `{ "a": 1 }`, Basic), basic...)
}

func TestVerboseOutputReversesAContextsOwnRowsAfterItsSummary(t *testing.T) {
	expectRows(t, dumpResults(t, mustCompile(t, exampleSchema), `{ "a": 1 }`, Verbose),
		"fail|/properties/a|/properties/a|/a|The value was expected to match the subschema.",
		"fail|/properties/a/type|/properties/a/type|/a|The value was expected to be of type 'string'",
		"match|/anyOf/0|/anyOf/0||The value was expected to match the subschema.",
		"match|/anyOf/0/required|/anyOf/0/required|/a|Required property present 'a'",
		"fail||||The value was expected to match the subschema.",
		"match|/anyOf|/anyOf||The value matched at least one subschema.",
		"fail|/required|/required|/b|Required property not present 'b'",
		"match|/type|/type||The value was expected to be of type 'object'",
	)
}

func TestNotSubtreesAndBooleanSchemas(t *testing.T) {
	expectRows(t, dumpResults(t, mustCompile(t, `{ "not": { "type": "string" } }`), `"x"`, Detailed),
		"fail||||The value was expected to match the subschema.",
		"fail|/not|/not||The value matched the subschema in a not composition, which means the evaluation was not a match.",
	)
	expectRows(t, dumpResults(t, mustCompile(t, `false`), `1`, Detailed),
		"fail||||The value was expected to match the subschema.", "fail||||")
}

func TestAValidInstanceAtDetailedLevelYieldsOnlyThePassingRootRow(t *testing.T) {
	expectRows(t, dumpResults(t, mustCompile(t, personSchema), `{ "name": "a" }`, Detailed), "match||||")
}

func TestPropertyNamesKeepsTheObjectLocationAndAddsAFailureRowPerName(t *testing.T) {
	v := mustCompile(t, `{ "propertyNames": { "maxLength": 2 } }`)
	expectRows(t, dumpResults(t, v, `{ "abc": 1 }`, Detailed),
		"fail|/propertyNames|/propertyNames||The value was expected to match the subschema.",
		"fail|/propertyNames/maxLength|/propertyNames/maxLength||Expected the length of the value to be less than or equal to '2'",
		"fail||||The value was expected to match the subschema.",
		"fail|/propertyNames|/propertyNames||The property name did not match the schema.",
	)
}

func TestDraft4ExclusiveBoundsReportUnderExclusiveMaximumWithTheMaximum(t *testing.T) {
	v := mustCompile(t, `{ "$schema": "http://json-schema.org/draft-04/schema#", "maximum": 3, "exclusiveMaximum": true }`)
	expectRows(t, dumpResults(t, v, `3`, Detailed),
		"fail||||The value was expected to match the subschema.",
		"fail|/exclusiveMaximum|/exclusiveMaximum||The value was expected to be less than '3'",
	)
}

func TestFailingAnyOfBranchesAreDiscardedAndUnevaluatedPropertiesHasNoMessage(t *testing.T) {
	v := mustCompile(t, `{ "anyOf": [{ "properties": { "a": true } }, { "required": ["zz"] }], "unevaluatedProperties": false }`)
	expectRows(t, dumpResults(t, v, `{ "a": 1, "b": 2 }`, Detailed),
		"fail|/unevaluatedProperties|/unevaluatedProperties|/b|The value was expected to match the subschema.",
		"fail|/unevaluatedProperties|/unevaluatedProperties|/b|",
		"fail||||The value was expected to match the subschema.",
		"fail|/unevaluatedProperties|/unevaluatedProperties||",
	)
}

func TestACollectorAccumulatesAcrossEvaluations(t *testing.T) {
	v := mustCompile(t, personSchema)
	c := NewResultsCollector(Detailed)
	v.EvaluateString(`{ "age": "x" }`, c)
	first := len(c.Results())
	v.EvaluateString(`{ "age": "x" }`, c)
	if first == 0 || len(c.Results()) != 2*first {
		t.Errorf("%d rows, then %d", first, len(c.Results()))
	}
	c.Reset()
	if len(c.Results()) != 0 {
		t.Errorf("%d rows after Reset", len(c.Results()))
	}
}

func TestDependenciesReportsUnderItsOwnNameInEveryDialect(t *testing.T) {
	v := mustCompile(t, `{
		"$schema": "https://json-schema.org/draft/2020-12/schema",
		"dependencies": { "a": ["b"], "c": { "required": ["d"] } }
	}`)
	expectRows(t, dumpResults(t, v, `{ "a": 1, "c": 1 }`, Detailed),
		"fail|/dependencies/c|/dependencies/c||The value was expected to match the subschema.",
		"fail|/dependencies/c/required|/dependencies/c/required|/d|Required property not present 'd'",
		"fail||||The value was expected to match the subschema.",
		"fail|/dependencies|/dependencies|/c|The value did match the schema applied because it contained the property 'c'",
		"fail|/dependencies|/dependencies|/b|Required property not present 'b'",
	)
	modern := mustCompile(t, `{ "dependentRequired": { "a": ["b"] }, "dependentSchemas": { "c": { "required": ["d"] } } }`)
	rows := dumpResults(t, modern, `{ "a": 1, "c": 1 }`, Detailed)
	if !strings.Contains(rows, "fail|/dependentRequired|/dependentRequired|/b|Required property not present 'b'") ||
		!strings.Contains(rows, "fail|/dependentSchemas/c|/dependentSchemas/c||") {
		t.Errorf("rows:\n%s", rows)
	}
}

func TestAStaticallyResolvedDynamicRefHopIsNamedDynamicRefInTheEvaluationPath(t *testing.T) {
	v := mustCompile(t, `{
		"$schema": "https://json-schema.org/draft/2020-12/schema",
		"properties": { "p": { "$dynamicRef": "#/$defs/n" } },
		"$defs": { "n": { "type": "integer" } }
	}`)
	expectRows(t, dumpResults(t, v, `{ "p": "x" }`, Detailed),
		"fail|/$defs/n|/properties/p/$dynamicRef|/p|The value was expected to match the subschema.",
		"fail|/$defs/n/type|/properties/p/$dynamicRef/type|/p|The value was expected to be of type 'integer'",
		"fail||||The value was expected to match the subschema.",
	)
}
