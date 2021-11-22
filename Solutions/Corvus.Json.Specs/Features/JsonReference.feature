Feature: JsonReference
	In order to make use of RF3986 compliant referencing when working with JSON
	As a developer
	I want to support JsonReference resolution.

Scenario Outline: Apply references to an absolute base (normal examples)
	When I apply "<ref>" to the base reference "<base>"
	Then the applied reference will be "<applied>"

	Examples:
		| base               | ref     | applied              |
		| http://a/b/c/d;p?q | g:h     | g:h                  |
		| http://a/b/c/d;p?q | g       | http://a/b/c/g       |
		| http://a/b/c/d;p?q | ./g     | http://a/b/c/g       |
		| http://a/b/c/d;p?q | g/      | http://a/b/c/g/      |
		| http://a/b/c/d;p?q | /g      | http://a/g           |
		| http://a/b/c/d;p?q | //g     | http://g             |
		| http://a/b/c/d;p?q | ?y      | http://a/b/c/d;p?y   |
		| http://a/b/c/d;p?q | g?y     | http://a/b/c/g?y     |
		| http://a/b/c/d;p?q | #s      | http://a/b/c/d;p?q#s |
		| http://a/b/c/d;p?q | g#s     | http://a/b/c/g#s     |
		| http://a/b/c/d;p?q | g?y#s   | http://a/b/c/g?y#s   |
		| http://a/b/c/d;p?q | ;x      | http://a/b/c/;x      |
		| http://a/b/c/d;p?q | g;x     | http://a/b/c/g;x     |
		| http://a/b/c/d;p?q | g;x?y#s | http://a/b/c/g;x?y#s |
		| http://a/b/c/d;p?q |         | http://a/b/c/d;p?q   |
		| http://a/b/c/d;p?q | .       | http://a/b/c/        |
		| http://a/b/c/d;p?q | ./      | http://a/b/c/        |
		| http://a/b/c/d;p?q | ..      | http://a/b/          |
		| http://a/b/c/d;p?q | ../     | http://a/b/          |
		| http://a/b/c/d;p?q | ../g    | http://a/b/g         |
		| http://a/b/c/d;p?q | ../..   | http://a/            |
		| http://a/b/c/d;p?q | ../../  | http://a/            |
		| http://a/b/c/d;p?q | ../../g | http://a/g           |

Scenario Outline: Apply references to an absolute base (abnormal examples)
	When I apply "<ref>" to the base reference "<base>"
	Then the applied reference will be "<applied>"

	Examples:
		| base               | ref           | applied               |
		| http://a/b/c/d;p?q | ../../../g    | http://a/g            |
		| http://a/b/c/d;p?q | ../../../../g | http://a/g            |
		| http://a/b/c/d;p?q | /./g          | http://a/g            |
		| http://a/b/c/d;p?q | /../g         | http://a/g            |
		| http://a/b/c/d;p?q | g.            | http://a/b/c/g.       |
		| http://a/b/c/d;p?q | .g            | http://a/b/c/.g       |
		| http://a/b/c/d;p?q | g..           | http://a/b/c/g..      |
		| http://a/b/c/d;p?q | ..g           | http://a/b/c/..g      |
		| http://a/b/c/d;p?q | ./../g        | http://a/b/g          |
		| http://a/b/c/d;p?q | ./g/.         | http://a/b/c/g/       |
		| http://a/b/c/d;p?q | g/./h         | http://a/b/c/g/h      |
		| http://a/b/c/d;p?q | g/../h        | http://a/b/c/h        |
		| http://a/b/c/d;p?q | g;x=1/./y     | http://a/b/c/g;x=1/y  |
		| http://a/b/c/d;p?q | g;x=1/../y    | http://a/b/c/y        |
		| http://a/b/c/d;p?q | g?y/./x       | http://a/b/c/g?y/./x  |
		| http://a/b/c/d;p?q | g?y/../x      | http://a/b/c/g?y/../x |
		| http://a/b/c/d;p?q | g#s/./x       | http://a/b/c/g#s/./x  |
		| http://a/b/c/d;p?q | g#s/../x      | http://a/b/c/g#s/../x |

Scenario Outline: Apply references to an absolute base (backwards compatibility)
	When I apply "<ref>" to the base reference "<base>" using <strict>
	Then the applied reference will be "<applied>"

	Examples:
		| base               | ref    | applied        | strict |
		| http://a/b/c/d;p?q | http:g | http:g         | true   |
		| http://a/b/c/d;p?q | http:g | http://a/b/c/g | false  |