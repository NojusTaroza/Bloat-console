using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UIElements;

namespace BloatConsole
{
    public class ConsoleUI : MonoBehaviour
    {
        [Header("Console Settings")]
        public KeyCode toggleKey = KeyCode.BackQuote;
        public bool showOnStart = false;
        public int maxLogLines = 500;

        [Header("Warning Settings")]
        [Tooltip("Show warnings when command instances are not found in scene")]
        public bool checkForFiles = true;
        [Tooltip("Suppress warnings specifically for ExampleConsoleCommands")]
        [HideInInspector] public bool suppressExampleWarnings = true;

        [Header("UI Settings")]
        [Range(0.3f, 0.8f)]
        public float consoleHeight = 0.5f;
        public bool enableAnimations = true;

        [Header("Suggestion Settings")]
        [Range(3, 10)]
        public int maxSuggestions = 6;
        public bool showSuggestionMeta = true;

        [Header("UI Assets (Auto-assigned if empty)")]
        public VisualTreeAsset consoleUIAsset;
        public StyleSheet consoleStyleSheet;
        public PanelSettings consolePanelSettings;

        // Static caching for faster subsequent loads
        private static VisualTreeAsset cachedUIAsset;
        private static StyleSheet cachedStyleSheet;
        private static PanelSettings cachedPanelSettings;

        private UIDocument uiDocument;
        private VisualElement rootElement;
        private VisualElement consoleContainer;
        private ScrollView outputScrollView;
        private VisualElement outputContent;
        private TextField inputField;
        private Label suggestionLabel;

        // New suggestion system
        private VisualElement suggestionsContainer;
        private ScrollView suggestionsScrollView;
        private VisualElement suggestionsContent;
        private List<SuggestionData> currentSuggestions = new List<SuggestionData>();
        private int selectedSuggestionIndex = -1;

        private bool isVisible = false;
        public bool IsVisible => isVisible;
        private List<LogEntry> logEntries = new List<LogEntry>();

        // Public accessors for warning settings
        public bool CheckForFiles => checkForFiles;
        public bool SuppressExampleWarnings => suppressExampleWarnings;

        private void Awake()
        {
            SetupBasicUI();
            StartCoroutine(CompleteSetupAsync());
        }

        private void SetupBasicUI()
        {
            uiDocument = gameObject.GetComponent<UIDocument>();
            if (uiDocument == null)
            {
                uiDocument = gameObject.AddComponent<UIDocument>();
            }
        }

        private System.Collections.IEnumerator CompleteSetupAsync()
        {
            yield return null;

            LoadAssets();
            SetupUIDocument();
            FindUIElements();
            SetupEventHandlers();
            SetConsoleVisible(showOnStart);
        }

        private void LoadAssets()
        {
            if (cachedUIAsset == null)
            {
                cachedUIAsset = Resources.Load<VisualTreeAsset>("BloatConsole/ConsoleUI");
                if (cachedUIAsset != null)
                {
                    Debug.Log("BloatConsole: Loaded and cached UXML asset");
                }
            }
            consoleUIAsset = cachedUIAsset;

            if (cachedStyleSheet == null)
            {
                cachedStyleSheet = Resources.Load<StyleSheet>("BloatConsole/console-styles");
                if (cachedStyleSheet != null)
                {
                    Debug.Log("BloatConsole: Loaded and cached USS stylesheet");
                }
            }
            consoleStyleSheet = cachedStyleSheet;

            if (cachedPanelSettings == null)
            {
                cachedPanelSettings = Resources.Load<PanelSettings>("BloatConsole/ConsoleSettings");
                if (cachedPanelSettings != null)
                {
                    Debug.Log("BloatConsole: Loaded and cached PanelSettings asset");
                }
                else
                {
                    Debug.LogWarning("BloatConsole: ConsoleSettings.asset not found. Creating minimal runtime settings...");
                    cachedPanelSettings = CreateMinimalPanelSettings();
                }
            }
            consolePanelSettings = cachedPanelSettings;
        }

        private PanelSettings CreateMinimalPanelSettings()
        {
            var settings = ScriptableObject.CreateInstance<PanelSettings>();

            var defaultTheme = Resources.Load<ThemeStyleSheet>("unity_builtin_extra/UIElementsDefaultRuntimeTheme");
            if (defaultTheme == null)
            {
                defaultTheme = ScriptableObject.CreateInstance<ThemeStyleSheet>();
            }

            settings.themeStyleSheet = defaultTheme;

            var textSettings = ScriptableObject.CreateInstance<PanelTextSettings>();
            settings.textSettings = textSettings;

            settings.scaleMode = PanelScaleMode.ConstantPixelSize;
            settings.scale = 1.0f;
            settings.fallbackDpi = 96;
            settings.referenceResolution = new Vector2Int(1920, 1080);
            settings.sortingOrder = 100;

            return settings;
        }

