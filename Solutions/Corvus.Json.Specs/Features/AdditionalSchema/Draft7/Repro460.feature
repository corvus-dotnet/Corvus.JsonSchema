@draft7

Feature: Repro 460 draft7

Scenario Outline: Repro Unable to get type declaration
	Given a schema file
		"""
		{
		  "$schema": "http://json-schema.org/draft-07/schema#",
		  "$ref": "#/definitions/Options",
		  "definitions": {
			"Options": {
			  "type": "object",
			  "properties": {
				"plugins": {
				  "type": "array",
				  "items": {
					"type": "array",
					"items": [
					  {
						"type": "string"
					  },
					  {
						"type": "object"
					  }
					]
				  }
				}
			  }
			}
		  },
		  "type": "object",
		  "$id": "https://json.schemastore.org/babelrc.json"
		}
		"""
	And the input data value <inputData>
	And I generate a type for the schema
	And I construct an instance of the schema type from the data
	When I validate the instance
	Then the result will be <valid>

Examples:
	| inputData                          | valid |
	| {"plugins": [["foo", {"bar": 3}]]} | true  |
