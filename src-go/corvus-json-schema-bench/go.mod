module github.com/corvus-dotnet/Corvus.JsonSchema/src-go/corvus-json-schema-bench

go 1.27.0

require (
	github.com/corvus-dotnet/Corvus.JsonSchema/src-go/corvus-json-schema v0.1.0
	github.com/santhosh-tekuri/jsonschema/v6 v6.0.1
)

require golang.org/x/text v0.14.0 // indirect

// The library as it is in this checkout.
replace github.com/corvus-dotnet/Corvus.JsonSchema/src-go/corvus-json-schema => ../corvus-json-schema
