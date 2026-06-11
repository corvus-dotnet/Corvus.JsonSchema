// <copyright file="ArazzoControlPlaneCatalogHandler.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using Corvus.Text.Json;
using Corvus.Text.Json.Arazzo.Durability;

namespace Corvus.Text.Json.Arazzo.Durability.ControlPlane.Server;

/// <summary>
/// Implements the generated <see cref="IApiCatalogHandler"/> over an <see cref="IWorkflowCatalogClient"/>,
/// mapping each catalog REST operation onto the corresponding catalog-client call and projecting the .NET
/// metadata records into the generated response models. The package and its individual documents are returned
/// as their stored JSON; the version-metadata responses carry the HATEOAS links the contract declares.
/// </summary>
public sealed class ArazzoControlPlaneCatalogHandler : IApiCatalogHandler
{
    private const string ProblemBase = "https://corvus-oss.org/arazzo/control-plane/problems/";

    private readonly IWorkflowCatalogClient catalog;

    /// <summary>Initializes a new instance of the <see cref="ArazzoControlPlaneCatalogHandler"/> class.</summary>
    /// <param name="catalog">The catalog client the endpoints delegate to.</param>
    public ArazzoControlPlaneCatalogHandler(IWorkflowCatalogClient catalog)
    {
        ArgumentNullException.ThrowIfNull(catalog);
        this.catalog = catalog;
    }

    /// <inheritdoc/>
    public async ValueTask<AddCatalogVersionResult> HandleAddCatalogVersionAsync(AddCatalogVersionParams parameters, JsonWorkspace workspace, CancellationToken cancellationToken = default)
    {
        Models.PostCatalogBody body = parameters.Body;
        if (parameters.Package.IsEmpty || body.Owner.IsUndefined())
        {
            return AddCatalogVersionResult.BadRequest(
                Problem("invalid-package", "Invalid submission", 400, "A 'package' file part and an 'owner' part are both required."), workspace);
        }

        CatalogOwner owner = ToOwner(body.Owner);
        IReadOnlyList<string>? tags = ToTags(body.Tags);

        try
        {
            CatalogVersion version = await this.catalog.AddAsync(parameters.Package, owner, tags, cancellationToken).ConfigureAwait(false);
            return AddCatalogVersionResult.Created(BuildSummary(version), workspace);
        }
        catch (ArgumentException ex)
        {
            return AddCatalogVersionResult.BadRequest(
                Problem("invalid-package", "Invalid package", 400, ex.Message), workspace);
        }
    }

    /// <inheritdoc/>
    public async ValueTask<SearchCatalogResult> HandleSearchCatalogAsync(SearchCatalogParams parameters, JsonWorkspace workspace, CancellationToken cancellationToken = default)
    {
        string? text = parameters.Q.IsNotUndefined() ? (string)parameters.Q : null;
        string? baseWorkflowId = parameters.BaseWorkflowId.IsNotUndefined() ? (string)parameters.BaseWorkflowId : null;
        IReadOnlyList<string>? tags = ToTags(parameters.Tag);
        CatalogStatus? status = parameters.Status.IsNotUndefined() ? Enum.Parse<CatalogStatus>((string)parameters.Status) : null;
        string? owner = parameters.Owner.IsNotUndefined() ? (string)parameters.Owner : null;
        int limit = parameters.Limit.IsNotUndefined() ? (int)parameters.Limit : 100;
        string? pageToken = parameters.PageToken.IsNotUndefined() ? (string)parameters.PageToken : null;

        CatalogPage page = await this.catalog.SearchAsync(
            new CatalogQuery(text, baseWorkflowId, tags, status, owner, limit, pageToken), cancellationToken).ConfigureAwait(false);
        return SearchCatalogResult.Ok(BuildPage(page), workspace);
    }

    /// <inheritdoc/>
    public async ValueTask<ListCatalogVersionsResult> HandleListCatalogVersionsAsync(ListCatalogVersionsParams parameters, JsonWorkspace workspace, CancellationToken cancellationToken = default)
    {
        string baseWorkflowId = (string)parameters.BaseWorkflowId;
        int limit = parameters.Limit.IsNotUndefined() ? (int)parameters.Limit : 100;
        string? pageToken = parameters.PageToken.IsNotUndefined() ? (string)parameters.PageToken : null;

        CatalogPage page = await this.catalog.SearchAsync(
            new CatalogQuery(BaseWorkflowId: baseWorkflowId, Limit: limit, ContinuationToken: pageToken), cancellationToken).ConfigureAwait(false);
        return ListCatalogVersionsResult.Ok(BuildPage(page), workspace);
    }

