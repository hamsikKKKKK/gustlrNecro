using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

namespace Necrocis
{
    /// <summary>
    /// 레벨업 시 스탯 선택 UI.
    /// Canvas가 없으면 자동 생성. LevelUpManager.OnLevelUp 이벤트에 연결.
    /// </summary>
    public class LevelUpUI : MonoBehaviour
    {
        public static LevelUpUI Instance { get; private set; }

        [Header("UI 설정")]
        [SerializeField] private Color panelColor = new Color(0f, 0f, 0f, 0.85f);
        [SerializeField] private Color buttonColor = new Color(0.2f, 0.2f, 0.3f, 1f);
        [SerializeField] private Color buttonHoverColor = new Color(0.3f, 0.3f, 0.5f, 1f);
        [SerializeField] private int fontSize = 20;

        private GameObject uiRoot;
        private Transform buttonContainer;
        private Text titleText;
        private Text levelText;
        private Text guideText;
        private List<GameObject> choiceButtons = new List<GameObject>();
        private List<StatChoice> currentChoices = new List<StatChoice>();
        private bool isShowing;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
        }

        private void OnEnable()
        {
            LevelUpManager.OnLevelUp += ShowLevelUpChoices;
        }

        private void OnDisable()
        {
            LevelUpManager.OnLevelUp -= ShowLevelUpChoices;
        }

        private void Start()
        {
            EnsureEventSystem();
            BuildUI();
            uiRoot.SetActive(false);
        }

        private void EnsureEventSystem()
        {
            if (FindFirstObjectByType<EventSystem>() == null)
            {
                GameObject esObj = new GameObject("EventSystem");
                esObj.AddComponent<EventSystem>();
                esObj.AddComponent<InputSystemUIInputModule>();
            }
        }

        private void Update()
        {
            if (!isShowing) return;

            var keyboard = Keyboard.current;
            if (keyboard == null) return;

            if (keyboard.digit1Key.wasPressedThisFrame && currentChoices.Count >= 1)
                SelectChoice(currentChoices[0]);
            else if (keyboard.digit2Key.wasPressedThisFrame && currentChoices.Count >= 2)
                SelectChoice(currentChoices[1]);
            else if (keyboard.digit3Key.wasPressedThisFrame && currentChoices.Count >= 3)
                SelectChoice(currentChoices[2]);
            else if (keyboard.digit4Key.wasPressedThisFrame && currentChoices.Count >= 4)
                SelectChoice(currentChoices[3]);
        }

        private void ShowLevelUpChoices()
        {
            List<StatChoice> choices = LevelUpManager.GetRandomChoices();
            if (choices == null || choices.Count == 0) return;

            currentChoices = choices;
            levelText.text = $"Lv.{LevelUpManager.GetCurrentLevel()}";
            ClearButtons();

            for (int i = 0; i < choices.Count; i++)
            {
                CreateChoiceButton(choices[i], i + 1);
            }

            uiRoot.SetActive(true);
            isShowing = true;
            Time.timeScale = 0f;
        }

        private void SelectChoice(StatChoice choice)
        {
            if (PlayerStats.Instance != null)
                PlayerStats.Instance.ApplyStatChoice(choice);

            LevelUpManager.RecordSelection(choice);

            // 대기 중인 레벨업이 있으면 다음 선택지 표시
            if (LevelUpManager.HasPendingLevelUp())
            {
                LevelUpManager.ProcessNextPendingLevelUp();
            }
            else
            {
                uiRoot.SetActive(false);
                isShowing = false;
                Time.timeScale = 1f;
            }
        }

        private void ClearButtons()
        {
            foreach (var btn in choiceButtons)
                Destroy(btn);
            choiceButtons.Clear();
        }

        private void CreateChoiceButton(StatChoice choice, int index)
        {
            GameObject btnObj = new GameObject(choice.ToString(), typeof(RectTransform));
            btnObj.transform.SetParent(buttonContainer, false);

            Image btnImage = btnObj.AddComponent<Image>();
            btnImage.color = buttonColor;

            Button btn = btnObj.AddComponent<Button>();
            ColorBlock colors = btn.colors;
            colors.normalColor = buttonColor;
            colors.highlightedColor = buttonHoverColor;
            colors.pressedColor = buttonHoverColor;
            colors.selectedColor = buttonColor;
            btn.colors = colors;

            StatChoice captured = choice;
            btn.onClick.AddListener(() => SelectChoice(captured));

            LayoutElement layout = btnObj.AddComponent<LayoutElement>();
            layout.preferredHeight = 70;
            layout.flexibleWidth = 1;

            // 버튼 텍스트
            GameObject textObj = new GameObject("Text", typeof(RectTransform));
            textObj.transform.SetParent(btnObj.transform, false);
            RectTransform textRect = textObj.GetComponent<RectTransform>();
            textRect.anchorMin = Vector2.zero;
            textRect.anchorMax = Vector2.one;
            textRect.offsetMin = new Vector2(10, 5);
            textRect.offsetMax = new Vector2(-10, -5);

            Text text = textObj.AddComponent<Text>();
            text.text = $"[{index}] {GetChoiceDescription(choice)}";
            text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            text.fontSize = fontSize;
            text.color = Color.white;
            text.alignment = TextAnchor.MiddleCenter;
            text.resizeTextForBestFit = true;
            text.resizeTextMinSize = 14;
            text.resizeTextMaxSize = fontSize;

            choiceButtons.Add(btnObj);
        }

