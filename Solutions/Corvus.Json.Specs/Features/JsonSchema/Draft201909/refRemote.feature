@draft2019-09

Feature: refRemote draft2019-09
    In order to use json-schema
    As a developer
    I want to support refRemote in draft2019-09

Scenario Outline: remote ref
/* Schema: 
{
            "$schema": "https://json-schema.org/draft/2019-09/schema",
            "$ref": "http://localhost:1234/draft2019-09/integer.json"
        }
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
{
            "$schema": "https://json-schema.org/draft/2019-09/schema",
            "$ref": "http://localhost:1234/draft2019-09/subSchemas.json#/$defs/integer"
        }
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

Scenario Outline: anchor within remote ref
/* Schema: 
{
             "$schema": "https://json-schema.org/draft/2019-09/schema",
             "$ref": "http://localhost:1234/draft2019-09/locationIndependentIdentifier.json#foo"
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
        | #/002/tests/000/data | true  | remote anchor valid                                                              |
        # a
        | #/002/tests/001/data | false | remote anchor invalid                                                            |

Scenario Outline: ref within remote ref
/* Schema: 
{
            "$schema": "https://json-schema.org/draft/2019-09/schema",
            "$ref": "http://localhost:1234/draft2019-09/subSchemas.json#/$defs/refToInteger"
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
        # 1
        | #/003/tests/000/data | true  | ref within ref valid                                                             |
        # a
        | #/003/tests/001/data | false | ref within ref invalid                                                           |

Scenario Outline: base URI change
/* Schema: 
{
            "$schema": "https://json-schema.org/draft/2019-09/schema",
            "$id": "http://localhost:1234/draft2019-09/",
            "items": {
                "$id": "baseUriChange/",
                "items": {"$ref": "folderInteger.json"}
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
        # [[1]]
        | #/004/tests/000/data | true  | base URI change ref valid                                                        |
        # [["a"]]
        | #/004/tests/001/data | false | base URI change ref invalid                                                      |

Scenario Outline: base URI change - change folder
/* Schema: 
{
            "$schema": "https://json-schema.org/draft/2019-09/schema",
            "$id": "http://localhost:1234/draft2019-09/scope_change_defs1.json",
            "type" : "object",
            "properties": {"list": {"$ref": "baseUriChangeFolder/"}},
            "$defs": {
                "baz": {
                    "$id": "baseUriChangeFolder/",
                    "type": "array",
                    "items": {"$ref": "folderInteger.json"}
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

Scenario Outline: base URI change - change folder in subschema
/* Schema: 
{
            "$schema": "https://json-schema.org/draft/2019-09/schema",
            "$id": "http://localhost:1234/draft2019-09/scope_change_defs2.json",
            "type" : "object",
            "properties": {"list": {"$ref": "baseUriChangeFolderInSubschema/#/$defs/bar"}},
            "$defs": {
                "baz": {
                    "$id": "baseUriChangeFolderInSubschema/",
                    "$defs": {
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
    And the schema at "#/6/schema"
    And the input data at "<inputDataReference>"
    And I assert format
    And I generate a type for the schema
    And I construct an instance of the schema type from the data
    When I validate the instance
    Then the result will be <valid>

    Examples:
        | inputDataReference   | valid | description                                                                      |
        # {"list": [1]}
        | #/006/tests/000/data | true  | number is valid                                                                  |
        # {"list": ["a"]}
        | #/006/tests/001/data | false | string is invalid                                                                |

Scenario Outline: root ref in remote ref
/* Schema: 
{
            "$schema": "https://json-schema.org/draft/2019-09/schema",
            "$id": "http://localhost:1234/draft2019-09/object",
            "type": "object",
            "properties": {
                "name": {"$ref": "name-defs.json#/$defs/orNull"}
            }
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
        # { "name": "foo" }
        | #/007/tests/000/data | true  | string is valid                                                                  |
        # { "name": null }
        | #/007/tests/001/data | true  | null is valid                                                                    |
        # { "name": { "name": null } }
        | #/007/tests/002/data | false | object is invalid                                                                |

Scenario Outline: remote ref with ref to defs
/* Schema: 
{
            "$schema": "https://json-schema.org/draft/2019-09/schema",
            "$id": "http://localhost:1234/draft2019-09/schema-remote-ref-ref-defs1.json",
            "$ref": "ref-and-defs.json"
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
        # { "bar": 1 }
        | #/008/tests/000/data | false | invalid                                                                          |
        # { "bar": "a" }
        | #/008/tests/001/data | true  | valid                                                                            |

Scenario Outline: Location-independent identifier in remote ref
/* Schema: 
{
            "$schema": "https://json-schema.org/draft/2019-09/schema",
            "$ref": "http://localhost:1234/draft2019-09/locationIndependentIdentifier.json#/$defs/refToInteger"
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
        # 1
        | #/009/tests/000/data | true  | integer is valid                                                                 |
        # foo
        | #/009/tests/001/data | false | string is invalid                                                                |

Scenario Outline: retrieved nested refs resolve relative to their URI not $id
/* Schema: 
{
            "$schema": "https://json-schema.org/draft/2019-09/schema",
            "$id": "http://localhost:1234/draft2019-09/some-id",
            "properties": {
                "name": {"$ref": "nested/foo-ref-string.json"}
            }
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
        # { "name": {"foo": 1} }
        | #/010/tests/000/data | false | number is invalid                                                                |
        # { "name": {"foo": "a"} }
        | #/010/tests/001/data | true  | string is valid                                                                  |

Scenario Outline: remote HTTP ref with different $id
/* Schema: 
{
            "$schema": "https://json-schema.org/draft/2019-09/schema",
            "$ref": "http://localhost:1234/different-id-ref-string.json"
        }
*/
    Given the input JSON file "refRemote.json"
    And the schema at "#/11/schema"
    And the input data at "<inputDataReference>"
    And I assert format
    And I generate a type for the schema
    And I construct an instance of the schema type from the data
    When I validate the instance
    Then the result will be <valid>

    Examples:
        | inputDataReference   | valid | description                                                                      |
        # 1
        | #/011/tests/000/data | false | number is invalid                                                                |
        # foo
        | #/011/tests/001/data | true  | string is valid                                                                  |

