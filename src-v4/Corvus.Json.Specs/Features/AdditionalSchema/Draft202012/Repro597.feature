@draft202012
@

Feature: Repro 597 draft202012

Scenario Outline: Validate date-time with and without minutes on the offset
	Given a schema file
		"""
		{
		  "$schema": "https://json-schema.org/draft/2020-12/schema",
		  "type": "string",
		  "format": "date-time"
		}
		"""
	And the input data value <inputData>
	And I assert format
	And I generate a type for the schema
	And I construct an instance of the schema type from the data
	When I validate the instance with level Detailed
	Then the result will be <valid>

Examples:
	| inputData                   | valid |
	| "2025-04-09T12:00:00+01"    | true  |
	| "2025-04-09T12:00:00+01:00" | true  |

Scenario Outline: Validate time with and without minutes on the offset
	Given a schema file
		"""
		{
		  "$schema": "https://json-schema.org/draft/2020-12/schema",
		  "type": "string",
		  "format": "time"
		}
		"""
	And the input data value <inputData>
	And I assert format
	And I generate a type for the schema
	And I construct an instance of the schema type from the data
	When I validate the instance with level Detailed
	Then the result will be <valid>

Examples:
	| inputData                   | valid |
	| "12:00:00+01"    | true  |
	| "12:00:00+01:00" | true  |