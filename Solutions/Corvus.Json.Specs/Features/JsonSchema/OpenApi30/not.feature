@openApi30

Feature: not openApi30
    In order to use json-schema
    As a developer
    I want to support not in openApi30

Scenario Outline: not
/* Schema: 
{
            "not": {"type": "integer"}
        }
*/
    Given the input JSON file "not.json"
    And the schema at "#/0/schema"
    And the input data at "<inputDataReference>"
    And I generate a type for the schema
    And I construct an instance of the schema type from the data
    When I validate the instance
    Then the result will be <valid>

    Examples:
        | inputDataReference   | valid | description                                                                      |
        # foo
        | #/000/tests/000/data | true  | allowed                                                                          |
        # 1
        | #/000/tests/001/data | false | disallowed                                                                       |

Scenario Outline: not more complex schema
/* Schema: 
{
            "not": {
                "type": "object",
                "properties": {
                    "foo": {
                        "type": "string"
                    }
                }
             }
        }
*/
    Given the input JSON file "not.json"
    And the schema at "#/1/schema"
    And the input data at "<inputDataReference>"
    And I generate a type for the schema
    And I construct an instance of the schema type from the data
    When I validate the instance
    Then the result will be <valid>

    Examples:
        | inputDataReference   | valid | description                                                                      |
        # 1
        | #/001/tests/000/data | true  | match                                                                            |
        # {"foo": 1}
        | #/001/tests/001/data | true  | other match                                                                      |
        # {"foo": "bar"}
        | #/001/tests/002/data | false | mismatch                                                                         |

Scenario Outline: forbidden property
/* Schema: 
{
            "properties": {
                "foo": { 
                    "not": {}
                }
            }
        }
*/
    Given the input JSON file "not.json"
    And the schema at "#/2/schema"
    And the input data at "<inputDataReference>"
    And I generate a type for the schema
    And I construct an instance of the schema type from the data
    When I validate the instance
    Then the result will be <valid>

    Examples:
        | inputDataReference   | valid | description                                                                      |
        # {"foo": 1, "bar": 2}
        | #/002/tests/000/data | false | property present                                                                 |
        # {"bar": 1, "baz": 2}
        | #/002/tests/001/data | true  | property absent                                                                  |

Scenario Outline: forbid everything with empty schema
/* Schema: 
{ "not": {} }
*/
    Given the input JSON file "not.json"
    And the schema at "#/3/schema"
    And the input data at "<inputDataReference>"
    And I generate a type for the schema
    And I construct an instance of the schema type from the data
    When I validate the instance
    Then the result will be <valid>

    Examples:
        | inputDataReference   | valid | description                                                                      |
        # 1
        | #/003/tests/000/data | false | number is invalid                                                                |
        # foo
        | #/003/tests/001/data | false | string is invalid                                                                |
        # True
        | #/003/tests/002/data | false | boolean true is invalid                                                          |
        # False
        | #/003/tests/003/data | false | boolean false is invalid                                                         |
        # 
        | #/003/tests/004/data | false | null is invalid                                                                  |
        # {"foo": "bar"}
        | #/003/tests/005/data | false | object is invalid                                                                |
        # {}
        | #/003/tests/006/data | false | empty object is invalid                                                          |
        # ["foo"]
        | #/003/tests/007/data | false | array is invalid                                                                 |
        # []
        | #/003/tests/008/data | false | empty array is invalid                                                           |

Scenario Outline: double negation
/* Schema: 
{ "not": { "not": {} } }
*/
    Given the input JSON file "not.json"
    And the schema at "#/4/schema"
    And the input data at "<inputDataReference>"
    And I generate a type for the schema
    And I construct an instance of the schema type from the data
    When I validate the instance
    Then the result will be <valid>

    Examples:
        | inputDataReference   | valid | description                                                                      |
        # foo
        | #/004/tests/000/data | true  | any value is valid                                                               |
