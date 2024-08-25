@draft2020-12

Feature: synchronous code generation draft2020-12
    In order to use json-schema
    As a developer
    I want to support synchronous code generation with pre-loaded files in draft2020-12

Scenario Outline: synchronous type generation
	Given a schema file
		"""
		{
			"type": "string"
		}
		"""
	And the input data value <inputData>
	And I synchronously generate a type for the schema
	And I construct an instance of the schema type from the data
	When I validate the instance
	Then the result will be <valid>

Examples:
	| inputData | valid |
	| "hello"   | true  |
	| 33        | false |
