using System.Net;
using System.Text;

namespace Corvus.Text.Json.OpenApi.Playground.Services;

/// <summary>
/// Provides canned HTTP responses for generated OpenAPI clients running in the browser playground.
/// </summary>
public sealed class PlaygroundHttpMessageHandler : HttpMessageHandler
{
    /// <inheritdoc />
    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        string path = request.RequestUri?.AbsolutePath ?? "/";
        HttpResponseMessage response = (request.Method.Method, path) switch
        {
            ("GET", "/v1/pets") => JsonResponse(HttpStatusCode.OK, """
                [
                  { "id": 1, "name": "Fido", "tag": "dog" },
                  { "id": 2, "name": "Mittens", "tag": "cat" }
                ]
                """, ("x-next", "/v1/pets?limit=10&cursor=next")),

            ("POST", "/v1/pets") => JsonResponse(HttpStatusCode.Created, """
                { "id": 42, "name": "Fido", "tag": "dog" }
                """),

            ("GET", "/v1/pets/pet-123") => JsonResponse(HttpStatusCode.OK, """
                { "id": 123, "name": "Fido", "tag": "dog" }
                """),

            ("GET", "/v2/pets") => JsonResponse(HttpStatusCode.OK, AdvancedPetList, ("x-total-count", "2"), ("x-next", "/v2/pets?limit=10&cursor=next")),
            ("GET", string p) when p.StartsWith("/v2/pets/batch/", StringComparison.Ordinal) => JsonResponse(HttpStatusCode.OK, AdvancedPetList),
            ("GET", "/v2/pets/pet-42") => JsonResponse(HttpStatusCode.OK, AdvancedPet),
            ("POST", "/v2/pets") => JsonResponse(HttpStatusCode.Created, AdvancedPet),

            ("POST", "/v2/pets/pet-42/photos") => JsonResponse(HttpStatusCode.Created, """
                {
                  "photoId": "photo-7",
                  "petId": "pet-42",
                  "caption": "Bella at the park",
                  "isPrimary": true,
                  "uploadedAt": "2026-05-28T09:00:00Z"
                }
                """),

            ("GET", "/v2/photos/photo-7") => BinaryResponse(HttpStatusCode.OK, "application/octet-stream", [0x89, 0x50, 0x4E, 0x47]),

            ("POST", "/v2/pets/pet-42/chat") => TextResponse(HttpStatusCode.OK, "text/event-stream", """
                event: message
                data: {"id":"chunk-1","delta":"Bella looks healthy.","done":false}

                event: message
                data: {"id":"chunk-2","done":true}

                """),

            ("GET", "/v2/pets/pet-42/activity") => TextResponse(HttpStatusCode.OK, "application/x-ndjson", """
                {"eventId":"evt-1","type":"walk","timestamp":"2026-05-28T08:00:00Z","description":"Morning walk"}
                {"eventId":"evt-2","type":"feeding","timestamp":"2026-05-28T09:00:00Z","description":"Breakfast"}

                """),

            ("POST", "/v2/adoption/apply") => JsonResponse(HttpStatusCode.Accepted, """
                {
                  "applicationId": "app-123",
                  "status": "received",
                  "estimatedReviewDays": 7
                }
                """),

            _ => JsonResponse(HttpStatusCode.NotFound, """
                { "code": 404, "message": "No playground response is configured for this request." }
                """),
        };

        response.RequestMessage = request;
        return Task.FromResult(response);
    }

    private const string AdvancedPet =
        """
        {
          "id": 42,
          "name": "Bella",
          "breed": "golden retriever",
          "age": 2,
          "status": "available",
          "tags": ["dog", "friendly", "trained"],
          "photoIds": ["photo-7"]
        }
        """;

    private const string AdvancedPetList =
        """
        [
          {
            "id": 42,
            "name": "Bella",
            "breed": "golden retriever",
            "age": 2,
            "status": "available",
            "tags": ["dog", "friendly", "trained"],
            "photoIds": ["photo-7"]
          },
          {
            "id": 43,
            "name": "Milo",
            "breed": "labrador",
            "age": 4,
            "status": "pending",
            "tags": ["dog", "calm"],
            "photoIds": []
          }
        ]
        """;

    private static HttpResponseMessage JsonResponse(HttpStatusCode statusCode, string json, params (string Name, string Value)[] headers)
    {
        HttpResponseMessage response = TextResponse(statusCode, "application/json", json);
        foreach ((string name, string value) in headers)
        {
            response.Headers.TryAddWithoutValidation(name, value);
        }

        return response;
    }

    private static HttpResponseMessage TextResponse(HttpStatusCode statusCode, string mediaType, string content)
    {
        return new HttpResponseMessage(statusCode)
        {
            Content = new StringContent(content, Encoding.UTF8, mediaType),
        };
    }

    private static HttpResponseMessage BinaryResponse(HttpStatusCode statusCode, string mediaType, byte[] content)
    {
        var response = new HttpResponseMessage(statusCode)
        {
            Content = new ByteArrayContent(content),
        };
        response.Content.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue(mediaType);
        return response;
    }
}