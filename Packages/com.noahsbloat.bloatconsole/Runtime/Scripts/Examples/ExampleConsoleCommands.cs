using UnityEngine;

namespace BloatConsole.Examples
{
    public class ExampleConsoleCommands : MonoBehaviour
    {
        [Header("Console Variables - Now Instance Variables")]
        [ConsoleVariable("simNetPing", "Simulated network ping in milliseconds", "Network")]
        public int simulatedPing = 0;

        [ConsoleVariable("simNetLoss", "Simulated packet loss percentage", "Network")]
        public float packetLoss = 0f;

        [ConsoleVariable("showFPS", "Show FPS counter on screen", "Debug")]
        public bool showFPS = false;

        [ConsoleVariable("godMode", "Enable invincibility mode", "Gameplay")]
        public bool godModeEnabled = false;

        [ConsoleVariable("playerSpeed", "Player movement speed multiplier", "Gameplay")]
        public float playerSpeedMultiplier = 1.0f;

        [ConsoleVariable("timeScale", "Game time scale", "Gameplay")]
        public float gameTimeScale = 1.0f;

        [ConsoleVariable("masterVolume", "Master audio volume", "Audio")]
        public float masterVolume = 1.0f;

        [Header("Read-only Variables - Now Instance")]
        [ConsoleVariable("currentFPS", "Current frames per second", "System", true)]
        public float currentFPS = 0f;

        [ConsoleVariable("memoryUsage", "Current memory usage in MB", "System", true)]
        public float memoryUsage = 0f;

        private float fpsTimer = 0f;
        private int frameCount = 0;

        private void Start()
        {
            // Apply initial settings
            ApplyTimeScale();
            ApplyAudioSettings();
        }

        private void Update()
        {
            UpdateFPS();
            ApplyRuntimeSettings();
        }

        private void UpdateFPS()
        {
            frameCount++;
            fpsTimer += Time.unscaledDeltaTime;

            if (fpsTimer >= 1f)
            {
                currentFPS = frameCount / fpsTimer;
                frameCount = 0;
                fpsTimer = 0f;
            }

            memoryUsage = System.GC.GetTotalMemory(false) / 1024f / 1024f;
        }

        private void ApplyRuntimeSettings()
        {
            if (Time.timeScale != gameTimeScale)
                ApplyTimeScale();

            if (AudioListener.volume != masterVolume)
                ApplyAudioSettings();
        }

        private void ApplyTimeScale()
        {
            Time.timeScale = Mathf.Max(0.1f, gameTimeScale);
        }

        private void ApplyAudioSettings()
        {
            AudioListener.volume = Mathf.Clamp01(masterVolume);
        }

        private void OnGUI()
        {
            if (showFPS)
            {
                var style = new GUIStyle();
                style.fontSize = 20;
                style.normal.textColor = Color.yellow;
                style.fontStyle = FontStyle.Bold;

                var rect = new Rect(10, 10, 200, 25);

                // Shadow
                var shadowRect = new Rect(rect.x + 1, rect.y + 1, rect.width, rect.height);
                var shadowStyle = new GUIStyle(style);
                shadowStyle.normal.textColor = Color.black;
                GUI.Label(shadowRect, $"FPS: {currentFPS:F1}", shadowStyle);

                // Main text
                GUI.Label(rect, $"FPS: {currentFPS:F1}", style);
            }
        }

        [ConsoleCommand("teleport", "Teleport player to coordinates", "Player")]
        public void TeleportPlayer(float x, float y, float z)
        {
            transform.position = new Vector3(x, y, z);
            Debug.Log($"Teleported to: {transform.position}");
        }

        [ConsoleCommand("heal", "Heal player by amount", "Player")]
        public void HealPlayer(float amount)
        {
            Debug.Log($"Player healed by {amount}");
        }

        [ConsoleCommand("giveItem", "Give item to player", "Player")]
        public void GiveItem(string itemName, int quantity)
        {
            Debug.Log($"Gave {quantity}x {itemName} to player");
        }

        [ConsoleCommand("setWeather", "Change weather condition", "World")]
        public void SetWeather(string weatherType)
        {
            Debug.Log($"Weather changed to: {weatherType}");
        }

        [ConsoleCommand("spawnEnemy", "Spawn enemy at location", "Debug")]
        public void SpawnEnemy(string enemyType, float x, float y, float z)
        {
            var position = new Vector3(x, y, z);
            Debug.Log($"Spawning {enemyType} at {position}");
        }

        [ConsoleCommand("saveGame", "Save the current game", "Game")]
        public void SaveGame(string saveName)
        {
            Debug.Log($"Game saved as: {saveName}");
            PlayerPrefs.SetString("LastSave", saveName);
        }

        [ConsoleCommand("loadGame", "Load a saved game", "Game")]
        public void LoadGame(string saveName)
        {
            if (PlayerPrefs.HasKey("LastSave"))
            {
                Debug.Log($"Loading game: {saveName}");
            }
            else
            {
                Debug.LogError($"Save file not found: {saveName}");
            }
        }

        [ConsoleCommand("screenshot", "Take a screenshot", "Utility")]
        public void TakeScreenshot()
        {
            string filename = $"screenshot_{System.DateTime.Now:yyyy-MM-dd_HH-mm-ss}.png";
            ScreenCapture.CaptureScreenshot(filename);
            Debug.Log($"Screenshot saved: {filename}");
        }

        [ConsoleCommand("benchmark", "Run performance benchmark", "Debug")]
        public void RunBenchmark(int iterations)
        {
            var stopwatch = System.Diagnostics.Stopwatch.StartNew();

            for (int i = 0; i < iterations; i++)
            {
                var go = new GameObject($"TestObject_{i}");
                DestroyImmediate(go);
            }

            stopwatch.Stop();
            Debug.Log($"Benchmark: {iterations} iterations in {stopwatch.ElapsedMilliseconds}ms");
        }

        [ConsoleCommand("listCommands", "List all available commands", "Help")]
        public void ListCommands()
        {
            var commands = FindFirstObjectByType<ConsoleUI>();
            if (commands != null)
            {
                Debug.Log("Use 'help' command to see all available commands");
            }
        }

        [ConsoleCommand("resetSettings", "Reset all console variables to defaults", "System")]
        public void ResetSettings()
        {
            simulatedPing = 0;
            packetLoss = 0f;
            showFPS = false;
            godModeEnabled = false;
            playerSpeedMultiplier = 1.0f;
            gameTimeScale = 1.0f;
            masterVolume = 1.0f;

            ApplyTimeScale();
            ApplyAudioSettings();

            Debug.Log("All settings reset to defaults");
        }
    }
}