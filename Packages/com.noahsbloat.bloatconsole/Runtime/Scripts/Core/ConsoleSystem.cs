using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEngine;

namespace BloatConsole
{
    public class ConsoleSystem : MonoBehaviour
    {
        private static ConsoleSystem instance;
        private static bool isQuitting = false;

        public static ConsoleSystem Instance
        {
            get
            {
                // Prevent creation during shutdown
                if (isQuitting) return null;

                if (instance == null)
                {
                    var go = new GameObject("[BloatConsole System]");
                    instance = go.AddComponent<ConsoleSystem>();
                    DontDestroyOnLoad(go);
                }
                return instance;
            }
        }

        public delegate void CommandExecutedDelegate(string command, string result);
        public delegate void LogMessageDelegate(string message, LogType logType);

        public event CommandExecutedDelegate OnCommandExecuted;
        public event LogMessageDelegate OnLogMessage;

        private Dictionary<string, CommandInfo> commands = new Dictionary<string, CommandInfo>();
        private Dictionary<string, VariableInfo> variables = new Dictionary<string, VariableInfo>();
        private List<string> commandHistory = new List<string>();
        private int historyIndex = -1;

        private void Awake()
        {
            if (instance == null)
            {
                instance = this;
                DontDestroyOnLoad(gameObject);
                InitializeConsole();

#if UNITY_EDITOR
                // Register for play mode state changes in editor
                UnityEditor.EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
#endif
            }
            else if (instance != this)
            {
                Destroy(gameObject);
            }
        }

#if UNITY_EDITOR
        private void OnPlayModeStateChanged(UnityEditor.PlayModeStateChange state)
        {
            if (state == UnityEditor.PlayModeStateChange.ExitingPlayMode)
            {
                isQuitting = true;
                CleanupInstance();
            }
        }
#endif

        private void OnApplicationQuit()
        {
            isQuitting = true;
            CleanupInstance();
        }

        private void OnDestroy()
        {
#if UNITY_EDITOR
            UnityEditor.EditorApplication.playModeStateChanged -= OnPlayModeStateChanged;
#endif
            if (instance == this)
            {
                instance = null;
            }
        }

        private static void CleanupInstance()
        {
            if (instance != null)
            {
                if (instance.gameObject != null)
                {
                    DestroyImmediate(instance.gameObject);
                }
                instance = null;
            }
        }

        // Reset the quitting flag when entering play mode
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStaticVariables()
        {
            isQuitting = false;
            instance = null;
        }

        private void InitializeConsole()
        {
            RegisterBuiltInCommands();

            // Start scanning asynchronously to avoid blocking
            StartCoroutine(ScanForCommandsAndVariablesAsync());

            LogMessage("BloatConsole initialized. Type 'help' for available commands.", LogType.Log);
        }

        private System.Collections.IEnumerator ScanForCommandsAndVariablesAsync()
        {
            var stopwatch = System.Diagnostics.Stopwatch.StartNew();
            int processedTypes = 0;

            // Get only relevant assemblies (skip Unity engine assemblies)
            var assemblies = AppDomain.CurrentDomain.GetAssemblies()
                .Where(assembly => !IsUnityEngineAssembly(assembly))
                .ToArray();

            LogMessage($"Scanning {assemblies.Length} assemblies for console commands...", LogType.Log);

            foreach (var assembly in assemblies)
            {
                // Process assembly types safely without try-catch around yield
                var result = ProcessAssemblyTypes(assembly, ref processedTypes);

                // Yield periodically for responsiveness
                if (processedTypes % 50 == 0)
                {
                    yield return null;
                }

                // Yield after each assembly to maintain responsiveness
                yield return null;
            }

            stopwatch.Stop();
            LogMessage($"Console scan complete! Found {commands.Count} commands and {variables.Count} variables in {stopwatch.ElapsedMilliseconds}ms", LogType.Log);
        }

