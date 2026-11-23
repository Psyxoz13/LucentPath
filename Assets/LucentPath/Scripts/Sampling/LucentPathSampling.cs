using System;

namespace LucentPath.Sampling
{
    public static class LucentPathSampling
    {
        private static readonly object _cacheLock = new object();
        private static int _cachedDim;
        private static int _cachedMatrixSize;
        private static uint[] _cached;

        public static uint[] GetSobolMatrices(int dimensionCount, int matrixSize)
        {
            if (dimensionCount <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(dimensionCount));
            }

            if (matrixSize <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(matrixSize));
            }

            int requiredCount = checked(dimensionCount * matrixSize);

            uint[] source;

            lock (_cacheLock)
            {
                if (_cached == null ||
                    _cachedDim != dimensionCount ||
                    _cachedMatrixSize != matrixSize)
                {
                    _cached = SobolMatrixGenerator.Build(dimensionCount, matrixSize);
                    _cachedDim = dimensionCount;
                    _cachedMatrixSize = matrixSize;
                }

                source = _cached;
            }

            var matrices = new uint[requiredCount];

            Array.Copy(source, matrices, requiredCount);

            return matrices;
        }
    }
}
