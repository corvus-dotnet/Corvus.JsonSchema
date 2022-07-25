// <copyright file="BenchmarkBase.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

namespace Corvus.JsonSchema.Benchmarking.Benchmarks
{
    using System;
    using System.IO;
    using System.Net.Http;
    using System.Text;
    using System.Text.Json;
    using System.Threading.Tasks;
    using Corvus.Json;
    using Microsoft.Extensions.Configuration;
    using Microsoft.Extensions.DependencyInjection;
    using Newtonsoft.Json.Linq;
    using Newtonsoft.Json.Schema;

    /// <summary>
    /// Base class for validation benchmarks.
    /// </summary>
    public class BenchmarkBase
    {
        private IServiceProvider? serviceProvider;
        private IDocumentResolver? resolver;
        private IConfiguration? configuration;
        private JSchema? newtonsoftSchema;
        private bool? isValid;
        private string? data;
        private byte[]? dataBytes;

        /// <summary>
        /// Setup the benchmark.
        /// </summary>
        /// <param name="filename">The filename for the benchmark data.</param>
        /// <param name="referenceFragment">The pointer to the schema in the reference file.</param>
        /// <param name="inputDataReference">The pointer to the data in the reference file.</param>
        /// <param name="isValid">Whether the item should be valid or not.</param>
        /// <returns>A <see cref="Task"/> which completes once setup is complete.</returns>
        protected async Task GlobalSetup(string filename, string referenceFragment, string inputDataReference, bool isValid)
        {
            this.serviceProvider = CreateServices().BuildServiceProvider();
            this.resolver = this.serviceProvider.GetRequiredService<IDocumentResolver>();
            this.configuration = this.serviceProvider.GetRequiredService<IConfiguration>();
            JsonElement? schema = await this.GetElement(filename, referenceFragment).ConfigureAwait(false);
            if (schema is null)
            {
                throw new InvalidOperationException($"Unable to find the element in file '{filename}' at location '{referenceFragment}'");
            }

            IConfiguration configuration = this.serviceProvider.GetRequiredService<IConfiguration>();
            License.RegisterLicense(configuration["newtonsoft:licenseKey"]);

            JsonElement? data = await this.GetElement(filename, inputDataReference).ConfigureAwait(false);
            if (data is not JsonElement d)
            {
                throw new InvalidOperationException($"Unable to find the element in file '{filename}' at location '{inputDataReference}'");
            }

            this.data = d.GetRawText();
            this.dataBytes = Encoding.UTF8.GetBytes(this.data!);

            this.newtonsoftSchema = JSchema.Parse(schema.Value.ToString(), new JSchemaUrlResolver());
            this.isValid = isValid;
        }

        /// <summary>
        /// Validate using the Corvus type.
        /// </summary>
        /// <typeparam name="T">The type of <see cref="IJsonValue"/> to validate.</typeparam>
        protected void ValidateCorvusCore<T>()
            where T : struct, IJsonValue
        {
            var reader = new Utf8JsonReader(this.dataBytes!.AsSpan());
            _ = JsonDocument.TryParseValue(ref reader, out JsonDocument? document);
            T value = new JsonAny(document!.RootElement).As<T>();

            if (value.Validate().IsValid != this.isValid)
            {
                throw new InvalidOperationException("Unable to validate.");
            }
        }

        /// <summary>
        /// Validate using the Newtonsoft type.
        /// </summary>
        protected void ValidateNewtonsoftCore()
        {
            var newtonsoftJToken = JToken.Parse(this.data!);
            if (newtonsoftJToken is null || this.newtonsoftSchema is null || newtonsoftJToken.IsValid(this.newtonsoftSchema) != this.isValid)
            {
                throw new InvalidOperationException("Unable to validate.");
            }
        }

        private static IServiceCollection CreateServices()
        {
            var services = new ServiceCollection();

            services.AddTransient<IDocumentResolver>(serviceProvider => new CompoundDocumentResolver(new FakeWebDocumentResolver(serviceProvider.GetRequiredService<IConfiguration>()["jsonSchemaBuilderDriverSettings:remotesBaseDirectory"]), new FileSystemDocumentResolver(), new HttpClientDocumentResolver(new HttpClient())));
            services.AddTransient<IConfiguration>(_ =>
            {
                IConfigurationBuilder configurationBuilder = new ConfigurationBuilder();
                configurationBuilder.AddJsonFile("appsettings.json", true);
                configurationBuilder.AddJsonFile("appsettings.local.json", true);
                return configurationBuilder.Build();
            });

            return services;
        }

        private Task<JsonElement?> GetElement(string filename, string referenceFragment)
        {
            if (this.configuration is not IConfiguration config)
            {
                throw new InvalidOperationException("You must provide IConfiguration in the container");
            }

            if (this.resolver is not IDocumentResolver resolver)
            {
                throw new InvalidOperationException("You must provide IConfiguration in the container");
            }

            string baseDirectory = config["jsonSchemaBuilderDriverSettings:testBaseDirectory"];
            string path = Path.Combine(baseDirectory, filename);

            Console.WriteLine(path);

            return resolver.TryResolve(new JsonReference(path, referenceFragment));
        }
    }
}
