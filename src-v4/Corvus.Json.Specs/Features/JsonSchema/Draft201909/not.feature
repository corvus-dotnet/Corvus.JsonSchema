@draft2019-09

Feature: not draft2019-09
    In order to use json-schema
    As a developer
    I want to support not in draft2019-09

Scenario Outline: not
/* Schema: 
{
            "$schema": "https://json-schema.org/draft/2019-09/schema",
            "not": {"type": "integer"}
        }
*/
    Given the input JSON file "not.json"
    And the schema at "#/0/schema"
    And the input data at "<inputDataReference>"
    And I generate a type for the schema
    And I construct an instance of the schema type from the data
    When I validate the instance
    Then the result will be <valid>

    Examples:
        | inputDataReference   | valid | description                                                                      |
        # foo
        | #/000/tests/000/data | true  | allowed                                                                          |
        # 1
        | #/000/tests/001/data | false | disallowed                                                                       |

Scenario Outline: not multiple types
/* Schema: 
{
            "$schema": "https://json-schema.org/draft/2019-09/schema",
            "not": {"type": ["integer", "boolean"]}
        }
*/
    Given the input JSON file "not.json"
    And the schema at "#/1/schema"
    And the input data at "<inputDataReference>"
    And I generate a type for the schema
    And I construct an instance of the schema type from the data
    When I validate the instance
    Then the result will be <valid>

    Examples:
        | inputDataReference   | valid | description                                                                      |
        # foo
        | #/001/tests/000/data | true  | valid                                                                            |
        # 1
        | #/001/tests/001/data | false | mismatch                                                                         |
        # True
        | #/001/tests/002/data | false | other mismatch                                                                   |

Scenario Outline: not more complex schema
/* Schema: 
{
            "$schema": "https://json-schema.org/draft/2019-09/schema",
            "not": {
                "type": "object",
                "properties": {
                    "foo": {
                        "type": "string"
                    }
                }
             }
        }
*/
    Given the input JSON file "not.json"
    And the schema at "#/2/schema"
    And the input data at "<inputDataReference>"
    And I generate a type for the schema
    And I construct an instance of the schema type from the data
    When I validate the instance
    Then the result will be <valid>

    Examples:
        | inputDataReference   | valid | description                                                                      |
        # 1
        | #/002/tests/000/data | true  | match                                                                            |
        # {"foo": 1}
        | #/002/tests/001/data | true  | other match                                                                      |
        # {"foo": "bar"}
        | #/002/tests/002/data | false | mismatch                                                                         |

Scenario Outline: forbidden property
/* Schema: 
{
            "$schema": "https://json-schema.org/draft/2019-09/schema",
            "properties": {
                "foo": { 
                    "not": {}
                }
            }
        }
*/
    Given the input JSON file "not.json"
    And the schema at "#/3/schema"
    And the input data at "<inputDataReference>"
    And I generate a type for the schema
    And I construct an instance of the schema type from the data
    When I validate the instance
    Then the result will be <valid>

    Examples:
        | inputDataReference   | valid | description                                                                      |
        # {"foo": 1, "bar": 2}
        | #/003/tests/000/data | false | property present                                                                 |
        # {"bar": 1, "baz": 2}
        | #/003/tests/001/data | true  | property absent                                                                  |

Scenario Outline: forbid everything with empty schema
/* Schema: 
{
            "$schema": "https://json-schema.org/draft/2019-09/schema",
            "not": {}
        }
*/
    Given the input JSON file "not.json"
    And the schema at "#/4/schema"
    And the input data at "<inputDataReference>"
    And I generate a type for the schema
    And I construct an instance of the schema type from the data
    When I validate the instance
    Then the result will be <valid>

    Examples:
        | inputDataReference   | valid | description                                                                      |
        # 1
        | #/004/tests/000/data | false | number is invalid                                                                |
        # foo
        | #/004/tests/001/data | false | string is invalid                                                                |
        # True
        | #/004/tests/002/data | false | boolean true is invalid                                                          |
        # False
        | #/004/tests/003/data | false | boolean false is invalid                                                         |
        # 
        | #/004/tests/004/data | false | null is invalid                                                                  |
        # {"foo": "bar"}
        | #/004/tests/005/data | false | object is invalid                                                                |
        # {}
        | #/004/tests/006/data | false | empty object is invalid                                                          |
        # ["foo"]
        | #/004/tests/007/data | false | array is invalid                                                                 |
        # []
        | #/004/tests/008/data | false | empty array is invalid                                                           |

