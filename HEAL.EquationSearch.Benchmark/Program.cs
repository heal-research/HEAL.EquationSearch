using BenchmarkDotNet.Configs;
using BenchmarkDotNet.Running;

var summary = BenchmarkRunner.Run<RARLikelihoodEvaluation>(/* new DebugInProcessConfig() */); // for running benchmarks in debug builds
