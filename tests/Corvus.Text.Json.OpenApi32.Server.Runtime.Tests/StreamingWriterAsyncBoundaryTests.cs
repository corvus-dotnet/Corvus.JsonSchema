// <copyright file="StreamingWriterAsyncBoundaryTests.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Buffers;
using System.IO.Pipelines;
using System.Runtime.CompilerServices;
using Corvus.Text.Json;

namespace Corvus.Text.Json.OpenApi32.Server.Runtime.Tests;

/// <summary>
/// Regression tests for issue #814: the OpenAPI 3.2 streaming response path must use a
/// JSON writer that can be safely held across an <see langword="await"/> boundary, because
/// the streaming continuation can resume on a different thread pool thread.
/// </summary>
[TestClass]
public sealed class StreamingWriterAsyncBoundaryTests
{
    /// <summary>
    /// Reproduces the exact writer lifecycle the generated streaming endpoint uses: obtain a
    /// writer from the workspace, <see langword="await"/> while still holding it (the
    /// streaming continuation resumes on a different thread), then release it in the finally
    /// block — on whatever thread the continuation landed on.
    /// </summary>
    /// <remarks>
    /// The continuation is forced onto a brand-new thread that has never touched the
    /// <c>Utf8JsonWriterCache</c> thread-local state, mirroring an ASP.NET Core continuation
    /// that migrates threads under a real (back-pressured) pipe. Before the fix the workspace
    /// handed out a thread-affine cached writer and the cross-thread <c>ReturnWriter</c>
    /// corrupted the cache; the writer now comes from <see cref="JsonWorkspace.CreateWriter"/>
    /// and is disposed, which has no thread affinity.
    /// </remarks>
    [TestMethod]
    public async Task StreamingWriter_HeldAcrossAwaitResumingOnAnotherThread_IsReleasedSafely()
    {
        Pipe pipe = new();
        using JsonWorkspace workspace = JsonWorkspace.CreateUnrented();

        int rentThreadId = Environment.CurrentManagedThreadId;
        int releaseThreadId = rentThreadId;

        // Mirror of the generated streaming endpoint body.
        Utf8JsonWriter writer = workspace.CreateWriter(pipe.Writer);
        try
        {
            // The generated code awaits result.WriteStreamAsync(...) here while still holding
            // the writer. Force the continuation onto a fresh thread to model thread migration.
            await ForceContinuationOnFreshThread();

            releaseThreadId = Environment.CurrentManagedThreadId;

            writer.WriteStartObject();
            writer.WriteString("status", "ok");
            writer.WriteEndObject();
            writer.Flush();
        }
        finally
        {
            await writer.DisposeAsync();
        }

        await pipe.Writer.CompleteAsync();

        Assert.AreNotEqual(rentThreadId, releaseThreadId, "Test precondition failed: the continuation did not migrate threads.");

        ReadResult readResult = await pipe.Reader.ReadAsync();
        string body = System.Text.Encoding.UTF8.GetString(readResult.Buffer.ToArray());
        pipe.Reader.AdvanceTo(readResult.Buffer.End);
        await pipe.Reader.CompleteAsync();

        Assert.AreEqual("""{"status":"ok"}""", body);
    }

    /// <summary>
    /// Gets an awaitable whose continuation always runs on a brand-new background thread,
    /// guaranteeing that the code following the <see langword="await"/> resumes on a thread
    /// with no <c>Utf8JsonWriterCache</c> thread-local state.
    /// </summary>
    private static FreshThreadAwaitable ForceContinuationOnFreshThread() => default;

    private readonly struct FreshThreadAwaitable
    {
        public FreshThreadAwaiter GetAwaiter() => default;
    }

    private readonly struct FreshThreadAwaiter : ICriticalNotifyCompletion
    {
        // Always report "not completed" so the state machine is forced to suspend and hand the
        // continuation to OnCompleted, which we run on a fresh thread.
        public bool IsCompleted => false;

        public void GetResult()
        {
        }

        public void OnCompleted(Action continuation) => RunOnFreshThread(continuation);

        public void UnsafeOnCompleted(Action continuation) => RunOnFreshThread(continuation);

        private static void RunOnFreshThread(Action continuation)
        {
            Thread thread = new(() => continuation())
            {
                IsBackground = true,
                Name = "issue-814-fresh-continuation",
            };
            thread.Start();
        }
    }
}