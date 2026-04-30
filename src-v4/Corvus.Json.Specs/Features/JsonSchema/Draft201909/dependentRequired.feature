@draft2019-09

Feature: dependentRequired draft2019-09
    In order to use json-schema
    As a developer
    I want to support dependentRequired in draft2019-09

Scenario Outline: single dependency
/* Schema: 
{
            "$schema": "https://json-schema.org/draft/2019-09/schema",
            "dependentRequired": {"bar": ["foo"]}
        }
*/
    Given the input JSON file "dependentRequired.json"
    And the schema at "#/0/schema"
    And the input data at "<inputDataReference>"
    And I generate a type for the schema
    And I construct an instance of the schema type from the data
    When I validate the instance
    Then the result will be <valid>

    Examples:
        | inputDataReference   | valid | description                                                                      |
        # {}
        | #/000/tests/000/data | true  | neither                                                                          |
        # {"foo": 1}
        | #/000/tests/001/data | true  | nondependant                                                                     |
        # {"foo": 1, "bar": 2}
        | #/000/tests/002/data | true  | with dependency                                                                  |
        # {"bar": 2}
        | #/000/tests/003/data | false | missing dependency                                                               |
        # ["bar"]
        | #/000/tests/004/data | true  | ignores arrays                                                                   |
        # foobar
        | #/000/tests/005/data | true  | ignores strings                                                                  |
        # 12
        | #/000/tests/006/data | true  | ignores other non-objects                                                        |

Scenario Outline: empty dependents
/* Schema: 
{
            "$schema": "https://json-schema.org/draft/2019-09/schema",
            "dependentRequired": {"bar": []}
        }
*/
    Given the input JSON file "dependentRequired.json"
    And the schema at "#/1/schema"
    And the input data at "<inputDataReference>"
    And I generate a type for the schema
    And I construct an instance of the schema type from the data
    When I validate the instance
    Then the result will be <valid>

    Examples:
        | inputDataReference   | valid | description                                                                      |
        # {}
        | #/001/tests/000/data | true  | empty object                                                                     |
        # {"bar": 2}
        | #/001/tests/001/data | true  | object with one property                                                         |
        # 1
        | #/001/tests/002/data | true  | non-object is valid                                                              |

Scenario Outline: multiple dependents required
/* Schema: 
{
            "$schema": "https://json-schema.org/draft/2019-09/schema",
            "dependentRequired": {"quux": ["foo", "bar"]}
        }
*/
    Given the input JSON file "dependentRequired.json"
    And the schema at "#/2/schema"
    And the input data at "<inputDataReference>"
    And I generate a type for the schema
    And I construct an instance of the schema type from the data
    When I validate the instance
    Then the result will be <valid>

    Examples:
        | inputDataReference   | valid | description                                                                      |
        # {}
        | #/002/tests/000/data | true  | neither                                                                          |
        # {"foo": 1, "bar": 2}
        | #/002/tests/001/data | true  | nondependants                                                                    |
        # {"foo": 1, "bar": 2, "quux": 3}
        | #/002/tests/002/data | true  | with dependencies                                                                |
        # {"foo": 1, "quux": 2}
        | #/002/tests/003/data | false | missing dependency                                                               |
        # {"bar": 1, "quux": 2}
        | #/002/tests/004/data | false | missing other dependency                                                         |
        # {"quux": 1}
        | #/002/tests/005/data | false | missing both dependencies                                                        |

Scenario Outline: dependencies with escaped characters
/* Schema: 
{
            "$schema": "https://json-schema.org/draft/2019-09/schema",
            "dependentRequired": {
                "foo\nbar": ["foo\rbar"],
                "foo\"bar": ["foo'bar"]
            }
        }
*/
    Given the input JSON file "dependentRequired.json"
    And the schema at "#/3/schema"
    And the input data at "<inputDataReference>"
    And I generate a type for the schema
    And I construct an instance of the schema type from the data
    When I validate the instance
    Then the result will be <valid>

    Examples:
        | inputDataReference   | valid | description                                                                      |
        # { "foo\nbar": 1, "foo\rbar": 2 }
        | #/003/tests/000/data | true  | CRLF                                                                             |
        # { "foo'bar": 1, "foo\"bar": 2 }
        | #/003/tests/001/data | true  | quoted quotes                                                                    |
        # { "foo\nbar": 1, "foo": 2 }
        | #/003/tests/002/data | false | CRLF missing dependent                                                           |
        # { "foo\"bar": 2 }
        | #/003/tests/003/data | false | quoted quotes missing dependent                                                  |
