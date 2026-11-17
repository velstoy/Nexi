using Xunit;

// The headless Avalonia session owns a single UI thread; run tests sequentially.
[assembly: CollectionBehavior(DisableTestParallelization = true)]
