using UnityEngine;
using TMPro;
using System.Collections.Generic;
using System;
using System.Collections;

namespace Translations.Examples
{
    /// <summary>
    /// Demonstrates various Smart String formats by creating TextMeshPro components for each example.
    /// Finds or creates a Canvas in the scene and populates it with the examples.
    /// </summary>
    [Translated]
    public class SmartStringExamples : MonoBehaviour
    {

        // Examples from Unity's SmartStrings documentation and our test cases
        private List<string> smartStringExamples = new List<string>
        {
            // Basic placeholder with plural forms
            "You have {count:plural:1 {item}|{} {items}} in your inventory.",
            
            // Named variables with properties
            "Welcome back, {player.name}! Your level is {player.level} and you have {player.gold} gold coins.",
            
            // Nested pluralization with counts
            "You have completed {quests.completed} out of {quests.total} quests. {quests.remaining:plural:0 {No quests remain}|1 {Only {} quest remains}|{} {quests remain}}.",
            
            // Conditional formatting using choose
            "Your shield is {durability:choose(0,30,70):broken|damaged|in good condition}.",
            
            // List formatting
            "Your party members are {party:list:{}.name}.",
            
            // Date/time formatting
            "The quest expires on {expiryDate:date:d} at {expiryDate:time:t}.",
            
            // Nested scopes with multiple variables
            "{player:{name} ({level}), your location is {location:{city}, {region}}}",
            
            // Complex pluralization with gender
            "Test {character:gender(male,female):{} found {count:plural:1 {{} potion}|{} {potions}} in {gender:his|her} inventory|{} found {count:plural:1 {{} potion}|{} {potions}} in their inventory}",
            
            // Conditional text based on values
            "The door is {doorState:choose(locked,unlocked,broken):locked. You need a key|unlocked. You can enter|broken. You can climb through the opening}.",
        };
    }
} 