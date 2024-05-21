@openApi30

Feature: additionalProperties openApi30
    In order to use json-schema
    As a developer
    I want to support additionalProperties in openApi30

Scenario Outline: additionalProperties being false does not allow other properties
/* Schema: 
{
            "properties": {"foo": {}, "bar": {}},
            "additionalProperties": false
        }
*/
    Given the input JSON file "additionalProperties.json"
    And the schema at "#/0/schema"
    And the input data at "<inputDataReference>"
    And I assert format
    And I generate a type for the schema
    And I construct an instance of the schema type from the data
    When I validate the instance
    Then the result will be <valid>

    Examples:
        | inputDataReference   | valid | description                                                                      |
        # {"foo": 1}
        | #/000/tests/000/data | true  | no additional properties is valid                                                |
        # {"foo" : 1, "bar" : 2, "quux" : "boom"}
        | #/000/tests/001/data | false | an additional property is invalid                                                |
        # [1, 2, 3]
        | #/000/tests/002/data | true  | ignores arrays                                                                   |
        # foobarbaz
        | #/000/tests/003/data | true  | ignores strings                                                                  |
        # 12
        | #/000/tests/004/data | true  | ignores other non-objects                                                        |

Scenario Outline: additionalProperties with schema
/* Schema: 
{
            "properties": {"foo": {}, "bar": {}},
            "additionalProperties": {"type": "boolean"}
        }
*/
    Given the input JSON file "additionalProperties.json"
    And the schema at "#/1/schema"
    And the input data at "<inputDataReference>"
    And I assert format
    And I generate a type for the schema
    And I construct an instance of the schema type from the data
    When I validate the instance
    Then the result will be <valid>

    Examples:
        | inputDataReference   | valid | description                                                                      |
        # {"foo": 1}
        | #/001/tests/000/data | true  | no additional properties is valid                                                |
        # {"foo" : 1, "bar" : 2, "quux" : true}
        | #/001/tests/001/data | true  | an additional valid property is valid                                            |
        # {"foo" : 1, "bar" : 2, "quux" : 12}
        | #/001/tests/002/data | false | an additional invalid property is invalid                                        |

Scenario Outline: additionalProperties can exist by itself
/* Schema: 
{
            "additionalProperties": {"type": "boolean"}
        }
*/
    Given the input JSON file "additionalProperties.json"
    And the schema at "#/2/schema"
    And the input data at "<inputDataReference>"
    And I assert format
    And I generate a type for the schema
    And I construct an instance of the schema type from the data
    When I validate the instance
    Then the result will be <valid>

    Examples:
        | inputDataReference   | valid | description                                                                      |
        # {"foo" : true}
        | #/002/tests/000/data | true  | an additional valid property is valid                                            |
        # {"foo" : 1}
        | #/002/tests/001/data | false | an additional invalid property is invalid                                        |

Scenario Outline: additionalProperties are allowed by default
/* Schema: 
{"properties": {"foo": {}, "bar": {}}}
*/
    Given the input JSON file "additionalProperties.json"
    And the schema at "#/3/schema"
    And the input data at "<inputDataReference>"
    And I assert format
    And I generate a type for the schema
    And I construct an instance of the schema type from the data
    When I validate the instance
    Then the result will be <valid>

    Examples:
        | inputDataReference   | valid | description                                                                      |
        # {"foo": 1, "bar": 2, "quux": true}
        | #/003/tests/000/data | true  | additional properties are allowed                                                |

Scenario Outline: additionalProperties does not look in applicators
/* Schema: 
{
            "allOf": [
                {"properties": {"foo": {}}}
            ],
            "additionalProperties": {"type": "boolean"}
        }
*/
    Given the input JSON file "additionalProperties.json"
    And the schema at "#/4/schema"
    And the input data at "<inputDataReference>"
    And I assert format
    And I generate a type for the schema
    And I construct an instance of the schema type from the data
    When I validate the instance
    Then the result will be <valid>

    Examples:
        | inputDataReference   | valid | description                                                                      |
        # {"foo": 1, "bar": true}
        | #/004/tests/000/data | false | properties defined in allOf are not examined                                     |

Scenario Outline: additionalProperties with null valued instance properties
/* Schema: 
{
            "additionalProperties": {
                "nullable": true
            }
        }
*/
    Given the input JSON file "additionalProperties.json"
    And the schema at "#/5/schema"
    And the input data at "<inputDataReference>"
    And I assert format
    And I generate a type for the schema
    And I construct an instance of the schema type from the data
    When I validate the instance
    Then the result will be <valid>

    Examples:
        | inputDataReference   | valid | description                                                                      |
        # {"foo": null}
        | #/005/tests/000/data | true  | allows null values                                                               |
