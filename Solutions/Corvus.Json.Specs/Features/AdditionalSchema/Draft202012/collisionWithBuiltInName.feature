@draft2020-12

Feature: Collision with a built-in name draft2020-12

Scenario Outline: A property that matches a built-in name
	Given a schema file
		"""
		{
		    "type": "object",
		    "properties": {
				"match": { "$ref": "#/$defs/SomeMatch" }
			},
			"$defs": {
				"SomeMatch": {"type": "string", "minLength": 2}
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
	| {"match": "foo" } | true  |
	| {"match": "f" }   | false |


Scenario Outline: Repro 625 - collides with System
	Given a schema file
		"""
		{
		  "type": "object",
		  "properties": {
			"system": {
			  "type": "string",
			  "minLength": 0
			},
			"someOtherProp": {
			  "type": "string",
			  "minLength": 0
			}
		  },
		  "required": ["system", "someOtherProp"]
		}
		"""
	And the input data value <inputData>
	And I generate a type for the schema
	And I construct an instance of the schema type from the data
	When I validate the instance
	Then the result will be <valid>

Examples:
	| inputData                                  | valid |
	| {"system": "foo", "someOtherProp": "bar" } | true  |
	| {"someOtherProp": "bar"  }                 | false |
