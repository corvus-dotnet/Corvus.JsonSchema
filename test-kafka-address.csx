using Testcontainers.Kafka;
using System;
using System.Threading.Tasks;

var container = new KafkaBuilder()
    .WithImage("confluentinc/cp-kafka:7.8.0")
    .Build();

await container.StartAsync();

Console.WriteLine($"Bootstrap Address: {container.GetBootstrapAddress()}");
Console.WriteLine($"Hostname: {container.Hostname}");
Console.WriteLine($"Mapped Port: {container.GetMappedPublicPort(9092)}");

await container.DisposeAsync();
