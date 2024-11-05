@draft202012

Feature: Repro 469 draft202012

Scenario Outline: Generate single valued single char enum.
	Given a schema file
		"""
		{
		  "$schema": "https://json-schema.org/draft/2020-12/schema",
		  "enum": ["+"]
		}
		"""
	And the input data value <inputData>
	And I generate a type for the schema
	And I construct an instance of the schema type from the data
	When I validate the instance with level Detailed
	Then the result will be <valid>

Examples:
	| inputData | valid |
	| "-"       | false |
	| "+"       | true  |

Scenario Outline: Generate single valued multi char enum.
	Given a schema file
		"""
		{
		  "$schema": "https://json-schema.org/draft/2020-12/schema",
		  "enum": ["++"]
		}
		"""
	And the input data value <inputData>
	And I generate a type for the schema
	And I construct an instance of the schema type from the data
	When I validate the instance with level Detailed
	Then the result will be <valid>

Examples:
	| inputData | valid |
	| "-"       | false |
	| "++"      | true  |

Scenario Outline: Generate multi valued multi char enum.
	Given a schema file
		"""
		{
		  "$schema": "https://json-schema.org/draft/2020-12/schema",
		  "enum": ["++", "--"]
		}
		"""
	And the input data value <inputData>
	And I generate a type for the schema
	And I construct an instance of the schema type from the data
	When I validate the instance with level Detailed
	Then the result will be <valid>

Examples:
	| inputData | valid |
	| ";;"      | false |
	| "--"      | true  |
	| "++"      | true  |