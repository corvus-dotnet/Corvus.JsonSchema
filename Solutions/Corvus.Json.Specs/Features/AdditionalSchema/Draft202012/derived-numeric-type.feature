@draft2020-12

Feature: derived numeric type conversion draft2020-12
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
	| sbyte   | 0         | false |
	| sbyte   | 1         | true  |
	| sbyte   | 65        | false |
	| int16   | 0         | false |
	| int16   | 1         | true  |
	| int16   | 65        | false |
	| int32   | 0         | false |
	| int32   | 1         | true  |
	| int32   | 65        | false |
	| int64   | 0         | false |
	| int64   | 1         | true  |
	| int64   | 65        | false |
	| byte    | 0         | false |
	| byte    | 1         | true  |
	| byte    | 65        | false |
	| uint16  | 0         | false |
	| uint16  | 1         | true  |
	| uint16  | 65        | false |
	| uint32  | 0         | false |
	| uint32  | 1         | true  |
	| uint32  | 65        | false |
	| uint64  | 0         | false |
	| uint64  | 1         | true  |
	| uint64  | 65        | false |
	| double  | 0         | false |
	| double  | 1.1       | true  |
	| double  | 64.1      | false |
	| decimal | 0         | false |
	| decimal | 1.1       | true  |
	| decimal | 64.1      | false |
	| single  | 0         | false |
	| single  | 1.1       | true  |
	| single  | 64.1      | false |
