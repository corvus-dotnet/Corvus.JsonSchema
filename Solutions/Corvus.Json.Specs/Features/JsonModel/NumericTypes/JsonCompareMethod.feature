Feature: JsonCompareMethod
  In order to ensure the correctness of the JsonUInt16 comparison operators
  As a developer
  I want to test the static comparison operators with various scenarios

Scenario Outline: Compare two json backeed integer values using the static Compare() method
	Given the JsonElement backed <TargetType> <left>
	When I compare the <TargetType> with the <TargetType> <right>
	Then the comparison result should equal the int <result>

Examples:
	| left | right | result | TargetType  |
	| 10   | 20    | -1     | JsonInteger |
	| 20   | 10    | 1      | JsonInteger |
	| 15   | 15    | 0      | JsonInteger |
	| 10   | 20    | -1     | JsonInt64   |
	| 20   | 10    | 1      | JsonInt64   |
	| 15   | 15    | 0      | JsonInt64   |
	| 10   | 20    | -1     | JsonInt32   |
	| 20   | 10    | 1      | JsonInt32   |
	| 15   | 15    | 0      | JsonInt32   |
	| 10   | 20    | -1     | JsonInt16   |
	| 20   | 10    | 1      | JsonInt16   |
	| 15   | 15    | 0      | JsonInt16   |
	| 10   | 20    | -1     | JsonSByte   |
	| 20   | 10    | 1      | JsonSByte   |
	| 15   | 15    | 0      | JsonSByte   |
	| 10   | 20    | -1     | JsonUInt64  |
	| 20   | 10    | 1      | JsonUInt64  |
	| 15   | 15    | 0      | JsonUInt64  |
	| 10   | 20    | -1     | JsonUInt32  |
	| 20   | 10    | 1      | JsonUInt32  |
	| 15   | 15    | 0      | JsonUInt32  |
	| 10   | 20    | -1     | JsonUInt16  |
	| 20   | 10    | 1      | JsonUInt16  |
	| 15   | 15    | 0      | JsonUInt16  |
	| 10   | 20    | -1     | JsonByte    |
	| 20   | 10    | 1      | JsonByte    |
	| 15   | 15    | 0      | JsonByte    |

Scenario Outline: Compare two dotnet backed integer values using the static Compare() method
	Given the dotnet backed <TargetType> <left>
	When I compare the <TargetType> with the <TargetType> <right>
	Then the comparison result should equal the int <result>

Examples:
	| left | right | result | TargetType  |
	| 10   | 20    | -1     | JsonInteger |
	| 20   | 10    | 1      | JsonInteger |
	| 15   | 15    | 0      | JsonInteger |
	| 10   | 20    | -1     | JsonInt64   |
	| 20   | 10    | 1      | JsonInt64   |
	| 15   | 15    | 0      | JsonInt64   |
	| 10   | 20    | -1     | JsonInt32   |
	| 20   | 10    | 1      | JsonInt32   |
	| 15   | 15    | 0      | JsonInt32   |
	| 10   | 20    | -1     | JsonInt16   |
	| 20   | 10    | 1      | JsonInt16   |
	| 15   | 15    | 0      | JsonInt16   |
	| 10   | 20    | -1     | JsonSByte   |
	| 20   | 10    | 1      | JsonSByte   |
	| 15   | 15    | 0      | JsonSByte   |
	| 10   | 20    | -1     | JsonUInt64  |
	| 20   | 10    | 1      | JsonUInt64  |
	| 15   | 15    | 0      | JsonUInt64  |
	| 10   | 20    | -1     | JsonUInt32  |
	| 20   | 10    | 1      | JsonUInt32  |
	| 15   | 15    | 0      | JsonUInt32  |
	| 10   | 20    | -1     | JsonUInt16  |
	| 20   | 10    | 1      | JsonUInt16  |
	| 15   | 15    | 0      | JsonUInt16  |
	| 10   | 20    | -1     | JsonByte    |
	| 20   | 10    | 1      | JsonByte    |
	| 15   | 15    | 0      | JsonByte    |