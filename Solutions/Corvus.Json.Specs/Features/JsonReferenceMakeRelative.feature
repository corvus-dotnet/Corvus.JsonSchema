Feature: JsonReference MakeRelative
	In order to generate correct relative schema locations on all platforms
	As a developer
	I want to support making relative references from absolute references

Scenario Outline: Make relative references from absolute URIs
	When I make "<target>" relative to the base reference "<base>"
	Then the relative reference will be "<relative>"

	Examples:
		| base                                         | target                                                   | relative                   |
		| https://endjin.com/home/user/test.json       | https://endjin.com/home/user/test.json                   |                            |
		| https://endjin.com/home/user/test.json       | https://endjin.com/home/user/test.json#/$defs/TestType   | #/$defs/TestType           |
		| https://endjin.com/home/user/test.json       | https://endjin.com/home/user/other.json                  | other.json                 |
		| https://endjin.com/home/user/test.json       | https://endjin.com/home/user/other.json#/$defs/TestType  | other.json#/$defs/TestType |
		| https://endjin.com/home/user/test.json       | https://endjin.com/home/other/file.json                  | ../other/file.json         |
		| https://endjin.com/schema.json               | https://endjin.com/schema.json#/$defs/Type               | #/$defs/Type               |
		| https://endjin.com/path/to/schema.json       | https://endjin.com/path/to/schema.json#/properties/name  | #/properties/name          |

Scenario Outline: Make relative references from implicit file paths (Windows-style)
	When I make "<target>" relative to the base reference "<base>"
	Then the relative reference will be "<relative>"

	Examples:
		| base                           | target                                       | relative                   |
		| C:/Users/test/schema.json      | C:/Users/test/schema.json                    |                            |
		| C:/Users/test/schema.json      | C:/Users/test/schema.json#/$defs/TestType    | #/$defs/TestType           |
		| C:/Users/test/schema.json      | C:/Users/test/other.json                     | other.json                 |
		| C:/Users/test/schema.json      | C:/Users/test/other.json#/$defs/TestType     | other.json#/$defs/TestType |

Scenario Outline: GetRelativeLocationFor returns filename for base location (implicit file paths)
	When I get relative location for "<target>" with base "<base>"
	Then the relative reference will be "<relative>"

	Examples:
		| base                           | target                                       | relative                       |
		| C:/Users/test/schema.json      | C:/Users/test/schema.json                    | schema.json                    |
		| C:/Users/test/schema.json      | C:/Users/test/schema.json#/$defs/TestType    | schema.json#/$defs/TestType    |
		| C:/Users/test/schema.json      | C:/Users/test/other.json                     | other.json                     |
		| C:/Users/test/schema.json      | C:/Users/test/other.json#/$defs/TestType     | other.json#/$defs/TestType     |

Scenario Outline: GetRelativeLocationFor returns filename for base location (absolute URIs)
	When I get relative location for "<target>" with base "<base>"
	Then the relative reference will be "<relative>"

	Examples:
		| base                                         | target                                                   | relative                       |
		| https://endjin.com/home/user/test.json       | https://endjin.com/home/user/test.json                   | test.json                      |
		| https://endjin.com/home/user/test.json       | https://endjin.com/home/user/test.json#/$defs/TestType   | test.json#/$defs/TestType      |
		| https://endjin.com/home/user/test.json       | https://endjin.com/home/user/other.json                  | other.json                     |
		| https://endjin.com/home/user/test.json       | https://endjin.com/home/user/other.json#/$defs/TestType  | other.json#/$defs/TestType     |
		| https://endjin.com/home/user/test.json       | https://endjin.com/home/other/file.json                  | ../other/file.json             |
		| https://endjin.com/schema.json               | https://endjin.com/schema.json#/$defs/Type               | schema.json#/$defs/Type        |
		| https://endjin.com/path/to/schema.json       | https://endjin.com/path/to/schema.json#/properties/name  | schema.json#/properties/name   |

Scenario Outline: GetRelativeLocationFor returns filename for base location (relative URIs)
	When I get relative location for "<target>" with base "<base>"
	Then the relative reference will be "<relative>"

	Examples:
		| base              | target                      | relative                       |
		| ./test.json       | test.json                   | test.json                      |
		| ./test.json       | test.json#/$defs/TestType   | test.json#/$defs/TestType      |
		| ./test.json       | other.json                  | other.json                     |
		| ./test.json       | other.json#/$defs/TestType  | other.json#/$defs/TestType     |
		| ./test.json       | user/test.json              | user/test.json                 |

