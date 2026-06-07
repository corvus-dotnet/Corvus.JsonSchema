// <copyright file="RecordedTelemetry.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Diagnostics;
using System.Diagnostics.Metrics;
using Corvus.Text.Json.Arazzo;

namespace Corvus.Text.Json.Arazzo.Testing;

/// <summary>
/// An in-memory OpenTelemetry capture harness for conformance tests: it listens to the Arazzo
/// <see cref="ActivitySource"/> and <see cref="Meter"/> and records every completed activity (span)
/// and metric measurement so a test can assert on the observability a workflow run emits, with no
/// real exporter. Dispose to detach the listeners.
/// </summary>
/// <remarks>
/// Make this the first thing constructed in a test: only activities started <em>after</em> the
/// listener attaches are sampled and recorded.
/// </remarks>
public sealed class RecordedTelemetry : IDisposable
{
    private readonly ActivityListener activityListener;
    private readonly MeterListener meterListener;
    private readonly List<Activity> activities = [];
    private readonly List<MetricMeasurement> measurements = [];
    private readonly Lock gate = new();

    /// <summary>
    /// Initializes a new instance of the <see cref="RecordedTelemetry"/> class listening to the
    /// Arazzo runtime's <see cref="ArazzoTelemetry.ActivitySourceName"/> and
    /// <see cref="ArazzoTelemetry.MeterName"/>.
    /// </summary>
    public RecordedTelemetry()
        : this(ArazzoTelemetry.ActivitySourceName, ArazzoTelemetry.MeterName)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="RecordedTelemetry"/> class listening to the
    /// named activity source and meter.
    /// </summary>
    /// <param name="activitySourceName">The activity source name to record.</param>
    /// <param name="meterName">The meter name to record.</param>
    public RecordedTelemetry(string activitySourceName, string meterName)
    {
        ArgumentNullException.ThrowIfNull(activitySourceName);
        ArgumentNullException.ThrowIfNull(meterName);

        this.activityListener = new ActivityListener
        {
            ShouldListenTo = source => source.Name == activitySourceName,
            Sample = static (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllDataAndRecorded,
            ActivityStopped = activity =>
            {
                lock (this.gate)
                {
                    this.activities.Add(activity);
                }
            },
        };
        ActivitySource.AddActivityListener(this.activityListener);

        this.meterListener = new MeterListener
        {
            InstrumentPublished = (instrument, listener) =>
            {
                if (instrument.Meter.Name == meterName)
                {
                    listener.EnableMeasurementEvents(instrument);
                }
            },
        };
        this.meterListener.SetMeasurementEventCallback<long>(this.OnMeasurement);
        this.meterListener.SetMeasurementEventCallback<double>(this.OnMeasurement);
        this.meterListener.Start();
    }

    /// <summary>Gets a snapshot of the completed activities (spans), in completion order.</summary>
    public IReadOnlyList<Activity> Activities
    {
        get
        {
            lock (this.gate)
            {
                return [.. this.activities];
            }
        }
    }

    /// <summary>Gets a snapshot of the recorded metric measurements, in observation order.</summary>
    public IReadOnlyList<MetricMeasurement> Measurements
    {
        get
        {
            lock (this.gate)
            {
                return [.. this.measurements];
            }
        }
    }

    /// <summary>
    /// Gets the completed activities with the given operation name, in completion order.
    /// </summary>
    /// <param name="operationName">The activity (span) name.</param>
    /// <returns>The matching activities.</returns>
    public IReadOnlyList<Activity> ActivitiesNamed(string operationName)
    {
        lock (this.gate)
        {
            return this.activities.Where(a => a.OperationName == operationName).ToList();
        }
    }

    /// <summary>
    /// Sums the recorded measurements for an instrument — the running total a counter reported, or
    /// the sum of values a histogram recorded.
    /// </summary>
    /// <param name="instrumentName">The instrument name (e.g. <c>corvus.arazzo.steps.executed</c>).</param>
    /// <returns>The summed value, or zero if the instrument reported nothing.</returns>
    public double Sum(string instrumentName)
    {
        lock (this.gate)
        {
            double total = 0;
            foreach (MetricMeasurement measurement in this.measurements)
            {
                if (measurement.InstrumentName == instrumentName)
                {
                    total += measurement.Value;
                }
            }

            return total;
        }
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        this.activityListener.Dispose();
        this.meterListener.Dispose();
    }

    private void OnMeasurement<T>(
        Instrument instrument,
        T measurement,
        ReadOnlySpan<KeyValuePair<string, object?>> tags,
        object? state)
        where T : struct
    {
        double value = measurement switch
        {
            long l => l,
            double d => d,
            int i => i,
            _ => Convert.ToDouble(measurement, System.Globalization.CultureInfo.InvariantCulture),
        };

        var capturedTags = tags.ToArray();
        lock (this.gate)
        {
            this.measurements.Add(new MetricMeasurement(instrument.Name, value, capturedTags));
        }
    }
}

/// <summary>
/// A single metric measurement captured by <see cref="RecordedTelemetry"/>.
/// </summary>
/// <param name="InstrumentName">The instrument that reported the measurement.</param>
/// <param name="Value">The measured value (counter delta or histogram sample).</param>
/// <param name="Tags">The tags attached to the measurement.</param>
public readonly record struct MetricMeasurement(
    string InstrumentName,
    double Value,
    KeyValuePair<string, object?>[] Tags);