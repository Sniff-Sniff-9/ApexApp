using ApexApp.Domain.Entities;
using ApexApp.Infrastructure.Common;
using ApexApp.Infrastructure.Services;
using ApexApp.Infrastructure.Services.Analytics;
using Microsoft.Extensions.Options;

namespace ApexApp.Checks;

/// <summary>
/// Deterministic checks of the pure analytics logic: arithmetic, buckets, range
/// validation, cache tags and the engagement aggregation equivalence.
/// </summary>
internal static class UnitChecks
{
    public static void Run(CheckRunner runner)
    {
        MetricsChecks(runner);
        BucketChecks(runner);
        RangeChecks(runner);
        CacheTagChecks(runner);
        EmployeeReferenceChecks(runner);
        EngagementEquivalenceChecks(runner);
        PricingChecks(runner);
    }

    private static void PricingChecks(CheckRunner runner)
    {
        runner.Check("pricing.product-price", () =>
        {
            var selection = ProductVariantPricing.Evaluate(MakeProduct(), null, 2);

            CheckRunner.Ensure(selection.IsValid, "selection must be valid");
            CheckRunner.Ensure(selection.UnitPrice == 100, "product price expected");
            CheckRunner.Ensure(selection.AvailableStock == 10, "product stock expected");
        });

        runner.Check("pricing.variant-price-wins", () =>
        {
            var selection = ProductVariantPricing.Evaluate(MakeProduct(), MakeVariant(), 2);

            CheckRunner.Ensure(selection.IsValid, "selection must be valid");
            CheckRunner.Ensure(selection.UnitPrice == 130, "variant price must win");
            CheckRunner.Ensure(selection.AvailableStock == 3, "variant stock expected");
        });

        runner.Check("pricing.variant-price-fallback", () =>
        {
            var selection = ProductVariantPricing.Evaluate(
                MakeProduct(),
                MakeVariant(price: null),
                1);

            CheckRunner.Ensure(selection.IsValid, "selection must be valid");
            CheckRunner.Ensure(selection.UnitPrice == 100, "product price must be used");
        });

        runner.Check("pricing.variant-of-another-product", () =>
        {
            var selection = ProductVariantPricing.Evaluate(
                MakeProduct(),
                MakeVariant(productId: 99),
                1);

            CheckRunner.Ensure(
                selection.Status == ProductSelectionStatus.VariantUnavailable,
                "foreign variant must be rejected");
        });

        runner.Check("pricing.inactive-variant", () =>
        {
            var selection = ProductVariantPricing.Evaluate(
                MakeProduct(),
                MakeVariant(isActive: false),
                1);

            CheckRunner.Ensure(
                selection.Status == ProductSelectionStatus.VariantUnavailable,
                "inactive variant must be rejected");
        });

        runner.Check("pricing.variant-stock", () =>
        {
            var selection = ProductVariantPricing.Evaluate(MakeProduct(), MakeVariant(), 4);

            CheckRunner.Ensure(
                selection.Status == ProductSelectionStatus.InsufficientStock,
                "variant stock must be enforced");
        });

        runner.Check("pricing.variant-zero-stock", () =>
        {
            var selection = ProductVariantPricing.Evaluate(
                MakeProduct(stock: null),
                MakeVariant(stock: 0),
                1);

            CheckRunner.Ensure(
                selection.Status == ProductSelectionStatus.InsufficientStock,
                "zero variant stock must be enforced even for unlimited product");
        });

        runner.Check("pricing.product-unlimited-stock", () =>
        {
            var selection = ProductVariantPricing.Evaluate(MakeProduct(stock: null), null, 10_000);

            CheckRunner.Ensure(selection.IsValid, "unlimited product stock must be valid");
            CheckRunner.Ensure(selection.AvailableStock is null, "unlimited stock is null");
        });

        runner.Check("pricing.product-stock", () =>
        {
            var selection = ProductVariantPricing.Evaluate(MakeProduct(stock: 2), null, 3);

            CheckRunner.Ensure(
                selection.Status == ProductSelectionStatus.InsufficientStock,
                "product stock must be enforced");
        });

        runner.Check("pricing.quantity-invalid", () =>
        {
            CheckRunner.Ensure(
                ProductVariantPricing.Evaluate(MakeProduct(), null, 0).Status
                    == ProductSelectionStatus.QuantityInvalid,
                "zero quantity must be rejected");

            CheckRunner.Ensure(
                ProductVariantPricing.Evaluate(MakeProduct(), MakeVariant(), -1).Status
                    == ProductSelectionStatus.QuantityInvalid,
                "negative quantity must be rejected");
        });

        runner.Check("pricing.product-state", () =>
        {
            CheckRunner.Ensure(
                ProductVariantPricing.Evaluate(MakeProduct(isActive: false), null, 1).Status
                    == ProductSelectionStatus.ProductUnavailable,
                "inactive product must be rejected");

            CheckRunner.Ensure(
                ProductVariantPricing.Evaluate(MakeProduct(isDigital: true), null, 1).Status
                    == ProductSelectionStatus.ProductUnavailable,
                "digital product must be rejected");
        });

        runner.Check("pricing.resolve-price", () =>
        {
            CheckRunner.Ensure(
                ProductVariantPricing.ResolveUnitPrice(MakeProduct(), null) == 100,
                "product price expected");

            CheckRunner.Ensure(
                ProductVariantPricing.ResolveUnitPrice(MakeProduct(), MakeVariant()) == 130,
                "variant price expected");
        });
    }

