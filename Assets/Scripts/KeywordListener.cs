using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using UnityEngine;

[RequireComponent(typeof(RunWhisperMicrophone))]
public class KeywordListener : MonoBehaviour
{
    [Header("Whisper Source")]
    public RunWhisperMicrophone whisper; // assign in inspector or auto-find

    [Header("Keywords")]
    public List<string> keywords = new List<string> { "upbeat music" };

    [Header("Settings")]
    [Tooltip("If true, will only trigger once per completed transcription until manually reset.")]
    public bool singleTriggerPerTranscript = true;

    [Header("Sound Player")]
    public RunJets runJets;

    [Header("Player Faces")]
    public GameObject idleFace;
    public GameObject talkingFace;

    // Tracks which keywords have already fired for the current transcript
    private HashSet<string> triggeredThisTranscript = new();

    // Example events you can hook into via inspector/other scripts:
    public Action<string> OnKeywordDetected; // passes the matched keyword
    public Action<string> OnTranscriptReceived; // full transcript

    void Reset()
    {
        // Auto-assign whisper if on same GameObject
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
        // Clean up subscriptions
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

        foreach (var kw in keywords)
        {
            if (singleTriggerPerTranscript && triggeredThisTranscript.Contains(kw))
                continue;

            if (ContainsWholeWord(transcript, kw))
            {
                Debug.Log($"Keyword '{kw}' detected in transcript."); 
                OnKeywordDetected?.Invoke(kw);

                // Turn Off Idle Face
                idleFace?.SetActive(false);
                // Turn on Talking Face
                talkingFace?.SetActive(true);

                // Trigger TTS playback
                runJets?.TextToSpeech();

                if (singleTriggerPerTranscript)
                    triggeredThisTranscript.Add(kw);
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

        // Case-insensitive whole-word match using word boundaries
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
