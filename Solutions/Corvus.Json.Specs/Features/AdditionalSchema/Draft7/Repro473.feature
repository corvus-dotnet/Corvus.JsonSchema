@draft7

Feature: Repro 473 draft7

Scenario Outline: Generation error.
	Given a schema file
		"""
		{
		  "$id": "#Configuration",
		  "$schema": "http://json-schema.org/draft-07/schema#",
		  "properties": {
			"language": {"type": "string"}
		  }
		}
		"""
	And the input data value <inputData>
	And I generate a type for the schema
	And I construct an instance of the schema type from the data
	When I validate the instance
	Then the result will be <valid>

Examples:
	| inputData         | valid |
	| {"language":"en"} | true  |