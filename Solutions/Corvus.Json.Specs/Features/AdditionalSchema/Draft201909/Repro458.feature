@draft201909

Feature: Empty Required draft2019-09

Scenario Outline: An empty required array
	Given a schema file
		"""
		{
		  "$schema": "https://json-schema.org/draft/2019-09/schema",
		  "type": "object",
		  "properties": {
			"flag": {
			  "type": "boolean"
			},
			"bar": {
			  "type": "number"
			}
		  },
		  "if": {
			"properties": {
			  "flag": {
				"const": true
			  }
			}
		  },
		  "then": {
			"required": []
		  },
		  "else": {
			"required": [
			  "bar"
			]
		  },
		  "required": [
			"flag"
		  ]
		}
		"""
	And the input data value <inputData>
	And I generate a type for the schema
	And I construct an instance of the schema type from the data
	When I validate the instance
	Then the result will be <valid>

Examples:
	| inputData                 | valid |
	| {"flag": false, "bar": 3} | true  |
	| {"flag": true}            | true  |
	| {"flag": false}           | false |
