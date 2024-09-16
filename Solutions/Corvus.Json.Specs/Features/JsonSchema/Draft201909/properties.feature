@draft2019-09

Feature: properties draft2019-09
    In order to use json-schema
    As a developer
    I want to support properties in draft2019-09

Scenario Outline: object properties validation
/* Schema: 
{
            "$schema": "https://json-schema.org/draft/2019-09/schema",
            "properties": {
                "foo": {"type": "integer"},
                "bar": {"type": "string"}
            }
        }
*/
    Given the input JSON file "properties.json"
    And the schema at "#/0/schema"
    And the input data at "<inputDataReference>"
    And I assert format
    And I generate a type for the schema
    And I construct an instance of the schema type from the data
    When I validate the instance
    Then the result will be <valid>

    Examples:
        | inputDataReference   | valid | description                                                                      |
        # {"foo": 1, "bar": "baz"}
        | #/000/tests/000/data | true  | both properties present and valid is valid                                       |
        # {"foo": 1, "bar": {}}
        | #/000/tests/001/data | false | one property invalid is invalid                                                  |
        # {"foo": [], "bar": {}}
        | #/000/tests/002/data | false | both properties invalid is invalid                                               |
        # {"quux": []}
        | #/000/tests/003/data | true  | doesn't invalidate other properties                                              |
        # []
        | #/000/tests/004/data | true  | ignores arrays                                                                   |
        # 12
        | #/000/tests/005/data | true  | ignores other non-objects                                                        |

Scenario Outline: properties, patternProperties, additionalProperties interaction
/* Schema: 
{
            "$schema": "https://json-schema.org/draft/2019-09/schema",
            "properties": {
                "foo": {"type": "array", "maxItems": 3},
                "bar": {"type": "array"}
            },
            "patternProperties": {"f.o": {"minItems": 2}},
            "additionalProperties": {"type": "integer"}
        }
*/
    Given the input JSON file "properties.json"
    And the schema at "#/1/schema"
    And the input data at "<inputDataReference>"
    And I assert format
    And I generate a type for the schema
    And I construct an instance of the schema type from the data
    When I validate the instance
    Then the result will be <valid>

    Examples:
        | inputDataReference   | valid | description                                                                      |
        # {"foo": [1, 2]}
        | #/001/tests/000/data | true  | property validates property                                                      |
        # {"foo": [1, 2, 3, 4]}
        | #/001/tests/001/data | false | property invalidates property                                                    |
        # {"foo": []}
        | #/001/tests/002/data | false | patternProperty invalidates property                                             |
        # {"fxo": [1, 2]}
        | #/001/tests/003/data | true  | patternProperty validates nonproperty                                            |
        # {"fxo": []}
        | #/001/tests/004/data | false | patternProperty invalidates nonproperty                                          |
        # {"bar": []}
        | #/001/tests/005/data | true  | additionalProperty ignores property                                              |
        # {"quux": 3}
        | #/001/tests/006/data | true  | additionalProperty validates others                                              |
        # {"quux": "foo"}
        | #/001/tests/007/data | false | additionalProperty invalidates others                                            |

Scenario Outline: properties with boolean schema
/* Schema: 
{
            "$schema": "https://json-schema.org/draft/2019-09/schema",
            "properties": {
                "foo": true,
                "bar": false
            }
        }
*/
    Given the input JSON file "properties.json"
    And the schema at "#/2/schema"
    And the input data at "<inputDataReference>"
    And I assert format
    And I generate a type for the schema
    And I construct an instance of the schema type from the data
    When I validate the instance
    Then the result will be <valid>

    Examples:
        | inputDataReference   | valid | description                                                                      |
        # {}
        | #/002/tests/000/data | true  | no property present is valid                                                     |
        # {"foo": 1}
        | #/002/tests/001/data | true  | only 'true' property present is valid                                            |
        # {"bar": 2}
        | #/002/tests/002/data | false | only 'false' property present is invalid                                         |
        # {"foo": 1, "bar": 2}
        | #/002/tests/003/data | false | both properties present is invalid                                               |

Scenario Outline: properties with escaped characters
/* Schema: 
{
            "$schema": "https://json-schema.org/draft/2019-09/schema",
            "properties": {
                "foo\nbar": {"type": "number"},
                "foo\"bar": {"type": "number"},
                "foo\\bar": {"type": "number"},
                "foo\rbar": {"type": "number"},
                "foo\tbar": {"type": "number"},
                "foo\fbar": {"type": "number"}
            }
        }
*/
    Given the input JSON file "properties.json"
    And the schema at "#/3/schema"
    And the input data at "<inputDataReference>"
    And I assert format
    And I generate a type for the schema
    And I construct an instance of the schema type from the data
    When I validate the instance
    Then the result will be <valid>

    Examples:
        | inputDataReference   | valid | description                                                                      |
        # { "foo\nbar": 1, "foo\"bar": 1, "foo\\bar": 1, "foo\rbar": 1, "foo\tbar": 1, "foo\fbar": 1 }
        | #/003/tests/000/data | true  | object with all numbers is valid                                                 |
        # { "foo\nbar": "1", "foo\"bar": "1", "foo\\bar": "1", "foo\rbar": "1", "foo\tbar": "1", "foo\fbar": "1" }
        | #/003/tests/001/data | false | object with strings is invalid                                                   |

Scenario Outline: properties with null valued instance properties
/* Schema: 
{
            "$schema": "https://json-schema.org/draft/2019-09/schema",
            "properties": {
                "foo": {"type": "null"}
            }
        }
*/
    Given the input JSON file "properties.json"
    And the schema at "#/4/schema"
    And the input data at "<inputDataReference>"
    And I assert format
    And I generate a type for the schema
    And I construct an instance of the schema type from the data
    When I validate the instance
    Then the result will be <valid>

    Examples:
        | inputDataReference   | valid | description                                                                      |
        # {"foo": null}
        | #/004/tests/000/data | true  | allows null values                                                               |

Scenario Outline: properties whose names are Javascript object property names
/* Schema: 
{
            "$schema": "https://json-schema.org/draft/2019-09/schema",
            "properties": {
                "__proto__": {"type": "number"},
                "toString": {
                    "properties": { "length": { "type": "string" } }
                },
                "constructor": {"type": "number"}
            }
        }
*/
    Given the input JSON file "properties.json"
    And the schema at "#/5/schema"
    And the input data at "<inputDataReference>"
    And I assert format
    And I generate a type for the schema
    And I construct an instance of the schema type from the data
    When I validate the instance
    Then the result will be <valid>

    Examples:
        | inputDataReference   | valid | description                                                                      |
        # []
        | #/005/tests/000/data | true  | ignores arrays                                                                   |
        # 12
        | #/005/tests/001/data | true  | ignores other non-objects                                                        |
        # {}
        | #/005/tests/002/data | true  | none of the properties mentioned                                                 |
        # { "__proto__": "foo" }
        | #/005/tests/003/data | false | __proto__ not valid                                                              |
        # { "toString": { "length": 37 } }
        | #/005/tests/004/data | false | toString not valid                                                               |
        # { "constructor": { "length": 37 } }
        | #/005/tests/005/data | false | constructor not valid                                                            |
        # { "__proto__": 12, "toString": { "length": "foo" }, "constructor": 37 }
        | #/005/tests/006/data | true  | all present and valid                                                            |
