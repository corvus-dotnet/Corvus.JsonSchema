@draft4

Feature: required draft4
    In order to use json-schema
    As a developer
    I want to support required in draft4

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
        # {"foo": 1}
        | #/000/tests/000/data | true  | present required property is valid                                               |
        # {"bar": 1}
        | #/000/tests/001/data | false | non-present required property is invalid                                         |
        # []
        | #/000/tests/002/data | true  | ignores arrays                                                                   |
        # 
        | #/000/tests/003/data | true  | ignores strings                                                                  |
        # 12
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
        # {}
        | #/001/tests/000/data | true  | not required by default                                                          |

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
    And the schema at "#/2/schema"
    And the input data at "<inputDataReference>"
    And I generate a type for the schema
    And I construct an instance of the schema type from the data
    When I validate the instance
    Then the result will be <valid>

    Examples:
        | inputDataReference   | valid | description                                                                      |
        # { "foo\nbar": 1, "foo\"bar": 1, "foo\\bar": 1, "foo\rbar": 1, "foo\tbar": 1, "foo\fbar": 1 }
        | #/002/tests/000/data | true  | object with all properties present is valid                                      |
        # { "foo\nbar": "1", "foo\"bar": "1" }
        | #/002/tests/001/data | false | object with some properties missing is invalid                                   |

Scenario Outline: required properties whose names are Javascript object property names
/* Schema: 
{ "required": ["__proto__", "toString", "constructor"] }
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
        # []
        | #/003/tests/000/data | true  | ignores arrays                                                                   |
        # 12
        | #/003/tests/001/data | true  | ignores other non-objects                                                        |
        # {}
        | #/003/tests/002/data | false | none of the properties mentioned                                                 |
        # { "__proto__": "foo" }
        | #/003/tests/003/data | false | __proto__ present                                                                |
        # { "toString": { "length": 37 } }
        | #/003/tests/004/data | false | toString present                                                                 |
        # { "constructor": { "length": 37 } }
        | #/003/tests/005/data | false | constructor present                                                              |
        # { "__proto__": 12, "toString": { "length": "foo" }, "constructor": 37 }
        | #/003/tests/006/data | true  | all present                                                                      |
