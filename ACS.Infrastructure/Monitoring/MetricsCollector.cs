using Microsoft.Extensions.Logging;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Diagnostics.Metrics;

namespace ACS.Infrastructure.Monitoring;

/// <summary>
/// Implementation of metrics collector using System.Diagnostics.Metrics
/// </summary>
public class MetricsCollector : IMetricsCollector, IDisposable
{
    private readonly ILogger<MetricsCollector> _logger;
    private readonly Meter _meter;
    private readonly ConcurrentDictionary<string, Counter<double>> _counters;
    private readonly ConcurrentDictionary<string, Histogram<double>> _histograms;
    private readonly ConcurrentDictionary<string, ObservableGauge<double>> _gauges;
    private readonly ConcurrentDictionary<string, double> _gaugeValues;
    private readonly ConcurrentDictionary<string, List<MetricDataPoint>> _timeSeries;
    private readonly ConcurrentDictionary<string, BusinessMetricValue> _businessMetrics;
    private readonly Timer _cleanupTimer;

    public MetricsCollector(ILogger<MetricsCollector> logger, string meterName = "ACS.Metrics")
    {
        _logger = logger;
        _meter = new Meter(meterName, "1.0.0");
        _counters = new ConcurrentDictionary<string, Counter<double>>();
        _histograms = new ConcurrentDictionary<string, Histogram<double>>();
        _gauges = new ConcurrentDictionary<string, ObservableGauge<double>>();
        _gaugeValues = new ConcurrentDictionary<string, double>();
        _timeSeries = new ConcurrentDictionary<string, List<MetricDataPoint>>();
        _businessMetrics = new ConcurrentDictionary<string, BusinessMetricValue>();
        
        // Cleanup old data every hour
        _cleanupTimer = new Timer(CleanupOldData, null, TimeSpan.FromHours(1), TimeSpan.FromHours(1));
        
        RegisterDefaultMetrics();
    }

    public void IncrementCounter(string name, double value = 1, params KeyValuePair<string, object?>[] tags)
    {
        var counter = _counters.GetOrAdd(name, n => _meter.CreateCounter<double>(n));
        counter.Add(value, tags);
        
        RecordDataPoint(name, value, tags);
    }

    public void RecordGauge(string name, double value, params KeyValuePair<string, object?>[] tags)
    {
        _gaugeValues[name] = value;
        
        if (!_gauges.ContainsKey(name))
        {
            var gauge = _meter.CreateObservableGauge(name, () => _gaugeValues.GetValueOrDefault(name, 0));
            _gauges[name] = gauge;
        }
        
        RecordDataPoint(name, value, tags);
    }

    public void RecordHistogram(string name, double value, params KeyValuePair<string, object?>[] tags)
    {
        var histogram = _histograms.GetOrAdd(name, n => _meter.CreateHistogram<double>(n));
        histogram.Record(value, tags);
        
        RecordDataPoint(name, value, tags);
    }

    public IDisposable StartTimer(string name, params KeyValuePair<string, object?>[] tags)
    {
        return new TimerScope(this, name, tags);
    }

    public void RecordDuration(string name, TimeSpan duration, params KeyValuePair<string, object?>[] tags)
    {
        RecordHistogram($"{name}.duration_ms", duration.TotalMilliseconds, tags);
    }

    public void RecordBusinessMetric(string category, string metric, double value, Dictionary<string, object?>? dimensions = null)
    {
        var key = $"{category}.{metric}";
        var businessMetric = new BusinessMetricValue
        {
            Name = key,
            Category = category,
            MetricName = metric,
            Value = value,
            Timestamp = DateTime.UtcNow,
            Dimensions = dimensions ?? new Dictionary<string, object?>()
        };
        
        _businessMetrics[key] = businessMetric;
        RecordHistogram($"business.{key}", value);
    }

    public async Task<MetricsSnapshot> GetSnapshotAsync()
    {
        return await Task.Run(() =>
        {
            var snapshot = new MetricsSnapshot
            {
                Timestamp = DateTime.UtcNow
            };

            // Collect counter values
            foreach (var (name, _) in _counters)
            {
                if (_timeSeries.TryGetValue(name, out var dataPoints) && dataPoints.Any())
                {
                    var total = dataPoints.Sum(dp => dp.Value);
                    snapshot.Counters[name] = new MetricValue
                    {
                        Name = name,
                        Value = total,
                        Timestamp = DateTime.UtcNow
                    };
                }
            }

            // Collect gauge values
            foreach (var (name, value) in _gaugeValues)
            {
                snapshot.Gauges[name] = new MetricValue
                {
                    Name = name,
                    Value = value,
                    Timestamp = DateTime.UtcNow
                };
            }

            // Collect histogram statistics
            foreach (var (name, _) in _histograms)
            {
                if (_timeSeries.TryGetValue(name, out var dataPoints) && dataPoints.Any())
                {
                    var values = dataPoints.Select(dp => dp.Value).OrderBy(v => v).ToList();
                    snapshot.Histograms[name] = CalculateHistogramStatistics(name, values);
                }
            }

            // Collect business metrics
            foreach (var (key, metric) in _businessMetrics)
            {
                snapshot.BusinessMetrics[key] = metric;
            }

            return snapshot;
        });
    }

