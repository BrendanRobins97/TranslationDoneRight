using UnityEngine;
using TMPro;
using System.Collections.Generic;

namespace Translations.Examples
{
    // Example of class-level translation attribute
    [Translated]
    public class LootChest : MonoBehaviour
    {
        // These will all be extracted from the prefab automatically
        public string chestName = "Ancient Dragon Hoard";
        public string chestDescription = "A weathered chest adorned with dragon scales. Legends say it contains treasures beyond imagination... or just some rusty equipment.";
        public string chestInteractionPrompt = "Press E to open";

        // UI References
        [SerializeField] private TextMeshProUGUI nameText;
        [SerializeField] private TextMeshProUGUI descriptionText;
        [SerializeField] private TextMeshProUGUI interactionPromptText;
        
        [SerializeField] private ParticleSystem openEffect;
        
        // Complex object with nested translations
        public List<LootItem> possibleLoot = new List<LootItem>()
        {
            new LootItem() { 
                itemName = "Dragon Fang Dagger",
                description = "A dagger carved from a dragon's tooth. Still warm to the touch.",
                flavorText = "The previous owner had terrible dental hygiene."
            },
            new LootItem() { 
                itemName = "Enchanted Coin Purse",
                description = "A magical coin purse that occasionally produces extra coins.",
                flavorText = "Warning: Not responsible for inflation."
            },
            new LootItem() { 
                itemName = "Scroll of Mysterious Wisdom",
                description = "Ancient parchment containing powerful knowledge. May also contain grocery list.",
                flavorText = "Sometimes the greatest treasures are the most mundane."
            }
        };
        
        // Using arrays of strings
        [SerializeField]
        private string[] chestOpenMessages = new string[]
        {
            "The chest creaks open with an ancient groan!",
            "A cloud of dust billows out as you open the chest!",
            "The lock gives way with a satisfying click!",
            "The chest pops open immediately - that's suspicious..."
        };
        
        // Various status messages
        [SerializeField] private string lockedMessage = "This chest is locked tight. Find the key or try breaking it open.";
        [SerializeField] private string emptyMessage = "The chest is empty. Someone got here before you.";
        [SerializeField] private string trapMessage = "It's a trap! The chest explodes in a cloud of poison gas!";
        
        private bool isLocked = true;
        private bool isEmpty = false;
        private bool isTrap = false;
        
        private void Awake()
        {
            InitializeTexts();
        }
        
        private void InitializeTexts()
        {
            // Set up translated text components
            if (nameText != null)
            {
                nameText.SetTextTranslated(chestName);
            }
            
            if (descriptionText != null)
            {
                descriptionText.SetTextTranslated(chestDescription);
            }
            
            if (interactionPromptText != null)
            {
                interactionPromptText.SetTextTranslated("{0} {1} test|123", chestInteractionPrompt, chestName);
                interactionPromptText.SetTextTranslated("{0} {1} test|124", chestInteractionPrompt, chestName);

            }
        }
        
        // This would be called by player interaction
        public void Interact()
        {
            if (isLocked)
            {
                DisplayMessage(lockedMessage);
                return;
            }
            
            if (isTrap)
            {
                DisplayMessage(trapMessage);
                TriggerTrap();
                return;
            }
            
            if (isEmpty)
            {
                DisplayMessage(emptyMessage);
                return;
            }
            
            OpenChest();
        }
        
        private void OpenChest()
        {
            // Get a random open message
            int messageIndex = Random.Range(0, chestOpenMessages.Length);
            string openMessage = Translations.Translate(chestOpenMessages[messageIndex]);
            
            DisplayMessage(openMessage);
            
            // Spawn random loot
            int lootIndex = Random.Range(0, possibleLoot.Count);
            LootItem item = possibleLoot[lootIndex];
            
            string lootMessage = Translations.Format("You found: {0}", item.itemName);
            DisplayMessage(lootMessage);
            
            if (openEffect != null)
            {
                openEffect.Play();
            }
            
            isEmpty = true;
        }
        
        private void TriggerTrap()
        {
            // Visual and gameplay effects for trap would go here
            Debug.Log("Trap triggered!");
        }
        
        private void DisplayMessage(string message)
        {
            Debug.Log(message);
            // In a real implementation, this would display in the UI
        }
        
        // When language changes, update all text elements
        private void OnEnable()
        {
            TranslationManager.OnLanguageChanged += RefreshText;
        }
        
        private void OnDisable()
        {
            TranslationManager.OnLanguageChanged -= RefreshText;
        }
        
        private void RefreshText()
        {
            InitializeTexts();
        }
        
        // Debug method to unlock the chest (would be triggered by finding a key, etc.)
        public void UnlockChest()
        {
            isLocked = false;
            Debug.Log(Translations.Translate("You unlocked the chest!"));
        }
    }

    [System.Serializable]
    [Translated]
    public class LootItem
    {
        public string itemName = "Mysterious Item";
        public string description = "An object of unknown origin and purpose.";
        public string flavorText = "It looks valuable to someone, somewhere... probably.";
        
        public int goldValue = 10;
        public Sprite icon;
    }
} 