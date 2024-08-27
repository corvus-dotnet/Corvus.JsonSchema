@draft2019-09

Feature: Validate with different flag levels draft2019-09

Scenario Outline: All of the format types
	Given a schema file
		"""
		{
			"oneOf": [
				{ "$ref": "#/$defs/CoreJsonAny" },
				{ "$ref": "#/$defs/CoreJsonNotAny" },
				{ "$ref": "#/$defs/CoreJsonArray" },
				{ "$ref": "#/$defs/CoreJsonBoolean" },
				{ "$ref": "#/$defs/CoreJsonNull" },
				{ "$ref": "#/$defs/CoreJsonObject" },
				{ "$ref": "#/$defs/CoreJsonString" },
				{ "$ref": "#/$defs/CoreJsonBase64Content" },
				{ "$ref": "#/$defs/CoreJsonBase64String" },
				{ "$ref": "#/$defs/CoreJsonContent" },
				{ "$ref": "#/$defs/CoreJsonDateTime" },
				{ "$ref": "#/$defs/CoreJsonDate" },
				{ "$ref": "#/$defs/CoreJsonDuration" },
				{ "$ref": "#/$defs/CoreJsonTime" },
				{ "$ref": "#/$defs/CoreJsonEmail" },
				{ "$ref": "#/$defs/CoreJsonHostname" },
				{ "$ref": "#/$defs/CoreJsonIdnEmail" },
				{ "$ref": "#/$defs/CoreJsonIpV4" },
				{ "$ref": "#/$defs/CoreJsonIpV6" },
				{ "$ref": "#/$defs/CoreJsonIri" },
				{ "$ref": "#/$defs/CoreJsonIriReference" },
				{ "$ref": "#/$defs/CoreJsonPointer" },
				{ "$ref": "#/$defs/CoreJsonRegex" },
				{ "$ref": "#/$defs/CoreJsonRelativePointer" },
				{ "$ref": "#/$defs/CoreJsonUri" },
				{ "$ref": "#/$defs/CoreJsonUriReference" },
				{ "$ref": "#/$defs/CoreJsonUriTemplate" },
				{ "$ref": "#/$defs/CoreJsonUuid" },
				{ "$ref": "#/$defs/CoreJsonNumber" },
				{ "$ref": "#/$defs/CoreJsonInteger" },
				{ "$ref": "#/$defs/CoreJsonHalf" },
				{ "$ref": "#/$defs/CoreJsonSingle" },
				{ "$ref": "#/$defs/CoreJsonDouble" },
				{ "$ref": "#/$defs/CoreJsonDecimal" },
				{ "$ref": "#/$defs/CoreJsonSByte" },
				{ "$ref": "#/$defs/CoreJsonInt16" },
				{ "$ref": "#/$defs/CoreJsonInt32" },
				{ "$ref": "#/$defs/CoreJsonInt64" },
				{ "$ref": "#/$defs/CoreJsonInt128" },
				{ "$ref": "#/$defs/CoreJsonByte" },
				{ "$ref": "#/$defs/CoreJsonUInt16" },
				{ "$ref": "#/$defs/CoreJsonUInt32" },
				{ "$ref": "#/$defs/CoreJsonUInt64" },
				{ "$ref": "#/$defs/CoreJsonUInt128" },
				{ "$ref": "#/$defs/ExtJsonArray" },
				{ "$ref": "#/$defs/ExtJsonObject" },
				{ "$ref": "#/$defs/ExtJsonString" },
				{ "$ref": "#/$defs/ExtJsonDateTime" },
				{ "$ref": "#/$defs/ExtJsonDate" },
				{ "$ref": "#/$defs/ExtJsonDuration" },
				{ "$ref": "#/$defs/ExtJsonTime" },
				{ "$ref": "#/$defs/ExtJsonEmail" },
				{ "$ref": "#/$defs/ExtJsonHostname" },
				{ "$ref": "#/$defs/ExtJsonIdnEmail" },
				{ "$ref": "#/$defs/ExtJsonIpV4" },
				{ "$ref": "#/$defs/ExtJsonIpV6" },
				{ "$ref": "#/$defs/ExtJsonIri" },
				{ "$ref": "#/$defs/ExtJsonIriReference" },
				{ "$ref": "#/$defs/ExtJsonPointer" },
				{ "$ref": "#/$defs/ExtJsonRegex" },
				{ "$ref": "#/$defs/ExtJsonRelativePointer" },
				{ "$ref": "#/$defs/ExtJsonUri" },
				{ "$ref": "#/$defs/ExtJsonUriReference" },
				{ "$ref": "#/$defs/ExtJsonUriTemplate" },
				{ "$ref": "#/$defs/ExtJsonUuid" },
				{ "$ref": "#/$defs/ExtJsonNumber" },
				{ "$ref": "#/$defs/ExtJsonInteger" },
				{ "$ref": "#/$defs/ExtJsonHalf" },
				{ "$ref": "#/$defs/ExtJsonSingle" },
				{ "$ref": "#/$defs/ExtJsonDouble" },
				{ "$ref": "#/$defs/ExtJsonDecimal" },
				{ "$ref": "#/$defs/ExtJsonSByte" },
				{ "$ref": "#/$defs/ExtJsonInt16" },
				{ "$ref": "#/$defs/ExtJsonInt32" },
				{ "$ref": "#/$defs/ExtJsonInt64" },
				{ "$ref": "#/$defs/ExtJsonInt128" },
				{ "$ref": "#/$defs/ExtJsonByte" },
				{ "$ref": "#/$defs/ExtJsonUInt16" },
				{ "$ref": "#/$defs/ExtJsonUInt32" },
				{ "$ref": "#/$defs/ExtJsonUInt64" },
				{ "$ref": "#/$defs/ExtJsonUInt128" }
			],
		
			"$defs": {
				"CoreJsonAny": true,
		
				"CoreJsonNotAny": false,
		
				"CoreJsonArray": {
					"type": "array"
				},
		
				"CoreJsonBoolean": {
					"type": "boolean"
				},
		
				"CoreJsonNull": {
					"type": "null"
				},
		
				"CoreJsonObject": {
					"type": "object"
				},
		
				"CoreJsonString": {
					"type": "string"
				},
		
				"CoreJsonBase64Content": {
					"type": "string",
					"contentMediaType": "application/json",
					"contentEncoding": "base64"
				},
		
				"CoreJsonBase64String": {
					"type": "string",
					"contentEncoding": "base64"
				},
		
				"CoreJsonContent": {
					"type": "string",
					"contentMediaType": "application/json"
				},
		
				"CoreJsonDateTime": {
					"type": "string",
					"format": "date-time"
				},
		
				"CoreJsonDate": {
					"type": "string",
					"format": "date"
				},
		
				"CoreJsonDuration": {
					"type": "string",
					"format": "duration"
				},
		
				"CoreJsonTime": {
					"type": "string",
					"format": "time"
				},
		
				"CoreJsonEmail": {
					"type": "string",
					"format": "email"
				},
		
				"CoreJsonHostname": {
					"type": "string",
					"format": "hostname"
				},
		
				"CoreJsonIdnEmail": {
					"type": "string",
					"format": "idn-email"
				},
		
				"CoreJsonIdnHostname": {
					"type": "string",
					"format": "idn-hostname"
				},
		
				"CoreJsonIpV4": {
					"type": "string",
					"format": "ipv4"
				},
		
				"CoreJsonIpV6": {
					"type": "string",
					"format": "ipv6"
				},
		
				"CoreJsonIri": {
					"type": "string",
					"format": "iri"
				},
		
				"CoreJsonIriReference": {
					"type": "string",
					"format": "iri-reference"
				},
		
				"CoreJsonPointer": {
					"type": "string",
					"format": "json-pointer"
				},
		
				"CoreJsonRegex": {
					"type": "string",
					"format": "regex"
				},
		
				"CoreJsonRelativePointer": {
					"type": "string",
					"format": "relative-json-pointer"
				},
		
				"CoreJsonUri": {
					"type": "string",
					"format": "uri"
				},
		
				"CoreJsonUriReference": {
					"type": "string",
					"format": "uri-reference"
				},
		
				"CoreJsonUriTemplate": {
					"type": "string",
					"format": "uri-template"
				},
		
				"CoreJsonUuid": {
					"type": "string",
					"format": "uuid"
				},
		
				"CoreJsonNumber": {
					"type": "number"
				},
		
				"CoreJsonInteger": {
					"type": "integer"
				},
		
				"CoreJsonHalf": {
					"type": "number",
					"format": "half"
				},
		
				"CoreJsonSingle": {
					"type": "number",
					"format": "single"
				},
		
				"CoreJsonDouble": {
					"type": "number",
					"format": "double"
				},
		
				"CoreJsonDecimal": {
					"type": "number",
					"format": "decimal"
				},
		
				"CoreJsonSByte": {
					"type": "number",
					"format": "sbyte"
				},
		
				"CoreJsonInt16": {
					"type": "number",
					"format": "int16"
				},
		
				"CoreJsonInt32": {
					"type": "number",
					"format": "int32"
				},
		
				"CoreJsonInt64": {
					"type": "number",
					"format": "int64"
				},
		
				"CoreJsonInt128": {
					"type": "number",
					"format": "int128"
				},
		
				"CoreJsonByte": {
					"type": "number",
					"format": "byte"
				},
		
				"CoreJsonUInt16": {
					"type": "number",
					"format": "uint16"
				},
		
				"CoreJsonUInt32": {
					"type": "number",
					"format": "uint32"
				},
		
				"CoreJsonUInt64": {
					"type": "number",
					"format": "uint64"
				},
		
				"CoreJsonUInt128": {
					"type": "number",
					"format": "uint128"
				},
		
				"ExtJsonArray": {
					"type": ["array", "string"],
					"minItems": 100
				},
		
				"ExtJsonObject": {
					"type": ["object", "string"],
					"properties": {"whizz": { "const": 3 } }
				},
		
				"ExtJsonString": {
					"type": ["string", "boolean"],
					"const": "wowsers"
				},
		
				"ExtJsonDateTime": {
					"type": ["string", "boolean"],
					"format": "date-time",
					"const": "wowsers"
				},
		
				"ExtJsonDate": {
					"type": ["string", "boolean"],
					"format": "date",
					"const": "wowsers"
				},
		
				"ExtJsonDuration": {
					"type": ["string", "boolean"],
					"format": "duration",
					"const": "wowsers"
				},
		
				"ExtJsonTime": {
					"type": ["string", "boolean"],
					"format": "time",
					"const": "wowsers"
				},
		
				"ExtJsonEmail": {
					"type": ["string", "boolean"],
					"format": "email",
					"const": "wowsers"
				},
		
				"ExtJsonHostname": {
					"type": ["string", "boolean"],
					"format": "hostname",
					"const": "wowsers"
				},
		
				"ExtJsonIdnEmail": {
					"type": ["string", "boolean"],
					"format": "idn-email",
					"const": "wowsers"
				},
		
				"ExtJsonIdnHostname": {
					"type": ["string", "boolean"],
					"format": "idn-hostname",
					"const": "wowsers"
				},
		
				"ExtJsonIpV4": {
					"type": ["string", "boolean"],
					"format": "ipv4",
					"const": "wowsers"
				},
		
				"ExtJsonIpV6": {
					"type": ["string", "boolean"],
					"format": "ipv6",
					"const": "wowsers"
				},
		
				"ExtJsonIri": {
					"type": ["string", "boolean"],
					"format": "iri",
					"const": "wowsers"
				},
		
				"ExtJsonIriReference": {
					"type": ["string", "boolean"],
					"format": "iri-reference",
					"const": "wowsers"
				},
		
				"ExtJsonPointer": {
					"type": ["string", "boolean"],
					"format": "json-pointer",
					"const": "wowsers"
				},
		
				"ExtJsonRegex": {
					"type": ["string", "boolean"],
					"format": "regex",
					"const": "wowsers"
				},
		
				"ExtJsonRelativePointer": {
					"type": ["string", "boolean"],
					"format": "relative-json-pointer",
					"const": "wowsers"
				},
		
				"ExtJsonUri": {
					"type": ["string", "boolean"],
					"format": "uri",
					"const": "wowsers"
				},
		
				"ExtJsonUriReference": {
					"type": ["string", "boolean"],
					"format": "uri-reference",
					"const": "wowsers"
				},
		
				"ExtJsonUriTemplate": {
					"type": ["string", "boolean"],
					"format": "uri-template",
					"const": "wowsers"
				},
		
				"ExtJsonUuid": {
					"type": ["string", "boolean"],
					"format": "uuid",
					"const": "wowsers"
				},
		
				"ExtJsonNumber": {
					"type": ["number", "boolean"],
					"minimum": 37
				},
		
				"ExtJsonInteger": {
					"type": ["integer", "boolean"],
					"minimum": 37
				},
		
				"ExtJsonHalf": {
					"type": ["number", "boolean"],
					"format": "half",
					"minimum": 37
				},
		
				"ExtJsonSingle": {
					"type": ["number", "boolean"],
					"format": "single",
					"minimum": 37
				},
		
				"ExtJsonDouble": {
					"type": ["number", "boolean"],
					"format": "double",
					"minimum": 37
				},
		
				"ExtJsonDecimal": {
					"type": "number",
					"format": "decimal",
					"minimum": 37
				},
		
				"ExtJsonSByte": {
					"type": ["number", "boolean"],
					"format": "sbyte",
					"minimum": 37
				},
		
				"ExtJsonInt16": {
					"type": ["number", "boolean"],
					"format": "int16",
					"minimum": 37
				},
		
				"ExtJsonInt32": {
					"type": ["number", "boolean"],
					"format": "int32",
					"minimum": 37
				},
		
				"ExtJsonInt64": {
					"type": ["number", "boolean"],
					"format": "int64",
					"minimum": 37
				},
		
				"ExtJsonInt128": {
					"type": ["number", "boolean"],
					"format": "int128",
					"minimum": 37
				},
		
				"ExtJsonByte": {
					"type": ["number", "boolean"],
					"format": "byte",
					"minimum": 37
				},
		
				"ExtJsonUInt16": {
					"type": ["number", "boolean"],
					"format": "uint16",
					"minimumm": 37
				},
		
				"ExtJsonUInt32": {
					"type": ["number", "boolean"],
					"format": "uint32",
					"minimumm": 37
				},
		
				"ExtJsonUInt64": {
					"type": ["number", "boolean"],
					"format": "uint64",
					"minimumm": 37
				},
		
				"ExtJsonUInt128": {
					"type": ["number", "boolean"],
					"format": "uint128",
					"minimum": 37
				}
			}
		}
		"""
	And the input data value <inputData>
	And I generate a type for the schema
	And I construct an instance of the schema type from the data
	When I validate the instance with level <level>
	Then the result will be <valid>
	And there will be <count> results

