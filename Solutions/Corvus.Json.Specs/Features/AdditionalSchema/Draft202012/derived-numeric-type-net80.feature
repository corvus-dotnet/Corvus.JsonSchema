@draft2020-12

Feature: derived numeric type conversion draft2020-12 net8
    In order to use json-schema
    As a developer
    I want to support numeric formats in draft2020-12

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
	And I generate a type for the schema
	And I create the instance by casting the <format> <inputData>
	When I validate the instance
	Then the result will be <valid>
Examples:
	| format  | inputData | valid |
	| int128  | 0         | false |
	| int128  | 1         | true  |
	| int128  | 65        | false |
	| uint128 | 0         | false |
	| uint128 | 1         | true  |
	| uint128 | 65        | false |
	| half    | 0         | false |
	| half    | 1.1       | true  |
	| half    | 64.1      | false |
