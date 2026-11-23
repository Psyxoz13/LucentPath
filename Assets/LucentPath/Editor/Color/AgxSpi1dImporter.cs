using LucentPath.Rendering;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using UnityEditor.AssetImporters;
using UnityEngine;

namespace LucentPath.Editor.Color
{
    [ScriptedImporter(1, "spi1d")]
    public class AgxSpi1dImporter : ScriptedImporter
    {
        private readonly struct Spi1dData
        {
            public readonly int Version;
            public readonly float From;
            public readonly float To;
            public readonly int Length;
            public readonly int Components;
            public readonly float[] Values;

            public Spi1dData(int version, float from, float to, int length, int components, float[] values)
            {
                Version = version;
                From = from;
                To = to;
                Length = length;
                Components = components;
                Values = values;
            }
        }

        public override void OnImportAsset(AssetImportContext ctx)
        {
            byte[] bytes = File.ReadAllBytes(ctx.assetPath);
            string text = Encoding.UTF8.GetString(bytes);
            string blobSha = ComputeGitBlobSha(bytes);

            var lut = ScriptableObject.CreateInstance<AgxSpi1dLut>();

            lut.name = Path.GetFileNameWithoutExtension(ctx.assetPath);

            try
            {
                Spi1dData data = Parse(text);

                bool valid = data.Version == 1
                    && data.Length == AgxSpi1dLut.OfficialLength
                    && data.Components == AgxSpi1dLut.OfficialComponents
                    && data.Values.Length == AgxSpi1dLut.OfficialLength
                    && string.Equals(blobSha, AgxSpi1dLut.OfficialBlobSha, StringComparison.OrdinalIgnoreCase);

                string message = valid
                    ? "Official AgX Default Contrast SPI1D LUT."
                    : $"Expected official AgX_Default_Contrast.spi1d blob {AgxSpi1dLut.OfficialBlobSha}; imported blob {blobSha}.";

                lut.SetImportedData(
                    AgxSpi1dLut.OfficialSourceUrl,
                    blobSha,
                    data.Version,
                    data.From,
                    data.To,
                    data.Length,
                    data.Components,
                    data.Values,
                    valid,
                    message);

                if (valid == false)
                {
                    ctx.LogImportError(message);
                }
            }
            catch (Exception ex)
            {
                lut.SetImportedData(AgxSpi1dLut.OfficialSourceUrl, blobSha, 0, 0f, 1f, 0, 0, Array.Empty<float>(), false, ex.Message);
                ctx.LogImportError(ex.Message);
            }

            ctx.AddObjectToAsset("lut", lut);
            ctx.SetMainObject(lut);
        }

        private static Spi1dData Parse(string text)
        {
            int version = 0;
            float from = 0f;
            float to = 1f;
            int length = 0;
            int components = 0;
            bool readingValues = false;
            var values = new List<float>(AgxSpi1dLut.OfficialLength);

            using var reader = new StringReader(text);
            string line;

            while ((line = reader.ReadLine()) != null)
            {
                string trimmed = line.Trim();

                if (trimmed.Length == 0)
                {
                    continue;
                }

                if (trimmed == "{")
                {
                    readingValues = true;
                    continue;
                }

                if (trimmed == "}")
                {
                    readingValues = false;
                    continue;
                }

                if (readingValues)
                {
                    var tokens = trimmed.Split((char[])null, StringSplitOptions.RemoveEmptyEntries);

                    for (var i = 0; i < tokens.Length; i++)
                    {
                        string token = tokens[i];

                        values.Add(ParseFloat(token));
                    }

                    continue;
                }

                string[] parts = trimmed.Split((char[])null, StringSplitOptions.RemoveEmptyEntries);

                if (parts.Length == 0)
                {
                    continue;
                }

                switch (parts[0])
                {
                    case "Version" when parts.Length == 2:
                        version = int.Parse(parts[1], CultureInfo.InvariantCulture);
                        break;
                    case "From" when parts.Length == 3:
                        from = ParseFloat(parts[1]);
                        to = ParseFloat(parts[2]);
                        break;
                    case "Length" when parts.Length == 2:
                        length = int.Parse(parts[1], CultureInfo.InvariantCulture);
                        break;
                    case "Components" when parts.Length == 2:
                        components = int.Parse(parts[1], CultureInfo.InvariantCulture);
                        break;
                }
            }

            if (version == 0)
            {
                throw new InvalidDataException("Missing SPI1D Version header.");
            }

            if (length <= 0)
            {
                throw new InvalidDataException("Missing or invalid SPI1D Length header.");
            }

            if (components <= 0)
            {
                throw new InvalidDataException("Missing or invalid SPI1D Components header.");
            }

            if (values.Count != length * components)
            {
                throw new InvalidDataException($"SPI1D value count mismatch. Expected {length * components}, found {values.Count}.");
            }

            return new Spi1dData(version, from, to, length, components, values.ToArray());
        }

        private static float ParseFloat(string value)
        {
            return float.Parse(value, NumberStyles.Float, CultureInfo.InvariantCulture);
        }

        private static string ComputeGitBlobSha(byte[] bytes)
        {
            byte[] prefix = Encoding.ASCII.GetBytes($"blob {bytes.Length}\0");
            var blob = new byte[prefix.Length + bytes.Length];

            Buffer.BlockCopy(prefix, 0, blob, 0, prefix.Length);
            Buffer.BlockCopy(bytes, 0, blob, prefix.Length, bytes.Length);

            using var sha1 = SHA1.Create();
            byte[] hash = sha1.ComputeHash(blob);
            var builder = new StringBuilder(hash.Length * 2);

            for (var i = 0; i < hash.Length; i++)
            {
                byte b = hash[i];

                builder.Append(b.ToString("x2", CultureInfo.InvariantCulture));
            }

            return builder.ToString();
        }
    }
}
