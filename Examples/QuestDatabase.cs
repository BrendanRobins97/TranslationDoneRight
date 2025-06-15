using UnityEngine;
using System.Collections.Generic;
using TMPro;

namespace Translations.Examples
{
    [Translated] // Extract all string fields from this class and its nested types
    public class QuestDatabase : MonoBehaviour
    {

        private void Start()
        {
            Debug.Log("Test Cutscene Dialogue".TranslateString());
        }
        
        [SerializeField]
        private QuestDefinition mainQuest = new QuestDefinition(){
            questID = "main_quest_1",
            title = "The Ancient Artifact",
            description = "The village elder has tasked you with finding the legendary Crystal of Eternity, lost in the Forsaken Ruins.",
            startDialogue = "Our village has protected the secret of the Crystal for generations. Now, with dark forces rising, you must retrieve it before it falls into the wrong hands.",
            completeDialogue = "You've done it! The Crystal is safe once more. The village is forever in your debt.",
        };

        [SerializeField]
        private List<QuestDefinition> availableQuests = new List<QuestDefinition>()
        {
            new QuestDefinition() {
                questID = "main_quest_1",
                title = "The Ancient Artifact",
                description = "The village elder has tasked you with finding the legendary Crystal of Eternity, lost in the Forsaken Ruins.",
                startDialogue = "Our village has protected the secret of the Crystal for generations. Now, with dark forces rising, you must retrieve it before it falls into the wrong hands.",
                completeDialogue = "You've done it! The Crystal is safe once more. The village is forever in your debt.",
                objectives = new List<string>() {
                    "Enter the Forsaken Ruins",
                    "Find the hidden chamber",
                    "Retrieve the Crystal of Eternity",
                    "Return to the village elder"
                },
                rewards = new List<string>() {
                    "500 Gold",
                    "Ancient Enchanted Armor",
                    "Village Hero Status"
                }
            },
            new QuestDefinition() {
                questID = "side_quest_fishing",
                title = "The Perfect Catch",
                description = "The local fisherman needs exotic fish for an upcoming festival. Catch three Glowing Angelfish from the Moonlit Lake.",
                startDialogue = "The festival is in three days, and I need those special fish! They only appear when the moon is reflected perfectly in the lake's center.",
                completeDialogue = "These are perfect specimens! The festival will be a huge success, thanks to you!",
                objectives = new List<string>() {
                    "Visit the Moonlit Lake at night",
                    "Use the Special Lure to attract Glowing Angelfish",
                    "Catch 3 Glowing Angelfish",
                    "Return to the fisherman"
                },
                rewards = new List<string>() {
                    "Fishing Master's Rod",
                    "Recipe: Exotic Fish Stew",
                    "250 Gold"
                }
            },
            new QuestDefinition() {
                questID = "side_quest_haunted",
                title = "The Haunted Mansion",
                description = "Strange noises have been reported from the abandoned mansion on the hill. Investigate what's causing them.",
                startDialogue = "Nobody's been brave enough to check what's happening at the old Willowbrook Estate. Some say it's ghosts, others say it's bandits using it as a hideout.",
                completeDialogue = "A magical music box was causing the haunting? Incredible! And you say it produces different melodies depending on who holds it? Fascinating!",
                objectives = new List<string>() {
                    "Enter Willowbrook Estate",
                    "Investigate the strange noises",
                    "Discover the source",
                    "Resolve the situation",
                    "Report back to the village council"
                },
                rewards = new List<string>() {
                    "Enchanted Music Box",
                    "Deed to Willowbrook Estate",
                    "350 Gold"
                }
            }
        };
        
        // Quest category names
        public string[] questCategories = new string[] {
            "Main Story",
            "Side Quests",
            "Crafting",
            "Exploration",
            "Combat Challenges"
        };
        
        // UI Messages
        public string questCompletedMessage = "Quest Completed!";
        public string questFailedMessage = "Quest Failed!";
        public string questUpdatedMessage = "Quest Updated!";
        
        // Helper method to get quest by ID
        public QuestDefinition GetQuest(string questID)
        {
            return availableQuests.Find(q => q.questID == questID);
        }
        
        // Example of runtime translation usage
        public string GetQuestSummary(string questID)
        {
            QuestDefinition quest = GetQuest(questID);
            if (quest != null)
            {
                return Translations.Format("{0}: {1}", 
                       Translations.Translate(quest.title), 
                       Translations.Translate(quest.description));
            }
            return Translations.Translate("Quest not found");
        }
        
        public string FormatQuestObjectives(string questID)
        {
            QuestDefinition quest = GetQuest(questID);
            if (quest == null) return "";
            
            string result = Translations.Translate("Objectives:") + "\n";
            foreach (string objective in quest.objectives)
            {
                result += "- " + Translations.Translate(objective) + "\n";
            }
            return result;
        }
        
        public string FormatQuestRewards(string questID)
        {
            QuestDefinition quest = GetQuest(questID);
            if (quest == null) return "";
            
            string result = Translations.Translate("Rewards:") + "\n";
            foreach (string reward in quest.rewards)
            {
                result += "- " + Translations.Translate(reward) + "\n";
            }
            return result;
        }
    }

    [System.Serializable]
    [Translated]
    public class QuestDefinition
    {
        // Technical ID (not translated)
        [NotTranslated]
        public string questID;
        
        // Translated fields
        public string title;
        public string description;
        public string startDialogue;
        public string completeDialogue;
        public string failDialogue = "I'm disappointed that you couldn't complete this task. Perhaps another adventurer will have better luck.";
        
        public List<string> objectives = new List<string>();
        public List<string> rewards = new List<string>();
        
        // Dictionary example with translatable strings
        public Dictionary<string, string> locationDescriptions = new Dictionary<string, string>()
        {
            { "ruins_entrance", "A crumbling archway marks the entrance to the ancient ruins. Moss covers the weathered stone, and the air feels heavy with history." },
            { "hidden_chamber", "Moonlight filters through a crack in the ceiling, illuminating a small pedestal in the center of the circular room." },
            { "village", "The quaint village of Riverdale bustles with activity. Merchants hawk their wares while children play in the streets." }
        };
    }
} 