        private bool ProcessAssemblyTypes(System.Reflection.Assembly assembly, ref int processedTypes)
        {
            try
            {
                var types = assembly.GetTypes();

                foreach (var type in types)
                {
                    // Skip Unity internal types and interfaces
                    if (ShouldSkipType(type))
                        continue;

                    ScanTypeForCommands(type);
                    ScanTypeForVariables(type);
                    processedTypes++;
                }
                return true;
            }
            catch (ReflectionTypeLoadException ex)
            {
                // Process only successfully loaded types
                foreach (var loadedType in ex.Types)
                {
                    if (loadedType != null && !ShouldSkipType(loadedType))
                    {
                        ScanTypeForCommands(loadedType);
                        ScanTypeForVariables(loadedType);
                        processedTypes++;
                    }
                }
                return true;
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"BloatConsole: Error scanning assembly {assembly.FullName}: {ex.Message}");
                return false;
            }
        }

        private bool IsUnityEngineAssembly(System.Reflection.Assembly assembly)
        {
            var name = assembly.FullName;
            return name.StartsWith("UnityEngine") ||
                   name.StartsWith("UnityEditor") ||
                   name.StartsWith("Unity.") ||
                   name.StartsWith("System.") ||
                   name.StartsWith("mscorlib") ||
                   name.StartsWith("netstandard") ||
                   name.StartsWith("Mono.") ||
                   name.Contains("VisualStudio") ||
                   name.Contains("nunit");
        }

        private bool ShouldSkipType(Type type)
        {
            // Skip interfaces, abstract classes, and Unity internal types
            return type.IsInterface ||
                   type.IsAbstract ||
                   type.IsGenericTypeDefinition ||
                   type.FullName.StartsWith("Unity") ||
                   type.FullName.StartsWith("System") ||
                   type.FullName.StartsWith("Microsoft") ||
                   type.FullName.Contains("<>") || // Anonymous types
                   type.FullName.Contains("+<"); // Compiler generated types
        }

        private void RegisterBuiltInCommands()
        {
            RegisterCommand("help", "Show all available commands", "", ExecuteHelp);
            RegisterCommand("clear", "Clear console output", "", ExecuteClear);
            RegisterCommand("quit", "Quit application", "", ExecuteQuit);
            RegisterCommand("history", "Show command history", "", ExecuteHistory);
        }

        public void RegisterCommand(string name, string description, string category, System.Action action)
        {
            var commandInfo = new CommandInfo
            {
                Name = name,
                Description = description,
                Category = string.IsNullOrEmpty(category) ? "Built-in" : category,
                Method = action.Method,
                Target = action.Target,
                ParameterTypes = new Type[0]
            };
            commands[name.ToLower()] = commandInfo;
        }

        private void ScanTypeForCommands(Type type)
        {
            // Get all methods at once and filter
            var methods = type.GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.Instance);

