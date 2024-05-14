using System.Numerics;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Running;

BenchmarkSwitcher.FromAssembly(typeof(Program).Assembly).Run(args);

[MemoryDiagnoser]
public class Benchmark {

    [Params(2, 4, 16, 256)]
    public int Rank { get; set; }

    [Params(typeof(float), typeof(double))]
    public Type Type { get; set; } = default!;

    Array _costs = default!;

    [GlobalSetup]
    public void Setup() {
        _costs = Array.CreateInstance(Type, Rank, Rank);
        for (var i = 0; i < Rank; i++) {
            for (var j = 0; j < Rank; j++) {
                var value = Random.Shared.NextSingle() switch {
                    var d when d < 0.9 => d,
                    _ => float.PositiveInfinity,
                };
                _costs.SetValue(
                    Type == typeof(float) ? (object)value : (double)value,
                    i, j);
            }
        }
        _v = typeof(Benchmark).GetMethod(nameof(GetFiniteAbsMax), BindingFlags.Static | BindingFlags.NonPublic)!
            .MakeGenericMethod(Type);
        _f = typeof(Benchmark).GetMethod(nameof(GetFiniteAbsMax_), BindingFlags.Static | BindingFlags.NonPublic)!
            .MakeGenericMethod(Type);
    }

    MethodInfo _v = default!;
    MethodInfo _f = default!;

    [Benchmark]
    public object Simd() {
        return _v.Invoke(null, [_costs])!;
    }

    [Benchmark]
    public object ForLoop() {
        return _f.Invoke(null, [_costs])!;
    }

    static T GetFiniteAbsMax<T>(T[,] costs) where T : unmanaged, IFloatingPointIeee754<T> {
        ref var current = ref Unsafe.As<byte, T>(ref MemoryMarshal.GetArrayDataReference(costs));

        if (costs.Length < Vector<T>.Count) {
            var max = T.NegativeOne;

            var span = MemoryMarshal.CreateReadOnlySpan(ref current, costs.Length);
            foreach (var value in span) {
                if (T.IsFinite(value)) {
                    max = T.Max(max, T.Abs(value));
                }
            }

            return max;
        } else {
            var result = -Vector<T>.One;

            ref var lastVectorStart = ref Unsafe.Add(ref current, costs.Length - Vector<T>.Count);
            while (Unsafe.IsAddressLessThan(ref current, ref lastVectorStart)) {
                result = FiniteMax(result, Vector.Abs(Vector.LoadUnsafe(ref current)));
                current = ref Unsafe.Add(ref current, Vector<T>.Count);
            }
            result = FiniteMax(result, Vector.Abs(Vector.LoadUnsafe(ref lastVectorStart)));

            var max = result[0];
            for (var i = 1; i < Vector<T>.Count; i++) {
                max = T.Max(max, result[i]);
            }

            return max;
        }

        static Vector<T> FiniteMax(Vector<T> left, Vector<T> right) {
            var inf = new Vector<T>(T.PositiveInfinity);
            return Vector.ConditionalSelect(Vector.Equals(inf, right),
                left,
                Vector.Max(left, right));
        }
    }

    static T GetFiniteAbsMax_<T>(T[,] costs) where T : IFloatingPointIeee754<T> {
        var nrows = costs.GetLength(0);
        var ncols = costs.GetLength(1);

        var max_abs_cost = T.NegativeOne;
        for (int i = 0; i < nrows; ++i) {
            for (int j = 0; j < ncols; ++j) {
                if (T.IsFinite(costs[i, j])) {
                    max_abs_cost = T.Max(max_abs_cost, T.Abs(costs[i, j]));
                }
            }
        }

        return max_abs_cost;
    }
}
