Feature: Formatting Identifiers

Scenario Outline: Formatting strings
	When I format the input string "<input>" with <format> casing
	Then The output will be "<output>"
Examples:
	| input               | format | output              |
	| aadB2cConfiguration | Pascal | AadB2cConfiguration |
	| aadB2CConfiguration | Pascal | AadB2Cconfiguration |