    private static Product MakeProduct(
        long price = 100,
        int? stock = 10,
        bool isActive = true,
        bool isDigital = false) => new()
    {
        Id = 1,
        Name = "Товар",
        Price = price,
        Stock = stock,
        IsActive = isActive,
        IsDigital = isDigital,
        CreatedAt = DateTime.UtcNow,
        UpdatedAt = DateTime.UtcNow
    };

    private static ProductVariant MakeVariant(
        int productId = 1,
        long? price = 130,
        int stock = 3,
        bool isActive = true) => new()
    {
        Id = 5,
        ProductId = productId,
        Name = "Вариант",
        Price = price,
        Stock = stock,
        IsActive = isActive
    };

    private static void MetricsChecks(CheckRunner runner)
    {
        runner.Check("metrics.ratio", () =>
        {
            CheckRunner.Ensure(AnalyticsMetrics.Ratio(0, 0) is null, "zero denominator must be null");
            CheckRunner.Ensure(AnalyticsMetrics.Ratio(1, 4) == 0.25d, "1/4 must be 0.25");
            CheckRunner.Ensure(AnalyticsMetrics.Ratio(5, 2) == 2.5d, "5/2 must be 2.5");
        });

        runner.Check("metrics.per100", () =>
        {
            CheckRunner.Ensure(AnalyticsMetrics.Per100(10, 200) == 5d, "10 per 200 must be 5");
            CheckRunner.Ensure(AnalyticsMetrics.Per100(10, 0) is null, "zero population must be null");
        });

        runner.Check("metrics.change", () =>
        {
            CheckRunner.Ensure(AnalyticsMetrics.ChangePercent(120, 100) == 20d, "+20% expected");
            CheckRunner.Ensure(AnalyticsMetrics.ChangePercent(80, 100) == -20d, "-20% expected");
            CheckRunner.Ensure(AnalyticsMetrics.ChangePercent(5, 0) is null, "zero base must be null");
            CheckRunner.Ensure(AnalyticsMetrics.ChangePercent(null, 5) is null, "null current must be null");
            CheckRunner.Ensure(AnalyticsMetrics.ChangePercent(5, null) is null, "null previous must be null");
        });

        runner.Check("metrics.direction", () =>
        {
            CheckRunner.Ensure(AnalyticsMetrics.Direction(25) == "up", "up expected");
            CheckRunner.Ensure(AnalyticsMetrics.Direction(-25) == "down", "down expected");
            CheckRunner.Ensure(AnalyticsMetrics.Direction(0.1) == "flat", "flat expected");
            CheckRunner.Ensure(AnalyticsMetrics.Direction(null) == "unknown", "unknown expected");
        });

        runner.Check("metrics.median", () =>
        {
            CheckRunner.Ensure(AnalyticsMetrics.Median([1d, 2d, 3d]) == 2d, "odd median");
            CheckRunner.Ensure(AnalyticsMetrics.Median([1d, 2d, 3d, 4d]) == 2.5d, "even median");
            CheckRunner.Ensure(AnalyticsMetrics.Median([]) is null, "empty median");
        });

        runner.Check("metrics.percentile", () =>
        {
            double[] values = [10d, 20d, 30d];

            CheckRunner.Ensure(AnalyticsMetrics.Percentile(values, 0d) == 10d, "p0");
            CheckRunner.Ensure(AnalyticsMetrics.Percentile(values, 0.5d) == 20d, "p50");
            CheckRunner.Ensure(AnalyticsMetrics.Percentile(values, 1d) == 30d, "p100");
            CheckRunner.Ensure(AnalyticsMetrics.Percentile([], 0.5d) is null, "empty percentile");
        });

        runner.Check("metrics.rate-suppression", () =>
        {
            var unreliable = MetricValue.Rate("r", "Доля", 1, 3, minCohort: 5);
            var reliable = MetricValue.Rate("r", "Доля", 3, 6, minCohort: 5);

            CheckRunner.Ensure(!unreliable.IsReliable && unreliable.Value is null, "small cohort must be suppressed");
            CheckRunner.Ensure(unreliable.Numerator == 1 && unreliable.Denominator == 3, "counts must be kept");
            CheckRunner.Ensure(reliable.IsReliable && reliable.Value == 0.5d, "reliable rate must be computed");

            var empty = MetricValue.Rate("r", "Доля", 0, 0, 5);
            CheckRunner.Ensure(!empty.IsReliable && empty.UnavailableReason is not null, "zero denominator marked");
        });
    }

