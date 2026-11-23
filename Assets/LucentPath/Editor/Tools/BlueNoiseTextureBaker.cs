using System;
using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.Experimental.Rendering;

namespace LucentPath.EditorUi.Tools
{
    [Serializable]
    public class BlueNoiseBakeSettings
    {
        public int Width = 128;
        public int Height = 128;
        public int Layers = 16;
        public float SpatialSigma = 1.9f;
        public float TemporalSigma = 1.9f;
        public float InitialOneFraction = .1f;
        public int RandomSeed = 0xC0FFEE;
        public string AssetPath = "Assets/LucentPath/GeneratedBlueNoise.asset";
    }

    public static class BlueNoiseTextureBaker
    {
        private const string ProgressTitle = "Bake Spatiotemporal Blue Noise";
        private const int StabilizationSafetyFactor = 4;

        public static int GetKernelRadius(float sigma)
        {
            return Mathf.CeilToInt(3f * Mathf.Max(.1f, sigma));
        }

        public static bool Bake(BlueNoiseBakeSettings settings, out string report)
        {
            int width = Mathf.Max(8, settings.Width);
            int height = Mathf.Max(8, settings.Height);
            int layers = Mathf.Max(1, settings.Layers);
            float spatialSigma = Mathf.Max(.1f, settings.SpatialSigma);
            float temporalSigma = Mathf.Max(.1f, settings.TemporalSigma);
            float initialOneFraction = Mathf.Clamp(settings.InitialOneFraction, .01f, .49f);

            int sliceCount = width * height;
            int voxelCount = sliceCount * layers;

            int spatialRadius = GetKernelRadius(spatialSigma);
            int temporalRadius = GetKernelRadius(temporalSigma);

            BuildSpatialKernel(spatialRadius, spatialSigma, out int[] spatialOffsets, out float[] spatialWeights);
            BuildTemporalKernel(temporalRadius, temporalSigma, out int[] temporalOffsets, out float[] temporalWeights);

            if (layers == 1)
            {
                temporalOffsets = Array.Empty<int>();
                temporalWeights = Array.Empty<float>();
            }

            var binary = new bool[voxelCount];
            var energy = new float[voxelCount];
            var ranks = new int[voxelCount];

            for (int i = 0; i < voxelCount; i++)
            {
                ranks[i] = -1;
            }

            var startTime = DateTime.Now;

            try
            {
                Step("Initial pattern", "random seed", 0f);

                int initialOnes = Mathf.Clamp(Mathf.RoundToInt(voxelCount * initialOneFraction), 1, voxelCount / 2);
                var rng = new System.Random(settings.RandomSeed);
                int placed = 0;

                while (placed < initialOnes)
                {
                    int idx = rng.Next(voxelCount);

                    if (binary[idx])
                    {
                        continue;
                    }

                    binary[idx] = true;

                    ApplyKernel(energy, width, height, layers, spatialOffsets, spatialWeights, temporalOffsets, temporalWeights, idx, +1f);
                    placed++;
                }

                int safety = voxelCount * StabilizationSafetyFactor;
                int stabSteps = 0;

                while (safety-- > 0)
                {
                    int tightestCluster = FindExtreme(energy, binary, true, true);

                    if (tightestCluster < 0)
                    {
                        break;
                    }

                    binary[tightestCluster] = false;

                    ApplyKernel(
                        energy,
                        width,
                        height,
                        layers,
                        spatialOffsets,
                        spatialWeights,
                        temporalOffsets,
                        temporalWeights,
                        tightestCluster,
                        -1f);

                    int largestVoid = FindExtreme(energy, binary, false, false);

                    if (largestVoid < 0 || largestVoid == tightestCluster)
                    {
                        binary[tightestCluster] = true;

                        ApplyKernel(
                            energy,
                            width,
                            height,
                            layers,
                            spatialOffsets,
                            spatialWeights,
                            temporalOffsets,
                            temporalWeights,
                            tightestCluster,
                            +1f);

                        break;
                    }

                    binary[largestVoid] = true;

                    ApplyKernel(energy, width, height, layers, spatialOffsets, spatialWeights, temporalOffsets, temporalWeights, largestVoid, +1f);

                    stabSteps++;

                    if ((stabSteps & 0xFF) == 0)
                    {
                        Step("Stabilising prototype pattern", $"swap {stabSteps}", 0f);
                    }
                }

                var phase1Binary = (bool[])binary.Clone();
                var phase1Energy = (float[])energy.Clone();
                int rank = initialOnes - 1;
                int phase1Step = 0;

                while (rank >= 0)
                {
                    int tightestCluster = FindExtreme(phase1Energy, phase1Binary, true, true);

                    if (tightestCluster < 0)
                    {
                        break;
                    }

                    phase1Binary[tightestCluster] = false;

                    ApplyKernel(
                        phase1Energy,
                        width,
                        height,
                        layers,
                        spatialOffsets,
                        spatialWeights,
                        temporalOffsets,
                        temporalWeights,
                        tightestCluster,
                        -1f);

                    ranks[tightestCluster] = rank;

                    rank--;
                    phase1Step++;

                    if ((phase1Step & 0x3FF) == 0)
                    {
                        Step("Phase I — rank initial pattern", $"{phase1Step}/{initialOnes}", phase1Step / (float)initialOnes);
                    }
                }

                int halfTarget = voxelCount / 2;
                int forwardRank = initialOnes;
                int phase2Total = Math.Max(1, halfTarget - initialOnes);
                int phase2Step = 0;

                while (forwardRank < halfTarget)
                {
                    int largestVoid = FindExtreme(energy, binary, false, false);

                    if (largestVoid < 0)
                    {
                        break;
                    }

                    binary[largestVoid] = true;

                    ApplyKernel(energy, width, height, layers, spatialOffsets, spatialWeights, temporalOffsets, temporalWeights, largestVoid, +1f);

                    ranks[largestVoid] = forwardRank;

                    forwardRank++;
                    phase2Step++;

                    if ((phase2Step & 0x3FF) == 0)
                    {
                        Step("Phase II — fill voids to half", $"{phase2Step}/{phase2Total}", phase2Step / (float)phase2Total);
                    }
                }

                var zeroEnergy = new float[voxelCount];

                for (int i = 0; i < voxelCount; i++)
                {
                    if (binary[i] == false)
                    {
                        ApplyKernel(zeroEnergy, width, height, layers, spatialOffsets, spatialWeights, temporalOffsets, temporalWeights, i, +1f);
                    }

                    if ((i & 0xFFF) == 0)
                    {
                        Step("Phase III — build zero energy", $"{i}/{voxelCount}", i / (float)voxelCount);
                    }
                }

                int backwardRank = forwardRank;
                int phase3Total = Math.Max(1, voxelCount - backwardRank);
                int phase3Step = 0;

                while (backwardRank < voxelCount)
                {
                    int tightestZeroCluster = FindExtreme(zeroEnergy, binary, false, true);

                    if (tightestZeroCluster < 0)
                    {
                        break;
                    }

                    binary[tightestZeroCluster] = true;

                    ApplyKernel(
                        zeroEnergy,
                        width,
                        height,
                        layers,
                        spatialOffsets,
                        spatialWeights,
                        temporalOffsets,
                        temporalWeights,
                        tightestZeroCluster,
                        -1f);

                    ranks[tightestZeroCluster] = backwardRank;

                    backwardRank++;
                    phase3Step++;

                    if ((phase3Step & 0x3FF) == 0)
                    {
                        Step("Phase III — fill to full", $"{phase3Step}/{phase3Total}", phase3Step / (float)phase3Total);
                    }
                }

                for (int i = 0; i < voxelCount; i++)
                {
                    if (ranks[i] < 0)
                    {
                        ranks[i] = backwardRank++;
                    }
                }

                Step("Packing texture", $"{layers} layers", 1f);
            }
            catch (OperationCanceledException)
            {
                EditorUtility.ClearProgressBar();

                report = "Bake cancelled.";

                return false;
            }

            var array = new Texture2DArray(
                width,
                height,
                layers,
                GraphicsFormat.R8_UNorm,
                TextureCreationFlags.None)
            { wrapMode = TextureWrapMode.Repeat, filterMode = FilterMode.Point, name = "BlueNoiseTextureArray" };

            var sliceBytes = new byte[sliceCount];
            float rankToByte = 256f / voxelCount;

            for (int layer = 0; layer < layers; layer++)
            {
                int layerOffset = layer * sliceCount;

                for (int i = 0; i < sliceCount; i++)
                {
                    sliceBytes[i] = (byte)Mathf.Clamp((int)((ranks[layerOffset + i] + .5f) * rankToByte), 0, 255);
                }

                array.SetPixelData(sliceBytes, 0, layer);
            }

            array.Apply(false, true);

            EditorUtility.ClearProgressBar();

            string assetPath = settings.AssetPath;
            string dir = Path.GetDirectoryName(assetPath);

            if (string.IsNullOrEmpty(dir) == false && Directory.Exists(dir) == false)
            {
                Directory.CreateDirectory(dir);
            }

            var existing = AssetDatabase.LoadAssetAtPath<Texture2DArray>(assetPath);
            Texture2DArray result;

            if (existing != null)
            {
                EditorUtility.CopySerialized(array, existing);
                UnityEngine.Object.DestroyImmediate(array);

                result = existing;
            }
            else
            {
                AssetDatabase.CreateAsset(array, assetPath);

                result = array;
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            EditorGUIUtility.PingObject(result);

            double seconds = (DateTime.Now - startTime).TotalSeconds;

            report = $"Baked {width}x{height}x{layers} STBN ({voxelCount} voxels, spatial r={spatialRadius}, temporal r={temporalRadius}) in {seconds:F1}s -> {assetPath}";

            Debug.Log($"[LucentPath] {report}");

            return true;
        }

        private static void Step(string stage, string info, float t)
        {
            if (EditorUtility.DisplayCancelableProgressBar(ProgressTitle, $"{stage}: {info}", t))
            {
                throw new OperationCanceledException();
            }
        }

        private static void BuildSpatialKernel(int radius, float sigma, out int[] offsets, out float[] weights)
        {
            float twoSigmaSq = 2f * sigma * sigma;
            int diameter = 2 * radius + 1;
            int len = diameter * diameter;

            offsets = new int[len * 2];
            weights = new float[len];

            int p = 0;
            int op = 0;

            for (int dy = -radius; dy <= radius; dy++)
            {
                for (int dx = -radius; dx <= radius; dx++)
                {
                    weights[p++] = Mathf.Exp(-(dx * dx + dy * dy) / twoSigmaSq);
                    offsets[op++] = dx;
                    offsets[op++] = dy;
                }
            }
        }

        private static void BuildTemporalKernel(int radius, float sigma, out int[] offsets, out float[] weights)
        {
            float twoSigmaSq = 2f * sigma * sigma;
            int len = 2 * radius;

            offsets = new int[len];
            weights = new float[len];

            int p = 0;

            for (int dt = -radius; dt <= radius; dt++)
            {
                if (dt == 0)
                {
                    continue;
                }

                weights[p] = Mathf.Exp(-(dt * dt) / twoSigmaSq);
                offsets[p] = dt;

                p++;
            }
        }

        private static void ApplyKernel(float[] energy, int width, int height, int layers,
            int[] spatialOffsets, float[] spatialWeights, int[] temporalOffsets, float[] temporalWeights,
            int centerIdx, float sign)
        {
            int slice = width * height;
            int cx = centerIdx % width;
            int cy = (centerIdx / width) % height;
            int ct = centerIdx / slice;
            int layerBase = ct * slice;

            int op = 0;

            for (int k = 0; k < spatialWeights.Length; k++)
            {
                int px = ((cx + spatialOffsets[op++]) % width + width) % width;
                int py = ((cy + spatialOffsets[op++]) % height + height) % height;

                energy[layerBase + py * width + px] += sign * spatialWeights[k];
            }

            int pixelOffset = cy * width + cx;

            for (int k = 0; k < temporalWeights.Length; k++)
            {
                int pt = ((ct + temporalOffsets[k]) % layers + layers) % layers;

                energy[pt * slice + pixelOffset] += sign * temporalWeights[k];
            }
        }

        private static int FindExtreme(float[] energy, bool[] mask, bool wantOnes, bool wantMax)
        {
            int best = -1;
            float bestE = wantMax ? float.NegativeInfinity : float.PositiveInfinity;
            int n = energy.Length;

            for (int i = 0; i < n; i++)
            {
                if (mask[i] != wantOnes)
                {
                    continue;
                }

                float e = energy[i];

                if (wantMax)
                {
                    if (e > bestE)
                    {
                        bestE = e;
                        best = i;
                    }
                }
                else
                {
                    if (e < bestE)
                    {
                        bestE = e;
                        best = i;
                    }
                }
            }

            return best;
        }
    }
}