        private void SetupUIDocument()
        {
            if (consolePanelSettings != null)
            {
                uiDocument.panelSettings = consolePanelSettings;
            }
            else
            {
                Debug.LogError("BloatConsole: No PanelSettings available! UI may not render properly.");
            }

            if (consoleUIAsset != null)
            {
                uiDocument.visualTreeAsset = consoleUIAsset;
                Debug.Log("BloatConsole: Using UXML-based UI");
            }
            else
            {
                CreateFallbackUI();
                Debug.LogWarning("BloatConsole: UXML not found, using programmatic UI");
            }

            if (consoleStyleSheet != null)
            {
                uiDocument.rootVisualElement.styleSheets.Add(consoleStyleSheet);
            }
        }

        private void CreateFallbackUI()
        {
            var root = uiDocument.rootVisualElement;
            root.Clear();

            rootElement = new VisualElement();
            rootElement.name = "console-root";
            rootElement.AddToClassList("console-root");

            consoleContainer = new VisualElement();
            consoleContainer.name = "console-container";
            consoleContainer.AddToClassList("console-container");

            // Create suggestions container first (appears at top in bottom console)
            suggestionsContainer = new VisualElement();
            suggestionsContainer.name = "suggestions-container";
            suggestionsContainer.AddToClassList("suggestions-container");

            suggestionsScrollView = new ScrollView();
            suggestionsScrollView.name = "suggestions-scroll";
            suggestionsScrollView.AddToClassList("suggestions-scroll");
            suggestionsScrollView.mode = ScrollViewMode.Vertical;
            suggestionsScrollView.horizontalScrollerVisibility = ScrollerVisibility.Hidden;

            suggestionsContent = new VisualElement();
            suggestionsContent.name = "suggestions-content";
            suggestionsScrollView.Add(suggestionsContent);
            suggestionsContainer.Add(suggestionsScrollView);

            // Create input field
            inputField = new TextField();
            inputField.name = "input-field";
            inputField.AddToClassList("input-field");

            // Create output scroll view
            outputScrollView = new ScrollView();
            outputScrollView.name = "output-scroll";
            outputScrollView.AddToClassList("output-scroll");
            outputScrollView.mode = ScrollViewMode.Vertical;

            outputContent = new VisualElement();
            outputContent.name = "output-content";
            outputContent.AddToClassList("output-content");
            outputScrollView.Add(outputContent);

            // Create legacy suggestion label (hidden)
            suggestionLabel = new Label();
            suggestionLabel.name = "suggestion-label";
            suggestionLabel.AddToClassList("suggestion-label");
            suggestionLabel.style.display = DisplayStyle.None;

            // Assemble hierarchy (order matters for bottom console)
            consoleContainer.Add(suggestionsContainer);
            consoleContainer.Add(inputField);
            consoleContainer.Add(outputScrollView);
            consoleContainer.Add(suggestionLabel);
            rootElement.Add(consoleContainer);
            root.Add(rootElement);

            ApplyDynamicHeight();
        }

        private void FindUIElements()
        {
            var root = uiDocument.rootVisualElement;

            rootElement = root.Q<VisualElement>("console-root");
            consoleContainer = root.Q<VisualElement>("console-container");
            outputScrollView = root.Q<ScrollView>("output-scroll");
            outputContent = outputScrollView?.Q<VisualElement>("output-content") ?? outputScrollView?.contentContainer;
            inputField = root.Q<TextField>("input-field");
            suggestionLabel = root.Q<Label>("suggestion-label");

            // Find new suggestion elements
            suggestionsContainer = root.Q<VisualElement>("suggestions-container");
            suggestionsScrollView = root.Q<ScrollView>("suggestions-scroll");
            suggestionsContent = suggestionsScrollView?.Q<VisualElement>("suggestions-content") ?? suggestionsScrollView?.contentContainer;

            if (rootElement == null || consoleContainer == null || outputScrollView == null ||
                inputField == null || suggestionsContainer == null)
            {
                Debug.LogError("BloatConsole: Critical UI elements missing! Creating fallback UI...");
                CreateFallbackUI();
            }
            else
            {
                ApplyDynamicHeight();
            }
        }