    /// <inheritdoc/>
    public async ValueTask<GetCatalogVersionResult> HandleGetCatalogVersionAsync(GetCatalogVersionParams parameters, JsonWorkspace workspace, CancellationToken cancellationToken = default)
    {
        string baseWorkflowId = (string)parameters.BaseWorkflowId;
        int versionNumber = (int)parameters.VersionNumber;
        CatalogVersion? version = await this.catalog.GetAsync(baseWorkflowId, versionNumber, cancellationToken).ConfigureAwait(false);
        return version is { } v
            ? GetCatalogVersionResult.Ok(BuildSummary(v), workspace)
            : GetCatalogVersionResult.NotFound(NotFoundProblem(baseWorkflowId, versionNumber), workspace);
    }

    /// <inheritdoc/>
    public async ValueTask<UpdateCatalogVersionResult> HandleUpdateCatalogVersionAsync(UpdateCatalogVersionParams parameters, JsonWorkspace workspace, CancellationToken cancellationToken = default)
    {
        string baseWorkflowId = (string)parameters.BaseWorkflowId;
        int versionNumber = (int)parameters.VersionNumber;
        Models.CatalogMetadataPatch patch = parameters.Body;

        CatalogOwner? owner = patch.Owner.IsNotUndefined() ? ToOwner(patch.Owner) : null;
        IReadOnlyList<string>? tags = ToTags(patch.Tags);
        CatalogStatus? status = patch.Status.IsNotUndefined() ? Enum.Parse<CatalogStatus>((string)patch.Status) : null;

        CatalogVersion? updated = await this.catalog.UpdateAsync(baseWorkflowId, versionNumber, owner, tags, status, cancellationToken).ConfigureAwait(false);
        return updated is { } v
            ? UpdateCatalogVersionResult.Ok(BuildSummary(v), workspace)
            : UpdateCatalogVersionResult.NotFound(NotFoundProblem(baseWorkflowId, versionNumber), workspace);
    }

    /// <inheritdoc/>
    public async ValueTask<DeleteCatalogVersionResult> HandleDeleteCatalogVersionAsync(DeleteCatalogVersionParams parameters, JsonWorkspace workspace, CancellationToken cancellationToken = default)
    {
        string baseWorkflowId = (string)parameters.BaseWorkflowId;
        int versionNumber = (int)parameters.VersionNumber;
        CatalogDeleteOutcome outcome = await this.catalog.DeleteAsync(baseWorkflowId, versionNumber, cancellationToken).ConfigureAwait(false);
        return outcome switch
        {
            CatalogDeleteOutcome.Deleted => DeleteCatalogVersionResult.NoContent(),
            CatalogDeleteOutcome.Referenced => DeleteCatalogVersionResult.Conflict(
                Problem("version-referenced", "Version is referenced", 409, $"Version {versionNumber} of '{baseWorkflowId}' cannot be deleted while workflow runs reference it; mark it obsolete instead."), workspace),
            _ => DeleteCatalogVersionResult.NotFound(NotFoundProblem(baseWorkflowId, versionNumber), workspace),
        };
    }

    /// <inheritdoc/>
    public async ValueTask<PurgeCatalogResult> HandlePurgeCatalogAsync(PurgeCatalogParams parameters, JsonWorkspace workspace, CancellationToken cancellationToken = default)
    {
        int purged = await this.catalog.PurgeAsync(cancellationToken).ConfigureAwait(false);
        return PurgeCatalogResult.Ok(
            new Models.PurgeResult.Source((ref Models.PurgeResult.Builder b) => b.Create(purgedCount: purged)), workspace);
    }

