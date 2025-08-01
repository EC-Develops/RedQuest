using System.Collections;
using UnityEngine;

public class AIControl : MonoBehaviour
{
    [Header("Component References")]
    public RunWhisperMicrophone whisperMicrophone;
    public RunJets textToSpeech;
    
    [Header("Animation References")]
    public Animator spriteAnimator;
    public Transform spriteTransform;
    public Transform playerTransform;
    
    [Header("Movement Settings - Actions.cs Style")]
    public float walkSpeed = 2f;
    public float stopDistance = 2f; 
    
    [Header("Animation Settings")]
    public string idleBooleanName = "Idle_Boolean";
    public string walkBooleanName = "Walk_Boolean";
    public string talkBooleanName = "Talk_Boolean";
    
    [Header("Silence Detection Settings")]
    [Range(0.001f, 0.1f)]
    public float silenceThreshold = 0.01f;
    [Range(30, 300)]
    public int silenceFrames = 120; 
    [Range(0.1f, 1.0f)]
    public float silenceCheckInterval = 0.1f;
    
    [Header("Pipeline Settings")]
    public bool enablePipeline = true;
    public bool debugOutput = true;
    
    private bool isListening = false;
    private bool isProcessing = false;
    private bool isWalkingToPlayer = false;
    private bool isTalking = false;
    private string lastTranscribedText = "";
    
    private int currentSilenceFrames = 0;
    private bool silenceDetectionActive = false;
    
    
    private float lastSilenceCheckTime = 0f;
    private float lastAmplitude = 0f;
    
    
    private enum AnimationState
    {
        Idle,
        Walk,
        Talk
    }
    
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
        
        
        if (playerTransform == null)
        {
            if (Camera.main != null)
            {
                playerTransform = Camera.main.transform;
                if (debugOutput)
                    Debug.Log("AIControl: Using main camera as player transform");
            }
            else
            {
                GameObject player = GameObject.FindGameObjectWithTag("Player");
                if (player != null)
                    playerTransform = player.transform;
            }
        }
        
        
        if (spriteAnimator == null)
        {
            spriteAnimator = GetComponent<Animator>();
        }
        
        
        if (spriteTransform == null)
        {
            spriteTransform = transform;
        }
        
        
        whisperMicrophone.OnTranscriptionComplete += OnTranscriptionReceived;
        
        
        whisperMicrophone.continuousMode = false;
        
        
        SetAnimationState(AnimationState.Idle);
        
