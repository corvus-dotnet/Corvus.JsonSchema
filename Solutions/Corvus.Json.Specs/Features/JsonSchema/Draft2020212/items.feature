@draft2020-12

Feature: items draft2020-12
    In order to use json-schema
    As a developer
    I want to support items in draft2020-12

Scenario Outline: a schema given for items
/* Schema: 
{
            "$schema": "https://json-schema.org/draft/2020-12/schema",
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

Scenario Outline: items with boolean schema (true)
/* Schema: 
{
            "$schema": "https://json-schema.org/draft/2020-12/schema",
            "items": true
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
        | #/001/tests/000/data | true  | any array is valid                                                               |
        | #/001/tests/001/data | true  | empty array is valid                                                             |

Scenario Outline: items with boolean schema (false)
/* Schema: 
{
            "$schema": "https://json-schema.org/draft/2020-12/schema",
            "items": false
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
        | #/002/tests/000/data | false | any non-empty array is invalid                                                   |
        | #/002/tests/001/data | true  | empty array is valid                                                             |

Scenario Outline: items and subitems
/* Schema: 
{
            "$schema": "https://json-schema.org/draft/2020-12/schema",
            "$defs": {
                "item": {
                    "type": "array",
                    "items": false,
                    "prefixItems": [
                        { "$ref": "#/$defs/sub-item" },
                        { "$ref": "#/$defs/sub-item" }
                    ]
                },
                "sub-item": {
                    "type": "object",
                    "required": ["foo"]
                }
            },
            "type": "array",
            "items": false,
            "prefixItems": [
                { "$ref": "#/$defs/item" },
                { "$ref": "#/$defs/item" },
                { "$ref": "#/$defs/item" }
            ]
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
        | #/003/tests/000/data | true  | valid items                                                                      |
        | #/003/tests/001/data | false | too many items                                                                   |
        | #/003/tests/002/data | false | too many sub-items                                                               |
        | #/003/tests/003/data | false | wrong item                                                                       |
        | #/003/tests/004/data | false | wrong sub-item                                                                   |
        | #/003/tests/005/data | true  | fewer items is valid                                                             |

Scenario Outline: nested items
/* Schema: 
{
            "$schema": "https://json-schema.org/draft/2020-12/schema",
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
    And the schema at "#/4/schema"
    And the input data at "<inputDataReference>"
    And I generate a type for the schema
    And I construct an instance of the schema type from the data
    When I validate the instance
    Then the result will be <valid>

    Examples:
        | inputDataReference   | valid | description                                                                      |
        | #/004/tests/000/data | true  | valid nested array                                                               |
        | #/004/tests/001/data | false | nested array with invalid type                                                   |
        | #/004/tests/002/data | false | not deep enough                                                                  |

Scenario Outline: prefixItems with no additional items allowed
/* Schema: 
{
            "$schema": "https://json-schema.org/draft/2020-12/schema",
            "prefixItems": [{}, {}, {}],
            "items": false
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
        | #/005/tests/000/data | true  | empty array                                                                      |
        | #/005/tests/001/data | true  | fewer number of items present (1)                                                |
        | #/005/tests/002/data | true  | fewer number of items present (2)                                                |
        | #/005/tests/003/data | true  | equal number of items present                                                    |
        | #/005/tests/004/data | false | additional items are not permitted                                               |

Scenario Outline: items does not look in applicators, valid case
/* Schema: 
{
            "$schema": "https://json-schema.org/draft/2020-12/schema",
            "allOf": [
                { "prefixItems": [ { "minimum": 3 } ] }
            ],
            "items": { "minimum": 5 }
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
        | #/006/tests/000/data | false | prefixItems in allOf does not constrain items, invalid case                      |
        | #/006/tests/001/data | true  | prefixItems in allOf does not constrain items, valid case                        |

Scenario Outline: prefixItems validation adjusts the starting index for items
/* Schema: 
{
            "$schema": "https://json-schema.org/draft/2020-12/schema",
            "prefixItems": [ { "type": "string" } ],
            "items": { "type": "integer" }
        }
*/
    Given the input JSON file "items.json"
    And the schema at "#/7/schema"
    And the input data at "<inputDataReference>"
    And I generate a type for the schema
    And I construct an instance of the schema type from the data
    When I validate the instance
    Then the result will be <valid>

    Examples:
        | inputDataReference   | valid | description                                                                      |
        | #/007/tests/000/data | true  | valid items                                                                      |
        | #/007/tests/001/data | false | wrong type of second item                                                        |

Scenario Outline: items with null instance elements
/* Schema: 
{
            "$schema": "https://json-schema.org/draft/2020-12/schema",
            "items": {
                "type": "null"
            }
        }
*/
    Given the input JSON file "items.json"
    And the schema at "#/8/schema"
    And the input data at "<inputDataReference>"
    And I generate a type for the schema
    And I construct an instance of the schema type from the data
    When I validate the instance
    Then the result will be <valid>

    Examples:
        | inputDataReference   | valid | description                                                                      |
        | #/008/tests/000/data | true  | allows null elements                                                             |