Scenario Outline: remote HTTP ref with different URN $id
/* Schema: 
{
            "$schema": "https://json-schema.org/draft/2019-09/schema",
            "$ref": "http://localhost:1234/urn-ref-string.json"
        }
*/
    Given the input JSON file "refRemote.json"
    And the schema at "#/12/schema"
    And the input data at "<inputDataReference>"
    And I assert format
    And I generate a type for the schema
    And I construct an instance of the schema type from the data
    When I validate the instance
    Then the result will be <valid>

    Examples:
        | inputDataReference   | valid | description                                                                      |
        # 1
        | #/012/tests/000/data | false | number is invalid                                                                |
        # foo
        | #/012/tests/001/data | true  | string is valid                                                                  |

Scenario Outline: remote HTTP ref with nested absolute ref
/* Schema: 
{
            "$schema": "https://json-schema.org/draft/2019-09/schema",
            "$ref": "http://localhost:1234/nested-absolute-ref-to-string.json"
        }
*/
    Given the input JSON file "refRemote.json"
    And the schema at "#/13/schema"
    And the input data at "<inputDataReference>"
    And I assert format
    And I generate a type for the schema
    And I construct an instance of the schema type from the data
    When I validate the instance
    Then the result will be <valid>

    Examples:
        | inputDataReference   | valid | description                                                                      |
        # 1
        | #/013/tests/000/data | false | number is invalid                                                                |
        # foo
        | #/013/tests/001/data | true  | string is valid                                                                  |

Scenario Outline: $ref to $ref finds detached $anchor
/* Schema: 
{
            "$schema": "https://json-schema.org/draft/2019-09/schema",
            "$ref": "http://localhost:1234/draft2019-09/detached-ref.json#/$defs/foo"
        }
*/
    Given the input JSON file "refRemote.json"
    And the schema at "#/14/schema"
    And the input data at "<inputDataReference>"
    And I assert format
    And I generate a type for the schema
    And I construct an instance of the schema type from the data
    When I validate the instance
    Then the result will be <valid>

    Examples:
        | inputDataReference   | valid | description                                                                      |
        # 1
        | #/014/tests/000/data | true  | number is valid                                                                  |
        # a
        | #/014/tests/001/data | false | non-number is invalid                                                            |
