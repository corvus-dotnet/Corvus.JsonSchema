@draft2020-12

Feature: optional-dynamicRef draft2020-12
    In order to use json-schema
    As a developer
    I want to support optional-dynamicRef in draft2020-12

Scenario Outline: $dynamicRef skips over intermediate resources - pointer reference across resource boundary
/* Schema: 
{
        "$schema": "https://json-schema.org/draft/2020-12/schema",
        "$id": "https://test.json-schema.org/dynamic-ref-skips-intermediate-resource/optional/main",
        "type": "object",
          "properties": {
              "bar-item": {
                  "$ref": "bar#/$defs/item"
              }
          },
          "$defs": {
              "bar": {
                  "$id": "bar",
                  "type": "array",
                  "items": {
                      "$ref": "item"
                  },
                  "$defs": {
                      "item": {
                          "$id": "item",
                          "type": "object",
                          "properties": {
                              "content": {
                                  "$dynamicRef": "#content"
                              }
                          },
                          "$defs": {
                              "defaultContent": {
                                  "$dynamicAnchor": "content",
                                  "type": "integer"
                              }
                          }
                      },
                      "content": {
                          "$dynamicAnchor": "content",
                          "type": "string"
                      }
                  }
              }
          }
      }
*/
    Given the input JSON file "optional/dynamicRef.json"
    And the schema at "#/0/schema"
    And the input data at "<inputDataReference>"
    And I generate a type for the schema
    And I construct an instance of the schema type from the data
    When I validate the instance
    Then the result will be <valid>

    Examples:
        | inputDataReference   | valid | description                                                                      |
        # { "bar-item": { "content": 42 } }
        | #/000/tests/000/data | true  | integer property passes                                                          |
        # { "bar-item": { "content": "value" } }
        | #/000/tests/001/data | false | string property fails                                                            |
