@draft2019-09

Feature: default draft2019-09
    In order to use json-schema
    As a developer
    I want to support default in draft2019-09

Scenario Outline: invalid type for default
/* Schema: 
{
            "$schema": "https://json-schema.org/draft/2019-09/schema",
            "properties": {
                "foo": {
                    "type": "integer",
                    "default": []
                }
            }
        }
*/
    Given the input JSON file "default.json"
    And the schema at "#/0/schema"
    And the input data at "<inputDataReference>"
    And I assert format
    And I generate a type for the schema
    And I construct an instance of the schema type from the data
    When I validate the instance
    Then the result will be <valid>

    Examples:
        | inputDataReference   | valid | description                                                                      |
        # {"foo": 13}
        | #/000/tests/000/data | true  | valid when property is specified                                                 |
        # {}
        | #/000/tests/001/data | true  | still valid when the invalid default is used                                     |

Scenario Outline: invalid string value for default
/* Schema: 
{
            "$schema": "https://json-schema.org/draft/2019-09/schema",
            "properties": {
                "bar": {
                    "type": "string",
                    "minLength": 4,
                    "default": "bad"
                }
            }
        }
*/
    Given the input JSON file "default.json"
    And the schema at "#/1/schema"
    And the input data at "<inputDataReference>"
    And I assert format
    And I generate a type for the schema
    And I construct an instance of the schema type from the data
    When I validate the instance
    Then the result will be <valid>

    Examples:
        | inputDataReference   | valid | description                                                                      |
        # {"bar": "good"}
        | #/001/tests/000/data | true  | valid when property is specified                                                 |
        # {}
        | #/001/tests/001/data | true  | still valid when the invalid default is used                                     |

Scenario Outline: the default keyword does not do anything if the property is missing
/* Schema: 
{
            "$schema": "https://json-schema.org/draft/2019-09/schema",
            "type": "object",
            "properties": {
                "alpha": {
                    "type": "number",
                    "maximum": 3,
                    "default": 5
                }
            }
        }
*/
    Given the input JSON file "default.json"
    And the schema at "#/2/schema"
    And the input data at "<inputDataReference>"
    And I assert format
    And I generate a type for the schema
    And I construct an instance of the schema type from the data
    When I validate the instance
    Then the result will be <valid>

    Examples:
        | inputDataReference   | valid | description                                                                      |
        # { "alpha": 1 }
        | #/002/tests/000/data | true  | an explicit property value is checked against maximum (passing)                  |
        # { "alpha": 5 }
        | #/002/tests/001/data | false | an explicit property value is checked against maximum (failing)                  |
        # {}
        | #/002/tests/002/data | true  | missing properties are not filled in with the default                            |
