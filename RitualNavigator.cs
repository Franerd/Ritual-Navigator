
using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;

public class RitualNavigator : Mod
{
    private const string Version = "0.5.3";
    private const string Ritual3UnlockHash = "-1834927741";
    private const string Ritual4UnlockHash = "1211834070";
    private const float TorchDeliveryWindowSeconds = 30f;

    private Player m_Player;
    private FieldInfo m_DreamActiveField;
    private FieldInfo m_LastPosBeforeDreamField;
    private MethodInfo m_StopDreamMethod;
    private bool m_SnapshotValid;
    private Vector3 m_SnapshotPos;
    private Quaternion m_SnapshotRot;
    private bool m_ArrivalRepositionPending;
    private float m_ArrivalRepositionDeadline;
    private Vector3 m_ArrivalTargetPos;
    private Vector3 m_ArrivalTargetForward;
    private bool m_RitualMenuOpen;
    private int m_RitualMenuSelection;
    private bool m_MaterialDropArmed;
    private float m_MaterialDropArmUntil;
    private Component m_TorchPendingVerification;
    private float m_TorchVerificationTime;
    private Component m_TorchTemporaryInfinite;
    private float m_TorchTemporaryInfiniteEndTime;
    private readonly Dictionary<string, bool> m_ScenarioBoolSnapshot = new Dictionary<string, bool>();
    private readonly HashSet<string> m_LoggedResolvedMappings = new HashSet<string>();
    private float m_NextScenarioTraceTime;
    private bool m_ScenarioTraceInitialized;
    private Player m_LastLoggedPlayer;
    private bool m_BindNullLogged;
    private bool m_CursorStateSaved;
    private CursorLockMode m_PreviousCursorLockMode;
    private bool m_PreviousCursorVisible;
    private bool m_PreviousScreenLockCursor;
    private Texture2D m_MenuPanelTexture;
    private Texture2D m_MenuHoverTexture;
    private Texture2D m_MenuAccentTexture;
    private GUIStyle m_MenuPanelStyle;
    private GUIStyle m_MenuTitleStyle;
    private GUIStyle m_MenuHintStyle;
    private GUIStyle m_MenuButtonStyle;
    private GUIStyle m_MenuButtonSelectedStyle;
    private object m_PlayerInputBlockTarget;
    private MethodInfo m_BlockPlayerInputsMethod;
    private MethodInfo m_UnblockPlayerInputsMethod;
    private bool m_PlayerInputsBlocked;
    private GameObject m_TemporaryInputBlockerObject;
    private object m_NativeCursorManager;
    private MethodInfo m_ShowCursorMethod;
    private bool m_NativeCursorRequestActive;
    private readonly string[] m_RitualMenuLabels = new string[]
    {
        "Ritual 1 Prerequisite - Burned Backpack",
        "Ritual 1 - Dream 1",
        "Ritual 2 - Dream 2",
        "Ritual 3 - Dream 3",
        "Ritual 4 - Dream 4",
        "Drop Required Materials",
        "Return to Previous Location",
        "Close"
    };
    private readonly Vector3[] m_RitualBowls = new Vector3[]
    {
        new Vector3(479.58f, 106.47f, 1406.46f),
        new Vector3(660.97f, 138.29f, 1406.14f),
        new Vector3(1398.26f, 94.50f, 1141.85f),
        new Vector3(1071.94f, 94.22f, 1062.88f)
    };

    public void Start()
    {
        Debug.Log("[SceneExplorer] LOAD_OK version=" + Version +
            " mode=release ritualMenu=true scenarioVariablesChanged=false " +
            "hotkeys=F5_menu,F7_safe_stop,Shift+F7_return F9_F10_reserved_GHMod");

        Bind();
        Debug.Log("[SceneExplorer] READY F5=ritual_menu F7=safe_stop Shift+F7=return");
    }

    public void Update()
    {
        ProcessPendingArrivalReposition();
        if (m_MaterialDropArmed && Time.unscaledTime > m_MaterialDropArmUntil) m_MaterialDropArmed = false;
        VerifySpawnedTorch();
        UpdateTorchDeliveryWindow();
        TraceScenarioProgression();

        if (m_RitualMenuOpen && (!IsGameplayAvailable() || !IsStoryMode()))
        {
            SetRitualMenuOpen(false, "gameplay_or_story_unavailable");
        }
        if (m_RitualMenuOpen) MaintainMenuCursor();

        if (Input.GetKeyDown(KeyCode.F5))
        {
            if (!IsGameplayAvailable())
            {
                SetRitualMenuOpen(false, "not_in_active_gameplay");
                Debug.Log("[SceneExplorer] RITUAL_MENU_BLOCKED source=F5 reason=not_in_active_gameplay");
                return;
            }
            string detectedMode;
            if (!IsStoryMode(out detectedMode))
            {
                SetRitualMenuOpen(false, "not_story_mode");
                Debug.Log("[SceneExplorer] RITUAL_MENU_BLOCKED source=F5 reason=story_mode_required detectedMode=\"" + detectedMode + "\"");
                return;
            }
            SetRitualMenuOpen(!m_RitualMenuOpen, "F5");
        }

        if (m_RitualMenuOpen)
        {
            List<int> visible = GetVisibleMenuOptions();
            if (m_RitualMenuSelection >= visible.Count) m_RitualMenuSelection = 0;
            if (Input.GetKeyDown(KeyCode.UpArrow)) m_RitualMenuSelection = (m_RitualMenuSelection + visible.Count - 1) % visible.Count;
            if (Input.GetKeyDown(KeyCode.DownArrow)) m_RitualMenuSelection = (m_RitualMenuSelection + 1) % visible.Count;
            if (Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter)) ExecuteRitualMenuOption(visible[m_RitualMenuSelection]);
            if (Input.GetKeyDown(KeyCode.Escape)) SetRitualMenuOpen(false, "Escape");
        }

