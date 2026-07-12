using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using BepInEx;
using BepInEx.Configuration;
using UnityEngine;
using UnityEngine.Rendering.PostProcessing;

namespace UmaPetForge
{
    [BepInPlugin(PluginGuid, PluginName, PluginVersion)]
    public sealed class UmaPetForgePlugin : BaseUnityPlugin
    {
        public const string PluginGuid = "dev.pqqqqq.umapetforge";
        public const string PluginName = "UmaPetForge";
        public const string PluginVersion = "0.3.0";

        private const int CellWidth = 192;
        private const int CellHeight = 208;
        private const int Columns = 8;
        private const int Rows = 9;
        private const int SupersampleFactor = 4;
        private const int CaptureWidth = CellWidth * SupersampleFactor;
        private const int CaptureHeight = CellHeight * SupersampleFactor;

        private static readonly int[][] StateSpriteIndices =
        {
            new[] { 0, 1, 2, 3, 4, 5 },
            new[] { 8, 9, 10, 11, 12, 13, 14, 15 },
            new[] { 16, 17, 18, 19, 20, 21, 22, 23 },
            new[] { 24, 25, 26, 27 },
            new[] { 32, 33, 34, 35, 36 },
            new[] { 40, 41, 42, 43, 44, 45, 46, 47 },
            new[] { 48, 49, 50, 51, 52, 53 },
            new[] { 56, 57, 58, 59, 60, 61 },
            new[] { 64, 65, 66, 67, 68, 69 }
        };

        private static readonly StateDefinition[] States =
        {
            new StateDefinition("idle", 6, 0f, true,
                new[] { "idle01_loop", "idle", "stand", "homestand" }),
            new StateDefinition("run_right", 8, -90f, true,
                new[] { "run", "dash", "jog", "walk" }),
            new StateDefinition("run_left", 8, 90f, true,
                new[] { "run", "dash", "jog", "walk" }),
            new StateDefinition("wave", 4, 0f, false,
                new[] { "hello", "byebye", "come", "clap", "wave", "greet" }),
            new StateDefinition("jump", 5, 0f, false,
                new[] { "jump", "hop", "leap" }),
            new StateDefinition("failure", 8, 0f, false,
                new[] { "tired", "akire", "soppo", "hungry", "shy", "fail", "lose", "sad" }),
            new StateDefinition("waiting", 6, 0f, true,
                new[] { "homestand", "sudachi", "koshiate", "udekumi", "idle02", "wait", "stand" }),
            new StateDefinition("working", 6, 0f, true,
                new[] { "idea", "book", "job", "work", "study", "write", "think" }),
            new StateDefinition("review", 6, 0f, false,
                new[] { "check", "see", "near", "look", "inspect", "search", "read" })
        };

        private static readonly string[] PickerMotionStates =
        {
            "idle",
            "run_right",
            "run_left",
            "jump"
        };

        private const string DefaultCharacters = "";
        private const string LegacyDefaultCharacters =
            "Special Week, Silence Suzuka, Tokai Teio, Mejiro McQueen, Gold Ship, " +
            "Rice Shower, Oguri Cap, Kitasan Black, Satono Diamond, Twin Turbo, " +
            "Nice Nature, Haru Urara";

        private ConfigEntry<string> _characters;
        private ConfigEntry<string> _outputDirectory;
        private ConfigEntry<bool> _writeIndividualFrames;
        private ConfigEntry<string> _motionOverridesFile;
        private ConfigEntry<string> _characterCostumes;
        private ConfigEntry<string> _characterStateMotions;
        private ConfigEntry<string> _characterStateFaces;
        private Dictionary<string, string> _motionOverrides =
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        private Dictionary<string, string> _configuredPickerMotions =
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        private Dictionary<string, MiniFaceSelection> _configuredPickerFaces =
            new Dictionary<string, MiniFaceSelection>(StringComparer.OrdinalIgnoreCase);
        private Dictionary<int, string> _configuredCostumes = new Dictionary<int, string>();
        private Dictionary<int, List<MiniCostumeOption>> _miniCostumeOptions =
            new Dictionary<int, List<MiniCostumeOption>>();
        private readonly HashSet<int> _pickerSelected = new HashSet<int>();
        private readonly Dictionary<int, string> _pickerCostumes = new Dictionary<int, string>();
        private readonly Dictionary<string, string> _pickerStateMotions =
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, MiniFaceSelection> _pickerStateFaces =
            new Dictionary<string, MiniFaceSelection>(StringComparer.OrdinalIgnoreCase);
        private List<CharaEntry> _pickerCharacters = new List<CharaEntry>();
        private List<UmaDatabaseEntry> _pickerMiniMotions = new List<UmaDatabaseEntry>();
        private Rect _pickerWindowRect;
        private Vector2 _pickerScroll;
        private Vector2 _pickerCostumeScroll;
        private Vector2 _pickerMotionScroll;
        private string _pickerSearch = string.Empty;
        private string _pickerCostumeSearch = string.Empty;
        private string _pickerMotionSearch = string.Empty;
        private string _pickerMotionState = string.Empty;
        private string _pickerFaceState = string.Empty;
        private string _pickerStatus = string.Empty;
        private int _pickerCostumeCharacterId = -1;
        private int _pickerMotionCharacterId = -1;
        private MiniFaceSelection _pickerFaceDraft;
        private MiniFaceMaterials _pickerFacePreviewMaterials;
        private UmaContainerCharacter _pickerFacePreviewContainer;
        private UmaContainerCharacter _pickerFacePreviewAttemptContainer;
        private bool _pickerPreviewRequested;
        private bool _pickerPreviewLoading;
        private int _pickerPreviewCharacterId = -1;
        private string _pickerPreviewCostumeId = string.Empty;
        private UmaDatabaseEntry _pickerPreviewMotion;
        private string _pickerPreviewError = string.Empty;
        private bool _pickerOpen;
        private PickerAction _pickerAction;
        private bool _exportRequested;
        private bool _viewerReady;
        private bool _running;

        private const int PickerWindowId = 0x554D41;

        private enum PickerAction
        {
            None,
            Save,
            SaveAndExport
        }

        private void Awake()
        {
            _characters = Config.Bind(
                "General",
                "Characters",
                DefaultCharacters,
                "Comma-separated UmaViewer character names or numeric character IDs.");
            _outputDirectory = Config.Bind(
                "General",
                "OutputDirectory",
                "UmaPetForge_Output",
                "Relative output directory beneath the UmaViewer folder.");
            _writeIndividualFrames = Config.Bind(
                "General",
                "WriteIndividualFrames",
                true,
                "Keep each sampled PNG frame in addition to the final atlas.");
            _motionOverridesFile = Config.Bind(
                "General",
                "MotionOverridesFile",
                "UmaPetForge_Overrides.csv",
                "Optional viewer-relative CSV with character_id,state,motion_key_or_path rows.");
            _characterCostumes = Config.Bind(
                "General",
                "CharacterCostumes",
                "",
                "Semicolon-separated characterId=miniCostumeId choices managed by the F6 picker.");
            _characterStateMotions = Config.Bind(
                "General",
                "CharacterStateMotions",
                "",
                "Semicolon-separated characterId:state=motionKey choices managed by the F6 picker.");
            _characterStateFaces = Config.Bind(
                "General",
                "CharacterStateFaces",
                "",
                "Semicolon-separated characterId:state=eyeL,eyeR,mouth,browL,browR choices managed by the F6 picker.");

            if (string.Equals(
                    _characters.Value,
                    LegacyDefaultCharacters,
                    StringComparison.Ordinal))
            {
                _characters.Value = string.Empty;
                Config.Save();
                Logger.LogInfo("Cleared the legacy preselected roster; the F6 picker now starts empty.");
            }

            Logger.LogInfo("Loaded. Waiting for UmaViewer initialization.");
            StartCoroutine(WaitForViewer());
        }

        private void Update()
        {
            if (!_viewerReady)
            {
                return;
            }

            if (_pickerPreviewRequested && !_pickerPreviewLoading && !_running)
            {
                _pickerPreviewRequested = false;
                _pickerPreviewLoading = true;
                StartCoroutine(LoadPickerPreview(
                    _pickerPreviewCharacterId,
                    _pickerPreviewCostumeId,
                    _pickerPreviewMotion));
                return;
            }

            if (Input.GetKeyDown(KeyCode.F6))
            {
                if (IsPickerPreviewBusy())
                {
                    ShowViewerMessage(
                        "UmaPetForge Mini preview is still loading",
                        UIMessageType.Default);
                }
                else if (_running)
                {
                    ShowViewerMessage("UmaPetForge export is already running", UIMessageType.Default);
                }
                else
                {
                    ToggleCharacterPicker();
                }
                return;
            }

            if (IsPickerPreviewBusy())
            {
                return;
            }

            if (_exportRequested && !_running)
            {
                _exportRequested = false;
                StartCoroutine(ExportBatch());
                return;
            }

            if (_pickerAction != PickerAction.None && !_running)
            {
                ProcessPickerAction();
                return;
            }

            if (_running)
            {
                return;
            }

            if (Input.GetKeyDown(KeyCode.F7))
            {
                _running = true;
                try
                {
                    Config.Reload();
                    string catalogDirectory = WriteSelectionCatalogs();
                    Logger.LogInfo("Selection catalogs written to " + catalogDirectory);
                    ShowViewerMessage("UmaPetForge catalogs ready", UIMessageType.Success);
                }
                catch (Exception exception)
                {
                    Logger.LogError(exception);
                    ShowViewerMessage("UmaPetForge catalog failed — check log", UIMessageType.Error);
                }
                finally
                {
                    _running = false;
                }
                return;
            }

            if (Input.GetKeyDown(KeyCode.F8))
            {
                RestorePickerFacePreview();
                if (_pickerOpen && !SavePickerSelection(true))
                {
                    return;
                }
                if (!_pickerOpen)
                {
                    Config.Reload();
                    if (ResolveRoster(_characters.Value).Count == 0)
                    {
                        ToggleCharacterPicker();
                        _pickerStatus = "Select at least one character before exporting.";
                        ShowViewerMessage(
                            "UmaPetForge: choose characters with F6",
                            UIMessageType.Default);
                        return;
                    }
                }
                _pickerOpen = false;
                StartCoroutine(ExportBatch());
            }
        }

        private void OnGUI()
        {
            if (!_viewerReady || !_pickerOpen)
            {
                return;
            }

            if (_pickerWindowRect.width <= 0f || _pickerWindowRect.height <= 0f)
            {
                ResetPickerWindowRect();
            }

            ClampPickerWindowRect();
            int previousDepth = GUI.depth;
            GUI.depth = -1000;
            try
            {
                _pickerWindowRect = GUI.Window(
                    GetInstanceID() ^ PickerWindowId,
                    _pickerWindowRect,
                    DrawCharacterPicker,
                    "UmaPetForge Pet Picker");
                ClampPickerWindowRect();
            }
            finally
            {
                GUI.depth = previousDepth;
            }
        }

        private void ToggleCharacterPicker()
        {
            if (_pickerOpen)
            {
                RestorePickerFacePreview();
                _pickerOpen = false;
                return;
            }

            try
            {
                Config.Reload();
                LoadPickerSelection();
                ResetPickerWindowRect();
                _pickerStatus = string.Empty;
                _pickerCostumeCharacterId = -1;
                _pickerCostumeSearch = string.Empty;
                _pickerMotionCharacterId = -1;
                _pickerMotionState = string.Empty;
                _pickerMotionSearch = string.Empty;
                _pickerFaceState = string.Empty;
                _pickerFaceDraft = null;
                _pickerFacePreviewMaterials = null;
                _pickerFacePreviewContainer = null;
                _pickerFacePreviewAttemptContainer = null;
                _pickerPreviewRequested = false;
                _pickerPreviewLoading = false;
                _pickerPreviewCharacterId = -1;
                _pickerPreviewCostumeId = string.Empty;
                _pickerPreviewMotion = null;
                _pickerPreviewError = string.Empty;
                _pickerOpen = true;
            }
            catch (Exception exception)
            {
                Logger.LogError(exception);
                ShowViewerMessage("UmaPetForge picker failed — check log", UIMessageType.Error);
            }
        }

        private void LoadPickerSelection()
        {
            _pickerCharacters = UmaViewerMain.Instance.Characters
                .Where(character => !character.IsMob)
                .OrderBy(character => character.Id)
                .ThenBy(character => character.GetName(), StringComparer.Ordinal)
                .ToList();
            _miniCostumeOptions = BuildMiniCostumeCatalog(_pickerCharacters);
            _pickerMiniMotions = GetMiniMotions();
            var availableIds = new HashSet<int>(_pickerCharacters.Select(character => character.Id));
            _pickerSelected.Clear();
            foreach (CharaEntry character in ResolveRoster(_characters.Value))
            {
                if (availableIds.Contains(character.Id))
                {
                    _pickerSelected.Add(character.Id);
                }
            }
            _pickerCostumes.Clear();
            foreach (KeyValuePair<int, string> choice in ParseCostumeSelections(_characterCostumes.Value))
            {
                if (availableIds.Contains(choice.Key))
                {
                    _pickerCostumes[choice.Key] = choice.Value;
                }
            }
            _pickerStateMotions.Clear();
            foreach (KeyValuePair<string, string> choice in
                     ParsePickerMotionSelections(_characterStateMotions.Value))
            {
                int characterId;
                string stateName;
                if (TryParsePickerMotionKey(choice.Key, out characterId, out stateName) &&
                    availableIds.Contains(characterId))
                {
                    _pickerStateMotions[choice.Key] = choice.Value;
                }
            }
            _pickerStateFaces.Clear();
            foreach (KeyValuePair<string, MiniFaceSelection> choice in
                     ParsePickerFaceSelections(_characterStateFaces.Value))
            {
                int characterId;
                string stateName;
                if (TryParsePickerMotionKey(choice.Key, out characterId, out stateName) &&
                    availableIds.Contains(characterId))
                {
                    _pickerStateFaces[choice.Key] = choice.Value.Clone();
                }
            }
        }

        private void ResetPickerWindowRect()
        {
            float width = Mathf.Min(640f, Mathf.Max(100f, Screen.width - 20f));
            float height = Mathf.Min(720f, Mathf.Max(140f, Screen.height - 40f));
            _pickerWindowRect = new Rect(
                Mathf.Max(10f, (Screen.width - width) * 0.5f),
                Mathf.Max(30f, (Screen.height - height) * 0.5f),
                width,
                height);
        }

        private void ClampPickerWindowRect()
        {
            float maxWidth = Mathf.Max(100f, Screen.width - 20f);
            float maxHeight = Mathf.Max(140f, Screen.height - 40f);
            _pickerWindowRect.width = Mathf.Min(_pickerWindowRect.width, maxWidth);
            _pickerWindowRect.height = Mathf.Min(_pickerWindowRect.height, maxHeight);
            _pickerWindowRect.x = Mathf.Clamp(
                _pickerWindowRect.x,
                0f,
                Mathf.Max(0f, Screen.width - _pickerWindowRect.width));
            _pickerWindowRect.y = Mathf.Clamp(
                _pickerWindowRect.y,
                0f,
                Mathf.Max(0f, Screen.height - _pickerWindowRect.height));
        }

        private void DrawCharacterPicker(int windowId)
        {
            if (_pickerCostumeCharacterId >= 0)
            {
                DrawCostumePicker();
                GUI.DragWindow(new Rect(0f, 0f, _pickerWindowRect.width, 24f));
                return;
            }
            if (_pickerMotionCharacterId >= 0)
            {
                bool pageEnabled = GUI.enabled;
                GUI.enabled = pageEnabled && !IsPickerPreviewBusy();
                if (!string.IsNullOrEmpty(_pickerFaceState))
                {
                    DrawMiniFacePicker();
                }
                else if (string.IsNullOrEmpty(_pickerMotionState))
                {
                    DrawStateAnimationsPicker();
                }
                else
                {
                    DrawMotionChoicePicker();
                }
                GUI.enabled = pageEnabled;
                GUI.DragWindow(new Rect(0f, 0f, _pickerWindowRect.width, 24f));
                return;
            }

            GUILayout.Space(4f);
            GUILayout.Label("Choose pets, clothes, interaction animations, and Mini faces exported by F8.");

            GUILayout.BeginHorizontal();
            GUILayout.Label("Search", GUILayout.Width(54f));
            _pickerSearch = GUILayout.TextField(_pickerSearch ?? string.Empty);
            if (GUILayout.Button("X", GUILayout.Width(30f)))
            {
                _pickerSearch = string.Empty;
            }
            GUILayout.EndHorizontal();

            List<CharaEntry> visible = _pickerCharacters
                .Where(CharacterMatchesPickerSearch)
                .ToList();
            GUILayout.Label(
                "Selected " + _pickerSelected.Count +
                " / " + _pickerCharacters.Count +
                " — showing " + visible.Count);

            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Select All"))
            {
                _pickerSelected.Clear();
                foreach (CharaEntry character in _pickerCharacters)
                {
                    _pickerSelected.Add(character.Id);
                }
                _pickerStatus =
                    "Selected all " + _pickerCharacters.Count +
                    " characters. A full export may take a long time.";
            }
            if (GUILayout.Button("Select Visible"))
            {
                foreach (CharaEntry character in visible)
                {
                    _pickerSelected.Add(character.Id);
                }
                _pickerStatus = visible.Count + " visible characters selected.";
            }
            if (GUILayout.Button("Clear"))
            {
                _pickerSelected.Clear();
                _pickerStatus = "Selection cleared.";
            }
            GUILayout.EndHorizontal();

            float listHeight = Mathf.Max(60f, _pickerWindowRect.height - 205f);
            _pickerScroll = GUILayout.BeginScrollView(
                _pickerScroll,
                GUI.skin.box,
                GUILayout.Height(listHeight));
            foreach (CharaEntry character in visible)
            {
                bool selected = _pickerSelected.Contains(character.Id);
                string label = character.Id.ToString(CultureInfo.InvariantCulture) +
                    "  " + character.GetName();
                GUILayout.BeginHorizontal();
                bool next = GUILayout.Toggle(selected, label);
                if (next != selected)
                {
                    if (next)
                    {
                        _pickerSelected.Add(character.Id);
                    }
                    else
                    {
                        _pickerSelected.Remove(character.Id);
                    }
                }
                if (next && GUILayout.Button(
                        GetPickerCostumeButtonLabel(character.Id),
                        GUILayout.Width(180f)))
                {
                    _pickerCostumeCharacterId = character.Id;
                    _pickerCostumeSearch = string.Empty;
                    _pickerCostumeScroll = Vector2.zero;
                }
                if (next && GUILayout.Button(
                        GetPickerAnimationsButtonLabel(character.Id),
                        GUILayout.Width(190f)))
                {
                    _pickerMotionCharacterId = character.Id;
                    _pickerMotionState = string.Empty;
                    _pickerFaceState = string.Empty;
                    _pickerMotionSearch = string.Empty;
                    _pickerMotionScroll = Vector2.zero;
                }
                GUILayout.EndHorizontal();
            }
            GUILayout.EndScrollView();

            if (!string.IsNullOrEmpty(_pickerStatus))
            {
                GUILayout.Label(_pickerStatus);
            }

            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Cancel"))
            {
                RestorePickerFacePreview();
                _pickerOpen = false;
            }

            if (GUILayout.Button("Save"))
            {
                _pickerAction = PickerAction.Save;
            }
            bool previousEnabled = GUI.enabled;
            GUI.enabled = _pickerSelected.Count > 0;
            if (GUILayout.Button("Save & Export"))
            {
                _pickerAction = PickerAction.SaveAndExport;
            }
            GUI.enabled = previousEnabled;
            GUILayout.EndHorizontal();

            GUI.DragWindow(new Rect(0f, 0f, _pickerWindowRect.width, 24f));
        }

