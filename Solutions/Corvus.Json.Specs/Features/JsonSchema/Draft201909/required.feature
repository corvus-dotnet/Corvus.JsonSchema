@draft2019-09

Feature: required draft2019-09
    In order to use json-schema
    As a developer
    I want to support required in draft2019-09

Scenario Outline: required validation
/* Schema: 
{
            "properties": {
                "foo": {},
                "bar": {}
            },
            "required": ["foo"]
        }
*/
    Given the input JSON file "required.json"
    And the schema at "#/0/schema"
    And the input data at "<inputDataReference>"
    And I generate a type for the schema
    And I construct an instance of the schema type from the data
    When I validate the instance
    Then the result will be <valid>

    Examples:
        | inputDataReference   | valid | description                                                                      |
        | #/000/tests/000/data | true  | present required property is valid                                               |
        | #/000/tests/001/data | false | non-present required property is invalid                                         |
        | #/000/tests/002/data | true  | ignores arrays                                                                   |
        | #/000/tests/003/data | true  | ignores strings                                                                  |
        | #/000/tests/004/data | true  | ignores other non-objects                                                        |

Scenario Outline: required default validation
/* Schema: 
{
            "properties": {
                "foo": {}
            }
        }
*/
    Given the input JSON file "required.json"
    And the schema at "#/1/schema"
    And the input data at "<inputDataReference>"
    And I generate a type for the schema
    And I construct an instance of the schema type from the data
    When I validate the instance
    Then the result will be <valid>

    Examples:
        | inputDataReference   | valid | description                                                                      |
        | #/001/tests/000/data | true  | not required by default                                                          |

Scenario Outline: required with empty array
/* Schema: 
{
            "properties": {
                "foo": {}
            },
            "required": []
        }
*/
    Given the input JSON file "required.json"
    And the schema at "#/2/schema"
    And the input data at "<inputDataReference>"
    And I generate a type for the schema
    And I construct an instance of the schema type from the data
    When I validate the instance
    Then the result will be <valid>

    Examples:
        | inputDataReference   | valid | description                                                                      |
        | #/002/tests/000/data | true  | property not required                                                            |

Scenario Outline: required with escaped characters
/* Schema: 
{
            "required": [
                "foo\nbar",
                "foo\"bar",
                "foo\\bar",
                "foo\rbar",
                "foo\tbar",
                "foo\fbar"
            ]
        }
*/
    Given the input JSON file "required.json"
    And the schema at "#/3/schema"
    And the input data at "<inputDataReference>"
    And I generate a type for the schema
    And I construct an instance of the schema type from the data
    When I validate the instance
    Then the result will be <valid>

    Examples:
        | inputDataReference   | valid | description                                                                      |
        | #/003/tests/000/data | true  | object with all properties present is valid                                      |
        | #/003/tests/001/data | false | object with some properties missing is invalid                                   |
