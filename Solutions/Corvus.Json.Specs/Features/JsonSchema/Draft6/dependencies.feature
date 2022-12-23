@draft6

Feature: dependencies draft6
    In order to use json-schema
    As a developer
    I want to support dependencies in draft6

Scenario Outline: dependencies
/* Schema: 
{
            "dependencies": {"bar": ["foo"]}
        }
*/
    Given the input JSON file "dependencies.json"
    And the schema at "#/0/schema"
    And the input data at "<inputDataReference>"
    And I generate a type for the schema
    And I construct an instance of the schema type from the data
    When I validate the instance
    Then the result will be <valid>

    Examples:
        | inputDataReference   | valid | description                                                                      |
        | #/000/tests/000/data | true  | neither                                                                          |
        | #/000/tests/001/data | true  | nondependant                                                                     |
        | #/000/tests/002/data | true  | with dependency                                                                  |
        | #/000/tests/003/data | false | missing dependency                                                               |
        | #/000/tests/004/data | true  | ignores arrays                                                                   |
        | #/000/tests/005/data | true  | ignores strings                                                                  |
        | #/000/tests/006/data | true  | ignores other non-objects                                                        |

Scenario Outline: dependencies with empty array
/* Schema: 
{
            "dependencies": {"bar": []}
        }
*/
    Given the input JSON file "dependencies.json"
    And the schema at "#/1/schema"
    And the input data at "<inputDataReference>"
    And I generate a type for the schema
    And I construct an instance of the schema type from the data
    When I validate the instance
    Then the result will be <valid>

    Examples:
        | inputDataReference   | valid | description                                                                      |
        | #/001/tests/000/data | true  | empty object                                                                     |
        | #/001/tests/001/data | true  | object with one property                                                         |
        | #/001/tests/002/data | true  | non-object is valid                                                              |

Scenario Outline: multiple dependencies
/* Schema: 
{
            "dependencies": {"quux": ["foo", "bar"]}
        }
*/
    Given the input JSON file "dependencies.json"
    And the schema at "#/2/schema"
    And the input data at "<inputDataReference>"
    And I generate a type for the schema
    And I construct an instance of the schema type from the data
    When I validate the instance
    Then the result will be <valid>

    Examples:
        | inputDataReference   | valid | description                                                                      |
        | #/002/tests/000/data | true  | neither                                                                          |
        | #/002/tests/001/data | true  | nondependants                                                                    |
        | #/002/tests/002/data | true  | with dependencies                                                                |
        | #/002/tests/003/data | false | missing dependency                                                               |
        | #/002/tests/004/data | false | missing other dependency                                                         |
        | #/002/tests/005/data | false | missing both dependencies                                                        |

Scenario Outline: multiple dependencies subschema
/* Schema: 
{
            "dependencies": {
                "bar": {
                    "properties": {
                        "foo": {"type": "integer"},
                        "bar": {"type": "integer"}
                    }
                }
            }
        }
*/
    Given the input JSON file "dependencies.json"
    And the schema at "#/3/schema"
    And the input data at "<inputDataReference>"
    And I generate a type for the schema
    And I construct an instance of the schema type from the data
    When I validate the instance
    Then the result will be <valid>

    Examples:
        | inputDataReference   | valid | description                                                                      |
        | #/003/tests/000/data | true  | valid                                                                            |
        | #/003/tests/001/data | true  | no dependency                                                                    |
        | #/003/tests/002/data | false | wrong type                                                                       |
        | #/003/tests/003/data | false | wrong type other                                                                 |
        | #/003/tests/004/data | false | wrong type both                                                                  |

Scenario Outline: dependencies with boolean subschemas
/* Schema: 
{
            "dependencies": {
                "foo": true,
                "bar": false
            }
        }
*/
    Given the input JSON file "dependencies.json"
    And the schema at "#/4/schema"
    And the input data at "<inputDataReference>"
    And I generate a type for the schema
    And I construct an instance of the schema type from the data
    When I validate the instance
    Then the result will be <valid>

    Examples:
        | inputDataReference   | valid | description                                                                      |
        | #/004/tests/000/data | true  | object with property having schema true is valid                                 |
        | #/004/tests/001/data | false | object with property having schema false is invalid                              |
        | #/004/tests/002/data | false | object with both properties is invalid                                           |
        | #/004/tests/003/data | true  | empty object is valid                                                            |

Scenario Outline: dependencies with escaped characters
/* Schema: 
{
            "dependencies": {
                "foo\nbar": ["foo\rbar"],
                "foo\tbar": {
                    "minProperties": 4
                },
                "foo'bar": {"required": ["foo\"bar"]},
                "foo\"bar": ["foo'bar"]
            }
        }
*/
    Given the input JSON file "dependencies.json"
    And the schema at "#/5/schema"
    And the input data at "<inputDataReference>"
    And I generate a type for the schema
    And I construct an instance of the schema type from the data
    When I validate the instance
    Then the result will be <valid>

    Examples:
        | inputDataReference   | valid | description                                                                      |
        | #/005/tests/000/data | true  | valid object 1                                                                   |
        | #/005/tests/001/data | true  | valid object 2                                                                   |
        | #/005/tests/002/data | true  | valid object 3                                                                   |
        | #/005/tests/003/data | false | invalid object 1                                                                 |
        | #/005/tests/004/data | false | invalid object 2                                                                 |
        | #/005/tests/005/data | false | invalid object 3                                                                 |
        | #/005/tests/006/data | false | invalid object 4                                                                 |
