// <copyright file="RefusalRecordLimiter.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

namespace Corvus.Text.Json.Arazzo.Durability.Security;

/// <summary>
/// Bounds how many refusal records one subject can put on the audit chain (ADR 0070). A refusal is an append to a signed
/// chain that any caller can cause, by asking for ids it cannot see, on the path every governance mutation queues
/// behind. Each subject's refusals are admitted up to a bound in a window; past it they are counted and not appended,
/// and the count is what gets recorded, once, when the window turns. The probe is still evidenced, with its volume,
/// and an enumeration cannot make the chain or the signing key the bottleneck.
/// </summary>
/// <remarks>
/// The table of subjects is bounded too, since in a deployment that authenticates nobody the subject is whatever the
/// caller says it is. When it is full and nothing in it has expired, further subjects share one bucket, which keeps
/// memory fixed and the cap in force at the cost of attributing that bucket's suppressed count to no one subject.
/// </remarks>
public sealed class RefusalRecordLimiter
{
    /// <summary>The default number of refusal records a subject may append in a window.</summary>
    public const int DefaultPerSubject = 60;

    /// <summary>The name the shared bucket's suppressed refusals are recorded under.</summary>
    public const string OverflowSubject = "(many subjects)";

    private readonly Lock sync = new();
    private readonly Dictionary<string, Window> windows = new(StringComparer.Ordinal);
    private readonly int perSubject;
    private readonly TimeSpan window;
    private readonly int maxSubjects;

    /// <summary>Initializes a new instance of the <see cref="RefusalRecordLimiter"/> class.</summary>
    /// <param name="perSubject">The number of refusal records a subject may append in a window.</param>
    /// <param name="window">The window (defaults to one minute).</param>
    /// <param name="maxSubjects">The number of subjects tracked at once.</param>
    public RefusalRecordLimiter(int perSubject = DefaultPerSubject, TimeSpan? window = null, int maxSubjects = 4096)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(perSubject, 1);
        ArgumentOutOfRangeException.ThrowIfLessThan(maxSubjects, 1);
        this.perSubject = perSubject;
        this.window = window ?? TimeSpan.FromMinutes(1);
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(this.window, TimeSpan.Zero);
        this.maxSubjects = maxSubjects;
    }

    /// <summary>Gets the window.</summary>
    public TimeSpan WindowLength => this.window;

    /// <summary>Decides whether a subject's refusal is appended, and reports what its last window suppressed.</summary>
    /// <param name="subject">The subject that was refused.</param>
    /// <param name="now">The time of the refusal.</param>
    /// <param name="suppressedBefore">The number of this subject's refusals that its previous window counted and did not append, where that window has just turned; otherwise zero. The caller records it.</param>
    /// <param name="recordedAs">The subject the count belongs to: <paramref name="subject"/>, or <see cref="OverflowSubject"/> where the table was full.</param>
    /// <returns><see langword="true"/> to append the refusal; <see langword="false"/> where it is over the bound and only counted.</returns>
    public bool Admit(string subject, DateTimeOffset now, out long suppressedBefore, out string recordedAs)
    {
        ArgumentNullException.ThrowIfNull(subject);
        lock (this.sync)
        {
            recordedAs = subject;
            if (!this.windows.TryGetValue(subject, out Window current))
            {
                // A full table is emptied by the sweep alone, which is what reports what the ended windows suppressed.
                // Evicting here would drop those counts, so until the next sweep further subjects share one bucket.
                if (this.windows.Count >= this.maxSubjects)
                {
                    recordedAs = OverflowSubject;
                    this.windows.TryGetValue(OverflowSubject, out current);
                }

                if (current.Start == default)
                {
                    current = new Window(now, 0, 0);
                }
            }

            suppressedBefore = 0;
            if (now - current.Start >= this.window)
            {
                suppressedBefore = current.Suppressed;
                current = new Window(now, 0, 0);
            }

            bool admit = current.Admitted < this.perSubject;
            this.windows[recordedAs] = admit
                ? current with { Admitted = current.Admitted + 1 }
                : current with { Suppressed = current.Suppressed + 1 };
            return admit;
        }
    }

    /// <summary>
    /// Takes every window that has ended out of the table, reporting those that suppressed anything. It is what records a
    /// flood's count when the subject that made it never asks again, and what keeps the table from holding subjects that
    /// have gone.
    /// </summary>
    /// <param name="now">The current time.</param>
    /// <param name="flushed">Receives each ended window's subject and suppressed count, where that count is not zero.</param>
    public void Sweep(DateTimeOffset now, List<KeyValuePair<string, long>> flushed)
    {
        ArgumentNullException.ThrowIfNull(flushed);
        lock (this.sync)
        {
            this.RemoveExpired(now, flushed);
        }
    }

    private void RemoveExpired(DateTimeOffset now, List<KeyValuePair<string, long>> flushed)
    {
        List<string>? expired = null;
        foreach (KeyValuePair<string, Window> entry in this.windows)
        {
            if (now - entry.Value.Start >= this.window)
            {
                (expired ??= []).Add(entry.Key);
                if (entry.Value.Suppressed > 0)
                {
                    flushed.Add(new KeyValuePair<string, long>(entry.Key, entry.Value.Suppressed));
                }
            }
        }

        if (expired is not null)
        {
            foreach (string subject in expired)
            {
                this.windows.Remove(subject);
            }
        }
    }

    private readonly record struct Window(DateTimeOffset Start, int Admitted, long Suppressed);
}