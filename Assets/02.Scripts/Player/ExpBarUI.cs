using UnityEngine;
using UnityEngine.UI;

namespace Necrocis
{
    /// <summary>
    /// 화면 하단 경험치 바 + 레벨 표시.
    /// Canvas가 없으면 자동 생성.
    /// </summary>
    public class ExpBarUI : MonoBehaviour
    {
        [Header("바 색상")]
        [SerializeField] private Color barColor = new Color(0.3f, 0.7f, 1f, 1f);
        [SerializeField] private Color bgColor = new Color(0.15f, 0.15f, 0.15f, 0.8f);

        private Image fillImage;
        private Text levelText;
        private Text expText;
        private int lastLevel;

        private void Start()
        {
            BuildUI();
            lastLevel = LevelUpManager.GetCurrentLevel();
            UpdateDisplay();
        }

        private void OnEnable()
        {
            LevelUpManager.OnExpGained += OnExpGained;
            LevelUpManager.OnLevelUp += OnLevelUp;
        }

        private void OnDisable()
        {
            LevelUpManager.OnExpGained -= OnExpGained;
            LevelUpManager.OnLevelUp -= OnLevelUp;
        }

        private void OnExpGained(int amount) => UpdateDisplay();
        private void OnLevelUp() => UpdateDisplay();

        private void UpdateDisplay()
        {
            int level = LevelUpManager.GetCurrentLevel();
            float progress = LevelUpManager.GetExpProgress();

            if (fillImage != null)
                fillImage.fillAmount = progress;

            if (levelText != null)
                levelText.text = $"Lv.{level}";

            if (expText != null)
                expText.text = $"{LevelUpManager.GetCurrentExp()} / {LevelUpManager.GetExpRequired()}";
        }

        private void BuildUI()
        {
            GameObject canvasObj = new GameObject("ExpBarCanvas");
            canvasObj.transform.SetParent(transform);
            Canvas canvas = canvasObj.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 50;
            CanvasScaler scaler = canvasObj.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            canvasObj.AddComponent<GraphicRaycaster>();

            // 바 컨테이너 (화면 하단)
            GameObject barRoot = CreateUIElement("ExpBar", canvasObj.transform);
            RectTransform barRect = barRoot.GetComponent<RectTransform>();
            barRect.anchorMin = new Vector2(0.2f, 0f);
            barRect.anchorMax = new Vector2(0.8f, 0f);
            barRect.pivot = new Vector2(0.5f, 0f);
            barRect.anchoredPosition = new Vector2(0, 20);
            barRect.sizeDelta = new Vector2(0, 25);

            // 배경
            Image bgImage = barRoot.AddComponent<Image>();
            bgImage.color = bgColor;

            // 채움
            GameObject fill = CreateUIElement("Fill", barRoot.transform);
            RectTransform fillRect = fill.GetComponent<RectTransform>();
            fillRect.anchorMin = Vector2.zero;
            fillRect.anchorMax = Vector2.one;
            fillRect.offsetMin = Vector2.zero;
            fillRect.offsetMax = Vector2.zero;
            fillImage = fill.AddComponent<Image>();
            fillImage.color = barColor;
            fillImage.type = Image.Type.Filled;
            fillImage.fillMethod = Image.FillMethod.Horizontal;
            fillImage.fillAmount = 0f;

            // 레벨 텍스트 (바 왼쪽)
            GameObject lvlObj = CreateUIElement("Level", canvasObj.transform);
            RectTransform lvlRect = lvlObj.GetComponent<RectTransform>();
            lvlRect.anchorMin = new Vector2(0.1f, 0f);
            lvlRect.anchorMax = new Vector2(0.2f, 0f);
            lvlRect.pivot = new Vector2(0.5f, 0f);
            lvlRect.anchoredPosition = new Vector2(0, 20);
            lvlRect.sizeDelta = new Vector2(0, 25);

            levelText = lvlObj.AddComponent<Text>();
            levelText.text = "Lv.1";
            levelText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            levelText.fontSize = 18;
            levelText.color = Color.white;
            levelText.alignment = TextAnchor.MiddleCenter;

            // EXP 텍스트 (바 위)
            GameObject expObj = CreateUIElement("ExpText", barRoot.transform);
            RectTransform expRect = expObj.GetComponent<RectTransform>();
            expRect.anchorMin = Vector2.zero;
            expRect.anchorMax = Vector2.one;
            expRect.offsetMin = Vector2.zero;
            expRect.offsetMax = Vector2.zero;

            expText = expObj.AddComponent<Text>();
            expText.text = "0 / 100";
            expText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            expText.fontSize = 14;
            expText.color = Color.white;
            expText.alignment = TextAnchor.MiddleCenter;
        }

        private GameObject CreateUIElement(string name, Transform parent)
        {
            GameObject obj = new GameObject(name, typeof(RectTransform));
            obj.transform.SetParent(parent, false);
            return obj;
        }
    }
}