    /// <inheritdoc/>
    public async ValueTask<GetCatalogPackageResult> HandleGetCatalogPackageAsync(GetCatalogPackageParams parameters, JsonWorkspace workspace, CancellationToken cancellationToken = default)
    {
        string baseWorkflowId = (string)parameters.BaseWorkflowId;
        int versionNumber = (int)parameters.VersionNumber;
        ReadOnlyMemory<byte>? package = await this.catalog.GetPackageAsync(baseWorkflowId, versionNumber, cancellationToken).ConfigureAwait(false);
        if (package is not { } bytes)
        {
            return GetCatalogPackageResult.NotFound(NotFoundProblem(baseWorkflowId, versionNumber), workspace);
        }

        // The stored package is an opaque binary artifact streamed back as-is (octet-stream).
        return GetCatalogPackageResult.Ok(bytes);
    }

    /// <inheritdoc/>
    public async ValueTask<GetCatalogWorkflowResult> HandleGetCatalogWorkflowAsync(GetCatalogWorkflowParams parameters, JsonWorkspace workspace, CancellationToken cancellationToken = default)
    {
        string baseWorkflowId = (string)parameters.BaseWorkflowId;
        int versionNumber = (int)parameters.VersionNumber;
        ReadOnlyMemory<byte>? document = await this.catalog.GetDocumentAsync(baseWorkflowId, versionNumber, CatalogPackage.WorkflowDocumentName, cancellationToken).ConfigureAwait(false);
        if (document is not { } bytes)
        {
            return GetCatalogWorkflowResult.NotFound(NotFoundProblem(baseWorkflowId, versionNumber), workspace);
        }

        // Hand the parsed document to the response workspace so it lives until the response is written
        // (the result Body references it); the workspace disposes it — do not dispose it here.
        ParsedJsonDocument<Models.JsonObject> parsed = ParsedJsonDocument<Models.JsonObject>.Parse(bytes);
        workspace.TakeOwnership(parsed);
        return GetCatalogWorkflowResult.Ok(parsed.RootElement, workspace);
    }

    /// <inheritdoc/>
    public async ValueTask<GetCatalogSourceResult> HandleGetCatalogSourceAsync(GetCatalogSourceParams parameters, JsonWorkspace workspace, CancellationToken cancellationToken = default)
    {
        string baseWorkflowId = (string)parameters.BaseWorkflowId;
        int versionNumber = (int)parameters.VersionNumber;
        string sourceName = (string)parameters.SourceName;
        ReadOnlyMemory<byte>? document = await this.catalog.GetDocumentAsync(baseWorkflowId, versionNumber, sourceName, cancellationToken).ConfigureAwait(false);
        if (document is not { } bytes)
        {
            return GetCatalogSourceResult.NotFound(
                Problem("source-not-found", "Source not found", 404, $"Version {versionNumber} of '{baseWorkflowId}' has no source document named '{sourceName}'."), workspace);
        }

        // Hand the parsed document to the response workspace (see HandleGetCatalogWorkflowAsync) so it lives
        // until the response is written; the workspace disposes it.
        ParsedJsonDocument<Models.JsonObject> parsed = ParsedJsonDocument<Models.JsonObject>.Parse(bytes);
        workspace.TakeOwnership(parsed);
        return GetCatalogSourceResult.Ok(parsed.RootElement, workspace);
    }

    private static CatalogOwner ToOwner(Models.CatalogOwner owner)
        => new(
            Name: owner.Name.IsNotUndefined() ? (string)owner.Name : string.Empty,
            Email: owner.Email.IsNotUndefined() ? (string)owner.Email : string.Empty,
            Team: owner.Team.IsNotUndefined() ? (string)owner.Team : null,
            Url: owner.Url.IsNotUndefined() ? (string)owner.Url : null);

    private static IReadOnlyList<string>? ToTags(Models.PostCatalogBody.JsonStringArray tags)
    {
        if (tags.IsUndefined())
        {
            return null;
        }

        var list = new List<string>();
        foreach (Models.JsonString item in tags.EnumerateArray())
        {
            list.Add((string)item);
        }

        return list.Count > 0 ? list : null;
    }

    private static IReadOnlyList<string>? ToTags(Models.CatalogMetadataPatch.JsonStringArray tags)
    {
        if (tags.IsUndefined())
        {
            return null;
        }

        var list = new List<string>();
        foreach (Models.JsonString item in tags.EnumerateArray())
        {
            list.Add((string)item);
        }

        return list.Count > 0 ? list : null;
    }

