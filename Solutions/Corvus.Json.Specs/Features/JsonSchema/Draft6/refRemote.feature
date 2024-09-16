@draft6

Feature: refRemote draft6
    In order to use json-schema
    As a developer
    I want to support refRemote in draft6

Scenario Outline: remote ref
/* Schema: 
{"$ref": "http://localhost:1234/integer.json"}
*/
    Given the input JSON file "refRemote.json"
    And the schema at "#/0/schema"
    And the input data at "<inputDataReference>"
    And I assert format
    And I generate a type for the schema
    And I construct an instance of the schema type from the data
    When I validate the instance
    Then the result will be <valid>

    Examples:
        | inputDataReference   | valid | description                                                                      |
        # 1
        | #/000/tests/000/data | true  | remote ref valid                                                                 |
        # a
        | #/000/tests/001/data | false | remote ref invalid                                                               |

Scenario Outline: fragment within remote ref
/* Schema: 
{"$ref": "http://localhost:1234/subSchemas.json#/definitions/integer"}
*/
    Given the input JSON file "refRemote.json"
    And the schema at "#/1/schema"
    And the input data at "<inputDataReference>"
    And I assert format
    And I generate a type for the schema
    And I construct an instance of the schema type from the data
    When I validate the instance
    Then the result will be <valid>

    Examples:
        | inputDataReference   | valid | description                                                                      |
        # 1
        | #/001/tests/000/data | true  | remote fragment valid                                                            |
        # a
        | #/001/tests/001/data | false | remote fragment invalid                                                          |

Scenario Outline: ref within remote ref
/* Schema: 
{
            "$ref": "http://localhost:1234/subSchemas.json#/definitions/refToInteger"
        }
*/
    Given the input JSON file "refRemote.json"
    And the schema at "#/2/schema"
    And the input data at "<inputDataReference>"
    And I assert format
    And I generate a type for the schema
    And I construct an instance of the schema type from the data
    When I validate the instance
    Then the result will be <valid>

    Examples:
        | inputDataReference   | valid | description                                                                      |
        # 1
        | #/002/tests/000/data | true  | ref within ref valid                                                             |
        # a
        | #/002/tests/001/data | false | ref within ref invalid                                                           |

Scenario Outline: base URI change
/* Schema: 
{
            "$id": "http://localhost:1234/",
            "items": {
                "$id": "baseUriChange/",
                "items": {"$ref": "folderInteger.json"}
            }
        }
*/
    Given the input JSON file "refRemote.json"
    And the schema at "#/3/schema"
    And the input data at "<inputDataReference>"
    And I assert format
    And I generate a type for the schema
    And I construct an instance of the schema type from the data
    When I validate the instance
    Then the result will be <valid>

    Examples:
        | inputDataReference   | valid | description                                                                      |
        # [[1]]
        | #/003/tests/000/data | true  | base URI change ref valid                                                        |
        # [["a"]]
        | #/003/tests/001/data | false | base URI change ref invalid                                                      |

Scenario Outline: base URI change - change folder
/* Schema: 
{
            "$id": "http://localhost:1234/scope_change_defs1.json",
            "type" : "object",
            "properties": {
                "list": {"$ref": "#/definitions/baz"}
            },
            "definitions": {
                "baz": {
                    "$id": "baseUriChangeFolder/",
                    "type": "array",
                    "items": {"$ref": "folderInteger.json"}
                }
            }
        }
*/
    Given the input JSON file "refRemote.json"
    And the schema at "#/4/schema"
    And the input data at "<inputDataReference>"
    And I assert format
    And I generate a type for the schema
    And I construct an instance of the schema type from the data
    When I validate the instance
    Then the result will be <valid>

    Examples:
        | inputDataReference   | valid | description                                                                      |
        # {"list": [1]}
        | #/004/tests/000/data | true  | number is valid                                                                  |
        # {"list": ["a"]}
        | #/004/tests/001/data | false | string is invalid                                                                |

