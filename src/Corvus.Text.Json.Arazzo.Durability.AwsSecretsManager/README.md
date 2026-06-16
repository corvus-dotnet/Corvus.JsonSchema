# Corvus.Text.Json.Arazzo.Durability.AwsSecretsManager

An [AWS Secrets Manager](https://aws.amazon.com/secrets-manager/) `ISecretResolver` for Arazzo
source credentials (design §13). The runner dereferences an `awssm://` secret reference to live
secret material at transport-bind time; the control plane and the Arazzo durability stores never
read it.

A reference is `awssm://<secret-id-or-arn>[#<version-id>]` — the locator is the secret name or full
ARN, and an optional `#version-id` pins a specific `VersionId` (otherwise the current version,
`AWSCURRENT`, is read). Supply a caller-configured `IAmazonSecretsManager` client (region +
least-privileged, `secretsmanager:GetSecretValue`-only credentials).
