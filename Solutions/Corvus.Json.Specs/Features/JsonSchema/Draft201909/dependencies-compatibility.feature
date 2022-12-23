@draft2019-09

Feature: dependencies-compatibility draft2019-09
    In order to use json-schema
    As a developer
    I want to support dependencies-compatibility in draft2019-09

Scenario Outline: single dependency
/* Schema: 
{
            "$schema": "https://json-schema.org/draft/2019-09/schema",
            "dependencies": {"bar": ["foo"]}
        }
*/
    Given the input JSON file "optional/dependencies-compatibility.json"
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

Scenario Outline: empty dependents
/* Schema: 
{
            "$schema": "https://json-schema.org/draft/2019-09/schema",
            "dependencies": {"bar": []}
        }
*/
    Given the input JSON file "optional/dependencies-compatibility.json"
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

Scenario Outline: multiple dependents required
/* Schema: 
{
            "$schema": "https://json-schema.org/draft/2019-09/schema",
            "dependencies": {"quux": ["foo", "bar"]}
        }
*/
    Given the input JSON file "optional/dependencies-compatibility.json"
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

Scenario Outline: dependencies with escaped characters
/* Schema: 
{
            "$schema": "https://json-schema.org/draft/2019-09/schema",
            "dependencies": {
                "foo\nbar": ["foo\rbar"],
                "foo\"bar": ["foo'bar"]
            }
        }
*/
    Given the input JSON file "optional/dependencies-compatibility.json"
    And the schema at "#/3/schema"
    And the input data at "<inputDataReference>"
    And I generate a type for the schema
    And I construct an instance of the schema type from the data
    When I validate the instance
    Then the result will be <valid>

    Examples:
        | inputDataReference   | valid | description                                                                      |
        | #/003/tests/000/data | true  | CRLF                                                                             |
        | #/003/tests/001/data | true  | quoted quotes                                                                    |
        | #/003/tests/002/data | false | CRLF missing dependent                                                           |
        | #/003/tests/003/data | false | quoted quotes missing dependent                                                  |

Scenario Outline: single schema dependency
/* Schema: 
{
            "$schema": "https://json-schema.org/draft/2019-09/schema",
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
    Given the input JSON file "optional/dependencies-compatibility.json"
    And the schema at "#/4/schema"
    And the input data at "<inputDataReference>"
    And I generate a type for the schema
    And I construct an instance of the schema type from the data
    When I validate the instance
    Then the result will be <valid>

    Examples:
        | inputDataReference   | valid | description                                                                      |
        | #/004/tests/000/data | true  | valid                                                                            |
        | #/004/tests/001/data | true  | no dependency                                                                    |
        | #/004/tests/002/data | false | wrong type                                                                       |
        | #/004/tests/003/data | false | wrong type other                                                                 |
        | #/004/tests/004/data | false | wrong type both                                                                  |
        | #/004/tests/005/data | true  | ignores arrays                                                                   |
        | #/004/tests/006/data | true  | ignores strings                                                                  |
        | #/004/tests/007/data | true  | ignores other non-objects                                                        |

Scenario Outline: boolean subschemas
/* Schema: 
{
            "$schema": "https://json-schema.org/draft/2019-09/schema",
            "dependencies": {
                "foo": true,
                "bar": false
            }
        }
*/
    Given the input JSON file "optional/dependencies-compatibility.json"
    And the schema at "#/5/schema"
    And the input data at "<inputDataReference>"
    And I generate a type for the schema
    And I construct an instance of the schema type from the data
    When I validate the instance
    Then the result will be <valid>

    Examples:
        | inputDataReference   | valid | description                                                                      |
        | #/005/tests/000/data | true  | object with property having schema true is valid                                 |
        | #/005/tests/001/data | false | object with property having schema false is invalid                              |
        | #/005/tests/002/data | false | object with both properties is invalid                                           |
        | #/005/tests/003/data | true  | empty object is valid                                                            |

Scenario Outline: schema dependencies with escaped characters
/* Schema: 
{
            "$schema": "https://json-schema.org/draft/2019-09/schema",
            "dependencies": {
                "foo\tbar": {"minProperties": 4},
                "foo'bar": {"required": ["foo\"bar"]}
            }
        }
*/
    Given the input JSON file "optional/dependencies-compatibility.json"
    And the schema at "#/6/schema"
    And the input data at "<inputDataReference>"
    And I generate a type for the schema
    And I construct an instance of the schema type from the data
    When I validate the instance
    Then the result will be <valid>

    Examples:
        | inputDataReference   | valid | description                                                                      |
        | #/006/tests/000/data | true  | quoted tab                                                                       |
        | #/006/tests/001/data | false | quoted quote                                                                     |
        | #/006/tests/002/data | false | quoted tab invalid under dependent schema                                        |
        | #/006/tests/003/data | false | quoted quote invalid under dependent schema                                      |