    private static IReadOnlyList<string>? ToTags(Models.TagList tags)
    {
        if (tags.IsUndefined())
        {
            return null;
        }

        var list = new List<string>();
        foreach (Models.JsonString item in tags.EnumerateArray())
        {
            list.Add((string)item);
        }

        return list.Count > 0 ? list : null;
    }

    private static Models.CatalogVersionSummary.Source BuildSummary(CatalogVersion version)
        => new((ref Models.CatalogVersionSummary.Builder b) =>
        {
            Models.JsonString.Source description = version.Description is { } d ? (Models.JsonString.Source)d : default;
            Models.JsonString.Source lastUpdatedBy = version.LastUpdatedBy is { } lub ? (Models.JsonString.Source)lub : default;
            Models.JsonDateTime.Source lastUpdatedAt = version.LastUpdatedAt is { } lua ? (Models.JsonDateTime.Source)lua : default;
            Models.JsonString.Source obsoletedBy = version.ObsoletedBy is { } ob ? (Models.JsonString.Source)ob : default;
            Models.JsonDateTime.Source obsoletedAt = version.ObsoletedAt is { } oa ? (Models.JsonDateTime.Source)oa : default;

            b.Create(
                baseWorkflowId: version.BaseWorkflowId,
                createdAt: version.CreatedAt,
                createdBy: version.CreatedBy,
                hash: version.Hash,
                owner: BuildOwner(version.Owner),
                sources: BuildSources(version.Sources),
                status: version.Status.ToString(),
                tags: BuildTags(version.Tags),
                title: version.Title,
                versionNumber: version.VersionNumber,
                workflowId: version.WorkflowId,
                description: description,
                lastUpdatedAt: lastUpdatedAt,
                lastUpdatedBy: lastUpdatedBy,
                obsoletedAt: obsoletedAt,
                obsoletedBy: obsoletedBy);
        });

    private static Models.CatalogOwner.Source BuildOwner(CatalogOwner owner)
        => new((ref Models.CatalogOwner.Builder b) =>
        {
            Models.JsonString.Source team = owner.Team is { } t ? (Models.JsonString.Source)t : default;
            Models.JsonIri.Source url = owner.Url is { } u ? (Models.JsonIri.Source)u : default;
            b.Create(email: owner.Email, name: owner.Name, team: team, url: url);
        });

    private static Models.CatalogVersionSummary.CatalogSourceRefArray.Source BuildSources(IReadOnlyList<CatalogSourceRef> sources)
        => new((ref Models.CatalogVersionSummary.CatalogSourceRefArray.Builder ab) =>
        {
            foreach (CatalogSourceRef source in sources)
            {
                ab.AddItem(new Models.CatalogSourceRef.Source((ref Models.CatalogSourceRef.Builder cb) =>
                {
                    Models.JsonString.Source type = source.Type is { } t ? (Models.JsonString.Source)t : default;
                    cb.Create(name: source.Name, type: type);
                }));
            }
        });

    private static Models.CatalogVersionSummary.JsonStringArray.Source BuildTags(IReadOnlyList<string> tags)
        => new((ref Models.CatalogVersionSummary.JsonStringArray.Builder ab) =>
        {
            foreach (string tag in tags)
            {
                ab.AddItem(tag);
            }
        });

    private static Models.CatalogPage.Source BuildPage(CatalogPage page)
        => new((ref Models.CatalogPage.Builder b) =>
        {
            Models.JsonString.Source nextPageToken = page.ContinuationToken is { } token ? (Models.JsonString.Source)token : default;
            b.Create(
                versions: new Models.CatalogPage.CatalogVersionSummaryArray.Source(
                    (ref Models.CatalogPage.CatalogVersionSummaryArray.Builder ab) =>
                    {
                        foreach (CatalogVersion version in page.Versions)
                        {
                            ab.AddItem(BuildSummary(version));
                        }
                    }),
                nextPageToken: nextPageToken);
        });

    private static Models.ProblemDetails.Source NotFoundProblem(string baseWorkflowId, int versionNumber)
        => Problem("version-not-found", "Version not found", 404, $"No version {versionNumber} of workflow '{baseWorkflowId}' exists.");

    private static Models.ProblemDetails.Source Problem(string type, string title, int status, string detail)
        => new((ref Models.ProblemDetails.Builder b) => b.Create(
            detail: detail,
            status: status,
            title: title,
            type: ProblemBase + type));
}