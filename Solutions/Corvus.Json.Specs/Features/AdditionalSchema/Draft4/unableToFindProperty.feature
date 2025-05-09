@draft4

Feature: Unable to find property
 
Scenario: A schema that fails to generate
	Given a schema file
"""
{
  "$comment": "https://docs.codeclimate.com/docs/advanced-configuration",
  "$schema": "http://json-schema.org/draft-04/schema#",
  "definitions": {
    "enabled": {
      "type": "object",
      "properties": {
        "enabled": {
          "title": "Enabled",
          "type": "boolean",
          "default": true
        }
      }
    },
    "config": {
      "title": "Config",
      "type": "object"
    },
    "threshold": {
      "title": "Threshold",
      "type": [
        "integer",
        "null"
      ]
    }
  },
  "description": "Configuration file as an alternative for configuring your repository in the settings page.",
  "id": "https://json.schemastore.org/codeclimate.json",
  "properties": {
    "version": {
      "title": "Version",
      "description": "Required to adjust maintainability checks.",
      "type": "string",
      "default": "2"
    },
    "prepare": {
      "title": "Prepare",
      "type": "array",
      "items": {
        "type": "object",
        "properties": {
          "url": {
            "title": "URL",
            "type": "string",
            "format": "uri"
          },
          "path": {
            "title": "Path",
            "type": "string"
          }
        }
      }
    },
    "checks": {
      "title": "Checks",
      "type": "object",
      "properties": {
        "argument-count": {
          "title": "Argument Count",
          "$ref": "#/definitions/enabled",
          "properties": {
            "config": {
              "$ref": "#/definitions/config",
              "properties": {
                "threshold": {
                  "$ref": "#/definitions/threshold",
                  "default": 4
                }
              }
            }
          }
        },
        "complex-logic": {
          "title": "Complex Logic",
          "$ref": "#/definitions/enabled",
          "properties": {
            "config": {
              "$ref": "#/definitions/config",
              "properties": {
                "threshold": {
                  "$ref": "#/definitions/threshold",
                  "default": 4
                }
              }
            }
          }
        },
        "file-lines": {
          "title": "File Lines",
          "$ref": "#/definitions/enabled",
          "properties": {
            "config": {
              "$ref": "#/definitions/config",
              "properties": {
                "threshold": {
                  "$ref": "#/definitions/threshold",
                  "default": 250
                }
              }
            }
          }
        },
        "method-complexity": {
          "title": "Method Complexity",
          "$ref": "#/definitions/enabled",
          "properties": {
            "config": {
              "$ref": "#/definitions/config",
              "properties": {
                "threshold": {
                  "$ref": "#/definitions/threshold",
                  "default": 5
                }
              }
            }
          }
        },
        "method-count": {
          "title": "Method Count",
          "$ref": "#/definitions/enabled",
          "properties": {
            "config": {
              "$ref": "#/definitions/config",
              "properties": {
                "threshold": {
                  "$ref": "#/definitions/threshold",
                  "default": 20
                }
              }
            }
          }
        },
        "method-lines": {
          "title": "Method Lines",
          "$ref": "#/definitions/enabled",
          "properties": {
            "config": {
              "$ref": "#/definitions/config",
              "properties": {
                "threshold": {
                  "$ref": "#/definitions/threshold",
                  "default": 25
                }
              }
            }
          }
        },
        "nested-control-flow": {
          "title": "Nested Control Flow",
          "$ref": "#/definitions/enabled",
          "properties": {
            "config": {
              "$ref": "#/definitions/config",
              "properties": {
                "threshold": {
                  "$ref": "#/definitions/threshold",
                  "default": 4
                }
              }
            }
          }
        },
        "return-statements": {
          "title": "Return Statements",
          "$ref": "#/definitions/enabled",
          "properties": {
            "config": {
              "$ref": "#/definitions/config",
              "properties": {
                "threshold": {
                  "$ref": "#/definitions/threshold",
                  "default": 4
                }
              }
            }
          }
        },
        "similar-code": {
          "title": "Similar Code",
          "$ref": "#/definitions/enabled",
          "properties": {
            "config": {
              "$ref": "#/definitions/config",
              "properties": {
                "threshold": {
                  "$ref": "#/definitions/threshold"
                }
              }
            }
          }
        },
        "identical-code": {
          "title": "Identical Code",
          "$ref": "#/definitions/enabled",
          "properties": {
            "config": {
              "$ref": "#/definitions/config",
              "properties": {
                "threshold": {
                  "$ref": "#/definitions/threshold"
                }
              }
            }
          }
        }
      }
    },
    "plugins": {
      "title": "Plugins",
      "description": "To add a plugin to your analysis. You can find the complete list of available plugins here: https://docs.codeclimate.com/docs/list-of-engines",
      "type": "object",
      "additionalProperties": {
        "$ref": "#/definitions/enabled"
      }
    },
    "exclude_patterns": {
      "title": "Exclude Patterns",
      "type": "array",
      "items": {
        "title": "Exclude Pattern",
        "type": "string"
      }
    }
  },
  "title": "Code Climate Configuration",
  "type": "object"
}
"""
	When I generate the code for the schema
# We only need to validate that we don't get an exception here, which is fine.