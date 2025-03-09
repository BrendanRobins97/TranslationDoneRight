using UnityEngine;
using System.Collections.Generic;

namespace Translations.Examples
{
    // Example script demonstrating field-level [Translated] attributes and [NotTranslated]
    public class CharacterStats : MonoBehaviour
    {
        // Basic character info - these fields need translation
        [Translated]
        public string characterName = "Sir Arthur of Camelot";
        
        [Translated]
        public string characterTitle = "Knight of the Round Table";
        
        [Translated]
        public string characterDescription = "A brave knight known for his unwavering loyalty and masterful swordsmanship. His deeds have been celebrated in songs across the realm.";
        
        // Technical fields - no need for translation
        public int strength = 18;
        public int dexterity = 14;
        public int constitution = 16;
        public int intelligence = 12;
        public int wisdom = 10;
        public int charisma = 13;
        
        // This field contains a text we want to translate
        [Translated]
        public string classDescription = "Knight: A warrior who specializes in heavy armor and weapons. Knights are sworn protectors of their realms and lords.";
        
        // This could be automatically translated because it's a string,
        // but we want to keep it as a code reference
        [NotTranslated]
        public string characterClassID = "knight_heavy_armor";
        
        // This is another field we don't want to translate
        [NotTranslated]
        public string internalUniqueID = "char_324_5423a";
        
        // Translated collection
        [Translated]
        public List<string> specialAbilities = new List<string>()
        {
            "Shield Bash: Stun an enemy for 3 seconds",
            "Rally: Boost nearby allies' morale and defense",
            "Last Stand: Gain temporary health when near death"
        };
        
        // Explicitly translated class
        [Translated]
        public CharacterBackstory backstory = new CharacterBackstory()
        {
            birthplace = "The Northern Kingdom of Arathor",
            childhoodEvent = "Lost his parents to a dragon attack at age 7",
            adultEvent = "Won the Grand Tournament at age 19",
            motivation = "To slay the dragon that orphaned him and protect others from suffering the same fate"
        };
        
        // Complex nested object with mixed translations
        public Inventory inventory = new Inventory()
        {
            // These specific fields are marked for translation
            equipmentSlots = new Dictionary<string, EquipmentItem>()
            {
                { 
                    "MainHand", 
                    new EquipmentItem() { 
                        itemName = "Excalibur", 
                        itemDescription = "A legendary sword said to have been given by the Lady of the Lake." 
                    } 
                },
                { 
                    "OffHand", 
                    new EquipmentItem() { 
                        itemName = "Shield of the Lion", 
                        itemDescription = "A sturdy shield emblazoned with a golden lion." 
                    } 
                },
                { 
                    "Head", 
                    new EquipmentItem() { 
                        itemName = "Helmet of Courage", 
                        itemDescription = "A helmet that inspires bravery in all who see it." 
                    } 
                }
            }
        };
        
        [Translated]
        public Dictionary<string, string> knightlyQuotes = new Dictionary<string, string>()
        {
            { "battle_start", "For honor and glory!" },
            { "victory", "Justice has been served this day." },
            { "defeat", "I shall return... stronger..." },
            { "level_up", "My skills grow sharper with each challenge." }
        };
        
        // Some mixed fields - only specific ones are translated
        [Translated]
        public string characterGreeting = "Well met, traveler! May the roads bring you fortune.";
        
        public float movementSpeed = 3.5f;
        
        [NotTranslated]
        public string animatorControllerPath = "Animations/HumanoidKnight";
        
        [Translated]
        public string[] combatTaunts = new string[]
        {
            "Face me if you dare!",
            "Your villainy ends today!",
            "For the kingdom!",
            "Stand and fight, coward!"
        };
        
        // UI Reference
        [SerializeField] private TMPro.TextMeshProUGUI nameText;
        [SerializeField] private TMPro.TextMeshProUGUI descriptionText;
        
        private void Start()
        {
            InitializeUI();
        }
        
        private void InitializeUI()
        {
            if (nameText != null)
            {
                nameText.SetTextTranslated("{0}, {1}", characterName, characterTitle);
            }
            
            if (descriptionText != null)
            {
                descriptionText.SetTextTranslated(characterDescription);
            }
        }
        
        // Subscribe to language change events
        private void OnEnable()
        {
            TranslationManager.OnLanguageChanged += RefreshUI;
        }
        
        private void OnDisable()
        {
            TranslationManager.OnLanguageChanged -= RefreshUI;
        }
        
        private void RefreshUI()
        {
            InitializeUI();
        }
    }

    [System.Serializable]
    public class CharacterBackstory
    {
        [Translated]
        public string birthplace;
        
        [Translated]
        public string childhoodEvent;
        
        [Translated]
        public string adultEvent;
        
        [Translated]
        public string motivation;
    }

    [System.Serializable]
    public class Inventory
    {
        public int gold = 250;
        public int maxWeight = 150;
        public int currentWeight = 42;
        
        public Dictionary<string, EquipmentItem> equipmentSlots = new Dictionary<string, EquipmentItem>();
    }

    [System.Serializable]
    public class EquipmentItem
    {
        [Translated]
        public string itemName;
        
        [Translated]
        public string itemDescription;
        
        public int itemLevel = 1;
        public int durability = 100;
        
        [NotTranslated]
        public string itemID;
    }
} 