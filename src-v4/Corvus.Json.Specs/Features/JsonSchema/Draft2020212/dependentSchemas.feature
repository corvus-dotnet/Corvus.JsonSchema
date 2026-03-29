@draft2020-12

Feature: dependentSchemas draft2020-12
    In order to use json-schema
    As a developer
    I want to support dependentSchemas in draft2020-12

Scenario Outline: single dependency
/* Schema: 
{
            "$schema": "https://json-schema.org/draft/2020-12/schema",
            "dependentSchemas": {
                "bar": {
                    "properties": {
                        "foo": {"type": "integer"},
                        "bar": {"type": "integer"}
                    }
                }
            }
        }
*/
    Given the input JSON file "dependentSchemas.json"
    And the schema at "#/0/schema"
    And the input data at "<inputDataReference>"
    And I generate a type for the schema
    And I construct an instance of the schema type from the data
    When I validate the instance
    Then the result will be <valid>

    Examples:
        | inputDataReference   | valid | description                                                                      |
        # {"foo": 1, "bar": 2}
        | #/000/tests/000/data | true  | valid                                                                            |
        # {"foo": "quux"}
        | #/000/tests/001/data | true  | no dependency                                                                    |
        # {"foo": "quux", "bar": 2}
        | #/000/tests/002/data | false | wrong type                                                                       |
        # {"foo": 2, "bar": "quux"}
        | #/000/tests/003/data | false | wrong type other                                                                 |
        # {"foo": "quux", "bar": "quux"}
        | #/000/tests/004/data | false | wrong type both                                                                  |
        # ["bar"]
        | #/000/tests/005/data | true  | ignores arrays                                                                   |
        # foobar
        | #/000/tests/006/data | true  | ignores strings                                                                  |
        # 12
        | #/000/tests/007/data | true  | ignores other non-objects                                                        |

Scenario Outline: boolean subschemas
/* Schema: 
{
            "$schema": "https://json-schema.org/draft/2020-12/schema",
            "dependentSchemas": {
                "foo": true,
                "bar": false
            }
        }
*/
    Given the input JSON file "dependentSchemas.json"
    And the schema at "#/1/schema"
    And the input data at "<inputDataReference>"
    And I generate a type for the schema
    And I construct an instance of the schema type from the data
    When I validate the instance
    Then the result will be <valid>

    Examples:
        | inputDataReference   | valid | description                                                                      |
        # {"foo": 1}
        | #/001/tests/000/data | true  | object with property having schema true is valid                                 |
        # {"bar": 2}
        | #/001/tests/001/data | false | object with property having schema false is invalid                              |
        # {"foo": 1, "bar": 2}
        | #/001/tests/002/data | false | object with both properties is invalid                                           |
        # {}
        | #/001/tests/003/data | true  | empty object is valid                                                            |

Scenario Outline: dependencies with escaped characters
/* Schema: 
{
            "$schema": "https://json-schema.org/draft/2020-12/schema",
            "dependentSchemas": {
                "foo\tbar": {"minProperties": 4},
                "foo'bar": {"required": ["foo\"bar"]}
            }
        }
*/
    Given the input JSON file "dependentSchemas.json"
    And the schema at "#/2/schema"
    And the input data at "<inputDataReference>"
    And I generate a type for the schema
    And I construct an instance of the schema type from the data
    When I validate the instance
    Then the result will be <valid>

    Examples:
        | inputDataReference   | valid | description                                                                      |
        # { "foo\tbar": 1, "a": 2, "b": 3, "c": 4 }
        | #/002/tests/000/data | true  | quoted tab                                                                       |
        # { "foo'bar": {"foo\"bar": 1} }
        | #/002/tests/001/data | false | quoted quote                                                                     |
        # { "foo\tbar": 1, "a": 2 }
        | #/002/tests/002/data | false | quoted tab invalid under dependent schema                                        |
        # {"foo'bar": 1}
        | #/002/tests/003/data | false | quoted quote invalid under dependent schema                                      |

Scenario Outline: dependent subschema incompatible with root
/* Schema: 
{
            "$schema": "https://json-schema.org/draft/2020-12/schema",
            "properties": {
                "foo": {}
            },
            "dependentSchemas": {
                "foo": {
                    "properties": {
                        "bar": {}
                    },
                    "additionalProperties": false
                }
            }
        }
*/
    Given the input JSON file "dependentSchemas.json"
    And the schema at "#/3/schema"
    And the input data at "<inputDataReference>"
    And I generate a type for the schema
    And I construct an instance of the schema type from the data
    When I validate the instance
    Then the result will be <valid>

    Examples:
        | inputDataReference   | valid | description                                                                      |
        # {"foo": 1}
        | #/003/tests/000/data | false | matches root                                                                     |
        # {"bar": 1}
        | #/003/tests/001/data | true  | matches dependency                                                               |
        # {"foo": 1, "bar": 2}
        | #/003/tests/002/data | false | matches both                                                                     |
        # {"baz": 1}
        | #/003/tests/003/data | true  | no dependency                                                                    |
