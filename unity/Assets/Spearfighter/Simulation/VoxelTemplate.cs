using System;
using System.Collections.Generic;
using System.Text;

namespace Spearfighter.Simulation
{
    /// <summary>
    /// A player-authored build shape (WS4 custom voxel authoring): a bounded
    /// SizeX×SizeY×SizeZ grid of filled/empty cells. Local axes: X = width, Y = up,
    /// Z = depth (extends away from the builder when placed). Engine-agnostic — the
    /// voxel editor UI authors one of these, the sim places it (SimCore.TryGetBuild-
    /// Placement), and it serialises to a compact string so it can be stored per
    /// player now and (WS10) replicated + rebuilt identically on the server later.
    /// The same Cell representation the default staircase and collision already use.
    /// </summary>
    public sealed class VoxelTemplate
    {
        public readonly int SizeX, SizeY, SizeZ;
        private readonly bool[] _cells;

        public VoxelTemplate(int sizeX, int sizeY, int sizeZ)
        {
            SizeX = Math.Max(1, sizeX); SizeY = Math.Max(1, sizeY); SizeZ = Math.Max(1, sizeZ);
            _cells = new bool[SizeX * SizeY * SizeZ];
        }

        private int Index(int x, int y, int z) => (y * SizeZ + z) * SizeX + x;
        public bool InBounds(int x, int y, int z) => x >= 0 && x < SizeX && y >= 0 && y < SizeY && z >= 0 && z < SizeZ;

        public bool Get(int x, int y, int z) => InBounds(x, y, z) && _cells[Index(x, y, z)];
        public void Set(int x, int y, int z, bool value) { if (InBounds(x, y, z)) _cells[Index(x, y, z)] = value; }
        public void Toggle(int x, int y, int z) { if (InBounds(x, y, z)) _cells[Index(x, y, z)] = !_cells[Index(x, y, z)]; }
        public void Clear() { for (int i = 0; i < _cells.Length; i++) _cells[i] = false; }

        public bool IsEmpty { get { for (int i = 0; i < _cells.Length; i++) if (_cells[i]) return false; return true; } }
        public int Count { get { int n = 0; for (int i = 0; i < _cells.Length; i++) if (_cells[i]) n++; return n; } }

        /// <summary>Filled cells as local (x,y,z) offsets.</summary>
        public IEnumerable<Cell> FilledCells()
        {
            for (int y = 0; y < SizeY; y++)
                for (int z = 0; z < SizeZ; z++)
                    for (int x = 0; x < SizeX; x++)
                        if (_cells[Index(x, y, z)]) yield return new Cell(x, y, z);
        }

        /// <summary>Compact, round-trippable string: "SX,SY,SZ:hexbitmask".</summary>
        public string Encode()
        {
            var sb = new StringBuilder();
            sb.Append(SizeX).Append(',').Append(SizeY).Append(',').Append(SizeZ).Append(':');
            int nbytes = (_cells.Length + 7) / 8;
            for (int b = 0; b < nbytes; b++)
            {
                int val = 0;
                for (int bit = 0; bit < 8; bit++)
                {
                    int i = b * 8 + bit;
                    if (i < _cells.Length && _cells[i]) val |= 1 << bit;
                }
                sb.Append(val.ToString("x2"));
            }
            return sb.ToString();
        }

        /// <summary>Parse a string produced by <see cref="Encode"/>. Null on bad input.</summary>
        public static VoxelTemplate Decode(string s)
        {
            if (string.IsNullOrEmpty(s)) return null;
            int colon = s.IndexOf(':');
            if (colon <= 0) return null;
            var dims = s.Substring(0, colon).Split(',');
            if (dims.Length != 3) return null;
            if (!int.TryParse(dims[0], out int sx) || !int.TryParse(dims[1], out int sy) || !int.TryParse(dims[2], out int sz))
                return null;
            var t = new VoxelTemplate(sx, sy, sz);
            string hex = s.Substring(colon + 1);
            int total = t._cells.Length;
            for (int b = 0; b * 2 + 1 < hex.Length; b++)
            {
                if (!int.TryParse(hex.Substring(b * 2, 2), System.Globalization.NumberStyles.HexNumber, null, out int val)) continue;
                for (int bit = 0; bit < 8; bit++)
                {
                    int i = b * 8 + bit;
                    if (i < total && (val & (1 << bit)) != 0) t._cells[i] = true;
                }
            }
            return t;
        }
    }
}
