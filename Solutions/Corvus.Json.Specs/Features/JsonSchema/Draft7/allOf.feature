@draft7

Feature: allOf draft7
    In order to use json-schema
    As a developer
    I want to support allOf in draft7

Scenario Outline: allOf
/* Schema: 
{
            "allOf": [
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
    Given the input JSON file "allOf.json"
    And the schema at "#/0/schema"
    And the input data at "<inputDataReference>"
    And I generate a type for the schema
    And I construct an instance of the schema type from the data
    When I validate the instance
    Then the result will be <valid>

    Examples:
        | inputDataReference   | valid | description                                                                      |
        # {"foo": "baz", "bar": 2}
        | #/000/tests/000/data | true  | allOf                                                                            |
        # {"foo": "baz"}
        | #/000/tests/001/data | false | mismatch second                                                                  |
        # {"bar": 2}
        | #/000/tests/002/data | false | mismatch first                                                                   |
        # {"foo": "baz", "bar": "quux"}
        | #/000/tests/003/data | false | wrong type                                                                       |

Scenario Outline: allOf with base schema
/* Schema: 
{
            "properties": {"bar": {"type": "integer"}},
            "required": ["bar"],
            "allOf" : [
                {
                    "properties": {
                        "foo": {"type": "string"}
                    },
                    "required": ["foo"]
                },
                {
                    "properties": {
                        "baz": {"type": "null"}
                    },
                    "required": ["baz"]
                }
            ]
        }
*/
    Given the input JSON file "allOf.json"
    And the schema at "#/1/schema"
    And the input data at "<inputDataReference>"
    And I generate a type for the schema
    And I construct an instance of the schema type from the data
    When I validate the instance
    Then the result will be <valid>

    Examples:
        | inputDataReference   | valid | description                                                                      |
        # {"foo": "quux", "bar": 2, "baz": null}
        | #/001/tests/000/data | true  | valid                                                                            |
        # {"foo": "quux", "baz": null}
        | #/001/tests/001/data | false | mismatch base schema                                                             |
        # {"bar": 2, "baz": null}
        | #/001/tests/002/data | false | mismatch first allOf                                                             |
        # {"foo": "quux", "bar": 2}
        | #/001/tests/003/data | false | mismatch second allOf                                                            |
        # {"bar": 2}
        | #/001/tests/004/data | false | mismatch both                                                                    |

Scenario Outline: allOf simple types
/* Schema: 
{
            "allOf": [
                {"maximum": 30},
                {"minimum": 20}
            ]
        }
*/
    Given the input JSON file "allOf.json"
    And the schema at "#/2/schema"
    And the input data at "<inputDataReference>"
    And I generate a type for the schema
    And I construct an instance of the schema type from the data
    When I validate the instance
    Then the result will be <valid>

    Examples:
        | inputDataReference   | valid | description                                                                      |
        # 25
        | #/002/tests/000/data | true  | valid                                                                            |
        # 35
        | #/002/tests/001/data | false | mismatch one                                                                     |

Scenario Outline: allOf with boolean schemas, all true
/* Schema: 
{"allOf": [true, true]}
*/
    Given the input JSON file "allOf.json"
    And the schema at "#/3/schema"
    And the input data at "<inputDataReference>"
    And I generate a type for the schema
    And I construct an instance of the schema type from the data
    When I validate the instance
    Then the result will be <valid>

    Examples:
        | inputDataReference   | valid | description                                                                      |
        # foo
        | #/003/tests/000/data | true  | any value is valid                                                               |

Scenario Outline: allOf with boolean schemas, some false
/* Schema: 
{"allOf": [true, false]}
*/
    Given the input JSON file "allOf.json"
    And the schema at "#/4/schema"
    And the input data at "<inputDataReference>"
    And I generate a type for the schema
    And I construct an instance of the schema type from the data
    When I validate the instance
    Then the result will be <valid>

    Examples:
        | inputDataReference   | valid | description                                                                      |
        # foo
        | #/004/tests/000/data | false | any value is invalid                                                             |

Scenario Outline: allOf with boolean schemas, all false
/* Schema: 
{"allOf": [false, false]}
*/
    Given the input JSON file "allOf.json"
    And the schema at "#/5/schema"
    And the input data at "<inputDataReference>"
    And I generate a type for the schema
    And I construct an instance of the schema type from the data
    When I validate the instance
    Then the result will be <valid>

    Examples:
        | inputDataReference   | valid | description                                                                      |
        # foo
        | #/005/tests/000/data | false | any value is invalid                                                             |

