@draft7

Feature: derived numeric type conversion draft7
    In order to use json-schema
    As a developer
    I want to support numeric formats in draft7

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
	| sbyte   | 0         | false |
	| sbyte   | 1         | true  |
	| sbyte   | 65        | true  |
	| int16   | 0         | false |
	| int16   | 1         | true  |
	| int16   | 65        | true  |
	| int32   | 0         | false |
	| int32   | 1         | true  |
	| int32   | 65        | true  |
	| int64   | 0         | false |
	| int64   | 1         | true  |
	| int64   | 65        | true  |
	| byte    | 0         | false |
	| byte    | 1         | true  |
	| byte    | 65        | true  |
	| uint16  | 0         | false |
	| uint16  | 1         | true  |
	| uint16  | 65        | true  |
	| uint32  | 0         | false |
	| uint32  | 1         | true  |
	| uint32  | 65        | true  |
	| uint64  | 0         | false |
	| uint64  | 1         | true  |
	| uint64  | 65        | true  |
	| double  | 0         | false |
	| double  | 1.1       | true  |
	| double  | 64.1      | true  |
	| decimal | 0         | false |
	| decimal | 1.1       | true  |
	| decimal | 64.1      | true  |
	| single  | 0         | false |
	| single  | 1.1       | true  |
	| single  | 64.1      | true  |
