@draft202012

Feature: Repro 679 draft202012

Scenario: Defaulted property
	Given a schema file
		"""
		{
			"type": "object",
			"properties": {
				"regularProperty": { "type": "string" },
				"propertyWithDefault": { "$ref": "#/$defs/defaultedValue" },
				"unsetProperty": { "type": "string" }
			},
			"$defs": {
				"defaultedValue": {
					"type": "string",
					"default": "Hello"
				}
			}
		}
		"""
	And the input data value {"regularProperty": "Goodbye"}
	And I generate a type for the schema
	And I construct an instance of the schema type from the data
	Then the property 'PropertyWithDefault' from the instance has the value 'Hello'
	And the property 'RegularProperty' from the instance has the value 'Goodbye'
	And the property 'UnsetProperty' from the instance is undefined