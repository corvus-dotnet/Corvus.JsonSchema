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
    And I generate a type for the schema
    And I construct an instance of the schema type from the data
    When I validate the instance
    Then the result will be <valid>

    Examples:
        | inputDataReference   | valid | description                                                                      |
        | #/000/tests/000/data | true  | remote ref valid                                                                 |
        | #/000/tests/001/data | false | remote ref invalid                                                               |

Scenario Outline: fragment within remote ref
/* Schema: 
{
            "$schema": "https://json-schema.org/draft/2019-09/schema",
            "$ref": "http://localhost:1234/draft2019-09/subSchemas-defs.json#/$defs/integer"
        }
*/
    Given the input JSON file "refRemote.json"
    And the schema at "#/1/schema"
    And the input data at "<inputDataReference>"
    And I generate a type for the schema
    And I construct an instance of the schema type from the data
    When I validate the instance
    Then the result will be <valid>

    Examples:
        | inputDataReference   | valid | description                                                                      |
        | #/001/tests/000/data | true  | remote fragment valid                                                            |
        | #/001/tests/001/data | false | remote fragment invalid                                                          |

Scenario Outline: ref within remote ref
/* Schema: 
{
            "$schema": "https://json-schema.org/draft/2019-09/schema",
            "$ref": "http://localhost:1234/draft2019-09/subSchemas-defs.json#/$defs/refToInteger"
        }
*/
    Given the input JSON file "refRemote.json"
    And the schema at "#/2/schema"
    And the input data at "<inputDataReference>"
    And I generate a type for the schema
    And I construct an instance of the schema type from the data
    When I validate the instance
    Then the result will be <valid>

    Examples:
        | inputDataReference   | valid | description                                                                      |
        | #/002/tests/000/data | true  | ref within ref valid                                                             |
        | #/002/tests/001/data | false | ref within ref invalid                                                           |

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
    And the schema at "#/3/schema"
    And the input data at "<inputDataReference>"
    And I generate a type for the schema
    And I construct an instance of the schema type from the data
    When I validate the instance
    Then the result will be <valid>

    Examples:
        | inputDataReference   | valid | description                                                                      |
        | #/003/tests/000/data | true  | base URI change ref valid                                                        |
        | #/003/tests/001/data | false | base URI change ref invalid                                                      |

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
    And the schema at "#/4/schema"
    And the input data at "<inputDataReference>"
    And I generate a type for the schema
    And I construct an instance of the schema type from the data
    When I validate the instance
    Then the result will be <valid>

    Examples:
        | inputDataReference   | valid | description                                                                      |
        | #/004/tests/000/data | true  | number is valid                                                                  |
        | #/004/tests/001/data | false | string is invalid                                                                |

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
    And the schema at "#/5/schema"
    And the input data at "<inputDataReference>"
    And I generate a type for the schema
    And I construct an instance of the schema type from the data
    When I validate the instance
    Then the result will be <valid>

    Examples:
        | inputDataReference   | valid | description                                                                      |
        | #/005/tests/000/data | true  | number is valid                                                                  |
        | #/005/tests/001/data | false | string is invalid                                                                |

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
    And the schema at "#/6/schema"
    And the input data at "<inputDataReference>"
    And I generate a type for the schema
    And I construct an instance of the schema type from the data
    When I validate the instance
    Then the result will be <valid>

    Examples:
        | inputDataReference   | valid | description                                                                      |
        | #/006/tests/000/data | true  | string is valid                                                                  |
        | #/006/tests/001/data | true  | null is valid                                                                    |
        | #/006/tests/002/data | false | object is invalid                                                                |

Scenario Outline: remote ref with ref to defs
/* Schema: 
{
            "$schema": "https://json-schema.org/draft/2019-09/schema",
            "$id": "http://localhost:1234/draft2019-09/schema-remote-ref-ref-defs1.json",
            "$ref": "ref-and-defs.json"
        }
*/
    Given the input JSON file "refRemote.json"
    And the schema at "#/7/schema"
    And the input data at "<inputDataReference>"
    And I generate a type for the schema
    And I construct an instance of the schema type from the data
    When I validate the instance
    Then the result will be <valid>

    Examples:
        | inputDataReference   | valid | description                                                                      |
        | #/007/tests/000/data | false | invalid                                                                          |
        | #/007/tests/001/data | true  | valid                                                                            |

Scenario Outline: Location-independent identifier in remote ref
/* Schema: 
{
            "$schema": "https://json-schema.org/draft/2019-09/schema",
            "$ref": "http://localhost:1234/draft2019-09/locationIndependentIdentifier.json#/$defs/refToInteger"
        }
*/
    Given the input JSON file "refRemote.json"
    And the schema at "#/8/schema"
    And the input data at "<inputDataReference>"
    And I generate a type for the schema
    And I construct an instance of the schema type from the data
    When I validate the instance
    Then the result will be <valid>

    Examples:
        | inputDataReference   | valid | description                                                                      |
        | #/008/tests/000/data | true  | integer is valid                                                                 |
        | #/008/tests/001/data | false | string is invalid                                                                |

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
    And the schema at "#/9/schema"
    And the input data at "<inputDataReference>"
    And I generate a type for the schema
    And I construct an instance of the schema type from the data
    When I validate the instance
    Then the result will be <valid>

    Examples:
        | inputDataReference   | valid | description                                                                      |
        | #/009/tests/000/data | false | number is invalid                                                                |
        | #/009/tests/001/data | true  | string is valid                                                                  |

Scenario Outline: remote HTTP ref with different $id
/* Schema: 
{"$ref": "http://localhost:1234/different-id-ref-string.json"}
*/
    Given the input JSON file "refRemote.json"
    And the schema at "#/10/schema"
    And the input data at "<inputDataReference>"
    And I generate a type for the schema
    And I construct an instance of the schema type from the data
    When I validate the instance
    Then the result will be <valid>

    Examples:
        | inputDataReference   | valid | description                                                                      |
        | #/010/tests/000/data | false | number is invalid                                                                |
        | #/010/tests/001/data | true  | string is valid                                                                  |

Scenario Outline: remote HTTP ref with different URN $id
/* Schema: 
{"$ref": "http://localhost:1234/urn-ref-string.json"}
*/
    Given the input JSON file "refRemote.json"
    And the schema at "#/11/schema"
    And the input data at "<inputDataReference>"
    And I generate a type for the schema
    And I construct an instance of the schema type from the data
    When I validate the instance
    Then the result will be <valid>

    Examples:
        | inputDataReference   | valid | description                                                                      |
        | #/011/tests/000/data | false | number is invalid                                                                |
        | #/011/tests/001/data | true  | string is valid                                                                  |

Scenario Outline: remote HTTP ref with nested absolute ref
/* Schema: 
{"$ref": "http://localhost:1234/nested-absolute-ref-to-string.json"}
*/
    Given the input JSON file "refRemote.json"
    And the schema at "#/12/schema"
    And the input data at "<inputDataReference>"
    And I generate a type for the schema
    And I construct an instance of the schema type from the data
    When I validate the instance
    Then the result will be <valid>

    Examples:
        | inputDataReference   | valid | description                                                                      |
        | #/012/tests/000/data | false | number is invalid                                                                |
        | #/012/tests/001/data | true  | string is valid                                                                  |