        if (Input.GetKeyDown(KeyCode.F7) && (Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift)))
        {
            EmergencyRestore();
        }
        else if (Input.GetKeyDown(KeyCode.F7))
        {
            SafeStopDream();
        }

    }

    public void OnGUI()
    {
        if (!m_RitualMenuOpen || !IsGameplayAvailable() || !IsStoryMode()) return;
        MaintainMenuCursor();
        EnsureMenuStyles();
        float width = Mathf.Clamp(Screen.width * 0.34f, 390f, 520f);
        List<int> visible = GetVisibleMenuOptions();
        if (m_RitualMenuSelection >= visible.Count) m_RitualMenuSelection = 0;
        float itemHeight = 38f;
        float height = 118f + visible.Count * itemHeight;
        float x = Mathf.Max(28f, Screen.width * 0.075f);
        float y = (Screen.height - height) * 0.5f;
        GUI.Box(new Rect(x, y, width, height), GUIContent.none, m_MenuPanelStyle);
        GUI.DrawTexture(new Rect(x + 20f, y + 25f, 3f, height - 50f), m_MenuAccentTexture);
        GUI.Label(new Rect(x + 42f, y + 22f, width - 68f, 34f), "RITUALS", m_MenuTitleStyle);
        GUI.Label(new Rect(x + 42f, y + 58f, width - 68f, 24f), "Mouse or Arrow Keys + Enter  |  F5/Esc to close", m_MenuHintStyle);
        for (int i = 0; i < visible.Count; i++)
        {
            int option = visible[i];
            string label = (option == 5 && m_MaterialDropArmed) ? "CONFIRM: Drop Required Materials" : m_RitualMenuLabels[option];
            bool selected = i == m_RitualMenuSelection;
            string text = (selected ? ">  " : "   ") + label;
            GUIStyle style = selected ? m_MenuButtonSelectedStyle : m_MenuButtonStyle;
            Rect buttonRect = new Rect(x + 36f, y + 92f + i * itemHeight, width - 58f, 34f);
            if (buttonRect.Contains(Event.current.mousePosition))
            {
                m_RitualMenuSelection = i;
                selected = true;
                text = ">  " + label;
                style = m_MenuButtonSelectedStyle;
            }
            if (GUI.Button(buttonRect, text, style))
            {
                m_RitualMenuSelection = i;
                ExecuteRitualMenuOption(option);
            }
        }
    }

    private void EnsureMenuStyles()
    {
        if (m_MenuPanelStyle != null) return;
        m_MenuPanelTexture = CreateSolidTexture(new Color(0.035f, 0.045f, 0.038f, 0.10f));
        m_MenuHoverTexture = CreateSolidTexture(new Color(0.72f, 0.48f, 0.18f, 0.12f));
        m_MenuAccentTexture = CreateSolidTexture(new Color(0.74f, 0.54f, 0.27f, 0.22f));
        m_MenuPanelStyle = new GUIStyle(GUI.skin.box); m_MenuPanelStyle.normal.background = m_MenuPanelTexture;
        m_MenuTitleStyle = new GUIStyle(GUI.skin.label); m_MenuTitleStyle.fontSize = 25; m_MenuTitleStyle.fontStyle = FontStyle.Bold;
        m_MenuTitleStyle.normal.textColor = new Color(0.93f, 0.90f, 0.78f, 1f);
        m_MenuHintStyle = new GUIStyle(GUI.skin.label); m_MenuHintStyle.fontSize = 13;
        m_MenuHintStyle.normal.textColor = new Color(0.72f, 0.72f, 0.66f, 1f);
        m_MenuButtonStyle = new GUIStyle(GUI.skin.button); m_MenuButtonStyle.alignment = TextAnchor.MiddleLeft;
        m_MenuButtonStyle.fontSize = 18; m_MenuButtonStyle.padding = new RectOffset(14, 10, 0, 0);
        m_MenuButtonStyle.normal.background = null; m_MenuButtonStyle.normal.textColor = new Color(0.88f, 0.88f, 0.82f, 1f);
        m_MenuButtonStyle.hover.background = m_MenuHoverTexture; m_MenuButtonStyle.hover.textColor = new Color(1f, 0.78f, 0.38f, 1f);
        m_MenuButtonStyle.active.background = m_MenuHoverTexture; m_MenuButtonStyle.active.textColor = Color.white;
        m_MenuButtonSelectedStyle = new GUIStyle(m_MenuButtonStyle);
        m_MenuButtonSelectedStyle.normal.background = m_MenuHoverTexture;
        m_MenuButtonSelectedStyle.normal.textColor = new Color(1f, 0.78f, 0.38f, 1f);
    }

    private Texture2D CreateSolidTexture(Color color)
    {
        Texture2D texture = new Texture2D(1, 1, TextureFormat.ARGB32, false);
        texture.SetPixel(0, 0, color); texture.Apply(); return texture;
    }

    private void SetRitualMenuOpen(bool open, string source)
    {
        if (m_RitualMenuOpen == open) return;
        m_RitualMenuOpen = open;
        if (open)
        {
            m_RitualMenuSelection = 0;
            if (!m_CursorStateSaved)
            {
                m_PreviousCursorLockMode = Cursor.lockState;
                m_PreviousCursorVisible = Cursor.visible;
                m_PreviousScreenLockCursor = Screen.lockCursor;
                m_CursorStateSaved = true;
            }
            AcquireNativeCursorRequest();
            MaintainMenuCursor();
            BlockPlayerInputsForMenu();
        }
        else if (m_CursorStateSaved)
        {
            UnblockPlayerInputsForMenu();
            ReleaseNativeCursorRequest();
            Screen.lockCursor = m_PreviousScreenLockCursor;
            Cursor.lockState = m_PreviousCursorLockMode;
            Cursor.visible = m_PreviousCursorVisible;
            m_CursorStateSaved = false;
        }
    }

    private void ForceMenuCursor()
    {
        Screen.lockCursor = false;
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
    }

    private void MaintainMenuCursor()
    {
        if (!m_NativeCursorRequestActive) ForceMenuCursor();
    }

    private void AcquireNativeCursorRequest()
    {
        if (m_NativeCursorRequestActive) return;
        try
        {
            Type[] types = typeof(Player).Assembly.GetTypes();
            Type cursorType = null;
            for (int i = 0; i < types.Length; i++)
                if (types[i].Name == "CursorManager") { cursorType = types[i]; break; }
            if (cursorType == null) throw new Exception("CursorManager_not_found");
            MethodInfo get = cursorType.GetMethod("Get", BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic, null, Type.EmptyTypes, null);
            m_NativeCursorManager = get != null ? get.Invoke(null, null) : null;
            if (m_NativeCursorManager == null) throw new Exception("CursorManager_instance_missing");
            m_ShowCursorMethod = cursorType.GetMethod("ShowCursor", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic, null, new Type[] { typeof(bool), typeof(bool) }, null);
            if (m_ShowCursorMethod == null)
                throw new Exception("CursorManager_ShowCursor_API_incomplete");
            m_ShowCursorMethod.Invoke(m_NativeCursorManager, new object[] { true, false });
            m_NativeCursorRequestActive = true;
        }
        catch (Exception ex)
        {
            m_NativeCursorRequestActive = false;
            Debug.Log("[SceneExplorer] NATIVE_CURSOR_REQUEST_FAILED fallback=ForceMenuCursor " + ex);
        }
    }

    private void ReleaseNativeCursorRequest()
    {
        if (!m_NativeCursorRequestActive) return;
        try
        {
            m_ShowCursorMethod.Invoke(m_NativeCursorManager, new object[] { false, false });
        }
        catch (Exception ex) { Debug.Log("[SceneExplorer] NATIVE_SHOWCURSOR_RELEASE_FAILED " + ex); }
        m_NativeCursorRequestActive = false;
        m_NativeCursorManager = null;
    }

    private void BlockPlayerInputsForMenu()
    {
        if (m_PlayerInputsBlocked) return;
        try
        {
            if (m_BlockPlayerInputsMethod == null || m_UnblockPlayerInputsMethod == null)
                ResolvePlayerInputBlocker();
            if (m_BlockPlayerInputsMethod == null || m_UnblockPlayerInputsMethod == null)
            {
                Debug.Log("[SceneExplorer] PLAYER_INPUT_BLOCK_FAILED reason=native_pair_not_found");
                return;
            }
            m_BlockPlayerInputsMethod.Invoke(m_BlockPlayerInputsMethod.IsStatic ? null : m_PlayerInputBlockTarget, null);
            m_PlayerInputsBlocked = true;
        }
        catch (Exception ex) { Debug.Log("[SceneExplorer] PLAYER_INPUT_BLOCK_FAILED " + ex); }
        if (!m_PlayerInputsBlocked) DestroyTemporaryInputBlocker();
    }

    private void UnblockPlayerInputsForMenu()
    {
        if (!m_PlayerInputsBlocked) return;
        try
        {
            m_UnblockPlayerInputsMethod.Invoke(m_UnblockPlayerInputsMethod.IsStatic ? null : m_PlayerInputBlockTarget, null);
        }
        catch (Exception ex) { Debug.Log("[SceneExplorer] PLAYER_INPUT_UNBLOCK_FAILED " + ex); }
        m_PlayerInputsBlocked = false;
        DestroyTemporaryInputBlocker();
    }

    private void ResolvePlayerInputBlocker()
    {
        Type[] types = typeof(Player).Assembly.GetTypes();
        for (int i = 0; i < types.Length; i++)
        {
            MethodInfo block = types[i].GetMethod("BlockPlayerInputs", BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic, null, Type.EmptyTypes, null);
            MethodInfo unblock = types[i].GetMethod("UnblockPlayerInputs", BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic, null, Type.EmptyTypes, null);
            if (block == null || unblock == null) continue;
            object target = null;
            if (!block.IsStatic || !unblock.IsStatic)
            {
                if (m_Player != null && types[i].IsInstanceOfType(m_Player)) target = m_Player;
                if (target == null)
                {
                    MethodInfo get = types[i].GetMethod("Get", BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic, null, Type.EmptyTypes, null);
                    if (get != null) target = get.Invoke(null, null);
                }
                if (target == null && typeof(UnityEngine.Object).IsAssignableFrom(types[i]))
                {
                    UnityEngine.Object[] instances = Resources.FindObjectsOfTypeAll(types[i]);
                    if (instances.Length > 0) target = instances[0];
                }
                if (target == null && types[i].FullName == "TribeDialogTrigger" && typeof(Component).IsAssignableFrom(types[i]))
                {
                    m_TemporaryInputBlockerObject = new GameObject("SceneExplorer_TemporaryInputBlocker");
                    m_TemporaryInputBlockerObject.hideFlags = HideFlags.HideAndDontSave;
                    m_TemporaryInputBlockerObject.SetActive(false);
                    target = m_TemporaryInputBlockerObject.AddComponent(types[i]);
                }
                if (target == null) continue;
            }
            m_BlockPlayerInputsMethod = block;
            m_UnblockPlayerInputsMethod = unblock;
            m_PlayerInputBlockTarget = target;
            return;
        }
    }

    private void DestroyTemporaryInputBlocker()
    {
        if (m_TemporaryInputBlockerObject == null) return;
        Destroy(m_TemporaryInputBlockerObject);
        m_TemporaryInputBlockerObject = null;
        m_PlayerInputBlockTarget = null;
        m_BlockPlayerInputsMethod = null;
        m_UnblockPlayerInputsMethod = null;
    }

    public void OnDestroy()
    {
        UnblockPlayerInputsForMenu();
        ReleaseNativeCursorRequest();
        DestroyTemporaryInputBlocker();
        if (!m_CursorStateSaved) return;
        Screen.lockCursor = m_PreviousScreenLockCursor;
        Cursor.lockState = m_PreviousCursorLockMode;
        Cursor.visible = m_PreviousCursorVisible;
        m_CursorStateSaved = false;
    }

    private List<int> GetVisibleMenuOptions()
    {
        List<int> visible = new List<int>();
        visible.Add(0);
        visible.Add(1);
        visible.Add(2);
        if (IsProgressionHashTrue(Ritual3UnlockHash)) visible.Add(3);
        if (IsProgressionHashTrue(Ritual4UnlockHash)) visible.Add(4);
        visible.Add(5);
        visible.Add(6);
        visible.Add(7);
        return visible;
    }

    private bool IsProgressionHashTrue(string hash)
    {
        bool value;
        return m_ScenarioBoolSnapshot.TryGetValue(hash, out value) && value;
    }

    private bool IsGameplayAvailable()
    {
        Bind();
        if (m_Player == null || m_Player.gameObject == null || !m_Player.gameObject.activeInHierarchy) return false;
        if (!m_Player.gameObject.scene.IsValid() || !m_Player.gameObject.scene.isLoaded) return false;
        if (Time.timeScale <= 0.001f) return false;
        try
        {
            Type menuType = typeof(Player).Assembly.GetType("MenuInGameManager");
            MethodInfo get = menuType != null ? menuType.GetMethod("Get", BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic, null, Type.EmptyTypes, null) : null;
            object menu = get != null ? get.Invoke(null, null) : null;
            if (menu != null)
            {
                string[] checks = new string[] { "IsPause", "IsPaused" };
                for (int i = 0; i < checks.Length; i++)
                {
                    MethodInfo method = menuType.GetMethod(checks[i], BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic, null, Type.EmptyTypes, null);
                    if (method != null && method.ReturnType == typeof(bool) && (bool)method.Invoke(menu, null)) return false;
                }
            }
        }
        catch (Exception ex) { Debug.Log("[SceneExplorer] GAMEPLAY_GUARD_WARNING " + ex.GetType().Name + ": " + ex.Message); }
        return true;
    }

    private bool IsStoryMode()
    {
        string detected; return IsStoryMode(out detected);
    }

    private bool IsStoryMode(out string detectedMode)
    {
        detectedMode = "<unresolved>";
        try
        {
            Type gameType = typeof(Player).Assembly.GetType("GreenHellGame");
            if (gameType == null) return false;
            PropertyInfo instanceProperty = gameType.GetProperty("Instance", BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
            MethodInfo getInstance = gameType.GetMethod("get_Instance", BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic, null, Type.EmptyTypes, null);
            object game = instanceProperty != null ? instanceProperty.GetValue(null, null) : (getInstance != null ? getInstance.Invoke(null, null) : null);
            if (game == null) return false;
            PropertyInfo modeProperty = gameType.GetProperty("m_GHGameMode", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            object mode = modeProperty != null ? modeProperty.GetValue(game, null) : null;
            if (mode == null)
            {
                FieldInfo modeField = FindField(gameType, "m_GameMode");
                if (modeField != null) mode = modeField.GetValue(game);
            }
            if (mode == null) return false;
            detectedMode = mode.ToString();
            return detectedMode.IndexOf("Story", StringComparison.OrdinalIgnoreCase) >= 0;
        }
        catch (Exception ex)
        {
            detectedMode = "<error:" + ex.GetType().Name + ">";
            Debug.Log("[SceneExplorer] STORY_MODE_CHECK_FAILED " + ex);
            return false;
        }
    }

    private void TraceScenarioProgression()
    {
        if (!IsGameplayAvailable() || Time.unscaledTime < m_NextScenarioTraceTime) return;
        m_NextScenarioTraceTime = Time.unscaledTime + 1f;
        try
        {
            Dictionary<string, bool> current = ReadScenarioBoolVariables();
            if (current.Count == 0) return;
            if (!m_ScenarioTraceInitialized)
            {
                m_ScenarioTraceInitialized = true;
                m_ScenarioBoolSnapshot.Clear();
                foreach (KeyValuePair<string, bool> pair in current) m_ScenarioBoolSnapshot[pair.Key] = pair.Value;
                Debug.Log("[SceneExplorer] RITUAL_PROGRESS_READY ritual3=" + IsProgressionHashTrue(Ritual3UnlockHash)+
                    " ritual4=" + IsProgressionHashTrue(Ritual4UnlockHash));
                return;
            }
            foreach (KeyValuePair<string, bool> pair in current)
            {
                bool before;
                bool changed = !m_ScenarioBoolSnapshot.TryGetValue(pair.Key, out before) || before != pair.Value;
                if (changed && (pair.Key == Ritual3UnlockHash || pair.Key == Ritual4UnlockHash))
                {
                    Debug.Log("[SceneExplorer] RITUAL_UNLOCK_CHANGED ritual=" + (pair.Key == Ritual3UnlockHash ? "3" : "4")+
                        " before=" + (m_ScenarioBoolSnapshot.ContainsKey(pair.Key) ? before.ToString() : "<missing>")+
                        " after=" + pair.Value);
                }
                m_ScenarioBoolSnapshot[pair.Key] = pair.Value;
            }
        }
        catch (Exception ex) { Debug.Log("[SceneExplorer] PROGRESSION_TRACE_FAILED " + ex); }
    }

    private Dictionary<string, bool> ReadScenarioBoolVariables()
    {
        Dictionary<string, bool> result = new Dictionary<string, bool>();
        Type managerType = typeof(Player).Assembly.GetType("ScenarioManager");
        MethodInfo get = managerType != null ? managerType.GetMethod("Get", BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic, null, Type.EmptyTypes, null) : null;
        object manager = get != null ? get.Invoke(null, null) : null;
        if (manager == null) return result;
        ExtractBoolVariables(manager, "manager", result, manager, null);
        FieldInfo scenarioField = FindField(manager.GetType(), "m_Scenario");
        object scenario = scenarioField != null ? scenarioField.GetValue(manager) : null;
        if (scenario != null) ExtractBoolVariables(scenario, "scenario", result, manager, scenario);
        return result;
    }

    private void ExtractBoolVariables(object owner, string source, Dictionary<string, bool> output, object manager, object scenario)
    {
        string[] collectionMethods = new string[] { "GetBoolVariables", "GetLocalBoolVariables" };
        for (int i = 0; i < collectionMethods.Length; i++)
        {
            MethodInfo method = owner.GetType().GetMethod(collectionMethods[i], BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic, null, Type.EmptyTypes, null);
            if (method == null) continue;
            object collection = method.Invoke(owner, null);
            ExtractBoolCollection(collection, source + "." + collectionMethods[i], output, manager, scenario);
        }
        MethodInfo countMethod = owner.GetType().GetMethod("GetVariablesCount", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic, null, Type.EmptyTypes, null);
        MethodInfo variableMethod = owner.GetType().GetMethod("GetVariable", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic, null, new Type[] { typeof(int) }, null);
        if (countMethod == null || variableMethod == null) return;
        int count = Convert.ToInt32(countMethod.Invoke(owner, null));
        for (int i = 0; i < count; i++) ExtractBoolEntry(variableMethod.Invoke(owner, new object[] { i }), source + "[" + i + "]", output, manager, scenario);
    }

    private void ExtractBoolCollection(object collection, string source, Dictionary<string, bool> output, object manager, object scenario)
    {
        System.Collections.IEnumerable enumerable = collection as System.Collections.IEnumerable;
        if (enumerable == null) return;
        foreach (object entry in enumerable) ExtractBoolEntry(entry, source, output, manager, scenario);
    }

    private void ExtractBoolEntry(object entry, string source, Dictionary<string, bool> output, object manager, object scenario)
    {
        if (entry == null) return;
        object key = ReadMember(entry, "Key");
        object value = ReadMember(entry, "Value");
        if (key != null && value is bool)
        {
            string resolved = ResolveScenarioVariableName(key, manager, scenario);
            output[resolved != null ? resolved : key.ToString()] = (bool)value;
            return;
        }
        object candidate = value != null ? value : entry;
        object name = InvokeNoArg(candidate, "GetName");
        if (name == null) name = ReadMember(candidate, "Name");
        object boolValue = ReadMember(candidate, "BValue");
        if (!(boolValue is bool)) boolValue = InvokeNoArg(candidate, "get_BValue");
        if (name != null && boolValue is bool) output[name.ToString()] = (bool)boolValue;
    }

    private string ResolveScenarioVariableName(object key, object manager, object scenario)
    {
        object[] owners = new object[] { scenario, manager };
        for (int o = 0; o < owners.Length; o++)
        {
            if (owners[o] == null) continue;
            MethodInfo[] methods = owners[o].GetType().GetMethods(BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
            for (int i = 0; i < methods.Length; i++)
            {
                if (methods[i].Name != "GetVariableName") continue;
                ParameterInfo[] parameters = methods[i].GetParameters();
                if (parameters.Length != 1) continue;
                try
                {
                    object converted = Convert.ChangeType(key, parameters[0].ParameterType);
                    object name = methods[i].Invoke(methods[i].IsStatic ? null : owners[o], new object[] { converted });
                    if (name != null && !String.IsNullOrEmpty(name.ToString()))
                    {
                        string resolved = name.ToString();
                        string mapping = key.ToString() + "=" + resolved;
                        if (m_LoggedResolvedMappings.Add(mapping))
                            Debug.Log("[SceneExplorer] PROGRESSION_NAME_RESOLVED hash=\"" + key + "\" name=\"" + resolved + "\"");
                        return resolved;
                    }
                }
                catch { }
            }
        }
        return null;
    }

    private object ReadMember(object owner, string name)
    {
        if (owner == null) return null;
        PropertyInfo property = owner.GetType().GetProperty(name, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
        if (property != null && property.GetIndexParameters().Length == 0) return property.GetValue(owner, null);
        FieldInfo field = FindField(owner.GetType(), name);
        return field != null ? field.GetValue(owner) : null;
    }

    private object InvokeNoArg(object owner, string name)
    {
        if (owner == null) return null;
        MethodInfo method = owner.GetType().GetMethod(name, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic, null, Type.EmptyTypes, null);
        return method != null ? method.Invoke(owner, null) : null;
    }

    private void ExecuteRitualMenuOption(int option)
    {
        if (option == 0)
        {
            TeleportToRitualOnePrerequisite();
            SetRitualMenuOpen(false, "prerequisite_action");
        }
        else if (option >= 1 && option <= 4)
        {
            TeleportToRitual(option - 1);
            SetRitualMenuOpen(false, "ritual_action");
        }
        else if (option == 5)
        {
            if (!m_MaterialDropArmed || Time.unscaledTime > m_MaterialDropArmUntil)
            {
                m_MaterialDropArmed = true;
                m_MaterialDropArmUntil = Time.unscaledTime + 5f;
                Debug.Log("[SceneExplorer] MATERIAL_DROP_ARMED confirmWithinSeconds=5 items=17");
                return;
            }
            m_MaterialDropArmed = false;
            DropRitualMaterials();
            SetRitualMenuOpen(false, "materials_action");
        }
        else if (option == 6)
        {
            EmergencyRestore();
            SetRitualMenuOpen(false, "return_action");
        }
        else
        {
            SetRitualMenuOpen(false, "menu_close_action");
        }
    }

    private void TeleportToRitualOnePrerequisite()
    {
        Bind();
        if (m_Player == null) { Debug.Log("[SceneExplorer] RITUAL1_PREREQUISITE_TELEPORT_BLOCKED reason=player_missing"); return; }
        if (IsDreamActive()) { Debug.Log("[SceneExplorer] RITUAL1_PREREQUISITE_TELEPORT_BLOCKED reason=dream_active"); return; }
        try
        {
            if (!m_SnapshotValid)
            {
                m_SnapshotPos = m_Player.transform.position;
                m_SnapshotRot = m_Player.transform.rotation;
                m_SnapshotValid = true;
            }
            Vector3 prerequisite = new Vector3(465.40f, 106.69f, 1405.06f);
            Vector3 target = prerequisite + new Vector3(-1.20f, 0f, 0.80f);
            Vector3 look = prerequisite - target; look.y = 0f; if (look.sqrMagnitude > 0.001f) look.Normalize();
            MethodInfo reposition = FindRepositionMethod(m_Player.GetType());
            if (reposition == null) { Debug.Log("[SceneExplorer] RITUAL1_PREREQUISITE_TELEPORT_BLOCKED reason=Reposition_missing"); return; }
            ParameterInfo[] parameters = reposition.GetParameters();
            Debug.Log("[SceneExplorer] RITUAL1_PREREQUISITE_TELEPORT_BEGIN snapshotPos=" + m_SnapshotPos+
                " candidate=QuestItem_Map_A prerequisite=" + prerequisite + " target=" + target+
                " recipeGranted=false mapGranted=false scenarioVariablesChanged=false");
            if (parameters.Length == 2) reposition.Invoke(m_Player, new object[] { target, look });
            else reposition.Invoke(m_Player, new object[] { target });
            Debug.Log("[SceneExplorer] RITUAL1_PREREQUISITE_TELEPORT_APPLIED currentPos=" + m_Player.transform.position+
                " expected=burned_backpack_document return=menu_or_Shift+F7");
        }
        catch (Exception ex) { Debug.Log("[SceneExplorer] RITUAL1_PREREQUISITE_TELEPORT_FAILED " + ex); }
    }

    private void DropRitualMaterials()
    {
        Bind();
        if (m_Player == null) { Debug.Log("[SceneExplorer] MATERIAL_DROP_BLOCKED reason=player_missing"); return; }
        if (IsDreamActive()) { Debug.Log("[SceneExplorer] MATERIAL_DROP_BLOCKED reason=dream_active"); return; }
        try
        {
            Type itemIdType = typeof(Player).Assembly.GetType("Enums.ItemID");
            Type managerType = typeof(Player).Assembly.GetType("ItemsManager");
            if (itemIdType == null || managerType == null) { Debug.Log("[SceneExplorer] MATERIAL_DROP_BLOCKED reason=item_or_manager_type_missing"); return; }

            object manager = null;
            UnityEngine.Object[] managers = Resources.FindObjectsOfTypeAll(managerType);
            for (int i = 0; i < managers.Length; i++)
            {
                Component component = managers[i] as Component;
                if (component == null) continue;
                manager = component;
                if (component.gameObject.activeInHierarchy) break;
            }
            MethodInfo create = managerType.GetMethod("CreateItem", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic, null,
                new Type[] { itemIdType, typeof(bool), typeof(Vector3), typeof(Quaternion), typeof(bool) }, null);
            if (manager == null || create == null) { Debug.Log("[SceneExplorer] MATERIAL_DROP_BLOCKED reason=manager_instance_or_CreateItem_missing"); return; }

            int[] ids = new int[] { 708, 709, 296, 9, 9, 9, 9, 9, 9, 9, 9, 8, 8, 8, 8, 8, 8 };
            string[] names = new string[] { "banisteriopsis_scraps", "psychotria_viridis", "Torch", "Stick", "Stick", "Stick", "Stick", "Stick", "Stick", "Stick", "Stick", "Small_Stick", "Small_Stick", "Small_Stick", "Small_Stick", "Small_Stick", "Small_Stick" };
            Vector3 forward = m_Player.transform.forward; forward.y = 0f; if (forward.sqrMagnitude < 0.001f) forward = Vector3.forward; forward.Normalize();
            Vector3 right = new Vector3(forward.z, 0f, -forward.x);
            Vector3 origin = m_Player.transform.position + right * 1.35f - forward * 0.55f + Vector3.up * 0.65f;
            int created = 0;
            Debug.Log("[SceneExplorer] MATERIAL_DROP_BEGIN total=" + ids.Length + " origin=" + origin + " layout=4_columns replicated=false inInventory=false");
            for (int i = 0; i < ids.Length; i++)
            {
                int column = i % 4;
                int row = i / 4;
                Vector3 pos = origin + right * (column * 0.38f) + forward * (row * 0.42f);
                object itemId = Enum.ToObject(itemIdType, ids[i]);
                object item = create.Invoke(manager, new object[] { itemId, false, pos, Quaternion.identity, false });
                if (item != null)
                {
                    created++;
                    if (ids[i] == 296) IgniteSpawnedTorch(item);
                }
                else Debug.Log("[SceneExplorer] MATERIAL_DROP_ITEM_FAILED index=" + i + " id=" + ids[i] + " name=\"" + names[i] + "\" pos=" + pos);
            }
            Debug.Log("[SceneExplorer] MATERIAL_DROP_END requested=" + ids.Length + " created=" + created +
                " contents=banisteriopsis:1,psychotria:1,torch:1,stick:8,small_stick:6");
        }
        catch (Exception ex) { Debug.Log("[SceneExplorer] MATERIAL_DROP_FAILED " + ex); }
    }

    private void IgniteSpawnedTorch(object item)
    {
        try
        {
            Component torch = item as Component;
            if (torch == null) { Debug.Log("[SceneExplorer] TORCH_IGNITE_FAILED reason=item_not_component"); return; }
            Type type = torch.GetType();
            MethodInfo ignite = type.GetMethod("Ignite", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic, null, Type.EmptyTypes, null);
            MethodInfo isBurning = type.GetMethod("IsBurning", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic, null, Type.EmptyTypes, null);
            if (ignite == null || isBurning == null) { Debug.Log("[SceneExplorer] TORCH_IGNITE_FAILED reason=Ignite_or_IsBurning_missing type=\"" + type.FullName + "\""); return; }
            FieldInfo infiniteBurn = FindField(type, "m_DebugInfiniteBurn");
            if (infiniteBurn == null || infiniteBurn.FieldType != typeof(bool))
            {
                Debug.Log("[SceneExplorer] TORCH_IGNITE_FAILED reason=m_DebugInfiniteBurn_missing_or_wrong_type type=\"" + type.FullName + "\"");
                return;
            }
            infiniteBurn.SetValue(torch, true);
            object before = isBurning.Invoke(torch, null);
            ignite.Invoke(torch, null);
            object after = isBurning.Invoke(torch, null);
            m_TorchPendingVerification = torch;
            m_TorchVerificationTime = Time.unscaledTime + 2f;
            m_TorchTemporaryInfinite = torch;
            m_TorchTemporaryInfiniteEndTime = Time.unscaledTime + TorchDeliveryWindowSeconds;
            Debug.Log("[SceneExplorer] TORCH_IGNITE_INVOKED type=\"" + type.FullName + "\" before=" + before +
                " after=" + after + " temporaryInfiniteBurn=" + infiniteBurn.GetValue(torch) +
                " deliveryWindowSeconds=" + TorchDeliveryWindowSeconds + " delayedVerificationSeconds=2");
        }
        catch (Exception ex) { Debug.Log("[SceneExplorer] TORCH_IGNITE_FAILED " + ex); }
    }

    private void VerifySpawnedTorch()
    {
        if (m_TorchPendingVerification == null || Time.unscaledTime < m_TorchVerificationTime) return;
        Component torch = m_TorchPendingVerification;
        m_TorchPendingVerification = null;
        try
        {
            Type type = torch.GetType();
            MethodInfo isBurning = type.GetMethod("IsBurning", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic, null, Type.EmptyTypes, null);
            FieldInfo duration = FindField(type, "m_BurningDuration");
            FieldInfo burningTime = FindField(type, "m_BurningTime");
            FieldInfo infiniteBurn = FindField(type, "m_DebugInfiniteBurn");
            object state = isBurning != null ? isBurning.Invoke(torch, null) : "<missing>";
            object durationValue = duration != null ? duration.GetValue(torch) : "<missing>";
            object timeValue = burningTime != null ? burningTime.GetValue(torch) : "<missing>";
            object infiniteValue = infiniteBurn != null ? infiniteBurn.GetValue(torch) : "<missing>";
            Debug.Log("[SceneExplorer] TORCH_IGNITE_VERIFIED isBurning=" + state + " burningDuration=" + durationValue+
                " burningTime=" + timeValue + " infiniteBurn=" + infiniteValue +
                " active=" + torch.gameObject.activeInHierarchy);
        }
        catch (Exception ex) { Debug.Log("[SceneExplorer] TORCH_IGNITE_VERIFICATION_FAILED " + ex); }
    }

    private void UpdateTorchDeliveryWindow()
    {
        if (m_TorchTemporaryInfinite == null || Time.unscaledTime < m_TorchTemporaryInfiniteEndTime) return;
        Component torch = m_TorchTemporaryInfinite;
        m_TorchTemporaryInfinite = null;
        try
        {
            Type type = torch.GetType();
            FieldInfo infiniteBurn = FindField(type, "m_DebugInfiniteBurn");
            MethodInfo isBurning = type.GetMethod("IsBurning", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic, null, Type.EmptyTypes, null);
            if (infiniteBurn == null || infiniteBurn.FieldType != typeof(bool))
            {
                Debug.Log("[SceneExplorer] TORCH_DELIVERY_WINDOW_END_FAILED reason=m_DebugInfiniteBurn_missing");
                return;
            }
            infiniteBurn.SetValue(torch, false);
            object burning = isBurning != null ? isBurning.Invoke(torch, null) : "<missing>";
            Debug.Log("[SceneExplorer] TORCH_DELIVERY_WINDOW_END infiniteBurn=" + infiniteBurn.GetValue(torch) +
                " isBurning=" + burning + " active=" + torch.gameObject.activeInHierarchy);
        }
        catch (Exception ex) { Debug.Log("[SceneExplorer] TORCH_DELIVERY_WINDOW_END_FAILED " + ex); }
    }

    private void TeleportToRitual(int ritualIndex)
    {
        Bind();
        if (m_Player == null) { Debug.Log("[SceneExplorer] RITUAL_TELEPORT_BLOCKED reason=player_missing"); return; }
        if (IsDreamActive()) { Debug.Log("[SceneExplorer] RITUAL_TELEPORT_BLOCKED reason=dream_active"); return; }
        if (ritualIndex < 0 || ritualIndex >= m_RitualBowls.Length) return;
        try
        {
            if (!m_SnapshotValid)
            {
                m_SnapshotPos = m_Player.transform.position;
                m_SnapshotRot = m_Player.transform.rotation;
                m_SnapshotValid = true;
            }
            Vector3 bowl = m_RitualBowls[ritualIndex];
            Vector3 target = bowl + new Vector3(-1.20f, 0f, 0.80f);
            Vector3 look = bowl - target;
            look.y = 0f;
            if (look.sqrMagnitude > 0.001f) look.Normalize();
            MethodInfo reposition = FindRepositionMethod(m_Player.GetType());
            if (reposition == null) { Debug.Log("[SceneExplorer] RITUAL_TELEPORT_BLOCKED reason=Reposition_missing"); return; }
            ParameterInfo[] parameters = reposition.GetParameters();
            Debug.Log("[SceneExplorer] RITUAL_TELEPORT_BEGIN ritual=" + (ritualIndex + 1) + " snapshotPos=" + m_SnapshotPos +
                " bowl=" + bowl + " target=" + target + " itemsGranted=false scenarioVariablesChanged=false");
            if (parameters.Length == 2) reposition.Invoke(m_Player, new object[] { target, look });
            else reposition.Invoke(m_Player, new object[] { target });
            Debug.Log("[SceneExplorer] RITUAL_TELEPORT_APPLIED ritual=" + (ritualIndex + 1) +
                " currentPos=" + m_Player.transform.position + " return=menu_or_Shift+F7");
        }
        catch (Exception ex) { Debug.Log("[SceneExplorer] RITUAL_TELEPORT_FAILED ritual=" + (ritualIndex + 1) + " " + ex); }
    }

    private void ProcessPendingArrivalReposition()
    {
        if (!m_ArrivalRepositionPending) return;
        if (m_Player == null) Bind();
        if (m_Player == null) return;

        if (!IsDreamActive())
        {
            if (Time.unscaledTime > m_ArrivalRepositionDeadline)
            {
                m_ArrivalRepositionPending = false;
                Debug.Log("[SceneExplorer] ARRIVAL_REPOSITION_FAILED reason=dream_activation_timeout currentPos=" + m_Player.transform.position);
            }
            return;
        }

        m_ArrivalRepositionPending = false;
        try
        {
            object savedReturnPos = m_LastPosBeforeDreamField != null ? m_LastPosBeforeDreamField.GetValue(m_Player) : "<missing>";
            MethodInfo reposition = FindRepositionMethod(m_Player.GetType());
            if (reposition == null)
            {
                Debug.Log("[SceneExplorer] ARRIVAL_REPOSITION_FAILED reason=compatible_method_missing savedReturnPos=" + savedReturnPos + " F7_available=true");
                return;
            }

            ParameterInfo[] repositionParameters = reposition.GetParameters();
            Debug.Log("[SceneExplorer] ARRIVAL_REPOSITION_BEGIN trigger=dreamActive savedReturnPos=" + savedReturnPos +
                " targetPos=" + m_ArrivalTargetPos + " targetForward=" + m_ArrivalTargetForward +
                " signature=\"" + FormatMethodSignature(reposition) + "\"");
            if (repositionParameters.Length == 2)
                reposition.Invoke(m_Player, new object[] { m_ArrivalTargetPos, m_ArrivalTargetForward });
            else
                reposition.Invoke(m_Player, new object[] { m_ArrivalTargetPos });

            Debug.Log("[SceneExplorer] ARRIVAL_REPOSITION_APPLIED currentPos=" + m_Player.transform.position +
                " currentForward=" + m_Player.transform.forward + " dreamActive=" + IsDreamActive() +
                " savedReturnPos=" + savedReturnPos);
            DumpState("after_delayed_arrival_reposition");
        }
        catch (Exception ex)
        {
            Debug.Log("[SceneExplorer] ARRIVAL_REPOSITION_FAILED exception=" + ex);
        }
    }

    private MethodInfo FindRepositionMethod(Type playerType)
    {
        MethodInfo[] methods = playerType.GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
        for (int i = 0; i < methods.Length; i++)
        {
            if (methods[i].Name != "Reposition") continue;
            ParameterInfo[] parameters = methods[i].GetParameters();
            if ((parameters.Length == 1 || parameters.Length == 2) && parameters[0].ParameterType == typeof(Vector3))
                return methods[i];
        }
        return null;
    }

    private string FormatMethodSignature(MethodInfo method)
    {
        if (method == null) return "<null>";
        ParameterInfo[] parameters = method.GetParameters();
        List<string> parts = new List<string>();
        for (int i = 0; i < parameters.Length; i++) parts.Add(parameters[i].ParameterType.FullName);
        return method.Name + "(" + string.Join(",", parts.ToArray()) + ")";
    }

    private void EmergencyRestore()
    {
        Bind();
        if (!m_SnapshotValid || m_Player == null) { Debug.Log("[SceneExplorer] EMERGENCY_RESTORE_BLOCKED reason=snapshot_or_player_missing"); return; }
        if (IsDreamActive()) { Debug.Log("[SceneExplorer] EMERGENCY_RESTORE_BLOCKED reason=dream_active_use_F7_first"); return; }
        try
        {
            MethodInfo reposition = FindRepositionMethod(m_Player.GetType());
            if (reposition == null) { Debug.Log("[SceneExplorer] EMERGENCY_RESTORE_BLOCKED reason=Reposition_missing"); return; }
            ParameterInfo[] parameters = reposition.GetParameters();
            if (parameters.Length == 2) reposition.Invoke(m_Player, new object[] { m_SnapshotPos, m_SnapshotRot * Vector3.forward });
            else reposition.Invoke(m_Player, new object[] { m_SnapshotPos });
            Debug.Log("[SceneExplorer] EMERGENCY_RESTORE_APPLIED pos=" + m_SnapshotPos + " rot=" + m_SnapshotRot);
            m_SnapshotValid = false;
        }
        catch (Exception ex) { Debug.Log("[SceneExplorer] EMERGENCY_RESTORE_FAILED " + ex); }
    }

    private void Bind()
    {
        try
        {
            Player resolved = Player.Get();
            if (resolved == m_Player && resolved != null && m_DreamActiveField != null) return;
            m_Player = resolved;
            if (m_Player == null)
            {
                if (!m_BindNullLogged) Debug.Log("[SceneExplorer] BIND player=null");
                m_BindNullLogged = true;
                m_LastLoggedPlayer = null;
                return;
            }

            m_BindNullLogged = false;

            Type t = m_Player.GetType();
            BindingFlags flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;

            m_DreamActiveField = t.GetField("m_DreamActive", flags);
            m_LastPosBeforeDreamField = t.GetField("m_LastPosBeforeDream", flags);
            m_StopDreamMethod = t.GetMethod("StopDream", flags, null, Type.EmptyTypes, null);

            if (m_LastLoggedPlayer != m_Player)
            {
                Debug.Log("[SceneExplorer] BIND player=true dreamActiveField=" +
                    (m_DreamActiveField != null) +
                    " lastPosField=" + (m_LastPosBeforeDreamField != null) +
                    " stopDream=" + (m_StopDreamMethod != null));
                m_LastLoggedPlayer = m_Player;
            }
        }
        catch (Exception ex)
        {
            Debug.Log("[SceneExplorer] BIND_FAILED " + ex);
        }
    }

    private bool IsDreamActive()
    {
        try
        {
            if (m_Player == null || m_DreamActiveField == null)
                return false;

            object v = m_DreamActiveField.GetValue(m_Player);
            return v is bool && (bool)v;
        }
        catch
        {
            return false;
        }
    }

    private void SafeStopDream()
    {
        Bind();

        bool active = IsDreamActive();
        Vector3 current = (m_Player != null) ? m_Player.transform.position : Vector3.zero;
        object lastPos = "<unavailable>";

        try
        {
            if (m_Player != null && m_LastPosBeforeDreamField != null)
                lastPos = m_LastPosBeforeDreamField.GetValue(m_Player);
        }
        catch { }

        Debug.Log("[SceneExplorer] SAFE_STOP_REQUEST source=F7 dreamActive=" + active +
            " currentPos=" + current + " lastPos=" + lastPos);

        if (!active)
        {
            Debug.Log("[SceneExplorer] SAFE_STOP_BLOCKED reason=dream_not_active stopDream_not_called");
            return;
        }

        if (m_StopDreamMethod == null || m_Player == null)
        {
            Debug.Log("[SceneExplorer] SAFE_STOP_BLOCKED reason=stop_method_or_player_missing");
            return;
        }

        try
        {
            m_StopDreamMethod.Invoke(m_Player, null);
            Debug.Log("[SceneExplorer] SAFE_STOP_INVOKED");
            DumpState("after_safe_stop");
        }
        catch (Exception ex)
        {
            Debug.Log("[SceneExplorer] SAFE_STOP_FAILED " + ex);
        }
    }

    private void DumpState(string tag)
    {
        try
        {
            if (m_Player == null)
                Bind();

            if (m_Player == null)
            {
                Debug.Log("[SceneExplorer] STATE tag=" + tag + " player=null");
                return;
            }

            object dreamActive = (m_DreamActiveField != null) ? m_DreamActiveField.GetValue(m_Player) : "<missing>";
            object lastPos = (m_LastPosBeforeDreamField != null) ? m_LastPosBeforeDreamField.GetValue(m_Player) : "<missing>";

            Debug.Log("[SceneExplorer] STATE tag=" + tag +
                " pos=" + m_Player.transform.position +
                " rot=" + m_Player.transform.rotation +
                " dreamActive=" + dreamActive +
                " lastPos=" + lastPos);
        }
        catch (Exception ex)
        {
            Debug.Log("[SceneExplorer] STATE_FAILED tag=" + tag + " " + ex);
        }
    }

    private FieldInfo FindField(Type type, string name)
    {
        Type current = type;
        while (current != null)
        {
            FieldInfo field = current.GetField(name, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly);
            if (field != null) return field;
            current = current.BaseType;
        }
        return null;
    }
}
