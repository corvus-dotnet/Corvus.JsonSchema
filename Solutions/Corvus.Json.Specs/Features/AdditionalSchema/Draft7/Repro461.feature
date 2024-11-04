@draft7

Feature: Repro 461 draft7

Scenario Outline: Repro duplicate anyOf schema
	Given a schema file
		"""
		{
		  "$schema": "http://json-schema.org/draft-07/schema#",
		  "additionalProperties": false,
		  "anyOf": [
			{"type": "string"},
			{"type": "string"}
		  ]
		}
		"""
	And the input data value <inputData>
	And I generate a type for the schema
	And I construct an instance of the schema type from the data
	When I validate the instance
	Then the result will be <valid>

Examples:
	| inputData    | valid |
	| "foo"        | true  |
	| { "bar": 3 } | false |
