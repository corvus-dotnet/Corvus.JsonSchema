@draft7

Feature: items draft7
    In order to use json-schema
    As a developer
    I want to support items in draft7

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
        | #/000/tests/000/data | true  | valid items                                                                      |
        | #/000/tests/001/data | false | wrong type of items                                                              |
        | #/000/tests/002/data | true  | ignores non-arrays                                                               |
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
        | #/001/tests/000/data | true  | correct types                                                                    |
        | #/001/tests/001/data | false | wrong types                                                                      |
        | #/001/tests/002/data | true  | incomplete array of items                                                        |
        | #/001/tests/003/data | true  | array with additional items                                                      |
        | #/001/tests/004/data | true  | empty array                                                                      |
        | #/001/tests/005/data | true  | JavaScript pseudo-array is valid                                                 |

Scenario Outline: items with boolean schema (true)
/* Schema: 
{"items": true}
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
        | #/002/tests/000/data | true  | any array is valid                                                               |
        | #/002/tests/001/data | true  | empty array is valid                                                             |

Scenario Outline: items with boolean schema (false)
/* Schema: 
{"items": false}
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
        | #/003/tests/000/data | false | any non-empty array is invalid                                                   |
        | #/003/tests/001/data | true  | empty array is valid                                                             |

Scenario Outline: items with boolean schemas
/* Schema: 
{
            "items": [true, false]
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
        | #/004/tests/000/data | true  | array with one item is valid                                                     |
        | #/004/tests/001/data | false | array with two items is invalid                                                  |
        | #/004/tests/002/data | true  | empty array is valid                                                             |

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
    And the schema at "#/5/schema"
    And the input data at "<inputDataReference>"
    And I generate a type for the schema
    And I construct an instance of the schema type from the data
    When I validate the instance
    Then the result will be <valid>

    Examples:
        | inputDataReference   | valid | description                                                                      |
        | #/005/tests/000/data | true  | valid items                                                                      |
        | #/005/tests/001/data | false | too many items                                                                   |
        | #/005/tests/002/data | false | too many sub-items                                                               |
        | #/005/tests/003/data | false | wrong item                                                                       |
        | #/005/tests/004/data | false | wrong sub-item                                                                   |
        | #/005/tests/005/data | true  | fewer items is valid                                                             |

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
    And the schema at "#/6/schema"
    And the input data at "<inputDataReference>"
    And I generate a type for the schema
    And I construct an instance of the schema type from the data
    When I validate the instance
    Then the result will be <valid>

    Examples:
        | inputDataReference   | valid | description                                                                      |
        | #/006/tests/000/data | true  | valid nested array                                                               |
        | #/006/tests/001/data | false | nested array with invalid type                                                   |
        | #/006/tests/002/data | false | not deep enough                                                                  |
