using UnityEngine;

namespace Necrocis
{
    /// <summary>
    /// 시드 + 좌표 기반 결정론 유틸
    /// </summary>
    public static class BiomeDeterministic
    {
        public static float Hash01(int seed, int x, int y, int salt = 0)
        {
            uint h = Hash((uint)seed, (uint)x, (uint)y, (uint)salt);
            return (h & 0x00FFFFFF) / 16777216f; // 0..1
        }

        public static int HashRange(int seed, int x, int y, int salt, int range)
        {
            if (range <= 1) return 0;
            uint h = Hash((uint)seed, (uint)x, (uint)y, (uint)salt);
            return (int)(h % (uint)range);
        }

        public static Vector2 HashInCell(int seed, int cellX, int cellY, int salt)
        {
            float ox = Hash01(seed, cellX, cellY, salt);
            float oy = Hash01(seed, cellX, cellY, salt + 1337);
            return new Vector2(ox, oy);
        }

        private static uint Hash(uint seed, uint x, uint y, uint salt)
        {
            uint h = seed;
            h ^= x * 0x9E3779B9u;
            h ^= y * 0x85EBCA6Bu;
            h ^= salt * 0xC2B2AE35u;
            h ^= h >> 16;
            h *= 0x7FEB352Du;
            h ^= h >> 15;
            h *= 0x846CA68Bu;
            h ^= h >> 16;
            return h;
        }
    }
}
