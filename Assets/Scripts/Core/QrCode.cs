using System;
using System.Collections.Generic;
using System.Text;

namespace KingsCourt.Core
{
    /// <summary>
    /// A minimal, from-scratch QR code encoder (ISO/IEC 18004), byte mode, error correction level L,
    /// versions 1-6 only (a join link is always short, and versions above 6 need an extra "version
    /// information" block this class doesn't implement — not needed at this size). No Unity dependency,
    /// so it's testable and reusable the same way as the rest of Assets/Scripts/Core.
    /// Returns a module grid (true = dark); the caller draws it (see SpriteFactory.QrCode in the Unity layer).
    /// </summary>
    public static class QrEncoder
    {
        // Per-version (1-6) table for ECC level L: (totalCodewords, ecCodewordsPerBlock, blockSizes[] of data codewords per block).
        private static readonly int[] TotalCodewords = { 26, 44, 70, 100, 134, 172 };
        private static readonly int[] EcPerBlock = { 7, 10, 15, 20, 26, 18 };
        private static readonly int[][] BlockSizes =
        {
            new[] { 19 },
            new[] { 34 },
            new[] { 55 },
            new[] { 80 },
            new[] { 108 },
            new[] { 68, 68 },
        };
        private static readonly int[] RemainderBits = { 0, 7, 7, 7, 7, 7 };
        private static readonly int[] AlignmentCenter = { -1, -1, 18, 22, 26, 30, 34 }; // index = version, -1 = none

        public static bool[,] Encode(string text)
        {
            byte[] data = Encoding.UTF8.GetBytes(text ?? "");
            int version = ChooseVersion(data.Length);
            byte[] codewords = BuildDataCodewords(data, version);
            byte[] full = InterleaveWithEcc(codewords, version);
            bool[] bits = ToBits(full, RemainderBits[version - 1]);
            return BuildMatrix(bits, version);
        }

        // ------------------------------------------------------------------ data encoding

        private static int ChooseVersion(int byteLength)
        {
            for (int v = 1; v <= 6; v++)
            {
                int capacityBits = BlockSizesTotal(v) * 8;
                int neededBits = 4 + 8 + byteLength * 8; // mode + byte-mode count indicator (8 bits, versions 1-9) + data
                if (neededBits <= capacityBits) return v;
            }
            return 6; // longer than we ever expect; caller (a LAN URL) always fits well within version 6
        }

        private static int BlockSizesTotal(int version)
        {
            int sum = 0;
            foreach (var b in BlockSizes[version - 1]) sum += b;
            return sum;
        }

        private static byte[] BuildDataCodewords(byte[] data, int version)
        {
            int capacityBytes = BlockSizesTotal(version);
            var bits = new List<bool>(capacityBytes * 8);
            AppendBits(bits, 0b0100, 4);                          // byte mode
            AppendBits(bits, Math.Min(data.Length, 255), 8);      // character count (8 bits for versions 1-9)
            foreach (var b in data) AppendBits(bits, b, 8);

            int capacityBits = capacityBytes * 8;
            for (int i = 0; i < 4 && bits.Count < capacityBits; i++) bits.Add(false);   // terminator
            while (bits.Count % 8 != 0) bits.Add(false);                                 // pad to byte boundary

            var bytes = new List<byte>(capacityBytes);
            for (int i = 0; i < bits.Count; i += 8)
            {
                int v = 0;
                for (int j = 0; j < 8; j++) v = (v << 1) | (bits[i + j] ? 1 : 0);
                bytes.Add((byte)v);
            }
            bool fill = true; // alternate 0xEC / 0x11 padding codewords
            while (bytes.Count < capacityBytes) { bytes.Add(fill ? (byte)0xEC : (byte)0x11); fill = !fill; }
            return bytes.ToArray();
        }

        private static void AppendBits(List<bool> bits, int value, int count)
        {
            for (int i = count - 1; i >= 0; i--) bits.Add(((value >> i) & 1) != 0);
        }

        // ------------------------------------------------------------------ Reed-Solomon error correction (GF(256))

        private static readonly int[] GfExp = new int[512];
        private static readonly int[] GfLog = new int[256];