Scenario Outline: allOf with one empty schema
/* Schema: 
{
            "allOf": [
                {}
            ]
        }
*/
    Given the input JSON file "allOf.json"
    And the schema at "#/6/schema"
    And the input data at "<inputDataReference>"
    And I generate a type for the schema
    And I construct an instance of the schema type from the data
    When I validate the instance
    Then the result will be <valid>

    Examples:
        | inputDataReference   | valid | description                                                                      |
        # 1
        | #/006/tests/000/data | true  | any data is valid                                                                |

Scenario Outline: allOf with two empty schemas
/* Schema: 
{
            "allOf": [
                {},
                {}
            ]
        }
*/
    Given the input JSON file "allOf.json"
    And the schema at "#/7/schema"
    And the input data at "<inputDataReference>"
    And I generate a type for the schema
    And I construct an instance of the schema type from the data
    When I validate the instance
    Then the result will be <valid>

    Examples:
        | inputDataReference   | valid | description                                                                      |
        # 1
        | #/007/tests/000/data | true  | any data is valid                                                                |

Scenario Outline: allOf with the first empty schema
/* Schema: 
{
            "allOf": [
                {},
                { "type": "number" }
            ]
        }
*/
    Given the input JSON file "allOf.json"
    And the schema at "#/8/schema"
    And the input data at "<inputDataReference>"
    And I generate a type for the schema
    And I construct an instance of the schema type from the data
    When I validate the instance
    Then the result will be <valid>

    Examples:
        | inputDataReference   | valid | description                                                                      |
        # 1
        | #/008/tests/000/data | true  | number is valid                                                                  |
        # foo
        | #/008/tests/001/data | false | string is invalid                                                                |

Scenario Outline: allOf with the last empty schema
/* Schema: 
{
            "allOf": [
                { "type": "number" },
                {}
            ]
        }
*/
    Given the input JSON file "allOf.json"
    And the schema at "#/9/schema"
    And the input data at "<inputDataReference>"
    And I generate a type for the schema
    And I construct an instance of the schema type from the data
    When I validate the instance
    Then the result will be <valid>

    Examples:
        | inputDataReference   | valid | description                                                                      |
        # 1
        | #/009/tests/000/data | true  | number is valid                                                                  |
        # foo
        | #/009/tests/001/data | false | string is invalid                                                                |

Scenario Outline: nested allOf, to check validation semantics
/* Schema: 
{
            "allOf": [
                {
                    "allOf": [
                        {
                            "type": "null"
                        }
                    ]
                }
            ]
        }
*/
    Given the input JSON file "allOf.json"
    And the schema at "#/10/schema"
    And the input data at "<inputDataReference>"
    And I generate a type for the schema
    And I construct an instance of the schema type from the data
    When I validate the instance
    Then the result will be <valid>

    Examples:
        | inputDataReference   | valid | description                                                                      |
        # 
        | #/010/tests/000/data | true  | null is valid                                                                    |
        # 123
        | #/010/tests/001/data | false | anything non-null is invalid                                                     |

Scenario Outline: allOf combined with anyOf, oneOf
/* Schema: 
{
            "allOf": [ { "multipleOf": 2 } ],
            "anyOf": [ { "multipleOf": 3 } ],
            "oneOf": [ { "multipleOf": 5 } ]
        }
*/
    Given the input JSON file "allOf.json"
    And the schema at "#/11/schema"
    And the input data at "<inputDataReference>"
    And I generate a type for the schema
    And I construct an instance of the schema type from the data
    When I validate the instance
    Then the result will be <valid>

    Examples:
        | inputDataReference   | valid | description                                                                      |
        # 1
        | #/011/tests/000/data | false | allOf: false, anyOf: false, oneOf: false                                         |
        # 5
        | #/011/tests/001/data | false | allOf: false, anyOf: false, oneOf: true                                          |
        # 3
        | #/011/tests/002/data | false | allOf: false, anyOf: true, oneOf: false                                          |
        # 15
        | #/011/tests/003/data | false | allOf: false, anyOf: true, oneOf: true                                           |
        # 2
        | #/011/tests/004/data | false | allOf: true, anyOf: false, oneOf: false                                          |
        # 10
        | #/011/tests/005/data | false | allOf: true, anyOf: false, oneOf: true                                           |
        # 6
        | #/011/tests/006/data | false | allOf: true, anyOf: true, oneOf: false                                           |
        # 30
        | #/011/tests/007/data | true  | allOf: true, anyOf: true, oneOf: true                                            |
