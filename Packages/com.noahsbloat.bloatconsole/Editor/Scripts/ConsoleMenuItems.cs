using UnityEngine;
using UnityEditor;

namespace BloatConsole.Editor
{
    public static class ConsoleMenuItems
    {
        [MenuItem("Tools/BloatConsole/Setup Console")]
        public static void SetupConsole()
        {
            var existingConsole = Object.FindFirstObjectByType<ConsoleUI>();
            if (existingConsole != null)
            {
                EditorUtility.DisplayDialog("Console Already Exists",
                    "A BloatConsole is already set up in this scene.", "OK");
                Selection.activeGameObject = existingConsole.gameObject;
                return;
            }

            var consoleGO = new GameObject("[BloatConsole]");
            var consoleUI = consoleGO.AddComponent<ConsoleUI>();
            var exampleCommands = consoleGO.AddComponent<BloatConsole.Examples.ExampleConsoleCommands>();

            Selection.activeGameObject = consoleGO;
            EditorGUIUtility.PingObject(consoleGO);

            Debug.Log("BloatConsole setup complete! Press ~ to toggle console during play.");
        }

        [MenuItem("Tools/BloatConsole/Create Example Script")]
        public static void CreateExampleScript()
        {
            string template = @"using UnityEngine;
using BloatConsole;

public class MyConsoleCommands : MonoBehaviour
{
    [ConsoleVariable(""myVariable"", ""Description of my variable"", ""MyCategory"")]
    public float myFloat = 1.0f;
    
    [ConsoleVariable(""debugMode"", ""Enable debug mode"", ""Debug"")]
    public bool debugEnabled = false;

    [ConsoleCommand(""myCommand"", ""Description of my command"", ""MyCategory"")]
    public void MyCommand()
    {
        Debug.Log(""My custom command executed!"");
    }
    
    [ConsoleCommand(""heal"", ""Heal player by amount"", ""Player"")]
    public void HealPlayer(float amount)
    {
        Debug.Log($""Player healed by {amount}"");
    }
    
    [ConsoleCommand(""teleport"", ""Teleport to coordinates"", ""Player"")]
    public void TeleportPlayer(float x, float y, float z)
    {
        transform.position = new Vector3(x, y, z);
        Debug.Log($""Teleported to: {transform.position}"");
    }
}";

            string path = EditorUtility.SaveFilePanel("Create Console Example Script",
                Application.dataPath, "MyConsoleCommands", "cs");

            if (!string.IsNullOrEmpty(path))
            {
                System.IO.File.WriteAllText(path, template);
                AssetDatabase.Refresh();

                var relativePath = "Assets" + path.Substring(Application.dataPath.Length);
                var script = AssetDatabase.LoadAssetAtPath<MonoScript>(relativePath);
                Selection.activeObject = script;
                EditorGUIUtility.PingObject(script);

                Debug.Log($"Example script created at: {relativePath}");
            }
        }

        [MenuItem("Tools/BloatConsole/Documentation")]
        public static void OpenDocumentation()
        {
            Application.OpenURL("https://github.com/noahsbloat/BloatConsole");
        }

        [MenuItem("GameObject/BloatConsole/Console UI", false, 10)]
        public static void CreateConsoleFromHierarchy()
        {
            SetupConsole();
        }

        [MenuItem("Tools/BloatConsole/Validate Installation")]
        public static void ValidateInstallation()
        {
            bool isValid = true;
            string issues = "";

            // Check for required components
            if (System.Type.GetType("BloatConsole.ConsoleUI") == null)
            {
                isValid = false;
                issues += "- ConsoleUI class not found\n";
            }

            if (System.Type.GetType("BloatConsole.ConsoleSystem") == null)
            {
                isValid = false;
                issues += "- ConsoleSystem class not found\n";
            }

            if (isValid)
            {
                EditorUtility.DisplayDialog("Validation Complete",
                    "BloatConsole installation is valid!", "OK");
            }
            else
            {
                EditorUtility.DisplayDialog("Validation Failed",
                    "Issues found:\n" + issues, "OK");
            }
        }
    }
}