        static QrEncoder()
        {
            int x = 1;
            for (int i = 0; i < 255; i++)
            {
                GfExp[i] = x;
                GfLog[x] = i;
                x <<= 1;
                if ((x & 0x100) != 0) x ^= 0x11D; // primitive polynomial x^8+x^4+x^3+x^2+1
            }
            for (int i = 255; i < 512; i++) GfExp[i] = GfExp[i - 255];
        }

        private static int GfMul(int a, int b)
        {
            if (a == 0 || b == 0) return 0;
            return GfExp[GfLog[a] + GfLog[b]];
        }

        /// <summary>Returns the generator polynomial's coefficients MSB-first (index 0 = the leading,
        /// degree-`degree` coefficient, always 1) — the orientation ComputeEcc's synthetic division needs.</summary>
        private static int[] GeneratorPoly(int degree)
        {
            var g = new int[] { 1 };
            for (int i = 0; i < degree; i++)
            {
                var next = new int[g.Length + 1];
                for (int j = 0; j < g.Length; j++)
                {
                    next[j] ^= GfMul(g[j], GfExp[i]);
                    next[j + 1] ^= g[j];
                }
                g = next;
            }
            Array.Reverse(g);
            return g;
        }

        private static byte[] ComputeEcc(byte[] blockData, int ecCount)
        {
            var gen = GeneratorPoly(ecCount);
            var res = new int[blockData.Length + ecCount];
            for (int i = 0; i < blockData.Length; i++) res[i] = blockData[i];
            for (int i = 0; i < blockData.Length; i++)
            {
                int coef = res[i];
                if (coef == 0) continue;
                for (int j = 0; j < gen.Length; j++) res[i + j] ^= GfMul(gen[j], coef);
            }
            var ecc = new byte[ecCount];
            for (int i = 0; i < ecCount; i++) ecc[i] = (byte)res[blockData.Length + i];
            return ecc;
        }

        private static byte[] InterleaveWithEcc(byte[] allData, int version)
        {
            var sizes = BlockSizes[version - 1];
            int ecCount = EcPerBlock[version - 1];
            var dataBlocks = new byte[sizes.Length][];
            var eccBlocks = new byte[sizes.Length][];
            int offset = 0;
            for (int b = 0; b < sizes.Length; b++)
            {
                var block = new byte[sizes[b]];
                Array.Copy(allData, offset, block, 0, sizes[b]);
                offset += sizes[b];
                dataBlocks[b] = block;
                eccBlocks[b] = ComputeEcc(block, ecCount);
            }

            var outBytes = new List<byte>(TotalCodewords[version - 1]);
            int maxData = 0;
            foreach (var s in sizes) maxData = Math.Max(maxData, s);
            for (int i = 0; i < maxData; i++)
                for (int b = 0; b < sizes.Length; b++)
                    if (i < sizes[b]) outBytes.Add(dataBlocks[b][i]);
            for (int i = 0; i < ecCount; i++)
                for (int b = 0; b < sizes.Length; b++)
                    outBytes.Add(eccBlocks[b][i]);
            return outBytes.ToArray();
        }

        private static bool[] ToBits(byte[] bytes, int remainderBits)
        {
            var bits = new bool[bytes.Length * 8 + remainderBits];
            int k = 0;
            foreach (var b in bytes)
                for (int i = 7; i >= 0; i--) bits[k++] = ((b >> i) & 1) != 0;
            return bits;
        }

        // ------------------------------------------------------------------ matrix construction