        private void ApplyDynamicHeight()
        {
            if (consoleContainer != null)
            {
                consoleContainer.style.height = Length.Percent(consoleHeight * 100);
            }
        }

        private void SetupEventHandlers()
        {
            if (inputField == null) return;

            var consoleSystem = ConsoleSystem.Instance;
            if (consoleSystem != null)
            {
                consoleSystem.OnLogMessage += OnLogReceived;
            }

            inputField.RegisterCallback<KeyDownEvent>(OnInputKeyDown, TrickleDown.TrickleDown);
            inputField.RegisterValueChangedCallback(OnInputValueChanged);
            inputField.RegisterCallback<FocusInEvent>(OnInputFocused);
            inputField.RegisterCallback<FocusOutEvent>(OnInputUnfocused);
        }

        private void OnInputKeyDown(KeyDownEvent evt)
        {
            bool hasSuggestions = currentSuggestions.Count > 0 && suggestionsContainer.style.display == DisplayStyle.Flex;

            switch (evt.keyCode)
            {
                case KeyCode.Return:
                case KeyCode.KeypadEnter:
                    if (hasSuggestions && selectedSuggestionIndex >= 0)
                    {
                        ApplySelectedSuggestion();
                    }
                    else
                    {
                        ExecuteCommand();
                    }
                    evt.StopPropagation();
                    evt.StopImmediatePropagation();
                    break;

                case KeyCode.UpArrow:
                    if (hasSuggestions)
                    {
                        NavigateSuggestions(-1);
                        evt.StopPropagation();
                        evt.StopImmediatePropagation();
                    }
                    else
                    {
                        NavigateHistory(-1);
                        evt.StopPropagation();
                        evt.StopImmediatePropagation();
                    }
                    break;

                case KeyCode.DownArrow:
                    if (hasSuggestions)
                    {
                        NavigateSuggestions(1);
                        evt.StopPropagation();
                        evt.StopImmediatePropagation();
                    }
                    else
                    {
                        NavigateHistory(1);
                        evt.StopPropagation();
                        evt.StopImmediatePropagation();
                    }
                    break;

                case KeyCode.Tab:
                    if (hasSuggestions && selectedSuggestionIndex >= 0)
                    {
                        ApplySelectedSuggestion();
                        evt.StopPropagation();
                        evt.StopImmediatePropagation();
                    }
                    break;

                case KeyCode.Escape:
                    if (hasSuggestions)
                    {
                        HideSuggestions();
                    }
                    else
                    {
                        SetConsoleVisible(false);
                    }
                    evt.StopPropagation();
                    evt.StopImmediatePropagation();
                    break;
            }
        }

        private void OnInputValueChanged(ChangeEvent<string> evt)
        {
            UpdateSuggestions(evt.newValue);
        }

        private void OnInputFocused(FocusInEvent evt)
        {
            inputField?.AddToClassList("input-field--focused");
        }

        private void OnInputUnfocused(FocusOutEvent evt)
        {
            inputField?.RemoveFromClassList("input-field--focused");
        }

        private void UpdateSuggestions(string input)
        {
            if (string.IsNullOrWhiteSpace(input))
            {
                HideSuggestions();
                return;
            }

            var commandPart = input.Split(' ')[0];
            var consoleSystem = ConsoleSystem.Instance;
            if (consoleSystem == null)
            {
                HideSuggestions();
                return;
            }

            // Get command and variable suggestions
            var commandMatches = consoleSystem.GetMatchingCommands(commandPart);
            var variableMatches = consoleSystem.GetMatchingVariables(commandPart);

            currentSuggestions.Clear();

            // Add command suggestions
            foreach (var command in commandMatches.Take(maxSuggestions / 2))
            {
                var commandInfo = consoleSystem.GetCommandInfo(command);
                currentSuggestions.Add(new SuggestionData
                {
                    Name = command,
                    Type = SuggestionType.Command,
                    Description = commandInfo?.Description ?? "",
                    Category = commandInfo?.Category ?? ""
                });
            }

            // Add variable suggestions
            foreach (var variable in variableMatches.Take(maxSuggestions / 2))
            {
                var variableInfo = consoleSystem.GetVariableInfo(variable);
                currentSuggestions.Add(new SuggestionData
                {
                    Name = variable,
                    Type = SuggestionType.Variable,
                    Description = variableInfo?.Description ?? "",
                    Category = variableInfo?.Category ?? ""
                });
            }

            // Limit to max suggestions
            if (currentSuggestions.Count > maxSuggestions)
            {
                currentSuggestions = currentSuggestions.Take(maxSuggestions).ToList();
            }

            if (currentSuggestions.Count > 0)
            {
                ShowSuggestions();
            }
            else
            {
                HideSuggestions();
            }
        }