Scenario Outline: forbid everything with boolean schema true
/* Schema: 
{
            "$schema": "https://json-schema.org/draft/2019-09/schema",
            "not": true
        }
*/
    Given the input JSON file "not.json"
    And the schema at "#/5/schema"
    And the input data at "<inputDataReference>"
    And I generate a type for the schema
    And I construct an instance of the schema type from the data
    When I validate the instance
    Then the result will be <valid>

    Examples:
        | inputDataReference   | valid | description                                                                      |
        # 1
        | #/005/tests/000/data | false | number is invalid                                                                |
        # foo
        | #/005/tests/001/data | false | string is invalid                                                                |
        # True
        | #/005/tests/002/data | false | boolean true is invalid                                                          |
        # False
        | #/005/tests/003/data | false | boolean false is invalid                                                         |
        # 
        | #/005/tests/004/data | false | null is invalid                                                                  |
        # {"foo": "bar"}
        | #/005/tests/005/data | false | object is invalid                                                                |
        # {}
        | #/005/tests/006/data | false | empty object is invalid                                                          |
        # ["foo"]
        | #/005/tests/007/data | false | array is invalid                                                                 |
        # []
        | #/005/tests/008/data | false | empty array is invalid                                                           |

Scenario Outline: allow everything with boolean schema false
/* Schema: 
{
            "$schema": "https://json-schema.org/draft/2019-09/schema",
            "not": false
        }
*/
    Given the input JSON file "not.json"
    And the schema at "#/6/schema"
    And the input data at "<inputDataReference>"
    And I generate a type for the schema
    And I construct an instance of the schema type from the data
    When I validate the instance
    Then the result will be <valid>

    Examples:
        | inputDataReference   | valid | description                                                                      |
        # 1
        | #/006/tests/000/data | true  | number is valid                                                                  |
        # foo
        | #/006/tests/001/data | true  | string is valid                                                                  |
        # True
        | #/006/tests/002/data | true  | boolean true is valid                                                            |
        # False
        | #/006/tests/003/data | true  | boolean false is valid                                                           |
        # 
        | #/006/tests/004/data | true  | null is valid                                                                    |
        # {"foo": "bar"}
        | #/006/tests/005/data | true  | object is valid                                                                  |
        # {}
        | #/006/tests/006/data | true  | empty object is valid                                                            |
        # ["foo"]
        | #/006/tests/007/data | true  | array is valid                                                                   |
        # []
        | #/006/tests/008/data | true  | empty array is valid                                                             |

Scenario Outline: double negation
/* Schema: 
{
            "$schema": "https://json-schema.org/draft/2019-09/schema",
            "not": { "not": {} }
        }
*/
    Given the input JSON file "not.json"
    And the schema at "#/7/schema"
    And the input data at "<inputDataReference>"
    And I generate a type for the schema
    And I construct an instance of the schema type from the data
    When I validate the instance
    Then the result will be <valid>

    Examples:
        | inputDataReference   | valid | description                                                                      |
        # foo
        | #/007/tests/000/data | true  | any value is valid                                                               |

Scenario Outline: collect annotations inside a 'not', even if collection is disabled
/* Schema: 
{
            "$schema": "https://json-schema.org/draft/2019-09/schema",
            "not": {
                "$comment": "this subschema must still produce annotations internally, even though the 'not' will ultimately discard them",
                "anyOf": [
                    true,
                    { "properties": { "foo": true } }
                ],
                "unevaluatedProperties": false
            }
        }
*/
    Given the input JSON file "not.json"
    And the schema at "#/8/schema"
    And the input data at "<inputDataReference>"
    And I generate a type for the schema
    And I construct an instance of the schema type from the data
    When I validate the instance
    Then the result will be <valid>

    Examples:
        | inputDataReference   | valid | description                                                                      |
        # { "bar": 1 }
        | #/008/tests/000/data | true  | unevaluated property                                                             |
        # { "foo": 1 }
        | #/008/tests/001/data | false | annotations are still collected inside a 'not'                                   |