Examples:
	| inputData        | valid | level    | count |
	| ""               | false | Flag     | 0     |
	| ""               | false | Basic    | 1     |
	| ""               | false | Detailed | 1     |
	| ""               | false | Verbose  | 159   |
	| "foo"            | false | Flag     | 0     |
	| "foo"            | false | Basic    | 1     |
	| "foo"            | false | Detailed | 1     |
	| "foo"            | false | Verbose  | 159   |
	| null             | false | Flag     | 0     |
	| null             | false | Basic    | 1     |
	| null             | false | Detailed | 1     |
	| null             | false | Verbose  | 147   |
	| -1               | false | Flag     | 0     |
	| 0                | false | Flag     | 0     |
	| 1                | false | Flag     | 0     |
	| 256              | false | Flag     | 0     |
	| -256             | false | Flag     | 0     |
	| 256.1            | false | Flag     | 0     |
	| -256.1           | false | Flag     | 0     |
	| 256              | false | Flag     | 0     |
	| -32769           | false | Flag     | 0     |
	| 32769            | false | Flag     | 0     |
	| -32769.1         | false | Flag     | 0     |
	| 32769.1          | false | Flag     | 0     |
	| -65536           | false | Flag     | 0     |
	| 65536            | false | Flag     | 0     |
	| -65536.1         | false | Flag     | 0     |
	| 65536.1          | false | Flag     | 0     |
	| -2159483648      | false | Flag     | 0     |
	| 2159483648       | false | Flag     | 0     |
	| -2159483648.1    | false | Flag     | 0     |
	| 2159483648.1     | false | Flag     | 0     |
	| true             | false | Flag     | 0     |
	| [1,2,3]          | false | Flag     | 0     |
	| { "foo": "bar" } | false | Flag     | 0     |
	| -1               | false | Basic    | 1     |
	| 0                | false | Basic    | 1     |
	| 1                | false | Basic    | 1     |
	| 256              | false | Basic    | 1     |
	| -256             | false | Basic    | 1     |
	| 256.1            | false | Basic    | 1     |
	| -256.1           | false | Basic    | 1     |
	| 256              | false | Basic    | 1     |
	| -32769           | false | Basic    | 1     |
	| 32769            | false | Basic    | 1     |
	| -32769.1         | false | Basic    | 1     |
	| 32769.1          | false | Basic    | 1     |
	| -65536           | false | Basic    | 1     |
	| 65536            | false | Basic    | 1     |
	| -65536.1         | false | Basic    | 1     |
	| 65536.1          | false | Basic    | 1     |
	| -2159483648      | false | Basic    | 1     |
	| 2159483648       | false | Basic    | 1     |
	| -2159483648.1    | false | Basic    | 1     |
	| 2159483648.1     | false | Basic    | 1     |
	| true             | false | Basic    | 1     |
	| [1,2,3]          | false | Basic    | 1     |
	| { "foo": "bar" } | false | Basic    | 1     |
	| -1               | false | Detailed | 1     |
	| 0                | false | Detailed | 1     |
	| 1                | false | Detailed | 1     |
	| 256              | false | Detailed | 1     |
	| -256             | false | Detailed | 1     |
	| 256.1            | false | Detailed | 1     |
	| -256.1           | false | Detailed | 1     |
	| 256              | false | Detailed | 1     |
	| -32769           | false | Detailed | 1     |
	| 32769            | false | Detailed | 1     |
	| -32769.1         | false | Detailed | 1     |
	| 32769.1          | false | Detailed | 1     |
	| -65536           | false | Detailed | 1     |
	| 65536            | false | Detailed | 1     |
	| -65536.1         | false | Detailed | 1     |
	| 65536.1          | false | Detailed | 1     |
	| -2159483648      | false | Detailed | 1     |
	| 2159483648       | false | Detailed | 1     |
	| -2159483648.1    | false | Detailed | 1     |
	| 2159483648.1     | false | Detailed | 1     |
	| true             | false | Detailed | 1     |
	| [1,2,3]          | false | Detailed | 1     |
	| { "foo": "bar" } | false | Detailed | 1     |
	| -1               | false | Verbose  | 159   |
	| 0                | false | Verbose  | 159   |
	| 1                | false | Verbose  | 159   |
	| 256              | false | Verbose  | 159   |
	| -256             | false | Verbose  | 159   |
	| 256.1            | false | Verbose  | 159   |
	| -256.1           | false | Verbose  | 159   |
	| 256              | false | Verbose  | 147   |
	| -32769           | false | Verbose  | 159   |
	| 32769            | false | Verbose  | 159   |
	| -32769.1         | false | Verbose  | 151   |
	| 32769.1          | false | Verbose  | 151   |
	| -65536           | false | Verbose  | 153   |
	| 65536            | false | Verbose  | 156   |
	| -65536.1         | false | Verbose  | 150   |
	| 65536.1          | false | Verbose  | 150   |
	| -2159483648      | false | Verbose  | 159   |
	| 2159483648       | false | Verbose  | 159   |
	| -2159483648.1    | false | Verbose  | 159   |
	| 2159483648.1     | false | Verbose  | 159   |
	| true             | false | Verbose  | 147   |
	| [1,2,3]          | false | Verbose  | 147   |
	| { "foo": "bar" } | false | Verbose  | 146   |