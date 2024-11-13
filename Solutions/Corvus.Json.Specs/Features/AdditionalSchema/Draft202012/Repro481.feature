@draft202012

Feature: Repro 481 draft202012

Scenario Outline: Mixed-case email.
	Given a schema file
		"""
		{
			"$schema": "https://json-schema.org/draft/2020-12/schema",
			"format": "email"
		}
		"""
	And I assert format
	And the input data value <inputData>
	And I generate a type for the schema
	And I construct an instance of the schema type from the data
	When I validate the instance
	Then the result will be <valid>

Examples:
	| inputData           | valid |
	| "Endjin@corvus.net" | true  |
