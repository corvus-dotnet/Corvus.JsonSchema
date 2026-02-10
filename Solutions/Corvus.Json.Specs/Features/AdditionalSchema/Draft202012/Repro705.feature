@draft202012

Feature: Repro 705 draft202012

Scenario Outline: Defaulted required property 
	Given a schema file
		"""
		{
			"type": "object",
			"properties": {
				"regularProperty": { "type": "string" },
				"propertyWithDefault": { "$ref": "#/$defs/defaultedValue" },
				"unsetProperty": { "type": "string" }
			},
			"required": ["regularProperty", "propertyWithDefault"],
			"$defs": {
				"defaultedValue": {
					"type": "string",
					"default": "Hello"
				}
			}
		}
		"""
	And the input data value <inputData>
	And I generate a type for the schema
	And I construct an instance of the schema type from the data
	When I validate the instance with level Detailed
	Then the result will be <valid>

Examples:
	| inputData                                                | valid |
	| {"regularProperty": "foo"}                               | false |
	| {"regularProperty": "foo", "propertyWithDefault": "bar"} | true  |