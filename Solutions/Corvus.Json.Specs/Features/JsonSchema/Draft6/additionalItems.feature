@draft6

Feature: additionalItems draft6
    In order to use json-schema
    As a developer
    I want to support additionalItems in draft6

Scenario Outline: additionalItems as schema
/* Schema: 
{
            "items": [{}],
            "additionalItems": {"type": "integer"}
        }
*/
    Given the input JSON file "additionalItems.json"
    And the schema at "#/0/schema"
    And the input data at "<inputDataReference>"
    And I generate a type for the schema
    And I construct an instance of the schema type from the data
    When I validate the instance
    Then the result will be <valid>

    Examples:
        | inputDataReference   | valid | description                                                                      |
        | #/000/tests/000/data | true  | additional items match schema                                                    |
        | #/000/tests/001/data | false | additional items do not match schema                                             |

Scenario Outline: when items is schema, additionalItems does nothing
/* Schema: 
{
            "items": {
                "type": "integer"
            },
            "additionalItems": {
                "type": "string"
            }
        }
*/
    Given the input JSON file "additionalItems.json"
    And the schema at "#/1/schema"
    And the input data at "<inputDataReference>"
    And I generate a type for the schema
    And I construct an instance of the schema type from the data
    When I validate the instance
    Then the result will be <valid>

    Examples:
        | inputDataReference   | valid | description                                                                      |
        | #/001/tests/000/data | true  | valid with a array of type integers                                              |
        | #/001/tests/001/data | false | invalid with a array of mixed types                                              |

Scenario Outline: when items is schema, boolean additionalItems does nothing
/* Schema: 
{
            "items": {},
            "additionalItems": false
        }
*/
    Given the input JSON file "additionalItems.json"
    And the schema at "#/2/schema"
    And the input data at "<inputDataReference>"
    And I generate a type for the schema
    And I construct an instance of the schema type from the data
    When I validate the instance
    Then the result will be <valid>

    Examples:
        | inputDataReference   | valid | description                                                                      |
        | #/002/tests/000/data | true  | all items match schema                                                           |

Scenario Outline: array of items with no additionalItems permitted
/* Schema: 
{
            "items": [{}, {}, {}],
            "additionalItems": false
        }
*/
    Given the input JSON file "additionalItems.json"
    And the schema at "#/3/schema"
    And the input data at "<inputDataReference>"
    And I generate a type for the schema
    And I construct an instance of the schema type from the data
    When I validate the instance
    Then the result will be <valid>

    Examples:
        | inputDataReference   | valid | description                                                                      |
        | #/003/tests/000/data | true  | empty array                                                                      |
        | #/003/tests/001/data | true  | fewer number of items present (1)                                                |
        | #/003/tests/002/data | true  | fewer number of items present (2)                                                |
        | #/003/tests/003/data | true  | equal number of items present                                                    |
        | #/003/tests/004/data | false | additional items are not permitted                                               |

Scenario Outline: additionalItems as false without items
/* Schema: 
{"additionalItems": false}
*/
    Given the input JSON file "additionalItems.json"
    And the schema at "#/4/schema"
    And the input data at "<inputDataReference>"
    And I generate a type for the schema
    And I construct an instance of the schema type from the data
    When I validate the instance
    Then the result will be <valid>

    Examples:
        | inputDataReference   | valid | description                                                                      |
        | #/004/tests/000/data | true  | items defaults to empty schema so everything is valid                            |
        | #/004/tests/001/data | true  | ignores non-arrays                                                               |

Scenario Outline: additionalItems are allowed by default
/* Schema: 
{"items": [{"type": "integer"}]}
*/
    Given the input JSON file "additionalItems.json"
    And the schema at "#/5/schema"
    And the input data at "<inputDataReference>"
    And I generate a type for the schema
    And I construct an instance of the schema type from the data
    When I validate the instance
    Then the result will be <valid>

    Examples:
        | inputDataReference   | valid | description                                                                      |
        | #/005/tests/000/data | true  | only the first item is validated                                                 |

Scenario Outline: additionalItems does not look in applicators, valid case
/* Schema: 
{
            "allOf": [
                { "items": [ { "type": "integer" } ] }
            ],
            "additionalItems": { "type": "boolean" }
        }
*/
    Given the input JSON file "additionalItems.json"
    And the schema at "#/6/schema"
    And the input data at "<inputDataReference>"
    And I generate a type for the schema
    And I construct an instance of the schema type from the data
    When I validate the instance
    Then the result will be <valid>

    Examples:
        | inputDataReference   | valid | description                                                                      |
        | #/006/tests/000/data | true  | items defined in allOf are not examined                                          |

Scenario Outline: additionalItems does not look in applicators, invalid case
/* Schema: 
{
            "allOf": [
                { "items": [ { "type": "integer" }, { "type": "string" } ] }
            ],
            "items": [ {"type": "integer" } ],
            "additionalItems": { "type": "boolean" }
        }
*/
    Given the input JSON file "additionalItems.json"
    And the schema at "#/7/schema"
    And the input data at "<inputDataReference>"
    And I generate a type for the schema
    And I construct an instance of the schema type from the data
    When I validate the instance
    Then the result will be <valid>

    Examples:
        | inputDataReference   | valid | description                                                                      |
        | #/007/tests/000/data | false | items defined in allOf are not examined                                          |

Scenario Outline: items validation adjusts the starting index for additionalItems
/* Schema: 
{
            "items": [ { "type": "string" } ],
            "additionalItems": { "type": "integer" }
        }
*/
    Given the input JSON file "additionalItems.json"
    And the schema at "#/8/schema"
    And the input data at "<inputDataReference>"
    And I generate a type for the schema
    And I construct an instance of the schema type from the data
    When I validate the instance
    Then the result will be <valid>

    Examples:
        | inputDataReference   | valid | description                                                                      |
        | #/008/tests/000/data | true  | valid items                                                                      |
        | #/008/tests/001/data | false | wrong type of second item                                                        |

Scenario Outline: additionalItems with null instance elements
/* Schema: 
{
            "additionalItems": {
                "type": "null"
            }
        }
*/
    Given the input JSON file "additionalItems.json"
    And the schema at "#/9/schema"
    And the input data at "<inputDataReference>"
    And I generate a type for the schema
    And I construct an instance of the schema type from the data
    When I validate the instance
    Then the result will be <valid>

    Examples:
        | inputDataReference   | valid | description                                                                      |
        | #/009/tests/000/data | true  | allows null elements                                                             |