        private void DrawCostumePicker()
        {
            CharaEntry character = _pickerCharacters.FirstOrDefault(
                candidate => candidate.Id == _pickerCostumeCharacterId);
            if (character == null)
            {
                _pickerCostumeCharacterId = -1;
                return;
            }

            GUILayout.Space(4f);
            if (GUILayout.Button("< Back to characters", GUILayout.Width(180f)))
            {
                _pickerCostumeCharacterId = -1;
                return;
            }

            GUILayout.Label(
                "Clothes for " + character.Id.ToString(CultureInfo.InvariantCulture) +
                "  " + character.GetName());
            GUILayout.Label("Current: " + GetPickerCostumeDisplayName(character.Id));

            GUILayout.BeginHorizontal();
            GUILayout.Label("Search", GUILayout.Width(54f));
            _pickerCostumeSearch = GUILayout.TextField(_pickerCostumeSearch ?? string.Empty);
            if (GUILayout.Button("X", GUILayout.Width(30f)))
            {
                _pickerCostumeSearch = string.Empty;
            }
            GUILayout.EndHorizontal();

            List<MiniCostumeOption> options;
            if (!_miniCostumeOptions.TryGetValue(character.Id, out options))
            {
                options = new List<MiniCostumeOption>();
            }
            string query = (_pickerCostumeSearch ?? string.Empty).Trim();
            List<MiniCostumeOption> visible = options
                .Where(option =>
                    string.IsNullOrEmpty(query) ||
                    option.Id.IndexOf(query, StringComparison.OrdinalIgnoreCase) >= 0 ||
                    option.DisplayName.IndexOf(query, StringComparison.OrdinalIgnoreCase) >= 0)
                .ToList();

            float listHeight = Mathf.Max(80f, _pickerWindowRect.height - 150f);
            _pickerCostumeScroll = GUILayout.BeginScrollView(
                _pickerCostumeScroll,
                GUI.skin.box,
                GUILayout.Height(listHeight));

            bool isAuto = !_pickerCostumes.ContainsKey(character.Id);
            if (GUILayout.Button((isAuto ? "[Selected] " : "") + "Auto / character default"))
            {
                _pickerCostumes.Remove(character.Id);
                _pickerStatus = character.GetName() + " clothes set to Auto.";
                _pickerCostumeCharacterId = -1;
            }
            foreach (MiniCostumeOption option in visible)
            {
                string selectedId;
                bool selected = _pickerCostumes.TryGetValue(character.Id, out selectedId) &&
                    string.Equals(selectedId, option.Id, StringComparison.Ordinal);
                string label = (selected ? "[Selected] " : "") +
                    option.DisplayName + "  [" + option.Id + "]";
                if (GUILayout.Button(label))
                {
                    _pickerCostumes[character.Id] = option.Id;
                    _pickerStatus = character.GetName() + " clothes: " + option.DisplayName + ".";
                    _pickerCostumeCharacterId = -1;
                }
            }
            GUILayout.EndScrollView();
        }

        private void DrawStateAnimationsPicker()
        {
            CharaEntry character = _pickerCharacters.FirstOrDefault(
                candidate => candidate.Id == _pickerMotionCharacterId);
            if (character == null)
            {
                _pickerMotionCharacterId = -1;
                _pickerMotionState = string.Empty;
                return;
            }

            GUILayout.Space(4f);
            if (GUILayout.Button("< Back to characters", GUILayout.Width(180f)))
            {
                RestorePickerFacePreview();
                _pickerMotionCharacterId = -1;
                _pickerMotionState = string.Empty;
                _pickerFaceState = string.Empty;
                return;
            }

            GUILayout.Label(
                "Animations for " + character.Id.ToString(CultureInfo.InvariantCulture) +
                "  " + character.GetName());
            GUILayout.Label(
                "Auto uses any advanced CSV override, then the current automatic default.");
            DrawPickerPreviewStatus(character);

            GUILayout.Space(8f);
            foreach (string stateName in PickerMotionStates)
            {
                GUILayout.BeginHorizontal();
                GUILayout.Label(GetPickerStateDisplayName(stateName), GUILayout.Width(130f));
                if (GUILayout.Button(
                        "Motion: " + ShortenLabel(
                            GetPickerMotionDisplayName(character.Id, stateName),
                            28)))
                {
                    _pickerMotionState = stateName;
                    _pickerFaceState = string.Empty;
                    _pickerMotionSearch = string.Empty;
                    _pickerMotionScroll = Vector2.zero;
                }
                if (GUILayout.Button(
                        "Face: " + GetPickerFaceDisplayName(character.Id, stateName),
                        GUILayout.Width(150f)))
                {
                    BeginPickerFaceEdit(character.Id, stateName);
                }
                GUILayout.EndHorizontal();
            }

            if (!string.IsNullOrEmpty(_pickerStatus))
            {
                GUILayout.Label(_pickerStatus);
            }

            GUILayout.FlexibleSpace();
            GUILayout.Label(
                "Tip: idle pins all dances, hover pins near05, and Character labels are that Uma's special clips.");
        }

        private void DrawMotionChoicePicker()
        {
            CharaEntry character = _pickerCharacters.FirstOrDefault(
                candidate => candidate.Id == _pickerMotionCharacterId);
            StateDefinition state = GetPickerStateDefinition(_pickerMotionState);
            if (character == null || state == null)
            {
                _pickerMotionCharacterId = -1;
                _pickerMotionState = string.Empty;
                return;
            }

            GUILayout.Space(4f);
            if (GUILayout.Button("< Back to animations", GUILayout.Width(180f)))
            {
                _pickerMotionState = string.Empty;
                _pickerMotionSearch = string.Empty;
                return;
            }

            GUILayout.Label(
                GetPickerStateDisplayName(state.Name) + " motion for " + character.GetName());
            GUILayout.Label("Current: " + GetPickerMotionDisplayName(character.Id, state.Name));
            DrawPickerPreviewStatus(character);

            GUILayout.BeginHorizontal();
            GUILayout.Label("Search", GUILayout.Width(54f));
            _pickerMotionSearch = GUILayout.TextField(_pickerMotionSearch ?? string.Empty);
            if (GUILayout.Button("X", GUILayout.Width(30f)))
            {
                _pickerMotionSearch = string.Empty;
                _pickerMotionScroll = Vector2.zero;
            }
            GUILayout.EndHorizontal();

            List<UmaDatabaseEntry> compatible = GetCompatibleMotions(
                _pickerMiniMotions,
                character.Id);
            string query = (_pickerMotionSearch ?? string.Empty).Trim();
            string selectionKey = MotionOverrideKey(
                character.Id.ToString(CultureInfo.InvariantCulture),
                state.Name);
            string selectedMotion;
            _pickerStateMotions.TryGetValue(selectionKey, out selectedMotion);

            List<UmaDatabaseEntry> visible;
            if (string.IsNullOrEmpty(query))
            {
                visible = BuildPickerRecommendedMotions(
                    compatible,
                    state,
                    character.Id,
                    selectedMotion);
                GUILayout.Label(
                    GetPickerRecommendationLabel(state, character) +
                    " — search to browse all " + compatible.Count + " compatible motions");
            }
            else
            {
                List<UmaDatabaseEntry> matches = compatible
                    .Where(entry => MotionMatchesPickerSearch(entry, query))
                    .OrderBy(entry => entry.Name, StringComparer.Ordinal)
                    .ToList();
                visible = matches.Take(160).ToList();
                GUILayout.Label(
                    "Showing " + visible.Count + " / " + matches.Count +
                    " matches" + (matches.Count > visible.Count ? " — refine search for more" : ""));
            }

            float listHeight = Mathf.Max(80f, _pickerWindowRect.height - 225f);
            _pickerMotionScroll = GUILayout.BeginScrollView(
                _pickerMotionScroll,
                GUI.skin.box,
                GUILayout.Height(listHeight));

            bool isAuto = string.IsNullOrEmpty(selectedMotion);
            if (GUILayout.Button((isAuto ? "[Selected] " : "") + "Auto / advanced CSV fallback"))
            {
                _pickerStateMotions.Remove(selectionKey);
                _pickerStatus = character.GetName() + " " + state.Name + " motion set to Auto.";
                _pickerMotionState = string.Empty;
            }
            foreach (UmaDatabaseEntry entry in visible)
            {
                string motionKey = entry.Key.ToString(CultureInfo.InvariantCulture);
                bool selected = string.Equals(
                    selectedMotion,
                    motionKey,
                    StringComparison.Ordinal);
                string label = (selected ? "[Selected] " : "") +
                    ShortenLabel(GetFriendlyMotionName(entry), 72);
                if (GUILayout.Button(label))
                {
                    _pickerStateMotions[selectionKey] = motionKey;
                    _pickerStatus = character.GetName() + " " + state.Name +
                        " motion: " + GetFriendlyMotionName(entry) + ".";
                    PreviewOrQueuePickerMotion(character, entry);
                }
            }
            GUILayout.EndScrollView();
        }

        private void BeginPickerFaceEdit(int characterId, string stateName)
        {
            RestorePickerFacePreview();
            _pickerMotionState = string.Empty;
            _pickerFaceState = stateName;
            string selectionKey = MotionOverrideKey(
                characterId.ToString(CultureInfo.InvariantCulture),
                stateName);
            MiniFaceSelection configured;
            if (_pickerStateFaces.TryGetValue(selectionKey, out configured))
            {
                _pickerFaceDraft = configured.Clone();
            }
            else
            {
                _pickerFaceDraft = null;
            }

            CharaEntry character = _pickerCharacters.FirstOrDefault(
                candidate => candidate.Id == characterId);
            if (character != null && IsMatchingPickerPreview(character))
            {
                EnsurePickerFacePreview(characterId);
                InitializePickerFaceDraftFromPreview();
                ApplyPickerFacePreview();
                return;
            }

            QueuePickerPreview(character, null);
        }

        private void DrawMiniFacePicker()
        {
            CharaEntry character = _pickerCharacters.FirstOrDefault(
                candidate => candidate.Id == _pickerMotionCharacterId);
            StateDefinition state = GetPickerStateDefinition(_pickerFaceState);
            if (character == null || state == null)
            {
                RestorePickerFacePreview();
                _pickerMotionCharacterId = -1;
                _pickerFaceState = string.Empty;
                return;
            }

            RefreshActivePickerFacePreview(character);

            GUILayout.Space(4f);
            if (GUILayout.Button("< Back to animations", GUILayout.Width(180f)))
            {
                RestorePickerFacePreview();
                _pickerFaceState = string.Empty;
                _pickerFaceDraft = null;
                return;
            }

            GUILayout.Label(
                GetPickerStateDisplayName(state.Name) + " face for " + character.GetName());
            GUILayout.Label(
                "Auto keeps the default face. Custom uses Mini texture slots for this state.");
            DrawPickerPreviewStatus(character, true);

            string selectionKey = MotionOverrideKey(
                character.Id.ToString(CultureInfo.InvariantCulture),
                state.Name);
            bool isAuto = !_pickerStateFaces.ContainsKey(selectionKey);
            if (GUILayout.Button((isAuto ? "[Selected] " : "") + "Auto / default face"))
            {
                _pickerStateFaces.Remove(selectionKey);
                RestorePickerFacePreview();
                _pickerStatus = character.GetName() + " " + state.Name + " face set to Auto.";
                _pickerFaceState = string.Empty;
                _pickerFaceDraft = null;
                return;
            }

            GUILayout.Space(8f);
            bool faceReady = _pickerFacePreviewMaterials != null && _pickerFaceDraft != null;
            if (faceReady)
            {
                bool changed = false;
                int nextValue = DrawMiniFaceIndex(
                    "Left eye",
                    _pickerFaceDraft.EyeLeft,
                    MiniFaceSelection.EyeMinimum,
                    MiniFaceSelection.EyeMaximum);
                if (nextValue != _pickerFaceDraft.EyeLeft)
                {
                    _pickerFaceDraft.EyeLeft = nextValue;
                    changed = true;
                }
                nextValue = DrawMiniFaceIndex(
                    "Right eye",
                    _pickerFaceDraft.EyeRight,
                    MiniFaceSelection.EyeMinimum,
                    MiniFaceSelection.EyeMaximum);
                if (nextValue != _pickerFaceDraft.EyeRight)
                {
                    _pickerFaceDraft.EyeRight = nextValue;
                    changed = true;
                }
                nextValue = DrawMiniFaceIndex(
                    "Mouth",
                    _pickerFaceDraft.Mouth,
                    MiniFaceSelection.MouthMinimum,
                    MiniFaceSelection.MouthMaximum);
                if (nextValue != _pickerFaceDraft.Mouth)
                {
                    _pickerFaceDraft.Mouth = nextValue;
                    changed = true;
                }
                nextValue = DrawMiniFaceIndex(
                    "Left eyebrow",
                    _pickerFaceDraft.EyebrowLeft,
                    MiniFaceSelection.EyebrowMinimum,
                    MiniFaceSelection.EyebrowMaximum);
                if (nextValue != _pickerFaceDraft.EyebrowLeft)
                {
                    _pickerFaceDraft.EyebrowLeft = nextValue;
                    changed = true;
                }
                nextValue = DrawMiniFaceIndex(
                    "Right eyebrow",
                    _pickerFaceDraft.EyebrowRight,
                    MiniFaceSelection.EyebrowMinimum,
                    MiniFaceSelection.EyebrowMaximum);
                if (nextValue != _pickerFaceDraft.EyebrowRight)
                {
                    _pickerFaceDraft.EyebrowRight = nextValue;
                    changed = true;
                }

                GUILayout.BeginHorizontal();
                if (GUILayout.Button("Match eyes"))
                {
                    _pickerFaceDraft.EyeRight = _pickerFaceDraft.EyeLeft;
                    changed = true;
                }
                if (GUILayout.Button("Match eyebrows"))
                {
                    _pickerFaceDraft.EyebrowRight = _pickerFaceDraft.EyebrowLeft;
                    changed = true;
                }
                GUILayout.EndHorizontal();

                if (changed || _pickerFacePreviewMaterials != null)
                {
                    ApplyPickerFacePreview();
                }

                GUILayout.Label("Face changes are previewing live on the loaded Mini.");
            }
            else if (IsPickerPreviewBusy())
            {
                GUILayout.Label("Face controls unlock as soon as the Mini preview finishes loading.");
            }
            else
            {
                GUILayout.Label("Face preview is unavailable. Use Retry preview above.");
            }

            GUILayout.FlexibleSpace();
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Cancel"))
            {
                RestorePickerFacePreview();
                _pickerFaceState = string.Empty;
                _pickerFaceDraft = null;
            }
            bool previousEnabled = GUI.enabled;
            GUI.enabled = previousEnabled && faceReady;
            if (GUILayout.Button("Save custom face"))
            {
                _pickerStateFaces[selectionKey] = _pickerFaceDraft.ClampedClone();
                ApplyPickerFacePreview();
                RestorePickerFacePreview();
                _pickerStatus = character.GetName() + " " + state.Name + " face saved.";
                _pickerFaceState = string.Empty;
                _pickerFaceDraft = null;
            }
            GUI.enabled = previousEnabled;
            GUILayout.EndHorizontal();
        }

