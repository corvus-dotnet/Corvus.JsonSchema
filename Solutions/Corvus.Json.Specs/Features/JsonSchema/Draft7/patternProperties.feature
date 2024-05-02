@draft7

Feature: patternProperties draft7
    In order to use json-schema
    As a developer
    I want to support patternProperties in draft7

Scenario Outline: patternProperties validates properties matching a regex
/* Schema: 
{
            "patternProperties": {
                "f.*o": {"type": "integer"}
            }
        }
*/
    Given the input JSON file "patternProperties.json"
    And the schema at "#/0/schema"
    And the input data at "<inputDataReference>"
    And I generate a type for the schema
    And I construct an instance of the schema type from the data
    When I validate the instance
    Then the result will be <valid>

    Examples:
        | inputDataReference   | valid | description                                                                      |
        # {"foo": 1}
        | #/000/tests/000/data | true  | a single valid match is valid                                                    |
        # {"foo": 1, "foooooo" : 2}
        | #/000/tests/001/data | true  | multiple valid matches is valid                                                  |
        # {"foo": "bar", "fooooo": 2}
        | #/000/tests/002/data | false | a single invalid match is invalid                                                |
        # {"foo": "bar", "foooooo" : "baz"}
        | #/000/tests/003/data | false | multiple invalid matches is invalid                                              |
        # ["foo"]
        | #/000/tests/004/data | true  | ignores arrays                                                                   |
        # foo
        | #/000/tests/005/data | true  | ignores strings                                                                  |
        # 12
        | #/000/tests/006/data | true  | ignores other non-objects                                                        |

Scenario Outline: multiple simultaneous patternProperties are validated
/* Schema: 
{
            "patternProperties": {
                "a*": {"type": "integer"},
                "aaa*": {"maximum": 20}
            }
        }
*/
    Given the input JSON file "patternProperties.json"
    And the schema at "#/1/schema"
    And the input data at "<inputDataReference>"
    And I generate a type for the schema
    And I construct an instance of the schema type from the data
    When I validate the instance
    Then the result will be <valid>

    Examples:
        | inputDataReference   | valid | description                                                                      |
        # {"a": 21}
        | #/001/tests/000/data | true  | a single valid match is valid                                                    |
        # {"aaaa": 18}
        | #/001/tests/001/data | true  | a simultaneous match is valid                                                    |
        # {"a": 21, "aaaa": 18}
        | #/001/tests/002/data | true  | multiple matches is valid                                                        |
        # {"a": "bar"}
        | #/001/tests/003/data | false | an invalid due to one is invalid                                                 |
        # {"aaaa": 31}
        | #/001/tests/004/data | false | an invalid due to the other is invalid                                           |
        # {"aaa": "foo", "aaaa": 31}
        | #/001/tests/005/data | false | an invalid due to both is invalid                                                |

Scenario Outline: regexes are not anchored by default and are case sensitive
/* Schema: 
{
            "patternProperties": {
                "[0-9]{2,}": { "type": "boolean" },
                "X_": { "type": "string" }
            }
        }
*/
    Given the input JSON file "patternProperties.json"
    And the schema at "#/2/schema"
    And the input data at "<inputDataReference>"
    And I generate a type for the schema
    And I construct an instance of the schema type from the data
    When I validate the instance
    Then the result will be <valid>

    Examples:
        | inputDataReference   | valid | description                                                                      |
        # { "answer 1": "42" }
        | #/002/tests/000/data | true  | non recognized members are ignored                                               |
        # { "a31b": null }
        | #/002/tests/001/data | false | recognized members are accounted for                                             |
        # { "a_x_3": 3 }
        | #/002/tests/002/data | true  | regexes are case sensitive                                                       |
        # { "a_X_3": 3 }
        | #/002/tests/003/data | false | regexes are case sensitive, 2                                                    |

Scenario Outline: patternProperties with boolean schemas
/* Schema: 
{
            "patternProperties": {
                "f.*": true,
                "b.*": false
            }
        }
*/
    Given the input JSON file "patternProperties.json"
    And the schema at "#/3/schema"
    And the input data at "<inputDataReference>"
    And I generate a type for the schema
    And I construct an instance of the schema type from the data
    When I validate the instance
    Then the result will be <valid>

    Examples:
        | inputDataReference   | valid | description                                                                      |
        # {"foo": 1}
        | #/003/tests/000/data | true  | object with property matching schema true is valid                               |
        # {"bar": 2}
        | #/003/tests/001/data | false | object with property matching schema false is invalid                            |
        # {"foo": 1, "bar": 2}
        | #/003/tests/002/data | false | object with both properties is invalid                                           |
        # {"foobar":1}
        | #/003/tests/003/data | false | object with a property matching both true and false is invalid                   |
        # {}
        | #/003/tests/004/data | true  | empty object is valid                                                            |

Scenario Outline: patternProperties with null valued instance properties
/* Schema: 
{
            "patternProperties": {
                "^.*bar$": {"type": "null"}
            }
        }
*/
    Given the input JSON file "patternProperties.json"
    And the schema at "#/4/schema"
    And the input data at "<inputDataReference>"
    And I generate a type for the schema
    And I construct an instance of the schema type from the data
    When I validate the instance
    Then the result will be <valid>

    Examples:
        | inputDataReference   | valid | description                                                                      |
        # {"foobar": null}
        | #/004/tests/000/data | true  | allows null values                                                               |
