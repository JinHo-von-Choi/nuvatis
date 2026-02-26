using BenchmarkDotNet.Running;
using NuVatis.Benchmarks;

BenchmarkSwitcher.FromAssembly(typeof(SqliteReadBenchmark).Assembly).Run(args);