        private string GetChoiceDescription(StatChoice choice)
        {
            StatEffect effect = StatManager.GetStatEffect(choice);
            List<string> parts = new List<string>();

            foreach (var stat in effect.flatStats)
            {
                string sign = stat.Value >= 0 ? "+" : "";
                parts.Add($"{GetStatName(stat.Key)} {sign}{stat.Value}");
            }

            foreach (var stat in effect.percentStats)
            {
                string sign = stat.Value >= 0 ? "+" : "";
                parts.Add($"{GetStatName(stat.Key)} {sign}{stat.Value}%");
            }

            return string.Join(", ", parts);
        }

        private string GetStatName(StatType type)
        {
            switch (type)
            {
                case StatType.Health:      return "체력";
                case StatType.Speed:       return "이동속도";
                case StatType.Attack:      return "공격력";
                case StatType.Defense:     return "방어력";
                case StatType.AttackSpeed: return "공격속도";
                case StatType.Range:       return "사거리";
                case StatType.Magic:       return "마력";
                case StatType.Cooldown:    return "쿨타임";
                default:                   return type.ToString();
            }
        }

        // ─────────────────────────────────
        // UI 자동 생성
        // ─────────────────────────────────

        private void BuildUI()
        {
            // Canvas
            GameObject canvasObj = new GameObject("LevelUpCanvas");
            canvasObj.transform.SetParent(transform);
            Canvas canvas = canvasObj.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 100;
            canvasObj.AddComponent<CanvasScaler>().uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            canvasObj.GetComponent<CanvasScaler>().referenceResolution = new Vector2(1920, 1080);
            canvasObj.AddComponent<GraphicRaycaster>();

            uiRoot = canvasObj;

            // 배경 패널
            GameObject panel = CreateUIElement("Panel", canvasObj.transform);
            RectTransform panelRect = panel.GetComponent<RectTransform>();
            panelRect.anchorMin = Vector2.zero;
            panelRect.anchorMax = Vector2.one;
            panelRect.offsetMin = Vector2.zero;
            panelRect.offsetMax = Vector2.zero;
            Image panelImg = panel.AddComponent<Image>();
            panelImg.color = panelColor;

            // 중앙 컨테이너
            GameObject container = CreateUIElement("Container", panel.transform);
            RectTransform containerRect = container.GetComponent<RectTransform>();
            containerRect.anchorMin = new Vector2(0.25f, 0.15f);
            containerRect.anchorMax = new Vector2(0.75f, 0.85f);
            containerRect.offsetMin = Vector2.zero;
            containerRect.offsetMax = Vector2.zero;

            VerticalLayoutGroup vlg = container.AddComponent<VerticalLayoutGroup>();
            vlg.spacing = 15;
            vlg.childAlignment = TextAnchor.UpperCenter;
            vlg.childControlWidth = true;
            vlg.childControlHeight = false;
            vlg.childForceExpandWidth = true;
            vlg.childForceExpandHeight = false;
            vlg.padding = new RectOffset(20, 20, 20, 20);

            // 타이틀
            GameObject titleObj = CreateUIElement("Title", container.transform);
            LayoutElement titleLayout = titleObj.AddComponent<LayoutElement>();
            titleLayout.preferredHeight = 50;
            titleText = titleObj.AddComponent<Text>();
            titleText.text = "LEVEL UP!";
            titleText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            titleText.fontSize = 36;
            titleText.fontStyle = FontStyle.Bold;
            titleText.color = new Color(1f, 0.85f, 0.2f);
            titleText.alignment = TextAnchor.MiddleCenter;

            // 레벨 표시
            GameObject levelObj = CreateUIElement("Level", container.transform);
            LayoutElement levelLayout = levelObj.AddComponent<LayoutElement>();
            levelLayout.preferredHeight = 35;
            levelText = levelObj.AddComponent<Text>();
            levelText.text = "Lv.1";
            levelText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            levelText.fontSize = 24;
            levelText.color = Color.white;
            levelText.alignment = TextAnchor.MiddleCenter;

            // 안내 텍스트
            GameObject guideObj = CreateUIElement("Guide", container.transform);
            LayoutElement guideLayout = guideObj.AddComponent<LayoutElement>();
            guideLayout.preferredHeight = 30;
            guideText = guideObj.AddComponent<Text>();
            guideText.text = "숫자키(1~4)로 능력을 선택하세요";
            guideText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            guideText.fontSize = 18;
            guideText.color = new Color(0.8f, 0.8f, 0.8f);
            guideText.alignment = TextAnchor.MiddleCenter;

            // 버튼 컨테이너
            GameObject btnContainer = CreateUIElement("Buttons", container.transform);
            VerticalLayoutGroup btnVlg = btnContainer.AddComponent<VerticalLayoutGroup>();
            btnVlg.spacing = 10;
            btnVlg.childControlWidth = true;
            btnVlg.childControlHeight = false;
            btnVlg.childForceExpandWidth = true;
            btnVlg.childForceExpandHeight = false;

            LayoutElement btnContainerLayout = btnContainer.AddComponent<LayoutElement>();
            btnContainerLayout.flexibleHeight = 1;

            buttonContainer = btnContainer.transform;
        }

        private GameObject CreateUIElement(string name, Transform parent)
        {
            GameObject obj = new GameObject(name, typeof(RectTransform));
            obj.transform.SetParent(parent, false);
            return obj;
        }
    }
}
