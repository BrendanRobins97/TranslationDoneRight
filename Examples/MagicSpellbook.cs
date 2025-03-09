using UnityEngine;
using System.Collections.Generic;
using TMPro;

namespace Translations.Examples
{
    [Translated]
    public class MagicSpellbook : MonoBehaviour
    {
        // All string fields in this class will be automatically extracted since the class has [Translated]
        public string spellbookTitle = "Arcane Grimoire of Forbidden Knowledge";
        
        public string spellbookDescription = "A dusty tome containing secret spells gathered from the far corners of the realm. Handle with extreme caution - pages may bite.";
        
        // Even collections of complex types will be extracted 
        public List<SpellData> spells = new List<SpellData>()
        {
            new SpellData() { 
                spellName = "Fireball", 
                description = "Hurls a ball of arcane fire at your enemies. Warning: May cause excessive screaming.",
                incantation = "Ignis Maximus!",
                cooldownDescription = "You cannot cast Fireball again until your eyebrows grow back."
            },
            new SpellData() { 
                spellName = "Frog Transformation", 
                description = "Transforms the target into a small amphibian. Duration varies based on target's sense of humor.",
                incantation = "Rana Ridiculum!",
                cooldownDescription = "Spell requires one day to gather more frog essence."
            },
            new SpellData() { 
                spellName = "Levitation", 
                description = "Allows the caster to float gracefully above the ground. Effectiveness decreases proportionally with caster's weight.",
                incantation = "Gravitum Nullius!",
                cooldownDescription = "Requires recalibration of gravitational constants."
            }
        };
        
        // UI References
        [SerializeField] private TextMeshProUGUI spellNameText;
        [SerializeField] private TextMeshProUGUI spellDescriptionText;
        [SerializeField] private TextMeshProUGUI cooldownText;
        
        private void Start()
        {
            // Example of applying translations to UI using component attachment
            SetupTranslatedUI();
            
            // Display spell information
            DisplaySpell(0);
        }
        
        private void SetupTranslatedUI()
        {
            // Set the title using the extension method
            TextMeshProUGUI titleText = transform.Find("TitleText").GetComponent<TextMeshProUGUI>();
            titleText.SetTextTranslated(spellbookTitle);
            
            // Add TranslatedTMP component to description text
            TextMeshProUGUI descriptionText = transform.Find("DescriptionText").GetComponent<TextMeshProUGUI>();
            descriptionText.SetTextTranslated(spellbookDescription);
        }
        
        public void DisplaySpell(int spellIndex)
        {
            if (spellIndex >= 0 && spellIndex < spells.Count)
            {
                SpellData spell = spells[spellIndex];
                
                // Examples of runtime translation:
                string announcement = Translations.Format("{0} prepares to cast {1}!", "Player", spell.spellName);
                Debug.Log(announcement);
                
                // Update UI with translated text
                spellNameText.SetTextTranslated(spell.spellName);
                spellDescriptionText.SetTextTranslated(spell.description);
                cooldownText.SetTextTranslated("{0}: {1}", "Cooldown", spell.cooldownDescription);
            }
        }
        
        public void CastSelectedSpell()
        {
            // Example of translated string usage
            Debug.Log(Translations.Translate("The spellbook glows with arcane energy!"));
            
            // With formatting and multiple translations
            int manaRequired = Random.Range(10, 50);
            string manaMsg = Translations.Format("{0} uses {1} mana points to cast {2}!", 
                           "Player", manaRequired.ToString(), spells[0].spellName);
            Debug.Log(manaMsg);
        }
        
        // Subscribe to language change events
        private void OnEnable()
        {
            TranslationManager.OnLanguageChanged += RefreshAllText;
        }
        
        private void OnDisable()
        {
            TranslationManager.OnLanguageChanged -= RefreshAllText;
        }
        
        private void RefreshAllText()
        {
            // Refresh UI when language changes
            SetupTranslatedUI();
            
            // If we're displaying a spell, update it too
            DisplaySpell(0);
        }
    }

    [System.Serializable]
    [Translated]
    public class SpellData
    {
        public string spellName = "Unnamed Spell";
        public string description = "A mysterious magical effect.";
        public string incantation = "Magicus Genericus!";
        public string cooldownDescription = "Requires time to recharge.";
        
        public List<string> additionalEffects = new List<string>() {
            "Makes a pretty light show",
            "Smells vaguely of cinnamon",
            "May attract small woodland creatures"
        };
    }
} 