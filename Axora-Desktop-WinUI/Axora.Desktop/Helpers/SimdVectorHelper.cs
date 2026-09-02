using System;
using System.Numerics;
using System.Runtime.CompilerServices;

namespace Axora.Desktop.Helpers;

/// <summary>
/// Hardware-accelerated SIMD vector arithmetic routines (AVX2 / AVX-512 / ARM NEON)
/// for high-performance offline AI embedding similarity searches and data processing.
/// </summary>
public static class SimdVectorHelper
{
    /// <summary>
    /// Computes the dot product of two float spans using hardware SIMD vectorization.
    /// Operates in O(N / VectorSize) time complexity.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveOptimization | MethodImplOptions.AggressiveInlining)]
    public static float DotProduct(ReadOnlySpan<float> left, ReadOnlySpan<float> right)
    {
        if (left.Length != right.Length)
            throw new ArgumentException("Vector dimensions must match for dot product calculation.");

        int count = left.Length;
        int vectorSize = Vector<float>.Count;
        int i = 0;

        var sumVector = Vector<float>.Zero;

        // Process full SIMD vector width blocks
        while (i <= count - vectorSize)
        {
            var vLeft = new Vector<float>(left.Slice(i, vectorSize));
            var vRight = new Vector<float>(right.Slice(i, vectorSize));
            sumVector += vLeft * vRight;
            i += vectorSize;
        }

        float dot = Vector.Dot(sumVector, Vector<float>.One);

        // Process remaining scalar elements
        while (i < count)
        {
            dot += left[i] * right[i];
            i++;
        }

        return dot;
    }

    /// <summary>
    /// Computes the L2 norm (Euclidean magnitude) of a float vector using hardware SIMD.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveOptimization | MethodImplOptions.AggressiveInlining)]
    public static float Magnitude(ReadOnlySpan<float> vector)
    {
        return MathF.Sqrt(DotProduct(vector, vector));
    }

    /// <summary>
    /// Computes the Cosine Similarity between two embedding vectors in [-1.0, 1.0].
    /// SIMD hardware-accelerated for sub-millisecond document semantic matching.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveOptimization | MethodImplOptions.AggressiveInlining)]
    public static float CosineSimilarity(ReadOnlySpan<float> left, ReadOnlySpan<float> right)
    {
        if (left.Length != right.Length || left.Length == 0) return 0f;

        float dot = DotProduct(left, right);
        float magLeft = Magnitude(left);
        float magRight = Magnitude(right);

        if (magLeft <= 1e-7f || magRight <= 1e-7f) return 0f;

        return Math.Clamp(dot / (magLeft * magRight), -1.0f, 1.0f);
    }
}
