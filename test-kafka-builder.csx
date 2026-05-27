using Testcontainers.Kafka;
using System;
using System.Threading.Tasks;

var container = new KafkaBuilder("apache/kafka:3.8.1").Build();
await container.StartAsync();
Console.WriteLine("Bootstrap: " + container.GetBootstrapAddress());
Console.WriteLine("Hostname: " + container.Hostname);
Console.WriteLine("Port: " + container.GetMappedPublicPort(9092));
await container.DisposeAsync();
