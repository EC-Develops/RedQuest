using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using UnityEngine;

[RequireComponent(typeof(RunWhisperMicrophone))]
public class KeywordListener : MonoBehaviour
{
    [Header("Whisper Source")]
    public RunWhisperMicrophone whisper; // assign in inspector or auto-find

    [Header("Settings")]
    [Tooltip("If true, will only trigger once per completed transcription until manually reset.")]
    public bool singleTriggerPerTranscript = true;

    [Header("Sound Player")]
    public RunJets runJets; // TTS engine to speak responses

    [Header("Player Faces")]
    public GameObject idleFace;    // face when avatar is idle
    public GameObject talkingFace; // face when avatar is speaking

    // keyword -> prewritten response mapping; each keyword is unique to a player line
    private readonly Dictionary<string, string> responseMap = new()
    {
        { "upbeat music", "This test worked. UBOIKULSRBDVOIUSGR"},
        { "you", "Hello, I'm Victor E! I'm here to play this game with you!" },
        { "play", "Step on the tile in front of you, then memorize the path and walk across to the coin." },
        { "happened", "You messed up on the fourth tile. Try again and focus on the last two steps!" }
    };

    // Tracks which keywords have already fired for the current transcript
    private HashSet<string> triggeredThisTranscript = new();

    // Example events you can hook into via inspector/other scripts:
    public Action<string> OnKeywordDetected;      // passes the matched keyword
    public Action<string> OnTranscriptReceived;   // full transcript

    void Reset()
    {
        // Auto-assign whisper if the component is on the same GameObject
        whisper = GetComponent<RunWhisperMicrophone>();
    }

    void Start()
    {
        // Ensure we have a whisper source and wire up transcription callback
        if (whisper == null)
        {
            Debug.LogWarning("KeywordListener: Whisper reference missing, trying to find one on this GameObject.");
            whisper = GetComponent<RunWhisperMicrophone>();
            if (whisper == null)
            {
                Debug.LogError("KeywordListener: No RunWhisperMicrophone found.");
                return;
            }
        }
        whisper.OnTranscriptionComplete += HandleTranscriptionComplete;

        // Hook into RunJets completion so we can swap face back when speaking is done
        if (runJets != null)
        {
            runJets.OnSpeakComplete += HandleSpeakComplete;
        }
        else
        {
            Debug.LogWarning("KeywordListener: RunJets reference is null.");
        }
    }

    void OnDestroy()
    {
        // Clean up subscriptions to avoid leaks
        if (whisper != null)
            whisper.OnTranscriptionComplete -= HandleTranscriptionComplete;
        if (runJets != null)
            runJets.OnSpeakComplete -= HandleSpeakComplete;
    }

    void HandleTranscriptionComplete(string transcript)
    {
        if (string.IsNullOrWhiteSpace(transcript)) return;

        transcript = transcript.Trim();
        OnTranscriptReceived?.Invoke(transcript);
        Debug.Log($"KeywordListener received transcript: '{transcript}'");

        // Loop through each keyword-response pair to see if any trigger
        foreach (var kv in responseMap)
        {
            string keyword = kv.Key;
            string response = kv.Value;

            if (singleTriggerPerTranscript && triggeredThisTranscript.Contains(keyword))
                continue;

            if (ContainsWholeWord(transcript, keyword))
            {
                Debug.Log($"Keyword '{keyword}' detected in transcript.");
                OnKeywordDetected?.Invoke(keyword);

                // Turn Off Idle Face
                idleFace?.SetActive(false);
                // Turn on Talking Face
                talkingFace?.SetActive(true);

                // Set the prewritten response and speak it
                if (runJets != null)
                {
                    runJets.inputText = response; // overwrite TTS input with mapped response
                    runJets.TextToSpeech();
                }

                if (singleTriggerPerTranscript)
                    triggeredThisTranscript.Add(keyword);

                break; // only handle one keyword per transcription (remove if multiple allowed)
            }
        }
    }

    // Call this when a new recording session starts if you want to allow retriggering.
    public void ResetTriggers()
    {
        triggeredThisTranscript.Clear();
    }

    bool ContainsWholeWord(string text, string word)
    {
        if (string.IsNullOrEmpty(text) || string.IsNullOrEmpty(word))
            return false;

        // Case-insensitive whole-word match using word boundaries so "apple" doesn't match "pineapple"
        var pattern = $@"\b{Regex.Escape(word)}\b";
        return Regex.IsMatch(text, pattern, RegexOptions.IgnoreCase);
    }

    void HandleSpeakComplete()
    {
        // revert face when done speaking
        talkingFace?.SetActive(false);
        idleFace?.SetActive(true);
    }
}