        private void ShowSuggestions()
        {
            if (suggestionsContainer == null || suggestionsContent == null) return;

            suggestionsContent.Clear();
            selectedSuggestionIndex = -1;

            for (int i = 0; i < currentSuggestions.Count; i++)
            {
                var suggestion = currentSuggestions[i];
                var element = CreateSuggestionElement(suggestion, i);
                suggestionsContent.Add(element);
            }

            suggestionsContainer.style.display = DisplayStyle.Flex;
        }

        private void HideSuggestions()
        {
            if (suggestionsContainer == null) return;

            suggestionsContainer.style.display = DisplayStyle.None;
            selectedSuggestionIndex = -1;
            currentSuggestions.Clear();
        }

        private VisualElement CreateSuggestionElement(SuggestionData suggestion, int index)
        {
            var element = new VisualElement();
            element.AddToClassList("suggestion-item");

            if (suggestion.Type == SuggestionType.Command)
            {
                element.AddToClassList("suggestion-item--command");
            }
            else
            {
                element.AddToClassList("suggestion-item--variable");
            }

            var nameLabel = new Label(suggestion.Name);
            element.Add(nameLabel);

            if (showSuggestionMeta && !string.IsNullOrEmpty(suggestion.Description))
            {
                var metaLabel = new Label($"- {suggestion.Description}");
                metaLabel.AddToClassList("suggestion-meta");
                element.Add(metaLabel);
            }

            // Add click handler
            element.RegisterCallback<ClickEvent>(evt =>
            {
                selectedSuggestionIndex = index;
                ApplySelectedSuggestion();
            });

            return element;
        }

        private void NavigateSuggestions(int direction)
        {
            if (currentSuggestions.Count == 0) return;

            // Clear current selection
            if (selectedSuggestionIndex >= 0 && selectedSuggestionIndex < suggestionsContent.childCount)
            {
                suggestionsContent[selectedSuggestionIndex].RemoveFromClassList("suggestion-item--selected");
            }

            // Update selection
            selectedSuggestionIndex += direction;

            if (selectedSuggestionIndex < 0)
            {
                selectedSuggestionIndex = currentSuggestions.Count - 1;
            }
            else if (selectedSuggestionIndex >= currentSuggestions.Count)
            {
                selectedSuggestionIndex = 0;
            }

            // Apply new selection
            if (selectedSuggestionIndex >= 0 && selectedSuggestionIndex < suggestionsContent.childCount)
            {
                var selectedElement = suggestionsContent[selectedSuggestionIndex];
                selectedElement.AddToClassList("suggestion-item--selected");

                // Scroll to selected item
                if (suggestionsScrollView != null)
                {
                    var elementTop = selectedElement.layout.y;
                    var elementBottom = elementTop + selectedElement.layout.height;
                    var scrollTop = suggestionsScrollView.scrollOffset.y;
                    var scrollBottom = scrollTop + suggestionsScrollView.layout.height;

                    if (elementTop < scrollTop)
                    {
                        suggestionsScrollView.scrollOffset = new Vector2(0, elementTop);
                    }
                    else if (elementBottom > scrollBottom)
                    {
                        suggestionsScrollView.scrollOffset = new Vector2(0, elementBottom - suggestionsScrollView.layout.height);
                    }
                }
            }
        }

        private void ApplySelectedSuggestion()
        {
            if (selectedSuggestionIndex < 0 || selectedSuggestionIndex >= currentSuggestions.Count)
                return;

            var suggestion = currentSuggestions[selectedSuggestionIndex];
            var currentInput = inputField.value;
            var parts = currentInput.Split(' ');

            parts[0] = suggestion.Name;
            var newValue = string.Join(" ", parts);

            inputField.value = newValue;
            HideSuggestions();

            // Focus and position cursor at end
            inputField.schedule.Execute(() =>
            {
                inputField.Focus();
                var textInput = inputField.Q(className: "unity-text-field__input");
                textInput?.Focus();
            }).ExecuteLater(1);
        }