        private static int DrawMiniFaceIndex(
            string label,
            int value,
            int minimum,
            int maximum)
        {
            GUILayout.BeginHorizontal();
            GUILayout.Label(label, GUILayout.Width(140f));
            if (GUILayout.Button("-", GUILayout.Width(44f)))
            {
                value = Mathf.Max(minimum, value - 1);
            }
            GUILayout.Label(
                value.ToString(CultureInfo.InvariantCulture) +
                " / " + maximum.ToString(CultureInfo.InvariantCulture),
                GUILayout.Width(90f));
            if (GUILayout.Button("+", GUILayout.Width(44f)))
            {
                value = Mathf.Min(maximum, value + 1);
            }
            GUILayout.EndHorizontal();
            return value;
        }

        private void EnsurePickerFacePreview(int characterId)
        {
            UmaContainerCharacter container = UmaViewerBuilder.Instance == null
                ? null
                : UmaViewerBuilder.Instance.CurrentUMAContainer;
            if (ReferenceEquals(container, _pickerFacePreviewContainer) &&
                _pickerFacePreviewMaterials != null)
            {
                return;
            }
            if (ReferenceEquals(container, _pickerFacePreviewAttemptContainer) &&
                _pickerFacePreviewMaterials == null)
            {
                return;
            }

            RestorePickerFacePreview();
            if (container == null ||
                !container.IsMini ||
                container.CharaEntry == null ||
                container.CharaEntry.Id != characterId)
            {
                return;
            }

            _pickerFacePreviewAttemptContainer = container;

            MiniFaceMaterials materials;
            string error;
            if (MiniFaceMaterials.TryCapture(container, out materials, out error))
            {
                _pickerFacePreviewContainer = container;
                _pickerFacePreviewMaterials = materials;
                _pickerPreviewError = string.Empty;
            }
            else
            {
                Logger.LogWarning(error);
                _pickerStatus = error;
                _pickerPreviewError = error;
            }
        }

        private void RefreshActivePickerFacePreview(CharaEntry character)
        {
            if (character == null || !IsMatchingPickerPreview(character))
            {
                RestorePickerFacePreview();
                return;
            }

            if (_pickerFacePreviewMaterials == null)
            {
                EnsurePickerFacePreview(character.Id);
                InitializePickerFaceDraftFromPreview();
                ApplyPickerFacePreview();
                return;
            }

            UmaContainerCharacter current = UmaViewerBuilder.Instance == null
                ? null
                : UmaViewerBuilder.Instance.CurrentUMAContainer;
            if (ReferenceEquals(current, _pickerFacePreviewContainer))
            {
                return;
            }

            RestorePickerFacePreview();
            EnsurePickerFacePreview(character.Id);
            InitializePickerFaceDraftFromPreview();
            ApplyPickerFacePreview();
        }

        private void InitializePickerFaceDraftFromPreview()
        {
            if (_pickerFaceDraft != null || _pickerFacePreviewMaterials == null)
            {
                return;
            }

            MiniFaceSelection current;
            string error;
            if (_pickerFacePreviewMaterials.TryReadCurrent(out current, out error))
            {
                _pickerFaceDraft = current;
                return;
            }

            Logger.LogWarning(error);
            _pickerStatus = error;
            _pickerPreviewError = error;
            RestorePickerFacePreview();
        }

        private void ApplyPickerFacePreview()
        {
            if (_pickerFacePreviewMaterials == null || _pickerFaceDraft == null)
            {
                return;
            }

            string error;
            if (!_pickerFacePreviewMaterials.TryApply(_pickerFaceDraft, out error))
            {
                Logger.LogWarning(error);
                _pickerStatus = error;
                _pickerPreviewError = error;
                RestorePickerFacePreview();
            }
        }

        private void RestorePickerFacePreview()
        {
            try
            {
                if (_pickerFacePreviewMaterials != null)
                {
                    _pickerFacePreviewMaterials.RestoreBaseline();
                }
            }
            catch (Exception exception)
            {
                Logger.LogDebug("Could not restore Mini face preview: " + exception.Message);
            }
            finally
            {
                _pickerFacePreviewMaterials = null;
                _pickerFacePreviewContainer = null;
                _pickerFacePreviewAttemptContainer = null;
            }
        }

        private bool IsPickerPreviewBusy()
        {
            return _pickerPreviewRequested || _pickerPreviewLoading;
        }

        private bool IsMatchingPickerPreview(CharaEntry character)
        {
            if (character == null)
            {
                return false;
            }

            string costumeId;
            try
            {
                costumeId = ResolvePickerPreviewCostume(character.Id);
            }
            catch
            {
                return false;
            }
            return IsMatchingMiniPreview(character.Id, costumeId);
        }

        private static bool IsMatchingMiniPreview(
            int characterId,
            string costumeId)
        {
            UmaContainerCharacter container = UmaViewerBuilder.Instance == null
                ? null
                : UmaViewerBuilder.Instance.CurrentUMAContainer;
            if (container == null ||
                !container.IsMini ||
                container.CharaEntry == null ||
                container.CharaEntry.Id != characterId ||
                container.UmaAnimator == null ||
                container.gameObject == null)
            {
                return false;
            }

            string prefix = "Chara_" +
                characterId.ToString(CultureInfo.InvariantCulture) + "_";
            string objectName = container.gameObject.name ?? string.Empty;
            return objectName.StartsWith(prefix, StringComparison.Ordinal) &&
                   string.Equals(
                       objectName.Substring(prefix.Length),
                       costumeId,
                       StringComparison.Ordinal);
        }

        private void DrawPickerPreviewStatus(
            CharaEntry character,
            bool requireFacePreview = false)
        {
            bool matching = IsMatchingPickerPreview(character);
            GUILayout.BeginHorizontal();
            if (IsPickerPreviewBusy())
            {
                GUILayout.Label(
                    "Loading " + character.GetName() + " Mini for live preview...");
                GUILayout.Button("Loading...", GUILayout.Width(190f));
            }
            else if (!string.IsNullOrEmpty(_pickerPreviewError) &&
                     (!matching || requireFacePreview))
            {
                GUILayout.Label("Preview failed: " + ShortenLabel(_pickerPreviewError, 64));
                if (GUILayout.Button("Retry preview", GUILayout.Width(190f)))
                {
                    QueuePickerPreview(character, null);
                }
            }
            else if (matching)
            {
                GUILayout.Label(
                    "Live preview ready: " + character.GetName() +
                    " Mini — drag this window aside to watch");
                if (GUILayout.Button("Reload selected clothes", GUILayout.Width(190f)))
                {
                    QueuePickerPreview(character, null);
                }
            }
            else
            {
                GUILayout.Label("Live preview model is not loaded.");
                if (GUILayout.Button("Load Mini for preview", GUILayout.Width(190f)))
                {
                    QueuePickerPreview(character, null);
                }
            }
            GUILayout.EndHorizontal();
        }

        private void PreviewOrQueuePickerMotion(
            CharaEntry character,
            UmaDatabaseEntry motion)
        {
            if (IsMatchingPickerPreview(character))
            {
                PreviewPickerMotion(character.Id, motion);
                return;
            }

            QueuePickerPreview(character, motion);
        }

        private void QueuePickerPreview(
            CharaEntry character,
            UmaDatabaseEntry motion)
        {
            if (character == null || IsPickerPreviewBusy())
            {
                return;
            }

            try
            {
                _pickerPreviewCharacterId = character.Id;
                _pickerPreviewCostumeId = ResolvePickerPreviewCostume(character.Id);
                _pickerPreviewMotion = motion;
                _pickerPreviewError = string.Empty;
                _pickerPreviewRequested = true;
                _pickerStatus = "Loading " + character.GetName() +
                    " Mini for live preview...";
            }
            catch (Exception exception)
            {
                _pickerPreviewError = exception.Message;
                _pickerStatus = "Preview failed: " + exception.Message;
                Logger.LogWarning(_pickerStatus);
            }
        }

        private IEnumerator LoadPickerPreview(
            int characterId,
            string costumeId,
            UmaDatabaseEntry motion)
        {
            yield return null;

            IEnumerator routine = LoadPickerPreviewCore(
                characterId,
                costumeId,
                motion);
            Exception failure = null;
            while (true)
            {
                bool moved;
                object current = null;
                try
                {
                    moved = routine.MoveNext();
                    if (moved)
                    {
                        current = routine.Current;
                    }
                }
                catch (Exception exception)
                {
                    failure = exception;
                    break;
                }

                if (!moved)
                {
                    break;
                }

                yield return current;
            }

            _pickerPreviewLoading = false;
            _pickerPreviewMotion = null;
            if (failure == null)
            {
                _pickerPreviewError = string.Empty;
                yield break;
            }

            _pickerPreviewError = failure.Message;
            _pickerStatus = "Preview failed: " + failure.Message;
            Logger.LogError("UmaPetForge Mini preview failed: " + failure);
            ShowViewerMessage("UmaPetForge preview failed — retry in F6", UIMessageType.Error);
        }

        private IEnumerator LoadPickerPreviewCore(
            int characterId,
            string costumeId,
            UmaDatabaseEntry motion)
        {
            CharaEntry character = _pickerCharacters.FirstOrDefault(
                candidate => candidate.Id == characterId);
            if (character == null)
            {
                throw new InvalidOperationException(
                    "The selected character is no longer available in UmaViewer.");
            }

            float assetLoadDeadline = Time.realtimeSinceStartup + 30f;
            while (UmaAssetManager.LoadCoroutine != null)
            {
                if (Time.realtimeSinceStartup >= assetLoadDeadline)
                {
                    throw new InvalidOperationException(
                        "UmaViewer was still loading other assets after 30 seconds.");
                }
                yield return null;
            }

            UmaViewerBuilder builder = UmaViewerBuilder.Instance;
            if (builder == null)
            {
                throw new InvalidOperationException("UmaViewer's character builder is unavailable.");
            }

            RestorePickerFacePreview();
            builder.UnloadUma();
            yield return null;

            assetLoadDeadline = Time.realtimeSinceStartup + 30f;
            while (UmaAssetManager.LoadCoroutine != null)
            {
                if (Time.realtimeSinceStartup >= assetLoadDeadline)
                {
                    throw new InvalidOperationException(
                        "UmaViewer started another asset load and did not finish within 30 seconds.");
                }
                yield return null;
            }

            IEnumerator loadRoutine = builder.LoadUma(character, costumeId, true);
            while (loadRoutine.MoveNext())
            {
                yield return loadRoutine.Current;
            }
            yield return new WaitForEndOfFrame();

            UmaContainerCharacter container = builder.CurrentUMAContainer;
            if (container == null ||
                !container.IsMini ||
                container.CharaEntry == null ||
                container.CharaEntry.Id != characterId ||
                container.UmaAnimator == null)
            {
                throw new InvalidOperationException(
                    "UmaViewer did not create a usable matching Mini model.");
            }
            if (!IsMatchingMiniPreview(characterId, costumeId))
            {
                throw new InvalidOperationException(
                    "UmaViewer loaded the character with different clothes than the F6 selection.");
            }

            if (motion != null)
            {
                container.LoadAnimation(motion);
            }

            if (_pickerOpen &&
                _pickerMotionCharacterId == characterId &&
                !string.IsNullOrEmpty(_pickerFaceState))
            {
                EnsurePickerFacePreview(characterId);
                InitializePickerFaceDraftFromPreview();
                ApplyPickerFacePreview();
                if (_pickerFacePreviewMaterials == null || _pickerFaceDraft == null)
                {
                    throw new InvalidOperationException(
                        string.IsNullOrEmpty(_pickerPreviewError)
                            ? "The Mini loaded, but its live face materials were unavailable."
                            : _pickerPreviewError);
                }
            }

            _pickerStatus = "Live preview ready: " + character.GetName() +
                " Mini" + (motion == null
                    ? "."
                    : " — " + GetFriendlyMotionName(motion) + ".");
            ShowViewerMessage("UmaPetForge Mini preview ready", UIMessageType.Success);
        }

        private string ResolvePickerPreviewCostume(int characterId)
        {
            List<MiniCostumeOption> options;
            if (!_miniCostumeOptions.TryGetValue(characterId, out options) ||
                options.Count == 0)
            {
                throw new InvalidOperationException(
                    "No usable Mini clothes were found for this character.");
            }

            string selected;
            if (_pickerCostumes.TryGetValue(characterId, out selected) &&
                options.Any(option =>
                    string.Equals(option.Id, selected, StringComparison.Ordinal)))
            {
                return selected;
            }

            MiniCostumeOption automatic = options.FirstOrDefault(option => option.Id == "00") ??
                options.FirstOrDefault(option => option.Id.Length < 4) ??
                options.FirstOrDefault();
            if (automatic == null)
            {
                throw new InvalidOperationException(
                    "No usable Mini clothes were found for this character.");
            }
            return automatic.Id;
        }

        private string GetPickerFaceDisplayName(int characterId, string stateName)
        {
            string selectionKey = MotionOverrideKey(
                characterId.ToString(CultureInfo.InvariantCulture),
                stateName);
            MiniFaceSelection selection;
            return _pickerStateFaces.TryGetValue(selectionKey, out selection)
                ? "Custom"
                : "Auto";
        }

        private string GetPickerAnimationsButtonLabel(int characterId)
        {
            string prefix = characterId.ToString(CultureInfo.InvariantCulture) + ":";
            int customCount =
                _pickerStateMotions.Keys.Count(key =>
                    key.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)) +
                _pickerStateFaces.Keys.Count(key =>
                    key.StartsWith(prefix, StringComparison.OrdinalIgnoreCase));
            return customCount == 0
                ? "Animations/Face: Auto"
                : "Animations/Face: " + customCount;
        }

        private string GetPickerMotionDisplayName(int characterId, string stateName)
        {
            string selectionKey = MotionOverrideKey(
                characterId.ToString(CultureInfo.InvariantCulture),
                stateName);
            string configured;
            if (!_pickerStateMotions.TryGetValue(selectionKey, out configured))
            {
                return "Auto";
            }

            UmaDatabaseEntry entry = ResolveConfiguredMotion(
                configured,
                GetCompatibleMotions(_pickerMiniMotions, characterId));
            return entry == null
                ? "Unavailable [" + configured + "]"
                : GetFriendlyMotionName(entry);
        }

        private void PreviewPickerMotion(int characterId, UmaDatabaseEntry entry)
        {
            UmaContainerCharacter container = UmaViewerBuilder.Instance == null
                ? null
                : UmaViewerBuilder.Instance.CurrentUMAContainer;
            if (container == null ||
                !container.IsMini ||
                container.CharaEntry == null ||
                container.CharaEntry.Id != characterId)
            {
                _pickerStatus += " Load the same character under Characters > Mini for live preview.";
                return;
            }

            try
            {
                container.LoadAnimation(entry);
                _pickerStatus += " Previewing on the loaded Mini.";
            }
            catch (Exception exception)
            {
                Logger.LogWarning("Could not preview picker motion: " + exception.Message);
                _pickerStatus += " Preview failed; the saved choice can still be exported.";
            }
        }

        private static string GetFriendlyMotionName(UmaDatabaseEntry entry)
        {
            string motionName = entry == null ? string.Empty : entry.Name;
            string fileName = Path.GetFileName(motionName) ?? string.Empty;
            string lower = fileName.ToLowerInvariant();
            string[] keywords =
            {
                "dance", "near", "run", "walk", "jump", "hop", "idle", "stand",
                "happy", "joy", "wave", "clap", "pose", "wait", "work",
                "study", "read", "look", "fail", "sad", "res", "mot",
                "act", "pdk", "kanban"
            };
            string[] labels =
            {
                "Dance", "Near", "Run", "Walk", "Jump", "Hop", "Idle", "Stand",
                "Happy", "Joy", "Wave", "Clap", "Pose", "Wait", "Work",
                "Study", "Read", "Look", "Failure", "Sad", "Special", "Special",
                "Special", "Special", "Sign pose"
            };

            for (int index = 0; index < keywords.Length; index++)
            {
                int marker = lower.IndexOf(keywords[index], StringComparison.Ordinal);
                if (marker < 0)
                {
                    continue;
                }

                int numberStart = marker + keywords[index].Length;
                int numberEnd = numberStart;
                while (numberEnd < lower.Length && char.IsDigit(lower[numberEnd]))
                {
                    numberEnd++;
                }
                string number = numberEnd > numberStart
                    ? " " + lower.Substring(numberStart, numberEnd - numberStart)
                    : string.Empty;
                string variant = GetCharacterMotionVariant(motionName);
                string loop = lower.Contains("loop") ? " (Loop)" : string.Empty;
                string scope = IsCharacterSpecificMotion(motionName)
                    ? " - Character"
                    : " - General";
                return labels[index] + number + variant + loop + scope;
            }

            string cleaned = lower
                .Replace("anm_min_", string.Empty)
                .Replace("anm_", string.Empty)
                .Replace("eve_", string.Empty)
                .Replace("type00_", string.Empty)
                .Replace("_loop", " (loop)")
                .Replace('_', ' ')
                .Trim();
            string fallback = string.IsNullOrEmpty(cleaned)
                ? "Unnamed motion"
                : CultureInfo.InvariantCulture.TextInfo.ToTitleCase(cleaned);
            return fallback + (IsCharacterSpecificMotion(motionName)
                ? " - Character"
                : " - General");
        }

