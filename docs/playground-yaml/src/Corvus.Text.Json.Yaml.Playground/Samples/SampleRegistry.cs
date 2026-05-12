namespace Corvus.Text.Json.Yaml.Playground.Samples;

/// <summary>
/// A built-in sample with associated description.
/// </summary>
public sealed class Sample
{
    public required string Id { get; init; }

    public required string DisplayName { get; init; }

    public required string Content { get; init; }

    public required string Description { get; init; }
}

/// <summary>
/// Registry of built-in samples for both YAML→JSON and JSON→YAML directions.
/// </summary>
public static class SampleRegistry
{
    public static IReadOnlyList<Sample> YamlSamples { get; } =
    [
        new Sample
        {
            Id = "basic-mapping",
            DisplayName = "Basic Mapping",
            Description = "Simple key-value pairs — the foundation of YAML configuration files.",
            Content = """
            name: Alice
            age: 30
            email: alice@example.com
            active: true
            """,
        },

        new Sample
        {
            Id = "sequences",
            DisplayName = "Sequences (Lists)",
            Description = "Block sequences using - notation, producing JSON arrays.",
            Content = """
            fruits:
              - Apple
              - Banana
              - Cherry
            colors:
              - red
              - green
              - blue
            """,
        },

        new Sample
        {
            Id = "nested-mappings",
            DisplayName = "Nested Mappings",
            Description = "Mappings within mappings, producing nested JSON objects.",
            Content = """
            server:
              host: localhost
              port: 8080
              ssl:
                enabled: true
                cert: /etc/ssl/cert.pem
                key: /etc/ssl/key.pem
            database:
              host: db.example.com
              port: 5432
              name: myapp
            """,
        },

        new Sample
        {
            Id = "invoice",
            DisplayName = "Invoice (Anchors & Aliases)",
            Description = "A real-world invoice demonstrating anchors (&), aliases (*), and nested structures.",
            Content = """
            invoice: 34843
            date: 2001-01-23
            bill-to: &id001
              given: Chris
              family: Dumars
              address:
                lines: |
                  458 Walkman Dr.
                  Suite #292
                city: Royal Oak
                state: MI
                postal: 48046
            ship-to: *id001
            product:
              - sku: BL394D
                quantity: 4
                description: Basketball
                price: 450.00
              - sku: BL4438H
                quantity: 1
                description: Super Hoop
                price: 2392.00
            tax: 251.42
            total: 4443.52
            comments: >
              Late afternoon is best.
              Backup contact is Nancy
              Billsmer @ 338-4338.
            """,
        },

        new Sample
        {
            Id = "block-scalars",
            DisplayName = "Block Scalars (Literal & Folded)",
            Description = "Literal (|) preserves newlines; folded (>) joins lines into paragraphs. Chomping controls trailing newlines.",
            Content = """
            # Literal: preserves newlines exactly
            literal_block: |
              Line 1
              Line 2
              Line 3

            # Folded: joins lines into one paragraph
            folded_block: >
              This is a long
              paragraph that gets
              folded into a single line.

            # Strip chomping (-): no trailing newline
            stripped: |-
              No trailing newline here

            # Keep chomping (+): preserve all trailing newlines
            kept: |+
              Trailing newlines
              are preserved

            """,
        },

        new Sample
        {
            Id = "flow-style",
            DisplayName = "Flow Style (Compact)",
            Description = "Inline JSON-like notation: flow mappings {} and flow sequences [].",
            Content = """
            # Flow mappings (like JSON objects)
            person: {name: Alice, age: 30, active: true}

            # Flow sequences (like JSON arrays)
            tags: [yaml, json, converter]

            # Nested flow
            config: {db: {host: localhost, port: 5432}, cache: {ttl: 300}}

            # Mixed block and flow
            servers:
              - {name: web1, ip: 10.0.0.1}
              - {name: web2, ip: 10.0.0.2}
              - {name: db1, ip: 10.0.1.1}
            """,
        },

        new Sample
        {
            Id = "data-types",
            DisplayName = "Data Types (Core Schema)",
            Description = "YAML Core Schema resolves scalars to null, bool, int, float, and string JSON types.",
            Content = """
            # Null values
            null_key: null
            tilde_null: ~
            empty_null:

            # Booleans
            bool_true: true
            bool_false: false
            bool_True: True
            bool_FALSE: FALSE

            # Integers
            decimal: 42
            negative: -17
            octal: 0o77
            hexadecimal: 0xFF

            # Floating point
            float: 3.14159
            negative_float: -0.5
            exponent: 1.2e+3
            infinity: .inf
            neg_infinity: -.inf
            not_a_number: .nan

            # Strings (anything that doesn't match above patterns)
            plain_string: hello world
            quoted_string: "hello\nworld"
            single_quoted: 'it''s a string'
            """,
        },

        new Sample
        {
            Id = "ci-pipeline",
            DisplayName = "CI Pipeline Config",
            Description = "A typical CI/CD configuration file demonstrating practical YAML patterns.",
            Content = """
            name: Build and Test

            on:
              push:
                branches: [main, develop]
              pull_request:
                branches: [main]

            env:
              DOTNET_VERSION: "10.0.x"
              BUILD_CONFIG: Release

            jobs:
              build:
                runs-on: ubuntu-latest
                steps:
                  - uses: actions/checkout@v4
                  - name: Setup .NET
                    uses: actions/setup-dotnet@v4
                    with:
                      dotnet-version: "10.0.x"
                  - name: Restore
                    run: dotnet restore
                  - name: Build
                    run: dotnet build -c Release --no-restore
                  - name: Test
                    run: dotnet test -c Release --no-build
            """,
        },

        new Sample
        {
            Id = "kubernetes",
            DisplayName = "Kubernetes Deployment",
            Description = "A Kubernetes deployment manifest — a common real-world YAML use case.",
            Content = """
            apiVersion: apps/v1
            kind: Deployment
            metadata:
              name: web-app
              labels:
                app: web-app
                version: v2
            spec:
              replicas: 3
              selector:
                matchLabels:
                  app: web-app
              template:
                metadata:
                  labels:
                    app: web-app
                    version: v2
                spec:
                  containers:
                    - name: web
                      image: myregistry/web-app:2.0
                      ports:
                        - containerPort: 8080
                      env:
                        - name: DATABASE_URL
                          value: postgres://db:5432/app
                        - name: LOG_LEVEL
                          value: info
                      resources:
                        requests:
                          memory: 128Mi
                          cpu: 100m
                        limits:
                          memory: 256Mi
                          cpu: 500m
                      livenessProbe:
                        httpGet:
                          path: /health
                          port: 8080
                        initialDelaySeconds: 15
                        periodSeconds: 10
            """,
        },

        new Sample
        {
            Id = "multi-document",
            DisplayName = "Multi-Document Stream",
            Description = "Multiple YAML documents in one file, separated by ---. Use MultiAsArray mode to parse all.",
            Content = """
            ---
            name: Document 1
            type: config
            ---
            name: Document 2
            type: data
            items:
              - alpha
              - beta
            ---
            name: Document 3
            type: metadata
            version: 1.0
            """,
        },

        new Sample
        {
            Id = "yaml11-compat",
            DisplayName = "YAML 1.1 Compatibility",
            Description = "YAML 1.1 mode: yes/no/on/off booleans and merge keys (<<).",
            Content = """
            # YAML 1.1 boolean values
            enabled: yes
            disabled: no
            feature_on: on
            feature_off: off

            # Merge key with anchors
            defaults: &defaults
              adapter: postgres
              host: localhost
              port: 5432

            development:
              <<: *defaults
              database: dev_db

            production:
              <<: *defaults
              database: prod_db
              host: db.production.com
            """,
        },

        new Sample
        {
            Id = "quoted-strings",
            DisplayName = "Quoted Strings & Escapes",
            Description = "Double-quoted strings support escape sequences; single-quoted strings only escape '' to '.",
            Content = """
            # Double-quoted: full escape support
            newline: "line1\nline2"
            tab: "col1\tcol2"
            unicode: "caf\u00E9"
            backslash: "C:\\Users\\name"

            # Single-quoted: only '' -> '
            apostrophe: 'it''s simple'
            no_escapes: 'backslash is literal: C:\path'

            # Plain scalars: no quotes needed for simple strings
            plain: just a simple string

            # Strings that look like other types (must be quoted)
            not_a_bool: "true"
            not_a_number: "42"
            not_null: "null"
            """,
        },
    ];

