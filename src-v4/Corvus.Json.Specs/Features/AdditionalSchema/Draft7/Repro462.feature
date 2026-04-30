@draft7

Feature: Repro 462 draft7

Scenario Outline: Repro tilde description
	Given a schema file
		"""
		{
		  "$schema": "http://json-schema.org/draft-07/schema#",
		  "properties": {
			"dry": {
			  "description": "~"
			}
		  },
		  "type": "object",
		  "$id": "https://json.schemastore.org/jsconfig.json"
		}
		"""
	And the input data value <inputData>
	And I generate a type for the schema
	And I construct an instance of the schema type from the data
	When I validate the instance
	Then the result will be <valid>

Examples:
	| inputData      | valid |
	| { "dry": 123 } | true  |