        Debug.Log("AIControl initialized successfully!");
    }
    
    void Update()
    {
        if (!enablePipeline) return;
        
        
        if (isWalkingToPlayer)
        {
            HandleWalkingMovement();
        }
        
        
        if (Input.GetKeyDown(KeyCode.T) && !isListening && !isProcessing)
        {
            StartListening();
        }
        
        
        if (isListening && silenceDetectionActive)
        {
            if (Time.time - lastSilenceCheckTime >= silenceCheckInterval)
            {
                DetectSilenceAndStop();
                lastSilenceCheckTime = Time.time;
            }
        }
    }
    
    
    void HandleWalkingMovement()
    {
        if (playerTransform == null) return;

        Vector3 targetPosition = playerTransform.position;
        targetPosition.y = spriteTransform.position.y; 

        float distance = Vector3.Distance(spriteTransform.position, targetPosition);
        
        
        float bufferDistance = stopDistance * 0.8f;
        
        if (distance > bufferDistance)
        {
            
            Vector3 directionToPlayer = (targetPosition - spriteTransform.position).normalized;
            if (directionToPlayer != Vector3.zero)
            {
                Quaternion targetRotation = Quaternion.LookRotation(directionToPlayer);
                spriteTransform.rotation = Quaternion.Slerp(spriteTransform.rotation, targetRotation, Time.deltaTime * 5f);
            }

            
            spriteTransform.position += directionToPlayer * walkSpeed * Time.deltaTime;
            
            if (debugOutput && Time.frameCount % 60 == 0) 
            {
                Debug.Log($"AIControl: Walking... Distance: {distance:F2}, Target: {bufferDistance:F2}");
            }
        }
        else
        {
            
            if (debugOutput)
                Debug.Log($"AIControl: Close enough! Distance: {distance:F2} <= {bufferDistance:F2}");
            
            StopWalkingToPlayer();
        }
    }
    
    
    bool IsPlayerTooFar()
    {
        if (playerTransform == null) 
        {
            Debug.LogWarning("⚠️ No player transform found!");
            return false;
        }
        
        Vector3 playerPosition = playerTransform.position;
        Vector3 npcPosition = spriteTransform.position;
        
        
        float distance = Vector3.Distance(npcPosition, playerPosition);
        
        bool tooFar = distance > stopDistance;
        
        if (debugOutput && (isProcessing || tooFar))
        {
            Debug.Log($"📏 NPC Position: {npcPosition} | Player Position: {playerPosition}");
            Debug.Log($"📏 Distance: {distance:F2} | Stop Distance: {stopDistance} | Too Far: {tooFar}");
        }
        
        return tooFar;
    }
    
    public void StartListening()
    {
        if (isListening || isProcessing)
        {
            if (debugOutput)
                Debug.LogWarning("AIControl: Already listening or processing, ignoring request.");
            return;
        }
        
        
        ClearAudioResources();
        
        isListening = true;
        silenceDetectionActive = false;
        currentSilenceFrames = 0;
        lastAmplitude = 0f;
        lastSilenceCheckTime = Time.time;
        
        if (debugOutput)
            Debug.Log("AIControl: Starting to listen for speech...");
        
        
        if (IsPlayerTooFar())
        {
            if (debugOutput)
                Debug.Log("🚶 Player too far, walking first then listening");
            StartWalkingToPlayer();
        }
        else
        {
            if (debugOutput)
                Debug.Log("🎤 Player close enough, listening directly");
        }
        
        
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
        int samplesToCheck = Mathf.Min(512, whisperMicrophone.recordingBuffer.Count);
        int startIndex = Mathf.Max(0, whisperMicrophone.recordingBuffer.Count - samplesToCheck);

        
        for (int i = startIndex; i < whisperMicrophone.recordingBuffer.Count; i++)
        {
            float sample = Mathf.Abs(whisperMicrophone.recordingBuffer[i]);
            if (sample > recentAmplitude)
                recentAmplitude = sample;
        }

        lastAmplitude = recentAmplitude;

        
        if (recentAmplitude < silenceThreshold)
        {
            currentSilenceFrames++;

            
            float targetSilenceTime = silenceFrames / 60f; 
            int requiredSilenceChecks = Mathf.RoundToInt(targetSilenceTime / silenceCheckInterval);

            if (debugOutput && currentSilenceFrames % 10 == 0) 
            {
                Debug.Log($"Silence detected: {currentSilenceFrames}/{requiredSilenceChecks} checks, amplitude: {recentAmplitude:F4}");
            }

            if (currentSilenceFrames >= requiredSilenceChecks)
            {
                if (debugOutput)
                    Debug.Log($"Silence threshold reached. Stopping recording after {currentSilenceFrames} silence checks.");

                StopListening();
            }
        }
        else
        {
            
            if (currentSilenceFrames > 0 && debugOutput)
            {
                Debug.Log($"Sound detected, resetting silence counter. Amplitude: {recentAmplitude:F4}");
            }
            currentSilenceFrames = 0;
        }
    }
    
    public void StopListening()
    {
        if (!isListening) return;
        
        isListening = false;
        silenceDetectionActive = false;
        isProcessing = true;
        
        
        StopWalkingToPlayer();
        
        SetAnimationState(AnimationState.Talk);
        isTalking = true;
        
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
            
            
            CleanupAndReturnToIdle();
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
        
        
        CleanupAndReturnToIdle();
    }
    
    private void CleanupAndReturnToIdle()
    {
        
        ClearAudioResources();
        
        
        SetAnimationState(AnimationState.Idle);
        isTalking = false;
        isProcessing = false;
        
        
        System.GC.Collect();
    }
    
    private void ClearAudioResources()
    {
        
        if (whisperMicrophone != null && whisperMicrophone.recordingBuffer != null)
        {
            try
            {
                whisperMicrophone.recordingBuffer.Clear();
            }
            catch (System.Exception e)
            {
                if (debugOutput)
                    Debug.LogWarning($"AIControl: Could not clear recording buffer: {e.Message}");
            }
        }
        
        
        lastAmplitude = 0f;
        currentSilenceFrames = 0;
    }
    
    
    private void SetAnimationState(AnimationState state)
    {
        if (spriteAnimator == null) return;
        
        
        spriteAnimator.SetBool(idleBooleanName, true);
        spriteAnimator.SetBool(walkBooleanName, false);
        spriteAnimator.SetBool(talkBooleanName, false);
        
        
        switch (state)
        {
            case AnimationState.Idle:
                spriteAnimator.SetBool(idleBooleanName, true);
                break;
            case AnimationState.Walk:
                spriteAnimator.SetBool(walkBooleanName, true);
                break;
            case AnimationState.Talk:
                spriteAnimator.SetBool(talkBooleanName, true);
                break;
        }
        
        if (debugOutput)
            Debug.Log($"AIControl: Animation state changed to {state}");
    }
    
    private void StartWalkingToPlayer()
    {
        if (playerTransform == null)
        {
            if (debugOutput)
                Debug.LogWarning("AIControl: Player transform not found, cannot walk to player.");
            return;
        }
        
        isWalkingToPlayer = true;
        SetAnimationState(AnimationState.Walk);
        
        if (debugOutput)
            Debug.Log("AIControl: Started walking toward player.");
    }
    
    private void StopWalkingToPlayer()
    {
        isWalkingToPlayer = false;
        
        spriteAnimator.SetBool(idleBooleanName, true);
        spriteAnimator.SetBool(walkBooleanName, false);
        spriteAnimator.SetBool(talkBooleanName, false);

        if (debugOutput)
            Debug.Log("AIControl: Stopped walking toward player.");
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
        
        
        if (Time.frameCount % 10 != 0 && !isListening) return;
        
        GUILayout.BeginArea(new Rect(10, 450, 400, 350));
        
        GUILayout.Label("=== AI Control Pipeline ===");
        GUILayout.Label($"Status: {GetStatusString()}");
        GUILayout.Label($"Silence Threshold: {silenceThreshold:F3}");
        GUILayout.Label($"Silence Frames: {silenceFrames}");
        GUILayout.Label($"Walking to Player: {isWalkingToPlayer}");
        GUILayout.Label($"Talking: {isTalking}");
        
        if (silenceDetectionActive && isListening)
        {
            GUILayout.Label($"Current Silence Frames: {currentSilenceFrames}");
            GUILayout.Label($"Audio Level: {lastAmplitude:F4} {(lastAmplitude < silenceThreshold ? "(SILENT)" : "(SOUND)")}");
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
        if (isWalkingToPlayer && isListening)
            return "Walking & Listening";
        else if (isListening)
            return silenceDetectionActive ? "Listening (with silence detection)" : "Listening (warming up)";
        else if (isTalking && isProcessing)
            return "Talking...";
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
        
        
        ClearAudioResources();
    }
    
    
    void OnApplicationPause(bool pauseStatus)
    {
        if (pauseStatus && isListening)
        {
            StopListening();
        }
    }
    
    void OnApplicationFocus(bool hasFocus)
    {
        if (!hasFocus && isListening)
        {
            StopListening();
        }
    }
}