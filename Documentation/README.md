# Unity Language Translation System

A powerful, flexible translation and localization system for Unity games and applications.

## Overview

This Unity language translation package provides a comprehensive solution for managing translations in your Unity projects. It features automatic text extraction, an intuitive editor interface, runtime language switching, and seamless UI integration.

## Features

- 🌐 **Multiple Language Support** - Easily add and manage any number of languages
- 🔍 **Automatic Text Extraction** - Extract text from scenes, prefabs, scripts, and more
- 🖥️ **Editor Tools** - Intuitive editor window for managing translations
- 📝 **TextMeshPro Integration** - Full support for TextMeshPro UI elements
- 📊 **CSV Import/Export** - Work with external translation services
- 🔄 **Runtime Language Switching** - Change languages on the fly
- 🔤 **Font Mapping** - Set different fonts for different languages
- 📋 **Similarity Detection** - Identify and manage similar texts to reduce duplication

## Installation

1. Import the package into your Unity project
2. Ensure you have the following dependencies installed:
   - TextMeshPro
   - Addressable Assets package
3. Create your translation data asset (see Setup section)

## Quick Start

### 1. Create Translation Data

```csharp
// In the Project window, right-click and select:
// Create > Localization > TranslationData
// Place it in a Resources folder
```

### 2. Open Translation Manager

```
Window > Translations
```

### 3. Mark Text for Translation

```csharp
// Using attribute
[Translated]
public string welcomeMessage = "Welcome to our game!";

// Using static methods
string translated = Translations.Translate("Hello World");
```

### 4. Add to UI Elements

```csharp
// For TextMeshPro
myTMPText.SetTextTranslated("Welcome");

// For Unity UI Text
myText.SetTextTranslated("Welcome");
```

### 5. Extract Text

Run the extraction process. This will gather all text throughout your game

### 6. Translate Text

Translate your text manually or using DeepL

## Setup Guide

## Editor Interface

The Translations Manager window is the central hub for managing translations. It contains four main tabs:

### All Text Tab

- View and edit all translatable text
- Filter by search term, category, or state
- Add context information for translators
- Organize text into categories

### Text Extraction Tab

- Configure extraction sources (scenes, prefabs, scripts, etc.)
- Run the extraction process
- Choose how to handle existing translations when extracting

Example extraction setup:
```
Extraction Sources:
- Scenes: All scenes in build
- Prefabs: Assets/Prefabs
- Scripts: Assets/Scripts
- ScriptableObjects: Assets/Data
```

### Languages Tab

- Add/Remove languages
- Import/Export CSV files for translation
- View translation coverage statistics

### Config Tab

- Map fonts to different languages
- Configure similarity detection settings
- Set up machine translation services

## Code Usage

### Marking Text for Translation

```csharp
// Using attribute
[Translated]
public string welcomeMessage = "Welcome to our game!";

[Translated]
public List<string> menuItems = new List<string> 
{ 
    "Start Game",
    "Options",
    "Quit"
};

// Using static methods
string translated = Translations.Translate("Hello World");
string withExtension = "Hello World".TranslateString();
string formatted = Translations.Format("Player {0}: {1} points", playerName, score);
```

### Changing Language at Runtime

```csharp
// Change to a specific language
TranslationManager.ChangeLanguage("French");

// Get current language
string currentLang = TranslationManager.CurrentLanguage;

// Subscribe to language change events
TranslationManager.OnLanguageChanged += HandleLanguageChanged;

private void HandleLanguageChanged()
{
    Debug.Log("Language changed to: " + TranslationManager.CurrentLanguage);
}
```

## UI Integration

### TextMeshPro

```csharp
// Add component in code
var translatedTMP = myTMPText.gameObject.AddComponent<TranslatedTMP>();
translatedTMP.SetText("Welcome");

// Or use extension method
myTMPText.SetTextTranslated("Welcome");

// With formatting
myTMPText.SetTextTranslated("Player: {0}, Score: {1}", playerName, score);
```

### Standard Unity UI Text

```csharp
// Add component in code
var translatedText = myText.gameObject.AddComponent<TranslatedText>();
translatedText.SetText("Welcome");

// Or use extension method
myText.SetTextTranslated("Welcome");

// With formatting
myText.SetTextTranslated("Score: {0}", playerScore);
```

## Advanced Features

### Font Mapping

Different languages may require different fonts:

1. In the Config tab, select a default font
2. Map it to specific fonts for each language
3. The system automatically uses the correct font when language changes

```csharp
// Get appropriate font for current language
TMP_FontAsset font = TranslationManager.GetFontForText(defaultFont);
```

### Similarity Detection

The system can detect similar texts to reduce duplication:

1. Enable similarity detection in the Config tab
2. Set the similarity threshold
3. Review and approve/reject similar text groups
4. The system will use the canonical version for translation

### CSV Import/Export

For working with external translators:

1. Export translations to CSV from the Languages tab
2. Send the CSV to translators
3. Import the translated CSV back into the system

## Integration with Existing Projects

### Adding to an Existing Project

1. Import the package and resolve dependencies
2. Create and configure a TranslationData asset
3. Mark text for translation (attributes or direct calls)
4. Add components to UI elements
5. Extract text and prepare for translation

### Language Selection UI Example

```csharp
public class LanguageSelector : MonoBehaviour
{
    [SerializeField] private TMP_Dropdown languageDropdown;
    
    private void Start()
    {
        // Populate dropdown with available languages
        languageDropdown.ClearOptions();
        languageDropdown.AddOptions(TranslationManager.TranslationData.supportedLanguages);
        
        // Set current language
        int currentIndex = TranslationManager.TranslationData.supportedLanguages
            .IndexOf(TranslationManager.CurrentLanguage);
        languageDropdown.value = currentIndex;
        
        // Add listener
        languageDropdown.onValueChanged.AddListener(OnLanguageSelected);
    }
    
    private void OnLanguageSelected(int index)
    {
        string language = TranslationManager.TranslationData.supportedLanguages[index];
        TranslationManager.ChangeLanguage(language);
    }
}
```

## Best Practices

### Organization

- Add context information for translators
- Organize text into logical categories 
- Use format strings instead of concatenation

### Workflow

- Run text extraction regularly
- Keep translation files in version control
- Test your game in all supported languages

## API Reference

### TranslationManager

The core class that manages the translation system.

| Method | Description |
|--------|-------------|
| `Translate(string text)` | Translates the provided text to the current language |
| `ChangeLanguage(string language)` | Changes the active language |
| `GetFontForText(TMP_FontAsset defaultFont)` | Gets the appropriate font for the current language |

### Translations

Static utility class for translation operations.

| Method | Description |
|--------|-------------|
| `Translate(string text)` | Translates the provided text |
| `Format(string format, params object[] args)` | Formats a string with translated arguments |

### Components

| Component | Description |
|-----------|-------------|
| `TranslatedText` | For Unity UI Text components |
| `TranslatedTMP` | For TextMeshPro Text components |

## License

[MIT License](LICENSE)

## Support

For issues, questions, or contributions, please contact [support email] or open an issue on GitHub. 