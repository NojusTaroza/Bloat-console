using UnityEngine;

namespace BloatConsole
{
    public class ConsoleManager : MonoBehaviour
    {
        [Header("Auto Setup")]
        [SerializeField] private bool autoCreateConsole = true;
        [SerializeField] private KeyCode toggleKey = KeyCode.BackQuote;
        [SerializeField] private bool showOnStart = false;

        private static ConsoleManager instance;
        private ConsoleUI consoleUI;

        public static ConsoleManager Instance => instance;

        private void Awake()
        {
            if (instance == null)
            {
                instance = this;
                DontDestroyOnLoad(gameObject);

                if (autoCreateConsole)
                {
                    SetupConsole();
                }
            }
            else if (instance != this)
            {
                Destroy(gameObject);
            }
        }

        private void SetupConsole()
        {
            // Ensure console UI exists
            consoleUI = FindFirstObjectByType<ConsoleUI>();
            if (consoleUI == null)
            {
                var consoleGO = new GameObject("[BloatConsole UI]");
                consoleGO.transform.SetParent(transform);
                consoleUI = consoleGO.AddComponent<ConsoleUI>();

                // Apply settings
                consoleUI.toggleKey = toggleKey;
                consoleUI.showOnStart = showOnStart;

                Debug.Log("BloatConsole: Auto-created console UI");
            }

            // Ensure console system exists
            if (ConsoleSystem.Instance == null)
            {
                Debug.LogError("BloatConsole: ConsoleSystem failed to initialize");
            }
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Initialize()
        {
            var existingManager = FindFirstObjectByType<ConsoleManager>();
            if (existingManager == null)
            {
                var managerGO = new GameObject("[BloatConsole Manager]");
                managerGO.AddComponent<ConsoleManager>();
                DontDestroyOnLoad(managerGO);
            }
        }

        public void ToggleConsole()
        {
            if (consoleUI != null)
            {
                consoleUI.ToggleConsole();
            }
        }

        public void SetConsoleVisible(bool visible)
        {
            if (consoleUI != null)
            {
                consoleUI.SetConsoleVisible(visible);
            }
        }

        public bool IsConsoleVisible()
        {
            return consoleUI != null && consoleUI.IsVisible;
        }

        private void OnValidate()
        {
            if (consoleUI != null)
            {
                consoleUI.toggleKey = toggleKey;
                consoleUI.showOnStart = showOnStart;
            }
        }
    }
}