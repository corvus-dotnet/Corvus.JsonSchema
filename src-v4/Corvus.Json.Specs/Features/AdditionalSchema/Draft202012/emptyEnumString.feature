@draft2020-12

Feature: Empty enum string draft2020-12

Scenario Outline: An enum with an empty string
	Given a schema file
		"""
		{
		    "type": "string",
		    "enum": [
		        "",
		        "emptyString"
		     ]
		}
		"""
	And the input data value <inputData>
	And I generate a type for the schema
	And I construct an instance of the schema type from the data
	When I validate the instance
	Then the result will be <valid>

Examples:
	| inputData     | valid |
	| ""            | true  |
	| "emptyString" | true  |
	| "foo"         | false |
