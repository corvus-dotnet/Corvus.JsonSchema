@draft7

Feature: Repro 464 draft7

Scenario Outline: Repro property collision with definition
	Given a schema file
		"""
		{
		  "$schema": "http://json-schema.org/draft-07/schema",
		  "properties": {
			"builder": {}
		  },
		  "definitions": {
			"builder": {}
		  }
		}
		"""
	And the input data value <inputData>
	And I generate a type for the schema
	And I construct an instance of the schema type from the data
	When I validate the instance
	Then the result will be <valid>

Examples:
	| inputData          | valid |
	| { "builder": 123 } | true  |