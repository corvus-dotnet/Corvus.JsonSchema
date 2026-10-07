package jsonschema_test

import (
	"errors"
	"fmt"

	jsonschema "github.com/corvus-dotnet/Corvus.JsonSchema/src-go/corvus-json-schema"
)

func ExampleCompileString() {
	validator, err := jsonschema.CompileString(`{
		"type": "object",
		"properties": { "id": { "type": "integer", "minimum": 1 } },
		"required": ["id"]
	}`)
	if err != nil {
		fmt.Println(err)
		return
	}
	fmt.Println(validator.IsValidString(`{"id": 3}`))
	fmt.Println(validator.IsValidString(`{"id": 0}`))
	// Output:
	// true
	// false
}

func ExampleValidator_Evaluate() {
	validator, err := jsonschema.CompileString(`{"properties": {"id": {"type": "integer"}}, "required": ["name"]}`)
	if err != nil {
		fmt.Println(err)
		return
	}
	instance, err := jsonschema.ParseDocumentString(`{"id": "seven"}`)
	if err != nil {
		fmt.Println(err)
		return
	}
	collector := jsonschema.NewResultsCollector(jsonschema.Detailed)
	valid, _ := validator.Evaluate(instance, collector)
	fmt.Println(valid)
	for _, r := range collector.Results() {
		if r.EvaluationLocation != "" && r.Message != "" {
			fmt.Printf("%s at %q: %s\n", r.EvaluationLocation, r.DocumentEvaluationLocation, r.Message)
		}
	}
	// Output:
	// false
	// /properties/id at "/id": The value was expected to match the subschema.
	// /properties/id/type at "/id": The value was expected to be of type 'integer'
	// /required at "/name": Required property not present 'name'
}

func ExampleParseDocument() {
	validator, err := jsonschema.CompileString(`{"type": "array", "items": {"type": "integer"}}`)
	if err != nil {
		fmt.Println(err)
		return
	}
	// Parse once, validate any number of times.
	document, err := jsonschema.ParseDocument([]byte(`[1, 2, 3]`))
	if err != nil {
		fmt.Println(err)
		return
	}
	fmt.Println(validator.IsValid(document))

	// JSON text is parsed into buffers the validator reuses.
	fmt.Println(validator.IsValidBytes([]byte(`[1, "two"]`)))
	// Output:
	// true
	// false
}

func ExampleValidator_ValidateString() {
	validator, err := jsonschema.CompileString(`{"type": "object"}`)
	if err != nil {
		fmt.Println(err)
		return
	}
	valid, err := validator.ValidateString(`{"id": 3`)
	fmt.Println(valid, err)

	// IsValidString reports text that is not JSON as invalid.
	fmt.Println(validator.IsValidString(`{"id": 3`))
	// Output:
	// false invalid JSON at offset 8: unexpected end of input
	// false
}

func ExampleCompileString_options() {
	item, err := jsonschema.ParseDocumentString(`{"type": "string", "format": "even"}`)
	if err != nil {
		fmt.Println(err)
		return
	}
	validator, err := jsonschema.CompileString(
		`{"$defs": {"item": {"$ref": "item.json"}}}`,
		jsonschema.WithDefaultDialect(jsonschema.Draft201909), // for schemas without $schema (default 2020-12)
		jsonschema.WithAssertFormat(true),                     // true or false. Without it the vocabularies decide
		jsonschema.WithFormat("even", func(value string) bool { return len(value)%2 == 0 }),
		jsonschema.WithDocumentResolver(func(uri string) *jsonschema.Document {
			if uri == "https://example.com/item.json" {
				return item
			}
			return nil
		}),
		jsonschema.WithBaseURI("https://example.com/root.json"),
		jsonschema.WithEntryPoint("#/$defs/item"),
		jsonschema.WithMaxDepth(128),
	)
	if err != nil {
		fmt.Println(err)
		return
	}
	fmt.Println(validator.IsValidString(`"four"`))
	fmt.Println(validator.IsValidString(`"three"`))
	// Output:
	// true
	// false
}

func ExampleResultsCollector_CollectAnnotations() {
	validator, err := jsonschema.CompileString(`{
		"title": "Person",
		"properties": { "name": { "title": "Name", "type": "string" } }
	}`)
	if err != nil {
		fmt.Println(err)
		return
	}
	collector := jsonschema.NewResultsCollector(jsonschema.Verbose)
	valid, err := validator.EvaluateString(`{"name": "Ada"}`, collector)
	fmt.Println(valid, err)

	// Instance location, then keyword, then schema location, then the value as JSON text.
	annotations := collector.CollectAnnotations()
	fmt.Println(annotations[""]["title"]["#"])
	fmt.Println(annotations["/name"]["title"]["#/properties/name"])
	// Output:
	// true <nil>
	// "Person"
	// "Name"
}

func ExampleCompileString_errors() {
	_, err := jsonschema.CompileString(`{"$ref": "#/$defs/missing"}`)
	var compileError *jsonschema.CompileError
	fmt.Println(errors.As(err, &compileError))

	_, err = jsonschema.CompileString(`{"type": `)
	var parseError *jsonschema.ParseError
	fmt.Println(errors.As(err, &parseError))
	// Output:
	// true
	// true
}