    public static IReadOnlyList<Sample> JsonSamples { get; } =
    [
        new Sample
        {
            Id = "json-basic",
            DisplayName = "Basic Object",
            Description = "A simple JSON object — demonstrates key-value mapping to YAML.",
            Content = """
            {
              "name": "Alice",
              "age": 30,
              "email": "alice@example.com",
              "active": true
            }
            """,
        },

        new Sample
        {
            Id = "json-nested",
            DisplayName = "Nested Object",
            Description = "Nested objects and arrays — shows how JSON structure maps to YAML indentation.",
            Content = """
            {
              "server": {
                "host": "localhost",
                "port": 8080,
                "ssl": {
                  "enabled": true,
                  "cert": "/etc/ssl/cert.pem"
                }
              },
              "database": {
                "host": "db.example.com",
                "port": 5432,
                "name": "myapp"
              }
            }
            """,
        },

        new Sample
        {
            Id = "json-arrays",
            DisplayName = "Arrays",
            Description = "JSON arrays of primitives and objects — produces YAML block sequences.",
            Content = """
            {
              "fruits": ["Apple", "Banana", "Cherry"],
              "servers": [
                {"name": "web1", "ip": "10.0.0.1"},
                {"name": "web2", "ip": "10.0.0.2"}
              ]
            }
            """,
        },

        new Sample
        {
            Id = "json-data-types",
            DisplayName = "All Data Types",
            Description = "All JSON data types: string, number, boolean, null, object, and array.",
            Content = """
            {
              "string": "hello world",
              "integer": 42,
              "float": 3.14159,
              "negative": -17,
              "exponent": 1.2e+3,
              "bool_true": true,
              "bool_false": false,
              "null_value": null,
              "empty_object": {},
              "empty_array": [],
              "nested": {
                "key": "value"
              }
            }
            """,
        },

        new Sample
        {
            Id = "json-special-strings",
            DisplayName = "Special Strings",
            Description = "Strings that require YAML quoting — booleans, numbers, reserved words, special characters.",
            Content = """
            {
              "looks_like_bool": "true",
              "looks_like_number": "42",
              "looks_like_null": "null",
              "yaml_reserved": "yes",
              "contains_colon": "key: value",
              "contains_hash": "before # after",
              "leading_space": " indented",
              "empty": "",
              "multiline": "line1\nline2\nline3"
            }
            """,
        },

        new Sample
        {
            Id = "json-package",
            DisplayName = "Package Manifest",
            Description = "A package.json-style manifest — a common real-world JSON use case.",
            Content = """
            {
              "name": "@corvus/yaml-playground",
              "version": "1.0.0",
              "description": "YAML playground demo",
              "main": "index.js",
              "scripts": {
                "build": "dotnet build",
                "test": "dotnet test",
                "start": "dotnet run"
              },
              "keywords": ["yaml", "json", "converter"],
              "author": "Endjin",
              "license": "Apache-2.0",
              "dependencies": {
                "blazor-monaco": "^3.4.0"
              }
            }
            """,
        },

        new Sample
        {
            Id = "json-api-response",
            DisplayName = "API Response",
            Description = "A typical REST API response with pagination metadata and nested data.",
            Content = """
            {
              "status": "success",
              "data": {
                "users": [
                  {
                    "id": 1,
                    "name": "Alice",
                    "email": "alice@example.com",
                    "roles": ["admin", "user"]
                  },
                  {
                    "id": 2,
                    "name": "Bob",
                    "email": "bob@example.com",
                    "roles": ["user"]
                  }
                ],
                "pagination": {
                  "page": 1,
                  "per_page": 20,
                  "total": 2,
                  "total_pages": 1
                }
              },
              "meta": {
                "request_id": "abc-123",
                "timestamp": "2025-01-15T10:30:00Z"
              }
            }
            """,
        },
    ];
}
