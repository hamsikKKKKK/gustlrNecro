using UnityEngine;
using UnityEngine.SceneManagement;
using System.Collections;

namespace Necrocis
{
    /// <summary>
    /// 씬 전환 관리자
    /// </summary>
    public class SceneLoader : MonoBehaviour
    {
        public static SceneLoader Instance { get; private set; }

        [Header("씬 이름")]
        public const string SCENE_HUB = "Hub";
        public const string SCENE_INTESTINE = "Biome";
        public const string SCENE_LIVER = "Biome_Liver";
        public const string SCENE_STOMACH = "Biome_Stomach";
        public const string SCENE_LUNG = "Biome_Lung";

        private bool isLoading = false;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }

        /// <summary>
        /// 바이옴 타입으로 씬 이름 가져오기
        /// </summary>
        public static string GetSceneName(BiomeType biome)
        {
            return biome switch
            {
                BiomeType.Intestine => SCENE_INTESTINE,
                BiomeType.Liver => SCENE_LIVER,
                BiomeType.Stomach => SCENE_STOMACH,
                BiomeType.Lung => SCENE_LUNG,
                BiomeType.None => SCENE_HUB,  // None이면 허브로
                _ => SCENE_HUB
            };
        }

        /// <summary>
        /// 바이옴으로 이동
        /// </summary>
        public void LoadBiome(BiomeType biome)
        {
            if (isLoading) return;

            string sceneName = GetSceneName(biome);
            StartCoroutine(LoadSceneAsync(sceneName));
        }

        /// <summary>
        /// 허브로 돌아가기
        /// </summary>
        public void ReturnToHub()
        {
            if (isLoading) return;
            StartCoroutine(LoadSceneAsync(SCENE_HUB));
        }

        /// <summary>
        /// 비동기 씬 로드
        /// </summary>
        private IEnumerator LoadSceneAsync(string sceneName)
        {
            isLoading = true;

            // TODO: 페이드 아웃 효과 추가 가능

            Debug.Log($"[SceneLoader] {sceneName} 씬 로딩 시작...");

            AsyncOperation asyncLoad = SceneManager.LoadSceneAsync(sceneName);

            while (!asyncLoad.isDone)
            {
                // 로딩 진행률: asyncLoad.progress
                yield return null;
            }

            Debug.Log($"[SceneLoader] {sceneName} 씬 로딩 완료!");

            isLoading = false;

            // TODO: 페이드 인 효과 추가 가능
        }

        /// <summary>
        /// 현재 씬 이름
        /// </summary>
        public string GetCurrentSceneName()
        {
            return SceneManager.GetActiveScene().name;
        }

        /// <summary>
        /// 현재 허브인지 확인
        /// </summary>
        public bool IsInHub()
        {
            return GetCurrentSceneName() == SCENE_HUB;
        }
    }
}
