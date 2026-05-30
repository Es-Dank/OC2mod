using System;
using System.Collections.Generic;
using System.Reflection;
using BepInEx;
using BepInEx.Configuration;
using HarmonyLib;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace OC2BeachPredictorV3Force
{
    [BepInPlugin("dev.chatgpt.overcooked2.beachpredictor.v3force", "海滩菜单预测助手", "3.3.0")]
    [BepInProcess("Overcooked2.exe")]
    public class BeachPredictorV3ForcePlugin : BaseUnityPlugin
    {
        private static BeachPredictorV3ForcePlugin Instance;
        private static Harmony _harmony;
        private static object _lastServerController;
        private static float _nextRefreshTime;
        private static string _status = "Waiting for beach level...";
        private static string _predictionText = "";
        private static List<PredictedRecipe> _predictions = new List<PredictedRecipe>();
        private static List<PredictedRecipe> _forcedQueue = new List<PredictedRecipe>();
        private static int _forcedIndex = -1;
        private static bool _forcingActive = false;
        private static int _queueExpectedRecipeCount = -1;
        private static int _queuePhase = -1;
        private static int _ordersForced = 0;


        private static ConfigEntry<bool> cfgEnabled;
        private static ConfigEntry<KeyCode> cfgToggleKey;
        private static ConfigEntry<KeyCode> cfgRefreshKey;
        private static ConfigEntry<int> cfgPreviewCount;
        private static ConfigEntry<float> cfgRefreshInterval;
        private static ConfigEntry<float> cfgWindowX;
        private static ConfigEntry<float> cfgWindowY;
        private static ConfigEntry<bool> cfgRespectControlRecipeMod;
        private static ConfigEntry<KeyCode> cfgClearKey;
        private static ConfigEntry<bool> cfgAutoRefreshWhenEmpty;
        private static ConfigEntry<bool> cfgShowDebug;
        private static ConfigEntry<bool> cfgKeepQueueFull;

        private static GUIStyle _boxStyle;
        private static GUIStyle _labelStyle;
        private static GUIStyle _numberStyle;
        private static GUIStyle _firstLabelStyle;
        private static Texture2D _panelTex;
        private static Texture2D _rowTex;
        private static Texture2D _firstRowTex;
        private static Texture2D _dividerTex;
        private static Rect _windowRect = new Rect(30f, 120f, 360f, 260f);
        private static bool _show = true;
        private static bool _dragging;
        private static Vector2 _dragStart;

        private static readonly string[] BeachRecipeNames = new string[]
        {
            "鸡番", "鸡番肉", "菠萝蘑菇肉", "菠萝蘑菇番", "草莓", "西瓜", "香蕉"
        };

        private void Awake()
        {
            Instance = this;
            cfgEnabled = Config.Bind("海滩菜单预测助手", "启用", true, "Enable forced predictor.");
            cfgToggleKey = Config.Bind("海滩菜单预测助手", "显示隐藏按键", KeyCode.Alpha2, "Toggle predictor panel.");
            cfgRefreshKey = Config.Bind("海滩菜单预测助手", "重新规划按键", KeyCode.Alpha3, "Rebuild and lock the forced queue.");
            cfgClearKey = Config.Bind("海滩菜单预测助手", "清空队列按键", KeyCode.Alpha4, "Clear the current forced queue.");
            cfgPreviewCount = Config.Bind("海滩菜单预测助手", "强制规划未来几单", 6, "How many future orders to plan and force.");
            cfgRefreshInterval = Config.Bind("海滩菜单预测助手", "空队列自动规划间隔秒", 0.50f, "When the forced queue is empty, try to build a new queue at this interval.");
            cfgWindowX = Config.Bind("海滩菜单预测助手", "窗口X", 30f, "Panel X.");
            cfgWindowY = Config.Bind("海滩菜单预测助手", "窗口Y", 120f, "Panel Y.");
            cfgRespectControlRecipeMod = Config.Bind("海滩菜单预测助手", "兼容原麻海小工具规则", true, "Read OC2Controlrecipe static config/state if that mod is loaded.");
            cfgAutoRefreshWhenEmpty = Config.Bind("海滩菜单预测助手", "空队列自动规划", true, "Only auto-plan when queue is empty. It will not keep changing a non-empty queue.");
            cfgShowDebug = Config.Bind("海滩菜单预测助手", "显示调试信息", false, "Show internal state.");
            cfgKeepQueueFull = Config.Bind("海滩菜单预测助手", "始终补满未来队列", true, "After each generated order, append new predictions so the panel keeps showing the configured preview count.");

            _windowRect.x = cfgWindowX.Value;
            _windowRect.y = cfgWindowY.Value;

            try
            {
                _harmony = new Harmony("dev.chatgpt.overcooked2.beachpredictor.v3force");
                PatchByReflectionEx("DynamicRoundData", "GetNextRecipe", "DynamicRoundData_GetNextRecipe_Prefix", "DynamicRoundData_GetNextRecipe_Postfix", false);
                PatchByReflectionEx("DynamicRoundData", "GetWeight", null, "DynamicRoundData_GetWeight_Postfix", false);
                PatchByReflection("ServerOrderControllerBase", "Update", "ServerOrderControllerBase_Update_Postfix", false);
                PatchByReflection("ServerOrderControllerBase", "AddNewOrder", "ServerOrderControllerBase_AddNewOrder_Postfix", true);
                PatchByReflection("ServerDynamicOrderController", "MoveToNextPhase", "ServerDynamicOrderController_MoveToNextPhase_Postfix", false);
                PatchByReflection("ServerCampaignFlowController", "StartSynchronising", "Flow_Reset_Postfix", false);
                PatchByReflection("ServerCampaignFlowController", "StopSynchronising", "Flow_Reset_Postfix", false);
                PatchByReflection("ClientCampaignFlowController", "StopSynchronising", "Flow_Reset_Postfix", false);
                Logger.LogInfo("OC2 Beach Predictor V3 Force loaded.");
            }
            catch (Exception ex)
            {
                Logger.LogError("Patch failed: " + ex);
                _status = "Patch failed: " + ex.GetType().Name;
            }
        }

        private void OnDestroy()
        {
            try
            {
                if (_harmony != null)
                {
                    _harmony.UnpatchAll("dev.chatgpt.overcooked2.beachpredictor.v3force");
                }
            }
            catch { }
        }

        private void Update()
        {
            if (cfgToggleKey != null && Input.GetKeyDown(cfgToggleKey.Value))
            {
                _show = !_show;
            }
            if (cfgRefreshKey != null && Input.GetKeyDown(cfgRefreshKey.Value))
            {
                ForceRefresh();
            }
            if (cfgClearKey != null && Input.GetKeyDown(cfgClearKey.Value))
            {
                ClearPrediction("Forced queue cleared manually.");
            }

            if (!cfgEnabled.Value || !_show)
            {
                return;
            }

            if (cfgAutoRefreshWhenEmpty != null && cfgAutoRefreshWhenEmpty.Value && _forcedQueue.Count == 0 && Time.unscaledTime >= _nextRefreshTime)
            {
                _nextRefreshTime = Time.unscaledTime + Mathf.Max(0.10f, cfgRefreshInterval.Value);
                ForceRefresh();
            }
        }

        private void OnGUI()
        {
            if (!cfgEnabled.Value || !_show)
            {
                return;
            }
            if (!IsBeachScene())
            {
                return;
            }

            EnsureStyles();

            int count = _forcedQueue == null ? 0 : _forcedQueue.Count;
            if (count <= 0)
            {
                DrawWaitingPanel();
                HandleDrag();
                return;
            }

            int visibleCount = Mathf.Min(count, Math.Max(1, Math.Min(20, cfgPreviewCount.Value)));
            float rowHeight = 30f;
            float paddingX = 10f;
            float paddingY = 10f;

            _windowRect.width = 210f;
            _windowRect.height = paddingY * 2f + rowHeight * visibleCount + 2f;

            GUI.Box(_windowRect, "", _boxStyle);

            float y = _windowRect.y + paddingY;
            for (int i = 0; i < visibleCount; i++)
            {
                Rect row = new Rect(_windowRect.x + paddingX, y, _windowRect.width - paddingX * 2f, rowHeight - 2f);
                GUI.DrawTexture(row, i == 0 ? _firstRowTex : _rowTex);

                Rect numberRect = new Rect(row.x + 8f, row.y + 4f, 26f, row.height);
                Rect nameRect = new Rect(row.x + 36f, row.y + 3f, row.width - 42f, row.height);

                GUI.Label(numberRect, (i + 1).ToString(), _numberStyle);
                GUI.Label(nameRect, _forcedQueue[i].Name, i == 0 ? _firstLabelStyle : _labelStyle);

                y += rowHeight;

                if (i == 0 && visibleCount > 1)
                {
                    Rect divider = new Rect(_windowRect.x + paddingX + 6f, y - 2f, _windowRect.width - paddingX * 2f - 12f, 1f);
                    GUI.DrawTexture(divider, _dividerTex);
                }
            }

            HandleDrag();
        }

        private static void DrawWaitingPanel()
        {
            _windowRect.width = 180f;
            _windowRect.height = 50f;
            GUI.Box(_windowRect, "", _boxStyle);
            Rect label = new Rect(_windowRect.x + 12f, _windowRect.y + 13f, _windowRect.width - 24f, 26f);
            GUI.Label(label, "等待菜单...", _labelStyle);
        }

        private static void EnsureStyles()
        {
            if (_boxStyle != null) return;

            _panelTex = MakeTex(new Color(0f, 0f, 0f, 0.62f));
            _rowTex = MakeTex(new Color(1f, 1f, 1f, 0.055f));
            _firstRowTex = MakeTex(new Color(1f, 1f, 1f, 0.14f));
            _dividerTex = MakeTex(new Color(1f, 1f, 1f, 0.18f));

            _boxStyle = new GUIStyle(GUI.skin.box);
            _boxStyle.normal.background = _panelTex;
            _boxStyle.border = new RectOffset(8, 8, 8, 8);
            _boxStyle.padding = new RectOffset(0, 0, 0, 0);

            _labelStyle = new GUIStyle(GUI.skin.label);
            _labelStyle.normal.textColor = new Color(1f, 1f, 1f, 0.94f);
            _labelStyle.fontSize = 17;
            _labelStyle.fontStyle = FontStyle.Normal;
            _labelStyle.alignment = TextAnchor.MiddleLeft;
            _labelStyle.wordWrap = false;

            _firstLabelStyle = new GUIStyle(_labelStyle);
            _firstLabelStyle.fontSize = 18;
            _firstLabelStyle.fontStyle = FontStyle.Bold;
            _firstLabelStyle.normal.textColor = Color.white;

            _numberStyle = new GUIStyle(GUI.skin.label);
            _numberStyle.normal.textColor = new Color(1f, 1f, 1f, 0.62f);
            _numberStyle.fontSize = 14;
            _numberStyle.alignment = TextAnchor.MiddleCenter;
            _numberStyle.wordWrap = false;
        }

        private static Texture2D MakeTex(Color c)
        {
            Texture2D t = new Texture2D(1, 1);
            t.SetPixel(0, 0, c);
            t.Apply();
            return t;
        }

        private static void HandleDrag()
        {
            Event e = Event.current;
            if (e == null) return;
            Rect title = new Rect(_windowRect.x, _windowRect.y, _windowRect.width, _windowRect.height);
            if (e.type == EventType.MouseDown && title.Contains(e.mousePosition))
            {
                _dragging = true;
                _dragStart = e.mousePosition - new Vector2(_windowRect.x, _windowRect.y);
                e.Use();
            }
            if (_dragging && e.type == EventType.MouseDrag)
            {
                _windowRect.x = e.mousePosition.x - _dragStart.x;
                _windowRect.y = e.mousePosition.y - _dragStart.y;
                if (cfgWindowX != null) cfgWindowX.Value = _windowRect.x;
                if (cfgWindowY != null) cfgWindowY.Value = _windowRect.y;
                e.Use();
            }
            if (e.type == EventType.MouseUp)
            {
                _dragging = false;
            }
        }

        private static void PatchByReflectionEx(string typeName, string methodName, string prefixName, string postfixName, bool parameterlessOnly)
        {
            Type targetType = FindType(typeName);
            if (targetType == null)
            {
                if (Instance != null) Instance.Logger.LogWarning("Type not found: " + typeName);
                return;
            }

            MethodInfo target = null;
            MethodInfo[] methods = targetType.GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static);
            for (int i = 0; i < methods.Length; i++)
            {
                if (methods[i].Name != methodName) continue;
                if (parameterlessOnly && methods[i].GetParameters().Length != 0) continue;
                target = methods[i];
                break;
            }
            if (target == null)
            {
                if (Instance != null) Instance.Logger.LogWarning("Method not found: " + typeName + "." + methodName);
                return;
            }

            HarmonyMethod prefix = null;
            HarmonyMethod postfix = null;
            if (!string.IsNullOrEmpty(prefixName))
            {
                MethodInfo prefixMethod = typeof(BeachPredictorV3ForcePlugin).GetMethod(prefixName, BindingFlags.Static | BindingFlags.NonPublic);
                if (prefixMethod != null) prefix = new HarmonyMethod(prefixMethod);
            }
            if (!string.IsNullOrEmpty(postfixName))
            {
                MethodInfo postfixMethod = typeof(BeachPredictorV3ForcePlugin).GetMethod(postfixName, BindingFlags.Static | BindingFlags.NonPublic);
                if (postfixMethod != null) postfix = new HarmonyMethod(postfixMethod);
            }
            _harmony.Patch(target, prefix, postfix, null, null);
        }

        private static void PatchByReflection(string typeName, string methodName, string postfixName, bool parameterlessOnly)
        {
            Type targetType = FindType(typeName);
            if (targetType == null)
            {
                if (Instance != null) Instance.Logger.LogWarning("Type not found: " + typeName);
                return;
            }

            MethodInfo target = null;
            MethodInfo[] methods = targetType.GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static);
            for (int i = 0; i < methods.Length; i++)
            {
                if (methods[i].Name != methodName) continue;
                if (parameterlessOnly && methods[i].GetParameters().Length != 0) continue;
                target = methods[i];
                break;
            }
            if (target == null)
            {
                if (Instance != null) Instance.Logger.LogWarning("Method not found: " + typeName + "." + methodName);
                return;
            }

            MethodInfo postfix = typeof(BeachPredictorV3ForcePlugin).GetMethod(postfixName, BindingFlags.Static | BindingFlags.NonPublic);
            _harmony.Patch(target, null, new HarmonyMethod(postfix), null, null);
        }

        private static Type FindType(string simpleName)
        {
            Type t = Type.GetType(simpleName + ", Assembly-CSharp");
            if (t != null) return t;
            Assembly[] assemblies = AppDomain.CurrentDomain.GetAssemblies();
            for (int i = 0; i < assemblies.Length; i++)
            {
                try
                {
                    Type[] types = assemblies[i].GetTypes();
                    for (int j = 0; j < types.Length; j++)
                    {
                        if (types[j].Name == simpleName || types[j].FullName == simpleName) return types[j];
                    }
                }
                catch { }
            }
            return null;
        }

        private static void DynamicRoundData_GetNextRecipe_Prefix(object __instance, object _data)
        {
            if (!cfgEnabled.Value) return;
            if (!IsBeachScene()) return;

            try
            {
                int currentRecipeCount = GetIntField(_data, "RecipeCount", -1);
                int currentPhase = GetIntField(_data, "CurrentPhase", -1);

                if (_forcedQueue.Count > 0 && (currentRecipeCount != _queueExpectedRecipeCount || currentPhase != _queuePhase))
                {
                    _forcedQueue.Clear();
                    _queueExpectedRecipeCount = -1;
                    _queuePhase = -1;
                }

                if (_forcedQueue.Count == 0)
                {
                    BuildForcedQueue(__instance, _data, "Auto-plan before GetNextRecipe");
                }

                if (_forcedQueue.Count > 0)
                {
                    _forcingActive = true;
                    _forcedIndex = _forcedQueue[0].Index;
                    _status = "FORCING next order: " + _forcedQueue[0].Name;
                }
                else
                {
                    _forcingActive = false;
                    _forcedIndex = -1;
                }
            }
            catch (Exception ex)
            {
                _forcingActive = false;
                _forcedIndex = -1;
                _status = "Force prefix failed: " + ex.GetType().Name;
                if (Instance != null) Instance.Logger.LogWarning("Force prefix failed: " + ex);
            }
        }

        private static void DynamicRoundData_GetNextRecipe_Postfix(object __instance, object _data, object __result)
        {
            if (!_forcingActive)
            {
                return;
            }

            try
            {
                string actual = ReadResultName(__result);
                if (_forcedQueue.Count > 0)
                {
                    PredictedRecipe p = _forcedQueue[0];
                    _forcedQueue.RemoveAt(0);
                    _ordersForced++;
                    _queueExpectedRecipeCount = GetIntField(_data, "RecipeCount", _queueExpectedRecipeCount + 1);

                    string reason = "已强制出单：" + p.Name + (string.IsNullOrEmpty(actual) ? "" : " / 实际=" + actual);
                    if (cfgKeepQueueFull != null && cfgKeepQueueFull.Value)
                    {
                        RefillForcedQueue(__instance, _data, reason);
                    }
                    else
                    {
                        BuildQueueText(reason);
                    }
                }
            }
            catch (Exception ex)
            {
                _status = "Force postfix failed: " + ex.GetType().Name;
            }
            finally
            {
                _forcingActive = false;
                _forcedIndex = -1;
            }
        }

        private static void DynamicRoundData_GetWeight_Postfix(object __instance, object _data, int _recipeIndex, ref float __result)
        {
            if (!_forcingActive) return;
            if (_forcedIndex < 0) return;
            if (_recipeIndex != _forcedIndex)
            {
                __result = 0f;
            }
            else if (__result <= 0f)
            {
                // If another mod has zeroed the chosen entry, give it a tiny positive weight so the
                // original GetNextRecipe can still select exactly the planned candidate.
                __result = 0.0001f;
            }
        }

        private static string ReadResultName(object result)
        {
            Array arr = result as Array;
            if (arr == null || arr.Length == 0) return "";
            object entry = arr.GetValue(0);
            int uid = GetEntryUid(entry);
            return GetRecipeName(FindRecipeIndexByUid(uid), uid);
        }

        private static int FindRecipeIndexByUid(int uid)
        {
            try
            {
                if (_lastServerController == null) return -1;
                object roundData = GetField(_lastServerController, "m_roundData");
                object inst = GetField(_lastServerController, "m_roundInstanceData");
                if (roundData == null || inst == null) return -1;
                object phasesObj = GetField(roundData, "Phases");
                Array phases = phasesObj as Array;
                int currentPhase = GetIntField(inst, "CurrentPhase", 0);
                if (phases == null || currentPhase < 0 || currentPhase >= phases.Length) return -1;
                object phase = phases.GetValue(currentPhase);
                object recipeList = GetField(phase, "Recipes");
                Array entries = GetField(recipeList, "m_recipes") as Array;
                if (entries == null) return -1;
                for (int i = 0; i < entries.Length; i++)
                {
                    if (GetEntryUid(entries.GetValue(i)) == uid) return i;
                }
            }
            catch { }
            return -1;
        }

        private static void ServerOrderControllerBase_Update_Postfix(object __instance)
        {
            if (!cfgEnabled.Value) return;
            if (!IsBeachScene()) return;
            object roundData = GetField(__instance, "m_roundData");
            if (roundData == null) return;
            string n = roundData.GetType().Name;
            if (n == "DynamicRoundData" || roundData.GetType().BaseType != null && roundData.GetType().BaseType.Name == "DynamicRoundData")
            {
                _lastServerController = __instance;
            }
        }

        private static void ServerOrderControllerBase_AddNewOrder_Postfix(object __instance)
        {
            if (!cfgEnabled.Value) return;
            if (!IsBeachScene()) return;
            _lastServerController = __instance;
            if (_forcedQueue.Count == 0)
            {
                ForceRefresh();
            }
            else if (cfgKeepQueueFull != null && cfgKeepQueueFull.Value)
            {
                object roundData = GetField(__instance, "m_roundData");
                object inst = GetField(__instance, "m_roundInstanceData");
                RefillForcedQueue(roundData, inst, "订单已加入，补满未来队列");
            }
            else
            {
                BuildQueueText("订单已加入，继续使用已锁定队列");
            }
        }

        private static void ServerDynamicOrderController_MoveToNextPhase_Postfix()
        {
            ClearPrediction("Phase changed / reset.");
        }

        private static void Flow_Reset_Postfix()
        {
            ClearPrediction("Flow reset.");
        }

        private static bool IsBeachScene()
        {
            try
            {
                return SceneManager.GetActiveScene().name == "s_beach_special";
            }
            catch
            {
                return false;
            }
        }

        private static void ClearPrediction(string reason)
        {
            _predictions.Clear();
            _forcedQueue.Clear();
            _forcingActive = false;
            _forcedIndex = -1;
            _queueExpectedRecipeCount = -1;
            _queuePhase = -1;
            _predictionText = "";
            _status = reason;
        }

        private static void ForceRefresh()
        {
            try
            {
                if (!IsBeachScene())
                {
                    ClearPrediction("Not in s_beach_special.");
                    return;
                }
                if (_lastServerController == null)
                {
                    _predictionText = "等待服务器订单控制器...\n如果你不是房主，可能无法预测。";
                    _status = "No ServerOrderControllerBase captured yet.";
                    return;
                }
                ComputePrediction();
            }
            catch (Exception ex)
            {
                _predictionText = "预测失败：" + ex.GetType().Name;
                _status = ex.Message;
                if (Instance != null) Instance.Logger.LogWarning("Prediction failed: " + ex);
            }
        }

        private static void ComputePrediction()
        {
            object roundData = GetField(_lastServerController, "m_roundData");
            object inst = GetField(_lastServerController, "m_roundInstanceData");
            BuildForcedQueue(roundData, inst, "手动/空队列规划");
        }

        private static void BuildForcedQueue(object roundData, object inst, string reason)
        {
            if (roundData == null || inst == null)
            {
                _predictionText = "没有读取到 roundData / instanceData";
                _status = "Round state missing.";
                return;
            }

            object phasesObj = GetField(roundData, "Phases");
            Array phases = phasesObj as Array;
            int currentPhase = GetIntField(inst, "CurrentPhase", 0);
            if (phases == null || currentPhase < 0 || currentPhase >= phases.Length)
            {
                _predictionText = "当前不是 DynamicRoundData 阶段数据。";
                _status = "Invalid phase data.";
                return;
            }

            object phase = phases.GetValue(currentPhase);
            object recipeList = GetField(phase, "Recipes");
            Array entries = GetField(recipeList, "m_recipes") as Array;
            if (entries == null || entries.Length == 0)
            {
                _predictionText = "没有读取到海滩菜谱列表。";
                _status = "Recipe list missing.";
                return;
            }

            int[] counts = CopyIntArray(GetField(inst, "CumulativeFrequencies") as Array, entries.Length);
            int baseRecipeCount = GetIntField(inst, "RecipeCount", Sum(counts));
            int recipeCount = baseRecipeCount;
            int previewCount = Math.Max(1, Math.Min(20, cfgPreviewCount.Value));

            ControlRecipeState control = ReadControlRecipeState(recipeCount, counts);
            _predictions.Clear();

            object savedState = SaveRandomState();
            try
            {
                for (int step = 0; step < previewCount; step++)
                {
                    int chosen = ChooseNextRecipeIndex(entries.Length, counts, control);
                    if (chosen < 0)
                    {
                        break;
                    }
                    int uid = GetEntryUid(entries.GetValue(chosen));
                    string name = GetRecipeName(chosen, uid);
                    float[] weights = BuildWeights(entries.Length, counts, control);
                    float total = SumFloat(weights);
                    _predictions.Add(new PredictedRecipe(chosen, uid, name, total));

                    recipeCount++;
                    counts[chosen]++;
                    control.UpdateAfterChosen(chosen, recipeCount, counts);
                }
            }
            finally
            {
                RestoreRandomState(savedState);
            }

            _forcedQueue.Clear();
            for (int i = 0; i < _predictions.Count; i++)
            {
                _forcedQueue.Add(_predictions[i].Clone());
            }
            _queueExpectedRecipeCount = baseRecipeCount;
            _queuePhase = currentPhase;
            BuildQueueText(reason);
        }

        private static void RefillForcedQueue(object roundData, object inst, string reason)
        {
            try
            {
                if (roundData == null || inst == null)
                {
                    BuildQueueText(reason + " | 无法补队列：缺少 roundData/instanceData");
                    return;
                }

                object phasesObj = GetField(roundData, "Phases");
                Array phases = phasesObj as Array;
                int currentPhase = GetIntField(inst, "CurrentPhase", _queuePhase);
                if (phases == null || currentPhase < 0 || currentPhase >= phases.Length)
                {
                    BuildQueueText(reason + " | 无法补队列：phase 无效");
                    return;
                }

                object phase = phases.GetValue(currentPhase);
                object recipeList = GetField(phase, "Recipes");
                Array entries = GetField(recipeList, "m_recipes") as Array;
                if (entries == null || entries.Length == 0)
                {
                    BuildQueueText(reason + " | 无法补队列：菜谱为空");
                    return;
                }

                int targetCount = Math.Max(1, Math.Min(20, cfgPreviewCount.Value));
                if (_forcedQueue.Count >= targetCount)
                {
                    BuildQueueText(reason);
                    return;
                }

                int[] counts = CopyIntArray(GetField(inst, "CumulativeFrequencies") as Array, entries.Length);
                int liveRecipeCount = GetIntField(inst, "RecipeCount", Sum(counts));
                ControlRecipeState control = ReadControlRecipeState(liveRecipeCount, counts);

                // Advance a local simulation through the already locked tail.
                // This preserves the visible order: after A appears, the panel becomes B,C,D,E,F,G,
                // instead of discarding B-F and rebuilding a completely new six-item queue.
                for (int i = 0; i < _forcedQueue.Count; i++)
                {
                    int idx = _forcedQueue[i].Index;
                    if (idx >= 0 && idx < counts.Length)
                    {
                        liveRecipeCount++;
                        counts[idx]++;
                        control.UpdateAfterChosen(idx, liveRecipeCount, counts);
                    }
                }

                object savedState = SaveRandomState();
                try
                {
                    while (_forcedQueue.Count < targetCount)
                    {
                        int chosen = ChooseNextRecipeIndex(entries.Length, counts, control);
                        if (chosen < 0) break;

                        int uid = GetEntryUid(entries.GetValue(chosen));
                        string name = GetRecipeName(chosen, uid);
                        float[] weights = BuildWeights(entries.Length, counts, control);
                        float total = SumFloat(weights);
                        _forcedQueue.Add(new PredictedRecipe(chosen, uid, name, total));

                        liveRecipeCount++;
                        counts[chosen]++;
                        control.UpdateAfterChosen(chosen, liveRecipeCount, counts);
                    }
                }
                finally
                {
                    RestoreRandomState(savedState);
                }

                _queueExpectedRecipeCount = GetIntField(inst, "RecipeCount", _queueExpectedRecipeCount);
                _queuePhase = currentPhase;
                BuildQueueText(reason + " | 已补满到 " + _forcedQueue.Count + " 单");
            }
            catch (Exception ex)
            {
                BuildQueueText(reason + " | 补队列失败：" + ex.GetType().Name);
                if (Instance != null) Instance.Logger.LogWarning("Refill forced queue failed: " + ex);
            }
        }

        private static void BuildQueueText(string reason)
        {
            if (_forcedQueue.Count == 0)
            {
                _predictionText = "等待菜单...";
                _status = reason + " | No forced candidates.";
                return;
            }

            System.Text.StringBuilder sb = new System.Text.StringBuilder();
            for (int i = 0; i < _forcedQueue.Count; i++)
            {
                PredictedRecipe p = _forcedQueue[i];
                sb.Append(p.Name);
                if (cfgShowDebug.Value)
                {
                    sb.Append("  [").Append(p.Index).Append('/').Append(p.Uid).Append("]");
                }
                if (i < _forcedQueue.Count - 1) sb.AppendLine();
            }
            _predictionText = sb.ToString();

            System.Text.StringBuilder st = new System.Text.StringBuilder();
            st.Append(reason);
            st.Append(" | mode=FORCE | phase=").Append(_queuePhase);
            st.Append(" | nextRecipeCount=").Append(_queueExpectedRecipeCount);
            st.Append(" | forced=").Append(_ordersForced);
            if (cfgShowDebug.Value)
            {
                st.Append(" | currentForcedIndex=").Append(_forcedIndex);
            }
            _status = st.ToString();
        }

        private static float[] BuildWeights(int recipeLen, int[] counts, ControlRecipeState control)
        {
            float[] weights = new float[recipeLen];
            int sum = Sum(counts);
            float average = ((float)sum + 2f) / (float)recipeLen;
            for (int i = 0; i < recipeLen; i++)
            {
                if (control != null && !control.AllowCandidate(i, counts))
                {
                    weights[i] = 0f;
                }
                else
                {
                    float w = average - (float)counts[i];
                    weights[i] = Mathf.Max(w, 0f);
                }
            }
            return weights;
        }

        private static int ChooseNextRecipeIndex(int recipeLen, int[] counts, ControlRecipeState control)
        {
            float[] weights = BuildWeights(recipeLen, counts, control);
            float total = SumFloat(weights);
            if (total <= 0f) return -1;
            float r = UnityEngine.Random.Range(0f, total);
            float acc = 0f;
            for (int i = 0; i < weights.Length; i++)
            {
                acc += weights[i];
                if (r <= acc)
                {
                    return i;
                }
            }
            return -1;
        }

        private static void BuildPredictionText(int recipeCount, int phase, int[] counts, ControlRecipeState control)
        {
            if (_predictions.Count == 0)
            {
                _predictionText = "暂时无法预测：候选权重为 0。";
                _status = "No candidates.";
                return;
            }

            System.Text.StringBuilder sb = new System.Text.StringBuilder();
            sb.AppendLine("未来菜单：");
            for (int i = 0; i < _predictions.Count; i++)
            {
                PredictedRecipe p = _predictions[i];
                sb.Append(i + 1).Append(". ").Append(p.Name);
                if (cfgShowDebug.Value)
                {
                    sb.Append("  [idx=").Append(p.Index).Append(", uid=").Append(p.Uid).Append("]");
                }
                sb.AppendLine();
            }
            _predictionText = sb.ToString();

            System.Text.StringBuilder st = new System.Text.StringBuilder();
            st.Append("phase=").Append(phase).Append("  already=").Append(recipeCount - _predictions.Count);
            if (control != null && control.ModDetected)
            {
                st.Append("  原工具规则=").Append(control.BeachMode);
            }
            else
            {
                st.Append("  原工具规则=未检测/未启用");
            }
            if (cfgShowDebug.Value)
            {
                st.Append("\ncounts=");
                for (int i = 0; i < counts.Length; i++)
                {
                    if (i > 0) st.Append(',');
                    st.Append(counts[i]);
                }
            }
            _status = st.ToString();
        }

        private static ControlRecipeState ReadControlRecipeState(int recipeCount, int[] counts)
        {
            ControlRecipeState st = new ControlRecipeState();
            st.RecipeCount = recipeCount;
            st.Cumulative = counts;
            st.BeachMode = "不修改";
            st.BeachStart = false;
            st.CoiledThreeFruit = false;
            st.CoiledFourGrill = false;
            st.PreviousOne = -1;
            st.PreviousTwo = -1;
            st.GreaterThan3 = false;
            st.LessThan4 = false;
            st.ModDetected = false;

            if (!cfgRespectControlRecipeMod.Value)
            {
                return st;
            }

            Type t = FindType("OC2Controlrecipe.ControlrecipePlugin");
            if (t == null) t = FindType("ControlrecipePlugin");
            if (t == null)
            {
                return st;
            }

            st.ModDetected = true;
            st.BeachStart = GetStaticBool(t, "beachStart", false);
            st.PreviousOne = GetStaticInt(t, "previousoneIndex", -1);
            st.PreviousTwo = GetStaticInt(t, "previoustwoIndex", -1);
            st.GreaterThan3 = GetStaticBool(t, "greaterThan3", false);
            st.LessThan4 = GetStaticBool(t, "lessThan4", false);
            st.RecipeCount = GetStaticInt(t, "recipeCount", recipeCount);
            st.BeachMode = GetConfigEntryValueString(GetStaticField(t, "beachValueList"), "不修改");
            st.CoiledThreeFruit = GetConfigEntryValueBool(GetStaticField(t, "coiledThreeFruitEnabled"), false);
            st.CoiledFourGrill = GetConfigEntryValueBool(GetStaticField(t, "coiledFourGrillEnabled"), false);
            return st;
        }

        private static object SaveRandomState()
        {
            try
            {
                PropertyInfo prop = typeof(UnityEngine.Random).GetProperty("state", BindingFlags.Static | BindingFlags.Public);
                if (prop != null) return prop.GetValue(null, null);
            }
            catch { }
            return null;
        }

        private static void RestoreRandomState(object state)
        {
            if (state == null) return;
            try
            {
                PropertyInfo prop = typeof(UnityEngine.Random).GetProperty("state", BindingFlags.Static | BindingFlags.Public);
                if (prop != null && prop.CanWrite) prop.SetValue(null, state, null);
            }
            catch { }
        }

        private static object GetField(object obj, string fieldName)
        {
            if (obj == null) return null;
            Type t = obj.GetType();
            while (t != null)
            {
                FieldInfo f = t.GetField(fieldName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                if (f != null) return f.GetValue(obj);
                t = t.BaseType;
            }
            return null;
        }

        private static object GetStaticField(Type t, string fieldName)
        {
            if (t == null) return null;
            while (t != null)
            {
                FieldInfo f = t.GetField(fieldName, BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
                if (f != null) return f.GetValue(null);
                t = t.BaseType;
            }
            return null;
        }

        private static int GetIntField(object obj, string fieldName, int fallback)
        {
            object v = GetField(obj, fieldName);
            if (v == null) return fallback;
            try { return Convert.ToInt32(v); } catch { return fallback; }
        }

        private static int GetStaticInt(Type t, string fieldName, int fallback)
        {
            object v = GetStaticField(t, fieldName);
            if (v == null) return fallback;
            try { return Convert.ToInt32(v); } catch { return fallback; }
        }

        private static bool GetStaticBool(Type t, string fieldName, bool fallback)
        {
            object v = GetStaticField(t, fieldName);
            if (v == null) return fallback;
            try { return Convert.ToBoolean(v); } catch { return fallback; }
        }

        private static string GetConfigEntryValueString(object configEntry, string fallback)
        {
            if (configEntry == null) return fallback;
            try
            {
                PropertyInfo p = configEntry.GetType().GetProperty("Value");
                if (p == null) return fallback;
                object v = p.GetValue(configEntry, null);
                return v == null ? fallback : v.ToString();
            }
            catch { return fallback; }
        }

        private static bool GetConfigEntryValueBool(object configEntry, bool fallback)
        {
            if (configEntry == null) return fallback;
            try
            {
                PropertyInfo p = configEntry.GetType().GetProperty("Value");
                if (p == null) return fallback;
                object v = p.GetValue(configEntry, null);
                return Convert.ToBoolean(v);
            }
            catch { return fallback; }
        }

        private static int[] CopyIntArray(Array arr, int len)
        {
            int[] result = new int[len];
            if (arr == null) return result;
            int n = Math.Min(len, arr.Length);
            for (int i = 0; i < n; i++)
            {
                try { result[i] = Convert.ToInt32(arr.GetValue(i)); } catch { result[i] = 0; }
            }
            return result;
        }

        private static int GetEntryUid(object entry)
        {
            object order = GetField(entry, "m_order");
            if (order == null) order = GetField(entry, "Order");
            object uid = GetField(order, "m_uID");
            if (uid == null) return 0;
            try { return Convert.ToInt32(uid); } catch { return 0; }
        }

        private static string GetRecipeName(int index, int uid)
        {
            if (index >= 0 && index < BeachRecipeNames.Length) return BeachRecipeNames[index];
            return "未知菜品(" + uid + ")";
        }

        private static int Sum(int[] a)
        {
            int s = 0;
            if (a == null) return 0;
            for (int i = 0; i < a.Length; i++) s += a[i];
            return s;
        }

        private static float SumFloat(float[] a)
        {
            float s = 0f;
            if (a == null) return 0f;
            for (int i = 0; i < a.Length; i++) s += a[i];
            return s;
        }

        private class PredictedRecipe
        {
            public int Index;
            public int Uid;
            public string Name;
            public float TotalWeight;
            public PredictedRecipe(int index, int uid, string name, float totalWeight)
            {
                Index = index; Uid = uid; Name = name; TotalWeight = totalWeight;
            }

            public PredictedRecipe Clone()
            {
                return new PredictedRecipe(Index, Uid, Name, TotalWeight);
            }
        }

        private class ControlRecipeState
        {
            public bool ModDetected;
            public int RecipeCount;
            public int PreviousOne;
            public int PreviousTwo;
            public bool GreaterThan3;
            public bool LessThan4;
            public bool BeachStart;
            public string BeachMode;
            public bool CoiledThreeFruit;
            public bool CoiledFourGrill;
            public int[] Cumulative;

            public bool AllowCandidate(int candidate, int[] counts)
            {
                bool doubleBeach = BeachMode == "双海开局";
                bool fourBeach = BeachMode == "四海开局";
                bool barbecueFruit = BeachMode == "烧烤水果";
                bool doubleBarbecue = BeachMode == "双烧烤";
                bool doubleFruit = BeachMode == "双水果";

                if (ModDetected)
                {
                    if (BeachStart && fourBeach && RecipeCount == 2 && PreviousOne < 4 && PreviousTwo < 4 && candidate < 4) return false;
                    if (BeachStart && fourBeach && RecipeCount == 1 && PreviousOne > 3 && candidate > 3) return false;
                    if (BeachStart && doubleBeach && RecipeCount == 2 && PreviousOne > 3 && candidate > 3) return false;
                    if (BeachStart && (doubleBarbecue || barbecueFruit) && RecipeCount == 0 && candidate > 3) return false;
                    if (BeachStart && doubleFruit && RecipeCount == 0 && candidate < 4) return false;
                    if (BeachStart && (doubleFruit || barbecueFruit) && RecipeCount == 1 && candidate < 4) return false;
                    if (RecipeCount == 6 && counts != null && candidate >= 0 && candidate < counts.Length && counts[candidate] > 0) return false;
                    if (CoiledThreeFruit && GreaterThan3 && candidate > 3) return false;
                    if (CoiledFourGrill && LessThan4 && candidate < 4) return false;
                }
                return true;
            }

            public void UpdateAfterChosen(int chosen, int newRecipeCount, int[] counts)
            {
                RecipeCount = newRecipeCount;
                Cumulative = counts;
                if (!ModDetected) return;

                GreaterThan3 = false;
                LessThan4 = false;

                if (PreviousOne == -1)
                {
                    PreviousOne = chosen;
                    return;
                }

                if (PreviousOne != -1 && PreviousTwo == -1)
                {
                    if (PreviousOne > 3 && chosen > 3) GreaterThan3 = true;
                    PreviousTwo = chosen;
                    return;
                }

                if (PreviousOne != -1 && PreviousTwo != -1)
                {
                    if (PreviousTwo > 3 && chosen > 3)
                    {
                        GreaterThan3 = true;
                    }
                    else if (PreviousOne < 4 && PreviousTwo < 4 && chosen < 4)
                    {
                        LessThan4 = true;
                    }
                    PreviousOne = PreviousTwo;
                    PreviousTwo = chosen;
                }
            }
        }
    }
}