        private static bool[,] BuildMatrix(bool[] bits, int version)
        {
            int size = version * 4 + 17;
            var isFunction = new bool[size, size];
            var modules = new bool[size, size];

            MarkFinder(isFunction, modules, 0, 0);
            MarkFinder(isFunction, modules, 0, size - 7);
            MarkFinder(isFunction, modules, size - 7, 0);

            // Timing patterns only span the gap BETWEEN the finder patterns (cols/rows 8..size-9) —
            // the finder patterns themselves already occupy rows/cols 0-7 and size-8..size-1.
            for (int i = 8; i <= size - 9; i++) { isFunction[6, i] = true; modules[6, i] = i % 2 == 0; }
            for (int i = 8; i <= size - 9; i++) { isFunction[i, 6] = true; modules[i, 6] = i % 2 == 0; }

            int align = AlignmentCenter[version];
            if (align > 0) MarkAlignment(isFunction, modules, align, align);

            int darkRow = version * 4 + 9;
            isFunction[darkRow, 8] = true; modules[darkRow, 8] = true;

            // Reserve format-info areas (filled in after masking, once, per the chosen best mask).
            for (int i = 0; i <= 8; i++) { if (i != 6) isFunction[8, i] = true; }
            for (int i = 0; i <= 8; i++) { if (i != 6) isFunction[i, 8] = true; }
            for (int i = 0; i < 8; i++) isFunction[8, size - 1 - i] = true;
            for (int i = 0; i < 7; i++) isFunction[size - 1 - i, 8] = true;

            PlaceData(modules, isFunction, bits, size);

            bool[,] best = null;
            int bestScore = int.MaxValue;
            for (int mask = 0; mask < 8; mask++)
            {
                var candidate = (bool[,])modules.Clone();
                ApplyMask(candidate, isFunction, mask, size);
                WriteFormatInfo(candidate, mask, size);
                int score = Score(candidate, size);
                if (score < bestScore) { bestScore = score; best = candidate; }
            }
            return best;
        }

        private static void MarkFinder(bool[,] isFunction, bool[,] modules, int r, int c)
        {
            for (int i = -1; i <= 7; i++)
            {
                for (int j = -1; j <= 7; j++)
                {
                    int rr = r + i, cc = c + j;
                    if (rr < 0 || cc < 0 || rr >= isFunction.GetLength(0) || cc >= isFunction.GetLength(0)) continue;
                    isFunction[rr, cc] = true;
                    bool dark = (i >= 0 && i <= 6 && j >= 0 && j <= 6) && (i == 0 || i == 6 || j == 0 || j == 6 || (i >= 2 && i <= 4 && j >= 2 && j <= 4));
                    modules[rr, cc] = dark;
                }
            }
        }

        private static void MarkAlignment(bool[,] isFunction, bool[,] modules, int r, int c)
        {
            for (int i = -2; i <= 2; i++)
                for (int j = -2; j <= 2; j++)
                {
                    isFunction[r + i, c + j] = true;
                    modules[r + i, c + j] = i == -2 || i == 2 || j == -2 || j == 2 || (i == 0 && j == 0);
                }
        }

        private static void PlaceData(bool[,] modules, bool[,] isFunction, bool[] bits, int size)
        {
            int bitIndex = 0;
            bool goingUp = true;
            for (int col = size - 1; col > 0; col -= 2)
            {
                if (col == 6) col--;
                for (int step = 0; step < size; step++)
                {
                    int row = goingUp ? size - 1 - step : step;
                    for (int c = 0; c < 2; c++)
                    {
                        int cc = col - c;
                        if (isFunction[row, cc]) continue;
                        modules[row, cc] = bitIndex < bits.Length && bits[bitIndex];
                        bitIndex++;
                    }
                }
                goingUp = !goingUp;
            }
        }

        private static void ApplyMask(bool[,] modules, bool[,] isFunction, int mask, int size)
        {
            for (int i = 0; i < size; i++)
            {
                for (int j = 0; j < size; j++)
                {
                    if (isFunction[i, j]) continue;
                    bool flip;
                    switch (mask)
                    {
                        case 0: flip = (i + j) % 2 == 0; break;
                        case 1: flip = i % 2 == 0; break;
                        case 2: flip = j % 3 == 0; break;
                        case 3: flip = (i + j) % 3 == 0; break;
                        case 4: flip = (i / 2 + j / 3) % 2 == 0; break;
                        case 5: flip = (i * j) % 2 + (i * j) % 3 == 0; break;
                        case 6: flip = ((i * j) % 2 + (i * j) % 3) % 2 == 0; break;
                        default: flip = ((i + j) % 2 + (i * j) % 3) % 2 == 0; break;
                    }
                    if (flip) modules[i, j] = !modules[i, j];
                }
            }
        }