        private static string GetCharacterMotionVariant(string motionName)
        {
            int characterId;
            if (!TryGetMotionCharacterId(motionName, out characterId))
            {
                return string.Empty;
            }

            string fileName = Path.GetFileName(motionName ?? string.Empty) ?? string.Empty;
            string marker = "chr" + characterId.ToString(CultureInfo.InvariantCulture) + "_";
            int markerIndex = fileName.IndexOf(marker, StringComparison.OrdinalIgnoreCase);
            if (markerIndex < 0)
            {
                return string.Empty;
            }

            int start = markerIndex + marker.Length;
            int end = start;
            while (end < fileName.Length && char.IsDigit(fileName[end]))
            {
                end++;
            }
            if (end == start || end - start > 2)
            {
                return string.Empty;
            }

            string variant = fileName.Substring(start, end - start);
            return string.Equals(variant, "00", StringComparison.Ordinal)
                ? string.Empty
                : " (Variant " + variant + ")";
        }

        private static StateDefinition GetPickerStateDefinition(string stateName)
        {
            return States.FirstOrDefault(state =>
                string.Equals(state.Name, stateName, StringComparison.OrdinalIgnoreCase));
        }

        private static string GetPickerStateDisplayName(string stateName)
        {
            switch (stateName)
            {
                case "idle":
                    return "Idle / resting";
                case "run_right":
                    return "Move right";
                case "run_left":
                    return "Move left";
                case "jump":
                    return "Cursor hover / jump";
                default:
                    return stateName;
            }
        }

        private static int GetPickerRecommendationPriority(
            StateDefinition state,
            UmaDatabaseEntry entry,
            int characterId)
        {
            string fileName = Path.GetFileName(entry.Name ?? string.Empty) ?? string.Empty;
            string lower = fileName.ToLowerInvariant();
            if (state.Name == "idle" && lower.Contains("dance"))
            {
                return 4;
            }
            if (state.Name == "jump" && lower.Contains("near05"))
            {
                return 4;
            }
            if (MotionMatchesCharacter(entry.Name, characterId))
            {
                return 3;
            }
            return ContainsAnyKeyword(state, entry.Name) ? 2 : 0;
        }

        private static List<UmaDatabaseEntry> BuildPickerRecommendedMotions(
            IEnumerable<UmaDatabaseEntry> compatible,
            StateDefinition state,
            int characterId,
            string selectedMotion)
        {
            List<UmaDatabaseEntry> candidates = compatible.ToList();
            List<UmaDatabaseEntry> pinned = candidates
                .Where(entry =>
                    MotionEntryMatchesConfigured(entry, selectedMotion) ||
                    GetPickerRecommendationPriority(state, entry, characterId) >= 3)
                .OrderByDescending(entry =>
                    MotionEntryMatchesConfigured(entry, selectedMotion))
                .ThenByDescending(entry =>
                    GetPickerRecommendationPriority(state, entry, characterId))
                .ThenByDescending(entry => ScoreMotion(state, entry.Name, characterId))
                .ThenBy(entry => entry.Name, StringComparer.Ordinal)
                .ToList();

            var visible = new List<UmaDatabaseEntry>();
            var seen = new HashSet<long>();
            foreach (UmaDatabaseEntry entry in pinned)
            {
                if (seen.Add(entry.Key))
                {
                    visible.Add(entry);
                }
            }

            int targetCount = Math.Max(80, visible.Count);
            foreach (UmaDatabaseEntry entry in candidates
                         .Where(candidate =>
                             GetPickerRecommendationPriority(
                                 state,
                                 candidate,
                                 characterId) == 2)
                         .OrderByDescending(candidate =>
                             ScoreMotion(state, candidate.Name, characterId))
                         .ThenBy(candidate => candidate.Name, StringComparer.Ordinal))
            {
                if (visible.Count >= targetCount)
                {
                    break;
                }
                if (seen.Add(entry.Key))
                {
                    visible.Add(entry);
                }
            }

            if (visible.Count > 0)
            {
                return visible;
            }
            return candidates.Take(80).ToList();
        }

        private static string GetPickerRecommendationLabel(
            StateDefinition state,
            CharaEntry character)
        {
            string characterName = character == null
                ? "character"
                : character.GetName();
            if (state.Name == "idle")
            {
                return "Recommended: all dances, " + characterName +
                    " specials, and idle motions";
            }
            if (state.Name == "jump")
            {
                return "Recommended: near05, " + characterName +
                    " specials, and jump motions";
            }
            return "Recommended: " + characterName +
                " character specials and matching motions";
        }

