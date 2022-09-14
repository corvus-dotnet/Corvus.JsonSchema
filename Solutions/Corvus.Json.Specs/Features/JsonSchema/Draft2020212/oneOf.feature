@draft2020-12

Feature: oneOf draft2020-12
    In order to use json-schema
    As a developer
    I want to support oneOf in draft2020-12

Scenario Outline: oneOf
/* Schema: 
{
            "$schema": "https://json-schema.org/draft/2020-12/schema",
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
        | #/000/tests/000/data | true  | first oneOf valid                                                                |
        | #/000/tests/001/data | true  | second oneOf valid                                                               |
        | #/000/tests/002/data | false | both oneOf valid                                                                 |
        | #/000/tests/003/data | false | neither oneOf valid                                                              |

Scenario Outline: oneOf with base schema
/* Schema: 
{
            "$schema": "https://json-schema.org/draft/2020-12/schema",
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
        | #/001/tests/000/data | false | mismatch base schema                                                             |
        | #/001/tests/001/data | true  | one oneOf valid                                                                  |
        | #/001/tests/002/data | false | both oneOf valid                                                                 |

Scenario Outline: oneOf with boolean schemas, all true
/* Schema: 
{
            "$schema": "https://json-schema.org/draft/2020-12/schema",
            "oneOf": [true, true, true]
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
        | #/002/tests/000/data | false | any value is invalid                                                             |

Scenario Outline: oneOf with boolean schemas, one true
/* Schema: 
{
            "$schema": "https://json-schema.org/draft/2020-12/schema",
            "oneOf": [true, false, false]
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
        | #/003/tests/000/data | true  | any value is valid                                                               |

Scenario Outline: oneOf with boolean schemas, more than one true
/* Schema: 
{
            "$schema": "https://json-schema.org/draft/2020-12/schema",
            "oneOf": [true, true, false]
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
        | #/004/tests/000/data | false | any value is invalid                                                             |

Scenario Outline: oneOf with boolean schemas, all false
/* Schema: 
{
            "$schema": "https://json-schema.org/draft/2020-12/schema",
            "oneOf": [false, false, false]
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
        | #/005/tests/000/data | false | any value is invalid                                                             |

Scenario Outline: oneOf complex types
/* Schema: 
{
            "$schema": "https://json-schema.org/draft/2020-12/schema",
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
    And the schema at "#/6/schema"
    And the input data at "<inputDataReference>"
    And I generate a type for the schema
    And I construct an instance of the schema type from the data
    When I validate the instance
    Then the result will be <valid>

    Examples:
        | inputDataReference   | valid | description                                                                      |
        | #/006/tests/000/data | true  | first oneOf valid (complex)                                                      |
        | #/006/tests/001/data | true  | second oneOf valid (complex)                                                     |
        | #/006/tests/002/data | false | both oneOf valid (complex)                                                       |
        | #/006/tests/003/data | false | neither oneOf valid (complex)                                                    |

Scenario Outline: oneOf with empty schema
/* Schema: 
{
            "$schema": "https://json-schema.org/draft/2020-12/schema",
            "oneOf": [
                { "type": "number" },
                {}
            ]
        }
*/
    Given the input JSON file "oneOf.json"
    And the schema at "#/7/schema"
    And the input data at "<inputDataReference>"
    And I generate a type for the schema
    And I construct an instance of the schema type from the data
    When I validate the instance
    Then the result will be <valid>

    Examples:
        | inputDataReference   | valid | description                                                                      |
        | #/007/tests/000/data | true  | one valid - valid                                                                |
        | #/007/tests/001/data | false | both valid - invalid                                                             |

Scenario Outline: oneOf with required
/* Schema: 
{
            "$schema": "https://json-schema.org/draft/2020-12/schema",
            "type": "object",
            "oneOf": [
                { "required": ["foo", "bar"] },
                { "required": ["foo", "baz"] }
            ]
        }
*/
    Given the input JSON file "oneOf.json"
    And the schema at "#/8/schema"
    And the input data at "<inputDataReference>"
    And I generate a type for the schema
    And I construct an instance of the schema type from the data
    When I validate the instance
    Then the result will be <valid>

    Examples:
        | inputDataReference   | valid | description                                                                      |
        | #/008/tests/000/data | false | both invalid - invalid                                                           |
        | #/008/tests/001/data | true  | first valid - valid                                                              |
        | #/008/tests/002/data | true  | second valid - valid                                                             |
        | #/008/tests/003/data | false | both valid - invalid                                                             |

Scenario Outline: oneOf with missing optional property
/* Schema: 
{
            "$schema": "https://json-schema.org/draft/2020-12/schema",
            "oneOf": [
                {
                    "properties": {
                        "bar": true,
                        "baz": true
                    },
                    "required": ["bar"]
                },
                {
                    "properties": {
                        "foo": true
                    },
                    "required": ["foo"]
                }
            ]
        }
*/
    Given the input JSON file "oneOf.json"
    And the schema at "#/9/schema"
    And the input data at "<inputDataReference>"
    And I generate a type for the schema
    And I construct an instance of the schema type from the data
    When I validate the instance
    Then the result will be <valid>

    Examples:
        | inputDataReference   | valid | description                                                                      |
        | #/009/tests/000/data | true  | first oneOf valid                                                                |
        | #/009/tests/001/data | true  | second oneOf valid                                                               |
        | #/009/tests/002/data | false | both oneOf valid                                                                 |
        | #/009/tests/003/data | false | neither oneOf valid                                                              |

Scenario Outline: nested oneOf, to check validation semantics
/* Schema: 
{
            "$schema": "https://json-schema.org/draft/2020-12/schema",
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
    And the schema at "#/10/schema"
    And the input data at "<inputDataReference>"
    And I generate a type for the schema
    And I construct an instance of the schema type from the data
    When I validate the instance
    Then the result will be <valid>

    Examples:
        | inputDataReference   | valid | description                                                                      |
        | #/010/tests/000/data | true  | null is valid                                                                    |
        | #/010/tests/001/data | false | anything non-null is invalid                                                     |
