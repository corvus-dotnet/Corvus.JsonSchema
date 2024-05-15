@draft4

Feature: items draft4
    In order to use json-schema
    As a developer
    I want to support items in draft4

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

Scenario Outline: an array of schemas for items
/* Schema: 
{
            "items": [
                {"type": "integer"},
                {"type": "string"}
            ]
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
        # [ 1, "foo" ]
        | #/001/tests/000/data | true  | correct types                                                                    |
        # [ "foo", 1 ]
        | #/001/tests/001/data | false | wrong types                                                                      |
        # [ 1 ]
        | #/001/tests/002/data | true  | incomplete array of items                                                        |
        # [ 1, "foo", true ]
        | #/001/tests/003/data | true  | array with additional items                                                      |
        # [ ]
        | #/001/tests/004/data | true  | empty array                                                                      |
        # { "0": "invalid", "1": "valid", "length": 2 }
        | #/001/tests/005/data | true  | JavaScript pseudo-array is valid                                                 |

Scenario Outline: items and subitems
/* Schema: 
{
            "definitions": {
                "item": {
                    "type": "array",
                    "additionalItems": false,
                    "items": [
                        { "$ref": "#/definitions/sub-item" },
                        { "$ref": "#/definitions/sub-item" }
                    ]
                },
                "sub-item": {
                    "type": "object",
                    "required": ["foo"]
                }
            },
            "type": "array",
            "additionalItems": false,
            "items": [
                { "$ref": "#/definitions/item" },
                { "$ref": "#/definitions/item" },
                { "$ref": "#/definitions/item" }
            ]
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
        # [ [ {"foo": null}, {"foo": null} ], [ {"foo": null}, {"foo": null} ], [ {"foo": null}, {"foo": null} ] ]
        | #/002/tests/000/data | true  | valid items                                                                      |
        # [ [ {"foo": null}, {"foo": null} ], [ {"foo": null}, {"foo": null} ], [ {"foo": null}, {"foo": null} ], [ {"foo": null}, {"foo": null} ] ]
        | #/002/tests/001/data | false | too many items                                                                   |
        # [ [ {"foo": null}, {"foo": null}, {"foo": null} ], [ {"foo": null}, {"foo": null} ], [ {"foo": null}, {"foo": null} ] ]
        | #/002/tests/002/data | false | too many sub-items                                                               |
        # [ {"foo": null}, [ {"foo": null}, {"foo": null} ], [ {"foo": null}, {"foo": null} ] ]
        | #/002/tests/003/data | false | wrong item                                                                       |
        # [ [ {}, {"foo": null} ], [ {"foo": null}, {"foo": null} ], [ {"foo": null}, {"foo": null} ] ]
        | #/002/tests/004/data | false | wrong sub-item                                                                   |
        # [ [ {"foo": null} ], [ {"foo": null} ] ]
        | #/002/tests/005/data | true  | fewer items is valid                                                             |

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
    And the schema at "#/3/schema"
    And the input data at "<inputDataReference>"
    And I generate a type for the schema
    And I construct an instance of the schema type from the data
    When I validate the instance
    Then the result will be <valid>

    Examples:
        | inputDataReference   | valid | description                                                                      |
        # [[[[1]], [[2],[3]]], [[[4], [5], [6]]]]
        | #/003/tests/000/data | true  | valid nested array                                                               |
        # [[[["1"]], [[2],[3]]], [[[4], [5], [6]]]]
        | #/003/tests/001/data | false | nested array with invalid type                                                   |
        # [[[1], [2],[3]], [[4], [5], [6]]]
        | #/003/tests/002/data | false | not deep enough                                                                  |

Scenario Outline: items with null instance elements
/* Schema: 
{
            "items": {
                "type": "null"
            }
        }
*/
    Given the input JSON file "items.json"
    And the schema at "#/4/schema"
    And the input data at "<inputDataReference>"
    And I generate a type for the schema
    And I construct an instance of the schema type from the data
    When I validate the instance
    Then the result will be <valid>

    Examples:
        | inputDataReference   | valid | description                                                                      |
        # [ null ]
        | #/004/tests/000/data | true  | allows null elements                                                             |

Scenario Outline: array-form items with null instance elements
/* Schema: 
{
            "items": [
                {
                    "type": "null"
                }
            ]
        }
*/
    Given the input JSON file "items.json"
    And the schema at "#/5/schema"
    And the input data at "<inputDataReference>"
    And I generate a type for the schema
    And I construct an instance of the schema type from the data
    When I validate the instance
    Then the result will be <valid>

    Examples:
        | inputDataReference   | valid | description                                                                      |
        # [ null ]
        | #/005/tests/000/data | true  | allows null elements                                                             |
