@draft7

Feature: Repro 467 draft7

Scenario Outline: Validation error.
	Given a schema file
		"""
		{
		  "$schema": "http://json-schema.org/draft-07/schema#",
		  "$id": "urn:opcua:v2.0.0-rc.6",
		  "title": "OPC UA Schema",
		  "type": "object",
		  "properties": {
			"sources": {
			  "type": "object",
			  "propertyNames": {
				"$ref": "#/definitions/equipmentNumber"
			  },
			  "additionalProperties": {
				"$ref": "#/definitions/source"
			  },
			  "minProperties": 1
			},
			"sinks": {
			  "type": "object",
			  "propertyNames": {
				"$ref": "#/definitions/equipmentNumber"
			  },
			  "additionalProperties": {
				"$ref": "#/definitions/sink"
			  },
			  "minProperties": 1
			},
			"machines": {
			  "type": "object",
			  "propertyNames": {
				"$ref": "#/definitions/equipmentNumber"
			  },
			  "additionalProperties": {
				"$ref": "#/definitions/machine"
			  },
			  "minProperties": 1
			}
		  },
		  "definitions": {
			"key": {
			  "type": "string",
			  "pattern": "^[A-Za-z0-9_-]+$"
			},
			"equipmentNumber": {
			  "$ref": "#/definitions/key",
			  "description": "Equipment number of the machine"
			},
			"source": {
			  "type": "object",
			  "description": "Input source where to get data from",
			  "properties": {
				"ipAddress": {
				  "type": "string",
				  "description": "Ip address of the source server"
				},
				"port": {
				  "type": "string",
				  "description": "Port of the source server"
				},
				"type": {
				  "type": "string",
				  "description": "Type of the source equipment",
				  "default": "plc",
				  "enum": [
					"softopcua",
					"plc"
				  ]
				}
			  },
			  "required": [
				"ipAddress",
				"port",
				"type"
			  ],
			  "additionalProperties": false
			},
			"sink": {
			  "type": "object",
			  "description": "Output sink where to publish machine data",
			  "properties": {
				"subject": {
				  "type": "string",
				  "description": "Subject where to publish data"
				}
			  },
			  "required": [
				"subject"
			  ],
			  "additionalProperties": false
			},
			"machine": {
			  "type": "object",
			  "description": "Define machine severity to message mapping",
			  "properties": {
				"productionLine": {
				  "$ref": "#/definitions/equipmentNumber",
				  "description": "Parent production line of the machine"
				},
				"languageCode": {
				  "type": "string",
				  "description": "https://en.wikipedia.org/wiki/List_of_ISO_639_language_codes"
				},
				"messageTypeMapping": {
				  "type": "array",
				  "description": "Mapping informations from severity to message type",
				  "items": {
					"$ref": "#/definitions/messageTypeMappingElement"
				  }
				},
				"variableMapping": {
				  "type": "array",
				  "description": "Get information from OPC UA variables",
				  "items": {
					"$ref": "#/definitions/variableMappingElement"
				  }
				}
			  },
			  "required": [
				"productionLine",
				"languageCode",
				"messageTypeMapping",
				"variableMapping"
			  ],
			  "additionalProperties": false
			},
			"messageTypeMappingElement": {
			  "type": "array",
			  "items": [
				{
				  "type": "integer",
				  "description": "Lower bound inclusive"
				},
				{
				  "oneOf": [
					{
					  "type": "integer"
					},
					{
					  "type": "null"
					}
				  ],
				  "description": "Higher bound inclusive"
				},
				{
				  "type": "string",
				  "description": "Type of the message"
				}
			  ],
			  "minItems": 3,
			  "maxItems": 3
			},
			"variableMappingElement": {
			  "type": "object",
			  "properties": {
				"from": {
				  "$ref": "#/definitions/opcuaVariable"
				},
				"to": {
				  "$ref": "#/definitions/smpTimeSeries"
				},
				"transforms": {
				  "type": "array",
				  "items": {
					"$ref": "#/definitions/transform"
				  }
				}
			  },
			  "required": [
				"from",
				"to"
			  ],
			  "additionalProperties": false
			},
			"opcuaVariable": {
			  "type": "object",
			  "properties": {
				"namespace": {
				  "type": "integer",
				  "description": "Namespace of the variable",
				  "minimum": 0,
				  "maximum": 127
				},
				"identifier": {
				  "type": "string",
				  "description": "Identifier of the variable"
				},
				"type": {
				  "type": "string",
				  "description": "expected OPCUA data type of the variable value",
				  "enum": [
					"Boolean",
					"SByte",
					"Byte",
					"Int16",
					"UInt16",
					"Int32",
					"UInt32",
					"Int64",
					"UInt64",
					"Float",
					"Double",
					"String",
					"DatTime",
					"Guid",
					"ByteString",
					"XmlElement",
					"NodeId",
					"ExpandedNodeId",
					"StatusCode",
					"QualifiedName",
					"LocalizedText",
					"ExtensionObject",
					"DataValue",
					"Variant",
					"DiagnosticInfo"
				  ]
				}
			  },
			  "required": [
				"namespace",
				"identifier",
				"type"
			  ],
			  "additionalProperties": false
			},
			"smpTimeSeries": {
			  "type": "object",
			  "properties": {
				"measurement": {
				  "type": "string",
				  "description": "Measurement name in InfluxDB"
				},
				"field": {
				  "type": "string",
				  "description": "Field name in InfluxDB"
				},
				"tags": {
				  "type": "object",
				  "additionalProperties": {
					"type": "string"
				  }
				},
				"type": {
				  "type": "string",
				  "description": "Type of the variable",
				  "default": "string",
				  "enum": [
					"real",
					"string",
					"integer",
					"boolean"
				  ]
				}
			  },
			  "required": [
				"measurement",
				"field",
				"type"
			  ],
			  "additionalProperties": false
			},
			"transform": {
			  "type": "object",
			  "properties": {
				"mapping": {
				  "$ref": "#/definitions/mappingTransform"
				},
				"unitConversion": {
				  "$ref": "#/definitions/unitConversionTransform"
				},
				"valueOverride": {
				  "$ref": "#/definitions/valueOverrideTransform"
				}
			  },
			  "minProperties": 1,
			  "maxProperties": 1,
			  "additionalProperties": false
			},
			"mappingTransform": {
			  "description": "Mapping from one value to another",
			  "oneOf": [
				{
				  "$ref": "#/definitions/mappingIntegerToString"
				},
				{
				  "$ref": "#/definitions/mappingStringToString"
				}
			  ]
			},
			"mappingIntegerToString": {
			  "type": "array",
			  "description": "Mapping from integer to string",
			  "items": {
				"type": "array",
				"items": [
				  {
					"type": "integer"
				  },
				  {
					"type": "string"
				  }
				],
				"minItems": 2,
				"maxItems": 2
			  }
			},
			"mappingStringToString": {
			  "type": "array",
			  "description": "Mapping from string to string",
			  "items": {
				"type": "array",
				"items": [
				  {
					"type": "string"
				  },
				  {
					"type": "string"
				  }
				],
				"minItems": 2,
				"maxItems": 2
			  }
			},
			"unitConversionTransform": {
			  "type": "array",
			  "description": "Conversion from one unit to another",
			  "items": [
				{
				  "type": "string",
				  "description": "Value which should be mapped",
				  "default": "ms",
				  "enum": [
					"1/min",
					"1/s",
					"ms",
					"s"
				  ]
				},
				{
				  "type": "string",
				  "description": "Value to which should be mapped",
				  "default": "s",
				  "enum": [
					"1/min",
					"1/s",
					"ms",
					"s"
				  ]
				}
			  ],
			  "minItems": 2,
			  "maxItems": 2
			},
			"valueOverrideTransform": {
			  "type": "object",
			  "properties": {
				"condition": {
				  "type": "object",
				  "properties": {
					"from": {
					  "$ref": "#/definitions/opcuaVariable"
					},
					"value": {
					  "oneOf": [
						{
						  "type": "array",
						  "items": {
							"type": "integer"
						  }
						},
						{
						  "type": "array",
						  "items": {
							"type": "string"
						  }
						}
					  ],
					  "description": "Value to override"
					}
				  },
				  "required": [
					"from",
					"value"
				  ],
				  "additionalProperties": false
				},
				"newValue": {
				  "oneOf": [
					{
					  "type": "integer"
					},
					{
					  "type": "string"
					},
					{
					  "type": "boolean"
					}
				  ],
				  "description": "New value"
				}
			  },
			  "required": [
				"condition",
				"newValue"
			  ],
			  "additionalProperties": false
			}
		  }
		}
		"""
	And the input data value <inputData>
	And I generate a type for the schema
	And I construct an instance of the schema type from the data
	When I validate the instance with level Detailed
	Then the result will be <valid>

