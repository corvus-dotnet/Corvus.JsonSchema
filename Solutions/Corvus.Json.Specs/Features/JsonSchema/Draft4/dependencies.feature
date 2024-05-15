@draft4

Feature: dependencies draft4
    In order to use json-schema
    As a developer
    I want to support dependencies in draft4

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

Scenario Outline: multiple dependencies
/* Schema: 
{
            "dependencies": {"quux": ["foo", "bar"]}
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
        # {}
        | #/001/tests/000/data | true  | neither                                                                          |
        # {"foo": 1, "bar": 2}
        | #/001/tests/001/data | true  | nondependants                                                                    |
        # {"foo": 1, "bar": 2, "quux": 3}
        | #/001/tests/002/data | true  | with dependencies                                                                |
        # {"foo": 1, "quux": 2}
        | #/001/tests/003/data | false | missing dependency                                                               |
        # {"bar": 1, "quux": 2}
        | #/001/tests/004/data | false | missing other dependency                                                         |
        # {"quux": 1}
        | #/001/tests/005/data | false | missing both dependencies                                                        |

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
    And the schema at "#/2/schema"
    And the input data at "<inputDataReference>"
    And I generate a type for the schema
    And I construct an instance of the schema type from the data
    When I validate the instance
    Then the result will be <valid>

    Examples:
        | inputDataReference   | valid | description                                                                      |
        # {"foo": 1, "bar": 2}
        | #/002/tests/000/data | true  | valid                                                                            |
        # {"foo": "quux"}
        | #/002/tests/001/data | true  | no dependency                                                                    |
        # {"foo": "quux", "bar": 2}
        | #/002/tests/002/data | false | wrong type                                                                       |
        # {"foo": 2, "bar": "quux"}
        | #/002/tests/003/data | false | wrong type other                                                                 |
        # {"foo": "quux", "bar": "quux"}
        | #/002/tests/004/data | false | wrong type both                                                                  |

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
    And the schema at "#/3/schema"
    And the input data at "<inputDataReference>"
    And I generate a type for the schema
    And I construct an instance of the schema type from the data
    When I validate the instance
    Then the result will be <valid>

    Examples:
        | inputDataReference   | valid | description                                                                      |
        # { "foo\nbar": 1, "foo\rbar": 2 }
        | #/003/tests/000/data | true  | valid object 1                                                                   |
        # { "foo\tbar": 1, "a": 2, "b": 3, "c": 4 }
        | #/003/tests/001/data | true  | valid object 2                                                                   |
        # { "foo'bar": 1, "foo\"bar": 2 }
        | #/003/tests/002/data | true  | valid object 3                                                                   |
        # { "foo\nbar": 1, "foo": 2 }
        | #/003/tests/003/data | false | invalid object 1                                                                 |
        # { "foo\tbar": 1, "a": 2 }
        | #/003/tests/004/data | false | invalid object 2                                                                 |
        # { "foo'bar": 1 }
        | #/003/tests/005/data | false | invalid object 3                                                                 |
        # { "foo\"bar": 2 }
        | #/003/tests/006/data | false | invalid object 4                                                                 |

Scenario Outline: dependent subschema incompatible with root
/* Schema: 
{
            "properties": {
                "foo": {}
            },
            "dependencies": {
                "foo": {
                    "properties": {
                        "bar": {}
                    },
                    "additionalProperties": false
                }
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
        # {"foo": 1}
        | #/004/tests/000/data | false | matches root                                                                     |
        # {"bar": 1}
        | #/004/tests/001/data | true  | matches dependency                                                               |
        # {"foo": 1, "bar": 2}
        | #/004/tests/002/data | false | matches both                                                                     |
        # {"baz": 1}
        | #/004/tests/003/data | true  | no dependency                                                                    |
