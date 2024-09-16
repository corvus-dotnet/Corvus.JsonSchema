@draft6

Feature: derived numeric type conversion draft6 net8
    In order to use json-schema
    As a developer
    I want to support numeric formats in draft6

# Note that in draft6 and draft7, $ref ignores the adjacent validation, so this *does not work as you would expect*. The derived type is simply the base type without additional constraints.

Scenario Outline: type derived by reference supports implicit conversions
	Given a schema file with format <format>
		"""
		{
		    "$ref": "#/$defs/base",
		    "maximum": 64,
		    "$defs": {
		        "base": {
		            "type": "number",
		            "format": "{{format}}",
		            "exclusiveMinimum": 0
		        }
		    }
		}
		"""
	And I assert format
	And I generate a type for the schema
	And I create the instance by casting the <format> <inputData>
	When I validate the instance
	Then the result will be <valid>
Examples:
	| format  | inputData | valid |
	| int128  | 0         | false |
	| int128  | 1         | true  |
	| int128  | 65        | true  |
	| uint128 | 0         | false |
	| uint128 | 1         | true  |
	| uint128 | 65        | true  |
	| half    | 0         | false |
	| half    | 1.1       | true  |
	| half    | 64.1      | true  |