    private static void BucketChecks(CheckRunner runner)
    {
        runner.Check("buckets.day-utc", () =>
        {
            var range = MakeRange(
                new DateTime(2026, 11, 1, 0, 0, 0, DateTimeKind.Utc),
                new DateTime(2026, 11, 4, 0, 0, 0, DateTimeKind.Utc),
                AnalyticsGranularity.Day,
                offsetMinutes: 0);

            var buckets = AnalyticsBuckets.Build(range, 62);

            CheckRunner.Ensure(buckets.Count == 3, $"3 buckets expected for a 3-day window, got {buckets.Count}");
            CheckRunner.Ensure(buckets[0].Start == new DateTime(2026, 11, 1, 0, 0, 0, DateTimeKind.Utc), "first start");
            CheckRunner.Ensure(buckets[^1].End >= range.To, "last bucket must cover the end");
        });

        runner.Check("buckets.day-offset", () =>
        {
            // Moscow is UTC+3: the local day starts at 21:00 UTC of the previous day.
            var range = MakeRange(
                new DateTime(2026, 11, 1, 0, 0, 0, DateTimeKind.Utc),
                new DateTime(2026, 11, 3, 0, 0, 0, DateTimeKind.Utc),
                AnalyticsGranularity.Day,
                offsetMinutes: 180);

            var buckets = AnalyticsBuckets.Build(range, 62);

            CheckRunner.Ensure(
                buckets[0].Start == new DateTime(2026, 10, 31, 21, 0, 0, DateTimeKind.Utc),
                $"offset start expected 2026-10-31T21:00Z, got {buckets[0].Start:O}");
            CheckRunner.Ensure(buckets.All(bucket => (bucket.End - bucket.Start).TotalHours == 24d), "24h buckets");
        });

        runner.Check("buckets.week-monday", () =>
        {
            var range = MakeRange(
                new DateTime(2026, 11, 4, 12, 0, 0, DateTimeKind.Utc),
                new DateTime(2026, 11, 18, 12, 0, 0, DateTimeKind.Utc),
                AnalyticsGranularity.Week,
                offsetMinutes: 0);

            var buckets = AnalyticsBuckets.Build(range, 62);

            CheckRunner.Ensure(buckets.Count == 3, $"3 week buckets expected, got {buckets.Count}");
            CheckRunner.Ensure(buckets[0].Start.DayOfWeek == DayOfWeek.Monday, "week starts on Monday");
            CheckRunner.Ensure((buckets[0].End - buckets[0].Start).TotalDays == 7d, "7 day buckets");
        });

        runner.Check("buckets.month", () =>
        {
            var range = MakeRange(
                new DateTime(2026, 1, 15, 0, 0, 0, DateTimeKind.Utc),
                new DateTime(2026, 4, 2, 0, 0, 0, DateTimeKind.Utc),
                AnalyticsGranularity.Month,
                offsetMinutes: 0);

            var buckets = AnalyticsBuckets.Build(range, 62);

            CheckRunner.Ensure(buckets.Count == 4, $"4 month buckets expected, got {buckets.Count}");
            CheckRunner.Ensure(buckets[0].Start == new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc), "January start");
            CheckRunner.Ensure(buckets[^1].Start == new DateTime(2026, 4, 1, 0, 0, 0, DateTimeKind.Utc), "April start");
        });

