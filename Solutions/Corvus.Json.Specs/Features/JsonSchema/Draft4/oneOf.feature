@draft4

Feature: oneOf draft4
    In order to use json-schema
    As a developer
    I want to support oneOf in draft4

Scenario Outline: oneOf
/* Schema: 
{
            "oneOf": [
                {
                    "type": "integer"
                },
                {
                    "minimum": 2
                }
            ]
        }
*/
    Given the input JSON file "oneOf.json"
    And the schema at "#/0/schema"
    And the input data at "<inputDataReference>"
    And I generate a type for the schema
    And I construct an instance of the schema type from the data
    When I validate the instance
    Then the result will be <valid>

    Examples:
        | inputDataReference   | valid | description                                                                      |
        # 1
        | #/000/tests/000/data | true  | first oneOf valid                                                                |
        # 2.5
        | #/000/tests/001/data | true  | second oneOf valid                                                               |
        # 3
        | #/000/tests/002/data | false | both oneOf valid                                                                 |
        # 1.5
        | #/000/tests/003/data | false | neither oneOf valid                                                              |

Scenario Outline: oneOf with base schema
/* Schema: 
{
            "type": "string",
            "oneOf" : [
                {
                    "minLength": 2
                },
                {
                    "maxLength": 4
                }
            ]
        }
*/
    Given the input JSON file "oneOf.json"
    And the schema at "#/1/schema"
    And the input data at "<inputDataReference>"
    And I generate a type for the schema
    And I construct an instance of the schema type from the data
    When I validate the instance
    Then the result will be <valid>

    Examples:
        | inputDataReference   | valid | description                                                                      |
        # 3
        | #/001/tests/000/data | false | mismatch base schema                                                             |
        # foobar
        | #/001/tests/001/data | true  | one oneOf valid                                                                  |
        # foo
        | #/001/tests/002/data | false | both oneOf valid                                                                 |

Scenario Outline: oneOf complex types
/* Schema: 
{
            "oneOf": [
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
    Given the input JSON file "oneOf.json"
    And the schema at "#/2/schema"
    And the input data at "<inputDataReference>"
    And I generate a type for the schema
    And I construct an instance of the schema type from the data
    When I validate the instance
    Then the result will be <valid>

    Examples:
        | inputDataReference   | valid | description                                                                      |
        # {"bar": 2}
        | #/002/tests/000/data | true  | first oneOf valid (complex)                                                      |
        # {"foo": "baz"}
        | #/002/tests/001/data | true  | second oneOf valid (complex)                                                     |
        # {"foo": "baz", "bar": 2}
        | #/002/tests/002/data | false | both oneOf valid (complex)                                                       |
        # {"foo": 2, "bar": "quux"}
        | #/002/tests/003/data | false | neither oneOf valid (complex)                                                    |

Scenario Outline: oneOf with empty schema
/* Schema: 
{
            "oneOf": [
                { "type": "number" },
                {}
            ]
        }
*/
    Given the input JSON file "oneOf.json"
    And the schema at "#/3/schema"
    And the input data at "<inputDataReference>"
    And I generate a type for the schema
    And I construct an instance of the schema type from the data
    When I validate the instance
    Then the result will be <valid>

    Examples:
        | inputDataReference   | valid | description                                                                      |
        # foo
        | #/003/tests/000/data | true  | one valid - valid                                                                |
        # 123
        | #/003/tests/001/data | false | both valid - invalid                                                             |

Scenario Outline: oneOf with required
/* Schema: 
{
            "type": "object",
            "oneOf": [
                { "required": ["foo", "bar"] },
                { "required": ["foo", "baz"] }
            ]
        }
*/
    Given the input JSON file "oneOf.json"
    And the schema at "#/4/schema"
    And the input data at "<inputDataReference>"
    And I generate a type for the schema
    And I construct an instance of the schema type from the data
    When I validate the instance
    Then the result will be <valid>

    Examples:
        | inputDataReference   | valid | description                                                                      |
        # {"bar": 2}
        | #/004/tests/000/data | false | both invalid - invalid                                                           |
        # {"foo": 1, "bar": 2}
        | #/004/tests/001/data | true  | first valid - valid                                                              |
        # {"foo": 1, "baz": 3}
        | #/004/tests/002/data | true  | second valid - valid                                                             |
        # {"foo": 1, "bar": 2, "baz" : 3}
        | #/004/tests/003/data | false | both valid - invalid                                                             |

Scenario Outline: oneOf with missing optional property
/* Schema: 
{
            "oneOf": [
                {
                    "properties": {
                        "bar": {},
                        "baz": {}
                    },
                    "required": ["bar"]
                },
                {
                    "properties": {
                        "foo": {}
                    },
                    "required": ["foo"]
                }
            ]
        }
*/
    Given the input JSON file "oneOf.json"
    And the schema at "#/5/schema"
    And the input data at "<inputDataReference>"
    And I generate a type for the schema
    And I construct an instance of the schema type from the data
    When I validate the instance
    Then the result will be <valid>

    Examples:
        | inputDataReference   | valid | description                                                                      |
        # {"bar": 8}
        | #/005/tests/000/data | true  | first oneOf valid                                                                |
        # {"foo": "foo"}
        | #/005/tests/001/data | true  | second oneOf valid                                                               |
        # {"foo": "foo", "bar": 8}
        | #/005/tests/002/data | false | both oneOf valid                                                                 |
        # {"baz": "quux"}
        | #/005/tests/003/data | false | neither oneOf valid                                                              |

Scenario Outline: nested oneOf, to check validation semantics
/* Schema: 
{
            "oneOf": [
                {
                    "oneOf": [
                        {
                            "type": "null"
                        }
                    ]
                }
            ]
        }
*/
    Given the input JSON file "oneOf.json"
    And the schema at "#/6/schema"
    And the input data at "<inputDataReference>"
    And I generate a type for the schema
    And I construct an instance of the schema type from the data
    When I validate the instance
    Then the result will be <valid>

    Examples:
        | inputDataReference   | valid | description                                                                      |
        # 
        | #/006/tests/000/data | true  | null is valid                                                                    |
        # 123
        | #/006/tests/001/data | false | anything non-null is invalid                                                     |
