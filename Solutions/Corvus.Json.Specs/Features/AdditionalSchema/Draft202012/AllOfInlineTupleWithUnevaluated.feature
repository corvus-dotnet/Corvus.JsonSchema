@draft202012

Feature: AllOf Inline Tuple With Unevaluated draft202012

Scenario Outline: An inline tuple with unevaluated properties composed as with allOf 
	Given a schema file
		"""
        {
          "$schema": "https://json-schema.org/draft/2020-12/schema",
          "title": "AllOfInlineTupleWithUnevaluated",
          "description": "An inline tuple within allOf with unevaluatedItems at the top level.",
          "allOf": [
            {
              "prefixItems": [
                { "type": "string" },
                { "type": "number" }
              ]
            }
          ],
          "unevaluatedItems": { "type": "boolean" }
        }
		"""
	And the input data value <inputData>
	And I generate a type for the schema
	And I construct an instance of the schema type from the data
	When I validate the instance with level Detailed
	Then the result will be <valid>

Examples:
	| inputData          | valid |
	| ["foo", 32]        | true  |
	| ["foo", 32, true ] | true  |
	| ["foo", 32, 33 ]   | false |