Scenario Outline: base URI change - change folder in subschema
/* Schema: 
{
            "$id": "http://localhost:1234/scope_change_defs2.json",
            "type" : "object",
            "properties": {
                "list": {"$ref": "#/definitions/baz/definitions/bar"}
            },
            "definitions": {
                "baz": {
                    "$id": "baseUriChangeFolderInSubschema/",
                    "definitions": {
                        "bar": {
                            "type": "array",
                            "items": {"$ref": "folderInteger.json"}
                        }
                    }
                }
            }
        }
*/
    Given the input JSON file "refRemote.json"
    And the schema at "#/5/schema"
    And the input data at "<inputDataReference>"
    And I assert format
    And I generate a type for the schema
    And I construct an instance of the schema type from the data
    When I validate the instance
    Then the result will be <valid>

    Examples:
        | inputDataReference   | valid | description                                                                      |
        # {"list": [1]}
        | #/005/tests/000/data | true  | number is valid                                                                  |
        # {"list": ["a"]}
        | #/005/tests/001/data | false | string is invalid                                                                |

Scenario Outline: root ref in remote ref
/* Schema: 
{
            "$id": "http://localhost:1234/object",
            "type": "object",
            "properties": {
                "name": {"$ref": "name.json#/definitions/orNull"}
            }
        }
*/
    Given the input JSON file "refRemote.json"
    And the schema at "#/6/schema"
    And the input data at "<inputDataReference>"
    And I assert format
    And I generate a type for the schema
    And I construct an instance of the schema type from the data
    When I validate the instance
    Then the result will be <valid>

    Examples:
        | inputDataReference   | valid | description                                                                      |
        # { "name": "foo" }
        | #/006/tests/000/data | true  | string is valid                                                                  |
        # { "name": null }
        | #/006/tests/001/data | true  | null is valid                                                                    |
        # { "name": { "name": null } }
        | #/006/tests/002/data | false | object is invalid                                                                |

Scenario Outline: remote ref with ref to definitions
/* Schema: 
{
            "$id": "http://localhost:1234/schema-remote-ref-ref-defs1.json",
            "allOf": [
                { "$ref": "ref-and-definitions.json" }
            ]
        }
*/
    Given the input JSON file "refRemote.json"
    And the schema at "#/7/schema"
    And the input data at "<inputDataReference>"
    And I assert format
    And I generate a type for the schema
    And I construct an instance of the schema type from the data
    When I validate the instance
    Then the result will be <valid>

    Examples:
        | inputDataReference   | valid | description                                                                      |
        # { "bar": 1 }
        | #/007/tests/000/data | false | invalid                                                                          |
        # { "bar": "a" }
        | #/007/tests/001/data | true  | valid                                                                            |

Scenario Outline: Location-independent identifier in remote ref
/* Schema: 
{
            "$ref": "http://localhost:1234/locationIndependentIdentifierPre2019.json#/definitions/refToInteger"
        }
*/
    Given the input JSON file "refRemote.json"
    And the schema at "#/8/schema"
    And the input data at "<inputDataReference>"
    And I assert format
    And I generate a type for the schema
    And I construct an instance of the schema type from the data
    When I validate the instance
    Then the result will be <valid>

    Examples:
        | inputDataReference   | valid | description                                                                      |
        # 1
        | #/008/tests/000/data | true  | integer is valid                                                                 |
        # foo
        | #/008/tests/001/data | false | string is invalid                                                                |

Scenario Outline: retrieved nested refs resolve relative to their URI not $id
/* Schema: 
{
            "$id": "http://localhost:1234/some-id",
            "properties": {
                "name": {"$ref": "nested/foo-ref-string.json"}
            }
        }
*/
    Given the input JSON file "refRemote.json"
    And the schema at "#/9/schema"
    And the input data at "<inputDataReference>"
    And I assert format
    And I generate a type for the schema
    And I construct an instance of the schema type from the data
    When I validate the instance
    Then the result will be <valid>

    Examples:
        | inputDataReference   | valid | description                                                                      |
        # { "name": {"foo": 1} }
        | #/009/tests/000/data | false | number is invalid                                                                |
        # { "name": {"foo": "a"} }
        | #/009/tests/001/data | true  | string is valid                                                                  |

Scenario Outline: $ref to $ref finds location-independent $id
/* Schema: 
{
            "$ref": "http://localhost:1234/draft6/detached-ref.json#/definitions/foo"
        }
*/
    Given the input JSON file "refRemote.json"
    And the schema at "#/10/schema"
    And the input data at "<inputDataReference>"
    And I assert format
    And I generate a type for the schema
    And I construct an instance of the schema type from the data
    When I validate the instance
    Then the result will be <valid>

    Examples:
        | inputDataReference   | valid | description                                                                      |
        # 1
        | #/010/tests/000/data | true  | number is valid                                                                  |
        # a
        | #/010/tests/001/data | false | non-number is invalid                                                            |