        runner.Check("buckets.max-cap", () =>
        {
            var range = MakeRange(
                new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc),
                new DateTime(2026, 12, 31, 0, 0, 0, DateTimeKind.Utc),
                AnalyticsGranularity.Day,
                offsetMinutes: 0);

            var buckets = AnalyticsBuckets.Build(range, 10);

            CheckRunner.Ensure(buckets.Count == 10, "bucket cap must be honoured");
        });

        runner.Check("buckets.index-of", () =>
        {
            var range = MakeRange(
                new DateTime(2026, 11, 1, 0, 0, 0, DateTimeKind.Utc),
                new DateTime(2026, 11, 3, 0, 0, 0, DateTimeKind.Utc),
                AnalyticsGranularity.Day,
                offsetMinutes: 0);

            var buckets = AnalyticsBuckets.Build(range, 62);

            CheckRunner.Ensure(
                AnalyticsBuckets.IndexOf(buckets, new DateTime(2026, 11, 2, 5, 0, 0, DateTimeKind.Utc)) == 1,
                "second bucket expected");
            CheckRunner.Ensure(
                AnalyticsBuckets.IndexOf(buckets, new DateTime(2027, 1, 1, 0, 0, 0, DateTimeKind.Utc)) == -1,
                "out of range must be -1");
        });
    }

    private static void RangeChecks(CheckRunner runner)
    {
        var factory = new AnalyticsRangeFactory(Options.Create(new AnalyticsOptions
        {
            DefaultRangeDays = 30,
            MaxRangeDays = 400,
            MaxBuckets = 62,
            TimeZone = "UTC"
        }));

        runner.Check("range.defaults", () =>
        {
            var result = factory.Create(new AnalyticsQuery());

            CheckRunner.Ensure(result.IsSuccess, "default range must succeed");

            var range = result.Value!;

            CheckRunner.Ensure(Math.Abs(range.LengthDays - 30d) < 0.01d, "30 day default expected");
            CheckRunner.Ensure(range.PreviousTo == range.From, "previous window must be contiguous");
            CheckRunner.Ensure(
                Math.Abs((range.From - range.PreviousFrom).TotalDays - range.LengthDays) < 0.01d,
                "previous window length must match");
            CheckRunner.Ensure(range.GranularityCode == "day", "day granularity expected");
        });

        runner.Check("range.invalid-order", () =>
        {
            var from = DateTime.UtcNow.AddDays(-1);
            var result = factory.Create(new AnalyticsQuery(from, from.AddDays(-1)));

            CheckRunner.Ensure(!result.IsSuccess && result.StatusCode == 400, "400 expected");
        });

        runner.Check("range.future-end", () =>
        {
            var result = factory.Create(new AnalyticsQuery(
                DateTime.UtcNow.AddDays(-5),
                DateTime.UtcNow.AddDays(5)));

            CheckRunner.Ensure(!result.IsSuccess && result.StatusCode == 400, "future end must fail");
        });

        runner.Check("range.too-long", () =>
        {
            var result = factory.Create(new AnalyticsQuery(
                DateTime.UtcNow.AddDays(-500),
                DateTime.UtcNow.AddDays(-1)));

            CheckRunner.Ensure(!result.IsSuccess && result.StatusCode == 400, "500 days must fail");
        });

        runner.Check("range.too-many-buckets", () =>
        {
            var result = factory.Create(new AnalyticsQuery(
                DateTime.UtcNow.AddDays(-300),
                DateTime.UtcNow.AddDays(-1),
                AnalyticsGranularity.Day));

            CheckRunner.Ensure(!result.IsSuccess && result.StatusCode == 400, "300 daily buckets must fail");
        });

        runner.Check("range.timezone-offset", () =>
        {
            var withZone = new AnalyticsRangeFactory(Options.Create(new AnalyticsOptions
            {
                TimeZone = "Europe/Moscow",
                MaxBuckets = 62,
                MaxRangeDays = 400
            }));

            var result = withZone.Create(new AnalyticsQuery(
                DateTime.UtcNow.AddDays(-7),
                DateTime.UtcNow.AddDays(-1)));

            CheckRunner.Ensure(result.IsSuccess, "range with time zone must succeed");
            CheckRunner.Ensure(result.Value!.TimeZoneOffsetMinutes == 180, "Moscow offset must be 180 minutes");
        });
    }

    private static void CacheTagChecks(CheckRunner runner)
    {
        runner.Check("cache-tags.filter", () =>
        {
            CheckRunner.Ensure(AnalyticsCacheTags.Filter(null) == "all", "null department");
            CheckRunner.Ensure(AnalyticsCacheTags.Filter(7) == "dept-7", "department tag");
        });

        runner.Check("cache-tags.instant-bucket", () =>
        {
            var moment = new DateTime(2026, 11, 12, 10, 37, 42, DateTimeKind.Utc);
            var bucket = AnalyticsCacheTags.InstantBucket(moment);

            CheckRunner.Ensure(bucket == "202611121035", $"expected 202611121035, got {bucket}");

            var next = AnalyticsCacheTags.InstantBucket(moment.AddMinutes(1));
            CheckRunner.Ensure(next == bucket, "must be stable inside a bucket");
        });
    }

    private static void EmployeeReferenceChecks(CheckRunner runner)
    {
        runner.Check("employee-ref.stable", () =>
        {
            var id = Guid.NewGuid();
            var first = EngagementAnalyticsService.EmployeeReference(id);
            var second = EngagementAnalyticsService.EmployeeReference(id);
            var other = EngagementAnalyticsService.EmployeeReference(Guid.NewGuid());

            CheckRunner.Ensure(first == second, "reference must be stable");
            CheckRunner.Ensure(first != other, "reference must differ per employee");
            CheckRunner.Ensure(!first.Contains('+') && !first.Contains('/') && !first.Contains('='), "url-safe expected");
            CheckRunner.Ensure(!first.Contains(id.ToString(), StringComparison.OrdinalIgnoreCase), "must not leak the id");
        });
    }

    private static void EngagementEquivalenceChecks(CheckRunner runner)
    {
        runner.Check("engagement.banded-weights-equal-moments", () =>
        {
            var now = new DateTime(2026, 11, 12, 12, 0, 0, DateTimeKind.Utc);

            // Two surveys: one inside the recent band, one inside the steady band.
            var moments = new List<DateTime>
            {
                now.AddDays(-2),
                now.AddDays(-10)
            };

            var expectedWeighted = moments.Sum(moment => EngagementScoring.WeightFor(moment, now));

            // Reconstruct the weighted sum the way the loader does it (per band counts).
            var recent = moments.Count(moment => now - moment <= TimeSpan.FromDays(EngagementScoring.BandDays));
            var steady = moments.Count(moment =>
                now - moment > TimeSpan.FromDays(EngagementScoring.BandDays)
                && now - moment <= TimeSpan.FromDays(2 * EngagementScoring.BandDays));
            var aged = moments.Count - recent - steady;

            var bandedWeighted =
                recent * EngagementScoring.RecentWeight
                + steady * EngagementScoring.SteadyWeight
                + aged * EngagementScoring.AgedWeight;

            CheckRunner.Ensure(
                Math.Abs(expectedWeighted - bandedWeighted) < 1e-9,
                "banded weights must equal per-moment weights");

            var summary = EngagementCalculator.Evaluate(
                new EngagementMetrics(
                    EngagementSourceMetrics.Empty,
                    EngagementSourceMetrics.Empty,
                    EngagementSourceMetrics.Empty,
                    EngagementSourceMetrics.Empty,
                    moments,
                    Array.Empty<DateTime>()),
                now);

            CheckRunner.Ensure(summary.Score >= 0d && summary.Score <= EngagementScoring.MaxScore, "score bounds");
            CheckRunner.Ensure(summary.WindowDays == EngagementScoring.WindowDays, "window must be reused");
            CheckRunner.Ensure(summary.Breakdown.Count == 7, "breakdown must have seven sources");
        });

        runner.Check("engagement.empty-score", () =>
        {
            var now = DateTime.UtcNow;
            var summary = EngagementCalculator.Evaluate(
                new EngagementMetrics(
                    EngagementSourceMetrics.Empty,
                    EngagementSourceMetrics.Empty,
                    EngagementSourceMetrics.Empty,
                    EngagementSourceMetrics.Empty,
                    Array.Empty<DateTime>(),
                    Array.Empty<DateTime>()),
                now);

            CheckRunner.Ensure(summary.Score == 0d, "no activity must score zero");
            CheckRunner.Ensure(summary.Status == EngagementScoring.Spark, "lowest tier expected");
        });
    }

    private static AnalyticsRange MakeRange(
        DateTime from,
        DateTime to,
        AnalyticsGranularity granularity,
        int offsetMinutes) => new(
        from,
        to,
        from.AddDays(-(to - from).TotalDays),
        from,
        granularity,
        to,
        offsetMinutes == 0 ? "UTC" : "Europe/Moscow",
        offsetMinutes,
        []);
}
