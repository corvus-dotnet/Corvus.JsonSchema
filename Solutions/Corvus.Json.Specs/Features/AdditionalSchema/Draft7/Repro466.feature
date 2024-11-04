@draft7

Feature: Repro 466 draft7

Scenario Outline: Generation error.
	Given a schema file
		"""
		{
		  "$schema": "http://json-schema.org/draft-07/schema#",
		  "$id": "https://geojson.org/schema/GeoJSON.json",
		  "oneOf": [
			{
			  "title": "GeoJSON FeatureCollection",
			  "type": "object",
			  "properties": {
				"features": {
				  "type": "array",
				  "items": {
					"type": "object",
					"properties": {
					  "geometry": {
						"oneOf": [
						  {
							"type": "object",
							"properties": {
							  "geometries": {
								"type": "array",
								"items": {
								  "oneOf": [
									{
									  "title": "GeoJSON MultiPolygon",
									  "type": "object",
									  "properties": {
										"coordinates": {
										  "type": "array",
										  "items": {
											"type": "array",
											"items": {
											  "type": "array",
											  "items": {
												"type": "array",
												"items": {
												  "type": "number"
												}
											  }
											}
										  }
										}
									  }
									}
								  ]
								}
							  }
							}
						  }
						]
					  }
					}
				  }
				}
			  }
			}
		  ]
		}
		"""
	And the input data value <inputData>
	And I generate a type for the schema
	And I construct an instance of the schema type from the data
	When I validate the instance
	Then the result will be <valid>

Examples:
	| inputData | valid |
	| {}        | true  |