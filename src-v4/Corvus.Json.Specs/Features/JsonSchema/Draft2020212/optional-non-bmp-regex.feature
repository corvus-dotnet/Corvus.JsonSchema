@draft2020-12

Feature: optional-non-bmp-regex draft2020-12
    In order to use json-schema
    As a developer
    I want to support optional-non-bmp-regex in draft2020-12

Scenario Outline: Proper UTF-16 surrogate pair handling: pattern
/* Schema: 
{
            "$schema": "https://json-schema.org/draft/2020-12/schema",
            "pattern": "^🐲*$"
        }
*/
    Given the input JSON file "optional/non-bmp-regex.json"
    And the schema at "#/0/schema"
    And the input data at "<inputDataReference>"
    And I generate a type for the schema
    And I construct an instance of the schema type from the data
    When I validate the instance
    Then the result will be <valid>

    Examples:
        | inputDataReference   | valid | description                                                                      |
        # 
        | #/000/tests/000/data | true  | matches empty                                                                    |
        # 🐲
        | #/000/tests/001/data | true  | matches single                                                                   |
        # 🐲🐲
        | #/000/tests/002/data | true  | matches two                                                                      |
        # 🐉
        | #/000/tests/003/data | false | doesn't match one                                                                |
        # 🐉🐉
        | #/000/tests/004/data | false | doesn't match two                                                                |
        # D
        | #/000/tests/005/data | false | doesn't match one ASCII                                                          |
        # DD
        | #/000/tests/006/data | false | doesn't match two ASCII                                                          |

Scenario Outline: Proper UTF-16 surrogate pair handling: patternProperties
/* Schema: 
{
            "$schema": "https://json-schema.org/draft/2020-12/schema",
            "patternProperties": {
                "^🐲*$": {
                    "type": "integer"
                }
            }
        }
*/
    Given the input JSON file "optional/non-bmp-regex.json"
    And the schema at "#/1/schema"
    And the input data at "<inputDataReference>"
    And I generate a type for the schema
    And I construct an instance of the schema type from the data
    When I validate the instance
    Then the result will be <valid>

    Examples:
        | inputDataReference   | valid | description                                                                      |
        # { "": 1 }
        | #/001/tests/000/data | true  | matches empty                                                                    |
        # { "🐲": 1 }
        | #/001/tests/001/data | true  | matches single                                                                   |
        # { "🐲🐲": 1 }
        | #/001/tests/002/data | true  | matches two                                                                      |
        # { "🐲": "hello" }
        | #/001/tests/003/data | false | doesn't match one                                                                |
        # { "🐲🐲": "hello" }
        | #/001/tests/004/data | false | doesn't match two                                                                |
