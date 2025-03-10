@draft6

Feature: fized size numeric arrays draft6
    In order to use json-schema
    As a developer
    I want to support fixed sized numeric arrays in draft6

Scenario Outline: fixed size array can be converted to and from spans of primitive type
	Given a schema file with format <format>
		"""
		{
			"title": "A 4x3x2 tensor of {{format}}",
			"type": "array",
			"items": { "$ref": "#/$defs/SecondRank" },
			"minItems": 4,
			"maxItems": 4,
			"$defs": {
				"SecondRank": {
					"type": "array",
					"items": { "$ref": "#/$defs/ThirdRank" },
					"minItems": 3,
					"maxItems": 3
				},
				"ThirdRank": {
					"type": "array",
					"minItems": 2,
					"maxItems": 2,
					"items": {
						"type": "number",
						"format": "{{format}}"
					}
				}
			}
		}
		"""
	And I assert format
	And I generate a type for the schema
	When I create the instance using FromValues with format '<format>' and input data '<inputData>'
	Then the result <willWillNot> throw an exception
	And if an exception was not thrown then TryGetValues with format '<format>' will equal the input data '<inputData>'
Examples:
	| format  | inputData                                                        | willWillNot |
	| sbyte   | 0,1,2,3,4,5,6,7,8,9,10,11,12,13,14,15,16,17,18,19,20,21,22,23    | will not    |
	| sbyte   | 0,1,2,3,4,5,6,7,8,9,10,11,12,13,14,15,16,17,18,19,20,21,22       | will        |
	| sbyte   | 0,1,2,3,4,5,6,7,8,9,10,11,12,13,14,15,16,17,18,19,20,21,22,23,24 | will        |
	| int16   | 0,1,2,3,4,5,6,7,8,9,10,11,12,13,14,15,16,17,18,19,20,21,22,23    | will not    |
	| int16   | 0,1,2,3,4,5,6,7,8,9,10,11,12,13,14,15,16,17,18,19,20,21,22       | will        |
	| int16   | 0,1,2,3,4,5,6,7,8,9,10,11,12,13,14,15,16,17,18,19,20,21,22,23,24 | will        |
	| int32   | 0,1,2,3,4,5,6,7,8,9,10,11,12,13,14,15,16,17,18,19,20,21,22,23    | will not    |
	| int32   | 0,1,2,3,4,5,6,7,8,9,10,11,12,13,14,15,16,17,18,19,20,21,22       | will        |
	| int32   | 0,1,2,3,4,5,6,7,8,9,10,11,12,13,14,15,16,17,18,19,20,21,22,23,24 | will        |
	| int64   | 0,1,2,3,4,5,6,7,8,9,10,11,12,13,14,15,16,17,18,19,20,21,22,23    | will not    |
	| int64   | 0,1,2,3,4,5,6,7,8,9,10,11,12,13,14,15,16,17,18,19,20,21,22       | will        |
	| int64   | 0,1,2,3,4,5,6,7,8,9,10,11,12,13,14,15,16,17,18,19,20,21,22,23,24 | will        |
	| byte    | 0,1,2,3,4,5,6,7,8,9,10,11,12,13,14,15,16,17,18,19,20,21,22,23    | will not    |
	| byte    | 0,1,2,3,4,5,6,7,8,9,10,11,12,13,14,15,16,17,18,19,20,21,22       | will        |
	| byte    | 0,1,2,3,4,5,6,7,8,9,10,11,12,13,14,15,16,17,18,19,20,21,22,23,24 | will        |
	| uint16  | 0,1,2,3,4,5,6,7,8,9,10,11,12,13,14,15,16,17,18,19,20,21,22,23    | will not    |
	| uint16  | 0,1,2,3,4,5,6,7,8,9,10,11,12,13,14,15,16,17,18,19,20,21,22       | will        |
	| uint16  | 0,1,2,3,4,5,6,7,8,9,10,11,12,13,14,15,16,17,18,19,20,21,22,23,24 | will        |
	| uint32  | 0,1,2,3,4,5,6,7,8,9,10,11,12,13,14,15,16,17,18,19,20,21,22,23    | will not    |
	| uint32  | 0,1,2,3,4,5,6,7,8,9,10,11,12,13,14,15,16,17,18,19,20,21,22       | will        |
	| uint32  | 0,1,2,3,4,5,6,7,8,9,10,11,12,13,14,15,16,17,18,19,20,21,22,23,24 | will        |
	| uint64  | 0,1,2,3,4,5,6,7,8,9,10,11,12,13,14,15,16,17,18,19,20,21,22,23    | will not    |
	| uint64  | 0,1,2,3,4,5,6,7,8,9,10,11,12,13,14,15,16,17,18,19,20,21,22       | will        |
	| uint64  | 0,1,2,3,4,5,6,7,8,9,10,11,12,13,14,15,16,17,18,19,20,21,22,23,24 | will        |
	| double  | 0,1,2,3,4,5,6,7,8,9,10,11,12,13,14,15,16,17,18,19,20,21,22,23    | will not    |
	| double  | 0,1,2,3,4,5,6,7,8,9,10,11,12,13,14,15,16,17,18,19,20,21,22       | will        |
	| double  | 0,1,2,3,4,5,6,7,8,9,10,11,12,13,14,15,16,17,18,19,20,21,22,23,24 | will        |
	| decimal | 0,1,2,3,4,5,6,7,8,9,10,11,12,13,14,15,16,17,18,19,20,21,22,23    | will not    |
	| decimal | 0,1,2,3,4,5,6,7,8,9,10,11,12,13,14,15,16,17,18,19,20,21,22       | will        |
	| decimal | 0,1,2,3,4,5,6,7,8,9,10,11,12,13,14,15,16,17,18,19,20,21,22,23,24 | will        |
	| single  | 0,1,2,3,4,5,6,7,8,9,10,11,12,13,14,15,16,17,18,19,20,21,22,23    | will not    |
	| single  | 0,1,2,3,4,5,6,7,8,9,10,11,12,13,14,15,16,17,18,19,20,21,22       | will        |
	| single  | 0,1,2,3,4,5,6,7,8,9,10,11,12,13,14,15,16,17,18,19,20,21,22,23,24 | will        |
