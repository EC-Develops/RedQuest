using System.Collections;
using UnityEngine;

public class AIControl : MonoBehaviour
{
    [Header("Component References")]
    public RunWhisperMicrophone whisperMicrophone;
    public RunJets textToSpeech;
    
    [Header("Silence Detection Settings")]
    [Range(0.001f, 0.1f)]
    public float silenceThreshold = 0.01f;
    [Range(30, 300)]
    public int silenceFrames = 120; 
    
    [Header("Pipeline Settings")]
    public bool enablePipeline = true;
    public bool debugOutput = true;
    
    
    private bool isListening = false;
    private bool isProcessing = false;
    private string lastTranscribedText = "";
    
    
    private int currentSilenceFrames = 0;
    private bool silenceDetectionActive = false;
    
    void Start()
    {
        
        if (whisperMicrophone == null)
        {
            whisperMicrophone = FindObjectOfType<RunWhisperMicrophone>();
            if (whisperMicrophone == null)
            {
                Debug.LogError("AIControl: RunWhisperMicrophone component not found!");
                return;
            }
        }
        
        if (textToSpeech == null)
        {
            textToSpeech = FindObjectOfType<RunJets>();
            if (textToSpeech == null)
            {
                Debug.LogError("AIControl: RunJets component not found!");
                return;
            }
        }
        
        
        whisperMicrophone.OnTranscriptionComplete += OnTranscriptionReceived;
        
        
        whisperMicrophone.continuousMode = false;
        
        Debug.Log("AIControl initialized successfully!");
    }
    
    void Update()
    {
        if (!enablePipeline) return;
        
        
        if (Input.GetKeyDown(KeyCode.T) && !isListening && !isProcessing)
        {
            StartListening();
        }
        
        
        if (isListening && silenceDetectionActive)
        {
            DetectSilenceAndStop();
        }
    }
    
    public void StartListening()
    {
        if (isListening || isProcessing)
        {
            if (debugOutput)
                Debug.LogWarning("AIControl: Already listening or processing, ignoring request.");
            return;
        }
        
        isListening = true;
        silenceDetectionActive = false;
        currentSilenceFrames = 0;
        
        if (debugOutput)
            Debug.Log("AIControl: Starting to listen for speech...");
        
        
        whisperMicrophone.StartRecording();
        
        
        StartCoroutine(EnableSilenceDetectionAfterDelay());
    }
    
    IEnumerator EnableSilenceDetectionAfterDelay()
    {
        yield return new WaitForSeconds(0.5f); 
        silenceDetectionActive = true;
        
        if (debugOutput)
            Debug.Log("AIControl: Silence detection enabled.");
    }
    
    void DetectSilenceAndStop()
    {
        if (whisperMicrophone.recordingBuffer == null || whisperMicrophone.recordingBuffer.Count == 0)
            return;
        
        
        float recentAmplitude = 0f;
        int samplesToCheck = Mathf.Min(1024, whisperMicrophone.recordingBuffer.Count);
        int startIndex = whisperMicrophone.recordingBuffer.Count - samplesToCheck;
        
        for (int i = startIndex; i < whisperMicrophone.recordingBuffer.Count; i++)
        {
            recentAmplitude = Mathf.Max(recentAmplitude, Mathf.Abs(whisperMicrophone.recordingBuffer[i]));
        }
        
        
        if (recentAmplitude < silenceThreshold)
        {
            currentSilenceFrames++;
            
            if (currentSilenceFrames >= silenceFrames)
            {
                if (debugOutput)
                    Debug.Log($"AIControl: Silence detected for {silenceFrames} frames. Stopping recording...");
                
                StopListening();
            }
        }
        else
        {
            
            currentSilenceFrames = 0;
        }
    }
    
    public void StopListening()
    {
        if (!isListening) return;
        
        isListening = false;
        silenceDetectionActive = false;
        isProcessing = true;
        
        if (debugOutput)
            Debug.Log("AIControl: Stopping recording and processing speech...");
        
        
        whisperMicrophone.StopRecording();
    }
    
