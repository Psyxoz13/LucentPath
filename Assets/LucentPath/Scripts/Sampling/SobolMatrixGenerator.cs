using System;

namespace LucentPath.Sampling
{
    internal static class SobolMatrixGenerator
    {
        internal const int DefaultMatrixSize = 52;
        internal const int MaxMatrixSize = 52;

        internal static uint[] Build(int dimensionCount, int matrixSize = DefaultMatrixSize)
        {
            if (dimensionCount <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(dimensionCount));
            }

            if (matrixSize <= 0 || matrixSize > MaxMatrixSize)
            {
                throw new ArgumentOutOfRangeException(nameof(matrixSize),
                                $"matrixSize must be in [1, {MaxMatrixSize}].");
            }

            if (dimensionCount > SobolJoeKuoData.MaxDimension)
            {
                throw new ArgumentOutOfRangeException(nameof(dimensionCount),
                                $"Joe-Kuo table covers up to {SobolJoeKuoData.MaxDimension} dimensions. " +
                                "Use SobolJoeKuoData.LoadExtended(...) for more.");
            }

            int total = checked(dimensionCount * matrixSize);
            var result = new uint[total];

            for (int c = 1; c <= matrixSize; c++)
            {
                ulong full = 1UL << (64 - c);

                result[c - 1] = (uint)(full >> 32);
            }

            for (int d = 1; d < dimensionCount; d++)
            {
                BuildDimension(d, matrixSize, result);
            }

            return result;
        }

        private static void BuildDimension(int d, int matrixSize, uint[] result)
        {
            SobolJoeKuoData.GetEntry(d, out int s, out uint a, out ReadOnlySpan<uint> mInit);

            Span<ulong> m = stackalloc ulong[matrixSize + 1];

            int initCount = Math.Min(s, matrixSize);

            for (int k = 1; k <= initCount; k++)
            {
                m[k] = mInit[k - 1];
            }

            for (int k = s + 1; k <= matrixSize; k++)
            {
                ulong v = m[k - s] ^ (m[k - s] << s);

                for (int i = 1; i < s; i++)
                {
                    uint aBit = (a >> (s - 1 - i)) & 1u;

                    if (aBit != 0)
                    {
                        v ^= m[k - i] << i;
                    }
                }

                m[k] = v;
            }

            int baseIdx = d * matrixSize;

            for (int c = 1; c <= matrixSize; c++)
            {
                ulong full = m[c] << (64 - c);

                result[baseIdx + c - 1] = (uint)(full >> 32);
            }
        }
    }
}
