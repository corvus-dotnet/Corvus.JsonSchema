package jsonschema_test

import (
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
