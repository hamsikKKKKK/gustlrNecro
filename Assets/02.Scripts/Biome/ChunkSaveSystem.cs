using UnityEngine;
using System.Collections.Generic;
using System.IO;

namespace Necrocis
{
    /// <summary>
    /// 청크 저장/로드 시스템
    /// </summary>
    public static class ChunkSaveSystem
    {
        private static string SavePath => Path.Combine(Application.persistentDataPath, "ChunkData");

        /// <summary>
        /// 청크 데이터 저장
        /// </summary>
        public static void SaveChunk(BiomeType biome, int chunkX, int chunkY, ChunkSaveData data)
        {
            string directory = Path.Combine(SavePath, biome.ToString());
            if (!Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
            }

            string filePath = Path.Combine(directory, $"chunk_{chunkX}_{chunkY}.json");
            string json = JsonUtility.ToJson(data, true);
            File.WriteAllText(filePath, json);
        }

        /// <summary>
        /// 청크 데이터 로드
        /// </summary>
        public static ChunkSaveData LoadChunk(BiomeType biome, int chunkX, int chunkY)
        {
            string filePath = Path.Combine(SavePath, biome.ToString(), $"chunk_{chunkX}_{chunkY}.json");

            if (File.Exists(filePath))
            {
                string json = File.ReadAllText(filePath);
                return JsonUtility.FromJson<ChunkSaveData>(json);
            }

            return null;
        }

        /// <summary>
        /// 청크가 저장되어 있는지 확인
        /// </summary>
        public static bool HasSavedChunk(BiomeType biome, int chunkX, int chunkY)
        {
            string filePath = Path.Combine(SavePath, biome.ToString(), $"chunk_{chunkX}_{chunkY}.json");
            return File.Exists(filePath);
        }

        /// <summary>
        /// 바이옴 전체 청크 데이터 삭제
        /// </summary>
        public static void ClearBiomeData(BiomeType biome)
        {
            string directory = Path.Combine(SavePath, biome.ToString());
            if (Directory.Exists(directory))
            {
                Directory.Delete(directory, true);
            }
        }

        /// <summary>
        /// 전체 저장 데이터 삭제
        /// </summary>
        public static void ClearAllData()
        {
            if (Directory.Exists(SavePath))
            {
                Directory.Delete(SavePath, true);
            }
        }
    }

    /// <summary>
    /// 청크 저장 데이터
    /// </summary>
    [System.Serializable]
    public class ChunkSaveData
    {
        public int chunkX;
        public int chunkY;
        public int seed;
        public bool isGenerated;

        // 타일 데이터 (직렬화용)
        public List<TileSaveData> tiles = new List<TileSaveData>();

        // 오브젝트 데이터 (직렬화용)
        public List<ObjectSaveData> objects = new List<ObjectSaveData>();
    }

    /// <summary>
    /// 타일 저장 데이터
    /// </summary>
    [System.Serializable]
    public class TileSaveData
    {
        public int localX;  // 청크 내 로컬 좌표
        public int localY;
        public int tileType;
        public bool isWalkable;
        public string spriteKey;  // 스프라이트 식별자
    }

    /// <summary>
    /// 오브젝트 저장 데이터
    /// </summary>
    [System.Serializable]
    public class ObjectSaveData
    {
        public int localX;
        public int localY;
        public int objectType;
        public string spriteKey;
        public bool isDestroyed;  // 파괴되었는지
        public bool isCollected;  // 수집되었는지 (아이템)
    }
}
