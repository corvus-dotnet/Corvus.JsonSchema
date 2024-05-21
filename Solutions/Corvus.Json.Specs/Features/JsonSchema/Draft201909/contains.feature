@draft2019-09

Feature: contains draft2019-09
    In order to use json-schema
    As a developer
    I want to support contains in draft2019-09

Scenario Outline: contains keyword validation
/* Schema: 
{
            "$schema": "https://json-schema.org/draft/2019-09/schema",
            "contains": {"minimum": 5}
        }
*/
    Given the input JSON file "contains.json"
    And the schema at "#/0/schema"
    And the input data at "<inputDataReference>"
    And I assert format
    And I generate a type for the schema
    And I construct an instance of the schema type from the data
    When I validate the instance
    Then the result will be <valid>

    Examples:
        | inputDataReference   | valid | description                                                                      |
        # [3, 4, 5]
        | #/000/tests/000/data | true  | array with item matching schema (5) is valid                                     |
        # [3, 4, 6]
        | #/000/tests/001/data | true  | array with item matching schema (6) is valid                                     |
        # [3, 4, 5, 6]
        | #/000/tests/002/data | true  | array with two items matching schema (5, 6) is valid                             |
        # [2, 3, 4]
        | #/000/tests/003/data | false | array without items matching schema is invalid                                   |
        # []
        | #/000/tests/004/data | false | empty array is invalid                                                           |
        # {}
        | #/000/tests/005/data | true  | not array is valid                                                               |

Scenario Outline: contains keyword with const keyword
/* Schema: 
{
            "$schema": "https://json-schema.org/draft/2019-09/schema",
            "contains": { "const": 5 }
        }
*/
    Given the input JSON file "contains.json"
    And the schema at "#/1/schema"
    And the input data at "<inputDataReference>"
    And I assert format
    And I generate a type for the schema
    And I construct an instance of the schema type from the data
    When I validate the instance
    Then the result will be <valid>

    Examples:
        | inputDataReference   | valid | description                                                                      |
        # [3, 4, 5]
        | #/001/tests/000/data | true  | array with item 5 is valid                                                       |
        # [3, 4, 5, 5]
        | #/001/tests/001/data | true  | array with two items 5 is valid                                                  |
        # [1, 2, 3, 4]
        | #/001/tests/002/data | false | array without item 5 is invalid                                                  |

Scenario Outline: contains keyword with boolean schema true
/* Schema: 
{
            "$schema": "https://json-schema.org/draft/2019-09/schema",
            "contains": true
        }
*/
    Given the input JSON file "contains.json"
    And the schema at "#/2/schema"
    And the input data at "<inputDataReference>"
    And I assert format
    And I generate a type for the schema
    And I construct an instance of the schema type from the data
    When I validate the instance
    Then the result will be <valid>

    Examples:
        | inputDataReference   | valid | description                                                                      |
        # ["foo"]
        | #/002/tests/000/data | true  | any non-empty array is valid                                                     |
        # []
        | #/002/tests/001/data | false | empty array is invalid                                                           |

Scenario Outline: contains keyword with boolean schema false
/* Schema: 
{
            "$schema": "https://json-schema.org/draft/2019-09/schema",
            "contains": false
        }
*/
    Given the input JSON file "contains.json"
    And the schema at "#/3/schema"
    And the input data at "<inputDataReference>"
    And I assert format
    And I generate a type for the schema
    And I construct an instance of the schema type from the data
    When I validate the instance
    Then the result will be <valid>

    Examples:
        | inputDataReference   | valid | description                                                                      |
        # ["foo"]
        | #/003/tests/000/data | false | any non-empty array is invalid                                                   |
        # []
        | #/003/tests/001/data | false | empty array is invalid                                                           |
        # contains does not apply to strings
        | #/003/tests/002/data | true  | non-arrays are valid                                                             |

Scenario Outline: items + contains
/* Schema: 
{
            "$schema": "https://json-schema.org/draft/2019-09/schema",
            "items": { "multipleOf": 2 },
            "contains": { "multipleOf": 3 }
        }
*/
    Given the input JSON file "contains.json"
    And the schema at "#/4/schema"
    And the input data at "<inputDataReference>"
    And I assert format
    And I generate a type for the schema
    And I construct an instance of the schema type from the data
    When I validate the instance
    Then the result will be <valid>

    Examples:
        | inputDataReference   | valid | description                                                                      |
        # [ 2, 4, 8 ]
        | #/004/tests/000/data | false | matches items, does not match contains                                           |
        # [ 3, 6, 9 ]
        | #/004/tests/001/data | false | does not match items, matches contains                                           |
        # [ 6, 12 ]
        | #/004/tests/002/data | true  | matches both items and contains                                                  |
        # [ 1, 5 ]
        | #/004/tests/003/data | false | matches neither items nor contains                                               |

Scenario Outline: contains with false if subschema
/* Schema: 
{
            "$schema": "https://json-schema.org/draft/2019-09/schema",
            "contains": {
                "if": false,
                "else": true
            }
        }
*/
    Given the input JSON file "contains.json"
    And the schema at "#/5/schema"
    And the input data at "<inputDataReference>"
    And I assert format
    And I generate a type for the schema
    And I construct an instance of the schema type from the data
    When I validate the instance
    Then the result will be <valid>

    Examples:
        | inputDataReference   | valid | description                                                                      |
        # ["foo"]
        | #/005/tests/000/data | true  | any non-empty array is valid                                                     |
        # []
        | #/005/tests/001/data | false | empty array is invalid                                                           |

Scenario Outline: contains with null instance elements
/* Schema: 
{
            "$schema": "https://json-schema.org/draft/2019-09/schema",
            "contains": {
                "type": "null"
            }
        }
*/
    Given the input JSON file "contains.json"
    And the schema at "#/6/schema"
    And the input data at "<inputDataReference>"
    And I assert format
    And I generate a type for the schema
    And I construct an instance of the schema type from the data
    When I validate the instance
    Then the result will be <valid>

    Examples:
        | inputDataReference   | valid | description                                                                      |
        # [ null ]
        | #/006/tests/000/data | true  | allows null items                                                                |
