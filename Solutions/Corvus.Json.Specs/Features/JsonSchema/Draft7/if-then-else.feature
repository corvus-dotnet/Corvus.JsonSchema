@draft7

Feature: if-then-else draft7
    In order to use json-schema
    As a developer
    I want to support if-then-else in draft7

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
    And I assert format
    And I generate a type for the schema
    And I construct an instance of the schema type from the data
    When I validate the instance
    Then the result will be <valid>

    Examples:
        | inputDataReference   | valid | description                                                                      |
        # 0
        | #/000/tests/000/data | true  | valid when valid against lone if                                                 |
        # hello
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
    And I assert format
    And I generate a type for the schema
    And I construct an instance of the schema type from the data
    When I validate the instance
    Then the result will be <valid>

    Examples:
        | inputDataReference   | valid | description                                                                      |
        # 0
        | #/001/tests/000/data | true  | valid when valid against lone then                                               |
        # hello
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
    And I assert format
    And I generate a type for the schema
    And I construct an instance of the schema type from the data
    When I validate the instance
    Then the result will be <valid>

    Examples:
        | inputDataReference   | valid | description                                                                      |
        # 0
        | #/002/tests/000/data | true  | valid when valid against lone else                                               |
        # hello
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
    And I assert format
    And I generate a type for the schema
    And I construct an instance of the schema type from the data
    When I validate the instance
    Then the result will be <valid>

    Examples:
        | inputDataReference   | valid | description                                                                      |
        # -1
        | #/003/tests/000/data | true  | valid through then                                                               |
        # -100
        | #/003/tests/001/data | false | invalid through then                                                             |
        # 3
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
    And I assert format
    And I generate a type for the schema
    And I construct an instance of the schema type from the data
    When I validate the instance
    Then the result will be <valid>

    Examples:
        | inputDataReference   | valid | description                                                                      |
        # -1
        | #/004/tests/000/data | true  | valid when if test passes                                                        |
        # 4
        | #/004/tests/001/data | true  | valid through else                                                               |
        # 3
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
    And I assert format
    And I generate a type for the schema
    And I construct an instance of the schema type from the data
    When I validate the instance
    Then the result will be <valid>

    Examples:
        | inputDataReference   | valid | description                                                                      |
        # -1
        | #/005/tests/000/data | true  | valid through then                                                               |
        # -100
        | #/005/tests/001/data | false | invalid through then                                                             |
        # 4
        | #/005/tests/002/data | true  | valid through else                                                               |
        # 3
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
    And I assert format
    And I generate a type for the schema
    And I construct an instance of the schema type from the data
    When I validate the instance
    Then the result will be <valid>

    Examples:
        | inputDataReference   | valid | description                                                                      |
        # -100
        | #/006/tests/000/data | true  | valid, but would have been invalid through then                                  |
        # 3
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
    And I assert format
    And I generate a type for the schema
    And I construct an instance of the schema type from the data
    When I validate the instance
    Then the result will be <valid>

    Examples:
        | inputDataReference   | valid | description                                                                      |
        # then
        | #/007/tests/000/data | true  | boolean schema true in if always chooses the then path (valid)                   |
        # else
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
    And I assert format
    And I generate a type for the schema
    And I construct an instance of the schema type from the data
    When I validate the instance
    Then the result will be <valid>

    Examples:
        | inputDataReference   | valid | description                                                                      |
        # then
        | #/008/tests/000/data | false | boolean schema false in if always chooses the else path (invalid)                |
        # else
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
    And I assert format
    And I generate a type for the schema
    And I construct an instance of the schema type from the data
    When I validate the instance
    Then the result will be <valid>

    Examples:
        | inputDataReference   | valid | description                                                                      |
        # yes
        | #/009/tests/000/data | true  | yes redirects to then and passes                                                 |
        # other
        | #/009/tests/001/data | true  | other redirects to else and passes                                               |
        # no
        | #/009/tests/002/data | false | no redirects to then and fails                                                   |
        # invalid
        | #/009/tests/003/data | false | invalid redirects to else and fails                                              |
