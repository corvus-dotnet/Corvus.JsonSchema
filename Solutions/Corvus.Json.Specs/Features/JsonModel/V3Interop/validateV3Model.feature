Feature: Validate a V3 Model against V4

Scenario Outline: All of the format types
	Given the input data value <inputData>
	And I construct an instance of the V3 generated type
	When I validate the instance with level <level>
	Then the result will be <valid>
	And there will be <count> results

Examples:
	| inputData        | valid | level    | count |
	| ""               | false | Flag     | 0     |
	| ""               | false | Basic    | 1     |
	| ""               | false | Detailed | 1     |
	| ""               | false | Verbose  | 126   |
	| "foo"            | false | Flag     | 0     |
	| "foo"            | false | Basic    | 1     |
	| "foo"            | false | Detailed | 1     |
	| "foo"            | false | Verbose  | 126   |
	| null             | false | Flag     | 0     |
	| null             | false | Basic    | 1     |
	| null             | false | Detailed | 1     |
	| null             | false | Verbose  | 108   |
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
	| -2147483648      | false | Flag     | 0     |
	| 2147483648       | false | Flag     | 0     |
	| -2147483648.1    | false | Flag     | 0     |
	| 2147483648.1     | false | Flag     | 0     |
	| true             | false | Flag     | 0     |
	| [1,2,3]          | false | Flag     | 0     |
	| { "foo": "bar" } | true  | Flag     | 0     |
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
	| -2147483648      | false | Basic    | 1     |
	| 2147483648       | false | Basic    | 1     |
	| -2147483648.1    | false | Basic    | 1     |
	| 2147483648.1     | false | Basic    | 1     |
	| true             | false | Basic    | 1     |
	| [1,2,3]          | false | Basic    | 1     |
	| { "foo": "bar" } | true  | Basic    | 0     |
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
	| -2147483648      | false | Detailed | 1     |
	| 2147483648       | false | Detailed | 1     |
	| -2147483648.1    | false | Detailed | 1     |
	| 2147483648.1     | false | Detailed | 1     |
	| true             | false | Detailed | 1     |
	| [1,2,3]          | false | Detailed | 1     |
	| { "foo": "bar" } | true  | Detailed | 0     |
	| -1               | false | Verbose  | 122   |
	| 0                | false | Verbose  | 122   |
	| 1                | false | Verbose  | 122   |
	| 256              | false | Verbose  | 122   |
	| -256             | false | Verbose  | 122   |
	| 256.1            | false | Verbose  | 122   |
	| -256.1           | false | Verbose  | 122   |
	| 256              | false | Verbose  | 122   |
	| -32769           | false | Verbose  | 122   |
	| 32769            | false | Verbose  | 122   |
	| -32769.1         | false | Verbose  | 122   |
	| 32769.1          | false | Verbose  | 122   |
	| -65536           | false | Verbose  | 122   |
	| 65536            | false | Verbose  | 122   |
	| -65536.1         | false | Verbose  | 122   |
	| 65536.1          | false | Verbose  | 122   |
	| -2147483648      | false | Verbose  | 122   |
	| 2147483648       | false | Verbose  | 122   |
	| -2147483648.1    | false | Verbose  | 122   |
	| 2147483648.1     | false | Verbose  | 122   |
	| true             | false | Verbose  | 108   |
	| [1,2,3]          | false | Verbose  | 109   |
	| { "foo": "bar" } | true  | Verbose  | 108   |