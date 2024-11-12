@draft7

Feature: Repro 479 draft7

Scenario Outline: Generation error with regular expressions.
	Given a schema file
		"""
		{
			"$schema": "http://json-schema.org/draft-07/schema#",
			"title": "Schema validation for KrakenD",
			"type": "object",
			"required": [
				"version"
			],
			"properties": {
				"async_agent": {
					"$ref": "#/definitions/https%3A~1~1www.krakend.io~1schema~1v2.7~1async_agent.json"
				},
				"cache_ttl": {
					"title": "Cache TTL",
					"description": "Sets a default `Cache-Control: public, max-age=%d` header to all endpoints where `%d` is the conversion to seconds of any duration you write, indicating for how long the client (or CDN) can cache the content of the request. You can override this value per endpoint, but setting an endpoint to 0 will use the default value instead. Notice that KrakenD does not cache the content with this parameter, but tells the client how to do it. Defaults to `0s` (no cache). **For KrakenD cache, see [backend caching](https://www.krakend.io/docs/backends/caching/)**.",
					"default": "0s",
					"$ref": "#/definitions/https%3A~1~1www.krakend.io~1schema~1v2.7~1timeunits.json/$defs/timeunit",
					"type": "string"
				},
				"client_tls": {
					"title": "TLS Client settings",
					"description": "Allows to set specific transport settings when using TLS in your upstream services. See [TLS Client](https://www.krakend.io/docs/service-settings/tls/) for more settings",
					"$ref": "#/definitions/https%3A~1~1www.krakend.io~1schema~1v2.7~1client_tls.json"
				},
				"debug_endpoint": {
					"title": "Debug endpoint",
					"description": "Enables the `/__debug/` endpoint for this configuration. You can safely enable it in production.",
					"default": false,
					"type": "boolean"
				},
				"dialer_fallback_delay": {
					"title": "Dialer fallback delay",
					"description": "Specifies the length of time to wait before spawning a RFC 6555 Fast Fallback connection. If zero, a default delay of 300ms is used.\n\nSee: https://www.krakend.io/docs/service-settings/http-transport-settings/",
					"default": "300ms",
					"$ref": "#/definitions/https%3A~1~1www.krakend.io~1schema~1v2.7~1timeunits.json/$defs/timeunit",
					"type": "string"
				},
				"dialer_keep_alive": {
					"title": "Dialer keep alive",
					"description": "The interval between keep-alive probes for an active network connection. If zero, keep-alive probes are sent with a default value (currently 15 seconds), if supported by the protocol and operating system. Network protocols or operating systems that do not support keep-alives ignore this field. If negative, keep-alive probes are disabled.\n\nSee: https://www.krakend.io/docs/service-settings/http-transport-settings/",
					"default": "15s",
					"$ref": "#/definitions/https%3A~1~1www.krakend.io~1schema~1v2.7~1timeunits.json/$defs/timeunit",
					"type": "string"
				},
				"dialer_timeout": {
					"title": "Dialer Timeout",
					"description": "The timeout of the dial function for creating connections.The default is no timeout. With or without a timeout, the operating system may impose its own earlier timeout.\n\nSee: https://www.krakend.io/docs/service-settings/http-transport-settings/",
					"default": "0s",
					"$ref": "#/definitions/https%3A~1~1www.krakend.io~1schema~1v2.7~1timeunits.json/$defs/timeunit",
					"type": "string"
				},
				"disable_compression": {
					"title": "Disable compression",
					"description": "When true prevents requesting compression with an `Accept-Encoding: gzip` request header when the Request contains no existing Accept-Encoding value. If the Transport requests gzip on its own and gets a gzipped response, it's transparently decoded. However, if the user explicitly requested gzip it is not automatically uncompressed.\n\nSee: https://www.krakend.io/docs/service-settings/http-transport-settings/",
					"default": false,
					"type": "boolean"
				},
				"disable_keep_alives": {
					"title": "Disable keep alives",
					"description": "When true it disables HTTP keep-alives and will only use the connection to the server for a single HTTP request.\n\nSee: https://www.krakend.io/docs/service-settings/http-transport-settings/",
					"default": false,
					"type": "boolean"
				},
				"disable_rest": {
					"title": "Disable RESTful URLs",
					"description": "Only RESTful URL patterns are valid to access backends. Set to true if your backends aren't RESTful, e.g.: `/url.{some_variable}.json`",
					"default": false,
					"type": "boolean"
				},
				"dns_cache_ttl": {
					"title": "DNS Cache TTL",
					"description": "Sets the maximum time KrakenD can store the results of a query to the configured Service Discovery returning the available hosts list. For values under `1s` this setting is ignored.\n\nSee: https://www.krakend.io/docs/backends/service-discovery/",
					"default": "30s",
					"$ref": "#/definitions/https%3A~1~1www.krakend.io~1schema~1v2.7~1timeunits.json/$defs/timeunit",
					"type": "string"
				},
				"echo_endpoint": {
					"title": "Echo endpoint",
					"description": "Enables the `/__echo/` endpoint for this configuration, that returns information about the incoming request. When using /__echo as a backend you can check the actual headers and content a backend receives after all the zero-trust filtering.",
					"default": false,
					"type": "boolean"
				},
				"endpoints": {
					"title": "Endpoints",
					"description": "Your API contract, or the list of all paths recognized by this gateway. The paths `/__health/`, `/__debug/`, `/__echo/`, `/__catchall`, and `/__stats/` are reserved by the system and you cannot declare them. Their existence depends on their respective settings.\n\nSee: https://www.krakend.io/docs/endpoints/",
					"type": "array",
					"items": {
						"$ref": "#/definitions/https%3A~1~1www.krakend.io~1schema~1v2.7~1endpoint.json",
						"type": "object"
					}
				},
				"expect_continue_timeout": {
					"title": "Expect_continue_timeout",
					"description": "If non-zero, specifies the amount of time to wait for a server's first response headers after fully writing the request headers if the request has an `Expect: 100-continue` header. Zero means no timeout and causes the body to be sent immediately, without waiting for the server to approve. This time does not include the time to send the request header.\n\nSee: https://www.krakend.io/docs/service-settings/http-transport-settings/",
					"default": "0s",
					"$ref": "#/definitions/https%3A~1~1www.krakend.io~1schema~1v2.7~1timeunits.json/$defs/timeunit",
					"type": "string"
				},
				"extra_config": {
					"title": "Extra configuration",
					"description": "The optional configuration that extends the core functionality of the gateway is specified here. The `extra_config` at this level enables service components, meaning that they apply globally to all endpoints or activity.",
					"$ref": "#/definitions/https%3A~1~1www.krakend.io~1schema~1v2.7~1service_extra_config.json",
					"type": "object"
				},
				"host": {
					"title": "Default host",
					"description": "The default host list for all backends if they specify none.",
					"type": "array",
					"items": {
						"type": "string"
					}
				},
				"idle_connection_timeout": {
					"title": "HTTP Idle timeout",
					"description": "The maximum number of idle (keep-alive) connections across all hosts. Zero means no limit.\n\nSee: https://www.krakend.io/docs/service-settings/http-transport-settings/",
					"default": "0s",
					"$ref": "#/definitions/https%3A~1~1www.krakend.io~1schema~1v2.7~1timeunits.json/$defs/timeunit",
					"type": "string"
				},
				"idle_timeout": {
					"title": "HTTP Idle timeout",
					"description": "The maximum amount of time to wait for the next request when keep-alives are enabled. If `idle_timeout` is zero, the value of `read_timeout` is used. If both are zero, there is no timeout.\n\nSee: https://www.krakend.io/docs/service-settings/http-server-settings/",
					"default": "0s",
					"$ref": "#/definitions/https%3A~1~1www.krakend.io~1schema~1v2.7~1timeunits.json/$defs/timeunit",
					"type": "string"
				},
				"listen_ip": {
					"title": "Listen IP",
					"description": "The IP address that KrakenD listens to in IPv4 or IPv6. An empty string, or no declaration at all means listening on all interfaces. The inclusion of `::` is meant for IPv6 format only (**this is not the port**). Examples of valid addresses are `192.0.2.1` (IPv4), `2001:db8::68` (IPv6). The values `::` and `0.0.0.0` listen to all addresses and both are valid for IPv4 and IPv6 simultaneously.",
					"examples": [
						"172.12.1.1",
						"::1"
					],
					"default": "0.0.0.0",
					"type": "string"
				},
				"max_header_bytes": {
					"title": "Max header bytes",
					"description": "Allows overriding the maximum size of headers sent in bytes. It does not limit the request body. When the value is zero, the default is used instead (1MB)\n\nSee: https://www.krakend.io/docs/service-settings/http-transport-settings/",
					"default": 1000000,
					"type": "integer"
				},
				"max_idle_connections": {
					"title": "Max idle connections",
					"description": "The maximum number of idle (keep-alive) connections across all hosts. Zero means no limit.\n\nSee: https://www.krakend.io/docs/service-settings/http-transport-settings/",
					"default": 0,
					"type": "integer"
				},
				"max_idle_connections_per_host": {
					"title": "Max idle connections per host",
					"description": "If non-zero, controls the maximum idle (keep-alive) connections to keep per-host. If zero, `250` is used instead.\n\nSee: https://www.krakend.io/docs/service-settings/http-transport-settings/",
					"default": 250,
					"type": "integer"
				},
				"name": {
					"title": "Name",
					"description": "Used in telemetry. A friendly name, title, date, version or any other short description that helps you recognize the configuration.",
					"default": "KrakenD configuration at MyCompany",
					"type": "string"
				},
				"output_encoding": {
					"title": "Output Encoding",
					"description": "The encoding used to display the content to the end-user. This setting is the default for all endpoints, unless they have another `output_encoding` overrinding this value.\n\nSee: https://www.krakend.io/docs/endpoints/content-types/",
					"default": "json",
					"enum": [
						"json",
						"fast-json",
						"json-collection",
						"xml",
						"negotiate",
						"string",
						"no-op"
					]
				},
				"plugin": {
					"title": "Plugin",
					"description": "Enables external plugins that are copied in a specific folder",
					"type": "object",
					"required": [
						"pattern",
						"folder"
					],
					"properties": {
						"pattern": {
							"title": "Pattern",
							"description": "The pattern narrows down the contents of the folder. It represents the substring that must be present in the plugin name to load.",
							"examples": [
								".so",
								"-production.so"
							],
							"default": ".so",
							"type": "string"
						},
						"folder": {
							"title": "Folder",
							"description": "The path in the filesystem where all the plugins you want to load are. MUST END IN SLASH. The folder can be a relative or absolute path. KrakenD Enterprise uses /opt/krakend/plugins/ for all plugins.",
							"examples": [
								"/opt/krakend/plugins/",
								"./plugins/"
							],
							"default": "/opt/krakend/plugins/",
							"type": "string"
						}
					}
				},
				"port": {
					"title": "Port",
					"description": "The TCP port where KrakenD is listening to. Recommended value is in the range 1024-65535 to run as an unpriviliged user",
					"default": 8080,
					"type": "integer",
					"maximum": 65535,
					"minimum": 0
				},
				"read_header_timeout": {
					"title": "HTTP Idle timeout",
					"description": "The amount of time allowed to read request headers. The connection's read deadline is reset after reading the headers and the Handler can decide what is considered too slow for the body.\n\nSee: https://www.krakend.io/docs/service-settings/http-server-settings/",
					"default": "0s",
					"$ref": "#/definitions/https%3A~1~1www.krakend.io~1schema~1v2.7~1timeunits.json/$defs/timeunit",
					"type": "string"
				},
				"read_timeout": {
					"title": "HTTP read timeout",
					"description": "Is the maximum duration for reading the entire request, including the body. Because `read_timeout` does not let Handlers make per-request decisions on each request body's acceptable deadline or upload rate, most users will prefer to use `read_header_timeout`. It is valid to use them both.\n\nSee: https://www.krakend.io/docs/service-settings/http-server-settings/",
					"default": "0s",
					"$ref": "#/definitions/https%3A~1~1www.krakend.io~1schema~1v2.7~1timeunits.json/$defs/timeunit",
					"type": "string"
				},
				"response_header_timeout": {
					"title": "Response header timeout",
					"description": "If non-zero, specifies the amount of time to wait for a server's response headers after fully writing the request (including its body, if any). This time does not include the time to read the response body.\n\nSee: https://www.krakend.io/docs/service-settings/http-transport-settings/",
					"default": "0s",
					"$ref": "#/definitions/https%3A~1~1www.krakend.io~1schema~1v2.7~1timeunits.json/$defs/timeunit",
					"type": "string"
				},
				"sequential_start": {
					"title": "Sequential start",
					"description": "A sequential start registers all async agents in order, allowing you to have the starting logs in sequential order. A non-sequential start is much faster, but logs are harder to follow.\n\nSee: https://www.krakend.io/docs/service-settings/http-server-settings/",
					"default": false,
					"type": "boolean"
				},
				"timeout": {
					"title": "Global timeout",
					"description": "Defines a default timeout for all endpoints. Can be overriden per endpoint.\n\nSee: https://www.krakend.io/docs/service-settings/http-transport-settings/#global-timeout",
					"default": "2s",
					"$ref": "#/definitions/https%3A~1~1www.krakend.io~1schema~1v2.7~1timeunits.json/$defs/timeunit",
					"type": "string"
				},
				"tls": {
					"$ref": "#/definitions/https%3A~1~1www.krakend.io~1schema~1v2.7~1tls.json"
				},
				"use_h2c": {
					"title": "Enable h2c",
					"description": "Enable the support for HTTP/2 with no TLS (clear text). This option is less secure and less performant, use with caution.\n\nSee: https://www.krakend.io/docs/service-settings/router-options/",
					"default": false,
					"type": "boolean"
				},
				"version": {
					"title": "Version of this syntax",
					"description": "The syntax version tells KrakenD how to read this configuration. This is not the KrakenD version. Each KrakenD version is linked to a syntax version, and since KrakenD v2.0 the version must be `3`",
					"const": 3
				},
				"write_timeout": {
					"title": "HTTP write timeout",
					"description": "Maximum duration before timing out writes of the response.\n\nSee: https://www.krakend.io/docs/service-settings/http-server-settings/",
					"default": "0s",
					"$ref": "#/definitions/https%3A~1~1www.krakend.io~1schema~1v2.7~1timeunits.json/$defs/timeunit",
					"type": "string"
				}
			},
			"patternProperties": {
				"^[@$_#]": {}
			},
			"additionalProperties": false,
			"definitions": {
				"https://www.krakend.io/schema/v2.7/async/amqp.json": {
					"$schema": "http://json-schema.org/draft-07/schema#",
					"title": "Async AMQP Driver",
					"description": "The Async AMQP component enables the AMQP driver for the Async functionality.\n\nSee: https://www.krakend.io/docs/async/amqp/",
					"type": "object",
					"required": [
						"name",
						"host",
						"exchange"
					],
					"properties": {
						"auto_ack": {
							"title": "Auto ACK",
							"description": "When KrakenD retrieves the messages, regardless of the success or failure of the operation, it marks them as ACK. When auto ACK is not used, only successful backend responses do the ACK, and failing messages are requeued. Defaults to `false`.\n\nSee: https://www.krakend.io/docs/async/amqp/",
							"default": false,
							"type": "boolean"
						},
						"delete": {
							"title": "Delete",
							"description": "When `true`, AMQP deletes the queue when there are no remaining connections. This option is **not recommended** in most of the scenarios. If for instance, the connectivity between KrakenD and AMQP is lost for whatever reason and it's the only client, AMQP will delete the queue no matter the number of messages there are inside, and when KrakenD gets the connection again the queue won't exist and future connections will recreate it again.\n\nSee: https://www.krakend.io/docs/async/amqp/",
							"default": false,
							"type": "boolean"
						},
						"durable": {
							"title": "Durable",
							"description": "Durable queues will survive server restarts and remain when there are no remaining consumers or bindings. Most of the times `true` is recommended, but depends on the use case.\n\nSee: https://www.krakend.io/docs/async/amqp/",
							"default": false,
							"type": "boolean"
						},
						"exchange": {
							"title": "Exchange",
							"description": "The entity name where messages are retrieved (it will be created, or it must have a **topic** type if already exists).\n\nSee: https://www.krakend.io/docs/async/amqp/",
							"examples": [
								"some-exchange"
							],
							"type": "string"
						},
						"exclusive": {
							"title": "Exclusive",
							"description": "When `true`, AMQP will allow **a single KrakenD client** to access the queue. This option is **not recommended** in environments where the gateway needs high availability and you have several instances running.\n\nSee: https://www.krakend.io/docs/async/amqp/",
							"default": false,
							"type": "boolean"
						},
						"host": {
							"title": "Host",
							"description": "The connection string, ends in slash. E.g: `amqp://user:password@host:5672/`.\n\nSee: https://www.krakend.io/docs/async/amqp/",
							"type": "string"
						},
						"name": {
							"title": "Name",
							"description": "The queue name.\n\nSee: https://www.krakend.io/docs/async/amqp/",
							"type": "string"
						},
						"no_local": {
							"title": "No local",
							"description": "The no_local flag is not supported by RabbitMQ.\n\nSee: https://www.krakend.io/docs/async/amqp/",
							"type": "boolean"
						},
						"no_wait": {
							"title": "No wait",
							"description": "When true, do not wait for the server to confirm the request and immediately begin deliveries. If it is not possible to consume, a channel exception will be raised and the channel will be closed.\n\nSee: https://www.krakend.io/docs/async/amqp/",
							"type": "boolean"
						},
						"prefetch_count": {
							"title": "Prefetch count",
							"description": "The number of messages you want to prefetch prior to consume them.\n\nSee: https://www.krakend.io/docs/async/amqp/",
							"default": 10,
							"type": "integer"
						},
						"prefetch_size": {
							"title": "Prefetch size",
							"description": "The number of bytes you want to use to prefetch messages.\n\nSee: https://www.krakend.io/docs/async/amqp/",
							"default": 0,
							"type": "integer"
						}
					},
					"patternProperties": {
						"^[@$_#]": {}
					},
					"additionalProperties": false
				},
				"https://www.krakend.io/schema/v2.7/async_agent.json": {
					"$schema": "http://json-schema.org/draft-07/schema#",
					"title": "Async Agents",
					"description": "Async agents are routines listening to queues or PubSub systems that react to new events and push data to your backends. Through async agents, you can start a lot of consumers to process your events autonomously.\n\nSee: https://www.krakend.io/docs/async/",
					"type": "array",
					"items": {
						"required": [
							"name",
							"consumer",
							"backend",
							"extra_config"
						],
						"properties": {
							"backend": {
								"title": "Backend definition",
								"description": "The [backend definition](https://www.krakend.io/docs/backends/) (as you might have in any endpoint) indicating where the event data is sent. It is a full backend object definition, with all its possible options, transformations, filters, validations, etc.",
								"type": "array",
								"items": {
									"$ref": "#/definitions/https%3A~1~1www.krakend.io~1schema~1v2.7~1backend.json"
								}
							},
							"connection": {
								"title": "Connection",
								"description": "A key defining all the connection settings between the agent and your messaging system.\n\nSee: https://www.krakend.io/docs/async/",
								"type": "object",
								"properties": {
									"backoff_strategy": {
										"title": "Backoff strategy",
										"description": "When the connection to your event source gets interrupted for whatever reason, KrakenD keeps trying to reconnect until it succeeds or until it reaches the `max_retries`. The backoff strategy defines the delay in seconds in between consecutive failed retries.\n\nSee: https://www.krakend.io/docs/async/",
										"default": "fallback",
										"enum": [
											"linear",
											"linear-jitter",
											"exponential",
											"exponential-jitter",
											"fallback"
										]
									},
									"health_interval": {
										"title": "Health interval",
										"description": "The time between pings checking that the agent is connected to the queue and alive. Regardless of the health interval, if an agent fails, KrakenD will restart it again immediately as defined by `max_retries`and `backoff_strategy`.\n\nSee: https://www.krakend.io/docs/async/",
										"default": "1s",
										"$ref": "#/definitions/https%3A~1~1www.krakend.io~1schema~1v2.7~1timeunits.json/$defs/timeunit"
									},
									"max_retries": {
										"title": "Max retries",
										"description": "The maximum number of times you will allow KrakenD to retry reconnecting to a broken messaging system. Use 0 for unlimited retries.\n\nSee: https://www.krakend.io/docs/async/",
										"default": 0,
										"type": "integer"
									}
								},
								"patternProperties": {
									"^[@$_#]": {}
								},
								"additionalProperties": false
							},
							"consumer": {
								"title": "Consumer",
								"description": "Defines all the settings for each agent consuming messages.\n\nSee: https://www.krakend.io/docs/async/",
								"required": [
									"topic"
								],
								"properties": {
									"max_rate": {
										"title": "Max Rate",
										"description": "The maximum number of messages you allow each worker to consume per second. Use any of `0` or `-1` for unlimited speed.\n\nSee: https://www.krakend.io/docs/async/",
										"default": 0,
										"type": "number"
									},
									"timeout": {
										"title": "Timeout",
										"description": "The maximum time the agent will wait to process an event sent to the backend. If the backend fails to process it, the message is reinserted for later consumption. Defaults to the timeout in the root level, or to `2s` if no value is declared.\n\nSee: https://www.krakend.io/docs/async/",
										"$ref": "#/definitions/https%3A~1~1www.krakend.io~1schema~1v2.7~1timeunits.json/$defs/timeunit"
									},
									"topic": {
										"title": "Topic",
										"description": "The topic name you want to consume. The syntax depends on the driver. Examples for AMQP: `*`, `mytopic`, `lazy.#`, `*`, `foo.*`.\n\nSee: https://www.krakend.io/docs/async/",
										"type": "string"
									},
									"workers": {
										"title": "Workers",
										"description": "The number of workers (consuming processes) you want to start simultaneously for this agent.\n\nSee: https://www.krakend.io/docs/async/",
										"default": 1,
										"type": "integer"
									}
								}
							},
							"encoding": {
								"title": "Backend Encoding",
								"description": "Informs KrakenD how to parse the responses of your services.\n\nSee: https://www.krakend.io/docs/backends/supported-encodings/",
								"default": "json",
								"enum": [
									"json",
									"safejson",
									"xml",
									"rss",
									"string",
									"no-op"
								]
							},
							"extra_config": {
								"description": "Defines the driver that connects to your queue or PubSub system. In addition, you can place other middlewares to modify the request (message) or the response, apply logic or any other endpoint middleware, but adding the driver is mandatory.\n\nSee: https://www.krakend.io/docs/async/",
								"required": [
									"async/amqp"
								],
								"properties": {
									"async/amqp": {
										"title": "Async Agent extra configuration",
										"description": "See the configuration for async/amqp",
										"$ref": "#/definitions/https%3A~1~1www.krakend.io~1schema~1v2.7~1async~1amqp.json"
									}
								}
							},
							"name": {
								"title": "Name",
								"description": "A unique name for this agent. KrakenD shows it in the health endpoint and logs and metrics. KrakenD does not check collision names, so make sure each agent has a different name.\n\nSee: https://www.krakend.io/docs/async/",
								"type": "string"
							}
						},
						"patternProperties": {
							"^[@$_#]": {}
						},
						"additionalProperties": false
					},
					"patternProperties": {
						"^[@$_#]": {}
					},
					"additionalProperties": false
				},
				"https://www.krakend.io/schema/v2.7/auth/api-keys.json": {
					"$schema": "http://json-schema.org/draft-07/schema#",
					"title": "API-key Authentication",
					"description": "Enterprise only. Enables a Role-Based Access Control (RBAC) mechanism by reading the `Authorization` header of incoming requests.\n\nSee: https://www.krakend.io/docs/enterprise/authentication/api-keys/",
					"type": "object",
					"required": [
						"keys"
					],
					"properties": {
						"hash": {
							"title": "Hash",
							"description": "The hashing function used to store the value of the key. When you use `plain` the API key is written as it will passed by the user. The rest of the hashes require you to save the API key after applying the desired function.\n\nSee: https://www.krakend.io/docs/enterprise/authentication/api-keys/",
							"default": "plain",
							"enum": [
								"plain",
								"fnv128",
								"sha256",
								"sha1"
							]
						},
						"identifier": {
							"title": "Identifier",
							"description": "The header name or the query string name that contains the API key. Defaults to `key` when using the `query_string` strategy and to `Authorization` when using the `header` strategy. The identifier set here is used across all endpoints with API key authentication enabled, but they can override this entry individually.\n\nSee: https://www.krakend.io/docs/enterprise/authentication/api-keys/",
							"examples": [
								"Authorization",
								"X-Key"
							],
							"default": "Authorization",
							"type": "string"
						},
						"keys": {
							"title": "API Keys",
							"description": "A list of objects defining each API Key.\n\nSee: https://www.krakend.io/docs/enterprise/authentication/api-keys/",
							"type": "array",
							"items": {
								"type": "object",
								"properties": {
									"key": {
										"title": "API Key",
										"description": "The secret key used by the client to access the resources. Don't have a key? Execute in a terminal `uuidgen` to generate a random one.\n\nSee: https://www.krakend.io/docs/enterprise/authentication/api-keys/",
										"type": "string"
									},
									"roles": {
										"title": "Roles",
										"description": "All the roles this user has. See roles as all the identifying labels that belong to this client.\n\nSee: https://www.krakend.io/docs/enterprise/authentication/api-keys/",
										"type": "array",
										"items": {
											"type": "string"
										}
									}
								},
								"patternProperties": {
									"^[@$_#]": {}
								},
								"additionalProperties": false
							}
						},
						"propagate_role": {
							"title": "Propagate role as header",
							"description": "The name of a header that will propagate to the backend containing the matching role. The backend receives no header when the string is empty, or the attribute is not declared. Otherwise, the backend receives the declared header name containing **the first matching role of the user**. The header value will be  `ANY` when the endpoint does not require roles. For instance, if an API key has roles `[A, B]`, and the endpoint demands roles `[B, C]`, the backend will receive a header with the value `B`.\n\nSee: https://www.krakend.io/docs/enterprise/authentication/api-keys/",
							"examples": [
								"X-Krakend-Role"
							],
							"default": "",
							"type": "string"
						},
						"salt": {
							"title": "Salt",
							"description": "A salt string for the desired hashing function. When provided, the API key is concatenated after the salt string and both hashed together.\n\nSee: https://www.krakend.io/docs/enterprise/authentication/api-keys/",
							"examples": [
								"mySalt"
							],
							"default": "",
							"type": "string"
						},
						"strategy": {
							"title": "Strategy",
							"description": "Specifies where to expect the user API key, whether inside a header or as part of the query string. The strategy set here is used across all endpoints with API key authentication enabled, but they can override this entry individually.\n\nSee: https://www.krakend.io/docs/enterprise/authentication/api-keys/",
							"default": "header",
							"enum": [
								"header",
								"query_string"
							]
						}
					},
					"patternProperties": {
						"^[@$_#]": {}
					},
					"additionalProperties": false
				},
				"https://www.krakend.io/schema/v2.7/auth/basic.json": {
					"$schema": "http://json-schema.org/draft-07/schema#",
					"description": "Enterprise only. The Basic Authentication component protects the access to selected endpoints using basic username and password credentials.\n\nSee: https://www.krakend.io/docs/enterprise/authentication/basic-authentication/",
					"type": "object",
					"properties": {
						"htpasswd_path": {
							"title": "Path to htpasswd file",
							"description": "Absolute Path to the `htpasswd` filename (recommended) or relative `./` to the workdir (less secure).\n\nSee: https://www.krakend.io/docs/enterprise/authentication/basic-authentication/",
							"examples": [
								"/path/to/.htpasswd"
							],
							"type": "string"
						},
						"users": {
							"title": "Additional users",
							"description": "**Additional** users to the `htpasswd` file can be declared directly inside the configuration. The content of both places will be merged (and this list will overwrite users already defined in the htpasswd file). The key of each entry is the username, and the value the bcrypt.\n\nSee: https://www.krakend.io/docs/enterprise/authentication/basic-authentication/",
							"examples": [
								{
									"admin": "$2y$05$HpdPmv2Z3h3skMCVaf/CEep/UUBuhZ...",
									"user2": "$2y$05$HpdPmv2Z3h3skMCVaf/CEep/UUBuhZ..."
								}
							],
							"type": "object",
							"patternProperties": {
								"(.*)": {
									"type": "string",
									"additionalProperties": false
								}
							}
						}
					},
					"patternProperties": {
						"^[@$_#]": {}
					},
					"additionalProperties": false
				},
				"https://www.krakend.io/schema/v2.7/auth/client-credentials.json": {
					"$schema": "http://json-schema.org/draft-07/schema#",
					"title": "OAuth2 client-credentials",
					"description": "2-legged OAuth2 flow: Request to your authorization server an access token to reach protected resources.\n\nSee: https://www.krakend.io/docs/authorization/client-credentials/",
					"type": "object",
					"required": [
						"client_id",
						"client_secret",
						"token_url"
					],
					"properties": {
						"client_id": {
							"title": "Client ID",
							"description": "The Client ID provided to the Auth server\n\nSee: https://www.krakend.io/docs/authorization/client-credentials/",
							"type": "string"
						},
						"client_secret": {
							"title": "Client secret",
							"description": "The secret string provided to the Auth server.\n\nSee: https://www.krakend.io/docs/authorization/client-credentials/",
							"examples": [
								"mys3cr3t"
							],
							"type": "string"
						},
						"endpoint_params": {
							"title": "Endpoint parameters",
							"description": "Any additional parameters you want to include **in the payload** when requesting the token. For instance, adding the `audience` request parameter may denote the target API for which the token should be issued.\n\nSee: https://www.krakend.io/docs/authorization/client-credentials/",
							"examples": [
								{
									"audience": [
										"YOUR-AUDIENCE"
									]
								}
							],
							"type": "object"
						},
						"scopes": {
							"title": "Scopes",
							"description": "A comma-separated list of scopes needed, e.g.: `scopeA,scopeB`\n\nSee: https://www.krakend.io/docs/authorization/client-credentials/",
							"examples": [
								"scopeA,scopeB"
							],
							"type": "string"
						},
						"token_url": {
							"title": "Token URL",
							"description": "The endpoint URL where the negotiation of the token happens\n\nSee: https://www.krakend.io/docs/authorization/client-credentials/",
							"examples": [
								"https://your.custom.identity.service.tld/token_endpoint"
							],
							"type": "string"
						}
					},
					"patternProperties": {
						"^[@$_#]": {}
					},
					"additionalProperties": false
				},
				"https://www.krakend.io/schema/v2.7/auth/gcp.json": {
					"$schema": "http://json-schema.org/draft-07/schema#",
					"title": "GCP Authentication",
					"description": "Enterprise only. Enables GCP authentication between KrakenD and Google Cloud service account.\n\nSee: https://www.krakend.io/docs/enterprise/authentication/gcp/",
					"type": "object",
					"anyOf": [
						{
							"required": [
								"audience"
							]
						},
						{
							"required": [
								"audience",
								"credentials_file"
							]
						},
						{
							"required": [
								"audience",
								"credentials_json"
							]
						}
					],
					"properties": {
						"audience": {
							"title": "Audience",
							"description": "The audience in GCP looks like an URL, and contains the destination service you will ask a token for. Most of the times this URL will match exactly with the `host` entry.\n\nSee: https://www.krakend.io/docs/enterprise/authentication/gcp/",
							"examples": [
								"https://gcptest-76fewi6rca-uc.a.run.app"
							],
							"type": "string"
						},
						"credentials_file": {
							"title": "Path to credentials file",
							"description": "The relative or absolute path to a credentials file in JSON format that contains all the credentials to authenticate API calls to the given service account.\n\nSee: https://www.krakend.io/docs/enterprise/authentication/gcp/",
							"examples": [
								"/etc/krakend/gcp.json"
							],
							"type": "string"
						},
						"credentials_json": {
							"title": "JSON credentials file",
							"description": "An inline JSON object containing all the credentials fields to authenticate to GCP.\n\nSee: https://www.krakend.io/docs/enterprise/authentication/gcp/",
							"examples": [
								{
									"type": "service_account",
									"auth_provider_x509_cert_url": "https://www.googleapis.com/oauth2/v1/certs",
									"auth_uri": "https://accounts.google.com/o/oauth2/auth",
									"client_email": "xyz@developer.gserviceaccount.com",
									"client_id": "123",
									"client_x509_cert_url": "https://www.googleapis.com/robot/v1/metadata/x509/xyz%40developer.gserviceaccount.com",
									"private_key": "-----BEGIN PRIVATE KEY-----\nMIIEvQIBADANBgkqhkiG9w0BAQEFAASCBKcwggSjAgEAAoIBAQCzd9ZdbPLAR4/g\nj+Rodu15kEasMpxf/Mz+gKRb2fmgR2Y18Y/iRBYZ4SkmF2pBSfzvwE/aTCzSPBGl\njHhPzohXnSN029eWoItmxVONlqCbR29pD07aLzv08LGeIGdHIEdhVjhvRwTkYZIF\ndXmlHNDRUU/EbJN9D+3ahw22BNnC4PaDgfIWTs3xIlTCSf2rL39I4DSNLTS/LzxK\n/XrQfBMtfwMWwyQaemXbc7gRgzOy8L56wa1W1zyXx99th97j1bLnoAXBGplhB4Co\n25ohyDAuhxRm+XGMEaO0Mzo7u97kvhj48a569RH1QRhOf7EBf60jO4h5eOmfi5P5\nPV3l7041AgMBAAECggEAEZ0RTNoEeRqM5F067YW+iM/AH+ZXspP9Cn1VpC4gcbqQ\nLXsnw+0qvh97CmIB66Z3TJBzRdl0DK4YjUbcB/kdKHwjnrR01DOtesijCqJd4N+B\n762w73jzSXbV9872U+S3HLZ5k3JE6KUqz55X8fyCAgkY6w4862lEzs2yasrPFHEV\nRoQp3PM0Miif8R3hGDhOWcHxcobullthG6JHAQFfc1ctwEjZI4TK0iWqlzfWGyKN\nT9UgvjUDud5cGvS9el0AiLN6keAf77tcPn1zetUVhxN1KN4bVAm1Q+6O8esl63Rj\n7JXpHzxaRnit9S6/aH/twHsGGtLg5Puw6jey6xs4AQKBgQD2JNy1wzewCRkD+jug\n8CHbJ+LIJVRNIaWa/RK1QD8/UjmFPkIzRQSF3AKC5mRAWSa2FL3yVK3N/DD7hazW\n85XSBB7IDcnoJnA9SkUeWwqQGkDx3EntlU3gX8Kn/+ofF8O9jLXxAa901MAVXVuf\n5YDzrl4PNE3bFnPCdiNmSdRfhQKBgQC6p4DsCpwqbeTu9f5ak9VW/fQP47Fgt+Mf\nwGjBnKP5PbbNJpHCfamF7jqSRH83Xy0KNssH7jD/NZ2oT594sMmiQPUC5ni9VYY6\nsuYB0JbD5Mq+EjKIVhYtxaQJ76LzHreEI+G4z6k3H7/hRpr3/C48n9G/uVkT9DbJ\noplxxEx68QKBgQCdJ23vcwO0Firtmi/GEmtbVHz70rGfSXNFoHz4UlvPXv0wsE5u\nE4vOt2i3EMhDOWh46odYGG6bzH+tp2xyFTW70Dui+QLHgPs6dpfoyLHWzZxXj5F3\n6lK9hgZvYvqk/XRRKmzjwnK2wjsdqOyeC1covlR5mqh20D/6kZkKbur0TQKBgAwy\nCZBimRWEnKKoW/gbFKNccGfhXqONID/g2Hdd/rC4QYth68AjacIgcJ9B7nX1uAGk\n1tsryvPB0w0+NpMyKdp6GAgaeuUUA3MuYSzZLiCagEyu77JMvaI7+Z3UlHcCGMd/\neK4Uk1/QqT7U2Cc/yN2ZK6E1QQa2vCWshA4U31JhAoGAbtbSSSsul1c+PsJ13Cfk\n6qVnqYzPqt23QTyOZmGAvUHH/M4xRiQpOE0cDF4t/r5PwenAQPQzTvMmWRzj6uAY\n3eaU0eAK7ZfoweCoOIAPnpFbbRLrXfoY46H7MYh7euWGXOKEpxz5yzuEkd9ByNUE\n86vSEidqbMIiXVgEgnu/k08=\n-----END PRIVATE KEY-----\n",
									"private_key_id": "private_key_id",
									"project_id": "project_id",
									"token_uri": "https://accounts.google.com/o/oauth2/token"
								}
							],
							"type": "object"
						},
						"custom_claims": {
							"title": "Custom claims",
							"description": "Custom private claims that you can optionally add to an ID token.\n\nSee: https://www.krakend.io/docs/enterprise/authentication/gcp/",
							"type": "object"
						}
					},
					"patternProperties": {
						"^[@$_#]": {}
					},
					"additionalProperties": false
				},
				"https://www.krakend.io/schema/v2.7/auth/jose.json": {
					"$schema": "http://json-schema.org/draft-07/schema#",
					"title": "Shared JWK cache",
					"description": "Enables global configurations for the HTTP client responsible of downloading and caching the JWK URLs for token validation and signing.",
					"type": "object",
					"required": [
						"shared_cache_duration"
					],
					"properties": {
						"shared_cache_duration": {
							"title": "Shared cache duration",
							"description": "The cache duration in seconds for the JWK client retrieving the `jwk_url`. The endpoint must enable the `cache` option in order to use this second level cache.\n\nSee: https://www.krakend.io/docs/authorization/jwk-caching/",
							"type": "integer"
						}
					},
					"patternProperties": {
						"^[@$_#]": {}
					},
					"additionalProperties": false
				},
				"https://www.krakend.io/schema/v2.7/auth/ntlm.json": {
					"$schema": "http://json-schema.org/draft-07/schema#",
					"title": "NTLM Authentication",
					"description": "Enterprise only. Enables NTLM authentication between KrakenD and a Microsoft server such as Dynamics.\n\nSee: https://www.krakend.io/docs/enterprise/authentication/ntlm/",
					"type": "object",
					"required": [
						"user",
						"password"
					],
					"properties": {
						"password": {
							"title": "Password",
							"description": "The password you will use, in clear text.\n\nSee: https://www.krakend.io/docs/enterprise/authentication/ntlm/",
							"examples": [
								"myp4ssw0rd"
							],
							"type": "string"
						},
						"user": {
							"title": "User",
							"description": "The username you will send as NTLM authentication user.\n\nSee: https://www.krakend.io/docs/enterprise/authentication/ntlm/",
							"examples": [
								"krakendclient"
							],
							"type": "string"
						}
					},
					"patternProperties": {
						"^[@$_#]": {}
					},
					"additionalProperties": false
				},
				"https://www.krakend.io/schema/v2.7/auth/revoker.json": {
					"$schema": "http://json-schema.org/draft-07/schema#",
					"title": "Revoke Server",
					"description": "The API Gateway authorizes users that provide valid tokens according to your criteria, but at some point, you might want to change your mind and decide to revoke JWT tokens that are still valid.",
					"type": "object",
					"required": [
						"N",
						"P",
						"hash_name",
						"TTL",
						"port",
						"token_keys"
					],
					"properties": {
						"N": {
							"title": "Bot detector",
							"description": "The maximum `N`umber of elements you want to keep in the bloom filter. Tens of millions work fine on machines with low resources.\n\nSee: https://www.krakend.io/docs/authorization/revoking-tokens/",
							"examples": [
								10000000
							],
							"type": "integer"
						},
						"P": {
							"title": "Probability",
							"description": "The `P`robability of returning a false positive. E.g.,`1e-7` for one false positive every 10 million different tokens. The values `N` and `P` determine the size of the resulting bloom filter to fulfill your expectations. E.g: 0.0000001\n\nSee: https://www.krakend.io/docs/authorization/revoking-tokens/",
							"examples": [
								1e-07,
								1e-07
							],
							"type": "number"
						},
						"TTL": {
							"title": "Time To Live",
							"description": "The lifespan of the JWT you are generating in seconds. The value must match the expiration you are setting in the identity provider when creating the tokens.\n\nSee: https://www.krakend.io/docs/authorization/revoking-tokens/",
							"type": "integer"
						},
						"hash_name": {
							"title": "Hash function name",
							"description": "Either `optimal` (recommended) or `default`. The `optimal` consumes less CPU but has less entropy when generating the hash, although the loss is negligible.\n\nSee: https://www.krakend.io/docs/authorization/revoking-tokens/",
							"enum": [
								"optimal",
								"default"
							]
						},
						"port": {
							"title": "Port",
							"description": "The port number exposed on each KrakenD instance for the RPC service to interact with the bloomfilter. This port is allocated only to the clients (running KrakenDs).\n\nSee: https://www.krakend.io/docs/authorization/revoking-tokens/",
							"type": "integer"
						},
						"revoke_server_api_key": {
							"title": "Revoke Server Ping URL",
							"description": "A string used as an exchange API key to secure the communication between the Revoke Server and the KrakenD instances and to consume the REST API of the Revoker Server as well. E.g., a string generated with `uuidgen`.\n\nSee: https://www.krakend.io/docs/enterprise/authentication/revoke-server/",
							"examples": [
								"639ee23f-f4c5-40c4-855c-912bf01fae87"
							],
							"type": "string"
						},
						"revoke_server_max_retries": {
							"title": "Revoke Server Max Retries",
							"description": "Maximum number of retries after a connection fails. When the value is less than zero it is changed automatically to zero.\n\nSee: https://www.krakend.io/docs/enterprise/authentication/revoke-server/",
							"default": 0,
							"type": "integer"
						},
						"revoke_server_max_workers": {
							"title": "Max workers",
							"description": "How many workers are used concurrently to execute an action (e.g., push a token) to all registered instances, allowing you to limit the amount of memory consumed by the server. For example, if you have 100 KrakenD servers and need to push 5MB of data each, you need to send 500MB in total. A max_workers=5 will consume a maximum of `5MB x 5 workers = 25MB` of memory in a given instant. Defaults to the same number of CPUs available.\n\nSee: https://www.krakend.io/docs/enterprise/authentication/revoke-server/",
							"default": 5,
							"type": "integer"
						},
						"revoke_server_ping_interval": {
							"title": "Revoke Server ping interval",
							"description": "Time the server and the client wait to verify they are alive with each other (health check). Defaults to `30s`. Do not lower this value a lot; otherwise, you will have a lot of internal traffic.\n\nSee: https://www.krakend.io/docs/enterprise/authentication/revoke-server/",
							"examples": [
								"30s"
							],
							"$ref": "#/definitions/https%3A~1~1www.krakend.io~1schema~1v2.7~1timeunits.json/$defs/timeunit",
							"type": "string"
						},
						"revoke_server_ping_url": {
							"title": "Revoke Server Ping URL",
							"description": "The address to the `/instances` endpoint in the Revoke Server.\n\nSee: https://www.krakend.io/docs/enterprise/authentication/revoke-server/",
							"examples": [
								"http://revoke-server:8081/instances"
							],
							"type": "string"
						},
						"token_keys": {
							"title": "Token keys",
							"description": "The list with all the claims in your JWT payload that need watching. These fields establish the criteria to revoke accesses in the future. The Revoker does not use this value, only the clients.\n\nSee: https://www.krakend.io/docs/authorization/revoking-tokens/",
							"examples": [
								"jti"
							],
							"type": "array"
						}
					},
					"patternProperties": {
						"^[@$_#]": {}
					},
					"additionalProperties": false
				},
				"https://www.krakend.io/schema/v2.7/auth/signer.json": {
					"$schema": "http://json-schema.org/draft-07/schema#",
					"title": "JWT signer",
					"description": "creates a wrapper for your login endpoint that signs with your secret key the selected fields of the backend payload right before returning the content to the end-user.\n\nSee: https://www.krakend.io/docs/authorization/jwt-signing/",
					"type": "object",
					"oneOf": [
						{
							"required": [
								"alg",
								"jwk_local_path",
								"disable_jwk_security"
							]
						},
						{
							"required": [
								"alg",
								"jwk_url"
							]
						}
					],
					"required": [
						"alg",
						"kid",
						"keys_to_sign"
					],
					"properties": {
						"alg": {
							"title": "Algorithm",
							"description": "The hashing algorithm used by the issuer. Usually `RS256`. The algorithm you choose directly affects the CPU consumption.\n\nSee: https://www.krakend.io/docs/authorization/jwt-signing/",
							"enum": [
								"EdDSA",
								"HS256",
								"HS384",
								"HS512",
								"RS256",
								"RS384",
								"RS512",
								"ES256",
								"ES384",
								"ES512",
								"PS256",
								"PS384",
								"PS512"
							]
						},
						"cipher_suites": {
							"title": "Cipher suites",
							"description": "Override the default cipher suites (see [JWT validation](https://www.krakend.io/docs/authorization/jwt-signing/)). Unless you have a legacy JWK, **you don't need to set this value**.",
							"default": [
								49199,
								49195,
								49200,
								49196,
								52392,
								52393
							],
							"type": "array",
							"items": {
								"title": "Object in array",
								"description": "\n\nSee: https://www.krakend.io/docs/authorization/jwt-signing/",
								"enum": [
									5,
									10,
									47,
									53,
									60,
									156,
									157,
									49159,
									49161,
									49162,
									49169,
									49170,
									49171,
									49172,
									49187,
									49191,
									49199,
									49195,
									49200,
									49196,
									52392,
									52393
								]
							}
						},
						"cypher_key": {
							"title": "Cypher key",
							"description": "The cyphering key.\n\nSee: https://www.krakend.io/docs/authorization/jwt-validation/",
							"type": "string"
						},
						"disable_jwk_security": {
							"title": "Disable_jwk_security",
							"description": "Disables HTTP security of the JWK client and allows insecure connections (plain HTTP) to download the keys. The flag should be `false` when you use HTTPS, and `true` when using plain HTTP or loading the key from a local file.\n\nSee: https://www.krakend.io/docs/enterprise/authorization/jwt-validation/",
							"default": false,
							"type": "boolean"
						},
						"full": {
							"title": "Full format",
							"description": "Use JSON format instead of the compact form JWT provides.\n\nSee: https://www.krakend.io/docs/enterprise/authorization/jwt-validation/",
							"default": false,
							"type": "boolean"
						},
						"jwk_fingerprints": {
							"title": "JWK Fingerprints",
							"description": "A list of fingerprints (the unique identifier of the certificate) for certificate pinning and avoid man in the middle attacks. Add fingerprints in base64 format.\n\nSee: https://www.krakend.io/docs/authorization/jwt-signing/",
							"type": "array"
						},
						"jwk_local_ca": {
							"title": "Local CA",
							"description": "Path to the CA’s certificate verifying a secure connection when downloading the JWK. Use when not recognized by the system (e.g., self-signed certificates).\n\nSee: https://www.krakend.io/docs/authorization/jwt-validation/",
							"type": "string"
						},
						"jwk_local_path": {
							"title": "JWK local path",
							"description": "Local path to the JWK public keys, has preference over `jwk_url`. Instead of pointing to an external URL (with `jwk_url`), public keys are kept locally, in a plain JWK file (security alert!), or encrypted. When encrypted, also add `secret_url` and `cypher_key`.\n\nSee: https://www.krakend.io/docs/authorization/jwt-validation/",
							"examples": [
								"./jwk.txt"
							],
							"type": "string"
						},
						"jwk_url": {
							"title": "JWK URL",
							"description": " The URL to the JWK endpoint with the private keys used to sign the token.\n\nSee: https://www.krakend.io/docs/authorization/jwt-signing/",
							"examples": [
								"http://your-backend/jwk/symmetric.json"
							],
							"type": "string"
						},
						"keys_to_sign": {
							"title": "Keys to sign",
							"description": "List of all the specific keys that need signing (e.g., `refresh_token` and `access_token`).\n\nSee: https://www.krakend.io/docs/authorization/jwt-signing/",
							"examples": [
								"access_token",
								"refresh_token"
							],
							"type": "array"
						},
						"kid": {
							"title": "Kid",
							"description": "The key ID purpose is to match a specific key, as the jwk_url might contain several keys.\n\nSee: https://www.krakend.io/docs/enterprise/authorization/jwt-validation/",
							"examples": [
								"sim2"
							],
							"type": "string"
						},
						"leeway": {
							"title": "Leeway",
							"description": "A margin of extra time where you will still accept the token after its expiration date. You should not accept expired tokens other than enabling two environments that are not perfectly synchronized and have minor clock drifts to accept each other differences. Any value specified here will be rounded to seconds, with a minimum of one second.\n\nSee: https://www.krakend.io/docs/authorization/jwt-signing/",
							"examples": [
								"1m",
								"1s"
							],
							"default": "1s",
							"$ref": "#/definitions/https%3A~1~1www.krakend.io~1schema~1v2.7~1timeunits.json/$defs/timeunit",
							"type": "string"
						},
						"secret_url": {
							"title": "Secret's URL",
							"description": "An URL with a custom scheme using one of the supported providers (e.g.: `awskms://keyID`) ([see providers](https://www.krakend.io/docs/authorization/jwt-validation/#accepted-providers-for-encrypting-payloads)).\n\nSee: https://www.krakend.io/docs/authorization/jwt-validation/",
							"examples": [
								"base64key://smGbjm71Nxd1Ig5FS0wj9SlbzAIrnolCz9bQQ6uAhl4=",
								"awskms://keyID",
								"azurekeyvault://keyID",
								"gcpkms://projects/[PROJECT_ID]/locations/[LOCATION]/keyRings/[KEY_RING]/cryptoKeys/[KEY]",
								"hashivault://keyID"
							],
							"type": "string",
							"pattern": "(base64key|awskms|azurekeyvault|gcpkms|hashivault)://(.*)"
						}
					},
					"patternProperties": {
						"^[@$_#]": {}
					},
					"additionalProperties": false
				},
				"https://www.krakend.io/schema/v2.7/auth/validator.json": {
					"$schema": "http://json-schema.org/draft-07/schema#",
					"title": "JWT validator",
					"description": "Protect endpoints from public usage by validating JWT tokens generated by any industry-standard OpenID Connect (OIDC) integration.\n\nSee: https://www.krakend.io/docs/authorization/jwt-validation/",
					"type": "object",
					"oneOf": [
						{
							"required": [
								"alg",
								"jwk_local_path"
							]
						},
						{
							"required": [
								"alg",
								"jwk_url"
							]
						}
					],
					"properties": {
						"alg": {
							"title": "Algorithm",
							"description": "The hashing algorithm used by the token issuer.\n\nSee: https://www.krakend.io/docs/authorization/jwt-validation/",
							"default": "RS256",
							"enum": [
								"EdDSA",
								"HS256",
								"HS384",
								"HS512",
								"RS256",
								"RS384",
								"RS512",
								"ES256",
								"ES384",
								"ES512",
								"PS256",
								"PS384",
								"PS512"
							]
						},
						"audience": {
							"title": "Audience",
							"description": "Reject tokens that do not contain ALL audiences declared in the list.\n\nSee: https://www.krakend.io/docs/authorization/jwt-validation/",
							"examples": [
								[
									"audience1"
								]
							],
							"type": "array",
							"items": {
								"type": "string"
							}
						},
						"auth_header_name": {
							"title": "Authorization header",
							"description": "Allows to parse the token from a custom header.\n\nSee: https://www.krakend.io/docs/authorization/jwt-validation/",
							"examples": [
								"X-Custom-Auth"
							],
							"default": "Authorization",
							"type": "string"
						},
						"cache": {
							"title": "Cache",
							"description": "Set this value to `true` (recommended) to stop downloading keys on every request and store them in memory for the next `cache_duration` period and avoid hammering the key server, as recommended for performance. Do not use this flag when using `jwk_local_ca`.\n\nSee: https://www.krakend.io/docs/authorization/jwt-validation/",
							"default": false,
							"type": "boolean"
						},
						"cache_duration": {
							"title": "Cache duration",
							"description": "The cache duration in seconds when the `cache` is enabled. 15 minutes when unset.\n\nSee: https://www.krakend.io/docs/authorization/jwt-validation/",
							"default": 900,
							"type": "integer"
						},
						"cipher_suites": {
							"title": "Cipher suites",
							"description": "Override the default cipher suites. Use it if you want to enforce an even higher security standard.\n\nSee: https://www.krakend.io/docs/authorization/jwt-validation/",
							"default": [
								49199,
								49195,
								49200,
								49196,
								52392,
								52393
							],
							"type": "array",
							"items": {
								"title": "Object in array",
								"description": "\n\nSee: https://www.krakend.io/docs/authorization/jwt-validation/",
								"enum": [
									5,
									10,
									47,
									53,
									60,
									156,
									157,
									49159,
									49161,
									49162,
									49169,
									49170,
									49171,
									49172,
									49187,
									49191,
									49199,
									49195,
									49200,
									49196,
									52392,
									52393
								]
							}
						},
						"cookie_key": {
							"title": "Cookie key",
							"description": "Add the key name of the cookie containing the token when it is not passed in the headers\n\nSee: https://www.krakend.io/docs/authorization/jwt-validation/",
							"examples": [
								"cookie_jwt"
							],
							"type": "string"
						},
						"cypher_key": {
							"title": "Cypher key",
							"description": "The cyphering key.\n\nSee: https://www.krakend.io/docs/authorization/jwt-validation/",
							"type": "string"
						},
						"disable_jwk_security": {
							"title": "Disable_jwk_security",
							"description": "When true, disables security of the JWK client and allows insecure connections (plain HTTP) to download the keys. Useful for development environments.\n\nSee: https://www.krakend.io/docs/authorization/jwt-validation/",
							"default": false,
							"type": "boolean"
						},
						"failed_jwk_key_cooldown": {
							"title": "Failed JWK Key cooldown",
							"description": "When a request comes with a token declaring an unknown `kid` (or the key strategy you choose), and the JWK is in a remote destination, KrakenD downloads the JWK from the Identity Provider for its recognition. Suppose there is a network failure, or the key is not in the list (e.g., you rotated the keys without anticipation). In that case, you can tell the gateway not to contact the Identity Provider again during the time specified here. We recommend setting this value, even with a low time (e.g., `10s`), to prevent misconfigurations from hammering your Identity Providers. Any values under one second are ignored.\n\nSee: https://www.krakend.io/docs/authorization/jwt-validation/",
							"examples": [
								"1m",
								"10s",
								"1h"
							],
							"$ref": "#/definitions/https%3A~1~1www.krakend.io~1schema~1v2.7~1timeunits.json/$defs/timeunit",
							"type": "string"
						},
						"issuer": {
							"title": "Issuer",
							"description": "When set, tokens not matching the issuer are rejected.\n\nSee: https://www.krakend.io/docs/authorization/jwt-validation/",
							"examples": [
								"issuer"
							],
							"type": "string"
						},
						"jwk_fingerprints": {
							"title": "Roles",
							"description": "A list of fingerprints (the certificate's unique identifier) for certificate pinning and avoid man-in-the-middle attacks. Add fingerprints in base64 format.\n\nSee: https://www.krakend.io/docs/authorization/jwt-validation/",
							"type": "array",
							"items": {
								"type": "string"
							}
						},
						"jwk_local_ca": {
							"title": "Local CA",
							"description": "Path to the CA's certificate verifying a secure connection when downloading the JWK. Use when not recognized by the system (e.g., self-signed certificates).\n\nSee: https://www.krakend.io/docs/authorization/jwt-validation/",
							"type": "string"
						},
						"jwk_local_path": {
							"title": "JWK local path",
							"description": "Local path to the JWK public keys, has preference over `jwk_url`. Instead of pointing to an external URL (with `jwk_url`), public keys are kept locally, in a plain JWK file (security alert!), or encrypted. When encrypted, also add `secret_url` and `cypher_key`.\n\nSee: https://www.krakend.io/docs/authorization/jwt-validation/",
							"examples": [
								"./jwk.txt"
							],
							"type": "string"
						},
						"jwk_url": {
							"title": "JWK URL",
							"description": "The URL to the JWK endpoint with the public keys used to verify the token's authenticity and integrity. Use with `cache` to avoid re-downloading the key on every request. Consider enabling [shared caching](https://www.krakend.io/docs/authorization/jwk-caching/) too. The identity server will receive an HTTP(s) request from KrakenD with a KrakenD user agent, and the identity server must reply with a JSON object and a content-type `application/jwk-set+json` or `application/json`.\n\nSee: https://www.krakend.io/docs/authorization/jwt-validation/",
							"examples": [
								"https://some-domain.auth0.com/.well-known/jwks.json",
								"http://KEYCLOAK:8080/auth/realms/master/protocol/openid-connect/certs",
								"https://yourOktaBaseUrl/v1/keys"
							],
							"type": "string"
						},
						"key_identify_strategy": {
							"title": "Key identify strategy",
							"description": "Allows strategies other than `kid` to load keys.\n\nSee: https://www.krakend.io/docs/authorization/jwt-validation/",
							"enum": [
								"kid",
								"x5t",
								"kid_x5t"
							]
						},
						"leeway": {
							"title": "Leeway",
							"description": "A margin of time where you will accept an already expired token. You should not accept expired tokens other than enabling two environments that are not perfectly synchronized and have minor clock drifts to accept each other differences. Any value specified here will be rounded to seconds, with a minimum of one second.\n\nSee: https://www.krakend.io/docs/authorization/jwt-validation/",
							"examples": [
								"1m",
								"1s"
							],
							"default": "1s",
							"$ref": "#/definitions/https%3A~1~1www.krakend.io~1schema~1v2.7~1timeunits.json/$defs/timeunit",
							"type": "string"
						},
						"operation_debug": {
							"title": "Debug",
							"description": "When `true`, any JWT **validation operation** gets printed in the log with a level `ERROR`. You will see if a client does not have sufficient roles, the allowed claims, scopes, and other useful information.\n\nSee: https://www.krakend.io/docs/authorization/jwt-validation/",
							"default": false,
							"type": "boolean"
						},
						"propagate_claims": {
							"title": "Claims to propagate",
							"description": "Enables passing claims in the backend's request header. You can pass nested claims using the dot `.` operator. E.g.: `realm_access.roles`.\n\nSee: https://www.krakend.io/docs/authorization/jwt-validation/",
							"type": "array",
							"items": {
								"type": "array",
								"maxItems": 2,
								"minItems": 2,
								"items": {
									"type": "string"
								}
							}
						},
						"roles": {
							"title": "Roles",
							"description": " When set, the JWT token not having at least one of the listed roles is rejected.\n\nSee: https://www.krakend.io/docs/authorization/jwt-validation/",
							"type": "array",
							"items": {
								"type": "string"
							}
						},
						"roles_key": {
							"title": "Roles key",
							"description": "When validating users through roles, provide the key name inside the JWT payload that lists their roles. If this key is nested inside another object, add `roles_key_is_nested` and use the dot notation `.` to traverse each level. E.g.: `resource_access.myclient.roles` represents the payload `{resource_access: { myclient: { roles: [\"myrole\"] } }`. Notice that the roles object you choose is a list, not a map.\n\nSee: https://www.krakend.io/docs/authorization/jwt-validation/",
							"examples": [
								"resource_access.myclient.roles"
							],
							"type": "string"
						},
						"roles_key_is_nested": {
							"title": "Roles key is nested",
							"description": "If the roles key uses a nested object using the `.` dot notation, you must set it to `true` to traverse the object.\n\nSee: https://www.krakend.io/docs/authorization/jwt-validation/",
							"type": "boolean"
						},
						"scopes": {
							"title": "Scopes",
							"description": "A list of scopes to validate. The token, after decoding it, can have the scopes declared as a space-separated list, e.g.: `\"my_scopes\": \"resource1:action1 resource3:action7\"` or inside a list, e.g.: `\"my_scopes\": [\"resource1:action1\",\"resource3:action7\"]`.\n\nSee: https://www.krakend.io/docs/authorization/jwt-validation/",
							"type": "array",
							"items": {
								"type": "string"
							}
						},
						"scopes_key": {
							"title": "Scopes key",
							"description": "The key name where KrakenD can find the scopes. The key can be a nested object using the `.` dot notation, e.g.: `data.access.my_scopes`.\n\nSee: https://www.krakend.io/docs/authorization/jwt-validation/",
							"type": "string"
						},
						"scopes_matcher": {
							"title": "Scopes matcher",
							"description": "Defines if the user needs to have in its token at least one of the listed claims (`any`), or `all` of them.\n\nSee: https://www.krakend.io/docs/authorization/jwt-validation/",
							"default": "any",
							"enum": [
								"any",
								"all"
							]
						},
						"secret_url": {
							"title": "Secret's URL",
							"description": "An URL with a custom scheme using one of the supported providers (e.g.: `awskms://keyID`) (see providers).\n\nSee: https://www.krakend.io/docs/authorization/jwt-validation/",
							"examples": [
								"base64key://smGbjm71Nxd1Ig5FS0wj9SlbzAIrnolCz9bQQ6uAhl4=",
								"awskms://keyID",
								"azurekeyvault://keyID",
								"gcpkms://projects/[PROJECT_ID]/locations/[LOCATION]/keyRings/[KEY_RING]/cryptoKeys/[KEY]",
								"hashivault://keyID"
							],
							"type": "string",
							"pattern": "(base64key|awskms|azurekeyvault|gcpkms|hashivault)://(.*)"
						}
					},
					"patternProperties": {
						"^[@$_#]": {}
					},
					"additionalProperties": false
				},
				"https://www.krakend.io/schema/v2.7/backend.json": {
					"$schema": "http://json-schema.org/draft-07/schema#",
					"title": "Backend Object",
					"description": "A backend object is an array of all the services that an endpoint connects to. It defines the list of hostnames that connects to and the URL to send or receive the data.",
					"type": "object",
					"required": [
						"url_pattern"
					],
					"properties": {
						"allow": {
							"title": "Allow (data manipulation)",
							"description": "**Only return the fields in the list**. Only the matching fields (case-sensitive) are returned in the final response. Use a dot `.` separator to define nested attributes, e.g.: `a.b` removes `{\"a\":{\"b\": true}}` \n\nSee: https://www.krakend.io/docs/backends/data-manipulation/",
							"examples": [
								[
									"user_id",
									"field1.subfield2"
								]
							],
							"type": "array",
							"uniqueItems": true
						},
						"deny": {
							"title": "Deny (data manipulation)",
							"description": "**Don't return the fields in the list**. All matching fields (case-sensitive) defined in the list, are removed from the response. Use a dot `.` separator to definr nested attributes, e.g.: `a.b` removes `{\"a\":{\"b\": true}}`.\n\nSee: https://www.krakend.io/docs/backends/data-manipulation/",
							"examples": [
								[
									"token",
									"CVV",
									"password"
								]
							],
							"type": "array",
							"uniqueItems": true
						},
						"disable_host_sanitize": {
							"title": "Disable host sanitize",
							"description": "Set it to `true` when the host doesn't need to be checked for an HTTP protocol. This is the case of `sd=dns` or when using other protocols like `amqp://`, `nats://`, `kafka://`, etc. When set to true, and the protocol is not HTTP, KrakenD fails with an `invalid host` error.",
							"default": false,
							"type": "boolean"
						},
						"encoding": {
							"title": "Backend Encoding",
							"description": "Defines your [needed encoding](https://www.krakend.io/docs/backends/supported-encodings/) to set how to parse the response. Defaults to the value of its endpoint's `encoding`, or to `json` if not defined anywhere else.\n\nSee: https://www.krakend.io/docs/backends/supported-encodings/",
							"default": "json",
							"enum": [
								"json",
								"safejson",
								"fast-json",
								"xml",
								"rss",
								"string",
								"no-op"
							]
						},
						"extra_config": {
							"title": "Backend Extra configuration",
							"description": "When there is additional configuration related to a specific component or middleware (like a circuit breaker, rate limit, etc.), it is declared under this section.",
							"$ref": "#/definitions/https%3A~1~1www.krakend.io~1schema~1v2.7~1backend_extra_config.json",
							"type": "object"
						},
						"group": {
							"title": "Group (data manipulation)",
							"description": "Instead of placing all the response attributes in the root of the response, create a new key and encapsulate the response inside.\n\nSee: https://www.krakend.io/docs/backends/data-manipulation/",
							"default": "backend1",
							"type": "string"
						},
						"host": {
							"title": "Host array",
							"description": "An array with all the available hosts to [load balance](https://www.krakend.io/docs/throttling/load-balancing/#balancing-egress-traffic-to-upstream) requests, including the schema (when possible) `schema://host:port`. E.g.: ` https://my.users-ms.com`. If you are in a platform where hosts or services are balanced (e.g., a K8S service), write a single entry in the array with the service name/balancer address. Defaults to the `host` declaration at the configuration's root level, and the service fails starting when there is none.",
							"type": "array"
						},
						"input_headers": {
							"title": "Allowed Headers In",
							"description": "A second level of header filtering that defines the list of all headers allowed to reach this backend when different than the endpoint.\nBy default, all headers in the endpoint `input_headers` reach the backend, unless otherwise specified here. An empty list `[]` is considered a zero-value and allows all headers to pass. Use `[\"\"]` to explicitly remove all headers. See [headers forwarding](https://www.krakend.io/docs/endpoints/parameter-forwarding/#headers-forwarding)",
							"default": [],
							"type": "array",
							"uniqueItems": true,
							"items": {
								"examples": [
									[
										"*"
									],
									[
										"User-Agent",
										"Accept"
									]
								],
								"type": "string"
							}
						},
						"input_query_strings": {
							"title": "Allowed Querystrings In",
							"description": "A second level of query string filtering that defines the list of all query strings allowed to reach this backend when different than the endpoint.\nBy default, all query strings in the endpoint `input_query_strings` reach the backend, unless otherwise specified here. An empty list `[]` is considered a zero-value and allows all headers to pass. Use `[\"\"]` to explicitly remove all query strings. See [query strings forwarding](https://www.krakend.io/docs/endpoints/parameter-forwarding/#query-string-forwarding)",
							"default": [],
							"type": "array",
							"uniqueItems": true,
							"items": {
								"examples": [
									[
										"*"
									],
									[
										"page",
										"limit"
									]
								],
								"type": "string"
							}
						},
						"is_collection": {
							"title": "Is a collection/array",
							"description": "Set to true when your API does not return an object {} but a collection []\n\nSee: https://www.krakend.io/docs/backends/data-manipulation/",
							"default": true,
							"type": "boolean"
						},
						"mapping": {
							"title": "Mapping",
							"description": "Mapping, or also known as renaming, let you change the name of the fields of the generated responses, so your composed response would be as close to your use case as possible without changing a line on any backend.\n\nSee: https://www.krakend.io/docs/backends/data-manipulation/",
							"examples": [
								{
									"from": "to"
								}
							],
							"type": "object"
						},
						"method": {
							"title": "Method",
							"description": "The method sent to this backend in **uppercase**. The method does not need to match the endpoint's method. When the value is omitted, it uses the same endpoint's method.\n\nSee: https://www.krakend.io/docs/backends/",
							"default": "GET",
							"enum": [
								"GET",
								"POST",
								"PUT",
								"PATCH",
								"DELETE"
							]
						},
						"sd": {
							"title": "Service Discovery",
							"description": "The [Service Discovery](https://www.krakend.io/docs/backends/service-discovery/) system to resolve your backend services. Defaults to `static` (no external Service Discovery). Use `dns` to use DNS SRV records.\n\nSee: https://www.krakend.io/docs/backends/",
							"default": "static",
							"enum": [
								"static",
								"dns"
							]
						},
						"sd_scheme": {
							"title": "Service Discovery Scheme",
							"description": "The [Service Discovery](https://www.krakend.io/docs/backends/service-discovery/) scheme to connect to your backend services.\n\nSee: https://www.krakend.io/docs/backends/",
							"examples": [
								"http",
								"https"
							],
							"default": "http",
							"type": "string"
						},
						"target": {
							"title": "Target (data manipulation)",
							"description": "Removes the matching object from the reponse and returns only its contents.\n\nSee: https://www.krakend.io/docs/backends/data-manipulation/",
							"examples": [
								"data",
								"content",
								"response"
							],
							"type": "string"
						},
						"url_pattern": {
							"title": "URL Pattern",
							"description": "The path inside the service (no protocol, no host, no method). E.g: `/users`. Some functionalities under `extra_config` might drop the requirement of declaring a valid `url_pattern`, but they are exceptions. The URL must be RESTful, if it is not (e.g.: `/url.{some_variable}.json`), then see how to [disable RESTful checking](#disable-restful-checking).\n\nSee: https://www.krakend.io/docs/backends/",
							"examples": [
								"/users",
								"/user/{id_user}"
							],
							"type": "string"
						}
					},
					"patternProperties": {
						"^[@$_#]": {}
					},
					"additionalProperties": false
				},
				"https://www.krakend.io/schema/v2.7/backend/amqp/consumer.json": {
					"$schema": "http://json-schema.org/draft-07/schema#",
					"title": "AMQP Consumer",
					"description": "The AMQP component allows to send and receive messages to and from a queue through the API Gateway.\n\nSee: https://www.krakend.io/docs/backends/amqp-consumer/",
					"type": "object",
					"required": [
						"name",
						"exchange",
						"routing_key"
					],
					"properties": {
						"auto_ack": {
							"title": "Auto ACK",
							"description": "When KrakenD retrieves the messages, regardless of the success or failure of the operation, it marks them as `ACK`nowledge.\n\nSee: https://www.krakend.io/docs/backends/amqp-consumer/",
							"default": false,
							"type": "boolean"
						},
						"backoff_strategy": {
							"title": "Backoff strategy",
							"description": "When the connection to your event source gets interrupted for whatever reason, KrakenD keeps trying to reconnect until it succeeds or until it reaches the `max_retries`. The backoff strategy defines the delay in seconds in between consecutive failed retries. [Check the meaning of each strategy](https://www.krakend.io/docs/async/#backoff-strategies).\n\nSee: https://www.krakend.io/docs/backends/amqp-consumer/",
							"default": "fallback",
							"enum": [
								"linear",
								"linear-jitter",
								"exponential",
								"exponential-jitter",
								"fallback"
							]
						},
						"delete": {
							"title": "Delete",
							"description": "When `true`, AMQP deletes the queue when there are no remaining connections. This option is **not recommended** in most of the scenarios. If for instance, the connectivity between KrakenD and AMQP is lost for whatever reason and it's the only client, AMQP will delete the queue no matter the number of messages there are inside, and when KrakenD gets the connection again the queue won't exist and future connections will recreate it again.\n\nSee: https://www.krakend.io/docs/backends/amqp-consumer/",
							"default": false,
							"type": "boolean"
						},
						"durable": {
							"title": "Durable",
							"description": "Durable queues will survive server restarts and remain when there are no remaining consumers or bindings. `true` is recommended, but depends on the use case. \n\nSee: https://www.krakend.io/docs/backends/amqp-consumer/",
							"default": false,
							"type": "boolean"
						},
						"exchange": {
							"title": "Exchange",
							"description": "The exchange name (must have a **topic** type if already exists).\n\nSee: https://www.krakend.io/docs/backends/amqp-consumer/",
							"examples": [
								"some-exchange"
							],
							"type": "string"
						},
						"exclusive": {
							"title": "Exclusive",
							"description": "When `true`, AMQP will allow **a single KrakenD instance** to access the queue. This option is **not recommended** in environments where the gateway needs high availability and you have several instances running.\n\nSee: https://www.krakend.io/docs/backends/amqp-consumer/",
							"default": false,
							"type": "boolean"
						},
						"max_retries": {
							"title": "Max retries",
							"description": "The maximum number of times you will allow KrakenD to retry reconnecting to a broken messaging system. During startup KrakenD will wait for a maximum of 3 retries before starting to use this policy. Use 0 for unlimited retries.\n\nSee: https://www.krakend.io/docs/backends/amqp-consumer/",
							"default": 0,
							"type": "integer"
						},
						"name": {
							"title": "Name",
							"description": "Queue name.\n\nSee: https://www.krakend.io/docs/backends/amqp-consumer/",
							"type": "string"
						},
						"no_local": {
							"title": "No local",
							"description": "The no_local flag is not supported by RabbitMQ.\n\nSee: https://www.krakend.io/docs/backends/amqp-consumer/",
							"type": "boolean"
						},
						"no_wait": {
							"title": "No wait",
							"description": "When true, do not wait for the server to confirm the request and immediately begin deliveries. If it is not possible to consume, a channel exception will be raised and the channel will be closed.\n\nSee: https://www.krakend.io/docs/backends/amqp-consumer/",
							"type": "boolean"
						},
						"prefetch_count": {
							"title": "Prefetch count",
							"description": "The number of messages you want to prefetch prior to consume them.\n\nSee: https://www.krakend.io/docs/backends/amqp-consumer/",
							"default": 0,
							"type": "integer"
						},
						"routing_key": {
							"title": "Routing keys",
							"description": "The list of routing keys you will use to consume messages.\n\nSee: https://www.krakend.io/docs/backends/amqp-consumer/",
							"examples": [
								"#"
							],
							"type": "array"
						}
					},
					"patternProperties": {
						"^[@$_#]": {}
					},
					"additionalProperties": false
				},
				"https://www.krakend.io/schema/v2.7/backend/amqp/producer.json": {
					"$schema": "http://json-schema.org/draft-07/schema#",
					"title": "AMQP Producer",
					"description": "Send messages to a queue through the API Gateway.\n\nSee: https://www.krakend.io/docs/backends/amqp-producer/",
					"type": "object",
					"required": [
						"name",
						"exchange",
						"routing_key"
					],
					"properties": {
						"backoff_strategy": {
							"title": "Backoff strategy",
							"description": "When the connection to your event source gets interrupted for whatever reason, KrakenD keeps trying to reconnect until it succeeds or until it reaches the `max_retries`. The backoff strategy defines the delay in seconds in between consecutive failed retries. [Check the meaning of each strategy](https://www.krakend.io/docs/async/#backoff-strategies).\n\nSee: https://www.krakend.io/docs/backends/amqp-producer/",
							"default": "fallback",
							"enum": [
								"linear",
								"linear-jitter",
								"exponential",
								"exponential-jitter",
								"fallback"
							]
						},
						"delete": {
							"title": "Delete",
							"description": "When `true`, AMQP deletes the queue when there are no remaining connections. This option is **not recommended** in most of the scenarios. If for instance, the connectivity between KrakenD and AMQP is lost for whatever reason and it's the only client, AMQP will delete the queue no matter the number of messages there are inside, and when KrakenD gets the connection again the queue won't exist and future connections will recreate it again.\n\nSee: https://www.krakend.io/docs/backends/amqp-producer/",
							"default": false,
							"type": "boolean"
						},
						"durable": {
							"title": "Durable",
							"description": "true is recommended, but depends on the use case. Durable queues will survive server restarts and remain when there are no remaining consumers or bindings.\n\nSee: https://www.krakend.io/docs/backends/amqp-producer/",
							"default": false,
							"type": "boolean"
						},
						"exchange": {
							"title": "Exchange",
							"description": "The exchange name (must have a topic type if already exists).\n\nSee: https://www.krakend.io/docs/backends/amqp-producer/",
							"examples": [
								"some-exchange"
							],
							"type": "string"
						},
						"exclusive": {
							"title": "Exclusive",
							"description": "When `true`, AMQP will allow **a single KrakenD instance** to access the queue. This option is **not recommended** in environments where the gateway needs high availability and you have several instances running.\n\nSee: https://www.krakend.io/docs/backends/amqp-producer/",
							"default": false,
							"type": "boolean"
						},
						"exp_key": {
							"title": "Expiration key",
							"description": "Take a parameter from a `{placeholder}` in the endpoint definition to use as the expiration key. The key must have the first letter uppercased. For instance, when an endpoint parameter is defined as `{id}`, you must write `Id`.\n\nSee: https://www.krakend.io/docs/backends/amqp-producer/",
							"default": "",
							"type": "string"
						},
						"immediate": {
							"title": "Immediate",
							"description": "A consumer must be connected to the queue when true.\n\nSee: https://www.krakend.io/docs/backends/amqp-producer/",
							"default": false,
							"type": "boolean"
						},
						"mandatory": {
							"title": "Mandatory",
							"description": "The exchange must have at least one queue bound when true.\n\nSee: https://www.krakend.io/docs/backends/amqp-producer/",
							"default": false,
							"type": "boolean"
						},
						"max_retries": {
							"title": "Max retries",
							"description": "The maximum number of times you will allow KrakenD to retry reconnecting to a broken messaging system. During startup KrakenD will wait for a maximum of 3 retries before starting to use this policy. Use 0 for unlimited retries.\n\nSee: https://www.krakend.io/docs/backends/amqp-producer/",
							"default": 0,
							"type": "integer"
						},
						"msg_id_key": {
							"title": "Expiration key",
							"description": "Take a parameter from a `{placeholder}` in the endpoint definition to use as the message identifier. The key must have the first letter uppercased. For instance, when an endpoint parameter is defined as `{id}`, you must write `Id`.\n\nSee: https://www.krakend.io/docs/backends/amqp-producer/",
							"default": "",
							"type": "string"
						},
						"name": {
							"title": "Name",
							"description": "Queue name.\n\nSee: https://www.krakend.io/docs/backends/amqp-producer/",
							"type": "string"
						},
						"no_local": {
							"title": "No local",
							"description": "The no_local flag is not supported by RabbitMQ.\n\nSee: https://www.krakend.io/docs/backends/amqp-consumer/",
							"type": "boolean"
						},
						"no_wait": {
							"title": "No wait",
							"description": "When true, do not wait for the server to confirm the request and immediately begin deliveries. If it is not possible to consume, a channel exception will be raised and the channel will be closed.\n\nSee: https://www.krakend.io/docs/backends/amqp-producer/",
							"type": "boolean"
						},
						"priority_key": {
							"title": "Expiration key",
							"description": "Take a parameter from a `{placeholder}` in the endpoint definition to use as the reply key. The key must have the first letter uppercased. For instance, when an endpoint parameter is defined as `{id}`, you must write `Id`.\n\nSee: https://www.krakend.io/docs/backends/amqp-producer/",
							"default": "",
							"type": "string"
						},
						"reply_to_key": {
							"title": "Expiration key",
							"description": "Take a parameter from a `{placeholder}` in the endpoint definition to use as the reply key. The key must have the first letter uppercased. For instance, when an endpoint parameter is defined as `{id}`, you must write `Id`.\n\nSee: https://www.krakend.io/docs/backends/amqp-producer/",
							"default": "",
							"type": "string"
						},
						"routing_key": {
							"title": "Routing key",
							"description": "The routing key you will use to send messages, case sensitive.\n\nSee: https://www.krakend.io/docs/backends/amqp-producer/",
							"default": "#",
							"type": "string"
						},
						"static_routing_key": {
							"title": "Static Routing key",
							"description": "Defines whether the `routing_key` will have a static value or not, instead of taking the value from a parameter.\n\nSee: https://www.krakend.io/docs/backends/amqp-producer/",
							"default": false,
							"type": "boolean"
						}
					},
					"patternProperties": {
						"^[@$_#]": {}
					},
					"additionalProperties": false
				},
				"https://www.krakend.io/schema/v2.7/backend/graphql.json": {
					"$schema": "http://json-schema.org/draft-07/schema#",
					"title": "GraphQL",
					"description": "Convert REST endpoints to GraphQL calls (adapter/transformer)",
					"type": "object",
					"oneOf": [
						{
							"required": [
								"type",
								"query"
							]
						},
						{
							"required": [
								"type",
								"query_path"
							]
						}
					],
					"properties": {
						"type": {
							"title": "Query type",
							"description": "The type of query you are declaring, `query` (read), or `mutation` (write).\n\nSee: https://www.krakend.io/docs/backends/graphql/",
							"enum": [
								"query",
								"mutation"
							]
						},
						"operationName": {
							"title": "Operation name",
							"description": "A meaningful and explicit name for your operation, required in multi-operation documents and for helpful debugging and server-side logging.\n\nSee: https://www.krakend.io/docs/backends/graphql/",
							"examples": [
								"addMktPreferencesForUser"
							],
							"type": "string"
						},
						"query": {
							"title": "Query",
							"description": "An inline GraphQL query you want to send to the server. Use this attribute for simple and inline queries, use `query_path` instead for larger queries. Use escaping when needed.\n\nSee: https://www.krakend.io/docs/backends/graphql/",
							"examples": [
								"{ \n find_follower(func: uid(\"0x3\")) {\n name \n }\n }"
							],
							"type": "string"
						},
						"query_path": {
							"title": "Query path",
							"description": "Path to the file containing the query. This file is loaded during startup and never checked again, if it changes KrakenD will be unaware of it.\n\nSee: https://www.krakend.io/docs/backends/graphql/",
							"examples": [
								"./graphql/mutations/marketing.graphql"
							],
							"type": "string"
						},
						"variables": {
							"title": "Variables",
							"description": "A dictionary defining all the variables sent to the GraphQL server. You can use `{placeholders}` to inject parameters from the endpoint URL.\n\nSee: https://www.krakend.io/docs/backends/graphql/",
							"type": "object"
						}
					},
					"patternProperties": {
						"^[@$_#]": {}
					},
					"additionalProperties": false
				},
				"https://www.krakend.io/schema/v2.7/backend/grpc.json": {
					"$schema": "http://json-schema.org/draft-07/schema#",
					"title": "gRPC backend connection",
					"description": "Enterprise only. Handles the communication with a backend using gRPC, after having defined the protocol buffer definitions.\n\nSee: https://www.krakend.io/docs/backends/grpc/",
					"type": "object",
					"properties": {
						"client_tls": {
							"title": "Enable TLS client options",
							"description": "Enables specific TLS connection options when using the gRPC service. Supports all options under [TLS client settings](https://www.krakend.io/docs/service-settings/tls/#client-tls-settings).\n\nSee: https://www.krakend.io/docs/backends/grpc/",
							"$ref": "#/definitions/https%3A~1~1www.krakend.io~1schema~1v2.7~1client_tls.json"
						},
						"disable_query_params": {
							"title": "Disable query parameters",
							"description": "When `true`, it does not use URL parameters (`{placeholders}` in endpoints)  or query strings to fill the gRPC payload to send. If `use_request_body` is not set, or set to `false`, and this option is set to `true`, there will be no input used for the gRPC message to send. That is still a valid option, when we just want to send the message with its default values, or when the input for the gRPC calls is just the [empty message](https://github.com/protocolbuffers/protobuf/blob/main/src/google/protobuf/empty.proto).\n\nSee: https://www.krakend.io/docs/backends/grpc/",
							"default": false,
							"type": "boolean"
						},
						"header_mapping": {
							"title": "Mapping of headers",
							"description": "A dictionary that rename the received header (key) to a new header name (value). If the header starts with `grpc` they will be renamed to `in-grpc-*` as the word is reserved.\n\nSee: https://www.krakend.io/docs/backends/grpc/",
							"examples": [
								{
									"X-Tenant": "customerid"
								}
							],
							"type": "object",
							"patternProperties": {
								".*": {
									"type": "string"
								}
							},
							"additionalProperties": false
						},
						"input_mapping": {
							"title": "Mapping of parameters",
							"description": "A dictionary that converts query string parameters and parameters from `{placeholders}` into a different field during the backend request. When passing parameters using `{placeholder}` the parameter capitalizes the first letter, so you receive `Placeholder`.\n\nSee: https://www.krakend.io/docs/backends/grpc/",
							"examples": [
								{
									"lat": "where.latitude",
									"lon": "where.longitude"
								}
							],
							"type": "object",
							"patternProperties": {
								".*": {
									"type": "string"
								}
							},
							"additionalProperties": false
						},
						"output_duration_as_string": {
							"title": "Output duration types as string",
							"description": "Well-known Duration types (`google.protobuf.Duration`) are returned as a struct containing fields with `seconds` and `nanos` fields (flag set to `false`). Setting this flag to `true` transforms the timestamps into a string representation in seconds.\n\nSee: https://www.krakend.io/docs/backends/grpc/",
							"default": false,
							"type": "boolean"
						},
						"output_enum_as_string": {
							"title": "Output enum types as string",
							"description": "Enum types are returned as numeric values (flag set to `false`). Set this flag to `true` to return the string representation of the enum value. For instance, an enum representing allergies, such as `['NUTS', 'MILK', ' SOY', 'WHEAT']` would return a value `SOY` when this flag is `true`, or `2` when `false`.\n\nSee: https://www.krakend.io/docs/backends/grpc/",
							"default": false,
							"type": "boolean"
						},
						"output_remove_unset_values": {
							"title": "Output removes unset values",
							"description": "When the response has missing fields from the definition, they are returned with default values. Setting this flag to `true` removes those fields from the response, while setting it to `false` or not setting it, returns all the fields in the definition.\n\nSee: https://www.krakend.io/docs/backends/grpc/",
							"default": false,
							"type": "boolean"
						},
						"output_timestamp_as_string": {
							"title": "Output timestamps types as string",
							"description": "Well-known Timestamp types (`google.protobuf.Timestamp`) are returned as a struct containing fields with `seconds` and `nanos` fields (flag set to `false`). Setting this flag to `true` transforms the timestamps into a string representation in RFC3999 format.\n\nSee: https://www.krakend.io/docs/backends/grpc/",
							"default": false,
							"type": "boolean"
						},
						"request_naming_convention": {
							"title": "Request naming convention",
							"description": "Defines the naming convention used to format the request. Applies to query strings and JSON field names. By default, the gateway uses `snake_case` which makes use of the standard `encoding/json` package, while when you choose `camelCase` the `protobuf/encoding` deserialization is used instead.\n\nSee: https://www.krakend.io/docs/backends/grpc/",
							"default": "snake_case",
							"enum": [
								"camelCase",
								"snake_case"
							]
						},
						"response_naming_convention": {
							"title": "Response naming convention",
							"description": "Defines the naming convention used to format the returned data. By default, the gateway uses `snake_case` which makes use of the standard `encoding/json` package, while when you choose `camelCase` the `protobuf/encoding` deserialization is used instead.\n\nSee: https://www.krakend.io/docs/backends/grpc/",
							"default": "snake_case",
							"enum": [
								"camelCase",
								"snake_case"
							]
						},
						"use_alternate_host_on_error": {
							"title": "Use alternate host on error",
							"description": "When `true`, before sending a message to a host, it checks if the connection status is in a \"transient failure\" or \"failure\" state and tries to use a different host (from the service discovery or randomly from the list of hosts). If the connection is in a valid state, but an error happens when sending the gRPC message, it also tries to use a different host to retry sending the message. Depending on the host list, the retry attempts may go to the same host initially in a \"bad state\".\n\nSee: https://www.krakend.io/docs/backends/grpc/",
							"type": "boolean"
						},
						"use_request_body": {
							"title": "Use body",
							"description": "Enables the use of the sent body to fill the gRPC request. Take into account that when you set this flag to `true` a body is expected, and this body is **consumed** in the first backend. If the endpoint that uses this gRPC backend has additional backends (either gRPC or HTTP) that also expect to consume the payload, these requests might fail.\n\nSee: https://www.krakend.io/docs/backends/grpc/",
							"default": false,
							"type": "boolean"
						}
					},
					"patternProperties": {
						"^[@$_#]": {}
					},
					"additionalProperties": false
				},
				"https://www.krakend.io/schema/v2.7/backend/http_client.json": {
					"$schema": "http://json-schema.org/draft-07/schema#",
					"title": "HTTP Client options",
					"description": "Enterprise only. Allows you to set the different HTTP client options with the backend, like TLS, no redirect or connect via a proxy.\n\nSee: https://www.krakend.io/docs/enterprise/backends/http-client/",
					"type": "object",
					"properties": {
						"client_tls": {
							"title": "TLS Client settings",
							"description": "Allows to set specific transport settings when using TLS in your upstream services. See the global [Client TLS options](https://www.krakend.io/docs/service-settings/tls/#client-tls-settings) for the list of all supported options.",
							"$ref": "#/definitions/https%3A~1~1www.krakend.io~1schema~1v2.7~1client_tls.json"
						},
						"no_redirect": {
							"title": "No redirect",
							"description": "Set `no_redirect` to true if you don't want KrakenD to follow redirects and let the consuming user to receive the `30x` status code.\n\nSee: https://www.krakend.io/docs/enterprise/backends/http-client/",
							"default": false,
							"type": "boolean"
						},
						"proxy_address": {
							"title": "Proxy address",
							"description": "The proxy address used to forward the traffic. The address must contain the protocol and the port.\n\nSee: https://www.krakend.io/docs/enterprise/backends/http-client/",
							"examples": [
								"http://proxy.corp:9099"
							]
						}
					},
					"patternProperties": {
						"^[@$_#]": {}
					},
					"additionalProperties": false
				},
				"https://www.krakend.io/schema/v2.7/backend/lambda.json": {
					"$schema": "http://json-schema.org/draft-07/schema#",
					"title": "AWS Lambda functions",
					"description": "Invoke Amazon Lambda functions on a KrakenD endpoint call.\n\nSee: https://www.krakend.io/docs/backends/lambda/",
					"type": "object",
					"properties": {
						"endpoint": {
							"title": "Endpoint",
							"description": "An optional parameter to customize the Lambda endpoint to call. Useful when Localstack is used for testing instead of direct AWS usage.\n\nSee: https://www.krakend.io/docs/backends/",
							"type": "string"
						},
						"function_name": {
							"title": "Function name",
							"description": "Name of the lambda function as saved in the AWS service. You have to choose between function_name and function_param_name but not both.\n\nSee: https://www.krakend.io/docs/backends/",
							"type": "string"
						},
						"function_param_name": {
							"title": "Function_param_name",
							"description": "The endpoint {placeholder} that sets the function name, with the **first letter uppercased**. You have to choose between `function_name` and `function_param_name` but not both. If your endpoint defines the route `/foo/{bar}` the value of `function_param_name` must be `Bar` with the uppercased B.\n\nSee: https://www.krakend.io/docs/backends/",
							"type": "string"
						},
						"max_retries": {
							"title": "Max retries",
							"description": "Maximum times you want to execute the function until you have a successful response. The value -1 defers the max retry setting to the service specific configuration.\n\nSee: https://www.krakend.io/docs/backends/",
							"default": 0,
							"type": "integer"
						},
						"region": {
							"title": "AWS Region",
							"description": "The AWS identifier region\n\nSee: https://www.krakend.io/docs/backends/",
							"examples": [
								"us-east-1",
								"eu-west-2"
							],
							"type": "string"
						}
					},
					"patternProperties": {
						"^[@$_#]": {}
					},
					"additionalProperties": false
				},
				"https://www.krakend.io/schema/v2.7/backend/pubsub/publisher.json": {
					"$schema": "http://json-schema.org/draft-07/schema#",
					"title": "Pubsub publisher",
					"description": "Publishes to a topic using the desired driver.\n\nSee: https://www.krakend.io/docs/backends/pubsub/",
					"type": "object",
					"required": [
						"topic_url"
					],
					"properties": {
						"topic_url": {
							"title": "Topic URL",
							"description": "Topic URL according to the selected driver\n\nSee: https://www.krakend.io/docs/backends/pubsub/",
							"type": "string"
						}
					},
					"patternProperties": {
						"^[@$_#]": {}
					},
					"additionalProperties": false
				},
				"https://www.krakend.io/schema/v2.7/backend/pubsub/subscriber.json": {
					"$schema": "http://json-schema.org/draft-07/schema#",
					"title": "Pubsub subscriber",
					"description": "Subscribes a backend using the desired driver.\n\nSee: https://www.krakend.io/docs/backends/pubsub/",
					"type": "object",
					"required": [
						"subscription_url"
					],
					"properties": {
						"subscription_url": {
							"title": "Subscription URL",
							"description": "Subscription URL according to the selected driver\n\nSee: https://www.krakend.io/docs/backends/pubsub/",
							"type": "string"
						}
					},
					"patternProperties": {
						"^[@$_#]": {}
					},
					"additionalProperties": false
				},
				"https://www.krakend.io/schema/v2.7/backend/soap.json": {
					"$schema": "http://json-schema.org/draft-07/schema#",
					"title": "SOAP Template modifier",
					"description": "Enterprise only. Build and modify requests to communicate with SOAP services.\n\nSee: https://www.krakend.io/docs/backends/soap/",
					"type": "object",
					"oneOf": [
						{
							"required": [
								"path"
							]
						},
						{
							"required": [
								"template"
							]
						}
					],
					"minItems": 1,
					"properties": {
						"content_type": {
							"title": "Content-Type",
							"description": "The `Content-Type` used in your template, and that will be sent to the SOAP server. This is not the content-type the end-user sent in the request.\n\nSee: https://www.krakend.io/docs/backends/soap/",
							"examples": [
								"application/xml",
								"text/xml"
							],
							"default": "text/xml",
							"type": "string"
						},
						"debug": {
							"title": "Enable debug",
							"description": "When `true`, shows useful information in the logs with `DEBUG` level about the input received and the body generated. Do not enable in production. Debug logs are multiline and designed fore developer readibility, not machine processing.\n\nSee: https://www.krakend.io/docs/backends/soap/",
							"default": false,
							"type": "boolean"
						},
						"path": {
							"title": "Path to template",
							"description": "The path to the Go template file you want to use to craft the body.\n\nSee: https://www.krakend.io/docs/backends/soap/",
							"examples": [
								"./path/to.xml"
							],
							"type": "string"
						},
						"template": {
							"title": "Template",
							"description": "An inline base64 encoded Go template with the body XML content you want to send to the SOAP service. This option is useful if you don't want to rely on external files and embed the template in the configuration.\n\nSee: https://www.krakend.io/docs/backends/soap/",
							"type": "string"
						}
					},
					"patternProperties": {
						"^[@$_#]": {}
					},
					"additionalProperties": false
				},
				"https://www.krakend.io/schema/v2.7/backend/static-filesystem.json": {
					"$schema": "http://json-schema.org/draft-07/schema#",
					"title": "Static Filesystem",
					"description": "Enterprise only. Allows you to fetch and serve static content from the disk instead of a remote server, and you can use it to mock data.\n\nSee: https://www.krakend.io/docs/enterprise/endpoints/serve-static-content/",
					"type": "object",
					"required": [
						"path"
					],
					"properties": {
						"directory_listing": {
							"description": "Whether to allow directory listings or not",
							"default": false,
							"type": "boolean"
						},
						"path": {
							"title": "Path",
							"description": "The folder in the filesystem containing the static files. Relative to the working dir where KrakenD config is (e.g.: `./assets`) or absolute (e.g.: `/var/www/assets`).\n\nSee: https://www.krakend.io/docs/enterprise/endpoints/serve-static-content/",
							"examples": [
								"./static/"
							],
							"type": "string"
						}
					},
					"patternProperties": {
						"^[@$_#]": {}
					},
					"additionalProperties": false
				},
				"https://www.krakend.io/schema/v2.7/backend_extra_config.json": {
					"$schema": "http://json-schema.org/draft-07/schema#",
					"title": "Extra configuration for backends",
					"type": "object",
					"properties": {
						"auth/client-credentials": {
							"$ref": "#/definitions/https%3A~1~1www.krakend.io~1schema~1v2.7~1auth~1client-credentials.json"
						},
						"auth/gcp": {
							"$ref": "#/definitions/https%3A~1~1www.krakend.io~1schema~1v2.7~1auth~1gcp.json"
						},
						"auth/ntlm": {
							"$ref": "#/definitions/https%3A~1~1www.krakend.io~1schema~1v2.7~1auth~1ntlm.json"
						},
						"backend/amqp/consumer": {
							"$ref": "#/definitions/https%3A~1~1www.krakend.io~1schema~1v2.7~1backend~1amqp~1consumer.json"
						},
						"backend/amqp/producer": {
							"$ref": "#/definitions/https%3A~1~1www.krakend.io~1schema~1v2.7~1backend~1amqp~1producer.json"
						},
						"backend/graphql": {
							"$ref": "#/definitions/https%3A~1~1www.krakend.io~1schema~1v2.7~1backend~1graphql.json"
						},
						"backend/grpc": {
							"$ref": "#/definitions/https%3A~1~1www.krakend.io~1schema~1v2.7~1backend~1grpc.json"
						},
						"backend/http": {
							"oneOf": [
								{
									"required": [
										"return_error_details"
									]
								},
								{
									"required": [
										"return_error_code"
									]
								}
							],
							"properties": {
								"return_error_code": {
									"title": "Return error code",
									"description": "Returns the HTTP status code of the backend (when there is only one). The headers are not returned.\n\nSee: https://www.krakend.io/docs/backends/detailed-errors/",
									"type": "boolean"
								},
								"return_error_details": {
									"title": "Return error details",
									"description": "Returns to the client details of a failing request.\n\nSee: https://www.krakend.io/docs/backends/detailed-errors/",
									"type": "string"
								}
							}
						},
						"backend/http/client": {
							"$ref": "#/definitions/https%3A~1~1www.krakend.io~1schema~1v2.7~1backend~1http_client.json"
						},
						"backend/lambda": {
							"$ref": "#/definitions/https%3A~1~1www.krakend.io~1schema~1v2.7~1backend~1lambda.json"
						},
						"backend/pubsub/publisher": {
							"$ref": "#/definitions/https%3A~1~1www.krakend.io~1schema~1v2.7~1backend~1pubsub~1publisher.json"
						},
						"backend/pubsub/subscriber": {
							"$ref": "#/definitions/https%3A~1~1www.krakend.io~1schema~1v2.7~1backend~1pubsub~1subscriber.json"
						},
						"backend/soap": {
							"$ref": "#/definitions/https%3A~1~1www.krakend.io~1schema~1v2.7~1backend~1soap.json"
						},
						"backend/static-filesystem": {
							"$ref": "#/definitions/https%3A~1~1www.krakend.io~1schema~1v2.7~1backend~1static-filesystem.json"
						},
						"modifier/body-generator": {
							"$ref": "#/definitions/https%3A~1~1www.krakend.io~1schema~1v2.7~1modifier~1body-generator.json"
						},
						"modifier/jmespath": {
							"$ref": "#/definitions/https%3A~1~1www.krakend.io~1schema~1v2.7~1modifier~1jmespath.json"
						},
						"modifier/lua-backend": {
							"$ref": "#/definitions/https%3A~1~1www.krakend.io~1schema~1v2.7~1modifier~1lua.json"
						},
						"modifier/martian": {
							"title": "Martian modifiers",
							"description": "Transform requests and responses through a simple DSL definition in the configuration file.\n\nSee: https://www.krakend.io/docs/backends/martian/",
							"$ref": "#/definitions/https%3A~1~1www.krakend.io~1schema~1v2.7~1modifier~1martian.json",
							"type": "object"
						},
						"modifier/request-body-generator": {
							"$ref": "#/definitions/https%3A~1~1www.krakend.io~1schema~1v2.7~1modifier~1body-generator.json"
						},
						"modifier/response-body-generator": {
							"$ref": "#/definitions/https%3A~1~1www.krakend.io~1schema~1v2.7~1modifier~1body-generator.json"
						},
						"plugin/http-client": {
							"$ref": "#/definitions/https%3A~1~1www.krakend.io~1schema~1v2.7~1plugin~1http-client.json"
						},
						"plugin/req-resp-modifier": {
							"$ref": "#/definitions/https%3A~1~1www.krakend.io~1schema~1v2.7~1plugin~1req-resp-modifier.json"
						},
						"proxy": {
							"$ref": "#/definitions/https%3A~1~1www.krakend.io~1schema~1v2.7~1proxy.json"
						},
						"qos/circuit-breaker": {
							"$ref": "#/definitions/https%3A~1~1www.krakend.io~1schema~1v2.7~1qos~1circuit-breaker.json"
						},
						"qos/http-cache": {
							"title": "Backend Cache",
							"description": "Enable in-memory caching for backend responses for as long as its Cache-Control header permits.\n\nSee: https://www.krakend.io/docs/backends/caching/",
							"type": "object",
							"properties": {
								"shared": {
									"title": "Shared cache",
									"description": "The `shared` cache makes that different backend definitions with this flag enabled can reuse the cache. When the `shared` flag is missing or set to false, the backend uses its own cache private context.\n\nSee: https://www.krakend.io/docs/backends/detailed-errors/",
									"type": "boolean"
								}
							},
							"patternProperties": {
								"^[@$_#]": {}
							},
							"additionalProperties": false
						},
						"qos/ratelimit/proxy": {
							"$ref": "#/definitions/https%3A~1~1www.krakend.io~1schema~1v2.7~1qos~1ratelimit~1proxy.json"
						},
						"security/policies": {
							"$ref": "#/definitions/https%3A~1~1www.krakend.io~1schema~1v2.7~1security~1policies.json",
							"anyOf": [
								{
									"required": [
										"req"
									]
								},
								{
									"required": [
										"resp"
									]
								}
							],
							"not": {
								"required": [
									"jwt"
								]
							},
							"patternProperties": {
								"^[@$_#]": {}
							},
							"additionalProperties": false
						},
						"telemetry/opentelemetry": {
							"$ref": "#/definitions/https%3A~1~1www.krakend.io~1schema~1v2.7~1telemetry~1opentelemetry-backend.json"
						},
						"validation/cel": {
							"$ref": "#/definitions/https%3A~1~1www.krakend.io~1schema~1v2.7~1validation~1cel.json"
						},
						"workflow": {
							"title": "Workflow",
							"description": "Enterprise only. A whole new endpoint that is executed within this backend context.",
							"$ref": "#/definitions/https%3A~1~1www.krakend.io~1schema~1v2.7~1workflow.json",
							"type": "object"
						}
					},
					"patternProperties": {
						"^[@$_#]": {}
					},
					"additionalProperties": false
				},
				"https://www.krakend.io/schema/v2.7/client_tls.json": {
					"$schema": "http://json-schema.org/draft-07/schema#",
					"title": "TLS client settings",
					"description": "TLS options to connect to upstream services.\n\nSee: https://www.krakend.io/docs/service-settings/tls/",
					"properties": {
						"allow_insecure_connections": {
							"title": "Allow insecure connections",
							"description": "By default, KrakenD verifies every SSL connection. This option allows you to connect to backends considered **insecure**, for instance when you are using self-signed certificates",
							"default": false,
							"type": "boolean"
						},
						"ca_certs": {
							"title": "CA certificates",
							"description": "An array with all the CA certificates you would like to validate the server you are connecting to.\n\nSee: https://www.krakend.io/docs/service-settings/tls/",
							"examples": [
								[
									"ca.pem"
								]
							],
							"default": [],
							"type": "array"
						},
						"cipher_suites": {
							"title": "Cipher Suites",
							"description": "The list of cipher suites as defined in the documentation.\n\nSee: https://www.krakend.io/docs/service-settings/tls/",
							"default": [
								4865,
								4866,
								4867
							],
							"type": "array",
							"uniqueItems": true
						},
						"client_certs": {
							"title": "Cipher Suites",
							"description": "The list of cipher suites as defined in the documentation.\n\nSee: https://www.krakend.io/docs/service-settings/tls/",
							"type": "array",
							"items": {
								"type": "object",
								"required": [
									"certificate",
									"private_key"
								],
								"properties": {
									"certificate": {
										"title": "Certificate",
										"description": "The path to the certificate you will use for mTLS connections.",
										"type": "string"
									},
									"private_key": {
										"title": "Private key",
										"description": "The path to the private key you will use for mTLS connections.",
										"type": "string"
									}
								},
								"patternProperties": {
									"^[@$_#]": {}
								},
								"additionalProperties": false
							}
						},
						"curve_preferences": {
							"title": "Curve identifiers",
							"description": "The list of all the identifiers for the curve preferences. Use `23` for CurveP256, `24` for CurveP384 or `25` for CurveP521.\n\nSee: https://www.krakend.io/docs/service-settings/tls/",
							"default": [
								23,
								24,
								25
							],
							"type": "array",
							"uniqueItems": true,
							"items": {
								"enum": [
									23,
									24,
									25
								]
							}
						},
						"disable_system_ca_pool": {
							"title": "Disable system's CA",
							"description": "Ignore any certificate in the system's CA. The only certificates loaded will be the ones in the `ca_certs` list when true.\n\nSee: https://www.krakend.io/docs/service-settings/http-server-settings/",
							"default": false,
							"type": "boolean"
						},
						"max_version": {
							"title": "Maximum TLS version",
							"description": "Maximum TLS version supported.\n\nSee: https://www.krakend.io/docs/service-settings/tls/",
							"default": "TLS13",
							"enum": [
								"SSL3.0",
								"TLS10",
								"TLS11",
								"TLS12",
								"TLS13"
							]
						},
						"min_version": {
							"title": "Minimum TLS version",
							"description": "Minimum TLS version supported. When specifiying very old and insecure versions under TLS12 you must provide the `ciphers_list`.\n\nSee: https://www.krakend.io/docs/service-settings/tls/",
							"default": "TLS13",
							"enum": [
								"SSL3.0",
								"TLS10",
								"TLS11",
								"TLS12",
								"TLS13"
							]
						}
					},
					"patternProperties": {
						"^[@$_#]": {}
					},
					"additionalProperties": false
				},
				"https://www.krakend.io/schema/v2.7/documentation/openapi.json": {
					"$schema": "http://json-schema.org/draft-07/schema#",
					"title": "Generate documentation using OpenAPI",
					"description": "Enterprise only. Generates OpenAPI documentation automatically through `krakend openapi export` command.\n\nSee: https://www.krakend.io/docs/enterprise/developer/openapi/",
					"type": "object",
					"properties": {
						"description": {
							"title": "API Description",
							"description": "An introductory, optionally verbose, explanation supporting [CommonMark](http://commonmark.org/help/) syntax. If you'd like to load an **external markdown file**, you can use flexible configuration, for instance `\"description\": {{include \"openapi/intro.md\" | toJson }}`\n\nSee: https://www.krakend.io/docs/enterprise/developer/openapi/",
							"examples": [
								"Hi there, I am [OpenAPI](https://www.krakend.io/docs/enterprise/endpoints/openapi/)"
							],
							"type": "string"
						},
						"audience": {
							"title": "Audience",
							"description": "The list of audiences that will consume this endpoint. These values **do not define the gateway logic** in any way. They are a way to group endpoints and filter them out when generating the OpenAPI documentation. Use `*` to indicate an endpoint will be present in any audience generated.\n\nSee: https://www.krakend.io/docs/enterprise/developer/openapi/",
							"examples": [
								[
									"gold",
									"silver",
									"*"
								]
							],
							"type": "array"
						},
						"base_path": {
							"title": "Base path",
							"description": "A starting path that is appended to any endpoint.\n\nSee: https://www.krakend.io/docs/enterprise/developer/openapi/",
							"examples": [
								"/v1"
							],
							"type": "string"
						},
						"components_schemas": {
							"title": "Component Schemas",
							"description": "The JSON Schemas you can reuse inside endpoint definitions using `ref`. You can either pass the JSON Schema object, or a bas64 string.",
							"examples": [
								{
									"Pet": {
										"type": "object",
										"required": [
											"id",
											"name"
										]
									}
								}
							],
							"type": "object",
							"patternProperties": {
								".*": {
									"title": "JSON Schema",
									"description": "JSON Schema in base64 or as an object",
									"type": [
										"string",
										"object"
									]
								}
							}
						},
						"contact_email": {
							"title": "Contact email",
							"description": "Email where users of your API can write to.\n\nSee: https://www.krakend.io/docs/enterprise/developer/openapi/",
							"examples": [
								"/v1"
							],
							"type": "string"
						},
						"contact_name": {
							"title": "Contact name",
							"description": "Contact name.\n\nSee: https://www.krakend.io/docs/enterprise/developer/openapi/",
							"examples": [
								"/v1"
							],
							"type": "string"
						},
						"contact_url": {
							"title": "Contact URL",
							"description": "Contact URL that users of your API can read.\n\nSee: https://www.krakend.io/docs/enterprise/developer/openapi/",
							"examples": [
								"/v1"
							],
							"type": "string"
						},
						"example": {
							"title": "Example",
							"description": "**Deprecated in OAS3** (use `response_definition` instead). A free form JSON object or a string you would like to show as a sample response of the endpoint. The examples assume they are JSON content types except when using the `output_encoding=string`.\n\nSee: https://www.krakend.io/docs/enterprise/developer/openapi/",
							"type": [
								"object",
								"string"
							]
						},
						"host": {
							"title": "Host",
							"description": "The hostname where you will publish your API.\n\nSee: https://www.krakend.io/docs/enterprise/developer/openapi/",
							"examples": [
								"my.api.com"
							],
							"type": "string"
						},
						"jwt_key": {
							"title": "JWT key",
							"description": "When generating an OpenAPI spec, the name of the JWT key used under components securitySchemes.",
							"default": "KrakenD-JWT",
							"type": "string"
						},
						"license_name": {
							"title": "License name",
							"description": "The license name (e.g.: Apache License)\n\nSee: https://www.krakend.io/docs/enterprise/developer/openapi/",
							"examples": [
								"/v1"
							],
							"type": "string"
						},
						"license_url": {
							"title": "License URL",
							"description": "The URL where the license is hosted\n\nSee: https://www.krakend.io/docs/enterprise/developer/openapi/",
							"examples": [
								"/v1"
							],
							"type": "string"
						},
						"operation_id": {
							"title": "Operation ID",
							"description": "A unique string identifying the operation identifier. Usually the method + the endpoint. If provided, these IDs must be unique among all operations described in your API.\n\nSee: https://www.krakend.io/docs/enterprise/developer/openapi/",
							"examples": [
								"GET/foo"
							],
							"type": "string"
						},
						"param_definition": {
							"title": "Param definition",
							"description": "Sets a description for the URL parameters (e.g.: `/foo/{param}`) required in the endpoint. Make sure to include to write the param exactly as in the endpoint definition.\n\nSee: https://www.krakend.io/docs/enterprise/developer/openapi/",
							"examples": [
								[
									{
										"description": "The unique user ID",
										"name": "id_user"
									}
								]
							],
							"type": "array",
							"items": {
								"title": "Parameters",
								"type": "object",
								"required": [
									"name"
								],
								"properties": {
									"description": {
										"title": "Name",
										"description": "The description of the parameter",
										"type": "string"
									},
									"name": {
										"title": "Name",
										"description": "The name of the parameter, as declared in `endpoint` without the curly braces `{}`",
										"type": "string"
									}
								},
								"patternProperties": {
									"^[@$_#]": {}
								},
								"additionalProperties": false
							}
						},
						"query_definition": {
							"title": "Query definition",
							"description": "Sets a description for the query strings allowed in the endpoint. Make sure to include the same strings in the endpoint's `input_query_strings`.\n\nSee: https://www.krakend.io/docs/enterprise/developer/openapi/",
							"examples": [
								[
									{
										"description": "The number of the page",
										"name": "page"
									}
								]
							],
							"type": "array",
							"items": {
								"title": "Query strings",
								"type": "object",
								"required": [
									"name"
								],
								"properties": {
									"description": {
										"title": "Name",
										"description": "The description of the querystring",
										"type": "string"
									},
									"required": {
										"title": "Required",
										"description": "Set to `true` when this query string is required",
										"type": "boolean"
									},
									"name": {
										"title": "Name",
										"description": "The name of the querystring, as declared in `input_query_strings`",
										"type": "string"
									}
								},
								"patternProperties": {
									"^[@$_#]": {}
								},
								"additionalProperties": false
							}
						},
						"request_definition": {
							"title": "Definition of a request",
							"description": "Describes the payload needed to consume the endpoint. If a JSON Schema validation exists, it takes precedence when generating the documentation. An example use case is when you need to document a `multipart/form-data` request body.This property is an array because you can document requests with multiple content types.\n\nSee: https://www.krakend.io/docs/enterprise/developer/openapi/",
							"examples": [
								[
									{
										"description": "Updates the user",
										"content_type": "application/json",
										"example": {
											"first_name": "Mary",
											"id_user": 33
										}
									}
								]
							],
							"type": "array",
							"items": {
								"required": [
									"content_type"
								],
								"properties": {
									"description": {
										"title": "Description",
										"description": "The description of the payload this endpoint accepts.\n\nSee: https://www.krakend.io/docs/enterprise/developer/openapi/",
										"type": [
											"string"
										]
									},
									"content_type": {
										"title": "Content Type",
										"description": "The content type returned by this error, e.g., `application/json`. You cannot repeat this content type in another item.",
										"type": "string"
									},
									"example": {
										"title": "Content Type",
										"description": "A free form JSON object or a string you would like to show as a sample response of the endpoint. The examples assume they are JSON content types except when using the `output_encoding=string`.\n\nSee: https://www.krakend.io/docs/enterprise/developer/openapi/",
										"type": [
											"string",
											"object",
											"array",
											"boolean",
											"integer",
											"null",
											"number"
										]
									},
									"example_schema": {
										"description": "A JSON schema that describes the request format for the accepted payload in the endpoint. Use either example or example_schema, but not both.",
										"type": [
											"string",
											"object"
										]
									},
									"ref": {
										"title": "Reference",
										"description": "The relative reference to the `components/schema` OpenAPI definition that will be used as definition of the accepted request. Notice that the path `#/components/schemas/` is not needed.",
										"examples": [
											"your_schema_name"
										],
										"type": "string"
									}
								},
								"patternProperties": {
									"^[@$_#]": {}
								},
								"additionalProperties": false
							}
						},
						"response_definition": {
							"title": "Definition of errors (OAS3 only)",
							"description": "Describes the different status codes returned by this endpoint. Each key is the definition of the status code, represented by a string. E.g., `200` (success), `500` (internal error), etc.\n\nSee: https://www.krakend.io/docs/enterprise/developer/openapi/",
							"examples": [
								{
									"404": {
										"description": "Page not found",
										"@comment": "Some comment",
										"content_type": "application/json",
										"example": {
											"status": "KO"
										}
									}
								}
							],
							"type": [
								"object"
							],
							"patternProperties": {
								"default|^[0-9]+$": {
									"type": "object",
									"required": [
										"content_type"
									],
									"properties": {
										"description": {
											"title": "Description",
											"description": "The description of this error code, e.g., `Page not found`.\n\nSee: https://www.krakend.io/docs/enterprise/developer/openapi/",
											"type": [
												"string"
											]
										},
										"content_type": {
											"title": "Content Type",
											"description": "The content type returned by this error, e.g., `application/json`",
											"type": "string"
										},
										"example": {
											"title": "Content Type",
											"description": "A free form JSON object or a string you would like to show as a sample response of the endpoint. The examples assume they are JSON content types except when using the `output_encoding=string`.\n\nSee: https://www.krakend.io/docs/enterprise/developer/openapi/",
											"type": [
												"string",
												"object",
												"array",
												"boolean",
												"integer",
												"null",
												"number"
											]
										},
										"example_schema": {
											"description": "A JSON schema that describes the response format for the endpoint, directly as a JSON object, or encoded as a base64 string. Use either example or example_schema, but not both.",
											"type": [
												"string",
												"object"
											]
										},
										"ref": {
											"title": "Reference",
											"description": "The relative reference to the `components/schema` OpenAPI definition that will be used as definition of the accepted request. Notice that the path `#/components/schemas/` is not needed.",
											"examples": [
												"your_schema_name"
											],
											"type": "string"
										}
									},
									"patternProperties": {
										"^[@$_#]": {}
									},
									"additionalProperties": false
								}
							},
							"additionalProperties": false
						},
						"schemes": {
							"title": "Supported schemes",
							"description": "The list of schemes supported by the API, e.g. `http` or `https`\n\nSee: https://www.krakend.io/docs/enterprise/developer/openapi/",
							"examples": [
								[
									"https",
									"http"
								]
							],
							"default": [
								"http"
							],
							"type": "array"
						},
						"summary": {
							"title": "Summary",
							"description": "A short summary for the endpoint. Use the description field for the longest explanation.\n\nSee: https://www.krakend.io/docs/enterprise/developer/openapi/",
							"type": "string"
						},
						"tag_definition": {
							"title": "Tag definition",
							"description": "Sets a description for the tags classifiying endpoints when generating the OpenAPI spec.\n\nSee: https://www.krakend.io/docs/enterprise/developer/openapi/",
							"examples": [
								[
									{
										"description": "Description of tag1",
										"name": "Tag1"
									}
								]
							],
							"type": "array",
							"items": {
								"title": "Tags",
								"type": "object",
								"required": [
									"name"
								],
								"properties": {
									"description": {
										"title": "Tag Description",
										"description": "Describe what this tag is grouping",
										"type": "string"
									},
									"name": {
										"title": "Tag name",
										"description": "The name of the tag. You will use this name in each endpoint.",
										"type": "string"
									}
								},
								"patternProperties": {
									"^[@$_#]": {}
								},
								"additionalProperties": false
							}
						},
						"tags": {
							"title": "Tags",
							"description": "You can assign a list of tags to each API operation. If you declare tags in the `tag_definition` at the OpenAPI service level, they will have a description in the documentation. Tagged operations may be handled differently by tools and libraries. For example, Swagger UI uses tags to group the displayed operations.\n\nSee: https://www.krakend.io/docs/enterprise/developer/openapi/",
							"type": "array"
						},
						"terms_of_service": {
							"title": "Terms of Service",
							"description": "The URL to the terms of service for using this API.\n\nSee: https://www.krakend.io/docs/enterprise/developer/openapi/",
							"examples": [
								"/v1"
							],
							"type": "string"
						},
						"version": {
							"title": "Version",
							"description": "The version numbering you want to apply to this release of API., e.g.: `1.0`.\n\nSee: https://www.krakend.io/docs/enterprise/developer/openapi/",
							"examples": [
								"1.0"
							],
							"type": "string"
						}
					},
					"patternProperties": {
						"^[@$_#]": {}
					},
					"additionalProperties": false
				},
				"https://www.krakend.io/schema/v2.7/endpoint.json": {
					"$schema": "http://json-schema.org/draft-07/schema#",
					"title": "Endpoint Object",
					"default": {
						"backend": [
							{
								"url_pattern": "/url"
							}
						],
						"endpoint": "/foo"
					},
					"type": "object",
					"required": [
						"endpoint",
						"backend"
					],
					"properties": {
						"backend": {
							"title": "Backend",
							"description": "List of all the [backend objects](https://www.krakend.io/docs/backends/) queried for this endpoint",
							"type": "array",
							"minItems": 1,
							"items": {
								"$ref": "#/definitions/https%3A~1~1www.krakend.io~1schema~1v2.7~1backend.json",
								"type": "object"
							}
						},
						"cache_ttl": {
							"title": "Cache TTL",
							"description": "Sets or overrides the cache headers to inform for how long the client or CDN can cache the request to this endpoint. Setting this value to a zero-value will use the `cache_ttl` of the service if any. Related: [caching backend responses](https://www.krakend.io/docs/backends/caching/).",
							"$ref": "#/definitions/https%3A~1~1www.krakend.io~1schema~1v2.7~1timeunits.json/$defs/timeunit"
						},
						"concurrent_calls": {
							"title": "Concurrent calls",
							"description": "The concurrent requests are an excellent technique to improve the response times and decrease error rates by requesting in parallel the same information multiple times. Yes, you make the same request to several backends instead of asking to just one. When the first backend returns the information, the remaining requests are canceled.\n\nSee: https://www.krakend.io/docs/endpoints/concurrent-requests/",
							"default": 1,
							"type": "integer",
							"maximum": 5,
							"minimum": 1
						},
						"endpoint": {
							"title": "Endpoint",
							"description": "The exact string resource URL you want to expose. You can use `{placeholders}` to use variables when needed. URLs do not support colons `:` in their definition. Endpoints should start with slash `/`. Example: `/foo/{var}`. If `{vars}` are placed in the middle words, like in `/var{iable}` you must set in the root level `disable_rest` strict checking. You can also add an ending `/*` in the path to enable [wildcards](https://www.krakend.io/docs/enterprise/endpoints/wildcard/) (Enterprise only)\n\nSee: https://www.krakend.io/docs/endpoints/",
							"examples": [
								"/new-endpoint",
								"/foo/{var}",
								"/foo/{var1}/{var2}"
							],
							"type": "string",
							"pattern": "^\\/[^\\*\\?\\&\\%]*(\\/\\*)?$"
						},
						"extra_config": {
							"title": "Extra configuration",
							"description": "Configuration entries for additional components that are executed within this endpoint, during the request, response or merge operations.",
							"$ref": "#/definitions/https%3A~1~1www.krakend.io~1schema~1v2.7~1endpoint_extra_config.json",
							"type": "object"
						},
						"input_headers": {
							"title": "Allowed Headers In",
							"description": "Defines the list of all headers allowed to reach the backend when passed.\nBy default, KrakenD won't pass any header from the client to the backend. This list is **case-insensitive**. You can declare headers in lowercase, uppercase, or mixed.\nAn entry `[\"Cookie\"]` forwards all cookies, and a single star element `[\"*\"]` as value forwards everything to the backend (**it's safer to avoid this option**), including cookies. See [headers forwarding](https://www.krakend.io/docs/endpoints/parameter-forwarding/#headers-forwarding)",
							"default": [],
							"type": "array",
							"uniqueItems": true,
							"items": {
								"examples": [
									"User-Agent",
									"Accept",
									"*"
								],
								"type": "string"
							}
						},
						"input_query_strings": {
							"title": "Allowed Query String parameters",
							"description": "Defines the exact list of quey strings parameters that are allowed to reach the backend. This list is **case-sensitive**.\nBy default, KrakenD won't pass any query string to the backend.\nA single star element `[\"*\"]` as value forwards everything to the backend (**it's safer to avoid this option**)\n\nSee: https://www.krakend.io/docs/endpoints/parameter-forwarding/",
							"default": [],
							"type": "array",
							"uniqueItems": true,
							"items": {
								"examples": [
									"page",
									"limit",
									"*"
								],
								"type": "string"
							}
						},
						"method": {
							"title": "Method",
							"description": "The method supported by this endpoint. Create multiple endpoint entries if you need different methods.\n\nSee: https://www.krakend.io/docs/endpoints/",
							"default": "GET",
							"enum": [
								"GET",
								"POST",
								"PUT",
								"PATCH",
								"DELETE"
							]
						},
						"output_encoding": {
							"title": "Output encoding",
							"description": "The gateway can work with several content types, even allowing your clients to choose how to consume the content. See the [supported encodings](https://www.krakend.io/docs/endpoints/content-types/)",
							"default": "json",
							"enum": [
								"json",
								"json-collection",
								"fast-json",
								"xml",
								"negotiate",
								"string",
								"no-op"
							]
						},
						"timeout": {
							"title": "Timeout",
							"description": "The duration you write in the timeout represents the **whole duration of the pipe**, so it counts the time all your backends take to respond and the processing of all the components involved in the endpoint (the request, fetching data, manipulation, etc.). Usually specified in seconds (`s`) or milliseconds (`ms`. e.g.: `2000ms` or `2s`). If you don't set any timeout, the timeout is taken from the entry in the service level, or to the system's default",
							"examples": [
								"2s",
								"1500ms"
							],
							"default": "2s",
							"$ref": "#/definitions/https%3A~1~1www.krakend.io~1schema~1v2.7~1timeunits.json/$defs/timeunit",
							"type": "string"
						}
					},
					"patternProperties": {
						"^[@$_#]": {}
					},
					"additionalProperties": false
				},
				"https://www.krakend.io/schema/v2.7/endpoint_extra_config.json": {
					"$schema": "http://json-schema.org/draft-07/schema#",
					"title": "Schema definition for extra_config of endpoints",
					"type": "object",
					"properties": {
						"auth/api-keys": {
							"title": "API-key validation",
							"description": "Enterprise only. Validates that users of this endpoint pass a valid API-key containing one of the declared roles.\n\nSee: https://www.krakend.io/docs/enterprise/authentication/api-keys/",
							"type": "object",
							"required": [
								"roles"
							],
							"properties": {
								"client_max_rate": {
									"title": "Max rate",
									"description": "If you want to limit the endpoint usage to this specific user at a number of requests per second. Exceeding the number of requests per second will give the client a `429 Too Many Requests` HTTP status code.\n\nSee: https://www.krakend.io/docs/enterprise/authentication/api-keys/",
									"type": "number"
								},
								"identifier": {
									"title": "Override Identifier",
									"description": "The header name or the query string name that contains the API key. By default uses any value declared in the `auth/api-keys` component in the service level.\n\nSee: https://www.krakend.io/docs/enterprise/authentication/api-keys/",
									"type": "string"
								},
								"roles": {
									"title": "",
									"description": "The list of roles allowed to access the endpoint. Values must match (case sensitive) definitions in the `keys` section at the service level of `auth/api-keys`. API Keys not having the right role, or unauthenticated requests, will receive a `401 Unauthorized`.\n\nSee: https://www.krakend.io/docs/enterprise/authentication/api-keys/",
									"type": "array",
									"items": {
										"type": "string"
									}
								},
								"strategy": {
									"title": "Strategy",
									"description": "Specifies where to expect the user API key, whether inside a header or as part of the query string. When you change the strategy at the endpoint level, **you should also set the identifier**, otherwise you could have for instance, a query string strategy expecting to have a URL like `/foo?Authorization=YOUR-KEY`.\n\nSee: https://www.krakend.io/docs/enterprise/authentication/api-keys/",
									"enum": [
										"header",
										"query_string"
									]
								}
							},
							"patternProperties": {
								"^[@$_#]": {}
							},
							"additionalProperties": false
						},
						"auth/basic": {
							"$ref": "#/definitions/https%3A~1~1www.krakend.io~1schema~1v2.7~1auth~1basic.json"
						},
						"auth/signer": {
							"$ref": "#/definitions/https%3A~1~1www.krakend.io~1schema~1v2.7~1auth~1signer.json"
						},
						"auth/validator": {
							"$ref": "#/definitions/https%3A~1~1www.krakend.io~1schema~1v2.7~1auth~1validator.json"
						},
						"documentation/openapi": {
							"$ref": "#/definitions/https%3A~1~1www.krakend.io~1schema~1v2.7~1documentation~1openapi.json"
						},
						"modifier/jmespath": {
							"$ref": "#/definitions/https%3A~1~1www.krakend.io~1schema~1v2.7~1modifier~1jmespath.json"
						},
						"modifier/lua-endpoint": {
							"$ref": "#/definitions/https%3A~1~1www.krakend.io~1schema~1v2.7~1modifier~1lua.json"
						},
						"modifier/lua-proxy": {
							"$ref": "#/definitions/https%3A~1~1www.krakend.io~1schema~1v2.7~1modifier~1lua.json"
						},
						"modifier/request-body-generator": {
							"$ref": "#/definitions/https%3A~1~1www.krakend.io~1schema~1v2.7~1modifier~1body-generator.json"
						},
						"modifier/response-body-generator": {
							"$ref": "#/definitions/https%3A~1~1www.krakend.io~1schema~1v2.7~1modifier~1body-generator.json"
						},
						"plugin/req-resp-modifier": {
							"$ref": "#/definitions/https%3A~1~1www.krakend.io~1schema~1v2.7~1plugin~1req-resp-modifier.json"
						},
						"proxy": {
							"title": "Proxy",
							"type": "object",
							"properties": {
								"combiner": {
									"title": "Custom combiner",
									"description": "For custom builds of KrakenD only",
									"examples": [
										"combiner_name"
									],
									"type": "string"
								},
								"flatmap_filter": {
									"title": "Flatmap (Array manipulation)",
									"description": "The list of operations to **execute sequentially** (top down). Every operation is defined with an object containing two properties:\n\nSee: https://www.krakend.io/docs/backends/flatmap/",
									"$ref": "#/definitions/https%3A~1~1www.krakend.io~1schema~1v2.7~1proxy~1flatmap.json",
									"type": "array"
								},
								"sequential": {
									"title": "Sequential proxy",
									"description": "The sequential proxy allows you to chain backend requests, making calls dependent one of each other.\n\nSee: https://www.krakend.io/docs/endpoints/sequential-proxy/",
									"default": true,
									"type": "boolean"
								},
								"static": {
									"title": "Static response",
									"description": "The static proxy injects static data in the final response when the selected strategy matches.\n\nSee: https://www.krakend.io/docs/endpoints/static-proxy/",
									"type": "object",
									"required": [
										"data",
										"strategy"
									],
									"properties": {
										"data": {
											"title": "Data",
											"description": "The static data (as a JSON object) that you will return.\n\nSee: https://www.krakend.io/docs/endpoints/static-proxy/",
											"type": "object"
										},
										"strategy": {
											"title": "Strategy",
											"description": "One of the supported strategies\n\nSee: https://www.krakend.io/docs/endpoints/static-proxy/",
											"enum": [
												"always",
												"success",
												"complete",
												"errored",
												"incomplete"
											]
										}
									},
									"patternProperties": {
										"^[@$_#]": {}
									},
									"additionalProperties": false
								}
							},
							"patternProperties": {
								"^[@$_#]": {}
							},
							"additionalProperties": false
						},
						"qos/ratelimit/router": {
							"title": "Router Rate-limiting",
							"description": "The router rate limit feature allows you to set a number of maximum requests per second a KrakenD endpoint will accept.\n\nSee: https://www.krakend.io/docs/endpoints/rate-limit/",
							"$ref": "#/definitions/https%3A~1~1www.krakend.io~1schema~1v2.7~1qos~1ratelimit~1router.json"
						},
						"qos/ratelimit/tiered": {
							"$ref": "#/definitions/https%3A~1~1www.krakend.io~1schema~1v2.7~1qos~1ratelimit~1tiered.json"
						},
						"security/bot-detector": {
							"$ref": "#/definitions/https%3A~1~1www.krakend.io~1schema~1v2.7~1security~1bot-detector.json"
						},
						"security/cors": {
							"$ref": "#/definitions/https%3A~1~1www.krakend.io~1schema~1v2.7~1security~1cors.json"
						},
						"security/http": {
							"$ref": "#/definitions/https%3A~1~1www.krakend.io~1schema~1v2.7~1security~1http.json"
						},
						"security/policies": {
							"$ref": "#/definitions/https%3A~1~1www.krakend.io~1schema~1v2.7~1security~1policies.json"
						},
						"telemetry/opentelemetry": {
							"$ref": "#/definitions/https%3A~1~1www.krakend.io~1schema~1v2.7~1telemetry~1opentelemetry-endpoint.json"
						},
						"validation/cel": {
							"$ref": "#/definitions/https%3A~1~1www.krakend.io~1schema~1v2.7~1validation~1cel.json"
						},
						"validation/json-schema": {
							"title": "Validating the body with the JSON Schema",
							"description": "apply automatic validations using the JSON Schema vocabulary before the content passes to the backends. The json schema component allows you to define validation rules on the body, type definition, or even validate the fields' values.\n\nSee: https://www.krakend.io/docs/endpoints/json-schema/",
							"type": "object"
						},
						"websocket": {
							"$ref": "#/definitions/https%3A~1~1www.krakend.io~1schema~1v2.7~1websocket.json"
						}
					},
					"patternProperties": {
						"^[@$_#]": {}
					},
					"additionalProperties": false
				},
				"https://www.krakend.io/schema/v2.7/grpc.json": {
					"$schema": "http://json-schema.org/draft-07/schema#",
					"title": "gRPC server",
					"description": "Enterprise only. gRPC server integration",
					"type": "object",
					"required": [
						"catalog"
					],
					"properties": {
						"catalog": {
							"title": "Catalog definition",
							"description": "The paths to the different `.pb` files you want to load, or the paths to directories containing `.pb` files. All content is scanned in the order of the list, and after fetching all files it resolves the dependencies of their imports. The order you use here is not important to resolve imports, but it matters when there are conflicts (different files using the same namespace and package type).\n\nSee: https://www.krakend.io/docs/enterprise/grpc/server/",
							"examples": [
								"./grpc/flights.pb",
								"./grpc/definitions",
								"/etc/krakend/grpc"
							],
							"type": "array",
							"items": {
								"type": "string"
							}
						},
						"server": {
							"title": "gRPC Server",
							"description": "Defines the gRPC server properties.\n\nSee: https://www.krakend.io/docs/enterprise/grpc/server/",
							"type": "object",
							"properties": {
								"opentelemetry": {
									"title": "OpenTelemetry settings",
									"description": "Overrides [OpenTelemetry settings](/docs/enterprise/telemetry/opentelemetry-layers-metrics/) for the gRPC server.",
									"type": "object",
									"properties": {
										"disable_metrics": {
											"title": "Disable metrics",
											"description": "Whether you want to disable all metrics happening in the gRPC server.\n\nSee: https://www.krakend.io/docs/enterprise/grpc/server/",
											"default": false,
											"type": "boolean"
										},
										"disable_traces": {
											"title": "Disable trace",
											"description": "Whether you want to disable all traces happening in the gRPC server.\n\nSee: https://www.krakend.io/docs/enterprise/grpc/server/",
											"default": false,
											"type": "boolean"
										}
									},
									"patternProperties": {
										"^[@$_#]": {}
									},
									"additionalProperties": false
								},
								"services": {
									"title": "gRPC services",
									"description": "Defines one object per available gRPC service.\n\nSee: https://www.krakend.io/docs/enterprise/grpc/server/",
									"type": "array",
									"items": {
										"type": "object",
										"properties": {
											"methods": {
												"title": "Methods",
												"description": "The gRPC methods available for this service (this is not related with HTTP methods despite using the same name).\n\nSee: https://www.krakend.io/docs/enterprise/grpc/server/",
												"type": "array",
												"items": {
													"type": "object",
													"properties": {
														"backend": {
															"title": "Backend",
															"description": "An array with all the [backend objects](https://www.krakend.io/docs/backends/) mapped to this method",
															"type": "array",
															"minItems": 1,
															"items": {
																"$ref": "#/definitions/https%3A~1~1www.krakend.io~1schema~1v2.7~1backend.json",
																"type": "object"
															}
														},
														"extra_config": {
															"title": "Extra configuration",
															"description": "Extra configuration for this method",
															"type": "object",
															"properties": {
																"auth/validator": {
																	"title": "JWT validation",
																	"description": "The JWT validation for this method. The configuration underneath is exactly as in the [JWT validator](https://www.krakend.io/docs/enterprise/authorization/jwt-validation/)",
																	"$ref": "#/definitions/https%3A~1~1www.krakend.io~1schema~1v2.7~1auth~1validator.json",
																	"type": "object"
																}
															},
															"patternProperties": {
																"^[@$_#]": {}
															},
															"additionalProperties": false
														},
														"input_headers": {
															"title": "Allowed Headers In",
															"description": "Defines the list of all client headers that you can use as gRPC metadata.\nBy default, KrakenD won't pass any header from the client to the backend. This list is **case-insensitive**. You can declare headers in lowercase, uppercase, or mixed.\nAn entry `[\"X-Something\"]` forwards a single `X-Something` header to the backend, ignoring everything else. A single star element `[\"*\"]` as value forwards everything to the backend (**it's safer to avoid this option**).",
															"examples": [
																"X-Custom-Trace",
																"*"
															],
															"default": [],
															"type": "array",
															"uniqueItems": true,
															"items": {
																"type": "string"
															}
														},
														"name": {
															"title": "Method name",
															"description": "The name of the published gRPC method.\n\nSee: https://www.krakend.io/docs/enterprise/grpc/server/",
															"examples": [
																"FindFlight"
															],
															"type": "string"
														},
														"payload_params": {
															"description": "Maps a property of the gRPC incoming payload to a `{parameter}` that you can inject and reuse in a `url_pattern`. It supports dot nation to access nested objects.\n\nSee: https://www.krakend.io/docs/enterprise/grpc/server/",
															"examples": [
																{
																	"some.grpc.object": "param1"
																}
															],
															"type": "object"
														}
													},
													"patternProperties": {
														"^[@$_#]": {}
													},
													"additionalProperties": false
												}
											},
											"name": {
												"title": "gRPC name",
												"description": "The name of the published gRPC service.\n\nSee: https://www.krakend.io/docs/enterprise/grpc/server/",
												"examples": [
													"flight_finder.Flights"
												],
												"type": "string"
											}
										},
										"patternProperties": {
											"^[@$_#]": {}
										},
										"additionalProperties": false
									}
								}
							},
							"patternProperties": {
								"^[@$_#]": {}
							},
							"additionalProperties": false
						}
					},
					"patternProperties": {
						"^[@$_#]": {}
					},
					"additionalProperties": false
				},
				"https://www.krakend.io/schema/v2.7/modifier/body-generator.json": {
					"$schema": "http://json-schema.org/draft-07/schema#",
					"title": "Template-based body generator",
					"description": "Enterprise only. Crafts the body/payload using a templating system.\n\nSee: https://www.krakend.io/backends/body-generator/",
					"type": "object",
					"oneOf": [
						{
							"required": [
								"path"
							]
						},
						{
							"required": [
								"template"
							]
						}
					],
					"minItems": 1,
					"properties": {
						"content_type": {
							"title": "Content-Type",
							"description": "The `Content-Type` you are generating in the template, so it can be recognized by whoever is using it.\n\nSee: https://www.krakend.io/docs/enterprise/backends/body-generator/",
							"examples": [
								"application/json",
								"application/xml",
								"text/xml"
							],
							"default": "application/json",
							"type": "string"
						},
						"debug": {
							"title": "Enable debug",
							"description": "When `true`, shows useful information in the logs with `DEBUG` level about the input received and the body generated. Do not enable in production. Debug logs are multiline and designed fore developer readibility, not machine processing.\n\nSee: https://www.krakend.io/docs/enterprise/backends/body-generator/",
							"default": false,
							"type": "boolean"
						},
						"path": {
							"title": "Path to template",
							"description": "The path to the Go template file you want to use to craft the body.\n\nSee: https://www.krakend.io/docs/enterprise/backends/body-generator/",
							"examples": [
								"./path/to.tmpl"
							],
							"type": "string"
						},
						"template": {
							"title": "Template",
							"description": "An inline base64 encoded Go template with the body you want to generate. This option is useful if you want to have the template embedded in the configuration instead of an external file.\n\nSee: https://www.krakend.io/docs/enterprise/backends/body-generator/",
							"type": "string"
						}
					},
					"patternProperties": {
						"^[@$_#]": {}
					},
					"additionalProperties": false,
					"@comment": "This schema is used by modifier/request-body-generator and modifier/body-generator simultaneously"
				},
				"https://www.krakend.io/schema/v2.7/modifier/jmespath.json": {
					"$schema": "http://json-schema.org/draft-07/schema#",
					"title": " JMESPath: Response manipulation with query language",
					"description": "The JMESPath query language allows you to select, slice, filter, map, project, flatten, sort, and all sorts of operations on data.\n\nSee: https://www.krakend.io/docs/enterprise/endpoints/jmespath/",
					"type": "object",
					"required": [
						"expr"
					],
					"properties": {
						"expr": {
							"title": "Expression",
							"description": "The JMESPath expression you want to apply to this endpoint.\n\nSee: https://www.krakend.io/docs/enterprise/endpoints/jmespath/",
							"type": "string"
						}
					},
					"patternProperties": {
						"^[@$_#]": {}
					},
					"additionalProperties": false
				},
				"https://www.krakend.io/schema/v2.7/modifier/lua.json": {
					"$schema": "http://json-schema.org/draft-07/schema#",
					"title": "Lua modifier",
					"description": "Scripting with Lua is an additional choice to extend your business logic, and is compatible with the rest of options such as CEL, Martian, or other Go plugins and middlewares.\n\nSee: https://www.krakend.io/docs/endpoints/lua/",
					"type": "object",
					"minItems": 1,
					"properties": {
						"allow_open_libs": {
							"title": "Open external libs",
							"description": "As an efficiency point the Lua component does not load the standard libraries by default. If you need to import Lua libraries (e.g, the I/O, String, etc.), then you must set this flag to true.\n\nSee: https://www.krakend.io/docs/endpoints/lua/",
							"default": false,
							"type": "boolean"
						},
						"live": {
							"title": "Live reload",
							"description": "For security and efficiency, the Lua script is loaded once into memory and not reloaded even if the file contents change. Set this flag to `true` if you want to modify the Lua script while KrakenD is running and apply the changes live (mostly during development to avoid the snippet being cached).\n\nSee: https://www.krakend.io/docs/endpoints/lua/",
							"default": false,
							"type": "boolean"
						},
						"md5": {
							"title": "MD5 Checksum",
							"description": "The md5sum is an extra security feature to make sure that once you have coded the Lua script, the MD5 of what is loaded into memory matches what you expect and has not been tampered by a malicious 3rd party. The key of the object must match **exactly** the filename under sources, **including all the path**.\n\nSee: https://www.krakend.io/docs/endpoints/lua/",
							"examples": [
								{
									"./path/to/file1.lua": "49ae50f58e35f4821ad4550e1a4d1de0"
								}
							],
							"type": "object"
						},
						"post": {
							"title": "post-execution code",
							"description": "The Lua code that is executed **after** performing the request. Available when used in the `backend` section. You can write all the Lua code inline (e.g., `print('Hi'); print('there!')` but you can also call functions that live inside one of the files under `sources` (e.g., `my_function()`).\n\nSee: https://www.krakend.io/docs/endpoints/lua/",
							"examples": [
								"local r = response.load(); r:headers('Set-Cookie', 'key1='.. r:data('response'));"
							],
							"type": "string"
						},
						"pre": {
							"title": "Pre-execution code",
							"description": "The Lua code that is executed **before** performing the request. Unlike `post`, it's available in all sections. You can write all the Lua code inline (e.g., `print('Hi'); print('there!')` but you can also call functions that live inside one of the files under `sources` (e.g., `my_function()`).\n\nSee: https://www.krakend.io/docs/endpoints/lua/",
							"examples": [
								"print('Backend response, pre-logic:'); local r = request.load(); print(r:body());"
							],
							"type": "string"
						},
						"skip_next": {
							"title": "Skip next",
							"description": "Available on the `backend` section only. Instead of connecting to next backend in the pipe, returns an empty response and executes the `post` lua function.\n\nSee: https://www.krakend.io/docs/endpoints/lua/",
							"default": false,
							"type": "boolean"
						},
						"sources": {
							"title": "Sources",
							"description": "An array with all the Lua files that will be processed. If no path is provided (e.g., `myfile.lua`) the file loads from the [working directory](https://www.krakend.io/docs/configuration/working-directory/).\n\nSee: https://www.krakend.io/docs/endpoints/lua/",
							"type": "array"
						}
					},
					"patternProperties": {
						"^[@$_#]": {}
					},
					"additionalProperties": false
				},
				"https://www.krakend.io/schema/v2.7/modifier/martian.json": {
					"$schema": "http://json-schema.org/draft-07/schema#",
					"title": "Martian modifiers",
					"description": "The Martian component allows you to modify requests and responses with static data through a simple DSL definition in the configuration file.\n\nSee: https://www.krakend.io/docs/endpoints/martian/",
					"type": "object",
					"minItems": 1,
					"maxProperties": 1,
					"properties": {
						"body.Modifier": {
							"title": "Body Modifier",
							"description": "The body.Modifier changes or sets the body of a request or response. The body must be uncompressed and Base64 encoded.\n\nSee: https://www.krakend.io/docs/backends/martian/",
							"type": "object",
							"required": [
								"scope",
								"body"
							],
							"properties": {
								"body": {
									"title": "Body in base64",
									"description": "The body you want to set, formatted in base64.",
									"type": "string"
								},
								"contentType": {
									"title": "Content Type",
									"description": "The content-type representing the body you are setting",
									"examples": [
										"application/x-www-form-urlencoded",
										"text/plain"
									],
									"type": "string"
								},
								"scope": {
									"title": "Scope",
									"description": "Scopes in which this modifier acts",
									"enum": [
										[
											"request",
											"response"
										],
										[
											"request"
										],
										[
											"response"
										]
									]
								}
							}
						},
						"cookie.Filter": {
							"title": "Cookie Filter",
							"description": "The cookie.Filter executes the contained modifier when a cookie is provided under the name.\n\nSee: https://www.krakend.io/docs/backends/martian/",
							"type": "object",
							"required": [
								"scope",
								"modifier",
								"name"
							],
							"properties": {
								"else": {
									"description": "The modifier you want to execute when the condition does not match",
									"$ref": "#/definitions/https%3A~1~1www.krakend.io~1schema~1v2.7~1modifier~1martian.json",
									"type": "object"
								},
								"modifier": {
									"description": "The modifier you want to execute when the cookie name (and value if provided) matches",
									"$ref": "#/definitions/https%3A~1~1www.krakend.io~1schema~1v2.7~1modifier~1martian.json",
									"type": "object"
								},
								"name": {
									"description": "The name of the Cookie you want to check. Notice that the `input_headers` must contain `Cookie` in the list when you want to check cookies sent by the client.",
									"type": "string"
								},
								"scope": {
									"title": "Scope",
									"description": "Scopes in which this modifier acts",
									"const": [
										"request"
									]
								},
								"value": {
									"description": "If besides the cookie name, you set this value, it ensures the cookie has a literal match."
								}
							}
						},
						"cookie.Modifier": {
							"title": "Cookie Modifier",
							"description": "Adds a cookie to a request or a response. If you set cookies in a response, the cookies are only set to the client when you use no-op encoding.\n\nSee: https://www.krakend.io/docs/backends/martian/",
							"type": "object",
							"required": [
								"scope",
								"name",
								"value"
							],
							"properties": {
								"domain": {
									"description": "Domain of the Cookie you want to set",
									"examples": [
										"example.com"
									],
									"type": "string"
								},
								"expires": {
									"description": "Date in RFC 3339 format and is absolute, not relative to the current time.",
									"examples": [
										"2025-04-12T23:20:50.52Z"
									],
									"type": "string"
								},
								"httpOnly": {
									"description": "Create the Cookie with the httpOnly flag. When `true`, mitigates the risk of client side script accessing the protected cookie (if the browser supports it), mitigating the Most Common XSS",
									"default": false,
									"type": "boolean"
								},
								"maxAge": {
									"description": "For how long this Cookie is valid, in seconds. `0` means that the attribute is not set. `maxAge<0` means delete cookie now",
									"default": 0,
									"type": "integer"
								},
								"name": {
									"description": "Name of the Cookie you want to set",
									"type": "string"
								},
								"path": {
									"description": "Path of the Cookie you want to set",
									"examples": [
										"/path/to"
									],
									"type": "string"
								},
								"scope": {
									"title": "Scope",
									"description": "Scopes in which this modifier acts",
									"enum": [
										[
											"request",
											"response"
										],
										[
											"request"
										],
										[
											"response"
										]
									]
								},
								"secure": {
									"description": "Cookie secure flag. When `true`, the user agent will include the cookie in the request when using https only",
									"default": false,
									"type": "boolean"
								},
								"value": {
									"description": "Value of the Cookie you want to set",
									"type": "string"
								}
							}
						},
						"fifo.Group": {
							"title": "FIFO group",
							"description": "The fifo.Group holds a list of modifiers executed in first-in, first-out order.\n\nSee: https://www.krakend.io/docs/backends/martian/",
							"type": "object",
							"required": [
								"scope",
								"modifiers"
							],
							"properties": {
								"aggregateErrors": {
									"description": "When true, the group will continue to execute consecutive modifiers when a modifier in the group encounters an error. The Group will then return all errors returned by each modifier after all modifiers have been executed.  When false, if an error is returned by a modifier, the error is returned by ModifyRequest/Response and no further modifiers are run.",
									"default": false,
									"type": "boolean"
								},
								"modifiers": {
									"description": "The list of modifiers you want to execute in the declared order",
									"type": "array",
									"items": {
										"$ref": "#/definitions/https%3A~1~1www.krakend.io~1schema~1v2.7~1modifier~1martian.json"
									}
								},
								"scope": {
									"title": "Scope",
									"description": "Scopes in which this modifier acts",
									"enum": [
										[
											"request",
											"response"
										],
										[
											"request"
										],
										[
											"response"
										]
									]
								}
							}
						},
						"header.Append": {
							"title": "Append a header",
							"description": "\n\nSee: https://www.krakend.io/docs/backends/martian/",
							"type": "object",
							"required": [
								"scope",
								"name",
								"value"
							],
							"properties": {
								"name": {
									"description": "Name of the header you want to append a value. Add the same name under the `input_headers` list to append more values to an existing header passed by the client. In addition, to see the header in the response, you must use `no-op`.",
									"type": "string"
								},
								"scope": {
									"title": "Scope",
									"description": "Scopes in which this modifier acts",
									"enum": [
										[
											"request",
											"response"
										],
										[
											"request"
										],
										[
											"response"
										]
									]
								},
								"value": {
									"description": "The value you want to add or append.",
									"type": "string"
								}
							}
						},
						"header.Blacklist": {
							"title": "Blacklist headers",
							"description": "The header.Blacklist removes the listed headers under names in the request and response of the backend.\n\nSee: https://www.krakend.io/docs/backends/martian/",
							"type": "object",
							"required": [
								"scope",
								"names"
							],
							"properties": {
								"names": {
									"description": "List of all the headers you want to supress from the request or the response. If you want to see the headers in the client, you must use the `output_encoding: no-op`, and if you want the client headers to propagate to the backend, you need to use `input_headers` too.",
									"type": "array",
									"items": {
										"type": "string"
									}
								},
								"scope": {
									"title": "Scope",
									"description": "Scopes in which this modifier acts",
									"enum": [
										[
											"request",
											"response"
										],
										[
											"request"
										],
										[
											"response"
										]
									]
								}
							}
						},
						"header.Copy": {
							"title": "Header Copy",
							"description": "The header.Copy lets you duplicate a header using another name\n\nSee: https://www.krakend.io/docs/backends/martian/",
							"type": "object",
							"required": [
								"scope",
								"from",
								"to"
							],
							"properties": {
								"from": {
									"description": "The origin header you want to copy. When the header is provided by the user it must be included in the `input_headers` list.",
									"type": "string"
								},
								"scope": {
									"title": "Scope",
									"description": "Scopes in which this modifier acts",
									"enum": [
										[
											"request",
											"response"
										],
										[
											"request"
										],
										[
											"response"
										]
									]
								},
								"to": {
									"description": "The destination header you want to create. If this header is returned to the end-user you must use `no-op` in the `output_encoding` of the endpoint.",
									"type": "string"
								}
							}
						},
						"header.Filter": {
							"title": "Header Filter",
							"description": "The header.Filter executes its contained modifier if the request or response contain a header that matches the defined name and value. The value is optional, and only the header’s existence evaluates when undefined.\n\nSee: https://www.krakend.io/docs/backends/martian/",
							"type": "object",
							"required": [
								"scope",
								"modifier",
								"name"
							],
							"properties": {
								"else": {
									"description": "The modifier you want to execute when the condition does not match",
									"$ref": "#/definitions/https%3A~1~1www.krakend.io~1schema~1v2.7~1modifier~1martian.json",
									"type": "object"
								},
								"modifier": {
									"description": "The modifier you want to execute when the condition matches",
									"$ref": "#/definitions/https%3A~1~1www.krakend.io~1schema~1v2.7~1modifier~1martian.json",
									"type": "object"
								},
								"name": {
									"description": "Name of the header you want to check. You must add under `input_headers` the `name` included in the filter.",
									"examples": [
										"X-Some",
										"Content-Type"
									],
									"type": "string"
								},
								"scope": {
									"title": "Scope",
									"description": "Scopes in which this modifier acts",
									"enum": [
										[
											"request",
											"response"
										],
										[
											"request"
										],
										[
											"response"
										]
									]
								},
								"value": {
									"description": "Value of the header you want to check",
									"type": "string"
								}
							}
						},
						"header.Id": {
							"title": "Header Id",
							"description": "\n\nSee: https://www.krakend.io/docs/backends/martian/",
							"type": "object",
							"required": [
								"scope"
							],
							"properties": {
								"scope": {
									"title": "Scope",
									"description": "Scopes in which this modifier acts",
									"const": [
										"request"
									]
								}
							}
						},
						"header.Modifier": {
							"title": "Header Modifier",
							"description": "The header.Modifier adds a new header or changes the value of an existing one.\n\nSee: https://www.krakend.io/docs/backends/martian/",
							"type": "object",
							"required": [
								"scope",
								"name",
								"value"
							],
							"properties": {
								"name": {
									"description": "Value of the header you want to set",
									"type": "string"
								},
								"scope": {
									"title": "Scope",
									"description": "Scopes in which this modifier acts",
									"enum": [
										[
											"request",
											"response"
										],
										[
											"request"
										],
										[
											"response"
										]
									]
								},
								"value": {
									"description": "Name of the header you want to set",
									"type": "string"
								}
							}
						},
						"header.RegexFilter": {
							"title": "Header RegexFilter",
							"description": "The header.RegexFilter checks that a regular expression (RE2 syntax) passes on the target header and, if it does, executes the modifier.\n\nSee: https://www.krakend.io/docs/backends/martian/",
							"type": "object",
							"required": [
								"scope",
								"modifier",
								"header",
								"regex"
							],
							"properties": {
								"header": {
									"description": "Name of the header you want to check. You must add under `input_headers` the `name` included in the filter.",
									"examples": [
										"X-Some",
										"Content-Type"
									],
									"type": "string"
								},
								"modifier": {
									"description": "The modifier you want to execute when the condition matches",
									"$ref": "#/definitions/https%3A~1~1www.krakend.io~1schema~1v2.7~1modifier~1martian.json",
									"type": "object"
								},
								"regex": {
									"description": "The regular expression you want to check against the header value",
									"examples": [
										".*localhost.*",
										"^foo-[a-z]+$"
									],
									"type": "string"
								},
								"scope": {
									"title": "Scope",
									"description": "Scopes in which this modifier acts",
									"enum": [
										[
											"request",
											"response"
										],
										[
											"request"
										],
										[
											"response"
										]
									]
								}
							}
						},
						"port.Filter": {
							"title": "Port Filter",
							"description": "The port.Filter executes its modifier only when the port matches the one used in the request. It does not support else.\n\nSee: https://www.krakend.io/docs/backends/martian/",
							"type": "object",
							"required": [
								"scope",
								"modifier",
								"port"
							],
							"properties": {
								"else": {
									"description": "The modifier you want to execute when the condition does not match",
									"$ref": "#/definitions/https%3A~1~1www.krakend.io~1schema~1v2.7~1modifier~1martian.json",
									"type": "object"
								},
								"modifier": {
									"description": "The modifier you want to execute when the condition matches",
									"$ref": "#/definitions/https%3A~1~1www.krakend.io~1schema~1v2.7~1modifier~1martian.json",
									"type": "object"
								},
								"port": {
									"description": "The port number you want to check",
									"type": "integer"
								},
								"scope": {
									"title": "Scope",
									"description": "Scopes in which this modifier acts",
									"const": [
										"request"
									]
								}
							}
						},
						"port.Modifier": {
							"title": "Port Modifier",
							"description": "The port.Modifier alters the request URL and Host header to use the provided port.\n\nSee: https://www.krakend.io/docs/backends/martian/",
							"type": "object",
							"oneOf": [
								{
									"required": [
										"scope",
										"port"
									]
								},
								{
									"required": [
										"scope",
										"defaultForScheme"
									]
								},
								{
									"required": [
										"scope",
										"remove"
									]
								}
							],
							"properties": {
								"defaultForScheme": {
									"description": "Uses the default port of the schema. `80` for `http://` or `443` for `https://`. Other schemas are ignored.",
									"type": "boolean"
								},
								"port": {
									"description": "Defines which port will be used.",
									"type": "integer"
								},
								"remove": {
									"description": "Removes the port from the host string when `true`.",
									"type": "boolean"
								},
								"scope": {
									"title": "Scope",
									"description": "Scopes in which this modifier acts",
									"const": [
										"request"
									]
								}
							}
						},
						"priority.Group": {
							"title": "Priority group",
							"description": "The priority.Group contains the modifiers you want to execute, but the order in which they are declared is unimportant. Instead, each modifier adds a priority attribute that defines the order in which they are run.\n\nSee: https://www.krakend.io/docs/backends/martian/",
							"type": "object",
							"required": [
								"scope",
								"modifiers"
							],
							"properties": {
								"modifiers": {
									"description": "The list of modifiers you want to execute, order specified in the items using `priority`.",
									"type": "array",
									"items": {
										"required": [
											"priority",
											"modifier"
										],
										"properties": {
											"modifier": {
												"description": "The modifier definition you want to execute",
												"$ref": "#/definitions/https%3A~1~1www.krakend.io~1schema~1v2.7~1modifier~1martian.json"
											},
											"priority": {
												"description": "The assigned priority number",
												"type": "integer"
											}
										}
									}
								},
								"scope": {
									"title": "Scope",
									"description": "Scopes in which this modifier acts",
									"enum": [
										[
											"request",
											"response"
										],
										[
											"request"
										],
										[
											"response"
										]
									]
								}
							}
						},
						"querystring.Filter": {
							"title": "QueryString Filter",
							"description": "The querystring.Filter executes the modifier if the request or response contains a query string parameter that matches the defined name and value in the filter.\n\nSee: https://www.krakend.io/docs/backends/martian/",
							"type": "object",
							"required": [
								"scope",
								"modifier",
								"name"
							],
							"properties": {
								"else": {
									"description": "The modifier you want to execute when the condition does not match",
									"$ref": "#/definitions/https%3A~1~1www.krakend.io~1schema~1v2.7~1modifier~1martian.json",
									"type": "object"
								},
								"modifier": {
									"description": "The modifier you want to execute when the condition matches",
									"$ref": "#/definitions/https%3A~1~1www.krakend.io~1schema~1v2.7~1modifier~1martian.json",
									"type": "object"
								},
								"name": {
									"description": "Name of the query string you want to check",
									"examples": [
										"page",
										"limit"
									],
									"type": "string"
								},
								"scope": {
									"title": "Scope",
									"description": "Scopes in which this modifier acts",
									"enum": [
										[
											"request",
											"response"
										],
										[
											"request"
										],
										[
											"response"
										]
									]
								},
								"value": {
									"description": "Value of the query string you want to check",
									"type": "string"
								}
							}
						},
						"querystring.Modifier": {
							"title": "Querystring Modifier",
							"description": "The querystring.Modifier adds a new query string or modifies existing ones in the request.\n\nSee: https://www.krakend.io/docs/backends/martian/",
							"type": "object",
							"required": [
								"scope",
								"name",
								"value"
							],
							"properties": {
								"name": {
									"description": "Name of the query string you want to set",
									"examples": [
										"page",
										"limit"
									],
									"type": "string"
								},
								"scope": {
									"title": "Scope",
									"description": "Scopes in which this modifier acts",
									"const": [
										"request"
									]
								},
								"value": {
									"description": "The value of the query string you want to set",
									"type": "string"
								}
							}
						},
						"stash.Modifier": {
							"title": "Stash Modifier",
							"description": "The stash.Modifier creates a new header (or replaces an existing one with a matching name) containing the value of the original URL and all its query string parameters.\n\nSee: https://www.krakend.io/docs/backends/martian/",
							"type": "object",
							"required": [
								"scope",
								"headerName"
							],
							"properties": {
								"headerName": {
									"description": "The header you want to create. If this header is returned to the end-user you must use `no-op` in the `output_encoding` of the endpoint.",
									"type": "string"
								},
								"scope": {
									"title": "Scope",
									"description": "Scopes in which this modifier acts",
									"enum": [
										[
											"request",
											"response"
										],
										[
											"request"
										],
										[
											"response"
										]
									]
								}
							}
						},
						"url.Filter": {
							"title": "URL Filter",
							"description": "The url.Filter executes its contained modifier if the request URL matches all of the provided parameters.\n\nSee: https://www.krakend.io/docs/backends/martian/",
							"type": "object",
							"required": [
								"scope",
								"modifier"
							],
							"properties": {
								"else": {
									"description": "The modifier you want to execute when the condition does not match",
									"$ref": "#/definitions/https%3A~1~1www.krakend.io~1schema~1v2.7~1modifier~1martian.json",
									"type": "object"
								},
								"host": {
									"description": "The literal hostname that must match, including the port",
									"examples": [
										"localhost:8080"
									],
									"type": "string"
								},
								"modifier": {
									"description": "The modifier you want to execute when the condition matches",
									"$ref": "#/definitions/https%3A~1~1www.krakend.io~1schema~1v2.7~1modifier~1martian.json",
									"type": "object"
								},
								"path": {
									"description": "The `/path` of the URL, without query strings.",
									"examples": [
										"/path/to"
									],
									"type": "string"
								},
								"query": {
									"description": "The query strings you want to check. Use `key1=value1&key2=value2` to check that the request has exactly these keys and values (order is irrelevant, but content not). Suppose the request has more query strings than declared here because the `input_query_strings` allowed them to pass. In that case, the evaluation will be `false`, and the `else` modifier will be executed.",
									"examples": [
										"/path/to"
									],
									"type": "string"
								},
								"scheme": {
									"description": "The literal scheme it must match",
									"examples": [
										"http",
										"https"
									],
									"type": "string"
								},
								"scope": {
									"title": "Scope",
									"description": "Scopes in which this modifier acts",
									"enum": [
										[
											"request",
											"response"
										],
										[
											"request"
										],
										[
											"response"
										]
									]
								}
							}
						},
						"url.Modifier": {
							"title": "URL Modifier",
							"description": "The url.Modifier allows you to change the URL despite what is set in the host and url_pattern combination.\n\nSee: https://www.krakend.io/docs/backends/martian/",
							"type": "object",
							"required": [
								"scope"
							],
							"properties": {
								"host": {
									"description": "The hostname part of the URL including the port",
									"examples": [
										"example.com",
										"localhost:8080"
									],
									"type": "string"
								},
								"path": {
									"description": "The path part of the URL",
									"examples": [
										"/path/to"
									],
									"type": "string"
								},
								"query": {
									"description": "Sets the query string parameters you want to pass, overwriting anything passed in the request. Notice that if you set a `query`, if the user passes other query string parameters listed under `input_query_strings`, they will be lost, and only the values passed in the modifier will be sent. For such uses, see the `querystring.Modifier`",
									"examples": [
										"param=1",
										"key1=val&key2=val"
									],
									"type": "string"
								},
								"scheme": {
									"description": "The scheme to apply",
									"examples": [
										"http",
										"https"
									],
									"type": "string"
								},
								"scope": {
									"title": "Scope",
									"description": "Scopes in which this modifier acts",
									"const": [
										"request"
									]
								}
							}
						},
						"url.RegexFilter": {
							"title": "URL RegexFilter",
							"description": "The url.RegexFilter evaluates a regular expression (RE2 syntax) and executes the modifier desired when it matches, and the modifier declared under else when it does not.\n\nSee: https://www.krakend.io/docs/backends/martian/",
							"type": "object",
							"required": [
								"scope",
								"modifier",
								"regex"
							],
							"properties": {
								"else": {
									"description": "The modifier you want to execute when the condition does not match",
									"$ref": "#/definitions/https%3A~1~1www.krakend.io~1schema~1v2.7~1modifier~1martian.json",
									"type": "object"
								},
								"modifier": {
									"description": "The modifier you want to execute when the condition matches",
									"$ref": "#/definitions/https%3A~1~1www.krakend.io~1schema~1v2.7~1modifier~1martian.json",
									"type": "object"
								},
								"regex": {
									"description": "The regular expression you want to check against the URL",
									"type": "string"
								},
								"scope": {
									"title": "Scope",
									"description": "Scopes in which this modifier acts",
									"const": [
										"request"
									]
								}
							}
						}
					},
					"additionalProperties": false
				},
				"https://www.krakend.io/schema/v2.7/modifier/response-headers.json": {
					"$schema": "http://json-schema.org/draft-07/schema#",
					"title": "Response headers modifier",
					"description": "Enterprise only. Allows you to transform response headers declaratively.\n\nSee: https://www.krakend.io/docs/enterprise/service-settings/response-headers-modifier/",
					"type": "object",
					"minProperties": 1,
					"properties": {
						"add": {
							"title": "Headers to add",
							"description": "The headers you want to add. Every key under `add` is the header name, and the values are declared in an array with all those you want to set. If the header didn't exist previously, it is created with the values you passed. If the header existed, then the new values are appended.\n\nSee: https://www.krakend.io/docs/enterprise/service-settings/response-headers-modifier/",
							"type": "object",
							"minProperties": 1,
							"patternProperties": {
								"(.+)": {
									"description": "Header name you want to add",
									"type": "array",
									"items": {
										"description": "Header value you want to add",
										"type": "string"
									}
								}
							}
						},
						"delete": {
							"title": "Headers to delete",
							"description": "The list of headers you want to delete. All headers listed will be missing in the response.\n\nSee: https://www.krakend.io/docs/enterprise/service-settings/response-headers-modifier/",
							"examples": [
								[
									"X-Krakend",
									"X-Krakend-Completed"
								]
							],
							"type": "array",
							"minItems": 1,
							"items": {
								"description": "Header you want to delete",
								"type": "string"
							}
						},
						"replace": {
							"title": "Headers to replace",
							"description": "The headers you want to replace. The key used under `replace` is the header name, and the value an array with all the header values you want to set. The replacement overwrites any other value that could exist in this header.\n\nSee: https://www.krakend.io/docs/enterprise/service-settings/response-headers-modifier/",
							"type": "object",
							"minProperties": 1,
							"patternProperties": {
								"(.+)": {
									"type": "array",
									"items": {
										"description": "Header value you want to replace",
										"type": "string"
									}
								}
							}
						}
					},
					"patternProperties": {
						"^[@$_#]": {}
					},
					"additionalProperties": false
				},
				"https://www.krakend.io/schema/v2.7/plugin/content-replacer.json": {
					"$schema": "http://json-schema.org/draft-07/schema#",
					"title": "Content Replacer",
					"description": "Enterprise only. The content replacer plugin allows you to modify the response of your services by doing literal replacements or more sophisticated replacements with regular expressions.\n\nSee: See: https://www.krakend.io/docs/enterprise/endpoints/content-replacer/",
					"type": "object",
					"minProperties": 1,
					"properties": {},
					"additionalProperties": {
						"type": "object",
						"required": [
							"find",
							"replace"
						],
						"properties": {
							"find": {
								"title": "Find",
								"description": "The find expression or literal you want to use.\n\nSee: https://www.krakend.io/docs/enterprise/endpoints/content-replacer/",
								"type": "string"
							},
							"regexp": {
								"title": "Is a regexp?",
								"description": "When you are passing regular expressions instead of literal values, set it to true.\n\nSee: https://www.krakend.io/docs/enterprise/endpoints/content-replacer/",
								"default": false,
								"type": "boolean"
							},
							"replace": {
								"title": "Find",
								"description": "The literal string or expression you want to use as a replacement.\n\nSee: https://www.krakend.io/docs/enterprise/endpoints/content-replacer/",
								"type": "string"
							}
						},
						"patternProperties": {
							"^[@$_#]": {}
						},
						"additionalProperties": false
					}
				},
				"https://www.krakend.io/schema/v2.7/plugin/geoip.json": {
					"$schema": "http://json-schema.org/draft-07/schema#",
					"title": "GeoIP",
					"description": "Enterprise only. The GeoIP integration allows you load Maxmind's GeoIP2 City database (payment and free versions) and enrich all KrakenD calls to your backends with geo data.\n\nSee: https://www.krakend.io/docs/enterprise/endpoints/geoip/",
					"type": "object",
					"required": [
						"citydb_path"
					],
					"properties": {
						"citydb_path": {
							"title": "CityDB path",
							"description": "The path in the filesystem containing the database in GeoIP2 Binary (`.mmdb`) format. Relative to the working dir or absolute path.\n\nSee: https://www.krakend.io/docs/enterprise/endpoints/geoip/",
							"examples": [
								"path/to/GeoIP2-City.mmdb"
							],
							"type": "string"
						}
					},
					"patternProperties": {
						"^[@$_#]": {}
					},
					"additionalProperties": false
				},
				"https://www.krakend.io/schema/v2.7/plugin/http-client.json": {
					"$schema": "http://json-schema.org/draft-07/schema#",
					"title": "HTTP client plugins.\n\nSee: https://www.krakend.io/docs/extending/injecting-plugins/",
					"type": "object",
					"properties": {
						"name": {
							"title": "Plugin name",
							"description": "The name of the plugin to load. Only one plugin is supported per backend.\n\nSee: https://www.krakend.io/docs/extending/injecting-plugins/",
							"examples": [
								"no-redirect",
								"http-logger",
								"static-filesystem"
							],
							"type": "string"
						}
					},
					"additionalProperties": true
				},
				"https://www.krakend.io/schema/v2.7/plugin/http-server.json": {
					"$schema": "http://json-schema.org/draft-07/schema#",
					"title": "HTTP Server plugins.\n\nSee: https://www.krakend.io/docs/extending/http-server-plugins/",
					"type": "object",
					"required": [
						"name"
					],
					"properties": {
						"geoip": {
							"$ref": "#/definitions/https%3A~1~1www.krakend.io~1schema~1v2.7~1plugin~1geoip.json"
						},
						"ip-filter": {
							"$ref": "#/definitions/https%3A~1~1www.krakend.io~1schema~1v2.7~1plugin~1ip-filter.json"
						},
						"jwk-aggregator": {
							"$ref": "#/definitions/https%3A~1~1www.krakend.io~1schema~1v2.7~1plugin~1jwk-aggregator.json"
						},
						"name": {
							"title": "Plugin name",
							"description": "An array with the names of plugins to load. The names are defined inside your plugin.\n\nSee: https://www.krakend.io/docs/extending/http-server-plugins/",
							"examples": [
								"myplugin"
							],
							"default": [],
							"type": "array"
						},
						"redis-ratelimit": {
							"$ref": "#/definitions/https%3A~1~1www.krakend.io~1schema~1v2.7~1plugin~1redis-ratelimit.json"
						},
						"static-filesystem": {
							"$ref": "#/definitions/https%3A~1~1www.krakend.io~1schema~1v2.7~1plugin~1static-filesystem.json"
						},
						"url-rewrite": {
							"$ref": "#/definitions/https%3A~1~1www.krakend.io~1schema~1v2.7~1plugin~1url-rewrite.json"
						},
						"virtualhost": {
							"$ref": "#/definitions/https%3A~1~1www.krakend.io~1schema~1v2.7~1plugin~1virtualhost.json"
						},
						"wildcard": {
							"$ref": "#/definitions/https%3A~1~1www.krakend.io~1schema~1v2.7~1plugin~1wildcard.json"
						}
					},
					"additionalProperties": true
				},
				"https://www.krakend.io/schema/v2.7/plugin/ip-filter.json": {
					"$schema": "http://json-schema.org/draft-07/schema#",
					"title": "IP filter",
					"description": "Enterprise only. The IP filtering plugin allows you to restrict the traffic to your API gateway based on the IP address. It works in two different modes (allow or deny) where you define the list of IPs (CIDR blocks) that are authorized to use the API, or that are denied from using the API.\n\nSee: https://www.krakend.io/docs/enterprise/throttling/ipfilter/",
					"type": "object",
					"required": [
						"CIDR",
						"allow"
					],
					"properties": {
						"CIDR": {
							"title": "CIDR",
							"description": "The CIDR blocks (list of IPs) you want to allow or deny.\n\nSee: https://www.krakend.io/docs/enterprise/throttling/ipfilter/",
							"examples": [
								[
									"192.168.0.0/24",
									"172.17.2.56/32"
								]
							],
							"type": "array"
						},
						"allow": {
							"title": "Allow or deny mode",
							"description": "When true, only the matching IPs are able to access the content. When false, all matching IPs are discarded.\n\nSee: https://www.krakend.io/docs/enterprise/throttling/ipfilter/",
							"default": false,
							"type": "boolean"
						},
						"client_ip_headers": {
							"title": "Client IP Headers",
							"description": "A custom list of all headers that might contain the real IP of the client. The first matching IP in the list will be used. Default headers are (in order of checking): X-Forwarded-For, X-Real-IP, and X-Appengine-Remote-Addr.\n\nSee: https://www.krakend.io/docs/enterprise/throttling/ipfilter/",
							"examples": [
								[
									"X-Forwarded-For",
									"X-Real-IP",
									"X-Appengine-Remote-Addr"
								]
							],
							"type": "array"
						},
						"trusted_proxies": {
							"title": "Trusted proxies",
							"description": "A custom list of all the recognized machines/balancers that proxy the client to your application. This list is used to avoid spoofing when trying to get the real IP of the client.\n\nSee: https://www.krakend.io/docs/enterprise/throttling/ipfilter/",
							"examples": [
								[
									"10.0.0.0/16"
								]
							],
							"type": "array"
						}
					},
					"patternProperties": {
						"^[@$_#]": {}
					},
					"additionalProperties": false
				},
				"https://www.krakend.io/schema/v2.7/plugin/jwk-aggregator.json": {
					"$schema": "http://json-schema.org/draft-07/schema#",
					"title": "JWK aggregator",
					"description": "Enterprise only. The JWK aggregator plugin allows KrakenD to validate tokens issued by multiple Identity Providers.\n\nSee: https://www.krakend.io/docs/enterprise/authentication/multiple-identity-providers/",
					"type": "object",
					"required": [
						"port",
						"origins"
					],
					"properties": {
						"cache": {
							"title": "Cache",
							"description": "When `true`, it stores the response of the Identity provider for the time specified in its `Cache-Control` header.\n\nSee: https://www.krakend.io/docs/enterprise/authentication/multiple-identity-providers/",
							"type": "boolean"
						},
						"origins": {
							"title": "Origins",
							"description": "The list of all JWK URLs recognized as valid Identity Providers by the gateway.\n\nSee: https://www.krakend.io/docs/enterprise/authentication/multiple-identity-providers/",
							"type": "array"
						},
						"port": {
							"title": "Port",
							"description": "The port of the local server doing the aggregation. The port is only accessible within the gateway machine using localhost, and it's never exposed to the external network. Choose any port that is free in the system.\n\nSee: https://www.krakend.io/docs/enterprise/authentication/multiple-identity-providers/",
							"examples": [
								9876
							],
							"type": "integer"
						}
					},
					"patternProperties": {
						"^[@$_#]": {}
					},
					"additionalProperties": false
				},
				"https://www.krakend.io/schema/v2.7/plugin/redis-ratelimit.json": {
					"$schema": "http://json-schema.org/draft-07/schema#",
					"title": "Redis ratelimit",
					"description": "Enterprise only. The global rate limit functionality enables a Redis database store to centralize all KrakenD node counters. Instead of having each KrakenD node count its hits, the counters are global and stored in the database.\n\nSee: https://www.krakend.io/docs/enterprise/endpoints/global-rate-limit/",
					"type": "object",
					"required": [
						"host",
						"tokenizer",
						"burst",
						"rate",
						"period"
					],
					"properties": {
						"burst": {
							"title": "Burst",
							"description": "How many requests a client can make above the rate specified during a peak.\n\nSee: https://www.krakend.io/docs/enterprise/endpoints/global-rate-limit/",
							"type": "integer"
						},
						"host": {
							"title": "Redis host",
							"description": "The URL to the Redis instance that stores the counters using the format `host:port`.\n\nSee: https://www.krakend.io/docs/enterprise/endpoints/global-rate-limit/",
							"examples": [
								"redis",
								"redis:6379"
							],
							"type": "string"
						},
						"period": {
							"title": "Period",
							"description": "For how long the content lives in the cache. Usually in seconds, minutes, or hours. E.g., use `120s` or `2m` for two minutes\n\nSee: https://www.krakend.io/docs/enterprise/endpoints/global-rate-limit/",
							"$ref": "#/definitions/https%3A~1~1www.krakend.io~1schema~1v2.7~1timeunits.json/$defs/timeunit",
							"type": "string"
						},
						"rate": {
							"title": "Rate",
							"description": "Number of allowed requests during the observed period.\n\nSee: https://www.krakend.io/docs/enterprise/endpoints/global-rate-limit/",
							"type": "integer"
						},
						"tokenizer": {
							"title": "Tokenizer",
							"description": "One of the preselected strategies to rate-limit users.\n\nSee: https://www.krakend.io/docs/enterprise/endpoints/global-rate-limit/",
							"enum": [
								"jwt",
								"ip",
								"url",
								"path",
								"header",
								"param",
								"cookie"
							]
						},
						"tokenizer_field": {
							"title": "Tokenizer field",
							"description": "The field used to set a custom field for the tokenizer (e.g., extracting the token from a custom header other than Authorization or using a claim from a JWT other than the jti).\n\nSee: https://www.krakend.io/docs/enterprise/endpoints/global-rate-limit/",
							"type": "string"
						}
					},
					"patternProperties": {
						"^[@$_#]": {}
					},
					"additionalProperties": false
				},
				"https://www.krakend.io/schema/v2.7/plugin/req-resp-modifier.json": {
					"$schema": "http://json-schema.org/draft-07/schema#",
					"title": "Request-Response modifier plugins",
					"type": "object",
					"properties": {
						"content-replacer": {
							"$ref": "#/definitions/https%3A~1~1www.krakend.io~1schema~1v2.7~1plugin~1content-replacer.json"
						},
						"ip-filter": {
							"$ref": "#/definitions/https%3A~1~1www.krakend.io~1schema~1v2.7~1plugin~1ip-filter.json"
						},
						"name": {
							"title": "Plugin name",
							"description": "An array with the names of plugins to load. The names are defined inside your plugin.\n\nSee: https://www.krakend.io/docs/extending/plugin-modifiers/",
							"examples": [
								"myplugin"
							],
							"default": [],
							"type": "array"
						},
						"response-schema-validator": {
							"$ref": "#/definitions/https%3A~1~1www.krakend.io~1schema~1v2.7~1plugin~1response-schema-validator.json"
						}
					},
					"additionalProperties": true
				},
				"https://www.krakend.io/schema/v2.7/plugin/response-schema-validator.json": {
					"$schema": "http://json-schema.org/draft-07/schema#",
					"title": "Response Schema Validator",
					"description": "Enterprise only. The response schema validator plugin adds a schema validation before the gateway returns the response to the end-user or before it’s merged in the endpoint with the rest of the backends.\n\nSee: https://www.krakend.io/docs/enterprise/endpoints/response-schema-validator/",
					"type": "object",
					"required": [
						"schema"
					],
					"properties": {
						"error": {
							"title": "Error definition",
							"description": "In case the validation fails, the error definition containing body and status.\n\nSee: https://www.krakend.io/docs/enterprise/endpoints/response-schema-validator/",
							"examples": [
								{
									"body": "We couldn't process you request, try again later.",
									"status": 401
								}
							],
							"type": "object",
							"properties": {
								"body": {
									"title": "Error body",
									"description": "The error message you want to show when the validation fails. Set it to an empty string `\"\"` to show the JSON-schema validation error.",
									"default": "",
									"type": "string"
								},
								"status": {
									"title": "Error code",
									"description": "The HTTP status code you want to set back in the response.",
									"default": 500,
									"type": "integer"
								}
							},
							"patternProperties": {
								"^[@$_#]": {}
							},
							"additionalProperties": false
						},
						"schema": {
							"title": "JSON Schema",
							"description": "Write your JSON schema directly in this field, with any number of fields or validations you need.\n\nSee: https://www.krakend.io/docs/enterprise/endpoints/response-schema-validator/",
							"type": "object"
						}
					},
					"patternProperties": {
						"^[@$_#]": {}
					},
					"additionalProperties": false
				},
				"https://www.krakend.io/schema/v2.7/plugin/static-filesystem.json": {
					"$schema": "http://json-schema.org/draft-07/schema#",
					"title": "Static Filesystem",
					"description": "Enterprise only. Allows you to fetch and serve static content in two different use cases. When the plugin is used as an http server handler, the static content is for your end-users, giving them CSS, JS, images, or JSON files, to name a few examples. On the other side, when the plugin is used as an http client executor, the KrakenD endpoints use static content as if it were a backend.\n\nSee: https://www.krakend.io/docs/enterprise/endpoints/serve-static-content/",
					"deprecated": true,
					"type": "object",
					"required": [
						"prefix",
						"path"
					],
					"properties": {
						"path": {
							"title": "Path",
							"description": "The folder in the filesystem containing the static files. Relative to the working dir where KrakenD config is (e.g.: `./assets`) or absolute (e.g.: `/var/www/assets`).\n\nSee: https://www.krakend.io/docs/enterprise/endpoints/serve-static-content/",
							"examples": [
								"./static/"
							],
							"type": "string"
						},
						"prefix": {
							"title": "Prefix",
							"description": "This is the beginning (prefix) of all URLs that are resolved using this plugin. All matching URLs won't be passed to the router, meaning that they are not considered endpoints. Make sure you are not overwriting valid endpoints. When the `prefix` is `/`, then **all traffic is served as static** and you must declare a prefix under `skip` (e.g.: `/api`) to match endpoints.\n\nSee: https://www.krakend.io/docs/enterprise/endpoints/serve-static-content/",
							"examples": [
								"/media/assets"
							],
							"type": "string"
						},
						"skip": {
							"title": "Skip paths",
							"description": "An array with all the prefix URLs that despite they could match with the `prefix`, you don't want to treat them as static content and pass them to the router.\n\nSee: https://www.krakend.io/docs/enterprise/endpoints/serve-static-content/",
							"examples": [
								[
									"/media/ignore/this/directory",
									"/media/file.json"
								]
							],
							"type": "array"
						}
					},
					"patternProperties": {
						"^[@$_#]": {}
					},
					"additionalProperties": false
				},
				"https://www.krakend.io/schema/v2.7/plugin/url-rewrite.json": {
					"$schema": "http://json-schema.org/draft-07/schema#",
					"title": "URL rewrite",
					"description": "Enterprise only. Allows you to declare additional URLs other than the ones defined under the endpoints configuration, used as aliases of existing endpoints.\n\nSee: https://www.krakend.io/docs/enterprise/endpoints/url-rewrite/",
					"type": "object",
					"anyOf": [
						{
							"required": [
								"literal"
							]
						},
						{
							"required": [
								"regexp"
							]
						}
					],
					"properties": {
						"literal": {
							"title": "Literal match",
							"description": "A map with the exact desired url and its mapping to an endpoint. If the endpoint has `{placeholders}` you need to write them, but the literal value `{placeholders}` is passed.\n\nSee: https://www.krakend.io/docs/enterprise/endpoints/url-rewrite/",
							"examples": [
								{
									"/hi-there": "/hello",
									"/whatsup": "/hello"
								}
							],
							"type": "object"
						},
						"regexp": {
							"title": "Regexp match",
							"description": "A list of lists, containing the regular expression that defines the URL to be rewritten, and its endpoint destination. You can use the capturing groups with the syntax `${1}`, `${2}`, etc.\n\nSee: https://www.krakend.io/docs/enterprise/endpoints/url-rewrite/",
							"type": "array",
							"items": {
								"type": "array"
							}
						}
					},
					"patternProperties": {
						"^[@$_#]": {}
					},
					"additionalProperties": false
				},
				"https://www.krakend.io/schema/v2.7/plugin/virtualhost.json": {
					"$schema": "http://json-schema.org/draft-07/schema#",
					"title": "VirtualHost",
					"description": "Enterprise only. The Virtual Host plugin allows you to run different configurations of KrakenD endpoints based on the host accessing the server.\n\nSee: https://www.krakend.io/docs/enterprise/service-settings/virtual-hosts/",
					"deprecated": true,
					"type": "object",
					"required": [
						"hosts"
					],
					"properties": {
						"hosts": {
							"title": "Virtualhosts",
							"description": "All recognized virtual hosts by KrakenD must be listed here. The values declared here must match the content of the `Host` header when passed by the client.\n\nSee: https://www.krakend.io/docs/enterprise/service-settings/virtual-hosts/",
							"examples": [
								[
									"api-a.host.com",
									"api-b.host.com"
								]
							],
							"type": "array",
							"items": {
								"type": "string"
							}
						}
					},
					"patternProperties": {
						"^[@$_#]": {}
					},
					"additionalProperties": false
				},
				"https://www.krakend.io/schema/v2.7/plugin/wildcard.json": {
					"$schema": "http://json-schema.org/draft-07/schema#",
					"title": "Wildcard",
					"description": "Enterprise only. Enables wildcard processing of requests without declaring all endpoint subresrouces.\n\nSee: https://www.krakend.io/docs/enterprise/endpoints/wildcard/",
					"type": "object",
					"required": [
						"endpoints"
					],
					"properties": {
						"endpoints": {
							"title": "Endpoints",
							"description": "The key of the map is the KrakenD endpoint that receives all the wildcard traffic. The value is an array with all the user paths that match this wildcard (you don't need to declare the subresources).\n\nSee: https://www.krakend.io/docs/enterprise/endpoints/wildcard/",
							"examples": [
								{
									"/__wildcard/foo": [
										"/foo",
										"/aliasfoo"
									]
								}
							],
							"type": "object"
						}
					},
					"patternProperties": {
						"^[@$_#]": {}
					},
					"additionalProperties": false
				},
				"https://www.krakend.io/schema/v2.7/proxy.json": {
					"$schema": "http://json-schema.org/draft-07/schema#",
					"title": "Proxy option",
					"type": "object",
					"properties": {
						"decompress_gzip": {
							"title": "Decompress Gzip",
							"description": "Enterprise Only. Decompresses any Gzipped content before sending it to the backend when the `Content-Encoding` has `gzip` in the first position. You can also set this value globally at the [service level](/docs/enterprise/service-settings/router-options/#max_payload).\n\nSee: https://www.krakend.io/docs/service-settings/router-options/",
							"default": false,
							"type": "boolean"
						},
						"flatmap_filter": {
							"title": "Flatmap (Array manipulation)",
							"description": "The list of operations to **execute sequentially** (top down). Every operation is defined with an object containing two properties:\n\nSee: https://www.krakend.io/docs/backends/flatmap/",
							"$ref": "#/definitions/https%3A~1~1www.krakend.io~1schema~1v2.7~1proxy~1flatmap.json",
							"type": "array"
						},
						"max_payload": {
							"title": "Maximum Payload",
							"description": "Enterprise Only. Limits the maximum number of bytes a user can send to the endpoint. `0` means no limit. You can also set this value globally at the [service level](/docs/enterprise/service-settings/router-options/#max_payload).\n\nSee: https://www.krakend.io/docs/service-settings/router-options/",
							"default": 0,
							"type": "integer"
						},
						"shadow": {
							"title": "Traffic shadowing or mirroring",
							"description": "Mark this backend as a shadow backend. Sending copies of the traffic but ignore its responses.\n\nSee: https://www.krakend.io/docs/backends/shadow-backends/",
							"default": true,
							"type": "boolean"
						}
					},
					"patternProperties": {
						"^[@$_#]": {}
					},
					"additionalProperties": false
				},
				"https://www.krakend.io/schema/v2.7/proxy/flatmap.json": {
					"$schema": "http://json-schema.org/draft-07/schema#",
					"title": "Flatmap (Array manipulation)",
					"description": "The flatmap middleware allows you to manipulate collections (or arrays, or lists, you name it) from the backend response. While the basic manipulation operations allow you to work directly with objects, the collections require a different approach: the flatmap component.\n\nSee: https://www.krakend.io/docs/backend/flatmap/",
					"examples": [
						{
							"type": "move",
							"args": [
								"a.*.b1.*.c",
								"a.*.b1.*.d"
							]
						}
					],
					"type": "array",
					"items": {
						"title": "Flatmap operation",
						"type": "object",
						"required": [
							"type",
							"args"
						],
						"properties": {
							"type": {
								"title": "Type",
								"description": "The types of operations are defined as follows.\n\nSee: https://www.krakend.io/docs/backends/flatmap/",
								"enum": [
									"move",
									"del",
									"append"
								]
							},
							"args": {
								"title": "Args",
								"description": "The arguments passed to the operation.\n\nSee: https://www.krakend.io/docs/backends/flatmap/",
								"type": "array"
							}
						},
						"patternProperties": {
							"^[@$_#]": {}
						},
						"additionalProperties": false
					}
				},
				"https://www.krakend.io/schema/v2.7/qos/circuit-breaker.json": {
					"$schema": "http://json-schema.org/draft-07/schema#",
					"title": "Circuit Breaker",
					"description": "The circuit breaker prevents sending more traffic to a failing backend.\n\nSee: https://www.krakend.io/docs/backends/circuit-breaker/",
					"type": "object",
					"required": [
						"interval",
						"timeout",
						"max_errors"
					],
					"properties": {
						"interval": {
							"title": "Interval",
							"description": "Time window where the errors count, in seconds.\n\nSee: https://www.krakend.io/docs/backends/circuit-breaker/",
							"type": "integer"
						},
						"log_status_change": {
							"title": "Log status change",
							"description": "Whether to log the changes of state of this circuit breaker or not.\n\nSee: https://www.krakend.io/docs/backends/circuit-breaker/",
							"default": false,
							"type": "boolean"
						},
						"max_errors": {
							"title": "Max Errors",
							"description": "The **CONSECUTIVE** (not total) number of errors within the `interval` window to consider the backend unhealthy. All HTTP status codes different than `20x` are considered an error, except for the `no-op` encoding that does not evaluate status codes and is limited to connectivity/networking, security and component errors. See the definition of error below.\n\nSee: https://www.krakend.io/docs/backends/circuit-breaker/",
							"type": "integer"
						},
						"name": {
							"title": "Name",
							"description": "A friendly name to follow this circuit breaker's activity in the logs.\n\nSee: https://www.krakend.io/docs/backends/circuit-breaker/",
							"examples": [
								"cb-backend-1"
							],
							"type": "string"
						},
						"timeout": {
							"title": "Timeout",
							"description": "For how many seconds the circuit breaker will wait before testing again if the backend is healthy.\n\nSee: https://www.krakend.io/docs/backends/circuit-breaker/",
							"type": "integer"
						}
					},
					"patternProperties": {
						"^[@$_#]": {}
					},
					"additionalProperties": false
				},
				"https://www.krakend.io/schema/v2.7/qos/ratelimit/proxy.json": {
					"$schema": "http://json-schema.org/draft-07/schema#",
					"title": "Backend Ratelimit",
					"description": "Restrict the rate of requests KrakenD makes to your backends.\n\nSee: https://www.krakend.io/docs/backends/rate-limit/",
					"type": "object",
					"required": [
						"max_rate",
						"capacity"
					],
					"properties": {
						"capacity": {
							"title": "Capacity",
							"description": "The capacity according to the [token bucket algorithm](https://www.krakend.io/docs/throttling/token-bucket/). Defines the maximum requests you can do in an instant (including the zero moment when you start the gateway), and can be larger or smaller than the `max_rate`. When unsure, use the same value of `max_rate`, so the maximum number of requests can be consumed at once.\n\nSee: https://www.krakend.io/docs/backends/rate-limit/",
							"default": 1,
							"type": "integer"
						},
						"every": {
							"title": "Time window",
							"description": "Time period in which the counter works. For instance, if you set an `every` of `10m` and a `max_rate` of `5`, you are allowing 5 requests every ten minutes.\n\nSee: https://www.krakend.io/docs/endpoints/rate-limit/",
							"default": "1s",
							"$ref": "#/definitions/https%3A~1~1www.krakend.io~1schema~1v2.7~1timeunits.json/$defs/timeunit",
							"type": "string"
						},
						"max_rate": {
							"title": "Max rate",
							"description": "Maximum requests per second you want to accept in this backend.\n\nSee: https://www.krakend.io/docs/backends/rate-limit/",
							"examples": [
								0.5
							],
							"type": "number"
						}
					},
					"patternProperties": {
						"^[@$_#]": {}
					},
					"additionalProperties": false
				},
				"https://www.krakend.io/schema/v2.7/qos/ratelimit/router.json": {
					"$schema": "http://json-schema.org/draft-07/schema#",
					"title": "Endpoint ratelimit",
					"type": "object",
					"anyOf": [
						{
							"required": [
								"max_rate"
							]
						},
						{
							"required": [
								"client_max_rate"
							]
						}
					],
					"properties": {
						"capacity": {
							"title": "Capacity",
							"description": "Defines the maximum number of [tokens a bucket can hold](https://www.krakend.io/docs/throttling/token-bucket/), or said otherwise, how many requests will you accept from **all users together** at **any given instant**. When the gateway starts, the bucket is full. As requests from users come, the remaining tokens in the bucket decrease. At the same time, the `max_rate` refills the bucket at the desired rate until its maximum capacity is reached. The default value for the `capacity` is the `max_rate` value expressed in seconds or 1 for smaller fractions. When unsure, use the same number as `max_rate`.\n\nSee: https://www.krakend.io/docs/endpoints/rate-limit/",
							"default": 1,
							"type": "integer"
						},
						"cleanup_period": {
							"title": "Cleanup Period",
							"description": "The cleanup period is how often the routine(s) in charge of optimizing the memory dedicated will go iterate all counters looking for outdated TTL and remove them. A low value keeps the memory slightly decreasing, but as a trade-off, it will increase the CPU dedicated to achieving this optimization. This is an advanced micro-optimization setting that should be used with caution.\n\nSee: https://www.krakend.io/docs/endpoints/rate-limit/",
							"default": "1m",
							"$ref": "#/definitions/https%3A~1~1www.krakend.io~1schema~1v2.7~1timeunits.json/$defs/timeunit",
							"type": "string"
						},
						"cleanup_threads": {
							"title": "Cleanup Threads",
							"description": "These are the number of routines that search for and remove outdated rate limit counters. The more routine(s) you add, the faster the memory optimization is completed, but the more CPU it will consume. Generally speaking, a single thread is more than enough because the delete operation is very fast, even with a large number of counters. This is an advanced micro-optimization setting that you should use with caution.\n\nSee: https://www.krakend.io/docs/endpoints/rate-limit/",
							"default": 1,
							"type": "integer"
						},
						"client_capacity": {
							"title": "Client capacity",
							"description": "Defines the maximum number of [tokens a bucket can hold](https://www.krakend.io/docs/throttling/token-bucket/), or said otherwise, how many requests will you accept from **each individual user** at **any given instant**. Works just as `capacity`, but instead of having one bucket for all users, keeps a counter for every connected client and endpoint, and refills from `client_max_rate` instead of `max_rate`. The client is recognized using the `strategy` field (an IP address, a token, a header, etc.). The default value for the `client_capacity` is the `client_max_rate` value expressed in seconds or 1 for smaller fractions. When unsure, use the same number as `client_max_rate`.\n\nSee: https://www.krakend.io/docs/endpoints/rate-limit/",
							"default": 1,
							"type": "integer"
						},
						"client_max_rate": {
							"title": "Client max rate",
							"description": "Number of tokens you add to the [Token Bucket](https://www.krakend.io/docs/throttling/token-bucket/) for each individual user (*user quota*) in the time interval you want (`every`). The remaining tokens in the bucket are the requests a specific user can do. It keeps a counter for every client and endpoint. Keep in mind that every KrakenD instance keeps its counters in memory for **every single client**.\n\nSee: https://www.krakend.io/docs/endpoints/rate-limit/",
							"type": "number"
						},
						"every": {
							"title": "Time window",
							"description": "Time period in which the maximum rates operate. For instance, if you set an `every` of `10m` and a rate of `5`, you are allowing 5 requests every ten minutes.\n\nSee: https://www.krakend.io/docs/endpoints/rate-limit/",
							"default": "1s",
							"$ref": "#/definitions/https%3A~1~1www.krakend.io~1schema~1v2.7~1timeunits.json/$defs/timeunit",
							"type": "string"
						},
						"key": {
							"title": "Header or Param key",
							"description": "Available when using `client_max_rate` and you have set a `strategy` equal to `header` or `param`. It makes no sense in other contexts. For `header` it is the header name containing the user identification (e.g., `Authorization` on tokens, or `X-Original-Forwarded-For` for IPs). When they contain a list of space-separated IPs, it will take the IP from the client that hit the first trusted proxy. For `param` it is the name of the placeholder used in the endpoint, like `id_user` for an endpoint `/user/{id_user}`.\n\nSee: https://www.krakend.io/docs/endpoints/rate-limit/",
							"examples": [
								"X-Tenant",
								"Authorization",
								"id_user"
							],
							"type": "string"
						},
						"max_rate": {
							"title": "Max rate",
							"description": "Sets the maximum number of requests all users can do in the given time frame. Internally uses the [Token Bucket](https://www.krakend.io/docs/throttling/token-bucket/) algorithm. The absence of `max_rate` in the configuration or a `0` is the equivalent to no limitation. You can use decimals if needed.\n\nSee: https://www.krakend.io/docs/endpoints/rate-limit/",
							"type": "number"
						},
						"num_shards": {
							"title": "Num Shards",
							"description": "All rate limit counters are stored in memory in groups (shards). All counters in the same shard share a mutex (which controls that one counter is modified at a time), and this helps with contention. Having, for instance, 2048 shards (default) and 1M users connected concurrently (same instant) means that each user will need to coordinate writes in their counter with an average of under 500 other users (1M/2048=489). Lowering the shards might increase contention and latency but free additional memory. This is an advanced micro-optimization setting that should be used with caution.\n\nSee: https://www.krakend.io/docs/endpoints/rate-limit/",
							"default": 2048,
							"type": "integer"
						},
						"strategy": {
							"title": "Strategy",
							"description": "Available when using `client_max_rate`. Sets the strategy you will use to set client counters. Choose `ip` when the restrictions apply to the client's IP address, or set it to `header` when there is a header that identifies a user uniquely. That header must be defined with the `key` entry.\n\nSee: https://www.krakend.io/docs/endpoints/rate-limit/",
							"enum": [
								"ip",
								"header",
								"param"
							]
						}
					}
				},
				"https://www.krakend.io/schema/v2.7/qos/ratelimit/tiered.json": {
					"$schema": "http://json-schema.org/draft-07/schema#",
					"title": "Tiered Rate Limit",
					"description": "Enterprise Only. Apply ratelimit based on tier match.",
					"type": "object",
					"required": [
						"tier_key",
						"tiers"
					],
					"properties": {
						"tier_key": {
							"title": "Tier key",
							"description": "The header name containing the tier name. The string you provide is case-insensitive. If you need to take the value from a place that is not a header (a token, an API key), you must use propagate functions in the components that convert values to internal headers.\n\nSee: /docs/enterprise/service-settings/tiered-rate-limit/",
							"type": "string"
						},
						"tiers": {
							"title": "Tiers",
							"description": "The list of all tier definitions and limits for each. Each item in the list is a tier object.\n\nSee: /docs/enterprise/service-settings/tiered-rate-limit/",
							"type": "array",
							"items": {
								"type": "object",
								"properties": {
									"ratelimit": {
										"title": "Ratelimit",
										"description": "The rate limit definition. This is an object with the same attributes the [service rate limit](https://www.krakend.io/docs/enterprise/service-settings/service-rate-limit/) has.\n\nSee: /docs/enterprise/service-settings/tiered-rate-limit/",
										"$ref": "#/definitions/https%3A~1~1www.krakend.io~1schema~1v2.7~1qos~1ratelimit~1router.json",
										"type": "object"
									},
									"tier_value": {
										"title": "Tier value",
										"description": "The tier value. When you use `literal`, it is the tier name. When you use `policy`, it is the expression you want to evaluate to determine if the user matches this tier or not (see security policies for syntax).\n\nSee: /docs/enterprise/service-settings/tiered-rate-limit/",
										"examples": [
											"gold",
											"silver",
											"value.matches('User-[a-Z]+')"
										],
										"default": "",
										"type": [
											"string"
										]
									},
									"tier_value_as": {
										"title": "Tier value as",
										"description": "Determines how to parse the value found in the tier header. When `literal` is used, the exact value of the header is compared against the tier name. When `policy` is used, the value is used to evaluate a policy. When `*` is used, all values will match. Make sure to put the `*` as the last tier; otherwise the rest will be ignored.\n\nSee: /docs/enterprise/service-settings/tiered-rate-limit/",
										"default": "literal",
										"enum": [
											"literal",
											"policy",
											"*"
										]
									}
								}
							}
						}
					}
				},
				"https://www.krakend.io/schema/v2.7/router.json": {
					"$schema": "http://json-schema.org/draft-07/schema#",
					"title": "Router Options",
					"description": "The optional router configuration allows you to set global flags that change the way KrakenD processes the requests at the router layer.\n\nSee: https://www.krakend.io/docs/service-settings/router-options/",
					"type": "object",
					"minItems": 1,
					"properties": {
						"app_engine": {
							"title": "App Engine integration",
							"description": "The app_engine boolean trusts headers starting with X-AppEngine... for better integration with that PaaS.\n\nSee: https://www.krakend.io/docs/service-settings/router-options/",
							"type": "boolean"
						},
						"auto_options": {
							"title": "Automatic OPTIONS",
							"description": "When true, enables the autogenerated OPTIONS endpoint for all the registered paths\n\nSee: https://www.krakend.io/docs/service-settings/router-options/",
							"type": "boolean"
						},
						"decompress_gzip": {
							"title": "Decompress Gzip",
							"description": "Enterprise Only. Decompresses any Gzipped content before sending it to the backend when the `Content-Encoding` has `gzip` in the first position. You can also set this value [per endpoint](/docs/enterprise/endpoints/maximum-request-size/).\n\nSee: https://www.krakend.io/docs/service-settings/router-options/",
							"default": 0,
							"type": "integer"
						},
						"disable_access_log": {
							"title": "Disable Access Log",
							"description": "Stops registering access requests to KrakenD and leaving any logs out from the output.\n\nSee: https://www.krakend.io/docs/service-settings/router-options/",
							"default": false,
							"type": "boolean"
						},
						"disable_gzip": {
							"title": "Disable gzip compression",
							"description": "**Enterprise only**. All the output to the end user on the Enterprise Edition uses gzip when accepted by the client. Use this flag to remove gzip compression.\n\nSee: https://www.krakend.io/docs/service-settings/router-options/",
							"default": false,
							"type": "boolean"
						},
						"disable_handle_method_not_allowed": {
							"title": "Disable method not allowed",
							"description": "Whether to checks if another method is allowed for the current route, if the current request can not be routed. If this is the case, the request is answered with Method Not Allowed and HTTP status code 405. If no other Method is allowed, the request is a 404.\n\nSee: https://www.krakend.io/docs/service-settings/router-options/",
							"type": "boolean"
						},
						"disable_health": {
							"title": "Disable Health",
							"description": "When true you don't have any exposed health endpoint. You can still use a TCP checker or build an endpoint yourself.\n\nSee: https://www.krakend.io/docs/service-settings/router-options/",
							"default": false,
							"type": "boolean"
						},
						"disable_path_decoding": {
							"title": "Disable method not allowed",
							"description": "Disables automatic validation of the url params looking for url encoded ones.\n\nSee: https://www.krakend.io/docs/service-settings/router-options/",
							"type": "boolean"
						},
						"disable_redirect_fixed_path": {
							"title": "Disable redirect fixed path",
							"description": "If true, the router tries to fix the current request path, if no handle is registered for it\n\nSee: https://www.krakend.io/docs/service-settings/router-options/",
							"type": "boolean"
						},
						"disable_redirect_trailing_slash": {
							"title": "Disable redirect trailing slash",
							"description": "Disables automatic redirection if the current route can't be matched but a handler for the path with (without) the trailing slash exists.\n\nSee: https://www.krakend.io/docs/service-settings/router-options/",
							"type": "boolean"
						},
						"error_body": {
							"title": "Custom error body",
							"description": "Sets custom error bodies for 404 and 405 errors.\n\nSee: https://www.krakend.io/docs/service-settings/router-options/",
							"type": "object",
							"properties": {
								"404": {
									"title": "404 errors",
									"description": "Write any JSON object structure you would like to return to users when they request an endpoint not known by KrakenD. 404 Not Found errors.",
									"type": "object"
								},
								"405": {
									"title": "405 errors",
									"description": "Write any JSON object structure you would like to return to users",
									"type": "object"
								}
							},
							"patternProperties": {
								"^[@$_#]": {}
							},
							"additionalProperties": false
						},
						"forwarded_by_client_ip": {
							"title": "Forwarded by client IP",
							"description": "When set to true, the client IP will be parsed from the default request's headers, or the custom ones (`remote_ip_headers`). If the IP has passed through a **trusted proxy** (e.g.: a proxy, load balancer, or a third party application) it will be extracted. If no IP can be fetched, it falls back to the IP obtained from the request's remote address. When declared **you must** configure `trusted_proxies` too.\n\nSee: https://www.krakend.io/docs/service-settings/router-options/",
							"default": false,
							"type": "boolean"
						},
						"health_path": {
							"title": "Health endpoint path",
							"description": "The path where you'd like to expose the health endpoint.\n\nSee: https://www.krakend.io/docs/service-settings/router-options/",
							"default": "/__health",
							"type": "string"
						},
						"hide_version_header": {
							"title": "Hide version header",
							"description": "Removes the version of KrakenD used in the X-KrakenD-version headers.\n\nSee: https://www.krakend.io/docs/service-settings/router-options/",
							"default": false,
							"type": "boolean"
						},
						"logger_skip_paths": {
							"title": "Remove requests from logs",
							"description": "Defines the set of paths that are removed from the logging.\n\nSee: https://www.krakend.io/docs/service-settings/router-options/",
							"type": "array",
							"items": {
								"title": "Paths",
								"type": "string"
							}
						},
						"max_multipart_memory": {
							"title": "Memory available for Multipart forms",
							"description": "Sets the maxMemory param that is given to http.Request's Multipart Form method call.\n\nSee: https://www.krakend.io/docs/service-settings/router-options/",
							"type": "integer"
						},
						"max_payload": {
							"title": "Maximum Payload",
							"description": "Enterprise Only. Limits the maximum number of bytes a user can send to the gateway. `0` means no limit. You can also set this value [per endpoint](/docs/enterprise/endpoints/maximum-request-size/).\n\nSee: https://www.krakend.io/docs/service-settings/router-options/",
							"default": 0,
							"type": "integer"
						},
						"remote_ip_headers": {
							"title": "Remote IP headers",
							"description": "List of headers used to obtain the client IP when `forwarded_by_client_ip` is set to `true` and the remote address is matched by at least one of the network origins of `trusted_proxies`.\n\nSee: https://www.krakend.io/docs/service-settings/router-options/",
							"type": "array"
						},
						"remove_extra_slash": {
							"title": "Remove extra slash",
							"description": "A parameter can be parsed from the URL even with extra slashes.\n\nSee: https://www.krakend.io/docs/service-settings/router-options/",
							"default": false,
							"type": "boolean"
						},
						"return_error_msg": {
							"title": "Returning the gateway error message",
							"description": "When there is an error in the gateway (such as a timeout, a non-200 status code, etc.) it returns to the client the reason for the failure. The error is written in the body as is.\n\nSee: https://www.krakend.io/docs/service-settings/router-options/",
							"default": false,
							"type": "boolean"
						},
						"trusted_proxies": {
							"title": "Trusted Proxies",
							"description": "List of network origins (IPv4 addresses, IPv4 CIDRs, IPv6 addresses or IPv6 CIDRs) from which to trust request's headers that contain alternative client IP when `forwarded_by_client_ip` is `true`. When declared **you must** configure `forwarded_by_client_ip` set to `true`, and optionally `remote_ip_headers`.\n\nSee: https://www.krakend.io/docs/service-settings/router-options/",
							"type": "array"
						},
						"use_h2c": {
							"title": "Enable h2c",
							"description": "This flag has [moved to the root level](https://www.krakend.io/docs/service-settings/http-server-settings/), and will be deleted from router in future versions.\n\nSee: https://www.krakend.io/docs/service-settings/",
							"deprecated": true,
							"default": false,
							"type": "boolean"
						}
					},
					"patternProperties": {
						"^[@$_#]": {}
					},
					"additionalProperties": false
				},
				"https://www.krakend.io/schema/v2.7/security/bot-detector.json": {
					"$schema": "http://json-schema.org/draft-07/schema#",
					"title": "Bot detector",
					"description": "The bot detector module checks incoming connections to the gateway to determine if a bot made them, helping you detect and reject bots carrying out scraping, content theft, and form spam.\n\nSee: https://www.krakend.io/docs/throttling/botdetector/",
					"type": "object",
					"minProperties": 1,
					"properties": {
						"allow": {
							"title": "Allow",
							"description": "An array with EXACT MATCHES of trusted user agents that can connect.\n\nSee: https://www.krakend.io/docs/throttling/botdetector/",
							"examples": [
								"MyAndroidClient/1.0",
								"Pingdom.com_bot_version_1.1"
							],
							"default": [],
							"type": "array"
						},
						"cache_size": {
							"title": "Cache size",
							"description": "Size of the LRU cache that helps speed the bot detection. The size is the mumber of users agents that you want to keep in memory.\n\nSee: https://www.krakend.io/docs/throttling/botdetector/",
							"examples": [
								1000
							],
							"type": "integer"
						},
						"deny": {
							"title": "Deny",
							"description": "An array with EXACT MATCHES of undesired bots, to reject immediately.\n\nSee: https://www.krakend.io/docs/throttling/botdetector/",
							"examples": [
								"facebookexternalhit/1.1"
							],
							"default": [],
							"type": "array"
						},
						"empty_user_agent_is_bot": {
							"title": "Empty user agent is a bot?",
							"description": "Whether to consider an empty user-agent a bot (and reject it) or not. \n\nSee: https://www.krakend.io/docs/throttling/botdetector/",
							"default": false,
							"type": "boolean"
						},
						"patterns": {
							"title": "Bot patterns",
							"description": "An array with all the regular expressions that define bots. Matching bots are rejected.\n\nSee: https://www.krakend.io/docs/throttling/botdetector/",
							"examples": [
								"GoogleBot.*",
								"(facebookexternalhit)/.*"
							],
							"default": [],
							"type": "array"
						}
					},
					"patternProperties": {
						"^[@$_#]": {}
					},
					"additionalProperties": false
				},
				"https://www.krakend.io/schema/v2.7/security/cors.json": {
					"$schema": "http://json-schema.org/draft-07/schema#",
					"title": "HTTP Security",
					"description": "Security through HTTP headers, including HSTS, HPKP, MIME-Sniffing prevention, Clickjacking protection, and others.\n\nSee: https://www.krakend.io/docs/service-settings/security/",
					"type": "object",
					"properties": {
						"allow_credentials": {
							"title": "Allow credentials",
							"description": "When requests can include user credentials like cookies, HTTP authentication or client side SSL certificates.\n\nSee: https://www.krakend.io/docs/service-settings/cors/",
							"default": false,
							"type": "boolean"
						},
						"allow_headers": {
							"title": "Allowed headers",
							"description": "An array with the headers allowed, but `Origin`is always appended to the list. Requests with headers not in this list are rejected.\n\nSee: https://www.krakend.io/docs/service-settings/cors/",
							"default": [],
							"type": "array",
							"example": [
								"Accept-Language"
							]
						},
						"allow_methods": {
							"title": "Allowed methods",
							"description": "An array with all the HTTP methods allowed, in uppercase. Possible values are `GET`, `HEAD`,`POST`,`PUT`,`PATCH`,`DELETE`, or `OPTIONS`\n\nSee: https://www.krakend.io/docs/service-settings/cors/",
							"default": [
								"GET",
								"HEAD",
								"POST"
							],
							"type": "array",
							"items": {
								"enum": [
									"GET",
									"HEAD",
									"POST",
									"PUT",
									"PATCH",
									"DELETE",
									"OPTIONS"
								]
							}
						},
						"allow_origins": {
							"title": "Allowed origins",
							"description": "An array with all the origins allowed, examples of values are `https://example.com`, or `*` (any origin).\n\nSee: https://www.krakend.io/docs/service-settings/cors/",
							"default": [
								"*"
							],
							"type": "array"
						},
						"allow_private_network": {
							"title": "Allow private network",
							"description": "Indicates whether to accept cross-origin requests over a private network.\n\nSee: https://www.krakend.io/docs/service-settings/cors/",
							"default": false,
							"type": "boolean"
						},
						"debug": {
							"title": "Development flag",
							"description": "Show debugging information in the logger, **use it only during development**.\n\nSee: https://www.krakend.io/docs/service-settings/security/",
							"default": false,
							"type": "boolean"
						},
						"expose_headers": {
							"title": "Expose headers",
							"description": "List of headers that are safe to expose to the API of a CORS API specification.\n\nSee: https://www.krakend.io/docs/service-settings/cors/",
							"default": [
								"Content-Length",
								"Content-Type"
							],
							"type": "array"
						},
						"max_age": {
							"title": "Max Age",
							"description": "For how long the response can be cached. For zero values the `Access-Control-Max-Age` header is not set.\n\nSee: https://www.krakend.io/docs/service-settings/cors/",
							"default": "0h",
							"$ref": "#/definitions/https%3A~1~1www.krakend.io~1schema~1v2.7~1timeunits.json/$defs/timeunit",
							"type": "string"
						},
						"options_passthrough": {
							"title": "OPTIONS Passthrough",
							"description": "Instructs preflight to let other potential next handlers to process the OPTIONS method. Turn this on when you set the [`auto_opts` flag in the router to `true`](https://www.krakend.io/docs/service-settings/router-options/#auto_options).\n\nSee: https://www.krakend.io/docs/service-settings/cors/",
							"default": false,
							"type": "boolean"
						},
						"options_success_status": {
							"title": "Success Status Codes",
							"description": "The HTTP status code that is considered a success.\n\nSee: https://www.krakend.io/docs/service-settings/cors/",
							"examples": [
								200
							],
							"default": 204,
							"type": "integer"
						}
					},
					"patternProperties": {
						"^[@$_#]": {}
					},
					"additionalProperties": false
				},
				"https://www.krakend.io/schema/v2.7/security/http.json": {
					"$schema": "http://json-schema.org/draft-07/schema#",
					"title": "HTTP Security",
					"description": "Security through HTTP headers, including HSTS, HPKP, MIME-Sniffing prevention, Clickjacking protection, and others.\n\nSee: https://www.krakend.io/docs/service-settings/security/",
					"type": "object",
					"properties": {
						"allowed_hosts": {
							"title": "Allowed hosts",
							"description": "When a request hits KrakenD, it will confirm if the value of the Host HTTP header is in the list. If so, it will further process the request. If the host is not in the allowed hosts list, KrakenD will simply reject the request.\n\nSee: https://www.krakend.io/docs/service-settings/security/",
							"default": [],
							"type": "array"
						},
						"allowed_hosts_are_regex": {
							"title": "Hosts are regexps",
							"description": "Treat the allowed hosts list as regular expressions.\n\nSee: https://www.krakend.io/docs/service-settings/security/",
							"default": false,
							"type": "boolean"
						},
						"browser_xss_filter": {
							"title": "This feature enables the Cross-site scripting (XSS) filter in the user's browser.\n\nSee: https://www.krakend.io/docs/service-settings/security/",
							"default": false,
							"type": "boolean"
						},
						"content_security_policy": {
							"title": "Content-Security-Policy (CSP)",
							"description": "The HTTP Content-Security-Policy (CSP) default-src directive serves as a fallback for the other CSP fetch directives.\n\nSee: https://www.krakend.io/docs/service-settings/security/",
							"examples": [
								"default-src 'self';"
							],
							"default": "",
							"type": "string"
						},
						"content_type_nosniff": {
							"title": "Nosniff",
							"description": "Enabling this feature will prevent the user's browser from interpreting files as something else than declared by the content type in the HTTP headers.\n\nSee: https://www.krakend.io/docs/service-settings/security/",
							"default": false,
							"type": "boolean"
						},
						"custom_frame_options_value": {
							"title": "Clickjacking protection. Frame-Options value",
							"description": "You can add an X-Frame-Options header using custom_frame_options_value with the value of DENY (default behavior) or even set your custom value.\n\nSee: https://www.krakend.io/docs/service-settings/security/",
							"examples": [
								"ALLOW-FROM https://example.com"
							],
							"default": "",
							"type": "string"
						},
						"force_sts_header": {
							"title": "Force STS header",
							"description": "Force a STS Header even if using plain HTTP.\n\nSee: https://www.krakend.io/docs/service-settings/security/",
							"default": false,
							"type": "boolean"
						},
						"frame_deny": {
							"title": "Clickjacking protection",
							"description": "Set to true to enable clickjacking protection, together with `custom_frame_options_value`.\n\nSee: https://www.krakend.io/docs/service-settings/security/",
							"default": false,
							"type": "boolean"
						},
						"host_proxy_headers": {
							"title": "SSL Host",
							"description": "A set of header keys that may hold a proxied hostname value for the request.\n\nSee: https://www.krakend.io/docs/service-settings/security/",
							"examples": [
								[
									"X-Forwarded-Hosts"
								]
							],
							"type": "array"
						},
						"hpkp_public_key": {
							"title": "HTTP Public Key Pinning (HPKP)",
							"description": "HTTP Public Key Pinning (HPKP) is a security mechanism which allows HTTPS websites to resist impersonation by attackers using mis-issued or otherwise fraudulent certificates. (For example, sometimes attackers can compromise certificate authorities, and then can mis-issue certificates for a web origin.).\n\nSee: https://www.krakend.io/docs/service-settings/security/",
							"examples": [
								"pin-sha256=\"base64==\"; max-age=expireTime [; includeSubDomains][; report-uri=\"reportURI\"]"
							],
							"default": "",
							"type": "string"
						},
						"is_development": {
							"title": "Development flag",
							"description": "This will cause the AllowedHosts, SSLRedirect, and STSSeconds/STSIncludeSubdomains options to be ignored during development. When deploying to production, be sure to set this to false.\n\nSee: https://www.krakend.io/docs/service-settings/security/",
							"default": false,
							"type": "boolean"
						},
						"referrer_policy": {
							"title": "Referrer Policy",
							"description": "Allows the Referrer-Policy header with the value to be set with a custom value.\n\nSee: https://www.krakend.io/docs/service-settings/security/",
							"default": "same-origin",
							"type": "string"
						},
						"ssl_host": {
							"title": "SSL Host",
							"description": "When the SSL redirect is true, the host where the request is redirected to.\n\nSee: https://www.krakend.io/docs/service-settings/security/",
							"examples": [
								"ssl.host.domain"
							],
							"default": "ssl.host.domain",
							"type": "string"
						},
						"ssl_proxy_headers": {
							"title": "SSL Proxy Headers",
							"description": "Header keys with associated values that would indicate a valid https request. Useful when using Nginx, e.g: `\"X-Forwarded-Proto\": \"https\"`\n\nSee: https://www.krakend.io/docs/service-settings/security/",
							"examples": [
								{
									"X-Forwarded-Proto": "https"
								}
							],
							"type": "object"
						},
						"ssl_redirect": {
							"title": "SSL redirect",
							"description": "Redirect any request that is not using HTTPS\n\nSee: https://www.krakend.io/docs/service-settings/security/",
							"default": true,
							"type": "boolean"
						},
						"sts_include_subdomains": {
							"title": "Strict-Transport-Security include_subdomains",
							"description": "Set to true when you want the `includeSubdomains` be appended to the Strict-Transport-Security header.\n\nSee: https://www.krakend.io/docs/service-settings/security/",
							"default": false,
							"type": "boolean"
						},
						"sts_seconds": {
							"title": "HTTP Strict Transport Security (HSTS) seconds",
							"description": "Enable this policy by setting the `max-age` of the `Strict-Transport-Security` header. Setting to `0` disables HSTS.\n\nSee: https://www.krakend.io/docs/service-settings/security/",
							"default": 0,
							"type": "integer"
						}
					},
					"patternProperties": {
						"^[@$_#]": {}
					},
					"additionalProperties": false
				},
				"https://www.krakend.io/schema/v2.7/security/policies.json": {
					"$schema": "http://json-schema.org/draft-07/schema#",
					"title": "Securiy Policies",
					"description": "The policies engine allows you to write custom sets of policies that are validated during requests, responses, or token validation.\n\nSee: https://www.krakend.io/docs/enterprise/security-policies/",
					"type": "object",
					"anyOf": [
						{
							"required": [
								"req"
							]
						},
						{
							"required": [
								"resp"
							]
						},
						{
							"required": [
								"jwt"
							]
						}
					],
					"properties": {
						"auto_join_policies": {
							"title": "Auto-join policies",
							"description": "When true, all policies of the same type concatenate with an AND operation to evaluate a single expression. Performs faster, but its harder the debug.",
							"default": false,
							"type": "boolean"
						},
						"debug": {
							"title": "Debug",
							"description": "When true, all the inputs and evaluation results are printed in the console.",
							"default": false,
							"type": "boolean"
						},
						"disable_macros": {
							"title": "Disable advanced macros",
							"description": "Advanced macros can be disabled in those policies not needing them for a faster evaluation.",
							"default": false,
							"type": "boolean"
						},
						"jwt": {
							"title": "JWT policies",
							"description": "All the policies applied in the JWT context (token validation). **You must configure `auth/validator`** for the policies to run, otherwise they will be skipped. Any policy failing will generate a `401 Unauthorized` error. Works in the `endpoint` context only, and is not available under `backend`.\n\nSee: https://www.krakend.io/docs/enterprise/security-policies/",
							"type": "object",
							"required": [
								"policies"
							],
							"properties": {
								"policies": {
									"title": "Policies",
									"description": "An array with all the policies to evaluate. Each policy is represented as a string\n\nSee: https://www.krakend.io/docs/enterprise/security-policies/",
									"type": "array",
									"items": {
										"title": "The policy you want to evaluate",
										"type": "string"
									}
								}
							},
							"patternProperties": {
								"^[@$_#]": {}
							},
							"additionalProperties": false
						},
						"req": {
							"title": "Request policies",
							"description": "All the policies applied in the request context.\n\nSee: https://www.krakend.io/docs/enterprise/security-policies/",
							"type": "object",
							"required": [
								"policies"
							],
							"properties": {
								"error": {
									"type": "object",
									"properties": {
										"body": {
											"title": "Error body",
											"description": "Leave an empty string to use the validation error, or write a string with the error response body. This error is NOT returned in the response, but in the application logs, unless you enable `return_detailed_errors` in the `router` section. You can add escaped JSON, XML, etc in the string and add a Content-Type.\n\nSee: https://www.krakend.io/docs/enterprise/security-policies/",
											"default": "",
											"type": "string"
										},
										"content_type": {
											"title": "Content-Type",
											"description": "The Content-Type header you'd like to send with the error response. When unset, uses `text/plain` by default.\n\nSee: https://www.krakend.io/docs/enterprise/security-policies/",
											"default": "text/plain",
											"type": "string"
										},
										"status": {
											"title": "HTTP status code",
											"description": "The HTTP status code you want to return when the validation fails.\n\nSee: https://www.krakend.io/docs/enterprise/security-policies/",
											"default": 500,
											"type": "integer"
										}
									}
								},
								"policies": {
									"title": "Policies",
									"description": "An array with all the policies to evaluate. Each policy is represented as a string\n\nSee: https://www.krakend.io/docs/enterprise/security-policies/",
									"type": "array",
									"items": {
										"title": "The policy you want to evaluate",
										"type": "string"
									}
								}
							},
							"patternProperties": {
								"^[@$_#]": {}
							},
							"additionalProperties": false
						},
						"resp": {
							"title": "Response policies",
							"description": "All the policies applied in the response context.\n\nSee: https://www.krakend.io/docs/enterprise/security-policies/",
							"type": "object",
							"required": [
								"policies"
							],
							"properties": {
								"error": {
									"type": "object",
									"properties": {
										"body": {
											"title": "Error body",
											"description": "Leave an empty string to use the validation error, or write a string with the error response body. This error is NOT returned in the response, but in the application logs, unless you enable `return_detailed_errors` in the `router` section. You can add escaped JSON, XML, etc in the string and add a Content-Type.\n\nSee: https://www.krakend.io/docs/enterprise/security-policies/",
											"default": "",
											"type": "string"
										},
										"content_type": {
											"title": "Content-Type",
											"description": "The Content-Type header you'd like to send with the error response. When unset, uses `text/plain` by default.\n\nSee: https://www.krakend.io/docs/enterprise/security-policies/",
											"default": "text/plain",
											"type": "string"
										},
										"status": {
											"title": "HTTP status code",
											"description": "The HTTP status code you want to return when the validation fails.\n\nSee: https://www.krakend.io/docs/enterprise/security-policies/",
											"default": 500,
											"type": "integer"
										}
									}
								},
								"policies": {
									"title": "Policies",
									"description": "An array with all the policies to evaluate. Each policy is represented as a string\n\nSee: https://www.krakend.io/docs/enterprise/security-policies/",
									"type": "array",
									"items": {
										"title": "The policy you want to evaluate",
										"type": "string"
									}
								}
							},
							"patternProperties": {
								"^[@$_#]": {}
							},
							"additionalProperties": false
						}
					},
					"patternProperties": {
						"^[@$_#]": {}
					},
					"additionalProperties": false
				},
				"https://www.krakend.io/schema/v2.7/server/static-filesystem.json": {
					"$schema": "http://json-schema.org/draft-07/schema#",
					"title": "Static Filesystem",
					"description": "Enterprise only. Allows you to fetch and serve static content by registering a static web server for a set of defined paths (the prefixes).\n\nSee: https://www.krakend.io/docs/enterprise/endpoints/serve-static-content/",
					"type": "object",
					"required": [
						"prefix",
						"path"
					],
					"properties": {
						"directory_listing": {
							"description": "Whether to allow directory listings or not",
							"default": false,
							"type": "boolean"
						},
						"path": {
							"title": "Path",
							"description": "The folder in the filesystem containing the static files. Relative to the working dir where KrakenD config is (e.g.: `./assets`) or absolute (e.g.: `/var/www/assets`).\n\nSee: https://www.krakend.io/docs/enterprise/endpoints/serve-static-content/",
							"examples": [
								"./static/"
							],
							"type": "string"
						},
						"prefix": {
							"title": "Prefix",
							"description": "This is the beginning (prefix) of all URLs that are resolved using this plugin. All matching URLs won't be passed to the router, meaning that they are not considered endpoints. Make sure you are not overwriting valid endpoints. When the `prefix` is `/`, then **all traffic is served as static** and you must declare a prefix under `skip` (e.g.: `/api`) to match endpoints.\n\nSee: https://www.krakend.io/docs/enterprise/endpoints/serve-static-content/",
							"examples": [
								"/media/assets"
							],
							"type": "string"
						},
						"skip": {
							"title": "Skip paths",
							"description": "An array with all the prefix URLs that despite they could match with the `prefix`, you don't want to treat them as static content and pass them to the router.\n\nSee: https://www.krakend.io/docs/enterprise/endpoints/serve-static-content/",
							"examples": [
								[
									"/media/ignore/this/directory",
									"/media/file.json"
								]
							],
							"type": "array"
						}
					},
					"patternProperties": {
						"^[@$_#]": {}
					},
					"additionalProperties": false
				},
				"https://www.krakend.io/schema/v2.7/server/virtualhost.json": {
					"$schema": "http://json-schema.org/draft-07/schema#",
					"title": "VirtualHost",
					"description": "Enterprise only. The Virtual Host server allows you to run different configurations of KrakenD endpoints based on the host accessing the server.\n\nSee: https://www.krakend.io/docs/enterprise/service-settings/virtual-hosts/",
					"type": "object",
					"oneOf": [
						{
							"required": [
								"hosts"
							]
						},
						{
							"required": [
								"aliased_hosts"
							]
						}
					],
					"properties": {
						"aliased_hosts": {
							"title": "Virtualhosts with alias",
							"description": "A map of all recognized virtual hosts where the key is the alias and the value the host name, including the port if it's not 443 or 80. The values declared here must match the content of the `Host` header passed by the client. The alias must be an alphanumeric string.\n\nSee: https://www.krakend.io/docs/enterprise/service-settings/virtual-hosts/",
							"examples": [
								{
									"user_api": "users.svc.example.com:9000"
								}
							],
							"type": "object",
							"properties": {
								"[a-z0-9_]": {
									"title": "Virtualhost",
									"description": "The key of this map must compile with the regexp `a-z0-9_` and the host name is the string that matches the value sent by the user in the `Host` header.",
									"type": "string"
								}
							}
						},
						"hosts": {
							"title": "Virtualhosts",
							"description": "All recognized virtual hosts by KrakenD must be listed here. The values declared here must match the content of the `Host` header when passed by the client.\n\nSee: https://www.krakend.io/docs/enterprise/service-settings/virtual-hosts/",
							"examples": [
								[
									"api-a.example.com",
									"api-b.example.com"
								]
							],
							"type": "array",
							"items": {
								"type": "string"
							}
						}
					},
					"patternProperties": {
						"^[@$_#]": {}
					},
					"additionalProperties": false
				},
				"https://www.krakend.io/schema/v2.7/service_extra_config.json": {
					"$schema": "http://json-schema.org/draft-07/schema#",
					"title": "Schema definition for service extra_config",
					"type": "object",
					"properties": {
						"auth/api-keys": {
							"$ref": "#/definitions/https%3A~1~1www.krakend.io~1schema~1v2.7~1auth~1api-keys.json"
						},
						"auth/basic": {
							"$ref": "#/definitions/https%3A~1~1www.krakend.io~1schema~1v2.7~1auth~1basic.json"
						},
						"auth/revoker": {
							"$ref": "#/definitions/https%3A~1~1www.krakend.io~1schema~1v2.7~1auth~1revoker.json"
						},
						"auth/validator": {
							"$ref": "#/definitions/https%3A~1~1www.krakend.io~1schema~1v2.7~1auth~1jose.json"
						},
						"documentation/openapi": {
							"$ref": "#/definitions/https%3A~1~1www.krakend.io~1schema~1v2.7~1documentation~1openapi.json"
						},
						"github_com/letgoapp/krakend-influx": {
							"$ref": "#/definitions/https%3A~1~1www.krakend.io~1schema~1v2.7~1telemetry~1influx.json"
						},
						"grpc": {
							"$ref": "#/definitions/https%3A~1~1www.krakend.io~1schema~1v2.7~1grpc.json"
						},
						"modifier/lua-endpoint": {
							"$ref": "#/definitions/https%3A~1~1www.krakend.io~1schema~1v2.7~1modifier~1lua.json"
						},
						"modifier/response-headers": {
							"title": "Response Headers modifier",
							"description": "Enterprise only. Allows you to transform response headers declaratively.\n\nSee: https://www.krakend.io/docs/enterprise/service-settings/response-headers-modifier/",
							"$ref": "#/definitions/https%3A~1~1www.krakend.io~1schema~1v2.7~1modifier~1response-headers.json"
						},
						"plugin/http-server": {
							"$ref": "#/definitions/https%3A~1~1www.krakend.io~1schema~1v2.7~1plugin~1http-server.json"
						},
						"qos/ratelimit/service": {
							"title": "Service Rate-limiting",
							"description": "Enterprise Only. The service rate limit feature allows you to set the maximum requests per second a user or group of users can do to KrakenD and works analogously to the endpoint rate limit.\n\nSee: https://www.krakend.io/docs/enterprise/service-settings/service-rate-limit/",
							"$ref": "#/definitions/https%3A~1~1www.krakend.io~1schema~1v2.7~1qos~1ratelimit~1router.json"
						},
						"qos/ratelimit/tiered": {
							"$ref": "#/definitions/https%3A~1~1www.krakend.io~1schema~1v2.7~1qos~1ratelimit~1tiered.json"
						},
						"router": {
							"$ref": "#/definitions/https%3A~1~1www.krakend.io~1schema~1v2.7~1router.json"
						},
						"security/bot-detector": {
							"$ref": "#/definitions/https%3A~1~1www.krakend.io~1schema~1v2.7~1security~1bot-detector.json"
						},
						"security/cors": {
							"title": "Cross Origin Resource Sharing",
							"description": "When KrakenD endpoints are consumed from a browser, you might need to enable the Cross-Origin Resource Sharing (CORS) module as browsers restrict cross-origin HTTP requests initiated from scripts.\n\nSee: https://www.krakend.io/docs/service-settings/cors/",
							"default": {
								"allow_methods": [
									"POST",
									"GET"
								],
								"allow_origins": [
									"http://foobar.com"
								],
								"max_age": "12h"
							},
							"type": "object",
							"required": [
								"allow_origins"
							],
							"properties": {
								"allow_credentials": {
									"title": "Allow_credentials",
									"description": "When requests can include user credentials like cookies, HTTP authentication or client side SSL certificates\n\nSee: https://www.krakend.io/docs/service-settings/cors/",
									"default": true,
									"type": "boolean"
								},
								"allow_headers": {
									"title": "Allowed headers",
									"default": [],
									"type": "array"
								},
								"allow_methods": {
									"title": "Allowed methods",
									"description": "The array of all HTTP methods accepted, in uppercase.\n\nSee: https://www.krakend.io/docs/service-settings/cors/",
									"type": "array",
									"uniqueItems": true,
									"items": {
										"title": "Object in array",
										"description": "\n\nSee: https://www.krakend.io",
										"enum": [
											"GET",
											"HEAD",
											"POST",
											"PUT",
											"PATCH",
											"DELETE",
											"OPTIONS"
										]
									}
								},
								"allow_origins": {
									"title": "Allowed origins",
									"description": "An array with all the origins allowed, examples of values are https://example.com, or * (any origin).\n\nSee: https://www.krakend.io/docs/service-settings/cors/",
									"type": "array",
									"items": {
										"title": "Allowed origins list",
										"examples": [
											"*",
											"https://example.com"
										],
										"type": "string"
									}
								},
								"debug": {
									"title": "Show debug",
									"description": "Show debugging information in the logger, to be used only during development.\n\nSee: https://www.krakend.io/docs/service-settings/cors/",
									"default": true,
									"type": "boolean"
								},
								"expose_headers": {
									"title": "Expose headers",
									"description": "Headers that are safe to expose to the API of a CORS API specification-\n\nSee: https://www.krakend.io/docs/service-settings/cors/",
									"default": [],
									"type": "array"
								},
								"max_age": {
									"title": "Max age",
									"description": "For how long the response can be cached.\n\nSee: https://www.krakend.io/docs/service-settings/cors/",
									"examples": [
										"12h"
									],
									"$ref": "#/definitions/https%3A~1~1www.krakend.io~1schema~1v2.7~1timeunits.json/$defs/timeunit",
									"type": "string"
								}
							}
						},
						"security/http": {
							"$ref": "#/definitions/https%3A~1~1www.krakend.io~1schema~1v2.7~1security~1http.json"
						},
						"server/static-filesystem": {
							"$ref": "#/definitions/https%3A~1~1www.krakend.io~1schema~1v2.7~1server~1static-filesystem.json"
						},
						"server/virtualhost": {
							"$ref": "#/definitions/https%3A~1~1www.krakend.io~1schema~1v2.7~1server~1virtualhost.json"
						},
						"telemetry/gelf": {
							"$ref": "#/definitions/https%3A~1~1www.krakend.io~1schema~1v2.7~1telemetry~1gelf.json"
						},
						"telemetry/influx": {
							"$ref": "#/definitions/https%3A~1~1www.krakend.io~1schema~1v2.7~1telemetry~1influx.json"
						},
						"telemetry/logging": {
							"$ref": "#/definitions/https%3A~1~1www.krakend.io~1schema~1v2.7~1telemetry~1logging.json"
						},
						"telemetry/logstash": {
							"$ref": "#/definitions/https%3A~1~1www.krakend.io~1schema~1v2.7~1telemetry~1logstash.json"
						},
						"telemetry/metrics": {
							"$ref": "#/definitions/https%3A~1~1www.krakend.io~1schema~1v2.7~1telemetry~1metrics.json"
						},
						"telemetry/moesif": {
							"$ref": "#/definitions/https%3A~1~1www.krakend.io~1schema~1v2.7~1telemetry~1moesif.json"
						},
						"telemetry/newrelic": {
							"$ref": "#/definitions/https%3A~1~1www.krakend.io~1schema~1v2.7~1telemetry~1newrelic.json"
						},
						"telemetry/opencensus": {
							"$ref": "#/definitions/https%3A~1~1www.krakend.io~1schema~1v2.7~1telemetry~1opencensus.json"
						},
						"telemetry/opentelemetry": {
							"$ref": "#/definitions/https%3A~1~1www.krakend.io~1schema~1v2.7~1telemetry~1opentelemetry.json"
						},
						"telemetry/opentelemetry-security": {
							"$ref": "#/definitions/https%3A~1~1www.krakend.io~1schema~1v2.7~1telemetry~1opentelemetry-security.json"
						}
					},
					"patternProperties": {
						"^[@$_#]": {}
					},
					"additionalProperties": false
				},
				"https://www.krakend.io/schema/v2.7/telemetry/gelf.json": {
					"$schema": "http://json-schema.org/draft-07/schema#",
					"title": "GELF",
					"description": "Send structured events in GELF format to your Graylog Cluster.\n\nSee: https://www.krakend.io/docs/logging/graylog-gelf/",
					"type": "object",
					"required": [
						"address",
						"enable_tcp"
					],
					"properties": {
						"address": {
							"title": "Address",
							"description": "The address (including the port) of your Graylog cluster (or any other service that receives GELF inputs). E.g., `myGraylogInstance:12201`\n\nSee: https://www.krakend.io/docs/logging/graylog-gelf/",
							"type": "string"
						},
						"enable_tcp": {
							"title": "Enable TCP",
							"description": "Set to false (recommended) to use UDP, or true to use TCP. TCP performance is worst than UDP under heavy load.\n\nSee: https://www.krakend.io/docs/logging/graylog-gelf/",
							"type": "boolean"
						}
					},
					"patternProperties": {
						"^[@$_#]": {}
					},
					"additionalProperties": false
				},
				"https://www.krakend.io/schema/v2.7/telemetry/influx.json": {
					"$schema": "http://json-schema.org/draft-07/schema#",
					"title": "Telemetry via influx",
					"description": "Enables the extended logging capabilities.\n\nSee: https://www.krakend.io/docs/telemetry/influxdb-native/",
					"type": "object",
					"required": [
						"address",
						"ttl"
					],
					"properties": {
						"address": {
							"title": "Address",
							"description": "The complete url of the influxdb including the port if different from defaults in http/https.\n\nSee: https://www.krakend.io/docs/telemetry/influxdb-native/",
							"type": "string"
						},
						"buffer_size": {
							"title": "Points in buffer",
							"description": "The buffer size is a protection mechanism that allows you to temporarily store datapoints for later reporting when Influx is unavailable. If the buffer is `0`, reported metrics that fail are discarded immediately. If the buffer is a positive number, KrakenD creates a buffer with the number of datapoints set. When the buffer is full because the Influx server keeps failing, newer datapoints replace older ones in the buffer.\n\nSee: https://www.krakend.io/docs/telemetry/influxdb-native/",
							"type": "integer",
							"minimum": 0
						},
						"db": {
							"title": "DB name",
							"description": "Name of the InfluxDB database (Influx v1) or the bucket name (Influx v2).\n\nSee: https://www.krakend.io/docs/telemetry/influxdb-native/",
							"default": "krakend",
							"type": "string"
						},
						"password": {
							"title": "Password",
							"description": "Password to authenticate to InfluxDB. In Influx v2, you also need to add grant access with `influx v1 auth`.\n\nSee: https://www.krakend.io/docs/telemetry/influxdb-native/",
							"type": "string"
						},
						"ttl": {
							"title": "TTL",
							"description": "TTL against Influx.\n\nSee: https://www.krakend.io/docs/telemetry/influxdb-native/",
							"$ref": "#/definitions/https%3A~1~1www.krakend.io~1schema~1v2.7~1timeunits.json/$defs/timeunit"
						},
						"username": {
							"title": "Username",
							"description": "Username to authenticate to InfluxDB.\n\nSee: https://www.krakend.io/docs/telemetry/influxdb-native/",
							"type": "string"
						}
					},
					"patternProperties": {
						"^[@$_#]": {}
					},
					"additionalProperties": false
				},
				"https://www.krakend.io/schema/v2.7/telemetry/logging.json": {
					"$schema": "http://json-schema.org/draft-07/schema#",
					"title": "Improved logging",
					"description": "Enables the extended logging capabilities.\n\nSee: https://www.krakend.io/docs/logging/",
					"type": "object",
					"required": [
						"level"
					],
					"properties": {
						"format": {
							"title": "Log format",
							"description": "Specify the format of the logs: default, logstash, or custom.\nThe custom format needs an additional key \"custom_format\".\nThe \"logstash\" format needs the \"telemetry/logstash\" component added too.\n\nSee: https://www.krakend.io/docs/logging/",
							"examples": [
								"default",
								"logstash",
								"custom"
							],
							"default": "default",
							"type": "string"
						},
						"custom_format": {
							"title": "Custom format",
							"description": "Lets you write a custom logging pattern using variables, e.g: `%{message}`.\n\nSee: https://www.krakend.io/docs/logging/",
							"type": "string"
						},
						"level": {
							"title": "Log Level",
							"description": "What type of **reporting level** do you expect from the application? Use the `DEBUG` level in the development stages but not in production. Possible values are from more verbose to least.\n\nSee: https://www.krakend.io/docs/logging/",
							"enum": [
								"DEBUG",
								"INFO",
								"WARNING",
								"ERROR",
								"CRITICAL"
							]
						},
						"prefix": {
							"title": "Prefix",
							"description": "Adds the defined string at the beginning of every logged line, so you can quickly filter messages with external tools later on. It's recommended to always add a prefix `[INSIDE BRACKETS]` to make use of predefined dashboards.\n\nSee: https://www.krakend.io/docs/logging/",
							"type": "string"
						},
						"stdout": {
							"title": "Logs to stdout",
							"description": "Set to true to send logs to stdout.\n\nSee: https://www.krakend.io/docs/logging/",
							"default": false,
							"type": "boolean"
						},
						"syslog": {
							"title": "Logs to syslog",
							"description": "Set to true to send logs to syslog.\n\nSee: https://www.krakend.io/docs/logging/",
							"default": false,
							"type": "boolean"
						},
						"syslog_facility": {
							"title": "Syslog facility",
							"description": "When using syslog, the facility tells KrakenD where to send the messages as set by the locals of the [syslog standard](https://www.rfc-editor.org/rfc/rfc5424.html).\n\nSee: https://www.krakend.io/docs/logging/",
							"default": "local3",
							"enum": [
								"local0",
								"local1",
								"local2",
								"local3",
								"local4",
								"local5",
								"local6",
								"local7"
							]
						}
					},
					"patternProperties": {
						"^[@$_#]": {}
					},
					"additionalProperties": false
				},
				"https://www.krakend.io/schema/v2.7/telemetry/logstash.json": {
					"$schema": "http://json-schema.org/draft-07/schema#",
					"title": "Logstash",
					"description": "Enables logstash when the extra_config \"telemetry/logging\" is also present.\n\nSee: https://www.krakend.io/docs/logging/logstash/",
					"type": "object",
					"required": [
						"enabled"
					],
					"properties": {
						"enabled": {
							"title": "Enabled",
							"default": true,
							"type": "boolean"
						}
					},
					"patternProperties": {
						"^[@$_#]": {}
					},
					"additionalProperties": false
				},
				"https://www.krakend.io/schema/v2.7/telemetry/metrics.json": {
					"$schema": "http://json-schema.org/draft-07/schema#",
					"title": "Extended metrics",
					"description": "Collects extended metrics to push to InfluxDB or expose them in the /__stats/ endpoint.\n\nSee: https://www.krakend.io/docs/telemetry/extended-metrics/",
					"type": "object",
					"properties": {
						"backend_disabled": {
							"title": "Backend disabled",
							"description": "Skip any metrics happening in the backend layer. Disabling layers saves memory consumption but reduces visibility.\n\nSee: https://www.krakend.io/docs/telemetry/extended-metrics/",
							"default": false,
							"type": "boolean"
						},
						"collection_time": {
							"title": "Collection time",
							"description": "The time window to consolidate collected metrics. Metrics are updated in their internal counters all the time, but the `/__stats/` endpoint, or the Influx reporter, won't see them updated until this window completes.\n\nSee: https://www.krakend.io/docs/telemetry/extended-metrics/",
							"default": "60s",
							"$ref": "#/definitions/https%3A~1~1www.krakend.io~1schema~1v2.7~1timeunits.json/$defs/timeunit",
							"type": "string"
						},
						"endpoint_disabled": {
							"title": "Endpoint disabled",
							"description": "When true do not publish the /__stats/ endpoint. Metrics won't be accessible via the endpoint but still collected (and you can send them to Influx for instance).\n\nSee: https://www.krakend.io/docs/telemetry/extended-metrics/",
							"default": false,
							"type": "boolean"
						},
						"listen_address": {
							"title": "Listen address",
							"description": "Change the listening address where the metrics endpoint is exposed.\n\nSee: https://www.krakend.io/docs/telemetry/extended-metrics/",
							"default": ":8090",
							"type": "string",
							"pattern": "^:[0-9]+$"
						},
						"proxy_disabled": {
							"title": "Proxy disabled",
							"description": "Skip any metrics happening in the proxy layer (traffic against your backends). Disabling layers saves memory consumption but reduces visibility.\n\nSee: https://www.krakend.io/docs/telemetry/extended-metrics/",
							"default": false,
							"type": "boolean"
						},
						"router_disabled": {
							"title": "Router disabled",
							"description": "Skip any metrics happening in the router layer (activity in KrakenD endpoints). Disabling layers saves memory consumption but reduces visibility.\n\nSee: https://www.krakend.io/docs/telemetry/extended-metrics/",
							"default": false,
							"type": "boolean"
						}
					},
					"patternProperties": {
						"^[@$_#]": {}
					},
					"additionalProperties": false
				},
				"https://www.krakend.io/schema/v2.7/telemetry/moesif.json": {
					"$schema": "http://json-schema.org/draft-07/schema#",
					"title": "Moesif integration",
					"description": "The Moesif integration helps you understand and monetize API usage with a robust analytics and billing platform.\n\nSee: https://www.krakend.io/docs/enterprise/governance/moesif/",
					"type": "object",
					"required": [
						"application_id",
						"user_id_headers"
					],
					"properties": {
						"application_id": {
							"title": "Collector Application ID",
							"description": "The Collector Application ID is used to send events, actions, users, and companies to Moesif's Collector API. Moesif provides it under the 'API Keys' section.\n\nSee: https://www.krakend.io/docs/enterprise/governance/moesif/",
							"type": "string"
						},
						"batch_size": {
							"title": "Batch Size",
							"description": "Number of events you will send on every batch reporting asynchronously to Moesif. For high throughput you will need to increase this value.\n\nSee: https://www.krakend.io/docs/enterprise/governance/moesif/",
							"default": 200,
							"type": "integer"
						},
						"debug": {
							"title": "Enable debug",
							"description": "Set to true when configuring Moesif for the first time while in development, to see the activity in the logs. Set to false in production.\n\nSee: https://www.krakend.io/docs/enterprise/governance/moesif/",
							"default": false,
							"type": "boolean"
						},
						"event_queue_size": {
							"title": "Event Queue Size",
							"description": "Sends the number of events you can hold in-memory to send them asynchronously to Moesif. If the throughput of your API generates more events than the size of the queue, the exceeding events will be discarded and not reported.\n\nSee: https://www.krakend.io/docs/enterprise/governance/moesif/",
							"default": 1000000,
							"type": "integer"
						},
						"identify_company": {
							"title": "Identify company",
							"description": "It sets which strategy you want to use to identify the company. Identifying the company helps you efficiently govern your API. Choose the system you wish to apply (**declare only one property**). The claim value you access must be of type string. You can access nested structured using the dot `.` separator. When using dots, literals with an exact match containing the dot are checked first.\n\nSee: https://www.krakend.io/docs/enterprise/governance/moesif/",
							"type": "object",
							"maxProperties": 1,
							"properties": {
								"header": {
									"title": "Company in Header",
									"description": "The company is identified using a header. Provide the header name.\n\nSee: https://www.krakend.io/docs/enterprise/governance/moesif/",
									"examples": [
										"X-Tenant"
									],
									"type": "string"
								},
								"jwt_claim": {
									"title": "Company in Claim",
									"description": "The company is stored in a claim inside the JWT. The claim must return a string.\n\nSee: https://www.krakend.io/docs/enterprise/governance/moesif/",
									"examples": [
										"company_id"
									],
									"type": "string"
								},
								"query_string": {
									"title": "Company in Query String",
									"description": "The company is always passed inside a query string when calling any URL. Provide the query string name.\n\nSee: https://www.krakend.io/docs/enterprise/governance/moesif/",
									"examples": [
										"company"
									],
									"type": "string"
								}
							},
							"patternProperties": {
								"^[@$_#]": {}
							},
							"additionalProperties": false
						},
						"log_body": {
							"title": "Send the body",
							"description": "Send the body of all endpoints and requests to Moesif.\n\nSee: https://www.krakend.io/docs/enterprise/governance/moesif/",
							"default": true,
							"type": "boolean"
						},
						"request_body_masks": {
							"title": "Request body masks",
							"description": "The list of fields in the request body that you want to mask before sending them to Moesif. You can set `log_body` to `false` to prevent any body being sent.\n\nSee: https://www.krakend.io/docs/enterprise/governance/moesif/",
							"examples": [
								[
									"password",
									"credit_card"
								]
							],
							"type": "array",
							"items": {
								"type": "string"
							}
						},
						"request_header_masks": {
							"title": "Request header masks",
							"description": "The list of request headers that you want to mask their values before sending them to Moesif.\n\nSee: https://www.krakend.io/docs/enterprise/governance/moesif/",
							"examples": [
								[
									"Authorization"
								]
							],
							"type": "array",
							"items": {
								"type": "string"
							}
						},
						"response_body_masks": {
							"title": "Response body masks",
							"description": "The list of fields in the response body that you want to mask before sending them to Moesif. You can set `log_body` to `false` to prevent any body being sent.\n\nSee: https://www.krakend.io/docs/enterprise/governance/moesif/",
							"examples": [
								[
									"password",
									"credit_card"
								]
							],
							"type": "array",
							"items": {
								"type": "string"
							}
						},
						"response_header_masks": {
							"title": "Response header masks",
							"description": "The list of response headers that you want to mask their values before sending them to Moesif.\n\nSee: https://www.krakend.io/docs/enterprise/governance/moesif/",
							"examples": [
								[
									"Cookie"
								]
							],
							"type": "array",
							"items": {
								"type": "string"
							}
						},
						"should_skip": {
							"title": "Should Skip",
							"description": "Defines an expression expressed as [Security Policy](https://www.krakend.io/docs/enterprise/security-policies/) that avoids reporting to Moesif when the result of the evaluation is `true`.\n\nSee: https://www.krakend.io/docs/enterprise/governance/moesif/",
							"examples": [
								"( req_method=='GET' || req_path.startsWith('/bar/')) && hasHeader('X-Something')"
							],
							"default": [],
							"type": "string"
						},
						"timer_wake_up_seconds": {
							"title": "Timer Wake Up",
							"description": "Specifies how often a background thread runs to send events to Moesif. Value in seconds.\n\nSee: https://www.krakend.io/docs/enterprise/governance/moesif/",
							"default": 2,
							"type": "integer"
						},
						"user_id_headers": {
							"title": "User ID headers",
							"description": "Defines the list of possible headers that can identify a user uniquely. When the header is `Authorization`, it automatically extracts the username if it contains an `Authorization: Basic` value with no additional configuration. If, on the other hand, you use tokens and pass an `Authorization: Bearer`, it will extract the user ID from the JWT claim defined under `user_id_jwt_claim`. If there are multiple headers in the list, all of them are tested in the given order, and the first existing header in the list is used to extract the user ID (successfully or not).\n\nSee: https://www.krakend.io/docs/enterprise/governance/moesif/",
							"examples": [
								[
									"X-User-ID",
									"Authorization"
								]
							],
							"type": "array"
						},
						"user_id_jwt_claim": {
							"title": "User ID JWT claim",
							"description": "When using JWT tokens, it defines which claim contains the user ID. The claim value you access must be of type string. You can access nested structured using the dot `.` separator. When using dots, literals with an exact match containing the dot are checked first.\n\nSee: https://www.krakend.io/docs/enterprise/governance/moesif/",
							"examples": [
								"sub",
								"user.id"
							],
							"default": "sub",
							"type": "string"
						}
					},
					"patternProperties": {
						"^[@$_#]": {}
					},
					"additionalProperties": false
				},
				"https://www.krakend.io/schema/v2.7/telemetry/newrelic.json": {
					"$schema": "http://json-schema.org/draft-07/schema#",
					"title": "NewRelic exporter",
					"description": "The New Relic integration lets you push KrakenD metrics and distributed traces to your New Relic dashboard. It uses internally the official New Relic SDK and brings its features to your APM dashboard.\n\nSee: https://www.krakend.io/docs/enterprise/telemetry/newrelic/",
					"type": "object",
					"required": [
						"license"
					],
					"properties": {
						"debug": {
							"title": "Enable debug",
							"description": "Set to true when configuring New Relic for the first time while in development, to see the activity in the logs. Set to false in production.\n\nSee: https://www.krakend.io/docs/enterprise/telemetry/newrelic/",
							"default": false,
							"type": "boolean"
						},
						"headers_to_pass": {
							"title": "Headers to pass",
							"description": "Defines an explicit list of headers sent during the client request that will be reported to NewRelic, in addition to the default headers NewRelic sets. Setting the `[\"*\"]` value will send all headers sent by the client to NewRelic. Whether you declare this setting or not, you will usually receive from the NewRelic SDK the `Accept`, `Content-Type`, `User-Agent`, and `Referer` headers.\n\nSee: https://www.krakend.io/docs/enterprise/telemetry/newrelic/",
							"examples": [
								[
									"*"
								]
							],
							"type": "array"
						},
						"license": {
							"title": "License key",
							"description": "The API key provided by New Relic to push data into your account.\n\nSee: https://www.krakend.io/docs/enterprise/telemetry/newrelic/",
							"type": "string"
						}
					},
					"patternProperties": {
						"^[@$_#]": {}
					},
					"additionalProperties": false
				},
				"https://www.krakend.io/schema/v2.7/telemetry/opencensus.json": {
					"$schema": "http://json-schema.org/draft-07/schema#",
					"title": "Telemetry via Opencensus",
					"description": "Enables the extended logging capabilities.\n\nSee: https://www.krakend.io/docs/telemetry/opencensus/",
					"type": "object",
					"required": [
						"exporters"
					],
					"properties": {
						"enabled_layers": {
							"title": "Enabled Layers",
							"description": "Lets you specify what data you want to export. All layers are enabled by default unless you declare this section.",
							"properties": {
								"backend": {
									"title": "Report backend",
									"description": "Reports the activity between KrakenD and your services",
									"default": false,
									"type": "boolean"
								},
								"pipe": {
									"title": "Report pipe",
									"description": "Reports the activity at the beginning of the proxy layer. It gives a more detailed view of the internals of the pipe between end-users and KrakenD, having into account merging of different backends.",
									"default": false,
									"type": "boolean"
								},
								"router": {
									"title": "Report router",
									"description": "Reports the activity between end-users and KrakenD",
									"default": false,
									"type": "boolean"
								}
							},
							"patternProperties": {
								"^[@$_#]": {}
							},
							"additionalProperties": false
						},
						"exporters": {
							"title": "Exporters",
							"description": "The exporter(s) you would like to enable. See each exporter configuration in its own section.",
							"type": "object",
							"minProperties": 1,
							"properties": {
								"datadog": {
									"title": "Datadog",
									"description": "Datadog is a monitoring and security platform for developers, IT operations teams and business in the cloud.\n\nSee: https://www.krakend.io/docs/telemetry/datadog/",
									"type": "object",
									"required": [
										"namespace",
										"service",
										"trace_address",
										"stats_address",
										"tags",
										"global_tags",
										"disable_count_per_buckets"
									],
									"properties": {
										"disable_count_per_buckets": {
											"title": "Disable count per buckets",
											"description": "Specifies whether to emit count_per_bucket metrics.\n\nSee: https://www.krakend.io/docs/telemetry/datadog/",
											"default": false,
											"type": "boolean"
										},
										"global_tags": {
											"title": "Global tags",
											"description": "A set of tags (key/value) that will automatically be applied to all exported spans.\n\nSee: https://www.krakend.io/docs/telemetry/datadog/",
											"examples": [
												{
													"env": "prod"
												}
											],
											"type": "object"
										},
										"namespace": {
											"title": "Namespace",
											"description": "The namespace to which metric keys are appended.\n\nSee: https://www.krakend.io/docs/telemetry/datadog/",
											"type": "string"
										},
										"service": {
											"title": "Service",
											"description": "Service specifies the service name used for tracing\n\nSee: https://www.krakend.io/docs/telemetry/datadog/",
											"examples": [
												"gateway"
											],
											"type": "string"
										},
										"stats_address": {
											"title": "Stats address",
											"description": "Specifies the host[:port] address for DogStatsD. To enable ingestion using Unix Domain Socket (UDS) mount your UDS path and reference it in the stats_address using a path like `unix:///var/run/datadog/dsd.socket`.\n\nSee: https://www.krakend.io/docs/telemetry/datadog/",
											"examples": [
												"localhost:8125"
											],
											"default": "localhost:8125",
											"type": "string"
										},
										"tags": {
											"title": "Tags",
											"description": "Specifies a set of global tags to attach to each metric.\n\nSee: https://www.krakend.io/docs/telemetry/datadog/",
											"type": "array"
										},
										"trace_address": {
											"title": "Trace address",
											"description": "Specifies the host[:port] address of the Datadog Trace Agent.\n\nSee: https://www.krakend.io/docs/telemetry/datadog/",
											"default": "localhost:8126",
											"type": "string"
										}
									},
									"patternProperties": {
										"^[@$_#]": {}
									},
									"additionalProperties": false
								},
								"influxdb": {
									"title": "Influxdb",
									"description": "Exports data to InfluxDB: A time series database designed to handle high write and query loads.\n\nSee: https://www.krakend.io/docs/telemetry/influxdb/",
									"type": "object",
									"required": [
										"address",
										"db"
									],
									"properties": {
										"address": {
											"title": "Address",
											"description": "The URL (including port) where your InfluxDB is installed.\n\nSee: https://www.krakend.io/docs/telemetry/influxdb/",
											"examples": [
												"http://192.168.99.100:8086"
											],
											"type": "string"
										},
										"db": {
											"title": "DB name",
											"description": "The database name\n\nSee: https://www.krakend.io/docs/telemetry/influxdb/",
											"examples": [
												"krakend"
											],
											"type": "string"
										},
										"password": {
											"title": "Password",
											"description": "The password to access the database\n\nSee: https://www.krakend.io/docs/telemetry/influxdb/",
											"examples": [
												"kr4k3nd"
											],
											"type": "string"
										},
										"timeout": {
											"title": "Timeout",
											"description": "The maximum time you will wait for InfluxDB to respond.\n\nSee: https://www.krakend.io/docs/telemetry/influxdb/",
											"examples": [
												"2s"
											],
											"$ref": "#/definitions/https%3A~1~1www.krakend.io~1schema~1v2.7~1timeunits.json/$defs/timeunit",
											"type": "string"
										},
										"username": {
											"title": "Username",
											"description": "The influxdb username to access the database\n\nSee: https://www.krakend.io/docs/telemetry/influxdb/",
											"examples": [
												"krakend"
											],
											"type": "string"
										}
									},
									"patternProperties": {
										"^[@$_#]": {}
									},
									"additionalProperties": false
								},
								"jaeger": {
									"title": "Jaeger",
									"description": "Submit spans to a Jaeger Collector (HTTP) with `endpoint` or to a Jaeger Agent (UDP) with `agent_endpoint`. \n\nSee https://www.krakend.io/docs/telemetry/jaeger/",
									"type": "object",
									"oneOf": [
										{
											"required": [
												"endpoint",
												"service_name"
											]
										},
										{
											"required": [
												"agent_endpoint",
												"service_name"
											]
										}
									],
									"properties": {
										"agent_endpoint": {
											"title": "Agent Endpoint",
											"description": "The address where the Jaeger Agent is (Thrift over UDP), e.g., `jaeger:6831`\n\nSee: https://www.krakend.io/docs/telemetry/jaeger/",
											"examples": [
												"http://192.168.99.100:14268/api/traces"
											],
											"type": "string"
										},
										"buffer_max_count": {
											"title": "Buffer max count",
											"description": "Total number of traces to buffer in memory\n\nSee: https://www.krakend.io/docs/telemetry/jaeger/",
											"type": "integer"
										},
										"endpoint": {
											"title": "Collector Endpoint",
											"description": "The full URL including port indicating where your Jaeger Collector is (Thrift over HTTP/S), e.g., `http://jaeger:14268/api/traces`\n\nSee: https://www.krakend.io/docs/telemetry/jaeger/",
											"examples": [
												"http://192.168.99.100:14268/api/traces"
											],
											"type": "string"
										},
										"service_name": {
											"title": "Service name",
											"description": "The service name registered in Jaeger\n\nSee: https://www.krakend.io/docs/telemetry/jaeger/",
											"examples": [
												"krakend"
											],
											"type": "string"
										}
									},
									"patternProperties": {
										"^[@$_#]": {}
									},
									"additionalProperties": false
								},
								"logger": {
									"title": "Logger",
									"description": "Opencensus can export data to the system logger as another exporter. Recommended to use `telemetry/logging` instead.\n\nSee: https://www.krakend.io/docs/telemetry/logger/",
									"type": "object",
									"properties": {
										"spans": {
											"title": "Spans",
											"description": "Whether to log the spans or not",
											"default": false,
											"type": "boolean"
										},
										"stats": {
											"title": "Stats",
											"description": "Whether to log the statistics or not",
											"default": false,
											"type": "boolean"
										}
									},
									"patternProperties": {
										"^[@$_#]": {}
									},
									"additionalProperties": false
								},
								"ocagent": {
									"title": "Ocagent",
									"description": "Exporting metrics, logs, and events to the OpenCensus Agent.\n\nSee: https://www.krakend.io/docs/telemetry/ocagent/",
									"type": "object",
									"required": [
										"address",
										"service_name"
									],
									"properties": {
										"address": {
											"title": "Address",
											"description": "The address of your Azure Monitor collector.\n\nSee: https://www.krakend.io/docs/telemetry/ocagent/",
											"examples": [
												"localhost:55678"
											],
											"type": "string"
										},
										"enable_compression": {
											"title": "Enable compression",
											"description": "Whether to send data compressed or not.\n\nSee: https://www.krakend.io/docs/telemetry/ocagent/",
											"default": false,
											"type": "boolean"
										},
										"headers": {
											"title": "Headers",
											"description": "List of keys and values for the headers sent. Keys and values must be of type string.\n\nSee: https://www.krakend.io/docs/telemetry/ocagent/",
											"examples": [
												{
													"header1": "value1"
												}
											],
											"type": "object"
										},
										"insecure": {
											"title": "Insecure",
											"description": "Whether the connection can be established in plain (insecure) or not.\n\nSee: https://www.krakend.io/docs/telemetry/ocagent/",
											"default": false,
											"type": "boolean"
										},
										"reconnection": {
											"title": "Reconnection",
											"description": "The reconnection time\n\nSee: https://www.krakend.io/docs/telemetry/ocagent/",
											"default": "2s",
											"$ref": "#/definitions/https%3A~1~1www.krakend.io~1schema~1v2.7~1timeunits.json/$defs/timeunit",
											"type": "string"
										},
										"service_name": {
											"title": "Service name",
											"description": "An identifier of your service, e.g, `krakend`.\n\nSee: https://www.krakend.io/docs/telemetry/ocagent/",
											"type": "string"
										}
									},
									"patternProperties": {
										"^[@$_#]": {}
									},
									"additionalProperties": false
								},
								"prometheus": {
									"title": "Prometheus",
									"description": "Prometheus is an open-source systems monitoring and alerting toolkit.",
									"type": "object",
									"required": [
										"port"
									],
									"properties": {
										"namespace": {
											"title": "Namespace",
											"description": "Sets the domain the metric belongs to.\n\nSee: https://www.krakend.io/docs/telemetry/prometheus/",
											"type": "string"
										},
										"port": {
											"title": "Port",
											"description": "Port on which the Prometheus exporter should listen\n\nSee: https://www.krakend.io/docs/telemetry/prometheus/",
											"examples": [
												9091
											],
											"type": "integer"
										},
										"tag_host": {
											"title": "Tag host",
											"description": "Whether to send the host as a metric or not.\n\nSee: https://www.krakend.io/docs/telemetry/prometheus/",
											"default": false,
											"type": "boolean"
										},
										"tag_method": {
											"title": "Tag method",
											"description": "Whether to send the HTTP method as a metric or not.\n\nSee: https://www.krakend.io/docs/telemetry/prometheus/",
											"default": true,
											"type": "boolean"
										},
										"tag_path": {
											"title": "Tag path",
											"description": "Whether to send the path as a metric or not.\n\nSee: https://www.krakend.io/docs/telemetry/prometheus/",
											"default": false,
											"type": "boolean"
										},
										"tag_statuscode": {
											"title": "Tag status code",
											"description": "Whether to send the status code as a metric or not.\n\nSee: https://www.krakend.io/docs/telemetry/prometheus/",
											"default": false,
											"type": "boolean"
										}
									},
									"patternProperties": {
										"^[@$_#]": {}
									},
									"additionalProperties": false
								},
								"stackdriver": {
									"title": "Stackdriver",
									"description": "Export metrics and traces to Google Cloud",
									"type": "object",
									"required": [
										"project_id",
										"default_labels"
									],
									"properties": {
										"default_labels": {
											"title": "Default_labels",
											"description": "A map object. Enter here any label that will be assigned by default to the reported metric so you can filter later on Stack Driver.\n\nSee: https://www.krakend.io/docs/telemetry/stackdriver/",
											"examples": [
												{
													"env": "production"
												}
											],
											"type": "object"
										},
										"metric_prefix": {
											"title": "Metric_prefix",
											"description": "A prefix that you can add to all your metrics for better organization.\n\nSee: https://www.krakend.io/docs/telemetry/stackdriver/",
											"type": "string"
										},
										"project_id": {
											"title": "Project_id",
											"description": "The identifier of your Google Cloud project. The `project_id` **is not the project name**. You can omit this value from the configuration if you have an application credential file for Google.\n\nSee: https://www.krakend.io/docs/telemetry/stackdriver/",
											"examples": [
												"ID"
											],
											"default": "",
											"type": "string",
											"pattern": "^.*$"
										}
									},
									"patternProperties": {
										"^[@$_#]": {}
									},
									"additionalProperties": false
								},
								"xray": {
									"title": "AWS X-ray",
									"description": "AWS X-Ray is a service offered by Amazon that provides an end-to-end view of requests as they travel through your application, and shows a map of your application's underlying components.",
									"type": "object",
									"oneOf": [
										{
											"required": [
												"region",
												"version",
												"access_key_id",
												"secret_access_key"
											]
										},
										{
											"required": [
												"region",
												"version",
												"use_env"
											]
										}
									],
									"required": [
										"region",
										"version"
									],
									"properties": {
										"access_key_id": {
											"title": "AWS access key id",
											"description": " Your access key ID provided by Amazon. Needed when `use_env` is unset or set to false.\n\nSee: https://www.krakend.io/docs/telemetry/xray/",
											"type": "string"
										},
										"region": {
											"title": "Region",
											"description": "The AWS geographical region, e.g, `us-east-1`.\n\nSee: https://www.krakend.io/docs/telemetry/xray/",
											"examples": [
												"eu-west-1"
											],
											"type": "string"
										},
										"secret_access_key": {
											"title": "AWS secret access key",
											"description": "Your secret access key provided by Amazon. Needed when `use_env` is unset or set to false.\n\nSee: https://www.krakend.io",
											"type": "string"
										},
										"use_env": {
											"title": "Use_env",
											"description": "When true the AWS credentials (access_key_id and secret_access_key) are taken from environment vars. Don't specify them then.\n\nSee: https://www.krakend.io/docs/telemetry/xray/",
											"default": false,
											"type": "boolean"
										},
										"version": {
											"title": "Version",
											"description": "The version of the AWS X-Ray service to use.\n\nSee: https://www.krakend.io/docs/telemetry/xray/",
											"default": "KrakenD-opencensus",
											"type": "string"
										}
									},
									"patternProperties": {
										"^[@$_#]": {}
									},
									"additionalProperties": false
								},
								"zipkin": {
									"title": "Zipkin",
									"description": "Export telemetry data to a Zipkin collector",
									"type": "object",
									"required": [
										"collector_url",
										"service_name"
									],
									"properties": {
										"collector_url": {
											"title": "Collector URL",
											"description": "The URL (including port and path) where your Zipkin is accepting the spans, e.g., `http://zipkin:9411/api/v2/spans`\n\nSee: https://www.krakend.io/docs/telemetry/zipkin/",
											"examples": [
												"http://192.168.99.100:9411/api/v2/spans"
											],
											"type": "string"
										},
										"service_name": {
											"title": "Service name",
											"description": "The service name registered in Zipkin.\n\nSee: https://www.krakend.io/docs/telemetry/zipkin/",
											"examples": [
												"krakend"
											],
											"type": "string"
										}
									},
									"patternProperties": {
										"^[@$_#]": {}
									},
									"additionalProperties": false
								}
							},
							"patternProperties": {
								"^[@$_#]": {}
							},
							"additionalProperties": false
						},
						"reporting_period": {
							"title": "Reporting period",
							"description": "The number of seconds passing between reports. If duration is less than or equal to zero, it enables the default behavior of each exporter.\n\nSee: https://www.krakend.io/docs/telemetry/opencensus/",
							"default": 0,
							"type": "integer"
						},
						"sample_rate": {
							"title": "Sample rate",
							"description": "A number between 0 (no requests at all) and 100 (all requests) representing the percentage of sampled requests you want to send to the exporter. **Sampling the 100% of the requests is generally discouraged** when the relationship between traffic and dedicated resources is sparse.\n\nSee: https://www.krakend.io/docs/telemetry/opencensus/",
							"default": 0,
							"type": "integer",
							"maximum": 100,
							"minimum": 0
						}
					},
					"patternProperties": {
						"^[@$_#]": {}
					},
					"additionalProperties": false
				},
				"https://www.krakend.io/schema/v2.7/telemetry/opentelemetry-backend.json": {
					"$schema": "http://json-schema.org/draft-07/schema#",
					"title": "OpenTelemetry Backend Override",
					"description": "Enterprise only. Overrides metrics and traces declared by the OpenTelemetry service.\n\nSee: https://www.krakend.io/docs/telemetry/opentelemetry-by-endpoint/",
					"type": "object",
					"properties": {
						"backend": {
							"title": "Report backend activity",
							"description": "Reports the activity between KrakenD and each of your backend services. This is the more granular layer.\n\nSee: https://www.krakend.io/docs/telemetry/opentelemetry/",
							"type": "object",
							"properties": {
								"metrics": {
									"type": "object",
									"properties": {
										"disable_stage": {
											"title": "Disable this stage",
											"description": "Whether to turn off the metrics or not. Setting this to `true` means stop reporting any data.\n\nSee: https://www.krakend.io/docs/telemetry/opentelemetry/",
											"default": false,
											"type": "boolean"
										}
									},
									"patternProperties": {
										"^[@$_#]": {}
									},
									"additionalProperties": false
								},
								"traces": {
									"type": "object",
									"properties": {
										"disable_stage": {
											"title": "Disable this stage",
											"description": "Whether to turn off the traces or not. Setting this to `true` means stop reporting any data.\n\nSee: https://www.krakend.io/docs/telemetry/opentelemetry/",
											"default": false,
											"type": "boolean"
										}
									},
									"patternProperties": {
										"^[@$_#]": {}
									},
									"additionalProperties": false
								}
							},
							"patternProperties": {
								"^[@$_#]": {}
							},
							"additionalProperties": false
						},
						"proxy": {
							"title": "Report proxy activity",
							"description": "Reports the activity at the beginning of the proxy layer, including spawning the required requests to multiple backends, merging, endpoint transformation and any other internals of the proxy between the request processing and the backend communication\n\nSee: https://www.krakend.io/docs/telemetry/opentelemetry-by-endpoint/",
							"type": "object",
							"properties": {
								"disable_metrics": {
									"title": "Disable metrics",
									"description": "Whether you want to disable all metrics in this endpoint or not.\n\nSee: https://www.krakend.io/docs/telemetry/opentelemetry-by-endpoint/",
									"default": false,
									"type": "boolean"
								},
								"disable_traces": {
									"title": "Disable traces",
									"description": "Whether you want to disable all traces in this endpoint or not.\n\nSee: https://www.krakend.io/docs/telemetry/opentelemetry-by-endpoint/",
									"default": false,
									"type": "boolean"
								}
							},
							"patternProperties": {
								"^[@$_#]": {}
							},
							"additionalProperties": false
						}
					},
					"patternProperties": {
						"^[@$_#]": {}
					},
					"additionalProperties": false
				},
				"https://www.krakend.io/schema/v2.7/telemetry/opentelemetry-endpoint.json": {
					"$schema": "http://json-schema.org/draft-07/schema#",
					"title": "OpenTelemetry Endpoint Override",
					"description": "Enterprise only. Overrides metrics and traces declared by the OpenTelemetry service.\n\nSee: https://www.krakend.io/docs/telemetry/opentelemetry-by-endpoint/",
					"type": "object",
					"properties": {
						"exporters_override": {
							"title": "Exporters override",
							"description": "Override exporter configuration for this endpoint",
							"type": "object",
							"properties": {
								"metric_exporters": {
									"title": "Metrics exporters",
									"description": "Overrides the metrics exporters used in this endpoint",
									"examples": [
										[
											"local_prometheus"
										]
									],
									"type": "array"
								},
								"metric_reporting_period": {
									"title": "Reporting period",
									"description": "Override how often you want to report and flush the metrics in seconds.\n\nSee: https://www.krakend.io/docs/telemetry/opentelemetry-by-endpoint/",
									"type": "integer"
								},
								"trace_exporters": {
									"title": "Trace exporters",
									"description": "Overrides the trace exporters used in this endpoint",
									"examples": [
										[
											"debug_jaeger",
											"newrelic",
											"local_tempo"
										]
									],
									"type": "array"
								},
								"trace_sample_rate": {
									"title": "Trace sample rate",
									"description": "Overrides the sample rate for traces defines the percentage of reported traces. This option is key to reduce the amount of data generated (and resource usage), while you still can debug and troubleshoot issues. For instance, a number of `0.25` will report a 25% of the traces seen in the system.\n\nSee: https://www.krakend.io/docs/telemetry/opentelemetry-by-endpoint/",
									"examples": [
										0.25
									],
									"type": "number",
									"maximum": 1,
									"minimum": 0
								}
							},
							"patternProperties": {
								"^[@$_#]": {}
							},
							"additionalProperties": false
						},
						"proxy": {
							"title": "Report proxy activity",
							"description": "Reports the activity at the beginning of the proxy layer, including spawning the required requests to multiple backends, merging, endpoint transformation and any other internals of the proxy between the request processing and the backend communication\n\nSee: https://www.krakend.io/docs/telemetry/opentelemetry-by-endpoint/",
							"type": "object",
							"properties": {
								"disable_metrics": {
									"title": "Disable metrics",
									"description": "Whether you want to disable all metrics in this endpoint or not.\n\nSee: https://www.krakend.io/docs/telemetry/opentelemetry-by-endpoint/",
									"default": false,
									"type": "boolean"
								},
								"disable_traces": {
									"title": "Disable traces",
									"description": "Whether you want to disable all traces in this endpoint or not.\n\nSee: https://www.krakend.io/docs/telemetry/opentelemetry-by-endpoint/",
									"default": false,
									"type": "boolean"
								}
							},
							"patternProperties": {
								"^[@$_#]": {}
							},
							"additionalProperties": false
						}
					},
					"patternProperties": {
						"^[@$_#]": {}
					},
					"additionalProperties": false
				},
				"https://www.krakend.io/schema/v2.7/telemetry/opentelemetry-security.json": {
					"$schema": "http://json-schema.org/draft-07/schema#",
					"title": "OpenTelemetry Security",
					"description": "Enables the security layer needed to use OpenTelemetry through the Internet, like pushing data to a SaaS provider.\n\nSee: https://www.krakend.io/docs/telemetry/opentelemetry-security/",
					"type": "object",
					"required": [
						"otlp"
					],
					"properties": {
						"otlp": {
							"title": "OTLP exporters",
							"description": "The list of OTLP exporters that require authentication. Set at least one object to push metrics and traces to an external collector using OTLP.\n\nSee: https://www.krakend.io/docs/telemetry/opentelemetry-security/",
							"type": "array",
							"minItems": 1,
							"items": {
								"required": [
									"name",
									"headers"
								],
								"properties": {
									"headers": {
										"title": "Headers",
										"description": "The custom headers you will send to authenticate requests. Each key is the header name you will add to all outgoing reports.\n\nSee: https://www.krakend.io/docs/telemetry/opentelemetry-security/",
										"type": "object",
										"properties": {
											".*": {
												"title": "Header value",
												"description": "The value of the header, usually an API token.",
												"type": "string"
											}
										}
									},
									"name": {
										"title": "Name",
										"description": "The **exact name** you used to define the exporter under `telemetry/opentelemetry`.\n\nSee: https://www.krakend.io/docs/telemetry/opentelemetry-security/",
										"examples": [
											"newrelic",
											"remote_datadog"
										],
										"type": "string"
									}
								},
								"patternProperties": {
									"^[@$_#]": {}
								},
								"additionalProperties": false
							}
						}
					},
					"patternProperties": {
						"^[@$_#]": {}
					},
					"additionalProperties": false
				},
				"https://www.krakend.io/schema/v2.7/telemetry/opentelemetry.json": {
					"$schema": "http://json-schema.org/draft-07/schema#",
					"title": "OpenTelemetry",
					"description": "Enables metrics and traces using OpenTelemetry.\n\nSee: https://www.krakend.io/docs/telemetry/opentelemetry/",
					"type": "object",
					"required": [
						"exporters"
					],
					"properties": {
						"exporters": {
							"title": "Exporters",
							"description": "The places where you will send telemetry data. You can declare multiple exporters even when they are of the same type. For instance, when you have a self-hosted Grafana and would like to migrate to its cloud version and check the double reporting during the transition. There are two families of exporters: `otlp` or `prometheus`.\n\nSee: https://www.krakend.io/docs/telemetry/opentelemetry/",
							"type": "object",
							"minProperties": 1,
							"properties": {
								"otlp": {
									"title": "OTLP exporters",
									"description": "The list of OTLP exporters you want to use. Set at least one object to push metrics and traces to an external collector using OTLP.\n\nSee: https://www.krakend.io/docs/telemetry/opentelemetry/",
									"type": "array",
									"minItems": 1,
									"items": {
										"required": [
											"name",
											"host"
										],
										"properties": {
											"disable_metrics": {
												"title": "Disable metrics",
												"description": "Disable metrics in this exporter (leaving only traces if any). It won't report any metrics when the flag is `true`.\n\nSee: https://www.krakend.io/docs/telemetry/opentelemetry/",
												"default": false,
												"type": "boolean"
											},
											"disable_traces": {
												"title": "Disable traces",
												"description": "Disable traces in this exporter (leaving only metrics if any). It won't report any metrics when the flag is `true`.\n\nSee: https://www.krakend.io/docs/telemetry/opentelemetry/",
												"default": false,
												"type": "boolean"
											},
											"host": {
												"title": "Host",
												"description": "The host where you want to push the data. It can be a sidecar or a remote collector.\n\nSee: https://www.krakend.io/docs/telemetry/opentelemetry/",
												"type": "string"
											},
											"name": {
												"title": "Name",
												"description": "A unique name to identify this exporter.\n\nSee: https://www.krakend.io/docs/telemetry/opentelemetry/",
												"examples": [
													"local_prometheus",
													"remote_grafana"
												],
												"type": "string"
											},
											"port": {
												"title": "Port",
												"description": "A custom port to send the data. The port defaults to 4317 for gRPC unless you enable `use_http`, which defaults to 4318.\n\nSee: https://www.krakend.io/docs/telemetry/opentelemetry/",
												"default": 4317,
												"type": "integer",
												"maximum": 65535,
												"minimum": 0
											},
											"use_http": {
												"title": "Use HTTP",
												"description": "Whether this exporter uses HTTP instead of gRPC.\n\nSee: https://www.krakend.io/docs/telemetry/opentelemetry/",
												"type": "boolean"
											}
										},
										"patternProperties": {
											"^[@$_#]": {}
										},
										"additionalProperties": false
									}
								},
								"prometheus": {
									"title": "Prometheus exporter",
									"description": "Set here at least the settings for one Prometheus exporter. Each exporter will start a local port that offers metrics to be pulled from KrakenD.\n\nSee: https://www.krakend.io/docs/telemetry/opentelemetry/",
									"type": "array",
									"minItems": 1,
									"items": {
										"required": [
											"name"
										],
										"properties": {
											"disable_metrics": {
												"title": "Disable metrics",
												"description": "Leave this exporter declared but disabled (useful in development). It won't report any metrics when the flag is `true`.\n\nSee: https://www.krakend.io/docs/telemetry/opentelemetry/",
												"default": false,
												"type": "boolean"
											},
											"go_metrics": {
												"title": "Go Metrics",
												"description": "Whether you want fine-grained details of Go language metrics or not.\n\nSee: https://www.krakend.io/docs/telemetry/opentelemetry/",
												"type": "boolean"
											},
											"listen_ip": {
												"title": "Listen IP",
												"description": "The IP address that KrakenD listens to in IPv4 or IPv6. You can, for instance, expose the Prometheus metrics only in a private IP address. An empty string, or no declaration means listening on all interfaces. The inclusion of `::` is intended for IPv6 format only (**this is not the port**). Examples of valid addresses are `192.0.2.1` (IPv4), `2001:db8::68` (IPv6). The values `::` and `0.0.0.0` listen to all addresses, which are valid for IPv4 and IPv6 simultaneously.",
												"examples": [
													"172.12.1.1",
													"::1"
												],
												"default": "0.0.0.0",
												"type": "string"
											},
											"name": {
												"title": "Name",
												"description": "A unique name to identify this exporter.\n\nSee: https://www.krakend.io/docs/telemetry/opentelemetry/",
												"examples": [
													"local_prometheus",
													"remote_grafana"
												],
												"type": "string"
											},
											"port": {
												"title": "Port",
												"description": "The port in KrakenD where Prometheus will connect to.\n\nSee: https://www.krakend.io/docs/telemetry/opentelemetry/",
												"default": 9090,
												"type": "integer",
												"maximum": 65535,
												"minimum": 0
											},
											"process_metrics": {
												"title": "Process Metrics",
												"description": "Whether this exporter shows detailed metrics about the running process like CPU or memory usage or not.\n\nSee: https://www.krakend.io/docs/telemetry/opentelemetry/",
												"type": "boolean"
											}
										},
										"patternProperties": {
											"^[@$_#]": {}
										},
										"additionalProperties": false
									}
								}
							},
							"patternProperties": {
								"^[@$_#]": {}
							},
							"additionalProperties": false
						},
						"layers": {
							"title": "Layers",
							"description": "A request and response flow passes through three different layers. This attribute lets you specify what data you want to export in each layer. All layers are enabled by default unless you declare this section.\n\nSee: https://www.krakend.io/docs/telemetry/opentelemetry/",
							"type": "object",
							"properties": {
								"backend": {
									"title": "Report backend activity",
									"description": "Reports the activity between KrakenD and each of your backend services. This is the more granular layer.\n\nSee: https://www.krakend.io/docs/telemetry/opentelemetry/",
									"type": "object",
									"properties": {
										"metrics": {
											"type": "object",
											"properties": {
												"detailed_connection": {
													"title": "Detailed HTTP connection metrics",
													"description": "Whether you want to enable detailed metrics for the HTTP connection phase or not. Includes times to connect, DNS querying, and the TLS handshake.\n\nSee: https://www.krakend.io/docs/telemetry/opentelemetry/",
													"default": false,
													"type": "boolean"
												},
												"disable_stage": {
													"title": "Disable this stage",
													"description": "Whether to turn off the metrics or not. Setting this to `true` means stop reporting any data.\n\nSee: https://www.krakend.io/docs/telemetry/opentelemetry/",
													"default": false,
													"type": "boolean"
												},
												"read_payload": {
													"title": "Detailed payload read",
													"description": "Whether you want to enable metrics for the response reading payload or not (HTTP connection not taken into account).\n\nSee: https://www.krakend.io/docs/telemetry/opentelemetry/",
													"default": false,
													"type": "boolean"
												},
												"round_trip": {
													"title": "Detailed Round Trip",
													"description": "Whether you want to enable metrics for the actual HTTP request for the backend or not (manipulation not taken into account). This is the time your backend needs to produce a result.\n\nSee: https://www.krakend.io/docs/telemetry/opentelemetry/",
													"default": false,
													"type": "boolean"
												},
												"static_attributes": {
													"title": "Static attributes",
													"description": "A list of tags or labels you want to associate with these metrics.\n\nSee: https://www.krakend.io/docs/telemetry/opentelemetry/",
													"examples": [
														[
															{
																"key": "my_metric_attr",
																"value": "my_metric_val"
															}
														]
													],
													"type": "array",
													"items": {
														"type": "object",
														"required": [
															"key",
															"value"
														],
														"properties": {
															"key": {
																"title": "Key",
																"description": "The key, tag, or label name you want to use.\n\nSee: https://www.krakend.io/docs/telemetry/opentelemetry/",
																"type": "string"
															},
															"value": {
																"title": "Value",
																"description": "The static value you want to assign to this key.\n\nSee: https://www.krakend.io/docs/telemetry/opentelemetry/",
																"type": "string"
															}
														},
														"patternProperties": {
															"^[@$_#]": {}
														},
														"additionalProperties": false
													}
												}
											},
											"patternProperties": {
												"^[@$_#]": {}
											},
											"additionalProperties": false
										},
										"traces": {
											"type": "object",
											"properties": {
												"detailed_connection": {
													"title": "Detailed HTTP connection",
													"description": "Whether you want to add detailed trace attributes for the HTTP connection phase or not. Includes times to connect, DNS querying, and the TLS handshake.\n\nSee: https://www.krakend.io/docs/telemetry/opentelemetry/",
													"default": false,
													"type": "boolean"
												},
												"disable_stage": {
													"title": "Disable this stage",
													"description": "Whether to turn off the traces or not. Setting this to `true` means stop reporting any data.\n\nSee: https://www.krakend.io/docs/telemetry/opentelemetry/",
													"default": false,
													"type": "boolean"
												},
												"read_payload": {
													"title": "Detailed payload read",
													"description": "Whether you want to add trace attributes for the response reading payload or not (HTTP connection not taken into account).\n\nSee: https://www.krakend.io/docs/telemetry/opentelemetry/",
													"default": false,
													"type": "boolean"
												},
												"report_headers": {
													"title": "Report headers",
													"description": "Whether you want to report the final headers that reached the backend.\n\nSee: https://www.krakend.io/docs/telemetry/opentelemetry/",
													"default": false,
													"type": "boolean"
												},
												"round_trip": {
													"title": "Detailed Round Trip",
													"description": "Whether you want to add trace attributes for the actual HTTP request for the backend or not (manipulation not taken into account). This is the time your backend needs to produce a result.\n\nSee: https://www.krakend.io/docs/telemetry/opentelemetry/",
													"default": false,
													"type": "boolean"
												},
												"static_attributes": {
													"title": "Static attributes",
													"description": "A list of tags or labels you want to associate to these traces.\n\nSee: https://www.krakend.io/docs/telemetry/opentelemetry/",
													"examples": [
														[
															{
																"key": "my_trace_attr",
																"value": "my_trace_val"
															}
														]
													],
													"type": "array",
													"items": {
														"type": "object",
														"required": [
															"key",
															"value"
														],
														"properties": {
															"key": {
																"title": "Key",
																"description": "The key, tag, or label name you want to use.\n\nSee: https://www.krakend.io/docs/telemetry/opentelemetry/",
																"type": "string"
															},
															"value": {
																"title": "Value",
																"description": "The static value you want to assign to this key.\n\nSee: https://www.krakend.io/docs/telemetry/opentelemetry/",
																"type": "string"
															}
														},
														"patternProperties": {
															"^[@$_#]": {}
														},
														"additionalProperties": false
													}
												}
											},
											"patternProperties": {
												"^[@$_#]": {}
											},
											"additionalProperties": false
										}
									},
									"patternProperties": {
										"^[@$_#]": {}
									},
									"additionalProperties": false
								},
								"global": {
									"title": "Report global activity",
									"description": "Reports the activity between end-users and KrakenD\n\nSee: https://www.krakend.io/docs/telemetry/opentelemetry/",
									"type": "object",
									"properties": {
										"disable_metrics": {
											"title": "Disable global metrics",
											"description": "Whether you want to disable all metrics happening in the global layer or not.\n\nSee: https://www.krakend.io/docs/telemetry/opentelemetry/",
											"default": false,
											"type": "boolean"
										},
										"disable_propagation": {
											"title": "Disable propagation",
											"description": "Whether you want to ignore previous propagation headers to KrakenD. When the flag is set to `true`, spans from a previous layer will never be linked to the KrakenD trace.\n\nSee: https://www.krakend.io/docs/telemetry/opentelemetry/",
											"default": false,
											"type": "boolean"
										},
										"disable_traces": {
											"title": "Disable global trace",
											"description": "Whether you want to disable all traces happening in the global layer or not.\n\nSee: https://www.krakend.io/docs/telemetry/opentelemetry/",
											"default": false,
											"type": "boolean"
										},
										"metrics_static_attributes": {
											"title": "Static attributes",
											"description": "Static attributes you want to pass for metrics.",
											"type": "array",
											"items": {
												"properties": {
													"key": {
														"title": "Key",
														"description": "The key of the static attribute you want to send",
														"type": "string"
													},
													"value": {
														"title": "Value",
														"description": "The value of the static attribute you want to send",
														"type": "string"
													}
												},
												"additionalProperties": false
											}
										},
										"report_headers": {
											"title": "Report headers",
											"description": "Whether you want to send all headers that the consumer passed in the request or not.\n\nSee: https://www.krakend.io/docs/telemetry/opentelemetry/",
											"default": false,
											"type": "boolean"
										},
										"traces_static_attributes": {
											"title": "Static attributes",
											"description": "Static attributes you want to pass for traces.",
											"type": "array",
											"items": {
												"properties": {
													"key": {
														"title": "Key",
														"description": "The key of the static attribute you want to send",
														"type": "string"
													},
													"value": {
														"title": "Value",
														"description": "The value of the static attribute you want to send",
														"type": "string"
													}
												},
												"additionalProperties": false
											}
										}
									},
									"patternProperties": {
										"^[@$_#]": {}
									},
									"additionalProperties": false
								},
								"proxy": {
									"title": "Report proxy activity",
									"description": "Reports the activity at the beginning of the proxy layer, including spawning the required requests to multiple backends, merging, endpoint transformation and any other internals of the proxy between the request processing and the backend communication\n\nSee: https://www.krakend.io/docs/telemetry/opentelemetry/",
									"type": "object",
									"properties": {
										"disable_metrics": {
											"title": "Disable proxy metrics",
											"description": "Whether you want to disable all metrics happening in the proxy layer or not.\n\nSee: https://www.krakend.io/docs/telemetry/opentelemetry/",
											"default": false,
											"type": "boolean"
										},
										"disable_traces": {
											"title": "Disable proxy trace",
											"description": "Whether you want to disable all traces happening in the proxy layer or not.\n\nSee: https://www.krakend.io/docs/telemetry/opentelemetry/",
											"default": false,
											"type": "boolean"
										},
										"metrics_static_attributes": {
											"title": "Static attributes",
											"description": "Static attributes you want to pass for metrics.",
											"type": "array",
											"items": {
												"properties": {
													"key": {
														"title": "Key",
														"description": "The key of the static attribute you want to send",
														"type": "string"
													},
													"value": {
														"title": "Value",
														"description": "The value of the static attribute you want to send",
														"type": "string"
													}
												},
												"additionalProperties": false
											}
										},
										"report_headers": {
											"title": "Report headers",
											"description": "Whether you want to report all headers that passed from the request to the proxy layer (`input_headers` policy in the endpoint plus KrakenD's headers).\n\nSee: https://www.krakend.io/docs/telemetry/opentelemetry/",
											"default": false,
											"type": "boolean"
										},
										"traces_static_attributes": {
											"title": "Static attributes",
											"description": "Static attributes you want to pass for traces.",
											"type": "array",
											"items": {
												"properties": {
													"key": {
														"title": "Key",
														"description": "The key of the static attribute you want to send",
														"type": "string"
													},
													"value": {
														"title": "Value",
														"description": "The value of the static attribute you want to send",
														"type": "string"
													}
												},
												"additionalProperties": false
											}
										}
									},
									"patternProperties": {
										"^[@$_#]": {}
									},
									"additionalProperties": false
								}
							},
							"patternProperties": {
								"^[@$_#]": {}
							},
							"additionalProperties": false
						},
						"metric_reporting_period": {
							"title": "Reporting period",
							"description": "How often you want to report and flush the metrics in seconds. This setting is only used by `otlp` exporters.\n\nSee: https://www.krakend.io/docs/telemetry/opentelemetry/",
							"default": 30,
							"type": "integer"
						},
						"service_name": {
							"title": "Service Name",
							"description": "A friendly name identifying metrics reported by this installation. When unset, it uses the `name` attribute in the root level of the configuration.\n\nSee: https://www.krakend.io/docs/telemetry/opentelemetry/",
							"type": "string"
						},
						"service_version": {
							"title": "Service Version",
							"description": "The version you are deploying, this can be useful for deployment tracking.\n\nSee: https://www.krakend.io/docs/telemetry/opentelemetry/",
							"type": "string"
						},
						"skip_paths": {
							"title": "Skip Paths",
							"description": "The paths you don't want to report. Use the literal value used in the `endpoint` definition, including any `{placeholders}`. In the `global` layer, this attribute works only on metrics, because traces are initiated before there is an endpoint to match against. If you do not want any path skipped, just add an array with an empty string `[\"\"]`.\n\nSee: https://www.krakend.io/docs/telemetry/opentelemetry/",
							"examples": [
								"/foo/{bar}"
							],
							"default": [
								"/__health",
								"/__debug/",
								"/__echo/",
								"/__stats/"
							],
							"type": "array",
							"items": {
								"type": "string"
							}
						},
						"trace_sample_rate": {
							"title": "Trace sample rate",
							"description": "The sample rate for traces defines the percentage of reported traces. This option is key to reduce the amount of data generated (and resource usage), while you still can debug and troubleshoot issues. For instance, a number of `0.25` will report a 25% of the traces seen in the system.\n\nSee: https://www.krakend.io/docs/telemetry/opentelemetry/",
							"examples": [
								0.25
							],
							"default": 1,
							"type": "number",
							"maximum": 1,
							"minimum": 0
						}
					},
					"patternProperties": {
						"^[@$_#]": {}
					},
					"additionalProperties": false
				},
				"https://www.krakend.io/schema/v2.7/timeunits.json": {
					"$schema": "http://json-schema.org/draft-07/schema#",
					"title": "Time units / Duration patterns",
					"$defs": {
						"timeunit": {
							"title": "Duration",
							"description": "The amount of time you want to assign followed by its unit (e.g.: `2s`, `200ms`). Valid time units are: ns, us, (or µs), ms, s, m, h.",
							"type": "string",
							"pattern": "^[0-9]+(ns|ms|us|µs|s|m|h)$"
						}
					}
				},
				"https://www.krakend.io/schema/v2.7/tls.json": {
					"$schema": "http://json-schema.org/draft-07/schema#",
					"title": "TLS/SSL",
					"description": "Enabling TLS for HTTPS and HTTP/2.\n\nSee: https://www.krakend.io/docs/service-settings/tls/",
					"anyOf": [
						{
							"type": "object",
							"required": [
								"keys"
							]
						},
						{
							"type": "null"
						}
					],
					"properties": {
						"ca_certs": {
							"title": "CA certificates (for mTLS)",
							"description": "An array with all the CA certificates you would like to load to KrakenD **when using mTLS**, in addition to the certificates present in the system's CA. Each certificate in the list is a relative or absolute path to the PEM file. If you have a format other than PEM, you must convert the certificate to PEM using a conversion tool. See also `disable_system_ca_pool` to avoid system's CA.\n\nSee: https://www.krakend.io/docs/authorization/mutual-authentication/",
							"examples": [
								[
									"ca.pem"
								]
							],
							"default": [],
							"type": "array"
						},
						"cipher_suites": {
							"title": "Cipher Suites",
							"description": "The list of cipher suites as defined in the documentation.\n\nSee: https://www.krakend.io/docs/service-settings/tls/",
							"default": [
								4865,
								4866,
								4867
							],
							"type": "array",
							"uniqueItems": true
						},
						"curve_preferences": {
							"title": "Curve identifiers",
							"description": "The list of all the identifiers for the curve preferences. Use `23` for CurveP256, `24` for CurveP384 or `25` for CurveP521.\n\nSee: https://www.krakend.io/docs/service-settings/tls/",
							"default": [
								23,
								24,
								25
							],
							"type": "array",
							"uniqueItems": true,
							"items": {
								"enum": [
									23,
									24,
									25
								]
							}
						},
						"disable_system_ca_pool": {
							"title": "Disable system's CA",
							"description": "Ignore any certificate in the system's CA. The only certificates loaded will be the ones in the `ca_certs` list when true.\n\nSee: https://www.krakend.io/docs/service-settings/http-server-settings/",
							"default": false,
							"type": "boolean"
						},
						"disabled": {
							"title": "Disable TLS",
							"description": "A flag to disable TLS (useful while in development).\n\nSee: https://www.krakend.io/docs/service-settings/tls/",
							"default": false,
							"type": "boolean"
						},
						"enable_mtls": {
							"title": "Enable Mutual Authentication",
							"description": "Whether to enable or not Mutual Authentication. When mTLS is enabled, **all KrakenD endpoints** require clients to provide a known client-side X.509 authentication certificate. KrakenD relies on the system’s CA to validate certificates.\n\nSee: https://www.krakend.io/docs/authorization/mutual-authentication/",
							"default": false,
							"type": "boolean"
						},
						"keys": {
							"description": "An array with all the key pairs you want the TLS to work with. You can support multiple and unrelated domains in a single process.",
							"type": "array",
							"minItems": 1,
							"items": {
								"properties": {
									"private_key": {
										"title": "Private key",
										"description": "Absolute path to the private key, or relative to the current [working directory](https://www.krakend.io/docs/configuration/working-directory/).\n\nSee: https://www.krakend.io/docs/service-settings/tls/",
										"examples": [
											"/path/to/key.pem",
											"./certs/key.pem"
										],
										"default": "./certs/key.pem",
										"type": "string"
									},
									"public_key": {
										"title": "Public key",
										"description": "Absolute path to the public key, or relative to the current [working directory](https://www.krakend.io/docs/configuration/working-directory/).\n\nSee: https://www.krakend.io/docs/service-settings/tls/",
										"examples": [
											"/path/to/cert.pem",
											"./certs/cert.pem"
										],
										"default": "./certs/cert.pem",
										"type": "string"
									}
								}
							}
						},
						"max_version": {
							"title": "Maximum TLS version",
							"description": "Maximum TLS version supported.\n\nSee: https://www.krakend.io/docs/service-settings/tls/",
							"default": "TLS13",
							"enum": [
								"SSL3.0",
								"TLS10",
								"TLS11",
								"TLS12",
								"TLS13"
							]
						},
						"min_version": {
							"title": "Minimum TLS version",
							"description": "Minimum TLS version supported. When specifiying very old and insecure versions under TLS12 you must provide the `ciphers_list`.\n\nSee: https://www.krakend.io/docs/service-settings/tls/",
							"default": "TLS13",
							"enum": [
								"SSL3.0",
								"TLS10",
								"TLS11",
								"TLS12",
								"TLS13"
							]
						},
						"private_key": {
							"description": "Declaration of the `private_key` under the `tls` object is now deprecated. Please move this attribute inside the `keys` array.",
							"deprecated": true,
							"type": "string"
						},
						"public_key": {
							"description": "Declaration of the `public_key` under the `tls` object is now deprecated. Please move this attribute inside the `keys` array.",
							"deprecated": true,
							"type": "string"
						}
					},
					"patternProperties": {
						"^[@$_#]": {}
					},
					"additionalProperties": false
				},
				"https://www.krakend.io/schema/v2.7/validation/cel.json": {
					"$schema": "http://json-schema.org/draft-07/schema#",
					"title": "Common Expression Language (CEL) validations",
					"description": "The Common Expression Language (CEL) middleware enables expression evaluation, when an expression returns false, KrakenD does not return the content as the condition has failed. Otherwise, if all expressions returned true, the content is served.\n\nSee: https://www.krakend.io/docs/endpoints/common-expression-language-cel/",
					"type": "array",
					"minItems": 1,
					"items": {
						"title": "Object in array",
						"type": "object",
						"required": [
							"check_expr"
						],
						"properties": {
							"check_expr": {
								"title": "Check expression",
								"description": "The expression that evaluates as a boolean, you can write here any conditional. If the result of the expression is `true`, the execution continues. See in the docs how to use additional variables to retrieve data from requests, responses, and tokens.\n\nSee: https://www.krakend.io/docs/endpoints/common-expression-language-cel/",
								"examples": [
									"int(req_params.Id) % 3 == 0"
								],
								"type": "string"
							}
						},
						"patternProperties": {
							"^[@$_#]": {}
						},
						"additionalProperties": false
					}
				},
				"https://www.krakend.io/schema/v2.7/websocket.json": {
					"$schema": "http://json-schema.org/draft-07/schema#",
					"title": "Schema definition for Websockets",
					"description": "Enterprise only. Enables websocket communication.\n\nSee: https://www.krakend.io/docs/enterprise/websockets/",
					"type": "object",
					"properties": {
						"backoff_strategy": {
							"title": "Backoff strategy",
							"description": "When the connection to your event source gets interrupted for whatever reason, KrakenD keeps trying to reconnect until it succeeds or until it reaches the max_retries. The backoff strategy defines the delay in seconds in between consecutive failed retries. Defaults to 'fallback'\n\nSee: https://www.krakend.io/docs/enterprise/websockets/",
							"default": "fallback",
							"enum": [
								"linear",
								"linear-jitter",
								"exponential",
								"exponential-jitter",
								"fallback"
							]
						},
						"connect_event": {
							"title": "Notify connections",
							"description": "Whether to send notification events to the backend or not when a user establishes a new Websockets connection.\n\nSee: https://www.krakend.io/docs/enterprise/websockets/",
							"default": false,
							"type": "boolean"
						},
						"disconnect_event": {
							"title": "Notify disconnections",
							"description": "Whether to send notification events to the backend or not when users disconnect from their Websockets connection.\n\nSee: https://www.krakend.io/docs/enterprise/websockets/",
							"default": false,
							"type": "boolean"
						},
						"enable_direct_communication": {
							"title": "Direct Communication (no multiplexing)",
							"description": "When the value is set to `true` the communication is set one to one, and disables multiplexing. One client to KrakenD opens one connection to the backend. This mode of operation is sub-optimal in comparison to multiplexing.\n\nSee: https://www.krakend.io/docs/enterprise/websockets/",
							"default": false,
							"type": "boolean"
						},
						"input_headers": {
							"title": "Allowed Headers In",
							"description": "Defines which input headers are allowed to pass to the backend. Notice that you need to declare the `input_headers` at the endpoint level too.\n\nSee: https://www.krakend.io/docs/enterprise/websockets/",
							"default": [],
							"type": "array",
							"uniqueItems": true,
							"items": {
								"examples": [
									"User-Agent",
									"Accept",
									"*"
								],
								"type": "string"
							}
						},
						"max_message_size": {
							"title": "Maximum message size",
							"description": "Sets the maximum size of messages **in bytes** sent by or returned to the client. Messages larger than this value are discarded by KrakenD and the client disconnected.\n\nSee: https://www.krakend.io/docs/enterprise/websockets/",
							"default": 512,
							"type": "integer"
						},
						"max_retries": {
							"title": "Max retries",
							"description": "The maximum number of times you will allow KrakenD to retry reconnecting to a broken websockets server. When the maximum retries are reached, the gateway gives up the connection for good. Minimum value is `1` retry, or use `<= 0` for unlimited retries.\n\nSee: https://www.krakend.io/docs/enterprise/websockets/",
							"default": 0,
							"type": "integer"
						},
						"message_buffer_size": {
							"title": "Message buffer size",
							"description": "Sets the maximum number of messages **each end-user** can have in the buffer waiting to be processed. As this is a per-end-user setting, you must forecast how many consumers of KrakenD websockets you will have. The default value may be too high (memory consumption) if you expect thousands of clients consuming simultaneously.\n\nSee: https://www.krakend.io/docs/enterprise/websockets/",
							"default": 256,
							"type": "integer"
						},
						"ping_period": {
							"title": "Ping frequency",
							"description": "Sets the time between pings checking the health of the system.\n\nSee: https://www.krakend.io/docs/enterprise/websockets/",
							"default": "54s",
							"$ref": "#/definitions/https%3A~1~1www.krakend.io~1schema~1v2.7~1timeunits.json/$defs/timeunit"
						},
						"pong_wait": {
							"title": "Pong timeout",
							"description": "Sets the maximum time KrakenD will until the pong times out.\n\nSee: https://www.krakend.io/docs/enterprise/websockets/",
							"default": "60s",
							"$ref": "#/definitions/https%3A~1~1www.krakend.io~1schema~1v2.7~1timeunits.json/$defs/timeunit"
						},
						"read_buffer_size": {
							"title": "Read buffer size",
							"description": "Connections buffer network input and output to reduce the number of system calls when reading messages. You can set the maximum buffer size for reading in bytes.\n\nSee: https://www.krakend.io/docs/enterprise/websockets/",
							"default": 1024,
							"type": "integer"
						},
						"return_error_details": {
							"title": "Return error details",
							"description": "Provides an error `{'error':'reason here'}` to the client when KrakenD was unable to send the message to the backend.\n\nSee: https://www.krakend.io/docs/enterprise/websockets/",
							"default": false,
							"type": "boolean"
						},
						"timeout": {
							"title": "Timeout",
							"description": "Sets the read timeout for the backend. After a read has timed out, the websocket connection is terminated and KrakenD will try to reconnect according the `backoff_strategy`. Minimum accepted time is one minute. This flag only applies when you use ' enable_direct_communication`.\n\nSee: https://www.krakend.io/docs/enterprise/websockets/",
							"default": "5m",
							"$ref": "#/definitions/https%3A~1~1www.krakend.io~1schema~1v2.7~1timeunits.json/$defs/timeunit"
						},
						"write_buffer_size": {
							"title": "Write buffer size",
							"description": "Connections buffer network input and output to reduce the number of system calls when writing messages. You can set the maximum buffer size for writing in bytes.\n\nSee: https://www.krakend.io/docs/enterprise/websockets/",
							"default": 1024,
							"type": "integer"
						},
						"write_wait": {
							"title": "Write timeout",
							"description": "Sets the maximum time KrakenD will wait until the write times out.\n\nSee: https://www.krakend.io/docs/enterprise/websockets/",
							"default": "10s",
							"$ref": "#/definitions/https%3A~1~1www.krakend.io~1schema~1v2.7~1timeunits.json/$defs/timeunit"
						}
					},
					"patternProperties": {
						"^[@$_#]": {}
					},
					"additionalProperties": false
				},
				"https://www.krakend.io/schema/v2.7/workflow.json": {
					"$schema": "http://json-schema.org/draft-07/schema#",
					"title": "Workflow Object",
					"type": "object",
					"required": [
						"endpoint",
						"backend"
					],
					"properties": {
						"backend": {
							"title": "Backend",
							"description": "List of all the [backend objects](https://www.krakend.io/docs/backends/) called within this workflow. Each backend can initiate another workflow if needed.",
							"type": "array",
							"minItems": 1,
							"items": {
								"$ref": "#/definitions/https%3A~1~1www.krakend.io~1schema~1v2.7~1backend.json",
								"type": "object"
							}
						},
						"concurrent_calls": {
							"title": "Concurrent calls",
							"description": "The concurrent requests are an excellent technique to improve the response times and decrease error rates by requesting in parallel the same information multiple times. Yes, you make the same request to several backends instead of asking to just one. When the first backend returns the information, the remaining requests are canceled.\n\nSee: https://www.krakend.io/docs/endpoints/concurrent-requests/",
							"default": 1,
							"type": "integer",
							"maximum": 5,
							"minimum": 1
						},
						"endpoint": {
							"title": "Workflow endpoint name",
							"description": "An endpoint name for the workflow that will be used in logs. The name will be appended to the string `/__workflow/` in the logs, and although it does not receive traffic under this route, it is necessary when you want to pass URL `{params}` to the nested backends.\n\nSee: https://www.krakend.io/docs/endpoints/",
							"examples": [
								"/workflow-1/{param1}"
							],
							"type": "string"
						},
						"extra_config": {
							"title": "Extra configuration",
							"description": "Configuration entries for additional components that are executed within this endpoint, during the request, response or merge operations.",
							"$ref": "#/definitions/https%3A~1~1www.krakend.io~1schema~1v2.7~1workflow_extra_config.json",
							"type": "object"
						},
						"ignore_errors": {
							"title": "Ignore errors",
							"description": "Allow the workflow to continue with the rest of declared actions when there are errors (like security policies, network errors, etc). The default behavior of KrakenD is to abort an execution that has errors as soon as possible. If you use conditional backends and similar approaches, you might want to allow the gateway to go through all steps.\n\nSee: https://www.krakend.io/docs/endpoints/",
							"default": false
						},
						"output_encoding": {
							"title": "Output encoding",
							"description": "The gateway can work with several content types, even allowing your clients to choose how to consume the content. See the [supported encodings](https://www.krakend.io/docs/endpoints/content-types/)",
							"default": "json",
							"enum": [
								"json",
								"json-collection",
								"fast-json",
								"xml",
								"negotiate",
								"string",
								"no-op"
							]
						},
						"timeout": {
							"title": "Timeout",
							"description": "The duration you write in the timeout represents the **whole duration of the pipe**, so it counts the time all your backends take to respond and the processing of all the components involved in the endpoint (the request, fetching data, manipulation, etc.). By default the timeout is taken from the parent endpoint, if redefined **make sure that is smaller than the endpoint's**",
							"examples": [
								"2s",
								"1500ms"
							],
							"$ref": "#/definitions/https%3A~1~1www.krakend.io~1schema~1v2.7~1timeunits.json/$defs/timeunit",
							"type": "string"
						}
					},
					"patternProperties": {
						"^[@$_#]": {}
					},
					"additionalProperties": false
				},
				"https://www.krakend.io/schema/v2.7/workflow_extra_config.json": {
					"$schema": "http://json-schema.org/draft-07/schema#",
					"title": "Schema definition for extra_config of workflows",
					"type": "object",
					"properties": {
						"modifier/jmespath": {
							"$ref": "#/definitions/https%3A~1~1www.krakend.io~1schema~1v2.7~1modifier~1jmespath.json"
						},
						"modifier/lua-proxy": {
							"$ref": "#/definitions/https%3A~1~1www.krakend.io~1schema~1v2.7~1modifier~1lua.json"
						},
						"modifier/request-body-generator": {
							"$ref": "#/definitions/https%3A~1~1www.krakend.io~1schema~1v2.7~1modifier~1body-generator.json"
						},
						"modifier/response-body-generator": {
							"$ref": "#/definitions/https%3A~1~1www.krakend.io~1schema~1v2.7~1modifier~1body-generator.json"
						},
						"plugin/req-resp-modifier": {
							"$ref": "#/definitions/https%3A~1~1www.krakend.io~1schema~1v2.7~1plugin~1req-resp-modifier.json"
						},
						"proxy": {
							"title": "Proxy",
							"type": "object",
							"properties": {
								"combiner": {
									"title": "Custom combiner",
									"description": "For custom builds of KrakenD only",
									"examples": [
										"combiner_name"
									],
									"type": "string"
								},
								"flatmap_filter": {
									"title": "Flatmap (Array manipulation)",
									"description": "The list of operations to **execute sequentially** (top down). Every operation is defined with an object containing two properties:\n\nSee: https://www.krakend.io/docs/backends/flatmap/",
									"$ref": "#/definitions/https%3A~1~1www.krakend.io~1schema~1v2.7~1proxy~1flatmap.json",
									"type": "array"
								},
								"sequential": {
									"title": "Sequential proxy",
									"description": "The sequential proxy allows you to chain backend requests, making calls dependent one of each other.\n\nSee: https://www.krakend.io/docs/endpoints/sequential-proxy/",
									"default": true,
									"type": "boolean"
								},
								"static": {
									"title": "Static response",
									"description": "The static proxy injects static data in the final response when the selected strategy matches.\n\nSee: https://www.krakend.io/docs/endpoints/static-proxy/",
									"type": "object",
									"required": [
										"data",
										"strategy"
									],
									"properties": {
										"data": {
											"title": "Data",
											"description": "The static data (as a JSON object) that you will return.\n\nSee: https://www.krakend.io/docs/endpoints/static-proxy/",
											"type": "object"
										},
										"strategy": {
											"title": "Strategy",
											"description": "One of the supported strategies\n\nSee: https://www.krakend.io/docs/endpoints/static-proxy/",
											"enum": [
												"always",
												"success",
												"complete",
												"errored",
												"incomplete"
											]
										}
									},
									"patternProperties": {
										"^[@$_#]": {}
									},
									"additionalProperties": false
								}
							},
							"patternProperties": {
								"^[@$_#]": {}
							},
							"additionalProperties": false
						},
						"security/policies": {
							"$ref": "#/definitions/https%3A~1~1www.krakend.io~1schema~1v2.7~1security~1policies.json"
						},
						"validation/cel": {
							"$ref": "#/definitions/https%3A~1~1www.krakend.io~1schema~1v2.7~1validation~1cel.json"
						},
						"validation/json-schema": {
							"title": "Validating the body with the JSON Schema",
							"description": "apply automatic validations using the JSON Schema vocabulary before the content passes to the backends. The json schema component allows you to define validation rules on the body, type definition, or even validate the fields' values.\n\nSee: https://www.krakend.io/docs/endpoints/json-schema/",
							"type": "object"
						}
					},
					"patternProperties": {
						"^[@$_#]": {}
					},
					"additionalProperties": false
				}
			}
		}

		"""
	And the input data value <inputData>
	And I generate a type for the schema
	And I construct an instance of the schema type from the data
	When I validate the instance
	Then the result will be <valid>

Examples:
	| inputData                                                                                                                                                                                                                                                                | valid |
	| {"$schema":"https://www.krakend.io/schema/v2.3/krakend.json","endpoints":[{"backend":[{"extra_config":{"modifier/lua-backend":{"allow_open_libs":true,"live":true,"post":"filter(request.load(), response.load())","sources":["./filter.lua"]}},"mapping":{"expandedBom":"collection"},"url_pattern":"/bom.json"}],"endpoint":"/using-lua","output_encoding":"json-collection"},{"backend":[{"url_pattern":"/bom.json"}],"endpoint":"/using-jmespath","extra_config":{"modifier/jmespath":{"expr":"expandedBom[*]"}},"output_encoding":"json-collection"},{"backend":[{"allow":["expandedBom"],"mapping":{"expandedBom":"collection"},"url_pattern":"/bom.json"}],"endpoint":"/using-allow","output_encoding":"json-collection"}],"host":["http://my_service:8080"],"name":"My KrakenD API Gateway","plugin":{"folder":"/opt/krakend/plugins/","pattern":".so"},"port":8080,"version":3} | true  |