            foreach (var method in methods)
            {
                // Quick check for attribute existence before expensive GetCustomAttribute call
                if (!method.IsDefined(typeof(ConsoleCommandAttribute), false))
                    continue;

                var attribute = method.GetCustomAttribute<ConsoleCommandAttribute>();
                if (attribute != null)
                {
                    RegisterMethodAsCommand(method, attribute, type);
                }
            }
        }

        private void ScanTypeForVariables(Type type)
        {
            // Scan fields
            var fields = type.GetFields(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.Instance);
            foreach (var field in fields)
            {
                if (!field.IsDefined(typeof(ConsoleVariableAttribute), false))
                    continue;

                var attribute = field.GetCustomAttribute<ConsoleVariableAttribute>();
                if (attribute != null)
                {
                    RegisterFieldAsVariable(field, attribute, type);
                }
            }

            // Scan properties
            var properties = type.GetProperties(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.Instance);
            foreach (var property in properties)
            {
                if (!property.IsDefined(typeof(ConsoleVariableAttribute), false))
                    continue;

                var attribute = property.GetCustomAttribute<ConsoleVariableAttribute>();
                if (attribute != null)
                {
                    RegisterPropertyAsVariable(property, attribute, type);
                }
            }
        }

        private void RegisterMethodAsCommand(MethodInfo method, ConsoleCommandAttribute attribute, Type declaringType)
        {
            var commandInfo = new CommandInfo
            {
                Name = attribute.CommandName,
                Description = attribute.Description,
                Category = attribute.Category,
                Method = method,
                Target = method.IsStatic ? null : FindObjectByType(method.DeclaringType),
                ParameterTypes = method.GetParameters().Select(p => p.ParameterType).ToArray()
            };

            if (!method.IsStatic && commandInfo.Target == null && typeof(MonoBehaviour).IsAssignableFrom(method.DeclaringType))
            {
                // Check if we should show warnings based on ConsoleUI settings
                if (ShouldShowWarningForType(declaringType))
                {
                    Debug.LogWarning($"BloatConsole: No instance found for command '{attribute.CommandName}' in type {method.DeclaringType.Name}");
                }
                return;
            }

            commands[attribute.CommandName.ToLower()] = commandInfo;
        }

        private void RegisterFieldAsVariable(FieldInfo field, ConsoleVariableAttribute attribute, Type declaringType)
        {
            var variableInfo = new VariableInfo
            {
                Name = attribute.VariableName,
                Description = attribute.Description,
                Category = attribute.Category,
                Field = field,
                Target = field.IsStatic ? null : FindObjectByType(field.DeclaringType),
                ReadOnly = attribute.ReadOnly
            };

            if (!field.IsStatic && variableInfo.Target == null && typeof(MonoBehaviour).IsAssignableFrom(field.DeclaringType))
            {
                if (ShouldShowWarningForType(declaringType))
                {
                    Debug.LogWarning($"BloatConsole: No instance found for variable '{attribute.VariableName}' in type {field.DeclaringType.Name}");
                }
                return;
            }

            variables[attribute.VariableName.ToLower()] = variableInfo;
        }

        private void RegisterPropertyAsVariable(PropertyInfo property, ConsoleVariableAttribute attribute, Type declaringType)
        {
            var variableInfo = new VariableInfo
            {
                Name = attribute.VariableName,
                Description = attribute.Description,
                Category = attribute.Category,
                Property = property,
                Target = (property.GetMethod?.IsStatic ?? property.SetMethod?.IsStatic ?? false) ? null : FindObjectByType(property.DeclaringType),
                ReadOnly = attribute.ReadOnly || !property.CanWrite
            };

            variables[attribute.VariableName.ToLower()] = variableInfo;
        }

        private bool ShouldShowWarningForType(Type type)
        {
            // Find the ConsoleUI to get warning settings
            var consoleUI = FindFirstObjectByType<ConsoleUI>();

            // If no ConsoleUI found, use default behavior (show warnings)
            if (consoleUI == null)
                return true;

            // If warnings are disabled globally, don't show any
            if (!consoleUI.CheckForFiles)
                return false;

            // If example warnings are suppressed, filter them out
            if (consoleUI.SuppressExampleWarnings && type.Name.Contains("ExampleConsoleCommands"))
                return false;

            return true;
        }

        private UnityEngine.Object FindObjectByType(Type type)
        {
            try
            {
                var method = typeof(UnityEngine.Object).GetMethod("FindFirstObjectByType", new Type[] { typeof(Type) });
                if (method != null)
                {
                    return (UnityEngine.Object)method.Invoke(null, new object[] { type });
                }
            }
            catch
            {
                // Fallback for older Unity versions
            }

#pragma warning disable CS0618
            return FindObjectOfType(type);
#pragma warning restore CS0618
        }

        public bool ExecuteCommand(string input)
        {
            if (string.IsNullOrWhiteSpace(input))
                return false;

            input = input.Trim();
            AddToHistory(input);

            // Check if it's a variable assignment (contains '=')
            if (input.Contains('='))
            {
                return ExecuteVariableAssignment(input);
            }

            // Parse as command
            var parts = ParseCommandInput(input);
            if (parts.Length == 0)
                return false;

            var commandName = parts[0].ToLower();

            // Check if it's a variable read
            if (parts.Length == 1 && variables.ContainsKey(commandName))
            {
                return ExecuteVariableRead(commandName);
            }

            // Execute command
            if (commands.TryGetValue(commandName, out var command))
            {
                return ExecuteCommandInternal(command, parts.Skip(1).ToArray());
            }

            LogMessage($"Unknown command: {commandName}", LogType.Error);
            return false;
        }

        private bool ExecuteVariableAssignment(string input)
        {
            var parts = input.Split('=', 2);
            if (parts.Length != 2)
            {
                LogMessage("Invalid assignment syntax. Use: variableName = value", LogType.Error);
                return false;
            }

            var varName = parts[0].Trim().ToLower();
            var value = parts[1].Trim();

            if (variables.TryGetValue(varName, out var variable))
            {
                if (variable.ReadOnly)
                {
                    LogMessage($"Variable '{variable.Name}' is read-only", LogType.Error);
                    return false;
                }

                try
                {
                    variable.SetValue(value);
                    LogMessage($"{variable.Name} = {variable.GetValue()}", LogType.Log);
                    OnCommandExecuted?.Invoke(input, "Variable set successfully");
                    return true;
                }
                catch (Exception ex)
                {
                    LogMessage($"Failed to set variable '{variable.Name}': {ex.Message}", LogType.Error);
                    return false;
                }
            }

            LogMessage($"Unknown variable: {varName}", LogType.Error);
            return false;
        }

        private bool ExecuteVariableRead(string varName)
        {
            if (variables.TryGetValue(varName, out var variable))
            {
                var value = variable.GetValue();
                LogMessage($"{variable.Name} = {value}", LogType.Log);
                return true;
            }
            return false;
        }

        private bool ExecuteCommandInternal(CommandInfo command, string[] args)
        {
            try
            {
                var paramTypes = command.ParameterTypes;
                if (args.Length != paramTypes.Length)
                {
                    LogMessage($"Command '{command.Name}' expects {paramTypes.Length} parameters, got {args.Length}", LogType.Error);
                    return false;
                }

                var parameters = new object[paramTypes.Length];
                for (int i = 0; i < paramTypes.Length; i++)
                {
                    parameters[i] = ConvertParameter(args[i], paramTypes[i]);
                }

                command.Method.Invoke(command.Target, parameters);
                OnCommandExecuted?.Invoke($"{command.Name} {string.Join(" ", args)}", "Command executed successfully");
                return true;
            }
            catch (Exception ex)
            {
                LogMessage($"Error executing command '{command.Name}': {ex.Message}", LogType.Error);
                return false;
            }
        }

        private object ConvertParameter(string value, Type targetType)
        {
            if (targetType == typeof(string)) return value;
            if (targetType == typeof(int)) return int.Parse(value);
            if (targetType == typeof(float)) return float.Parse(value);
            if (targetType == typeof(double)) return double.Parse(value);
            if (targetType == typeof(bool)) return bool.Parse(value) || value == "1";
            if (targetType == typeof(Vector3))
            {
                var parts = value.Split(',');
                if (parts.Length == 3)
                {
                    return new Vector3(float.Parse(parts[0]), float.Parse(parts[1]), float.Parse(parts[2]));
                }
            }

            return Convert.ChangeType(value, targetType);
        }

        private string[] ParseCommandInput(string input)
        {
            var parts = new List<string>();
            var current = "";
            var inQuotes = false;

            for (int i = 0; i < input.Length; i++)
            {
                var c = input[i];

                if (c == '"' && (i == 0 || input[i - 1] != '\\'))
                {
                    inQuotes = !inQuotes;
                }
                else if (c == ' ' && !inQuotes)
                {
                    if (!string.IsNullOrEmpty(current))
                    {
                        parts.Add(current);
                        current = "";
                    }
                }
                else
                {
                    current += c;
                }
            }

            if (!string.IsNullOrEmpty(current))
                parts.Add(current);

            return parts.ToArray();
        }

        // NEW METHODS FOR ENHANCED SUGGESTION SYSTEM

        public List<string> GetMatchingCommands(string partial)
        {
            partial = partial.ToLower();
            return commands.Keys.Where(cmd => cmd.StartsWith(partial)).OrderBy(x => x).ToList();
        }

        public List<string> GetMatchingVariables(string partial)
        {
            partial = partial.ToLower();
            return variables.Keys.Where(var => var.StartsWith(partial)).OrderBy(x => x).ToList();
        }

        public CommandInfo GetCommandInfo(string commandName)
        {
            commands.TryGetValue(commandName.ToLower(), out var command);
            return command;
        }

        public VariableInfo GetVariableInfo(string variableName)
        {
            variables.TryGetValue(variableName.ToLower(), out var variable);
            return variable;
        }

        public string GetPreviousCommand()
        {
            if (commandHistory.Count == 0) return "";
            historyIndex = Mathf.Max(0, historyIndex - 1);
            return commandHistory[historyIndex];
        }

        public string GetNextCommand()
        {
            if (commandHistory.Count == 0) return "";
            historyIndex = Mathf.Min(commandHistory.Count - 1, historyIndex + 1);
            return commandHistory[historyIndex];
        }

        private void AddToHistory(string command)
        {
            if (commandHistory.Count == 0 || commandHistory.Last() != command)
            {
                commandHistory.Add(command);
                if (commandHistory.Count > 100)
                {
                    commandHistory.RemoveAt(0);
                }
            }
            historyIndex = commandHistory.Count;
        }

        private void LogMessage(string message, LogType logType)
        {
            OnLogMessage?.Invoke(message, logType);
        }

        // Built-in commands
        private void ExecuteHelp()
        {
            LogMessage("Available Commands:", LogType.Log);

            var groupedCommands = commands.Values.GroupBy(c => c.Category).OrderBy(g => g.Key);
            foreach (var group in groupedCommands)
            {
                LogMessage($"\n[{group.Key}]", LogType.Log);
                foreach (var cmd in group.OrderBy(c => c.Name))
                {
                    LogMessage($"  {cmd.Name} - {cmd.Description}", LogType.Log);
                }
            }

            LogMessage("\nAvailable Variables:", LogType.Log);
            var groupedVars = variables.Values.GroupBy(v => v.Category).OrderBy(g => g.Key);
            foreach (var group in groupedVars)
            {
                LogMessage($"\n[{group.Key}]", LogType.Log);
                foreach (var var in group.OrderBy(v => v.Name))
                {
                    var readOnlyText = var.ReadOnly ? " (read-only)" : "";
                    LogMessage($"  {var.Name}{readOnlyText} - {var.Description}", LogType.Log);
                }
            }
        }

        private void ExecuteClear()
        {
            OnLogMessage?.Invoke("", LogType.Log); // Special signal to clear
        }

        private void ExecuteQuit()
        {
            LogMessage("Quitting application...", LogType.Log);
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }

        private void ExecuteHistory()
        {
            LogMessage("Command History:", LogType.Log);
            for (int i = 0; i < commandHistory.Count; i++)
            {
                LogMessage($"  {i + 1}: {commandHistory[i]}", LogType.Log);
            }
        }
    }

    [System.Serializable]
    public class CommandInfo
    {
        public string Name;
        public string Description;
        public string Category;
        public MethodInfo Method;
        public object Target;
        public Type[] ParameterTypes;
    }

    [System.Serializable]
    public class VariableInfo
    {
        public string Name;
        public string Description;
        public string Category;
        public FieldInfo Field;
        public PropertyInfo Property;
        public object Target;
        public bool ReadOnly;

        public object GetValue()
        {
            if (Field != null)
                return Field.GetValue(Target);
            if (Property != null)
                return Property.GetValue(Target);
            return null;
        }

        public void SetValue(string stringValue)
        {
            object value;
            Type targetType;

            if (Field != null)
            {
                targetType = Field.FieldType;
                value = ConvertStringToType(stringValue, targetType);
                Field.SetValue(Target, value);
            }
            else if (Property != null)
            {
                targetType = Property.PropertyType;
                value = ConvertStringToType(stringValue, targetType);
                Property.SetValue(Target, value);
            }
        }

        private object ConvertStringToType(string value, Type targetType)
        {
            if (targetType == typeof(string)) return value;
            if (targetType == typeof(int)) return int.Parse(value);
            if (targetType == typeof(float)) return float.Parse(value);
            if (targetType == typeof(double)) return double.Parse(value);
            if (targetType == typeof(bool)) return bool.Parse(value) || value == "1" || value.ToLower() == "true";

            return Convert.ChangeType(value, targetType);
        }
    }
}