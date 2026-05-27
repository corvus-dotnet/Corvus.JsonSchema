using Testcontainers.Kafka;
using System;

Console.WriteLine($"KafkaPort: {KafkaBuilder.KafkaPort}");
Console.WriteLine($"BrokerPort: {KafkaBuilder.BrokerPort}");
try { Console.WriteLine($"ZookeeperPort: {KafkaBuilder.ZookeeperPort}"); } catch { Console.WriteLine("ZookeeperPort: N/A"); }
