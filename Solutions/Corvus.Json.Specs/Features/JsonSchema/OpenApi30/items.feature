@openApi30

Feature: items openApi30
    In order to use json-schema
    As a developer
    I want to support items in openApi30

Scenario Outline: a schema given for items
/* Schema: 
{
            "items": {"type": "integer"}
        }
*/
    Given the input JSON file "items.json"
    And the schema at "#/0/schema"
    And the input data at "<inputDataReference>"
    And I generate a type for the schema
    And I construct an instance of the schema type from the data
    When I validate the instance
    Then the result will be <valid>

    Examples:
        | inputDataReference   | valid | description                                                                      |
        # [ 1, 2, 3 ]
        | #/000/tests/000/data | true  | valid items                                                                      |
        # [1, "x"]
        | #/000/tests/001/data | false | wrong type of items                                                              |
        # {"foo" : "bar"}
        | #/000/tests/002/data | true  | ignores non-arrays                                                               |
        # { "0": "invalid", "length": 1 }
        | #/000/tests/003/data | true  | JavaScript pseudo-array is valid                                                 |

Scenario Outline: nested items
/* Schema: 
{
            "type": "array",
            "items": {
                "type": "array",
                "items": {
                    "type": "array",
                    "items": {
                        "type": "array",
                        "items": {
                            "type": "number"
                        }
                    }
                }
            }
        }
*/
    Given the input JSON file "items.json"
    And the schema at "#/1/schema"
    And the input data at "<inputDataReference>"
    And I generate a type for the schema
    And I construct an instance of the schema type from the data
    When I validate the instance
    Then the result will be <valid>

    Examples:
        | inputDataReference   | valid | description                                                                      |
        # [[[[1]], [[2],[3]]], [[[4], [5], [6]]]]
        | #/001/tests/000/data | true  | valid nested array                                                               |
        # [[[["1"]], [[2],[3]]], [[[4], [5], [6]]]]
        | #/001/tests/001/data | false | nested array with invalid type                                                   |
        # [[[1], [2],[3]], [[4], [5], [6]]]
        | #/001/tests/002/data | false | not deep enough                                                                  |

Scenario Outline: items with null instance elements
/* Schema: 
{
            "items": {
                "nullable": true
            }
        }
*/
    Given the input JSON file "items.json"
    And the schema at "#/2/schema"
    And the input data at "<inputDataReference>"
    And I generate a type for the schema
    And I construct an instance of the schema type from the data
    When I validate the instance
    Then the result will be <valid>

    Examples:
        | inputDataReference   | valid | description                                                                      |
        # [ null ]
        | #/002/tests/000/data | true  | allows null elements                                                             |