Examples:
	| inputData                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                        | valid |
	| {"machines":{"testplc2":{"productionLine":"testline1","languageCode":"de","messageTypeMapping":[],"variableMapping":[{"from":{"namespace":3,"identifier":"state","type":"Int64"},"transforms":[{"mapping":[[0,"undefined"],[5,"auto-off-setup"],[10,"auto-off-stop-initial"],[15,"auto-off-stop-idling"],[16,"auto-off-stop-idling"],[20,"auto-waiting-no-interaction-required"],[26,"auto-waiting-interaction-required"],[30,"stop-emergency"],[35,"stop-error"],[40,"auto-off"],[50,"auto-on"]]}],"to":{"measurement":"state","field":"state_hint","type":"string"}}]}}}                                                                                                                                                                                                                                                       | true  |
	| {"sources":{"testplc1":{"ipAddress":"127.0.0.1","port":"32796","type":"softopcua"},"testplc2":{"ipAddress":"127.0.0.1","port":"32797","type":"softopcua"}},"machines":{"testplc1":{"productionLine":"testline1","languageCode":"de","messageTypeMapping":[],"variableMapping":[]},"testplc2":{"productionLine":"testline1","languageCode":"de","messageTypeMapping":[],"variableMapping":[{"from":{"namespace":3,"identifier":"state","type":"Int64"},"transforms":[{"valueOverride":{"condition":{"from":{"namespace":3,"identifier":"state_ecomode-active","type":"Boolean"},"value":[true]},"newValue":"auto-off-ecomode"}}],"to":{"measurement":"state","field":"state_hint","type":"string"}}]}},"sinks":{"testplc1":{"subject":"smpoac.test.eu.de.dev.testplc1"},"testplc2":{"subject":"smpoac.test.eu.de.dev.testplc2"}}} | false |