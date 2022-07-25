@draft2019-09

Feature: if-then-else draft2019-09
    In order to use json-schema
    As a developer
    I want to support if-then-else in draft2019-09

Scenario Outline: ignore if without then or else
/* Schema: 
{
            "if": {
                "const": 0
            }
        }
*/
    Given the input JSON file "if-then-else.json"
    And the schema at "#/0/schema"
    And the input data at "<inputDataReference>"
    And I generate a type for the schema
    And I construct an instance of the schema type from the data
    When I validate the instance
    Then the result will be <valid>

    Examples:
        | inputDataReference   | valid | description                                                                      |
        | #/000/tests/000/data | true  | valid when valid against lone if                                                 |
        | #/000/tests/001/data | true  | valid when invalid against lone if                                               |

Scenario Outline: ignore then without if
/* Schema: 
{
            "then": {
                "const": 0
            }
        }
*/
    Given the input JSON file "if-then-else.json"
    And the schema at "#/1/schema"
    And the input data at "<inputDataReference>"
    And I generate a type for the schema
    And I construct an instance of the schema type from the data
    When I validate the instance
    Then the result will be <valid>

    Examples:
        | inputDataReference   | valid | description                                                                      |
        | #/001/tests/000/data | true  | valid when valid against lone then                                               |
        | #/001/tests/001/data | true  | valid when invalid against lone then                                             |

Scenario Outline: ignore else without if
/* Schema: 
{
            "else": {
                "const": 0
            }
        }
*/
    Given the input JSON file "if-then-else.json"
    And the schema at "#/2/schema"
    And the input data at "<inputDataReference>"
    And I generate a type for the schema
    And I construct an instance of the schema type from the data
    When I validate the instance
    Then the result will be <valid>

    Examples:
        | inputDataReference   | valid | description                                                                      |
        | #/002/tests/000/data | true  | valid when valid against lone else                                               |
        | #/002/tests/001/data | true  | valid when invalid against lone else                                             |

Scenario Outline: if and then without else
/* Schema: 
{
            "if": {
                "exclusiveMaximum": 0
            },
            "then": {
                "minimum": -10
            }
        }
*/
    Given the input JSON file "if-then-else.json"
    And the schema at "#/3/schema"
    And the input data at "<inputDataReference>"
    And I generate a type for the schema
    And I construct an instance of the schema type from the data
    When I validate the instance
    Then the result will be <valid>

    Examples:
        | inputDataReference   | valid | description                                                                      |
        | #/003/tests/000/data | true  | valid through then                                                               |
        | #/003/tests/001/data | false | invalid through then                                                             |
        | #/003/tests/002/data | true  | valid when if test fails                                                         |

Scenario Outline: if and else without then
/* Schema: 
{
            "if": {
                "exclusiveMaximum": 0
            },
            "else": {
                "multipleOf": 2
            }
        }
*/
    Given the input JSON file "if-then-else.json"
    And the schema at "#/4/schema"
    And the input data at "<inputDataReference>"
    And I generate a type for the schema
    And I construct an instance of the schema type from the data
    When I validate the instance
    Then the result will be <valid>

    Examples:
        | inputDataReference   | valid | description                                                                      |
        | #/004/tests/000/data | true  | valid when if test passes                                                        |
        | #/004/tests/001/data | true  | valid through else                                                               |
        | #/004/tests/002/data | false | invalid through else                                                             |

Scenario Outline: validate against correct branch, then vs else
/* Schema: 
{
            "if": {
                "exclusiveMaximum": 0
            },
            "then": {
                "minimum": -10
            },
            "else": {
                "multipleOf": 2
            }
        }
*/
    Given the input JSON file "if-then-else.json"
    And the schema at "#/5/schema"
    And the input data at "<inputDataReference>"
    And I generate a type for the schema
    And I construct an instance of the schema type from the data
    When I validate the instance
    Then the result will be <valid>

    Examples:
        | inputDataReference   | valid | description                                                                      |
        | #/005/tests/000/data | true  | valid through then                                                               |
        | #/005/tests/001/data | false | invalid through then                                                             |
        | #/005/tests/002/data | true  | valid through else                                                               |
        | #/005/tests/003/data | false | invalid through else                                                             |

Scenario Outline: non-interference across combined schemas
/* Schema: 
{
            "allOf": [
                {
                    "if": {
                        "exclusiveMaximum": 0
                    }
                },
                {
                    "then": {
                        "minimum": -10
                    }
                },
                {
                    "else": {
                        "multipleOf": 2
                    }
                }
            ]
        }
*/
    Given the input JSON file "if-then-else.json"
    And the schema at "#/6/schema"
    And the input data at "<inputDataReference>"
    And I generate a type for the schema
    And I construct an instance of the schema type from the data
    When I validate the instance
    Then the result will be <valid>

    Examples:
        | inputDataReference   | valid | description                                                                      |
        | #/006/tests/000/data | true  | valid, but would have been invalid through then                                  |
        | #/006/tests/001/data | true  | valid, but would have been invalid through else                                  |

Scenario Outline: if with boolean schema true
/* Schema: 
{
            "if": true,
            "then": { "const": "then" },
            "else": { "const": "else" }
        }
*/
    Given the input JSON file "if-then-else.json"
    And the schema at "#/7/schema"
    And the input data at "<inputDataReference>"
    And I generate a type for the schema
    And I construct an instance of the schema type from the data
    When I validate the instance
    Then the result will be <valid>

    Examples:
        | inputDataReference   | valid | description                                                                      |
        | #/007/tests/000/data | true  | boolean schema true in if always chooses the then path (valid)                   |
        | #/007/tests/001/data | false | boolean schema true in if always chooses the then path (invalid)                 |

Scenario Outline: if with boolean schema false
/* Schema: 
{
            "if": false,
            "then": { "const": "then" },
            "else": { "const": "else" }
        }
*/
    Given the input JSON file "if-then-else.json"
    And the schema at "#/8/schema"
    And the input data at "<inputDataReference>"
    And I generate a type for the schema
    And I construct an instance of the schema type from the data
    When I validate the instance
    Then the result will be <valid>

    Examples:
        | inputDataReference   | valid | description                                                                      |
        | #/008/tests/000/data | false | boolean schema false in if always chooses the else path (invalid)                |
        | #/008/tests/001/data | true  | boolean schema false in if always chooses the else path (valid)                  |

Scenario Outline: if appears at the end when serialized (keyword processing sequence)
/* Schema: 
{
            "then": { "const": "yes" },
            "else": { "const": "other" },
            "if": { "maxLength": 4 }
        }
*/
    Given the input JSON file "if-then-else.json"
    And the schema at "#/9/schema"
    And the input data at "<inputDataReference>"
    And I generate a type for the schema
    And I construct an instance of the schema type from the data
    When I validate the instance
    Then the result will be <valid>

    Examples:
        | inputDataReference   | valid | description                                                                      |
        | #/009/tests/000/data | true  | yes redirects to then and passes                                                 |
        | #/009/tests/001/data | true  | other redirects to else and passes                                               |
        | #/009/tests/002/data | false | no redirects to then and fails                                                   |
        | #/009/tests/003/data | false | invalid redirects to else and fails                                              |
