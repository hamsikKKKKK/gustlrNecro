using UnityEngine;
using UnityEngine.InputSystem;

namespace Necrocis
{
    /// <summary>
    /// 입력 관리 싱글톤.
    /// 모든 InputAction을 코드로 정의하고, 리바인딩/저장/로드를 지원.
    /// </summary>
    public class InputManager : MonoBehaviour
    {
        public static InputManager Instance
        {
            get
            {
                if (instance == null)
                {
                    GameObject obj = new GameObject("InputManager");
                    obj.AddComponent<InputManager>();
                }
                return instance;
            }
        }
        private static InputManager instance;

        // 이동
        public InputAction MoveAction { get; private set; }

        // 공격
        public InputAction MeleeAttackAction { get; private set; }
        public InputAction RangedAttackAction { get; private set; }

        // UI
        public InputAction Digit1Action { get; private set; }
        public InputAction Digit2Action { get; private set; }
        public InputAction Digit3Action { get; private set; }
        public InputAction Digit4Action { get; private set; }

        // 스탯창
        public InputAction StatWindowAction { get; private set; }

        // 디버그
        public InputAction DebugLevelUpAction { get; private set; }

        private const string REBIND_KEY = "InputRebinds";

        private void Awake()
        {
            if (instance != null && instance != this)
            {
                Destroy(gameObject);
                return;
            }
            instance = this;
            DontDestroyOnLoad(gameObject);

            CreateActions();
            LoadRebinds();
            EnableAll();
            Debug.Log("[InputManager] 초기화 완료");
        }

        private void CreateActions()
        {
            // 이동: 방향키
            MoveAction = new InputAction("Move", InputActionType.Value);
            MoveAction.AddCompositeBinding("2DVector")
                .With("Up", "<Keyboard>/upArrow")
                .With("Down", "<Keyboard>/downArrow")
                .With("Left", "<Keyboard>/leftArrow")
                .With("Right", "<Keyboard>/rightArrow");

            // 근거리 공격
            MeleeAttackAction = new InputAction("MeleeAttack", InputActionType.Button,
                "<Keyboard>/q");

            // 원거리 공격
            RangedAttackAction = new InputAction("RangedAttack", InputActionType.Button,
                "<Keyboard>/e");

            // UI 숫자키
            Digit1Action = new InputAction("Digit1", InputActionType.Button,
                "<Keyboard>/1");
            Digit2Action = new InputAction("Digit2", InputActionType.Button,
                "<Keyboard>/2");
            Digit3Action = new InputAction("Digit3", InputActionType.Button,
                "<Keyboard>/3");
            Digit4Action = new InputAction("Digit4", InputActionType.Button,
                "<Keyboard>/4");

            // 스탯창
            StatWindowAction = new InputAction("StatWindow", InputActionType.Button,
                "<Keyboard>/o");

            // 디버그
            DebugLevelUpAction = new InputAction("DebugLevelUp", InputActionType.Button,
                "<Keyboard>/p");
        }

        private void EnableAll()
        {
            MoveAction.Enable();
            MeleeAttackAction.Enable();
            RangedAttackAction.Enable();
            Digit1Action.Enable();
            Digit2Action.Enable();
            Digit3Action.Enable();
            Digit4Action.Enable();
            StatWindowAction.Enable();
            DebugLevelUpAction.Enable();
        }

        private void OnDisable()
        {
            MoveAction?.Disable();
            MeleeAttackAction?.Disable();
            RangedAttackAction?.Disable();
            Digit1Action?.Disable();
            Digit2Action?.Disable();
            Digit3Action?.Disable();
            Digit4Action?.Disable();
            StatWindowAction?.Disable();
            DebugLevelUpAction?.Disable();
        }

        // ─────────────────────────────────
        // 리바인딩
        // ─────────────────────────────────

        public void SaveRebinds()
        {
            string json = BuildRebindJson();
            PlayerPrefs.SetString(REBIND_KEY, json);
            PlayerPrefs.Save();
        }

        public void LoadRebinds()
        {
            string json = PlayerPrefs.GetString(REBIND_KEY, string.Empty);
            if (string.IsNullOrEmpty(json)) return;
            ApplyRebindJson(json);
        }

        public void ResetToDefaults()
        {
            RemoveAllOverrides();
            PlayerPrefs.DeleteKey(REBIND_KEY);
            PlayerPrefs.Save();
        }

        private string BuildRebindJson()
        {
            InputAction[] actions = GetAllActions();
            System.Text.StringBuilder sb = new System.Text.StringBuilder();
            sb.Append("{");
            for (int i = 0; i < actions.Length; i++)
            {
                if (i > 0) sb.Append(",");
                sb.Append($"\"{actions[i].name}\":");
                sb.Append(actions[i].SaveBindingOverridesAsJson());
            }
            sb.Append("}");
            return sb.ToString();
        }

        private void ApplyRebindJson(string json)
        {
            // 간단한 파싱: 각 액션별 오버라이드 적용
            InputAction[] actions = GetAllActions();
            foreach (var action in actions)
            {
                string key = $"\"{action.name}\":";
                int start = json.IndexOf(key);
                if (start < 0) continue;
                start += key.Length;

                // JSON 배열/문자열 끝 찾기
                int depth = 0;
                int end = start;
                bool inString = false;
                for (int i = start; i < json.Length; i++)
                {
                    char c = json[i];
                    if (c == '"' && (i == 0 || json[i - 1] != '\\')) inString = !inString;
                    if (!inString)
                    {
                        if (c == '{' || c == '[') depth++;
                        else if (c == '}' || c == ']') depth--;
                        if (depth == 0 && (c == ',' || c == '}'))
                        {
                            end = i;
                            break;
                        }
                    }
                }

                string actionJson = json.Substring(start, end - start);
                action.LoadBindingOverridesFromJson(actionJson);
            }
        }

        private void RemoveAllOverrides()
        {
            foreach (var action in GetAllActions())
                action.RemoveAllBindingOverrides();
        }

        private InputAction[] GetAllActions()
        {
            return new InputAction[]
            {
                MoveAction,
                MeleeAttackAction, RangedAttackAction,
                Digit1Action, Digit2Action, Digit3Action, Digit4Action,
                StatWindowAction, DebugLevelUpAction
            };
        }
    }
}