        private static void WriteFormatInfo(bool[,] modules, int mask, int size)
        {
            int data = (0b01 << 3) | mask; // ECC level L = 1
            int bits = Bch(data) ^ 0x5412;

            // Placement mirrors the spec exactly (bit i = (bits >> i) & 1, i = 0 is the LSB):
            // vertical copy in column 8, then horizontal copy in row 8. The dark module at
            // (size-8, 8) is deliberately skipped by both (rows 8->size-7 jump over it).
            for (int i = 0; i < 15; i++)
            {
                bool mod = Bit(bits, i);
                if (i < 6) modules[i, 8] = mod;
                else if (i < 8) modules[i + 1, 8] = mod;
                else modules[size - 15 + i, 8] = mod;
            }
            for (int i = 0; i < 15; i++)
            {
                bool mod = Bit(bits, i);
                if (i < 8) modules[8, size - i - 1] = mod;
                else if (i < 9) modules[8, 15 - i] = mod;
                else modules[8, 14 - i] = mod;
            }
            modules[size - 8, 8] = true; // the fixed dark module
        }

        private static bool Bit(int value, int index) { return ((value >> index) & 1) != 0; }

        private static int Bch(int data)
        {
            const int gen = 0x537; // generator polynomial x^10+x^8+x^5+x^4+x^2+x+1
            int value = data << 10;
            for (int i = 14; i >= 10; i--)
                if (((value >> i) & 1) != 0) value ^= gen << (i - 10);
            return (data << 10) | value;
        }

        // ------------------------------------------------------------------ mask scoring (lower is better)

        private static int Score(bool[,] m, int size)
        {
            int score = 0;
            // Rule 1: runs of 5+ same-color modules, per row and column.
            for (int i = 0; i < size; i++)
            {
                score += RunPenalty(j => m[i, j], size);
                score += RunPenalty(j => m[j, i], size);
            }
            // Rule 2: 2x2 blocks of one color.
            for (int i = 0; i < size - 1; i++)
                for (int j = 0; j < size - 1; j++)
                    if (m[i, j] == m[i + 1, j] && m[i, j] == m[i, j + 1] && m[i, j] == m[i + 1, j + 1]) score += 3;
            // Rule 3: finder-like 1:1:3:1:1 pattern with 4 light modules on either side.
            for (int i = 0; i < size; i++)
            {
                score += FinderPatternPenalty(j => m[i, j], size);
                score += FinderPatternPenalty(j => m[j, i], size);
            }
            // Rule 4: overall dark-module percentage.
            int dark = 0;
            for (int i = 0; i < size; i++) for (int j = 0; j < size; j++) if (m[i, j]) dark++;
            int percent = dark * 100 / (size * size);
            int k = Math.Abs(percent - 50) / 5;
            score += k * 10;
            return score;
        }

        private static int RunPenalty(Func<int, bool> get, int size)
        {
            int penalty = 0, run = 1;
            bool prev = get(0);
            for (int i = 1; i < size; i++)
            {
                bool cur = get(i);
                if (cur == prev) { run++; }
                else { if (run >= 5) penalty += 3 + (run - 5); run = 1; prev = cur; }
            }
            if (run >= 5) penalty += 3 + (run - 5);
            return penalty;
        }

        private static int FinderPatternPenalty(Func<int, bool> get, int size)
        {
            int penalty = 0;
            for (int i = 0; i + 10 < size; i++)
            {
                if (MatchesFinderRun(get, i, true) || MatchesFinderRun(get, i, false)) penalty += 40;
            }
            return penalty;
        }

        private static bool MatchesFinderRun(Func<int, bool> get, int start, bool darkFirst)
        {
            // dark,light,dark,dark,dark,light,dark preceded/followed by 4 light modules.
            bool[] pattern = darkFirst
                ? new[] { true, false, true, true, true, false, true }
                : new[] { true, false, true, true, true, false, true }; // symmetric pattern, only one form in the spec
            for (int i = 0; i < 7; i++) if (get(start + i) != pattern[i]) return false;
            for (int i = -4; i < 0; i++) if (start + i >= 0 && get(start + i)) return false;
            for (int i = 7; i < 11; i++) if (get(start + i)) return false;
            return true;
        }
    }
}