    public async Task<IEnumerable<MetricDataPoint>> GetMetricsAsync(string name, DateTime start, DateTime end)
    {
        return await Task.Run(() =>
        {
            if (!_timeSeries.TryGetValue(name, out var dataPoints))
            {
                return Enumerable.Empty<MetricDataPoint>();
            }

            return dataPoints
                .Where(dp => dp.Timestamp >= start && dp.Timestamp <= end)
                .OrderBy(dp => dp.Timestamp)
                .ToList();
        });
    }

    private void RegisterDefaultMetrics()
    {
        // CPU metrics
        _meter.CreateObservableGauge("system.cpu.usage", () =>
        {
            using var process = Process.GetCurrentProcess();
            return process.TotalProcessorTime.TotalMilliseconds;
        });

        // Memory metrics
        _meter.CreateObservableGauge("system.memory.usage", () =>
        {
            using var process = Process.GetCurrentProcess();
            return process.WorkingSet64 / (1024.0 * 1024.0); // MB
        });

        _meter.CreateObservableGauge("system.gc.heap_size", () =>
        {
            return GC.GetTotalMemory(false) / (1024.0 * 1024.0); // MB
        });

        _meter.CreateObservableGauge("system.gc.gen0_collections", () => GC.CollectionCount(0));
        _meter.CreateObservableGauge("system.gc.gen1_collections", () => GC.CollectionCount(1));
        _meter.CreateObservableGauge("system.gc.gen2_collections", () => GC.CollectionCount(2));

        // Thread metrics
        _meter.CreateObservableGauge("system.threads.count", () =>
        {
            using var process = Process.GetCurrentProcess();
            return process.Threads.Count;
        });

        _meter.CreateObservableGauge("system.threadpool.threads", () => 
        {
            ThreadPool.GetAvailableThreads(out var workerThreads, out _);
            return workerThreads;
        });

        _meter.CreateObservableGauge("system.threadpool.io_threads", () =>
        {
            ThreadPool.GetAvailableThreads(out _, out var ioThreads);
            return ioThreads;
        });
    }

    private void RecordDataPoint(string name, double value, KeyValuePair<string, object?>[] tags)
    {
        var dataPoints = _timeSeries.GetOrAdd(name, _ => new List<MetricDataPoint>());
        
        lock (dataPoints)
        {
            dataPoints.Add(new MetricDataPoint
            {
                Timestamp = DateTime.UtcNow,
                Value = value,
                Tags = tags.ToDictionary(kvp => kvp.Key, kvp => kvp.Value)
            });

            // Keep only last 1000 points per metric
            if (dataPoints.Count > 1000)
            {
                dataPoints.RemoveRange(0, dataPoints.Count - 1000);
            }
        }
    }

    private HistogramValue CalculateHistogramStatistics(string name, List<double> values)
    {
        if (!values.Any())
        {
            return new HistogramValue { Name = name };
        }

        var count = values.Count;
        var sum = values.Sum();
        var mean = sum / count;
        
        var variance = values.Sum(v => Math.Pow(v - mean, 2)) / count;
        var stdDev = Math.Sqrt(variance);

        return new HistogramValue
        {
            Name = name,
            Min = values.Min(),
            Max = values.Max(),
            Mean = mean,
            StdDev = stdDev,
            P50 = GetPercentile(values, 0.50),
            P75 = GetPercentile(values, 0.75),
            P95 = GetPercentile(values, 0.95),
            P99 = GetPercentile(values, 0.99),
            Count = count,
            Sum = sum,
            Value = mean,
            Timestamp = DateTime.UtcNow
        };
    }

    private double GetPercentile(List<double> sortedValues, double percentile)
    {
        if (!sortedValues.Any())
            return 0;

        var index = (int)Math.Ceiling(percentile * sortedValues.Count) - 1;
        return sortedValues[Math.Max(0, Math.Min(index, sortedValues.Count - 1))];
    }

    private void CleanupOldData(object? state)
    {
        try
        {
            var cutoff = DateTime.UtcNow.AddHours(-24);
            
            foreach (var (name, dataPoints) in _timeSeries)
            {
                lock (dataPoints)
                {
                    dataPoints.RemoveAll(dp => dp.Timestamp < cutoff);
                }
            }

            // Clean up old business metrics
            var oldMetrics = _businessMetrics
                .Where(kvp => kvp.Value.Timestamp < cutoff)
                .Select(kvp => kvp.Key)
                .ToList();

            foreach (var key in oldMetrics)
            {
                _businessMetrics.TryRemove(key, out _);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error cleaning up old metric data");
        }
    }

    public void Dispose()
    {
        _cleanupTimer?.Dispose();
        _meter?.Dispose();
    }

    private class TimerScope : IDisposable
    {
        private readonly MetricsCollector _collector;
        private readonly string _name;
        private readonly KeyValuePair<string, object?>[] _tags;
        private readonly Stopwatch _stopwatch;

        public TimerScope(MetricsCollector collector, string name, KeyValuePair<string, object?>[] tags)
        {
            _collector = collector;
            _name = name;
            _tags = tags;
            _stopwatch = Stopwatch.StartNew();
        }

        public void Dispose()
        {
            _stopwatch.Stop();
            _collector.RecordDuration(_name, _stopwatch.Elapsed, _tags);
        }
    }
}