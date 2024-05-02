@draft2020-12

Feature: prefixItems draft2020-12
    In order to use json-schema
    As a developer
    I want to support prefixItems in draft2020-12

Scenario Outline: a schema given for prefixItems
/* Schema: 
{
            "$schema": "https://json-schema.org/draft/2020-12/schema",
            "prefixItems": [
                {"type": "integer"},
                {"type": "string"}
            ]
        }
*/
    Given the input JSON file "prefixItems.json"
    And the schema at "#/0/schema"
    And the input data at "<inputDataReference>"
    And I generate a type for the schema
    And I construct an instance of the schema type from the data
    When I validate the instance
    Then the result will be <valid>

    Examples:
        | inputDataReference   | valid | description                                                                      |
        # [ 1, "foo" ]
        | #/000/tests/000/data | true  | correct types                                                                    |
        # [ "foo", 1 ]
        | #/000/tests/001/data | false | wrong types                                                                      |
        # [ 1 ]
        | #/000/tests/002/data | true  | incomplete array of items                                                        |
        # [ 1, "foo", true ]
        | #/000/tests/003/data | true  | array with additional items                                                      |
        # [ ]
        | #/000/tests/004/data | true  | empty array                                                                      |
        # { "0": "invalid", "1": "valid", "length": 2 }
        | #/000/tests/005/data | true  | JavaScript pseudo-array is valid                                                 |

Scenario Outline: prefixItems with boolean schemas
/* Schema: 
{
            "$schema": "https://json-schema.org/draft/2020-12/schema",
            "prefixItems": [true, false]
        }
*/
    Given the input JSON file "prefixItems.json"
    And the schema at "#/1/schema"
    And the input data at "<inputDataReference>"
    And I generate a type for the schema
    And I construct an instance of the schema type from the data
    When I validate the instance
    Then the result will be <valid>

    Examples:
        | inputDataReference   | valid | description                                                                      |
        # [ 1 ]
        | #/001/tests/000/data | true  | array with one item is valid                                                     |
        # [ 1, "foo" ]
        | #/001/tests/001/data | false | array with two items is invalid                                                  |
        # []
        | #/001/tests/002/data | true  | empty array is valid                                                             |

Scenario Outline: additional items are allowed by default
/* Schema: 
{
            "$schema": "https://json-schema.org/draft/2020-12/schema",
            "prefixItems": [{"type": "integer"}]
        }
*/
    Given the input JSON file "prefixItems.json"
    And the schema at "#/2/schema"
    And the input data at "<inputDataReference>"
    And I generate a type for the schema
    And I construct an instance of the schema type from the data
    When I validate the instance
    Then the result will be <valid>

    Examples:
        | inputDataReference   | valid | description                                                                      |
        # [1, "foo", false]
        | #/002/tests/000/data | true  | only the first item is validated                                                 |

Scenario Outline: prefixItems with null instance elements
/* Schema: 
{
            "$schema": "https://json-schema.org/draft/2020-12/schema",
            "prefixItems": [
                {
                    "type": "null"
                }
            ]
        }
*/
    Given the input JSON file "prefixItems.json"
    And the schema at "#/3/schema"
    And the input data at "<inputDataReference>"
    And I generate a type for the schema
    And I construct an instance of the schema type from the data
    When I validate the instance
    Then the result will be <valid>

    Examples:
        | inputDataReference   | valid | description                                                                      |
        # [ null ]
        | #/003/tests/000/data | true  | allows null elements                                                             |