        private void NavigateHistory(int direction)
        {
            if (inputField == null) return;

            var consoleSystem = ConsoleSystem.Instance;
            if (consoleSystem == null) return;

            string historyCommand = direction < 0 ?
                consoleSystem.GetPreviousCommand() :
                consoleSystem.GetNextCommand();

            if (!string.IsNullOrEmpty(historyCommand))
            {
                inputField.value = historyCommand;
                UpdateSuggestions(historyCommand);
            }
        }

        private void ExecuteCommand()
        {
            if (inputField == null) return;

            var command = inputField.value.Trim();
            if (string.IsNullOrEmpty(command)) return;

            AddLogEntry($"> {command}", LogType.Log);

            var consoleSystem = ConsoleSystem.Instance;
            if (consoleSystem != null)
            {
                consoleSystem.ExecuteCommand(command);
            }

            inputField.value = "";
            HideSuggestions();
            inputField.schedule.Execute(() => inputField.Focus());
        }

        private void OnLogReceived(string message, LogType logType)
        {
            if (string.IsNullOrEmpty(message) && logType == LogType.Log)
            {
                ClearOutput();
                return;
            }

            AddLogEntry(message, logType);
        }

        private void AddLogEntry(string message, LogType logType)
        {
            if (outputContent == null) return;

            var logEntry = new LogEntry
            {
                Message = message,
                LogType = logType,
                Timestamp = System.DateTime.Now
            };

            logEntries.Add(logEntry);

            while (logEntries.Count > maxLogLines)
            {
                logEntries.RemoveAt(0);
                if (outputContent.childCount > 0)
                {
                    outputContent.RemoveAt(0);
                }
            }

            CreateLogElement(logEntry);
            ScrollToBottom();
        }

        private void CreateLogElement(LogEntry logEntry)
        {
            if (outputContent == null) return;

            var logElement = new Label(logEntry.Message);
            logElement.AddToClassList("log-entry");

            if (logEntry.Message.StartsWith("> "))
            {
                logElement.AddToClassList("log-entry--command");
            }
            else
            {
                switch (logEntry.LogType)
                {
                    case LogType.Error:
                    case LogType.Exception:
                        logElement.AddToClassList("log-entry--error");
                        break;
                    case LogType.Warning:
                        logElement.AddToClassList("log-entry--warning");
                        break;
                    case LogType.Assert:
                        logElement.AddToClassList("log-entry--assert");
                        break;
                }
            }

            outputContent.Add(logElement);
        }

        private void ScrollToBottom()
        {
            if (outputScrollView == null) return;

            outputScrollView.schedule.Execute(() =>
            {
                outputScrollView.scrollOffset = new Vector2(0, outputScrollView.contentContainer.layout.height);
            }).ExecuteLater(1);
        }

        private void ClearOutput()
        {
            logEntries.Clear();
            outputContent?.Clear();
        }

        private void Update()
        {
            if (Input.GetKeyDown(toggleKey))
            {
                ToggleConsole();
            }
        }

        public void ToggleConsole()
        {
            SetConsoleVisible(!isVisible);
        }

        public void SetConsoleVisible(bool visible)
        {
            if (rootElement == null) return;

            isVisible = visible;
            rootElement.style.display = visible ? DisplayStyle.Flex : DisplayStyle.None;

            if (visible)
            {
                inputField?.schedule.Execute(() => inputField.Focus()).ExecuteLater(100);

                if (logEntries.Count == 0)
                {
                    AddLogEntry("BloatConsole ready. Type 'help' for available commands.", LogType.Log);
                }

                if (enableAnimations)
                {
                    consoleContainer?.AddToClassList("console-container--visible");
                    consoleContainer?.RemoveFromClassList("console-container--hidden");
                }
            }
            else
            {
                HideSuggestions();

                if (enableAnimations)
                {
                    consoleContainer?.AddToClassList("console-container--hidden");
                    consoleContainer?.RemoveFromClassList("console-container--visible");
                }
            }
        }

        private void OnValidate()
        {
            if (Application.isPlaying && consoleContainer != null)
            {
                ApplyDynamicHeight();
            }
        }

        private void OnDestroy()
        {
            var consoleSystem = ConsoleSystem.Instance;
            if (consoleSystem != null)
            {
                consoleSystem.OnLogMessage -= OnLogReceived;
            }
        }
    }

    [System.Serializable]
    public class LogEntry
    {
        public string Message;
        public LogType LogType;
        public System.DateTime Timestamp;
    }

    [System.Serializable]
    public class SuggestionData
    {
        public string Name;
        public SuggestionType Type;
        public string Description;
        public string Category;
    }

    public enum SuggestionType
    {
        Command,
        Variable
    }
}