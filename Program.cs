using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Running;

BenchmarkRunner.Run<Benchmark>();

public class Benchmark
{
    const int N = 10000;

    private readonly Dictionary<(int, int), int> _tupleDict = new();
    private readonly Dictionary<int, Dictionary<int, int>> _nestDict = new();

    public Benchmark() {
        for (var i = 0; i < N; i++) {
            _nestDict[i] = new();
            for (var j = 0; j < N; j++) {
                _tupleDict[(i, j)] = i + j;
                _nestDict[i][j] = i + j;
            }
        }
    }

    [Benchmark]
    public object DictionaryWithValueTupleKey()
    {
        return _tupleDict[(Random.Shared.Next(N), Random.Shared.Next(N))];
    }

    [Benchmark]
    public object DictionaryWithNestedKey()
    {
        return _nestDict[Random.Shared.Next(N)][Random.Shared.Next(N)];
    }

}