        private static bool MotionMatchesPickerSearch(UmaDatabaseEntry entry, string query)
        {
            string key = entry.Key.ToString(CultureInfo.InvariantCulture);
            string path = entry.Name ?? string.Empty;
            return path.IndexOf(query, StringComparison.OrdinalIgnoreCase) >= 0 ||
                   Path.GetFileName(path).IndexOf(query, StringComparison.OrdinalIgnoreCase) >= 0 ||
                   GetFriendlyMotionName(entry)
                       .IndexOf(query, StringComparison.OrdinalIgnoreCase) >= 0 ||
                   key.IndexOf(query, StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private static bool MotionEntryMatchesConfigured(
            UmaDatabaseEntry entry,
            string configured)
        {
            if (string.IsNullOrEmpty(configured))
            {
                return false;
            }

            return string.Equals(
                       entry.Key.ToString(CultureInfo.InvariantCulture),
                       configured,
                       StringComparison.Ordinal) ||
                   string.Equals(entry.Name, configured, StringComparison.OrdinalIgnoreCase);
        }

        private string GetPickerCostumeButtonLabel(int characterId)
        {
            return "Clothes: " + ShortenLabel(GetPickerCostumeDisplayName(characterId), 28);
        }

        private string GetPickerCostumeDisplayName(int characterId)
        {
            string costumeId;
            if (!_pickerCostumes.TryGetValue(characterId, out costumeId))
            {
                return "Auto";
            }

            List<MiniCostumeOption> options;
            MiniCostumeOption option = _miniCostumeOptions.TryGetValue(characterId, out options)
                ? options.FirstOrDefault(candidate =>
                    string.Equals(candidate.Id, costumeId, StringComparison.Ordinal))
                : null;
            return option == null ? costumeId : option.DisplayName;
        }

        private static string ShortenLabel(string value, int maximumLength)
        {
            value = value ?? string.Empty;
            return value.Length <= maximumLength
                ? value
                : value.Substring(0, Mathf.Max(1, maximumLength - 3)) + "...";
        }

        private bool CharacterMatchesPickerSearch(CharaEntry character)
        {
            string query = (_pickerSearch ?? string.Empty).Trim();
            if (string.IsNullOrEmpty(query))
            {
                return true;
            }

            return character.Id.ToString(CultureInfo.InvariantCulture)
                       .IndexOf(query, StringComparison.OrdinalIgnoreCase) >= 0 ||
                   (character.GetName() ?? string.Empty)
                       .IndexOf(query, StringComparison.OrdinalIgnoreCase) >= 0 ||
                   (character.Name ?? string.Empty)
                       .IndexOf(query, StringComparison.OrdinalIgnoreCase) >= 0 ||
                   (character.EnName ?? string.Empty)
                       .IndexOf(query, StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private bool SavePickerSelection(bool requireCharacter)
        {
            if (requireCharacter && _pickerSelected.Count == 0)
            {
                _pickerStatus = "Select at least one character.";
                ShowViewerMessage("UmaPetForge: select at least one character", UIMessageType.Error);
                return false;
            }

            string value = string.Join(
                ", ",
                _pickerSelected
                    .OrderBy(characterId => characterId)
                    .Select(characterId => characterId.ToString(CultureInfo.InvariantCulture))
                    .ToArray());
            _characters.Value = value;
            var savedCostumes = new List<string>();
            foreach (KeyValuePair<int, string> choice in _pickerCostumes.OrderBy(pair => pair.Key))
            {
                List<MiniCostumeOption> options;
                if (_miniCostumeOptions.TryGetValue(choice.Key, out options) &&
                    options.Any(option =>
                        string.Equals(option.Id, choice.Value, StringComparison.Ordinal)))
                {
                    savedCostumes.Add(
                        choice.Key.ToString(CultureInfo.InvariantCulture) + "=" + choice.Value);
                }
                else
                {
                    Logger.LogWarning(
                        "Discarded unavailable picker costume " + choice.Key + "=" + choice.Value);
                }
            }
            _characterCostumes.Value = string.Join(";", savedCostumes.ToArray());

            var savedMotions = new List<string>();
            foreach (KeyValuePair<string, string> choice in _pickerStateMotions
                         .OrderBy(pair => pair.Key, StringComparer.OrdinalIgnoreCase))
            {
                int characterId;
                string stateName;
                CharaEntry character;
                UmaDatabaseEntry motion = null;
                if (TryParsePickerMotionKey(choice.Key, out characterId, out stateName))
                {
                    character = _pickerCharacters.FirstOrDefault(candidate =>
                        candidate.Id == characterId);
                    if (character != null)
                    {
                        motion = ResolveConfiguredMotion(
                            choice.Value,
                            GetCompatibleMotions(_pickerMiniMotions, characterId));
                    }
                }

                if (motion != null)
                {
                    savedMotions.Add(
                        MotionOverrideKey(
                            characterId.ToString(CultureInfo.InvariantCulture),
                            stateName) + "=" +
                        motion.Key.ToString(CultureInfo.InvariantCulture));
                }
                else
                {
                    Logger.LogWarning(
                        "Discarded unavailable picker motion " + choice.Key + "=" + choice.Value);
                }
            }
            _characterStateMotions.Value = string.Join(";", savedMotions.ToArray());

            var savedFaces = new List<string>();
            foreach (KeyValuePair<string, MiniFaceSelection> choice in _pickerStateFaces
                         .OrderBy(pair => pair.Key, StringComparer.OrdinalIgnoreCase))
            {
                int characterId;
                string stateName;
                string error;
                bool valid = TryParsePickerMotionKey(
                        choice.Key,
                        out characterId,
                        out stateName) &&
                    _pickerCharacters.Any(character => character.Id == characterId) &&
                    choice.Value != null &&
                    choice.Value.TryValidate(out error);
                if (valid)
                {
                    savedFaces.Add(
                        MotionOverrideKey(
                            characterId.ToString(CultureInfo.InvariantCulture),
                            stateName) + "=" + SerializeMiniFaceSelection(choice.Value));
                }
                else
                {
                    Logger.LogWarning("Discarded invalid picker face " + choice.Key);
                }
            }
            _characterStateFaces.Value = string.Join(";", savedFaces.ToArray());

            _pickerStateMotions.Clear();
            foreach (KeyValuePair<string, string> saved in
                     ParsePickerMotionSelections(_characterStateMotions.Value))
            {
                _pickerStateMotions[saved.Key] = saved.Value;
            }
            _pickerStateFaces.Clear();
            foreach (KeyValuePair<string, MiniFaceSelection> saved in
                     ParsePickerFaceSelections(_characterStateFaces.Value))
            {
                _pickerStateFaces[saved.Key] = saved.Value.Clone();
            }
            Config.Save();
            _pickerStatus = "Saved " + _pickerSelected.Count + " characters.";
            Logger.LogInfo("Character picker saved: " + value);
            ShowViewerMessage(
                "UmaPetForge saved " + _pickerSelected.Count + " characters",
                UIMessageType.Success);
            return true;
        }

        private void ProcessPickerAction()
        {
            PickerAction action = _pickerAction;
            _pickerAction = PickerAction.None;
            try
            {
                if (!SavePickerSelection(action == PickerAction.SaveAndExport))
                {
                    return;
                }

                if (action == PickerAction.SaveAndExport)
                {
                    RestorePickerFacePreview();
                    _pickerOpen = false;
                    _exportRequested = true;
                }
            }
            catch (Exception exception)
            {
                Logger.LogError(exception);
                _pickerStatus = "Could not save selection — check log.";
                ShowViewerMessage("UmaPetForge picker save failed", UIMessageType.Error);
            }
        }

        private IEnumerator WaitForViewer()
        {
            while (UmaViewerMain.Instance == null ||
                   UmaViewerBuilder.Instance == null ||
                   UmaViewerUI.Instance == null ||
                   UmaViewerMain.Instance.Characters == null ||
                   UmaViewerMain.Instance.Characters.Count == 0 ||
                   UmaViewerMain.Instance.Costumes == null ||
                   UmaViewerMain.Instance.Costumes.Count == 0 ||
                   UmaViewerMain.Instance.AbList == null ||
                   UmaViewerMain.Instance.AbList.Count == 0 ||
                   UmaViewerMain.Instance.AbChara == null ||
                   UmaViewerMain.Instance.AbChara.Count == 0 ||
                   UmaViewerMain.Instance.AbMotions == null ||
                   UmaViewerBuilder.Instance.ShaderList == null ||
                   UmaViewerBuilder.Instance.ShaderList.Count == 0 ||
                   Camera.main == null)
            {
                yield return new WaitForSeconds(0.5f);
            }

            _viewerReady = true;
            Logger.LogInfo("UmaViewer is ready. Press F6 for the picker, F7 for catalogs, or F8 to export.");
            ShowViewerMessage("UmaPetForge ready — F6 picker / F7 catalogs / F8 export", UIMessageType.Success);
        }

        private IEnumerator ExportBatch()
        {
            _running = true;
            IEnumerator routine = ExportBatchCore();
            Exception failure = null;

            while (true)
            {
                bool moved;
                object current = null;
                try
                {
                    moved = routine.MoveNext();
                    if (moved)
                    {
                        current = routine.Current;
                    }
                }
                catch (Exception exception)
                {
                    failure = exception;
                    break;
                }

                if (!moved)
                {
                    break;
                }

                yield return current;
            }

            if (failure != null)
            {
                Logger.LogError(failure);
                ShowViewerMessage("UmaPetForge failed — check BepInEx log", UIMessageType.Error);
            }

            _running = false;
        }

        private IEnumerator ExportBatchCore()
        {
            string runRoot = null;
            CameraState cameraState = null;
            var results = new List<CharacterResult>();

            try
            {
                Config.Reload();
                WriteSelectionCatalogs();
                _motionOverrides = LoadMotionOverrides();
                _configuredPickerMotions = ParsePickerMotionSelections(
                    _characterStateMotions.Value);
                _configuredPickerFaces = ParsePickerFaceSelections(
                    _characterStateFaces.Value);
                _configuredCostumes = ParseCostumeSelections(_characterCostumes.Value);
                _miniCostumeOptions = BuildMiniCostumeCatalog(
                    UmaViewerMain.Instance.Characters.Where(character => !character.IsMob));
                List<UmaDatabaseEntry> allMiniMotions = GetMiniMotions();
                List<CharaEntry> roster = ResolveRoster(_characters.Value);
                if (roster.Count == 0)
                {
                    throw new InvalidOperationException("No configured character matched UmaViewer's character database.");
                }
                ValidateMotionOverrides(roster, allMiniMotions);

                runRoot = CreateRunDirectory();
                WriteAnimationCatalog(Path.Combine(runRoot, "mini-animation-catalog.json"), allMiniMotions);

                cameraState = CameraState.Capture(Camera.main, UmaViewerBuilder.Instance);
                cameraState.PrepareForExport();
                Logger.LogInfo("Exporting " + roster.Count + " characters to " + runRoot);
                ShowViewerMessage("UmaPetForge export started", UIMessageType.Default);

                foreach (CharaEntry character in roster)
                {
                    CharacterResult result = new CharacterResult(character);
                    results.Add(result);
                    yield return ExportCharacter(runRoot, character, allMiniMotions, result);
                }

                WriteExportManifest(Path.Combine(runRoot, "export-manifest.json"), results);
                File.WriteAllText(
                    Path.Combine(runRoot, "EXPORT_COMPLETE.txt"),
                    "UmaPetForge completed at " + DateTimeOffset.Now.ToString("O", CultureInfo.InvariantCulture) +
                    Environment.NewLine + "Characters attempted: " + results.Count + Environment.NewLine,
                    new UTF8Encoding(false));

                int succeeded = results.Count(result => result.Success);
                Logger.LogInfo("Export complete: " + succeeded + "/" + results.Count + " characters succeeded. " + runRoot);
                ShowViewerMessage("UmaPetForge complete: " + succeeded + "/" + results.Count, UIMessageType.Success);
            }
            finally
            {
                if (cameraState != null)
                {
                    cameraState.Restore();
                }
            }
        }

        private IEnumerator ExportCharacter(
            string runRoot,
            CharaEntry character,
            List<UmaDatabaseEntry> allMiniMotions,
            CharacterResult result)
        {
            IEnumerator routine = ExportCharacterCore(runRoot, character, allMiniMotions, result);
            while (true)
            {
                bool moved;
                object current = null;
                try
                {
                    moved = routine.MoveNext();
                    if (moved)
                    {
                        current = routine.Current;
                    }
                }
                catch (Exception exception)
                {
                    result.Error = exception.Message;
                    Logger.LogError("Failed " + character.GetName() + ": " + exception);
                    yield break;
                }

                if (!moved)
                {
                    yield break;
                }

                yield return current;
            }
        }

        private IEnumerator ExportCharacterCore(
            string runRoot,
            CharaEntry character,
            List<UmaDatabaseEntry> allMiniMotions,
            CharacterResult result)
        {
            var builder = UmaViewerBuilder.Instance;
            Texture2D atlas = null;
            MiniFaceMaterials faceMaterials = null;

            try
            {
                Logger.LogInfo("Loading Mini character " + character.Id + " " + character.GetName());
                builder.UnloadUma();
                yield return null;
                UmaAssetManager.UnloadAllBundle(false);
                yield return Resources.UnloadUnusedAssets();

                string costumeId = ResolveMiniCostume(character.Id, result.Warnings);
                IEnumerator loadRoutine = builder.LoadUma(character, costumeId, true);
                while (loadRoutine.MoveNext())
                {
                    yield return loadRoutine.Current;
                }
                yield return null;

                UmaContainerCharacter container = builder.CurrentUMAContainer;
                if (container == null || container.UmaAnimator == null)
                {
                    throw new InvalidOperationException("UmaViewer did not create a usable Mini model.");
                }

                List<UmaDatabaseEntry> compatible = GetCompatibleMotions(allMiniMotions, character.Id);
                var resolved = new Dictionary<string, MotionResolution>(StringComparer.Ordinal);
                foreach (StateDefinition state in States)
                {
                    resolved[state.Name] = ResolveMotion(
                        state,
                        compatible,
                        character.Id,
                        result.Warnings);
                }

                var resolvedFaces = new Dictionary<string, MiniFaceSelection>(
                    StringComparer.OrdinalIgnoreCase);
                foreach (StateDefinition state in States)
                {
                    string faceKey = MotionOverrideKey(
                        character.Id.ToString(CultureInfo.InvariantCulture),
                        state.Name);
                    MiniFaceSelection faceSelection;
                    if (_configuredPickerFaces.TryGetValue(faceKey, out faceSelection))
                    {
                        resolvedFaces[state.Name] = faceSelection.Clone();
                    }
                }

                if (resolvedFaces.Count > 0)
                {
                    string faceError;
                    if (!MiniFaceMaterials.TryCapture(
                            container,
                            out faceMaterials,
                            out faceError))
                    {
                        result.Warnings.Add(faceError + " All configured faces fell back to Auto.");
                        resolvedFaces.Clear();
                    }
                }

                string petFolderName = PetSlug(character.GetName());
                string characterDirectory = Path.Combine(runRoot, petFolderName);
                if (Directory.Exists(characterDirectory))
                {
                    petFolderName += "-" + character.Id.ToString(CultureInfo.InvariantCulture);
                    characterDirectory = Path.Combine(runRoot, petFolderName);
                }
                Directory.CreateDirectory(characterDirectory);

                CameraFraming framing = FrameCameraForCharacter(Camera.main, container);
                IEnumerator calibrationRoutine = CalibrateCamera(
                    Camera.main,
                    container,
                    resolved,
                    framing,
                    result);
                while (calibrationRoutine.MoveNext())
                {
                    yield return calibrationRoutine.Current;
                }
                atlas = CreateTransparentTexture(CellWidth * Columns, CellHeight * Rows);

                for (int row = 0; row < States.Length; row++)
                {
                    StateDefinition state = States[row];
                    MotionResolution resolution = resolved[state.Name];
                    if (resolution.Entry == null)
                    {
                        result.Warnings.Add(state.Name + ": no compatible motion was available");
                        continue;
                    }

                    if (resolution.Fallback)
                    {
                        result.Warnings.Add(state.Name + ": fell back to " + resolution.Entry.Name);
                    }

                    string framesDirectory = Path.Combine(characterDirectory, "frames", state.Name);
                    if (_writeIndividualFrames.Value)
                    {
                        Directory.CreateDirectory(framesDirectory);
                    }

                    if (faceMaterials != null)
                    {
                        faceMaterials.RestoreBaseline();
                    }
                    container.transform.localRotation = Quaternion.Euler(0f, state.YawDegrees, 0f);
                    container.LoadAnimation(resolution.Entry);
                    yield return null;

                    AnimationClip clip = container.OverrideController["clip_2"];
                    if (clip == null)
                    {
                        result.Warnings.Add(state.Name + ": selected asset did not resolve to an AnimationClip");
                        continue;
                    }

                    container.UmaAnimator.applyRootMotion = false;
                    container.UmaAnimator.SetLayerWeight(2, 0f);
                    container.UmaAnimator.speed = 0f;

                    for (int frame = 0; frame < state.FrameCount; frame++)
                    {
                        bool loop = state.PreferLoop || clip.isLooping;
                        float normalizedTime = GetSampleTime(frame, state.FrameCount, loop);
                        Texture2D captured = null;
                        float[] recoveryOffsets = { 0f, -0.025f, 0.025f, -0.06f, 0.06f, -0.1f, 0.1f };
                        for (int attempt = 0; attempt < recoveryOffsets.Length; attempt++)
                        {
                            float recoveryTime = AdjustSampleTime(normalizedTime + recoveryOffsets[attempt], loop);
                            if (faceMaterials != null)
                            {
                                faceMaterials.RestoreBaseline();
                            }
                            SeekAnimator(container, recoveryTime);
                            yield return new WaitForEndOfFrame();

                            MiniFaceSelection stateFace;
                            if (faceMaterials != null &&
                                resolvedFaces.TryGetValue(state.Name, out stateFace))
                            {
                                string faceError;
                                if (!faceMaterials.TryApply(stateFace, out faceError))
                                {
                                    result.Warnings.Add(
                                        state.Name + ": " + faceError + " Falling back to Auto.");
                                    resolvedFaces.Remove(state.Name);
                                }
                            }

                            Texture2D candidate = CaptureFrame(Camera.main);
                            if (HasVisiblePixels(candidate))
                            {
                                captured = candidate;
                                if (attempt > 0)
                                {
                                    result.Warnings.Add(
                                        state.Name + " frame " + frame +
                                        ": recovered transparent sample at " +
                                        recoveryTime.ToString("0.000", CultureInfo.InvariantCulture));
                                }
                                break;
                            }

                            Destroy(candidate);
                        }

                        if (captured == null)
                        {
                            throw new InvalidOperationException(
                                state.Name + " frame " + frame + " remained fully transparent after recovery attempts.");
                        }

                        try
                        {
                            PixelBounds capturedBounds = GetVisibleBounds(captured);
                            if (capturedBounds.TouchesEdge(3, CellWidth, CellHeight))
                            {
                                throw new InvalidOperationException(
                                    state.Name + " frame " + frame +
                                    " touches a cell edge after camera calibration: " +
                                    capturedBounds.ToString());
                            }

                            int spriteIndex = StateSpriteIndices[row][frame];
                            int atlasColumn = spriteIndex % Columns;
                            int atlasRow = spriteIndex / Columns;
                            int atlasY = (Rows - atlasRow - 1) * CellHeight;
                            atlas.SetPixels32(
                                atlasColumn * CellWidth,
                                atlasY,
                                CellWidth,
                                CellHeight,
                                captured.GetPixels32());

                            if (_writeIndividualFrames.Value)
                            {
                                string framePath = Path.Combine(
                                    framesDirectory,
                                    frame.ToString("00", CultureInfo.InvariantCulture) + ".png");
                                File.WriteAllBytes(framePath, ImageConversion.EncodeToPNG(captured));
                            }
                        }
                        finally
                        {
                            Destroy(captured);
                        }
                    }
                }

                if (faceMaterials != null)
                {
                    faceMaterials.RestoreBaseline();
                }
                container.transform.localRotation = Quaternion.identity;
                atlas.Apply(false, false);
                ValidateAtlas(atlas);
                string atlasPath = Path.Combine(characterDirectory, "atlas.png");
                File.WriteAllBytes(atlasPath, ImageConversion.EncodeToPNG(atlas));
                WriteCharacterManifest(
                    Path.Combine(characterDirectory, "resolved-clips.json"),
                    character,
                    costumeId,
                    resolved,
                    resolvedFaces,
                    result.Warnings);
                WriteCodexPetManifest(
                    Path.Combine(characterDirectory, "pet.json"),
                    character,
                    petFolderName);

                result.Success = true;
                result.OutputDirectory = characterDirectory;
                Logger.LogInfo("Exported " + character.GetName() + " -> " + atlasPath);
            }
            finally
            {
                if (atlas != null)
                {
                    Destroy(atlas);
                }

                if (builder.CurrentUMAContainer != null)
                {
                    if (faceMaterials != null)
                    {
                        faceMaterials.RestoreBaseline();
                    }
                    builder.CurrentUMAContainer.transform.localRotation = Quaternion.identity;
                }
            }
        }

        private static void SeekAnimator(UmaContainerCharacter container, float normalizedTime)
        {
            Animator animator = container.UmaAnimator;
            animator.speed = 0f;
            animator.Play("motion_2", 0, normalizedTime);
            animator.Update(0f);

            if (container.UmaFaceAnimator != null)
            {
                container.UmaFaceAnimator.speed = 0f;
                container.UmaFaceAnimator.Play(0, 0, normalizedTime);
                container.UmaFaceAnimator.Update(0f);
            }
        }

        private static float GetSampleTime(int frame, int frameCount, bool loop)
        {
            if (frameCount <= 1)
            {
                return 0f;
            }

            if (loop)
            {
                return frame / (float)frameCount;
            }

            return Mathf.Min(0.98f, frame / (float)(frameCount - 1));
        }

        private static float AdjustSampleTime(float value, bool loop)
        {
            if (loop)
            {
                return Mathf.Repeat(value, 1f);
            }

            return Mathf.Clamp(value, 0f, 0.98f);
        }

        private List<CharaEntry> ResolveRoster(string configured)
        {
            var resolved = new List<CharaEntry>();
            var seen = new HashSet<int>();
            string[] requested = (configured ?? string.Empty)
                .Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries);

            foreach (string raw in requested)
            {
                string token = raw.Trim();
                CharaEntry match = null;
                int id;
                if (int.TryParse(token, NumberStyles.Integer, CultureInfo.InvariantCulture, out id))
                {
                    match = UmaViewerMain.Instance.Characters.FirstOrDefault(character => character.Id == id);
                }
                else
                {
                    string normalized = NormalizeName(token);
                    match = UmaViewerMain.Instance.Characters.FirstOrDefault(character =>
                        NormalizeName(character.GetName()) == normalized ||
                        NormalizeName(character.Name) == normalized ||
                        NormalizeName(character.EnName) == normalized);
                }

                if (match == null)
                {
                    Logger.LogWarning("Configured character was not found: " + token);
                    continue;
                }

                if (seen.Add(match.Id))
                {
                    resolved.Add(match);
                }
            }

            return resolved;
        }

        private string ResolveMiniCostume(int characterId, IList<string> warnings)
        {
            string configured;
            if (_configuredCostumes.TryGetValue(characterId, out configured))
            {
                List<MiniCostumeOption> options;
                bool available = _miniCostumeOptions.TryGetValue(characterId, out options) &&
                    options.Any(option =>
                        string.Equals(option.Id, configured, StringComparison.Ordinal));
                if (available)
                {
                    return configured;
                }

                string warning =
                    "clothes: saved Mini costume " + configured +
                    " is unavailable; using Auto";
                warnings.Add(warning);
                Logger.LogWarning(characterId + " " + warning);
            }

            List<MiniCostumeOption> automaticOptions;
            if (_miniCostumeOptions.TryGetValue(characterId, out automaticOptions))
            {
                MiniCostumeOption automatic = automaticOptions.FirstOrDefault(option => option.Id == "00") ??
                    automaticOptions.FirstOrDefault(option => option.Id.Length < 4) ??
                    automaticOptions.FirstOrDefault();
                if (automatic != null)
                {
                    return automatic.Id;
                }
            }

            throw new InvalidOperationException(
                "No usable Mini costume asset was found for character " + characterId + ".");
        }

        private static Dictionary<int, string> ParseCostumeSelections(string configured)
        {
            var result = new Dictionary<int, string>();
            string[] rows = (configured ?? string.Empty)
                .Split(new[] { ';' }, StringSplitOptions.RemoveEmptyEntries);
            foreach (string raw in rows)
            {
                string row = raw.Trim();
                int separator = row.IndexOf('=');
                int characterId;
                string costumeId = separator < 0
                    ? string.Empty
                    : row.Substring(separator + 1).Trim();
                if (separator <= 0 ||
                    !int.TryParse(
                        row.Substring(0, separator).Trim(),
                        NumberStyles.Integer,
                        CultureInfo.InvariantCulture,
                        out characterId) ||
                    characterId <= 0 ||
                    !IsSafeCostumeId(costumeId))
                {
                    continue;
                }
                result[characterId] = costumeId;
            }
            return result;
        }

        private static Dictionary<string, string> ParsePickerMotionSelections(string configured)
        {
            var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            string[] rows = (configured ?? string.Empty)
                .Split(new[] { ';' }, StringSplitOptions.RemoveEmptyEntries);
            foreach (string raw in rows)
            {
                string row = raw.Trim();
                int separator = row.IndexOf('=');
                if (separator <= 0)
                {
                    continue;
                }

                int characterId;
                string stateName;
                long motionKey;
                if (!TryParsePickerMotionKey(
                        row.Substring(0, separator).Trim(),
                        out characterId,
                        out stateName) ||
                    !long.TryParse(
                        row.Substring(separator + 1).Trim(),
                        NumberStyles.Integer,
                        CultureInfo.InvariantCulture,
                        out motionKey))
                {
                    continue;
                }

                result[MotionOverrideKey(
                    characterId.ToString(CultureInfo.InvariantCulture),
                    stateName)] = motionKey.ToString(CultureInfo.InvariantCulture);
            }
            return result;
        }

        private static Dictionary<string, MiniFaceSelection> ParsePickerFaceSelections(
            string configured)
        {
            var result = new Dictionary<string, MiniFaceSelection>(
                StringComparer.OrdinalIgnoreCase);
            string[] rows = (configured ?? string.Empty)
                .Split(new[] { ';' }, StringSplitOptions.RemoveEmptyEntries);
            foreach (string raw in rows)
            {
                string row = raw.Trim();
                int separator = row.IndexOf('=');
                int characterId;
                string stateName;
                MiniFaceSelection selection;
                if (separator <= 0 ||
                    !TryParsePickerMotionKey(
                        row.Substring(0, separator).Trim(),
                        out characterId,
                        out stateName) ||
                    !TryParseMiniFaceSelection(
                        row.Substring(separator + 1).Trim(),
                        out selection))
                {
                    continue;
                }

                result[MotionOverrideKey(
                    characterId.ToString(CultureInfo.InvariantCulture),
                    stateName)] = selection;
            }
            return result;
        }

        private static bool TryParseMiniFaceSelection(
            string configured,
            out MiniFaceSelection selection)
        {
            selection = null;
            string[] values = (configured ?? string.Empty).Split(',');
            if (values.Length != 5)
            {
                return false;
            }

            var parsed = new int[5];
            for (int index = 0; index < values.Length; index++)
            {
                if (!int.TryParse(
                        values[index].Trim(),
                        NumberStyles.Integer,
                        CultureInfo.InvariantCulture,
                        out parsed[index]))
                {
                    return false;
                }
            }

            var candidate = new MiniFaceSelection(
                parsed[0], parsed[1], parsed[2], parsed[3], parsed[4]);
            string error;
            if (!candidate.TryValidate(out error))
            {
                return false;
            }

            selection = candidate;
            return true;
        }

        private static string SerializeMiniFaceSelection(MiniFaceSelection selection)
        {
            return selection.EyeLeft.ToString(CultureInfo.InvariantCulture) + "," +
                   selection.EyeRight.ToString(CultureInfo.InvariantCulture) + "," +
                   selection.Mouth.ToString(CultureInfo.InvariantCulture) + "," +
                   selection.EyebrowLeft.ToString(CultureInfo.InvariantCulture) + "," +
                   selection.EyebrowRight.ToString(CultureInfo.InvariantCulture);
        }

        private static bool TryParsePickerMotionKey(
            string value,
            out int characterId,
            out string stateName)
        {
            characterId = 0;
            stateName = string.Empty;
            string input = (value ?? string.Empty).Trim();
            int separator = input.IndexOf(':');
            if (separator <= 0 ||
                !int.TryParse(
                    input.Substring(0, separator).Trim(),
                    NumberStyles.Integer,
                    CultureInfo.InvariantCulture,
                    out characterId) ||
                characterId <= 0)
            {
                return false;
            }

            string requestedState = input.Substring(separator + 1).Trim();
            stateName = PickerMotionStates.FirstOrDefault(candidate =>
                string.Equals(candidate, requestedState, StringComparison.OrdinalIgnoreCase));
            return !string.IsNullOrEmpty(stateName);
        }

        private static bool IsSafeCostumeId(string value)
        {
            if (string.IsNullOrEmpty(value))
            {
                return false;
            }
            foreach (char character in value)
            {
                if (!char.IsDigit(character) && character != '_')
                {
                    return false;
                }
            }
            return true;
        }

        private static Dictionary<int, List<MiniCostumeOption>> BuildMiniCostumeCatalog(
            IEnumerable<CharaEntry> characters)
        {
            List<CharaEntry> characterList = characters.ToList();
            var characterIds = new HashSet<int>(characterList.Select(character => character.Id));
            var specific = characterList.ToDictionary(
                character => character.Id,
                character => new HashSet<string>(StringComparer.Ordinal));
            var generic = new HashSet<string>(StringComparer.Ordinal);

            foreach (UmaDatabaseEntry entry in UmaViewerMain.Instance.AbChara)
            {
                string name = entry.Name ?? string.Empty;
                if (!name.StartsWith("3d/chara/mini/body/", StringComparison.OrdinalIgnoreCase) ||
                    name.IndexOf("/clothes/", StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    continue;
                }

                string fileName = Path.GetFileName(name);
                if (!fileName.StartsWith("pfb_mbdy", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }
                string[] parts = fileName.Split(new[] { '_' }, StringSplitOptions.RemoveEmptyEntries);
                if (fileName.StartsWith("pfb_mbdy0", StringComparison.OrdinalIgnoreCase))
                {
                    if (parts.Length >= 4 && parts[1].Length > 4)
                    {
                        generic.Add(
                            parts[1].Substring(4) + "_" + parts[2] + "_" + parts[3]);
                    }
                    continue;
                }

                if (parts.Length < 3 || parts[1].Length <= 4)
                {
                    continue;
                }
                int ownerId;
                if (int.TryParse(
                        parts[1].Substring(4),
                        NumberStyles.None,
                        CultureInfo.InvariantCulture,
                        out ownerId) &&
                    characterIds.Contains(ownerId))
                {
                    specific[ownerId].Add(parts[parts.Length - 1]);
                }
            }

            var result = new Dictionary<int, List<MiniCostumeOption>>();
            foreach (CharaEntry character in characterList)
            {
                var ids = new HashSet<string>(specific[character.Id], StringComparer.Ordinal);
                ids.UnionWith(generic);
                result[character.Id] = ids
                    .Where(costumeId => IsMiniCostumeAvailable(character.Id, costumeId))
                    .Select(costumeId => new MiniCostumeOption(
                        costumeId,
                        GetMiniCostumeDisplayName(character.Id, costumeId)))
                    .OrderBy(option => option.Id == "00" ? 0 : option.Id.Length < 4 ? 1 : 2)
                    .ThenBy(option => option.DisplayName, StringComparer.OrdinalIgnoreCase)
                    .ThenBy(option => option.Id, StringComparer.Ordinal)
                    .ToList();
            }
            return result;
        }

        private static bool IsMiniCostumeAvailable(int characterId, string costumeId)
        {
            string key;
            if (costumeId.Length >= 4)
            {
                int separator = costumeId.LastIndexOf('_');
                if (separator <= 0)
                {
                    return false;
                }
                string folderId = costumeId.Substring(0, separator);
                key = "3d/chara/mini/body/mbdy" + folderId +
                    "/pfb_mbdy" + costumeId + "_0";
            }
            else
            {
                key = "3d/chara/mini/body/mbdy" + characterId + "_" + costumeId +
                    "/pfb_mbdy" + characterId + "_" + costumeId;
            }
            return UmaViewerMain.Instance.AbList.ContainsKey(key);
        }

        private static string GetMiniCostumeDisplayName(int characterId, string costumeId)
        {
            CostumeEntry metadata = null;
            string[] parts = costumeId.Split('_');
            if (parts.Length >= 2)
            {
                int bodyType;
                int bodyTypeSub;
                if (int.TryParse(parts[0], out bodyType) &&
                    int.TryParse(parts[1], out bodyTypeSub))
                {
                    metadata = UmaViewerMain.Instance.Costumes.FirstOrDefault(costume =>
                        costume.BodyType == bodyType && costume.BodyTypeSub == bodyTypeSub);
                }
            }
            else
            {
                int bodyTypeSub;
                if (int.TryParse(costumeId, out bodyTypeSub))
                {
                    metadata = UmaViewerMain.Instance.Costumes.FirstOrDefault(costume =>
                        costume.CharaId == characterId && costume.BodyTypeSub == bodyTypeSub);
                }
            }

            string fallback = metadata != null && !string.IsNullOrEmpty(metadata.DressName)
                ? metadata.DressName
                : "Outfit " + costumeId;
            try
            {
                string translated = UmaViewerUI.GetCostumeName(costumeId, fallback);
                return string.IsNullOrEmpty(translated) ? fallback : translated;
            }
            catch
            {
                return fallback;
            }
        }

        private static List<UmaDatabaseEntry> GetMiniMotions()
        {
            return UmaViewerMain.Instance.AbMotions
                .Where(entry => IsMiniMotion(entry.Name))
                .OrderBy(entry => entry.Name, StringComparer.Ordinal)
                .ToList();
        }

        private static bool IsMiniMotion(string name)
        {
            if (string.IsNullOrEmpty(name))
            {
                return false;
            }

            string lower = name.ToLowerInvariant();
            return lower.StartsWith("3d/motion/mini/", StringComparison.Ordinal) &&
                   lower.Contains("/body/") &&
                   !lower.Contains("mirror") &&
                   !lower.Contains("facial") &&
                   !lower.Contains("_cam") &&
                   !lower.EndsWith("_s", StringComparison.Ordinal) &&
                   !lower.EndsWith("_e", StringComparison.Ordinal) &&
                   !lower.Contains("/tail") &&
                   !lower.EndsWith("_pos", StringComparison.Ordinal) &&
                   !lower.Contains("prop") &&
                   !lower.EndsWith("_pose", StringComparison.Ordinal) &&
                   !lower.Contains("_defaultmotion");
        }

        private static List<UmaDatabaseEntry> GetCompatibleMotions(
            IEnumerable<UmaDatabaseEntry> motions,
            int characterId)
        {
            return motions.Where(entry =>
            {
                int targetedCharacterId;
                if (TryGetMotionCharacterId(entry.Name, out targetedCharacterId))
                {
                    return targetedCharacterId == characterId;
                }
                return IsGeneralMotion(entry.Name);
            })
                .OrderBy(entry => entry.Name, StringComparer.Ordinal)
                .ToList();
        }

        private static bool MotionMatchesCharacter(string name, int characterId)
        {
            int motionCharacterId;
            return TryGetMotionCharacterId(name, out motionCharacterId) &&
                   motionCharacterId == characterId;
        }

        private static bool IsGeneralMotion(string name)
        {
            string lower = name.ToLowerInvariant();
            return lower.Contains("/type0") || lower.Contains("/type99") || lower.Contains("anm_sty_");
        }

        private static bool IsCharacterSpecificMotion(string name)
        {
            int characterId;
            return TryGetMotionCharacterId(name, out characterId);
        }

        private static bool TryGetMotionCharacterId(string name, out int characterId)
        {
            characterId = 0;
            if (string.IsNullOrEmpty(name))
            {
                return false;
            }

            for (int index = 0; index <= name.Length - 4; index++)
            {
                if ((index > 0 && char.IsLetterOrDigit(name[index - 1])) ||
                    !string.Equals(
                        name.Substring(index, 3),
                        "chr",
                        StringComparison.OrdinalIgnoreCase) ||
                    !char.IsDigit(name[index + 3]))
                {
                    continue;
                }

                int start = index + 3;
                int end = start;
                while (end < name.Length && char.IsDigit(name[end]))
                {
                    end++;
                }
                if (end - start < 4)
                {
                    continue;
                }

                return int.TryParse(
                    name.Substring(start, end - start),
                    NumberStyles.None,
                    CultureInfo.InvariantCulture,
                    out characterId);
            }
            return false;
        }

        private MotionResolution ResolveMotion(
            StateDefinition state,
            List<UmaDatabaseEntry> candidates,
            int characterId,
            IList<string> warnings)
        {
            string selectionKey = MotionOverrideKey(
                characterId.ToString(CultureInfo.InvariantCulture),
                state.Name);
            string pickerConfigured;
            if (_configuredPickerMotions.TryGetValue(selectionKey, out pickerConfigured))
            {
                UmaDatabaseEntry pickerMotion = ResolveConfiguredMotion(
                    pickerConfigured,
                    candidates);
                if (pickerMotion != null)
                {
                    return new MotionResolution(
                        pickerMotion,
                        0,
                        false,
                        pickerConfigured,
                        "picker_selection");
                }

                string pickerWarning = state.Name +
                    ": saved F6 motion is unavailable or incompatible: " +
                    pickerConfigured + "; falling back to CSV or automatic selection";
                warnings.Add(pickerWarning);
                Logger.LogWarning(characterId + " " + pickerWarning);
            }

            string configured;
            string configuredSource;
            TryGetMotionOverride(
                characterId,
                state.Name,
                out configured,
                out configuredSource);
            configured = (configured ?? string.Empty).Trim();
            if (!string.IsNullOrEmpty(configured) &&
                !string.Equals(configured, "auto", StringComparison.OrdinalIgnoreCase))
            {
                UmaDatabaseEntry overridden = ResolveConfiguredMotion(configured, candidates);
                if (overridden != null)
                {
                    return new MotionResolution(
                        overridden,
                        0,
                        false,
                        configured,
                        configuredSource);
                }

                string warning = state.Name +
                    ": configured motion was not found or was not compatible: " + configured;
                warnings.Add(warning);
                throw new InvalidOperationException(warning);
            }

            UmaDatabaseEntry best = null;
            int bestScore = int.MinValue;
            bool foundSemanticMatch = false;
            foreach (UmaDatabaseEntry candidate in candidates)
            {
                bool semanticMatch = ContainsAnyKeyword(state, candidate.Name);
                if (!semanticMatch)
                {
                    continue;
                }

                foundSemanticMatch = true;
                int score = ScoreMotion(state, candidate.Name, characterId);
                if (score > bestScore ||
                    (score == bestScore && best != null &&
                     string.CompareOrdinal(candidate.Name, best.Name) < 0))
                {
                    best = candidate;
                    bestScore = score;
                }
            }

            if (foundSemanticMatch && best != null)
            {
                return new MotionResolution(best, bestScore, false, null, "automatic");
            }

            UmaDatabaseEntry fallback = candidates.FirstOrDefault(candidate =>
                candidate.Name.IndexOf("idle01_loop", StringComparison.OrdinalIgnoreCase) >= 0 &&
                candidate.Name.IndexOf("chr" + characterId, StringComparison.OrdinalIgnoreCase) >= 0);
            if (fallback == null)
            {
                fallback = candidates.FirstOrDefault(candidate =>
                    candidate.Name.IndexOf("idle", StringComparison.OrdinalIgnoreCase) >= 0);
            }
            if (fallback == null)
            {
                fallback = candidates.FirstOrDefault();
            }

            return new MotionResolution(fallback, bestScore, true, null, "automatic");
        }

        private bool TryGetMotionOverride(int characterId, string state, out string configured)
        {
            string ignoredSource;
            return TryGetMotionOverride(
                characterId,
                state,
                out configured,
                out ignoredSource);
        }

        private bool TryGetMotionOverride(
            int characterId,
            string state,
            out string configured,
            out string source)
        {
            if (_motionOverrides.TryGetValue(
                    MotionOverrideKey(characterId.ToString(CultureInfo.InvariantCulture), state),
                    out configured))
            {
                source = "csv_exact";
                return true;
            }

            if (_motionOverrides.TryGetValue(MotionOverrideKey("*", state), out configured))
            {
                source = "csv_wildcard";
                return true;
            }

            configured = null;
            source = "automatic";
            return false;
        }

        private void ValidateMotionOverrides(
            IEnumerable<CharaEntry> roster,
            IEnumerable<UmaDatabaseEntry> allMiniMotions)
        {
            var failures = new List<string>();
            List<UmaDatabaseEntry> motions = allMiniMotions.ToList();
            foreach (CharaEntry character in roster)
            {
                List<UmaDatabaseEntry> compatible = GetCompatibleMotions(motions, character.Id);
                foreach (StateDefinition state in States)
                {
                    string pickerConfigured;
                    if (_configuredPickerMotions.TryGetValue(
                            MotionOverrideKey(
                                character.Id.ToString(CultureInfo.InvariantCulture),
                                state.Name),
                            out pickerConfigured) &&
                        ResolveConfiguredMotion(pickerConfigured, compatible) != null)
                    {
                        continue;
                    }

                    string configured;
                    if (!TryGetMotionOverride(character.Id, state.Name, out configured))
                    {
                        continue;
                    }

                    configured = (configured ?? string.Empty).Trim();
                    if (string.IsNullOrEmpty(configured) ||
                        string.Equals(configured, "auto", StringComparison.OrdinalIgnoreCase))
                    {
                        continue;
                    }

                    if (ResolveConfiguredMotion(configured, compatible) == null)
                    {
                        failures.Add(
                            character.Id.ToString(CultureInfo.InvariantCulture) + " " +
                            character.GetName() + " / " + state.Name + " = " + configured);
                    }
                }
            }

            if (failures.Count > 0)
            {
                throw new InvalidOperationException(
                    "Motion override preflight failed: " + string.Join("; ", failures.ToArray()) +
                    ". Choose a compatible key/path from the F7 catalog or use auto.");
            }
        }

        private static UmaDatabaseEntry ResolveConfiguredMotion(
            string configured,
            IEnumerable<UmaDatabaseEntry> candidates)
        {
            long key;
            if (long.TryParse(
                configured,
                NumberStyles.Integer,
                CultureInfo.InvariantCulture,
                out key))
            {
                return candidates.FirstOrDefault(candidate => candidate.Key == key);
            }

            return candidates.FirstOrDefault(candidate =>
                string.Equals(candidate.Name, configured, StringComparison.OrdinalIgnoreCase));
        }

        private static string MotionOverrideKey(string characterSelector, string state)
        {
            return characterSelector.Trim() + ":" + state.Trim();
        }

        private static bool ContainsAnyKeyword(StateDefinition state, string path)
        {
            string lower = path.ToLowerInvariant();
            for (int index = 0; index < state.Keywords.Length; index++)
            {
                if (lower.Contains(state.Keywords[index]))
                {
                    return true;
                }
            }

            return false;
        }

        private static int ScoreMotion(StateDefinition state, string path, int characterId)
        {
            string lower = path.ToLowerInvariant();
            int score = 0;
            for (int index = 0; index < state.Keywords.Length; index++)
            {
                if (lower.Contains(state.Keywords[index]))
                {
                    score += 100 - (index * 12);
                }
            }

            if (state.PreferLoop && lower.Contains("loop"))
            {
                score += 18;
            }
            if (!IsCharacterSpecificMotion(path) && IsGeneralMotion(path))
            {
                score += 8;
            }
            if (MotionMatchesCharacter(path, characterId))
            {
                score += 6;
            }

            if (state.Name.StartsWith("run_", StringComparison.Ordinal) &&
                (lower.Contains("idle") || lower.Contains("stand")))
            {
                score -= 80;
            }
            if (state.Name == "idle" &&
                (lower.Contains("run") || lower.Contains("jump") || lower.Contains("damage")))
            {
                score -= 80;
            }

            return score;
        }

        private static CameraFraming FrameCameraForCharacter(Camera camera, UmaContainerCharacter container)
        {
            Renderer[] renderers = container.GetComponentsInChildren<Renderer>(true);
            bool initialized = false;
            Bounds bounds = new Bounds(container.transform.position, Vector3.one);
            foreach (Renderer renderer in renderers)
            {
                if (!renderer.enabled || !renderer.gameObject.activeInHierarchy)
                {
                    continue;
                }

                if (!initialized)
                {
                    bounds = renderer.bounds;
                    initialized = true;
                }
                else
                {
                    bounds.Encapsulate(renderer.bounds);
                }
            }

            if (!initialized)
            {
                throw new InvalidOperationException("The loaded Mini model has no visible renderers.");
            }

            camera.fieldOfView = 25f;
            float verticalTangent = Mathf.Tan(camera.fieldOfView * Mathf.Deg2Rad * 0.5f);
            float horizontalTangent = verticalTangent * (CellWidth / (float)CellHeight);
            float horizontalExtent = Mathf.Max(bounds.extents.x, bounds.extents.z);
            float distance = Mathf.Max(
                bounds.extents.y / verticalTangent,
                horizontalExtent / horizontalTangent);
            distance = (distance + bounds.extents.z) * 1.3f;

            Vector3 center = bounds.center;
            camera.transform.position = center + Vector3.forward * distance;
            camera.transform.LookAt(center);
            return new CameraFraming(center, distance);
        }

        private IEnumerator CalibrateCamera(
            Camera camera,
            UmaContainerCharacter container,
            IDictionary<string, MotionResolution> resolved,
            CameraFraming framing,
            CharacterResult result)
        {
            const float targetWidth = 182f;
            const float targetHeight = 196f;
            const float targetCenterX = 96f;
            const float targetCenterY = 103f;

            for (int pass = 0; pass < 3; pass++)
            {
                PixelBounds union = PixelBounds.Empty();
                foreach (StateDefinition state in States)
                {
                    MotionResolution resolution = resolved[state.Name];
                    if (resolution.Entry == null)
                    {
                        continue;
                    }

                    container.transform.localRotation = Quaternion.Euler(0f, state.YawDegrees, 0f);
                    container.LoadAnimation(resolution.Entry);
                    yield return null;

                    AnimationClip clip = container.OverrideController["clip_2"];
                    if (clip == null)
                    {
                        continue;
                    }

                    container.UmaAnimator.applyRootMotion = false;
                    container.UmaAnimator.SetLayerWeight(2, 0f);
                    container.UmaAnimator.speed = 0f;

                    for (int frame = 0; frame < state.FrameCount; frame++)
                    {
                        bool loop = state.PreferLoop || clip.isLooping;
                        float normalizedTime = GetSampleTime(frame, state.FrameCount, loop);
                        SeekAnimator(container, normalizedTime);
                        yield return new WaitForEndOfFrame();

                        Texture2D sample = global::Screenshot.GrabFrame(
                            camera,
                            CellWidth,
                            CellHeight,
                            true);
                        try
                        {
                            PixelBounds bounds = GetVisibleBounds(sample);
                            if (bounds.Valid)
                            {
                                union.Encapsulate(bounds);
                            }
                        }
                        finally
                        {
                            Destroy(sample);
                        }
                    }
                }

                if (!union.Valid)
                {
                    throw new InvalidOperationException("Camera calibration rendered no visible character pixels.");
                }

                bool edgeRisk = union.TouchesEdge(3, CellWidth, CellHeight);
                float scale;
                if (edgeRisk)
                {
                    // A silhouette touching the render target may already be truncated. Do not
                    // fit against incomplete bounds: back off first so the next pass can see it.
                    scale = 0.8f;
                }
                else
                {
                    scale = Mathf.Min(targetWidth / union.Width, targetHeight / union.Height);
                    scale = Mathf.Clamp(scale, 0.65f, 1.5f);
                }
                float verticalWorldPerPixel =
                    (2f * framing.Distance * Mathf.Tan(camera.fieldOfView * Mathf.Deg2Rad * 0.5f)) /
                    CellHeight;
                float horizontalShiftPixels = union.CenterX - targetCenterX;
                float verticalShiftPixels = union.CenterY - targetCenterY;

                framing.Target += camera.transform.right * horizontalShiftPixels * verticalWorldPerPixel;
                framing.Target += camera.transform.up * verticalShiftPixels * verticalWorldPerPixel;
                framing.Distance /= scale;

                Vector3 forward = camera.transform.forward;
                camera.transform.position = framing.Target - forward * framing.Distance;
                camera.transform.LookAt(framing.Target, Vector3.up);

                Logger.LogInfo(
                    result.Character.GetName() + " camera calibration pass " + (pass + 1) +
                    ": source=" + union.ToString() +
                    ", edge_risk=" + (edgeRisk ? "true" : "false") +
                    ", scale=" + scale.ToString("0.000", CultureInfo.InvariantCulture));
            }

            container.transform.localRotation = Quaternion.identity;
        }

        private static Texture2D CreateTransparentTexture(int width, int height)
        {
            var texture = new Texture2D(width, height, TextureFormat.RGBA32, false);
            texture.SetPixels32(new Color32[width * height]);
            texture.Apply(false, false);
            return texture;
        }

        private static Texture2D CaptureFrame(Camera camera)
        {
            Texture2D supersampled = global::Screenshot.GrabFrame(
                camera,
                CaptureWidth,
                CaptureHeight,
                true);
            try
            {
                return DownsamplePremultiplied(
                    supersampled,
                    CellWidth,
                    CellHeight,
                    SupersampleFactor);
            }
            finally
            {
                Destroy(supersampled);
            }
        }

        private static Texture2D DownsamplePremultiplied(
            Texture2D source,
            int targetWidth,
            int targetHeight,
            int factor)
        {
            if (source.width != targetWidth * factor || source.height != targetHeight * factor)
            {
                throw new InvalidOperationException(
                    "Unexpected supersampled frame dimensions: " +
                    source.width.ToString(CultureInfo.InvariantCulture) + "x" +
                    source.height.ToString(CultureInfo.InvariantCulture));
            }

            Color32[] sourcePixels = source.GetPixels32();
            var targetPixels = new Color32[targetWidth * targetHeight];
            int sampleCount = factor * factor;

            for (int targetY = 0; targetY < targetHeight; targetY++)
            {
                int sourceY = targetY * factor;
                for (int targetX = 0; targetX < targetWidth; targetX++)
                {
                    int sourceX = targetX * factor;
                    long alphaSum = 0;
                    long redPremultiplied = 0;
                    long greenPremultiplied = 0;
                    long bluePremultiplied = 0;

                    for (int offsetY = 0; offsetY < factor; offsetY++)
                    {
                        int rowOffset = (sourceY + offsetY) * source.width + sourceX;
                        for (int offsetX = 0; offsetX < factor; offsetX++)
                        {
                            Color32 pixel = sourcePixels[rowOffset + offsetX];
                            long alpha = pixel.a;
                            alphaSum += alpha;
                            redPremultiplied += pixel.r * alpha;
                            greenPremultiplied += pixel.g * alpha;
                            bluePremultiplied += pixel.b * alpha;
                        }
                    }

                    int targetIndex = targetY * targetWidth + targetX;
                    if (alphaSum == 0)
                    {
                        targetPixels[targetIndex] = new Color32(0, 0, 0, 0);
                        continue;
                    }

                    byte alphaValue = (byte)Mathf.Clamp(
                        (int)((alphaSum + sampleCount / 2) / sampleCount),
                        0,
                        255);
                    byte redValue = (byte)Mathf.Clamp(
                        (int)((redPremultiplied + alphaSum / 2) / alphaSum),
                        0,
                        255);
                    byte greenValue = (byte)Mathf.Clamp(
                        (int)((greenPremultiplied + alphaSum / 2) / alphaSum),
                        0,
                        255);
                    byte blueValue = (byte)Mathf.Clamp(
                        (int)((bluePremultiplied + alphaSum / 2) / alphaSum),
                        0,
                        255);
                    targetPixels[targetIndex] = new Color32(
                        redValue,
                        greenValue,
                        blueValue,
                        alphaValue);
                }
            }

            var target = new Texture2D(targetWidth, targetHeight, TextureFormat.RGBA32, false);
            target.SetPixels32(targetPixels);
            target.Apply(false, false);
            return target;
        }

        private static bool HasVisiblePixels(Texture2D texture)
        {
            Color32[] pixels = texture.GetPixels32();
            for (int index = 0; index < pixels.Length; index++)
            {
                if (pixels[index].a != 0)
                {
                    return true;
                }
            }
            return false;
        }

        private static PixelBounds GetVisibleBounds(Texture2D texture)
        {
            PixelBounds bounds = PixelBounds.Empty();
            Color32[] pixels = texture.GetPixels32();
            for (int index = 0; index < pixels.Length; index++)
            {
                if (pixels[index].a == 0)
                {
                    continue;
                }

                int x = index % texture.width;
                int y = index / texture.width;
                bounds.Encapsulate(x, y);
            }

            return bounds;
        }

        private static void ValidateAtlas(Texture2D atlas)
        {
            if (atlas.width != CellWidth * Columns || atlas.height != CellHeight * Rows)
            {
                throw new InvalidOperationException("Atlas dimensions are invalid.");
            }

            Color32[] pixels = atlas.GetPixels32();
            int totalFrames = Columns * Rows;
            var assigned = new bool[totalFrames];
            for (int stateIndex = 0; stateIndex < States.Length; stateIndex++)
            {
                StateDefinition state = States[stateIndex];
                int[] spriteIndices = StateSpriteIndices[stateIndex];
                if (spriteIndices.Length != state.FrameCount)
                {
                    throw new InvalidOperationException(
                        "Sprite map length does not match state " + state.Name + ".");
                }
                for (int stateFrame = 0; stateFrame < state.FrameCount; stateFrame++)
                {
                    int spriteIndex = spriteIndices[stateFrame];
                    if (spriteIndex < 0 || spriteIndex >= totalFrames || assigned[spriteIndex])
                    {
                        throw new InvalidOperationException(
                            "Invalid or duplicate sprite index for " + state.Name + ".");
                    }
                    assigned[spriteIndex] = true;
                    int atlasRow = spriteIndex / Columns;
                    int atlasColumn = spriteIndex % Columns;
                    if (!CellHasAlpha(pixels, atlas.width, atlasRow, atlasColumn))
                    {
                        throw new InvalidOperationException(
                            "Required atlas cell is empty: " +
                            state.Name + " frame " + stateFrame + ".");
                    }
                }
            }

            for (int spriteIndex = 0; spriteIndex < totalFrames; spriteIndex++)
            {
                if (assigned[spriteIndex])
                {
                    continue;
                }
                int atlasRow = spriteIndex / Columns;
                int atlasColumn = spriteIndex % Columns;
                if (CellHasAlpha(pixels, atlas.width, atlasRow, atlasColumn))
                {
                    throw new InvalidOperationException(
                        "Unused atlas cell is not transparent: row " +
                        atlasRow + " column " + atlasColumn + ".");
                }
            }
        }

        private static bool CellHasAlpha(Color32[] pixels, int textureWidth, int atlasRow, int column)
        {
            int startX = column * CellWidth;
            int startY = (Rows - atlasRow - 1) * CellHeight;
            for (int y = 0; y < CellHeight; y++)
            {
                int offset = (startY + y) * textureWidth + startX;
                for (int x = 0; x < CellWidth; x++)
                {
                    if (pixels[offset + x].a != 0)
                    {
                        return true;
                    }
                }
            }
            return false;
        }

        private Dictionary<string, string> LoadMotionOverrides()
        {
            var overrides = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            string configured = (_motionOverridesFile.Value ?? string.Empty).Trim();
            if (string.IsNullOrEmpty(configured))
            {
                return overrides;
            }
            string path = ResolveViewerRelativePath(configured, "MotionOverridesFile");
            if (!File.Exists(path))
            {
                Logger.LogInfo("No motion override file found; using automatic motion selection: " + path);
                return overrides;
            }

            var validStates = new HashSet<string>(
                States.Select(state => state.Name),
                StringComparer.OrdinalIgnoreCase);
            string[] lines = File.ReadAllLines(path);
            for (int lineIndex = 0; lineIndex < lines.Length; lineIndex++)
            {
                string line = lines[lineIndex].Trim();
                if (string.IsNullOrEmpty(line) || line.StartsWith("#", StringComparison.Ordinal))
                {
                    continue;
                }
                if (line.StartsWith("character_id,", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                int firstComma = line.IndexOf(',');
                int secondComma = firstComma < 0 ? -1 : line.IndexOf(',', firstComma + 1);
                int thirdComma = secondComma < 0 ? -1 : line.IndexOf(',', secondComma + 1);
                if (firstComma <= 0 || secondComma <= firstComma + 1 || thirdComma >= 0)
                {
                    throw new InvalidOperationException(
                        "Invalid motion override CSV row " + (lineIndex + 1) +
                        ": expected exactly three unquoted fields: " +
                        "character_id,state,motion_key_or_path.");
                }

                string characterSelector = line.Substring(0, firstComma).Trim();
                string state = line.Substring(firstComma + 1, secondComma - firstComma - 1).Trim();
                string motion = line.Substring(secondComma + 1).Trim();
                int ignoredCharacterId = 0;
                if (characterSelector != "*" &&
                    !int.TryParse(
                        characterSelector,
                        NumberStyles.Integer,
                        CultureInfo.InvariantCulture,
                        out ignoredCharacterId))
                {
                    throw new InvalidOperationException(
                        "Invalid character selector on motion override CSV row " + (lineIndex + 1) + ".");
                }
                if (characterSelector != "*")
                {
                    characterSelector = ignoredCharacterId.ToString(CultureInfo.InvariantCulture);
                }
                if (!validStates.Contains(state))
                {
                    throw new InvalidOperationException(
                        "Unknown state on motion override CSV row " + (lineIndex + 1) + ": " + state);
                }
                if (string.IsNullOrEmpty(motion))
                {
                    throw new InvalidOperationException(
                        "Missing motion on override CSV row " + (lineIndex + 1) + ".");
                }

                string key = MotionOverrideKey(characterSelector, state);
                if (overrides.ContainsKey(key))
                {
                    throw new InvalidOperationException(
                        "Duplicate motion override on CSV row " + (lineIndex + 1) +
                        " for " + characterSelector + " " + state + ".");
                }
                overrides.Add(key, motion);
            }

            Logger.LogInfo("Loaded " + overrides.Count + " motion overrides from " + path);
            return overrides;
        }

        private static string ResolveViewerRelativePath(string configured, string settingName)
        {
            if (Path.IsPathRooted(configured))
            {
                throw new InvalidOperationException(settingName + " must be relative to the UmaViewer folder.");
            }

            string viewerRoot = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
            string path = Path.GetFullPath(Path.Combine(viewerRoot, configured));
            string requiredPrefix =
                viewerRoot.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar) +
                Path.DirectorySeparatorChar;
            if (!path.StartsWith(requiredPrefix, StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException(settingName + " cannot escape the UmaViewer folder.");
            }
            return path;
        }

        private string CreateRunDirectory()
        {
            string configured = (_outputDirectory.Value ?? string.Empty).Trim();
            if (string.IsNullOrEmpty(configured))
            {
                configured = "UmaPetForge_Output";
            }
            string outputRoot = ResolveViewerRelativePath(configured, "OutputDirectory");

            string runRoot = Path.Combine(outputRoot, DateTime.Now.ToString("yyyyMMdd-HHmmss", CultureInfo.InvariantCulture));
            Directory.CreateDirectory(runRoot);
            return runRoot;
        }

        private string WriteSelectionCatalogs()
        {
            string configuredOutput = (_outputDirectory.Value ?? string.Empty).Trim();
            if (string.IsNullOrEmpty(configuredOutput))
            {
                configuredOutput = "UmaPetForge_Output";
            }
            string outputRoot = ResolveViewerRelativePath(configuredOutput, "OutputDirectory");
            string catalogDirectory = Path.Combine(outputRoot, "catalog");
            Directory.CreateDirectory(catalogDirectory);

            WriteCharacterCatalog(
                Path.Combine(catalogDirectory, "characters.json"),
                UmaViewerMain.Instance.Characters);
            WriteAnimationCatalog(
                Path.Combine(catalogDirectory, "mini-animation-catalog.json"),
                GetMiniMotions());
            EnsureMotionOverrideTemplate();
            string overrideDisplay = (_motionOverridesFile.Value ?? string.Empty).Trim();
            if (string.IsNullOrEmpty(overrideDisplay))
            {
                overrideDisplay = "(disabled; automatic selection is active)";
            }
            File.WriteAllText(
                Path.Combine(catalogDirectory, "HOW_TO_SELECT.txt"),
                "CHARACTERS" + Environment.NewLine +
                "Press F6 in UmaViewer for the searchable character picker." +
                Environment.NewLine +
                "Advanced fallback: edit Characters in " +
                "BepInEx\\config\\dev.pqqqqq.umapetforge.cfg." +
                Environment.NewLine +
                "Use comma-separated display names or numeric IDs from characters.json." +
                Environment.NewLine + Environment.NewLine +
                "CLOTHES" + Environment.NewLine +
                "Select a character in the F6 picker, then click its Clothes button." +
                Environment.NewLine +
                "Auto uses that character's default Mini outfit." +
                Environment.NewLine + Environment.NewLine +
                "INTERACTION ANIMATIONS AND FACES" + Environment.NewLine +
                "In F6, select a character and click Animations/Face." +
                Environment.NewLine +
                "Choose separate motions for idle/resting, move right, move left, and cursor hover/jump." +
                Environment.NewLine +
                "Idle recommendations include every discovered dance; hover/jump includes near05; every page also includes that Uma's character-specific clips." +
                Environment.NewLine +
                "Choosing a motion previews it immediately. If needed, UmaPetForge first loads that Mini with the clothes selected in F6." +
                Environment.NewLine +
                "Leave a motion on Auto to use the advanced CSV fallback, then automatic selection." +
                Environment.NewLine +
                "Opening a face editor automatically loads the matching Mini when needed, then applies each face change live." +
                Environment.NewLine +
                "The face editor browses the local Mini eye, mouth, and eyebrow texture slots; Auto keeps the default face." +
                Environment.NewLine + Environment.NewLine +
                "MOTIONS (ADVANCED ALL-STATE OVERRIDES)" + Environment.NewLine +
                "Edit " + overrideDisplay + "." +
                Environment.NewLine +
                "CSV format: character_id,state,motion_key_or_path" + Environment.NewLine +
                "Use exactly three unquoted fields; commas inside values are not supported." +
                Environment.NewLine +
                "Use * as character_id to apply a general motion to every selected character." +
                Environment.NewLine +
                "An exact character row wins; set its motion to auto to ignore a wildcard." +
                Environment.NewLine +
                "States: idle, run_right, run_left, wave, jump, failure, waiting, working, review" +
                Environment.NewLine +
                "Leave the CSV with comments only to keep the automatic defaults." +
                Environment.NewLine,
                new UTF8Encoding(false));
            return catalogDirectory;
        }

        private void EnsureMotionOverrideTemplate()
        {
            string configured = (_motionOverridesFile.Value ?? string.Empty).Trim();
            if (string.IsNullOrEmpty(configured))
            {
                return;
            }

            string path = ResolveViewerRelativePath(configured, "MotionOverridesFile");
            if (File.Exists(path))
            {
                return;
            }
            string directory = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(directory))
            {
                Directory.CreateDirectory(directory);
            }
            File.WriteAllText(
                path,
                "# UmaPetForge optional per-character motion overrides" + Environment.NewLine +
                "# character_id,state,motion_key_or_path" + Environment.NewLine +
                "# Exactly three unquoted fields; commas inside values are not supported." +
                Environment.NewLine +
                "# Use * instead of an ID to apply a compatible general motion to every export." +
                Environment.NewLine +
                "# An exact row wins; use auto to opt that character out of a wildcard." +
                Environment.NewLine +
                "# Example: 1001,wave,4494058001413988142" + Environment.NewLine +
                "# Slow-host idle candidate:" + Environment.NewLine +
                "# *,idle,3d/motion/mini/event/body/type00/" +
                "anm_min_eve_type00_sudachi02_loop" + Environment.NewLine,
                new UTF8Encoding(false));
        }

        private static void WriteCharacterCatalog(string path, IEnumerable<CharaEntry> characters)
        {
            var entries = characters
                .OrderBy(character => character.Id)
                .ThenBy(character => character.GetName(), StringComparer.Ordinal)
                .ToList();
            var builder = new StringBuilder();
            builder.Append("{\n  \"generated_at\": \"")
                .Append(JsonEscape(DateTimeOffset.Now.ToString("O", CultureInfo.InvariantCulture)))
                .Append("\",\n  \"characters\": [\n");
            for (int index = 0; index < entries.Count; index++)
            {
                CharaEntry character = entries[index];
                builder.Append("    { \"id\": ")
                    .Append(character.Id.ToString(CultureInfo.InvariantCulture))
                    .Append(", \"display_name\": \"")
                    .Append(JsonEscape(character.GetName()))
                    .Append("\", \"name\": \"")
                    .Append(JsonEscape(character.Name))
                    .Append("\", \"english_name\": \"")
                    .Append(JsonEscape(character.EnName))
                    .Append("\", \"is_mob\": ")
                    .Append(character.IsMob ? "true" : "false")
                    .Append(" }")
                    .Append(index == entries.Count - 1 ? "\n" : ",\n");
            }
            builder.Append("  ]\n}\n");
            File.WriteAllText(path, builder.ToString(), new UTF8Encoding(false));
        }

        private static void WriteAnimationCatalog(string path, IEnumerable<UmaDatabaseEntry> motions)
        {
            var builder = new StringBuilder();
            builder.Append("{\n  \"generated_at\": \"")
                .Append(JsonEscape(DateTimeOffset.Now.ToString("O", CultureInfo.InvariantCulture)))
                .Append("\",\n  \"motions\": [\n");
            bool first = true;
            foreach (UmaDatabaseEntry motion in motions)
            {
                if (!first)
                {
                    builder.Append(",\n");
                }
                first = false;
                int characterId;
                bool hasCharacterId = TryGetMotionCharacterId(motion.Name, out characterId);
                string scope = hasCharacterId
                    ? "character"
                    : IsGeneralMotion(motion.Name) ? "general" : "other";
                builder.Append("    { \"key\": \"")
                    .Append(motion.Key.ToString(CultureInfo.InvariantCulture))
                    .Append("\", \"name\": \"")
                    .Append(JsonEscape(motion.Name))
                    .Append("\", \"scope\": \"")
                    .Append(scope)
                    .Append("\", \"selectable\": ")
                    .Append(scope == "other" ? "false" : "true")
                    .Append(", \"character_id\": ")
                    .Append(hasCharacterId
                        ? characterId.ToString(CultureInfo.InvariantCulture)
                        : "null")
                    .Append(", \"suggested_states\": [");
                bool firstState = true;
                foreach (StateDefinition state in States)
                {
                    if (!ContainsAnyKeyword(state, motion.Name))
                    {
                        continue;
                    }
                    if (!firstState)
                    {
                        builder.Append(", ");
                    }
                    firstState = false;
                    builder.Append("\"").Append(JsonEscape(state.Name)).Append("\"");
                }
                builder.Append("] }");
            }
            builder.Append("\n  ]\n}\n");
            File.WriteAllText(path, builder.ToString(), new UTF8Encoding(false));
        }

        private static void WriteCodexPetManifest(
            string path,
            CharaEntry character,
            string petFolderName)
        {
            string[] animationNames =
            {
                "idle",
                "running-right",
                "running-left",
                "waving",
                "jumping",
                "failed",
                "waiting",
                "running",
                "review",
                "move_right",
                "move_left",
                "wave",
                "bounce",
                "sad"
            };
            int[] animationStates = { 0, 1, 2, 3, 4, 5, 6, 7, 8, 1, 2, 3, 4, 5 };
            bool[] animationLoops =
            {
                true,
                true,
                true,
                false,
                false,
                false,
                false,
                false,
                false,
                true,
                true,
                false,
                false,
                false
            };

            var builder = new StringBuilder();
            builder.Append("{\n  \"id\": \"")
                .Append(JsonEscape(petFolderName))
                .Append("\",\n  \"displayName\": \"")
                .Append(JsonEscape(character.GetName()))
                .Append("\",\n  \"description\": \"Game-rendered Uma Musume chibi companion\",\n")
                .Append("  \"spritesheetPath\": \"atlas.png\",\n")
                .Append("  \"animations\": {\n");

            for (int animationIndex = 0; animationIndex < animationNames.Length; animationIndex++)
            {
                int stateIndex = animationStates[animationIndex];
                int[] spriteIndices = StateSpriteIndices[stateIndex];
                int stateFrameCount = spriteIndices.Length;
                builder.Append("    \"")
                    .Append(animationNames[animationIndex])
                    .Append("\": { \"frames\": [");
                int cycles = animationLoops[animationIndex] ? 1 : 3;
                bool firstWrittenFrame = true;
                for (int cycle = 0; cycle < cycles; cycle++)
                {
                    for (int frame = 0; frame < stateFrameCount; frame++)
                    {
                        if (!firstWrittenFrame)
                        {
                            builder.Append(", ");
                        }
                        firstWrittenFrame = false;
                        builder.Append(spriteIndices[frame].ToString(CultureInfo.InvariantCulture));
                    }
                }
                builder.Append("], \"fps\": 16")
                    .Append(", \"loop\": ")
                    .Append(animationLoops[animationIndex] ? "true" : "false");
                if (!animationLoops[animationIndex])
                {
                    builder.Append(", \"fallback\": \"idle\"");
                }
                builder.Append(" }")
                    .Append(animationIndex == animationNames.Length - 1 ? "\n" : ",\n");
            }

            builder.Append("  }\n}\n");
            File.WriteAllText(path, builder.ToString(), new UTF8Encoding(false));
        }

        private static void WriteCharacterManifest(
            string path,
            CharaEntry character,
            string costumeId,
            IDictionary<string, MotionResolution> resolved,
            IDictionary<string, MiniFaceSelection> resolvedFaces,
            IList<string> warnings)
        {
            var builder = new StringBuilder();
            builder.Append("{\n  \"character_id\": ")
                .Append(character.Id.ToString(CultureInfo.InvariantCulture))
                .Append(",\n  \"character_name\": \"")
                .Append(JsonEscape(character.GetName()))
                .Append("\",\n  \"costume_id\": \"")
                .Append(JsonEscape(costumeId))
                .Append("\",\n  \"states\": {\n");
            for (int index = 0; index < States.Length; index++)
            {
                StateDefinition state = States[index];
                MotionResolution resolution = resolved[state.Name];
                builder.Append("    \"").Append(JsonEscape(state.Name)).Append("\": { \"motion\": ");
                if (resolution.Entry == null)
                {
                    builder.Append("null");
                }
                else
                {
                    builder.Append("\"").Append(JsonEscape(resolution.Entry.Name)).Append("\"");
                }
                builder.Append(", \"motion_key\": ");
                if (resolution.Entry == null)
                {
                    builder.Append("null");
                }
                else
                {
                    builder.Append("\"")
                        .Append(resolution.Entry.Key.ToString(CultureInfo.InvariantCulture))
                        .Append("\"");
                }
                builder.Append(", \"score\": ")
                    .Append(resolution.Score.ToString(CultureInfo.InvariantCulture))
                    .Append(", \"fallback\": ")
                    .Append(resolution.Fallback ? "true" : "false")
                    .Append(", \"source\": \"")
                    .Append(JsonEscape(resolution.Source))
                    .Append("\"")
                    .Append(", \"configured_override\": ")
                    .Append(resolution.ConfiguredOverride == null
                        ? "null"
                        : "\"" + JsonEscape(resolution.ConfiguredOverride) + "\"")
                    .Append(", \"face\": ");
                MiniFaceSelection face;
                if (resolvedFaces == null || !resolvedFaces.TryGetValue(state.Name, out face))
                {
                    builder.Append("null")
                        .Append(", \"face_source\": \"automatic\"");
                }
                else
                {
                    builder.Append("{ \"eye_left\": ")
                        .Append(face.EyeLeft.ToString(CultureInfo.InvariantCulture))
                        .Append(", \"eye_right\": ")
                        .Append(face.EyeRight.ToString(CultureInfo.InvariantCulture))
                        .Append(", \"mouth\": ")
                        .Append(face.Mouth.ToString(CultureInfo.InvariantCulture))
                        .Append(", \"eyebrow_left\": ")
                        .Append(face.EyebrowLeft.ToString(CultureInfo.InvariantCulture))
                        .Append(", \"eyebrow_right\": ")
                        .Append(face.EyebrowRight.ToString(CultureInfo.InvariantCulture))
                        .Append(" }")
                        .Append(", \"face_source\": \"picker_selection\"");
                }
                builder
                    .Append(", \"yaw_degrees\": ")
                    .Append(state.YawDegrees.ToString("0.###", CultureInfo.InvariantCulture))
                    .Append(" }");
                builder.Append(index == States.Length - 1 ? "\n" : ",\n");
            }
            builder.Append("  },\n  \"warnings\": [");
            for (int index = 0; index < warnings.Count; index++)
            {
                if (index > 0)
                {
                    builder.Append(", ");
                }
                builder.Append("\"").Append(JsonEscape(warnings[index])).Append("\"");
            }
            builder.Append("]\n}\n");
            File.WriteAllText(path, builder.ToString(), new UTF8Encoding(false));
        }

        private static void WriteExportManifest(string path, IList<CharacterResult> results)
        {
            var builder = new StringBuilder();
            builder.Append("{\n  \"plugin_version\": \"")
                .Append(PluginVersion)
                .Append("\",\n  \"completed_at\": \"")
                .Append(JsonEscape(DateTimeOffset.Now.ToString("O", CultureInfo.InvariantCulture)))
                .Append("\",\n  \"characters\": [\n");
            for (int index = 0; index < results.Count; index++)
            {
                CharacterResult result = results[index];
                builder.Append("    { \"id\": ")
                    .Append(result.Character.Id.ToString(CultureInfo.InvariantCulture))
                    .Append(", \"name\": \"")
                    .Append(JsonEscape(result.Character.GetName()))
                    .Append("\", \"success\": ")
                    .Append(result.Success ? "true" : "false")
                    .Append(", \"error\": ");
                if (string.IsNullOrEmpty(result.Error))
                {
                    builder.Append("null");
                }
                else
                {
                    builder.Append("\"").Append(JsonEscape(result.Error)).Append("\"");
                }
                builder.Append(" }");
                builder.Append(index == results.Count - 1 ? "\n" : ",\n");
            }
            builder.Append("  ]\n}\n");
            File.WriteAllText(path, builder.ToString(), new UTF8Encoding(false));
        }

        private static string PetSlug(string value)
        {
            var builder = new StringBuilder();
            bool pendingSeparator = false;
            foreach (char character in value ?? string.Empty)
            {
                if (char.IsLetterOrDigit(character))
                {
                    if (pendingSeparator && builder.Length > 0)
                    {
                        builder.Append('-');
                    }
                    builder.Append(char.ToLowerInvariant(character));
                    pendingSeparator = false;
                }
                else
                {
                    pendingSeparator = true;
                }
            }
            string result = builder.ToString();
            return string.IsNullOrEmpty(result) ? "character" : result;
        }

        private static string NormalizeName(string value)
        {
            if (string.IsNullOrEmpty(value))
            {
                return string.Empty;
            }
            var builder = new StringBuilder();
            foreach (char character in value.ToLowerInvariant())
            {
                if (char.IsLetterOrDigit(character))
                {
                    builder.Append(character);
                }
            }
            return builder.ToString();
        }

        private static string JsonEscape(string value)
        {
            if (value == null)
            {
                return string.Empty;
            }
            return value.Replace("\\", "\\\\")
                .Replace("\"", "\\\"")
                .Replace("\r", "\\r")
                .Replace("\n", "\\n")
                .Replace("\t", "\\t");
        }

        private void ShowViewerMessage(string message, UIMessageType type)
        {
            try
            {
                if (UmaViewerUI.Instance != null)
                {
                    UmaViewerUI.Instance.ShowMessage(message, type);
                }
            }
            catch (Exception exception)
            {
                Logger.LogDebug("Could not show UmaViewer message: " + exception.Message);
            }
        }

        private sealed class StateDefinition
        {
            public readonly string Name;
            public readonly int FrameCount;
            public readonly float YawDegrees;
            public readonly bool PreferLoop;
            public readonly string[] Keywords;

            public StateDefinition(string name, int frameCount, float yawDegrees, bool preferLoop, string[] keywords)
            {
                Name = name;
                FrameCount = frameCount;
                YawDegrees = yawDegrees;
                PreferLoop = preferLoop;
                Keywords = keywords;
            }
        }

        private sealed class MotionResolution
        {
            public readonly UmaDatabaseEntry Entry;
            public readonly int Score;
            public readonly bool Fallback;
            public readonly string ConfiguredOverride;
            public readonly string Source;

            public MotionResolution(
                UmaDatabaseEntry entry,
                int score,
                bool fallback,
                string configuredOverride,
                string source)
            {
                Entry = entry;
                Score = score;
                Fallback = fallback;
                ConfiguredOverride = configuredOverride;
                Source = source;
            }
        }

        private sealed class MiniCostumeOption
        {
            public readonly string Id;
            public readonly string DisplayName;

            public MiniCostumeOption(string id, string displayName)
            {
                Id = id;
                DisplayName = displayName;
            }
        }

        private sealed class CharacterResult
        {
            public readonly CharaEntry Character;
            public readonly List<string> Warnings = new List<string>();
            public bool Success;
            public string Error;
            public string OutputDirectory;

            public CharacterResult(CharaEntry character)
            {
                Character = character;
            }
        }

        private sealed class CameraFraming
        {
            public Vector3 Target;
            public float Distance;

            public CameraFraming(Vector3 target, float distance)
            {
                Target = target;
                Distance = distance;
            }
        }

        private sealed class PixelBounds
        {
            public bool Valid { get; private set; }
            public int MinX { get; private set; }
            public int MinY { get; private set; }
            public int MaxX { get; private set; }
            public int MaxY { get; private set; }

            public int Width
            {
                get { return Valid ? MaxX - MinX + 1 : 0; }
            }

            public int Height
            {
                get { return Valid ? MaxY - MinY + 1 : 0; }
            }

            public float CenterX
            {
                get { return Valid ? (MinX + MaxX) * 0.5f : 0f; }
            }

            public float CenterY
            {
                get { return Valid ? (MinY + MaxY) * 0.5f : 0f; }
            }

            public static PixelBounds Empty()
            {
                return new PixelBounds();
            }

            public void Encapsulate(int x, int y)
            {
                if (!Valid)
                {
                    MinX = x;
                    MaxX = x;
                    MinY = y;
                    MaxY = y;
                    Valid = true;
                    return;
                }

                MinX = Math.Min(MinX, x);
                MinY = Math.Min(MinY, y);
                MaxX = Math.Max(MaxX, x);
                MaxY = Math.Max(MaxY, y);
            }

            public void Encapsulate(PixelBounds other)
            {
                if (other == null || !other.Valid)
                {
                    return;
                }

                Encapsulate(other.MinX, other.MinY);
                Encapsulate(other.MaxX, other.MaxY);
            }

            public bool TouchesEdge(int margin, int width, int height)
            {
                return !Valid ||
                       MinX < margin ||
                       MinY < margin ||
                       MaxX >= width - margin ||
                       MaxY >= height - margin;
            }

            public override string ToString()
            {
                if (!Valid)
                {
                    return "empty";
                }

                return Width.ToString(CultureInfo.InvariantCulture) + "x" +
                       Height.ToString(CultureInfo.InvariantCulture) + "+" +
                       MinX.ToString(CultureInfo.InvariantCulture) + "+" +
                       MinY.ToString(CultureInfo.InvariantCulture);
            }
        }

        private sealed class CameraState
        {
            private readonly Camera _camera;
            private readonly UmaViewerBuilder _builder;
            private readonly Vector3 _position;
            private readonly Quaternion _rotation;
            private readonly float _fieldOfView;
            private readonly bool _animationCameraEnabled;
            private readonly bool _animationCameraActive;
            private readonly CameraOrbit _orbit;
            private readonly bool _orbitEnabled;
            private readonly PostProcessLayer _postProcess;
            private readonly bool _postProcessEnabled;

            private CameraState(Camera camera, UmaViewerBuilder builder)
            {
                _camera = camera;
                _builder = builder;
                _position = camera.transform.position;
                _rotation = camera.transform.rotation;
                _fieldOfView = camera.fieldOfView;

                if (builder.AnimationCamera != null)
                {
                    _animationCameraEnabled = builder.AnimationCamera.enabled;
                    _animationCameraActive = builder.AnimationCamera.gameObject.activeSelf;
                }

                _orbit = CameraOrbit.instance;
                _orbitEnabled = _orbit != null && _orbit.enabled;
                _postProcess = camera.GetComponent<PostProcessLayer>();
                _postProcessEnabled = _postProcess != null && _postProcess.enabled;
            }

            public static CameraState Capture(Camera camera, UmaViewerBuilder builder)
            {
                return new CameraState(camera, builder);
            }

            public void PrepareForExport()
            {
                _builder.SetPreviewCamera(null);
                if (_builder.AnimationCamera != null)
                {
                    _builder.AnimationCamera.enabled = false;
                    _builder.AnimationCamera.gameObject.SetActive(false);
                }
                if (_orbit != null)
                {
                    _orbit.enabled = false;
                }
                if (_postProcess != null)
                {
                    _postProcess.enabled = false;
                }
            }

            public void Restore()
            {
                _camera.transform.position = _position;
                _camera.transform.rotation = _rotation;
                _camera.fieldOfView = _fieldOfView;
                if (_orbit != null)
                {
                    _orbit.enabled = _orbitEnabled;
                }
                if (_postProcess != null)
                {
                    _postProcess.enabled = _postProcessEnabled;
                }
                if (_builder.AnimationCamera != null)
                {
                    _builder.AnimationCamera.gameObject.SetActive(_animationCameraActive);
                    _builder.AnimationCamera.enabled = _animationCameraEnabled;
                }
            }
        }
    }
}
