@draft7

Feature: anyOf draft7
    In order to use json-schema
    As a developer
    I want to support anyOf in draft7

Scenario Outline: anyOf
/* Schema: 
{
            "anyOf": [
                {
                    "type": "integer"
                },
                {
                    "minimum": 2
                }
            ]
        }
*/
    Given the input JSON file "anyOf.json"
    And the schema at "#/0/schema"
    And the input data at "<inputDataReference>"
    And I generate a type for the schema
    And I construct an instance of the schema type from the data
    When I validate the instance
    Then the result will be <valid>

    Examples:
        | inputDataReference   | valid | description                                                                      |
        | #/000/tests/000/data | true  | first anyOf valid                                                                |
        | #/000/tests/001/data | true  | second anyOf valid                                                               |
        | #/000/tests/002/data | true  | both anyOf valid                                                                 |
        | #/000/tests/003/data | false | neither anyOf valid                                                              |

Scenario Outline: anyOf with base schema
/* Schema: 
{
            "type": "string",
            "anyOf" : [
                {
                    "maxLength": 2
                },
                {
                    "minLength": 4
                }
            ]
        }
*/
    Given the input JSON file "anyOf.json"
    And the schema at "#/1/schema"
    And the input data at "<inputDataReference>"
    And I generate a type for the schema
    And I construct an instance of the schema type from the data
    When I validate the instance
    Then the result will be <valid>

    Examples:
        | inputDataReference   | valid | description                                                                      |
        | #/001/tests/000/data | false | mismatch base schema                                                             |
        | #/001/tests/001/data | true  | one anyOf valid                                                                  |
        | #/001/tests/002/data | false | both anyOf invalid                                                               |

Scenario Outline: anyOf with boolean schemas, all true
/* Schema: 
{"anyOf": [true, true]}
*/
    Given the input JSON file "anyOf.json"
    And the schema at "#/2/schema"
    And the input data at "<inputDataReference>"
    And I generate a type for the schema
    And I construct an instance of the schema type from the data
    When I validate the instance
    Then the result will be <valid>

    Examples:
        | inputDataReference   | valid | description                                                                      |
        | #/002/tests/000/data | true  | any value is valid                                                               |

Scenario Outline: anyOf with boolean schemas, some true
/* Schema: 
{"anyOf": [true, false]}
*/
    Given the input JSON file "anyOf.json"
    And the schema at "#/3/schema"
    And the input data at "<inputDataReference>"
    And I generate a type for the schema
    And I construct an instance of the schema type from the data
    When I validate the instance
    Then the result will be <valid>

    Examples:
        | inputDataReference   | valid | description                                                                      |
        | #/003/tests/000/data | true  | any value is valid                                                               |

Scenario Outline: anyOf with boolean schemas, all false
/* Schema: 
{"anyOf": [false, false]}
*/
    Given the input JSON file "anyOf.json"
    And the schema at "#/4/schema"
    And the input data at "<inputDataReference>"
    And I generate a type for the schema
    And I construct an instance of the schema type from the data
    When I validate the instance
    Then the result will be <valid>

    Examples:
        | inputDataReference   | valid | description                                                                      |
        | #/004/tests/000/data | false | any value is invalid                                                             |

Scenario Outline: anyOf complex types
/* Schema: 
{
            "anyOf": [
                {
                    "properties": {
                        "bar": {"type": "integer"}
                    },
                    "required": ["bar"]
                },
                {
                    "properties": {
                        "foo": {"type": "string"}
                    },
                    "required": ["foo"]
                }
            ]
        }
*/
    Given the input JSON file "anyOf.json"
    And the schema at "#/5/schema"
    And the input data at "<inputDataReference>"
    And I generate a type for the schema
    And I construct an instance of the schema type from the data
    When I validate the instance
    Then the result will be <valid>

    Examples:
        | inputDataReference   | valid | description                                                                      |
        | #/005/tests/000/data | true  | first anyOf valid (complex)                                                      |
        | #/005/tests/001/data | true  | second anyOf valid (complex)                                                     |
        | #/005/tests/002/data | true  | both anyOf valid (complex)                                                       |
        | #/005/tests/003/data | false | neither anyOf valid (complex)                                                    |

Scenario Outline: anyOf with one empty schema
/* Schema: 
{
            "anyOf": [
                { "type": "number" },
                {}
            ]
        }
*/
    Given the input JSON file "anyOf.json"
    And the schema at "#/6/schema"
    And the input data at "<inputDataReference>"
    And I generate a type for the schema
    And I construct an instance of the schema type from the data
    When I validate the instance
    Then the result will be <valid>

    Examples:
        | inputDataReference   | valid | description                                                                      |
        | #/006/tests/000/data | true  | string is valid                                                                  |
        | #/006/tests/001/data | true  | number is valid                                                                  |

Scenario Outline: nested anyOf, to check validation semantics
/* Schema: 
{
            "anyOf": [
                {
                    "anyOf": [
                        {
                            "type": "null"
                        }
                    ]
                }
            ]
        }
*/
    Given the input JSON file "anyOf.json"
    And the schema at "#/7/schema"
    And the input data at "<inputDataReference>"
    And I generate a type for the schema
    And I construct an instance of the schema type from the data
    When I validate the instance
    Then the result will be <valid>

    Examples:
        | inputDataReference   | valid | description                                                                      |
        | #/007/tests/000/data | true  | null is valid                                                                    |
        | #/007/tests/001/data | false | anything non-null is invalid                                                     |

Scenario Outline: nested anyOf, to check validation semantics [3]
/* Schema: 
{
            "anyOf": [
                {
                    "anyOf": [
                        {
                            "type": "null"
                        }
                    ]
                }
            ]
        }
*/
    Given the input JSON file "anyOf.json"
    And the schema at "#/8/schema"
    And the input data at "<inputDataReference>"
    And I generate a type for the schema
    And I construct an instance of the schema type from the data
    When I validate the instance
    Then the result will be <valid>

    Examples:
        | inputDataReference   | valid | description                                                                      |
        | #/008/tests/000/data | true  | null is valid                                                                    |
        | #/008/tests/001/data | false | anything non-null is invalid                                                     |
