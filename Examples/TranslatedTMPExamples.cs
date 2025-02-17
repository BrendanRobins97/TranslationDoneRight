using UnityEngine;
using TMPro;
using PSS;

public class TranslatedTMPExamples : MonoBehaviour
{
    [SerializeField] private TextMeshProUGUI simpleText;
    [SerializeField] private TextMeshProUGUI scoreText;
    [SerializeField] private TextMeshProUGUI timerText;
    [SerializeField] private TextMeshProUGUI playerNameText;
    [SerializeField] private TextMeshProUGUI itemCountText;
    [SerializeField] private TextMeshProUGUI levelText;
    
    private void Start()
    {
        // Example 1: Simple translation of a single word/phrase
        // Result: "Welcome!" (translated)
        simpleText.SetTextTranslated("WelcomeMessage");

        // Example 2: Format string with suffix
        // Result: "Score: 123" (with "Score" and "Points" translated)
        scoreText.SetTextTranslated("{0}: {1} {2}", "Score", 123, "Points");

        // Example 3: Format string is itself a key
        // Translation of "TimeRemainingFormat" might be "Time Left: {0}"
        timerText.SetTextTranslated("TimeRemainingFormat", "2:30");

        // Example 4: Multiple translated parts
        // "PlayerStatus" might translate to "{0}: {1}"
        // "Level" might translate to "Level"
        playerNameText.SetTextTranslated("PlayerStatus", "John", "Level 5");

        // Example 5: Dynamic updates
        StartCoroutine(UpdateItemCount());

        // Example 6: Complex message with multiple translations
        // "LevelUpFormat" might translate to "Congratulations! {0} {1}"
        // "Reached" might translate to "reached"
        // "Level" might translate to "level"
        UpdateLevel(5);
    }

    private System.Collections.IEnumerator UpdateItemCount()
    {
        var translatedTMP = itemCountText.GetComponent<TranslatedTMP>();
        int count = 0;

        while (true)
        {
            // "ItemCountFormat" might translate to "Current Items: {0}"
            translatedTMP.SetText("ItemCountFormat", count);
            count++;
            yield return new WaitForSeconds(1f);
        }
    }

    public void UpdateScore(int newScore)
    {
        // "ScoreFormat" might translate to "{0}: {1} {2}"
        scoreText.SetTextTranslated("ScoreFormat", "Score", newScore, "Points");
    }

    public void UpdatePlayerStatus(string playerName, bool isOnline, int level)
    {
        // "PlayerStatusFormat" might translate to "{0} {1} - {2} {3}"
        string status = isOnline ? "Online" : "Offline";
        playerNameText.SetTextTranslated("PlayerStatusFormat", "Player", playerName, "Level", level, status);
    }

    private void UpdateLevel(int level)
    {
        // "LevelUpFormat" might translate to "{0} {1} {2}!"
        levelText.SetTextTranslated("LevelUpFormat", "Player", "Reached", $"Level {level}");
    }
} 