    void OnTranscriptionReceived(string transcribedText)
    {
        
        lastTranscribedText = transcribedText;
        
        if (debugOutput)
        {
            Debug.Log($"AIControl: Transcription received: '{transcribedText}'");
        }
        
        
        ProcessTranscribedText(transcribedText);
    }
    
    void ProcessTranscribedText(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            if (debugOutput)
                Debug.LogWarning("AIControl: Received empty transcription, skipping TTS.");
            
            isProcessing = false;
            return;
        }
        
        
        if (debugOutput)
            Debug.Log($"AIControl: Sending text to TTS: '{text}'");


        string actualtext = "This is a test";
        StartCoroutine(RunTextToSpeech(actualtext));
    }
    
    IEnumerator RunTextToSpeech(string text)
    {
        
        textToSpeech.inputText = text;
        
        
        textToSpeech.TextToSpeech();
        
        
        yield return null;
        
        
        yield return new WaitForSeconds(1.0f); 
        
        if (debugOutput)
            Debug.Log("AIControl: TTS processing completed.");
        
        
        isProcessing = false;
    }
    
    
    public void SetSilenceThreshold(float threshold)
    {
        silenceThreshold = Mathf.Clamp(threshold, 0.001f, 0.1f);
    }
    
    public void SetSilenceFrames(int frames)
    {
        silenceFrames = Mathf.Clamp(frames, 30, 300);
    }
    
    public string GetLastTranscribedText()
    {
        return lastTranscribedText;
    }
    
    public bool IsListening()
    {
        return isListening;
    }
    
    public bool IsProcessing()
    {
        return isProcessing;
    }
    
    
    void OnGUI()
    {
        if (!enablePipeline) return;
        
        GUILayout.BeginArea(new Rect(10, 450, 400, 300));
        
        GUILayout.Label("=== AI Control Pipeline ===");
        GUILayout.Label($"Status: {GetStatusString()}");
        GUILayout.Label($"Silence Threshold: {silenceThreshold:F3}");
        GUILayout.Label($"Silence Frames: {silenceFrames}");
        
        if (silenceDetectionActive && isListening)
        {
            GUILayout.Label($"Current Silence Frames: {currentSilenceFrames}");
            
            
            if (whisperMicrophone.recordingBuffer != null && whisperMicrophone.recordingBuffer.Count > 0)
            {
                float recentAmplitude = 0f;
                int samplesToCheck = Mathf.Min(1024, whisperMicrophone.recordingBuffer.Count);
                int startIndex = whisperMicrophone.recordingBuffer.Count - samplesToCheck;
                
                for (int i = startIndex; i < whisperMicrophone.recordingBuffer.Count; i++)
                {
                    recentAmplitude = Mathf.Max(recentAmplitude, Mathf.Abs(whisperMicrophone.recordingBuffer[i]));
                }
                
                GUILayout.Label($"Audio Level: {recentAmplitude:F4} {(recentAmplitude < silenceThreshold ? "(SILENT)" : "(SOUND)")}");
            }
        }
        
        GUILayout.Space(10);
        
        if (!string.IsNullOrEmpty(lastTranscribedText))
        {
            GUILayout.Label("Last Transcription:");
            GUILayout.TextArea(lastTranscribedText, GUILayout.Height(60));
        }
        
        GUILayout.Space(10);
        
        if (!isListening && !isProcessing)
        {
            if (GUILayout.Button("Start Listening (T)"))
            {
                StartListening();
            }
        }
        else if (isListening)
        {
            if (GUILayout.Button("Stop Listening"))
            {
                StopListening();
            }
        }
        
        GUILayout.Space(5);
        GUILayout.Label("Press 'T' to start voice input");
        GUILayout.Label("Recording will auto-stop on silence");
        
        GUILayout.EndArea();
    }
    
    string GetStatusString()
    {
        if (isListening)
            return silenceDetectionActive ? "Listening (with silence detection)" : "Listening (warming up)";
        else if (isProcessing)
            return "Processing...";
        else
            return "Ready";
    }
    
    void OnDestroy()
    {
        
        if (whisperMicrophone != null)
        {
            whisperMicrophone.OnTranscriptionComplete -= OnTranscriptionReceived;
        }
    }
}