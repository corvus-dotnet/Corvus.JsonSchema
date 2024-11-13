@draft7

Feature: Repro 463 draft7

Scenario Outline: Repro Stream title
	Given a schema file
		"""
		{
		  "$id": "https://json.schemastore.org/lazygit.json",
		  "$schema": "http://json-schema.org/draft-07/schema#",
		  "additionalProperties": false,
		  "properties": {
			"foo": {
			  "title": "stream"
			}
		  },
		  "type": "object"
		}
		"""
	And the input data value <inputData>
	And I generate a type for the schema
	And I construct an instance of the schema type from the data
	When I validate the instance
	Then the result will be <valid>

Examples:
	| inputData      | valid |
	| { "foo": 123 } | true  |