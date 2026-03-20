using UnityEngine;
using UnityEngine.UI;

namespace Necrocis
{
    /// <summary>
    /// O키로 열고 닫는 스탯 확인 UI.
    /// 현재 스탯과 기본값 대비 증감을 표시.
    /// </summary>
    public class StatUI : MonoBehaviour
    {
        [Header("UI 설정")]
        [SerializeField] private Color panelColor = new Color(0f, 0f, 0f, 0.85f);
        [SerializeField] private Color positiveColor = new Color(0.4f, 1f, 0.4f);
        [SerializeField] private Color negativeColor = new Color(1f, 0.4f, 0.4f);

        private GameObject uiRoot;
        private Text contentText;
        private bool isShowing;

        private static readonly CharacterStatType[] displayStats = new CharacterStatType[]
        {
            CharacterStatType.MaxHealth,
            CharacterStatType.AttackPower,
            CharacterStatType.Defense,
            CharacterStatType.MoveSpeed,
            CharacterStatType.AttackSpeed,
            CharacterStatType.Range,
            CharacterStatType.Magic,
            CharacterStatType.Cooldown
        };

        private void Start()
        {
            BuildUI();
            uiRoot.SetActive(false);
        }

        private void Update()
        {
            var input = InputManager.Instance;
            if (input == null) return;

            if (input.StatWindowAction.WasPressedThisFrame())
            {
                if (isShowing)
                    Hide();
                else
                    Show();
            }

            if (isShowing)
                RefreshStats();
        }

        private void Show()
        {
            RefreshStats();
            uiRoot.SetActive(true);
            isShowing = true;
        }

        private void Hide()
        {
            uiRoot.SetActive(false);
            isShowing = false;
        }

        private void RefreshStats()
        {
            if (PlayerStats.Instance == null) return;

            var stats = PlayerStats.Instance.RuntimeStats;
            if (stats == null) return;

            string posHex = ColorUtility.ToHtmlStringRGB(positiveColor);
            string negHex = ColorUtility.ToHtmlStringRGB(negativeColor);

            System.Text.StringBuilder sb = new System.Text.StringBuilder();

            // 레벨
            sb.AppendLine($"<color=#FFD933><b>스탯 정보</b></color>");
            sb.AppendLine($"Lv.{LevelUpManager.GetCurrentLevel()}");

            // 직업
            JobType job = LevelUpManager.GetCurrentJob();
            string jobName = job == JobType.None ? "없음" : GetJobName(job);
            sb.AppendLine($"<color=#99CCFF>직업: {jobName}</color>");
            sb.AppendLine();

            // HP (현재/최대 형태)
            float hp = stats.CurrentHealth;
            float maxHp = stats.MaxHealth;
            float hpBase = stats.GetBaseValue(CharacterStatType.MaxHealth);
            float hpDiff = maxHp - hpBase;
            sb.Append($"체력: {hp:F0}/{maxHp:F0}");
            if (Mathf.Abs(hpDiff) > 0.01f)
                sb.Append(hpDiff > 0 ? $" <color=#{posHex}>(+{hpDiff:F1})</color>" : $" <color=#{negHex}>({hpDiff:F1})</color>");
            sb.AppendLine();

            // 나머지 스탯
            for (int i = 1; i < displayStats.Length; i++)
            {
                CharacterStatType type = displayStats[i];
                float current = stats.GetValue(type);
                float baseVal = stats.GetBaseValue(type);
                float diff = current - baseVal;

                sb.Append($"{GetStatName(type)}: {current:F1}");
                if (Mathf.Abs(diff) > 0.01f)
                {
                    if (diff > 0)
                        sb.Append($" <color=#{posHex}>(+{diff:F1})</color>");
                    else
                        sb.Append($" <color=#{negHex}>({diff:F1})</color>");
                }
                sb.AppendLine();
            }

            contentText.text = sb.ToString();
        }

        private string GetStatName(CharacterStatType type)
        {
            switch (type)
            {
                case CharacterStatType.MaxHealth:   return "체력";
                case CharacterStatType.AttackPower:  return "공격력";
                case CharacterStatType.Defense:      return "방어력";
                case CharacterStatType.MoveSpeed:    return "이동속도";
                case CharacterStatType.AttackSpeed:  return "공격속도";
                case CharacterStatType.Range:        return "사거리";
                case CharacterStatType.Magic:        return "마력";
                case CharacterStatType.Cooldown:     return "쿨타임";
                default:                             return type.ToString();
            }
        }

        private string GetJobName(JobType job)
        {
            switch (job)
            {
                case JobType.Warrior: return "전사";
                case JobType.Mage:    return "마법사";
                case JobType.Archer:  return "궁수";
                default:              return job.ToString();
            }
        }

        // ─────────────────────────────────
        // UI 자동 생성
        // ─────────────────────────────────

        private void BuildUI()
        {
            // Canvas
            GameObject canvasObj = new GameObject("StatUICanvas");
            canvasObj.transform.SetParent(transform);
            Canvas canvas = canvasObj.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 90;
            CanvasScaler scaler = canvasObj.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            canvasObj.AddComponent<GraphicRaycaster>();

            uiRoot = canvasObj;

            // 패널 (왼쪽 상단 고정, 내용에 맞게 크기 조절)
            GameObject panel = new GameObject("StatPanel", typeof(RectTransform));
            panel.transform.SetParent(canvasObj.transform, false);
            RectTransform panelRect = panel.GetComponent<RectTransform>();
            panelRect.anchorMin = new Vector2(0f, 1f);
            panelRect.anchorMax = new Vector2(0f, 1f);
            panelRect.pivot = new Vector2(0f, 1f);
            panelRect.anchoredPosition = new Vector2(10, -10);

            Image panelImg = panel.AddComponent<Image>();
            panelImg.color = panelColor;

            ContentSizeFitter fitter = panel.AddComponent<ContentSizeFitter>();
            fitter.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            VerticalLayoutGroup vlg = panel.AddComponent<VerticalLayoutGroup>();
            vlg.padding = new RectOffset(20, 20, 15, 15);
            vlg.childControlWidth = true;
            vlg.childControlHeight = true;
            vlg.childForceExpandWidth = false;
            vlg.childForceExpandHeight = false;

            // 텍스트 1개로 모든 정보 표시
            GameObject textObj = new GameObject("Content", typeof(RectTransform));
            textObj.transform.SetParent(panel.transform, false);

            contentText = textObj.AddComponent<Text>();
            contentText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            contentText.fontSize = 22;
            contentText.color = Color.white;
            contentText.alignment = TextAnchor.UpperLeft;
            contentText.supportRichText = true;
            contentText.horizontalOverflow = HorizontalWrapMode.Overflow;
            contentText.verticalOverflow = VerticalWrapMode.Overflow;
        }
    }
}
