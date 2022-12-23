@draft2020-12

Feature: additionalProperties draft2020-12
    In order to use json-schema
    As a developer
    I want to support additionalProperties in draft2020-12

Scenario Outline: additionalProperties being false does not allow other properties
/* Schema: 
{
            "$schema": "https://json-schema.org/draft/2020-12/schema",
            "properties": {"foo": {}, "bar": {}},
            "patternProperties": { "^v": {} },
            "additionalProperties": false
        }
*/
    Given the input JSON file "additionalProperties.json"
    And the schema at "#/0/schema"
    And the input data at "<inputDataReference>"
    And I generate a type for the schema
    And I construct an instance of the schema type from the data
    When I validate the instance
    Then the result will be <valid>

    Examples:
        | inputDataReference   | valid | description                                                                      |
        | #/000/tests/000/data | true  | no additional properties is valid                                                |
        | #/000/tests/001/data | false | an additional property is invalid                                                |
        | #/000/tests/002/data | true  | ignores arrays                                                                   |
        | #/000/tests/003/data | true  | ignores strings                                                                  |
        | #/000/tests/004/data | true  | ignores other non-objects                                                        |
        | #/000/tests/005/data | true  | patternProperties are not additional properties                                  |

Scenario Outline: non-ASCII pattern with additionalProperties
/* Schema: 
{
            "$schema": "https://json-schema.org/draft/2020-12/schema",
            "patternProperties": {"^á": {}},
            "additionalProperties": false
        }
*/
    Given the input JSON file "additionalProperties.json"
    And the schema at "#/1/schema"
    And the input data at "<inputDataReference>"
    And I generate a type for the schema
    And I construct an instance of the schema type from the data
    When I validate the instance
    Then the result will be <valid>

    Examples:
        | inputDataReference   | valid | description                                                                      |
        | #/001/tests/000/data | true  | matching the pattern is valid                                                    |
        | #/001/tests/001/data | false | not matching the pattern is invalid                                              |

Scenario Outline: additionalProperties with schema
/* Schema: 
{
            "$schema": "https://json-schema.org/draft/2020-12/schema",
            "properties": {"foo": {}, "bar": {}},
            "additionalProperties": {"type": "boolean"}
        }
*/
    Given the input JSON file "additionalProperties.json"
    And the schema at "#/2/schema"
    And the input data at "<inputDataReference>"
    And I generate a type for the schema
    And I construct an instance of the schema type from the data
    When I validate the instance
    Then the result will be <valid>

    Examples:
        | inputDataReference   | valid | description                                                                      |
        | #/002/tests/000/data | true  | no additional properties is valid                                                |
        | #/002/tests/001/data | true  | an additional valid property is valid                                            |
        | #/002/tests/002/data | false | an additional invalid property is invalid                                        |

Scenario Outline: additionalProperties can exist by itself
/* Schema: 
{
            "$schema": "https://json-schema.org/draft/2020-12/schema",
            "additionalProperties": {"type": "boolean"}
        }
*/
    Given the input JSON file "additionalProperties.json"
    And the schema at "#/3/schema"
    And the input data at "<inputDataReference>"
    And I generate a type for the schema
    And I construct an instance of the schema type from the data
    When I validate the instance
    Then the result will be <valid>

    Examples:
        | inputDataReference   | valid | description                                                                      |
        | #/003/tests/000/data | true  | an additional valid property is valid                                            |
        | #/003/tests/001/data | false | an additional invalid property is invalid                                        |

Scenario Outline: additionalProperties are allowed by default
/* Schema: 
{
            "$schema": "https://json-schema.org/draft/2020-12/schema",
            "properties": {"foo": {}, "bar": {}}
        }
*/
    Given the input JSON file "additionalProperties.json"
    And the schema at "#/4/schema"
    And the input data at "<inputDataReference>"
    And I generate a type for the schema
    And I construct an instance of the schema type from the data
    When I validate the instance
    Then the result will be <valid>

    Examples:
        | inputDataReference   | valid | description                                                                      |
        | #/004/tests/000/data | true  | additional properties are allowed                                                |

Scenario Outline: additionalProperties does not look in applicators
/* Schema: 
{
            "$schema": "https://json-schema.org/draft/2020-12/schema",
            "allOf": [
                {"properties": {"foo": {}}}
            ],
            "additionalProperties": {"type": "boolean"}
        }
*/
    Given the input JSON file "additionalProperties.json"
    And the schema at "#/5/schema"
    And the input data at "<inputDataReference>"
    And I generate a type for the schema
    And I construct an instance of the schema type from the data
    When I validate the instance
    Then the result will be <valid>

    Examples:
        | inputDataReference   | valid | description                                                                      |
        | #/005/tests/000/data | false | properties defined in allOf are not examined                                     |

Scenario Outline: additionalProperties with null valued instance properties
/* Schema: 
{
            "$schema": "https://json-schema.org/draft/2020-12/schema",
            "additionalProperties": {
                "type": "null"
            }
        }
*/
    Given the input JSON file "additionalProperties.json"
    And the schema at "#/6/schema"
    And the input data at "<inputDataReference>"
    And I generate a type for the schema
    And I construct an instance of the schema type from the data
    When I validate the instance
    Then the result will be <valid>

    Examples:
        | inputDataReference   | valid | description                                                                      |
        | #/006/tests/000/data | true  | allows null values                                                               |
