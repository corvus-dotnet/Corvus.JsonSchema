@draft7

Feature: Repro 465 draft7

Scenario Outline: Repro similar definition names
	Given a schema file
		"""
		{
		  "$schema": "http://json-schema.org/draft-07/schema#",
		  "type": "object",
		  "properties": {
			"id": { "$ref": "#/definitions/id_def" },
			"id2": { "$ref": "#/definitions/iddef" }
		  },
		  "definitions": {
			"id_def": {
			  "type": "string",
			  "maxLength": 70
			},
			"iddef": {
			  "type": "string",
			  "maxLength": 70
			}
		  }
		}
		"""
	And the input data value <inputData>
	And I generate a type for the schema
	And I construct an instance of the schema type from the data
	When I validate the instance
	Then the result will be <valid>

Examples:
	| inputData                     | valid |
	| { "id": "foo", "id2": "bar" } | true  |