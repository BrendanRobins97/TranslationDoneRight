using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System;
using System.Collections.Generic;
using UnityEngine.Events;

namespace Translations
{
    /// <summary>
    /// This script demonstrates various ways developers might implement text in their games
    /// </summary>
    /// 
    [Translated]
    public class TextImplementationExamples : MonoBehaviour
    {
        // Direct string fields
        public string welcomeMessage = "Welcome to the game!";
        private const string ERROR_MESSAGE = "An error occurred!";
        private static readonly string LOADING_TEXT = "Loading...";
        
        // UI Components
        [SerializeField] private Text legacyUIText;
        [SerializeField] private TextMeshProUGUI tmpText;
        [SerializeField] private TextMeshPro worldSpaceText;

        [SerializeField] private DialogueLine dialogue;

        
        // Enums with text
        public enum GameState
        {
            MainMenu = 0,
            Loading = 1,
            Playing = 2,
            Paused = 3,
            GameOver = 4
        }

        // Structs/Classes with embedded text
        [Serializable]
        [Translated]
        public class DialogueLine
        {
            public string speakerName;
            public string dialogueText;
            public string[] responses = new string[] 
            {
                "Yes, I'll help",
                "Tell me more",
                "No thanks"
            };
        }

        [Serializable]
        public class QuestInfo
        {
            public string questName = "The Ancient Artifact";
            public string description = "Find the lost artifact in the dark cave";
            public string[] objectives = new string[]
            {
                "Enter the cave",
                "Find the map",
                "Locate the artifact",
                "Return to the village"
            };
            public string completionText = "You've found the artifact!";
            public string failureText = "You failed to find the artifact...";
        }

        [Translated]
        private Dictionary<string, string> errorMessages = new Dictionary<string, string>()
        {
            {"save_failed", "Failed to save game"},
            {"load_failed", "Could not load save file"},
            {"network_error", "Connection lost to server"},
            {"invalid_input", "Invalid player input"}
        };

        [Translated]
        private List<string> tutorialTips = new List<string>()
        {
            "Press WASD to move",
            "Press SPACE to jump",
            "Press E to interact",
            "Press I to open inventory"
        };

        // Text in ScriptableObject-like structure
        [Serializable]
        [Translated]
        public class ItemData
        {
            public string itemName;
            public string description;
            public string flavorText;
            public Dictionary<string, string> stats = new Dictionary<string, string>()
            {
                {"damage", "+5 Attack Power"},
                {"defense", "+3 Defense"},
                {"durability", "100/100"}
            };
        }

        // Dynamic text construction
        private string BuildItemTooltip(ItemData item)
        {
            return $"{item.itemName}\n{item.description}\n\n{item.flavorText}";
        }

        private string FormatTime(float seconds)
        {
            return $"Time Remaining: {seconds:0.0} seconds";
        }

        private string GetScoreText(int score, int multiplier)
        {
            return string.Format("Score: {0} (x{1} multiplier!)", score, multiplier);
        }

        // Event-driven text
        [Serializable]
        public class TextEvent : UnityEvent<string> { }
        public TextEvent onDisplayMessage;

        // UI Update methods
        private void UpdateUI(GameState state)
        {
            switch (state)
            {
                case GameState.MainMenu:
                    legacyUIText.SetTextTranslated("Press Start");
                    break;
                case GameState.Loading:
                    tmpText.SetTextTranslated("Loading next level...");
                    break;
                case GameState.Playing:
                    worldSpaceText.SetTextTranslated("Game in progress");
                    break;
                case GameState.Paused:
                    legacyUIText.SetTextTranslated("Game Paused\nPress ESC to continue");
                    break;
                case GameState.GameOver:
                    tmpText.SetTextTranslated("Game Over!\nFinal Score: 1000\nPress R to restart");
                    break;
            }
        }

        // Interpolated strings
        private void ShowPlayerStats(string playerName, int level, int exp)
        {
            string statsText = "Player: {0} | Level: {1} | EXP: {2}/100";
            Debug.Log(Translations.Translate("Test"));
            tmpText.SetTextTranslated(statsText, playerName, level, exp);
        }

        // StringBuilder usage (common in optimization scenarios)
        private void UpdateInventoryText(List<ItemData> items)
        {
            System.Text.StringBuilder sb = new System.Text.StringBuilder();
            sb.AppendLine("Inventory:");
            foreach (var item in items)
            {
                sb.AppendLine($"- {item.itemName} ({item.description})");
                foreach (var stat in item.stats)
                {
                    sb.AppendLine($"  {stat.Key}: {stat.Value}");
                }
            }
            legacyUIText.text = sb.ToString();
        }

        // Rich text formatting
        private void ShowFormattedMessage(string message, string color)
        {
            tmpText.text = $"<color={color}>{message}</color>";
        }

        private void ShowQuestUpdate(string questName, bool isComplete)
        {
            string status = isComplete ? "<color=green>Complete</color>" : "<color=yellow>In Progress</color>";
            tmpText.text = $"Quest: {questName}\nStatus: {status}";
        }

        // Text from external configuration
        [Serializable]
        public class UIConfig
        {
            public string menuTitle = "Main Menu";
            public string[] menuOptions = new string[]
            {
                "Start Game",
                "Options",
                "Credits",
                "Quit"
            };
            public Dictionary<string, string> buttonLabels = new Dictionary<string, string>()
            {
                {"confirm", "OK"},
                {"cancel", "Cancel"},
                {"back", "Return to Menu"},
                {"accept", "Accept Quest"},
                {"decline", "Decline Quest"}
            };
        }

        // Nested text in complex data structures
        [Serializable]
        public class GameSettings
        {
            public Dictionary<string, Dictionary<string, string>> localizedSettings = new Dictionary<string, Dictionary<string, string>>()
            {
                {
                    "graphics", new Dictionary<string, string>()
                    {
                        {"quality", "Graphics Quality"},
                        {"resolution", "Screen Resolution"},
                        {"fullscreen", "Fullscreen Mode"}
                    }
                },
                {
                    "audio", new Dictionary<string, string>()
                    {
                        {"master", "Master Volume"},
                        {"music", "Music Volume"},
                        {"sfx", "Sound Effects"}
                    }
                }
            };
        }

        // Methods that return text
        private string GetRandomTip()
        {
            int index = UnityEngine.Random.Range(0, tutorialTips.Count);
            return tutorialTips[index];
        }

        private string GetErrorMessage(string errorCode)
        {
            return errorMessages.TryGetValue(errorCode, out string message) 
                ? message 
                : "Unknown error occurred".TranslateString();
        }

        // Text modification methods
        private string AddPrefix(string text, string prefix)
        {
            return $"{prefix.TranslateString()}: {text}";
        }

        private string WrapInBrackets(string text)
        {
            return Translations.Format("Text is now in brackets {0}. Horray!", text);
        }

        private void Start()
        {
            ShowPlayerStats("Hero".TranslateString(), 10, 75);
            ShowFormattedMessage("Critical Hit!".TranslateString(), "red".TranslateString());
            ShowQuestUpdate("The Ancient Artifact".TranslateString(), false);
            
            onDisplayMessage?.Invoke("Welcome to the game!".TranslateString());
        }
    }
} 