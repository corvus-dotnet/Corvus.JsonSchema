@draft2019-09

Feature: patternProperties draft2019-09
    In order to use json-schema
    As a developer
    I want to support patternProperties in draft2019-09

Scenario Outline: patternProperties validates properties matching a regex
/* Schema: 
{
            "$schema": "https://json-schema.org/draft/2019-09/schema",
            "patternProperties": {
                "f.*o": {"type": "integer"}
            }
        }
*/
    Given the input JSON file "patternProperties.json"
    And the schema at "#/0/schema"
    And the input data at "<inputDataReference>"
    And I generate a type for the schema
    And I construct an instance of the schema type from the data
    When I validate the instance
    Then the result will be <valid>

    Examples:
        | inputDataReference   | valid | description                                                                      |
        | #/000/tests/000/data | true  | a single valid match is valid                                                    |
        | #/000/tests/001/data | true  | multiple valid matches is valid                                                  |
        | #/000/tests/002/data | false | a single invalid match is invalid                                                |
        | #/000/tests/003/data | false | multiple invalid matches is invalid                                              |
        | #/000/tests/004/data | true  | ignores arrays                                                                   |
        | #/000/tests/005/data | true  | ignores strings                                                                  |
        | #/000/tests/006/data | true  | ignores other non-objects                                                        |

Scenario Outline: multiple simultaneous patternProperties are validated
/* Schema: 
{
            "$schema": "https://json-schema.org/draft/2019-09/schema",
            "patternProperties": {
                "a*": {"type": "integer"},
                "aaa*": {"maximum": 20}
            }
        }
*/
    Given the input JSON file "patternProperties.json"
    And the schema at "#/1/schema"
    And the input data at "<inputDataReference>"
    And I generate a type for the schema
    And I construct an instance of the schema type from the data
    When I validate the instance
    Then the result will be <valid>

    Examples:
        | inputDataReference   | valid | description                                                                      |
        | #/001/tests/000/data | true  | a single valid match is valid                                                    |
        | #/001/tests/001/data | true  | a simultaneous match is valid                                                    |
        | #/001/tests/002/data | true  | multiple matches is valid                                                        |
        | #/001/tests/003/data | false | an invalid due to one is invalid                                                 |
        | #/001/tests/004/data | false | an invalid due to the other is invalid                                           |
        | #/001/tests/005/data | false | an invalid due to both is invalid                                                |

Scenario Outline: regexes are not anchored by default and are case sensitive
/* Schema: 
{
            "$schema": "https://json-schema.org/draft/2019-09/schema",
            "patternProperties": {
                "[0-9]{2,}": { "type": "boolean" },
                "X_": { "type": "string" }
            }
        }
*/
    Given the input JSON file "patternProperties.json"
    And the schema at "#/2/schema"
    And the input data at "<inputDataReference>"
    And I generate a type for the schema
    And I construct an instance of the schema type from the data
    When I validate the instance
    Then the result will be <valid>

    Examples:
        | inputDataReference   | valid | description                                                                      |
        | #/002/tests/000/data | true  | non recognized members are ignored                                               |
        | #/002/tests/001/data | false | recognized members are accounted for                                             |
        | #/002/tests/002/data | true  | regexes are case sensitive                                                       |
        | #/002/tests/003/data | false | regexes are case sensitive, 2                                                    |

Scenario Outline: patternProperties with boolean schemas
/* Schema: 
{
            "$schema": "https://json-schema.org/draft/2019-09/schema",
            "patternProperties": {
                "f.*": true,
                "b.*": false
            }
        }
*/
    Given the input JSON file "patternProperties.json"
    And the schema at "#/3/schema"
    And the input data at "<inputDataReference>"
    And I generate a type for the schema
    And I construct an instance of the schema type from the data
    When I validate the instance
    Then the result will be <valid>

    Examples:
        | inputDataReference   | valid | description                                                                      |
        | #/003/tests/000/data | true  | object with property matching schema true is valid                               |
        | #/003/tests/001/data | false | object with property matching schema false is invalid                            |
        | #/003/tests/002/data | false | object with both properties is invalid                                           |
        | #/003/tests/003/data | false | object with a property matching both true and false is invalid                   |
        | #/003/tests/004/data | true  | empty object is valid                                                            |

Scenario Outline: patternProperties with null valued instance properties
/* Schema: 
{
            "$schema": "https://json-schema.org/draft/2019-09/schema",
            "patternProperties": {
                "^.*bar$": {"type": "null"}
            }
        }
*/
    Given the input JSON file "patternProperties.json"
    And the schema at "#/4/schema"
    And the input data at "<inputDataReference>"
    And I generate a type for the schema
    And I construct an instance of the schema type from the data
    When I validate the instance
    Then the result will be <valid>

    Examples:
        | inputDataReference   | valid | description                                                                      |
        | #/004/tests/000/data | true  | allows null values                                                               |
