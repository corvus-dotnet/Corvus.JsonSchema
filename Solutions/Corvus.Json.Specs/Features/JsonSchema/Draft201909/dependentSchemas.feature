@draft2019-09

Feature: dependentSchemas draft2019-09
    In order to use json-schema
    As a developer
    I want to support dependentSchemas in draft2019-09

Scenario Outline: single dependency
/* Schema: 
{
            "$schema": "https://json-schema.org/draft/2019-09/schema",
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
        | #/000/tests/000/data | true  | valid                                                                            |
        | #/000/tests/001/data | true  | no dependency                                                                    |
        | #/000/tests/002/data | false | wrong type                                                                       |
        | #/000/tests/003/data | false | wrong type other                                                                 |
        | #/000/tests/004/data | false | wrong type both                                                                  |
        | #/000/tests/005/data | true  | ignores arrays                                                                   |
        | #/000/tests/006/data | true  | ignores strings                                                                  |
        | #/000/tests/007/data | true  | ignores other non-objects                                                        |

Scenario Outline: boolean subschemas
/* Schema: 
{
            "$schema": "https://json-schema.org/draft/2019-09/schema",
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
        | #/001/tests/000/data | true  | object with property having schema true is valid                                 |
        | #/001/tests/001/data | false | object with property having schema false is invalid                              |
        | #/001/tests/002/data | false | object with both properties is invalid                                           |
        | #/001/tests/003/data | true  | empty object is valid                                                            |

Scenario Outline: dependencies with escaped characters
/* Schema: 
{
            "$schema": "https://json-schema.org/draft/2019-09/schema",
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
        | #/002/tests/000/data | true  | quoted tab                                                                       |
        | #/002/tests/001/data | false | quoted quote                                                                     |
        | #/002/tests/002/data | false | quoted tab invalid under dependent schema                                        |
        | #/002/tests/003/data | false | quoted quote invalid under dependent schema                                      |

Scenario Outline: dependent subschema incompatible with root
/* Schema: 
{
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
        | #/003/tests/000/data | false | matches root                                                                     |
        | #/003/tests/001/data | true  | matches dependency                                                               |
        | #/003/tests/002/data | false | matches both                                                                     |
        | #/003/tests/003/data | true  | no dependency                                                                    |
