using System;
using System.Collections.Generic;
using UnityEngine;
using Unity.InferenceEngine;
using System.Text;
using Unity.Collections;
using Newtonsoft.Json;
using System.Collections;

public class RunWhisperMicrophone : MonoBehaviour
{
    [Header("Audio Settings")]
    public int sampleRate = 16000;
    public float recordingDuration = 10.0f; 
    public bool continuousMode = false; 
    
    [Header("Model Assets")]
    public ModelAsset audioDecoder1, audioDecoder2;
    public ModelAsset audioEncoder;
    public ModelAsset logMelSpectro;
    public TextAsset vocabAsset;
    
    [Header("Transcription Settings")]
    public bool translateToEnglish = false;
    public bool includeTimestamps = false;
    public List<float> recordingBuffer { get; private set; }

    
    
    Worker decoder1, decoder2, encoder, spectrogram;
    Worker argmax;
    
    
    private AudioClip micClip;
    private Queue<float> audioBuffer;
    private bool isRecording = false;
    private bool isTranscribing = false;
    private float recordingStartTime;
    private int lastMicPosition = 0;
    
    
    const int maxTokens = 100;
    const int maxSamples = 30 * 16000; 
    
    
    const int END_OF_TEXT = 50257;
    const int START_OF_TRANSCRIPT = 50258;
    const int ENGLISH = 50259;
    const int GERMAN = 50261;
    const int FRENCH = 50265;
    const int TRANSCRIBE = 50359;
    const int TRANSLATE = 50358;
    const int NO_TIME_STAMPS = 50363;
    const int START_TIME = 50364;
    
    
    int tokenCount = 0;
    NativeArray<int> outputTokens;
    int[] whiteSpaceCharacters = new int[256];
    string[] tokens;
    string outputString = "";
    bool transcribe = false;
    
    
    Tensor<float> encodedAudio;
    Tensor<int> tokensTensor;
    Tensor<int> lastTokenTensor;
    NativeArray<int> lastToken;
    Tensor<float> audioInput;
    
    public System.Action<string> OnTranscriptionComplete;
    
    void Start()
    {
        Debug.Log("=== Whisper Microphone Starting ===");
        
        SetupWhiteSpaceShifts();
        GetTokens();
        InitializeModels();
        InitializeAudio();
        
        Debug.Log("=== Whisper Microphone Ready ===");
    }
    
    void InitializeModels()
    {
        decoder1 = new Worker(ModelLoader.Load(audioDecoder1), BackendType.GPUCompute);
        decoder2 = new Worker(ModelLoader.Load(audioDecoder2), BackendType.GPUCompute);
        
        
        FunctionalGraph graph = new FunctionalGraph();
        var input = graph.AddInput(DataType.Float, new DynamicTensorShape(1, 1, 51865));
        var amax = Functional.ArgMax(input, -1, false);
        var selectTokenModel = graph.Compile(amax);
        argmax = new Worker(selectTokenModel, BackendType.GPUCompute);
        
        encoder = new Worker(ModelLoader.Load(audioEncoder), BackendType.GPUCompute);
        spectrogram = new Worker(ModelLoader.Load(logMelSpectro), BackendType.GPUCompute);
        
        outputTokens = new NativeArray<int>(maxTokens, Allocator.Persistent);
        lastToken = new NativeArray<int>(1, Allocator.Persistent);
    }
    
    void InitializeAudio()
    {
        audioBuffer = new Queue<float>();
        recordingBuffer = new List<float>();

        
        
        micClip = Microphone.Start(null, true, 30, sampleRate); 
        
        if (micClip == null)
        {
            Debug.LogError("Failed to start microphone");
            return;
        }
        
        Debug.Log($"Microphone started at {sampleRate}Hz");
        
        
        StartCoroutine(WaitForMicrophoneStart());
    }

    IEnumerator WaitForMicrophoneStart()
    {
        yield return new WaitForSeconds(0.1f);
        lastMicPosition = Microphone.GetPosition(null);
    }
        
    void Update()
    {
        if (micClip == null || !Microphone.IsRecording(null))
            return;
        
        if (Input.GetKeyDown(KeyCode.Space) && !isRecording && !isTranscribing)
        {
            StartRecording();
        }
        else if (Input.GetKeyUp(KeyCode.Space) && isRecording)
        {
            StopRecording();
        }
        
        
        if (isRecording && Time.time - recordingStartTime >= recordingDuration)
        {
            StopRecording();
        }
        
        
        if (continuousMode && !isRecording && !isTranscribing)
        {
            StartRecording();
        }
    }
    
    public void StartRecording()
    {
        if (isRecording || isTranscribing) return;
        
        Debug.Log("=== Starting Recording ===");
        isRecording = true;
        recordingStartTime = Time.time;
        
        
        recordingBuffer.Clear();
        outputString = "";
        
        
        lastMicPosition = Microphone.GetPosition(null);
    }

    public void StopRecording()
    {
        if (!isRecording) return;
        
        Debug.Log("=== Stopping Recording ===");
        isRecording = false;
        
        
        CaptureNewAudioData();
        
        if (recordingBuffer.Count > 0)
        {
            Debug.Log($"Captured {recordingBuffer.Count} samples for transcription");
            StartCoroutine(TranscribeAudio());
        }
        else
        {
            Debug.LogWarning("No audio data captured");
        }
    }

    void FixedUpdate()
    {
        if (!isRecording) return;
        
        CaptureNewAudioData();
    }

    void CaptureNewAudioData()
    {
        if (!Microphone.IsRecording(null)) return;
        
        int currentMicPosition = Microphone.GetPosition(null);
        
        
        int samplesToRead = 0;
        if (currentMicPosition > lastMicPosition)
        {
            
            samplesToRead = currentMicPosition - lastMicPosition;
        }
        else if (currentMicPosition < lastMicPosition)
        {
            
            samplesToRead = (micClip.samples - lastMicPosition) + currentMicPosition;
        }
        else
        {
            
            return;
        }
        
        if (samplesToRead <= 0) return;
        
        
        float[] newAudioData = new float[samplesToRead];
        
        if (currentMicPosition > lastMicPosition)
        {
            
            micClip.GetData(newAudioData, lastMicPosition);
        }
        else
        {
            
            int firstPartSize = micClip.samples - lastMicPosition;
            int secondPartSize = currentMicPosition;
            
            float[] tempBuffer = new float[micClip.samples];
            micClip.GetData(tempBuffer, 0);
            
            
            Array.Copy(tempBuffer, lastMicPosition, newAudioData, 0, firstPartSize);
            
            
            if (secondPartSize > 0)
            {
                Array.Copy(tempBuffer, 0, newAudioData, firstPartSize, secondPartSize);
            }
        }
        
        
        recordingBuffer.AddRange(newAudioData);
        
        
        int maxRecordingSamples = (int)(recordingDuration * sampleRate * 2); 
        if (recordingBuffer.Count > maxRecordingSamples)
        {
            int excessSamples = recordingBuffer.Count - maxRecordingSamples;
            recordingBuffer.RemoveRange(0, excessSamples);
        }
        
        lastMicPosition = currentMicPosition;
    }

    IEnumerator PrepareAudioForTranscription()
    {
        float[] audioData = new float[maxSamples];
        int sampleCount = Mathf.Min(recordingBuffer.Count, maxSamples);
        
        Debug.Log($"Preparing {sampleCount} samples from recording buffer of {recordingBuffer.Count} samples");
        
        
        for (int i = 0; i < sampleCount; i++)
        {
            audioData[i] = recordingBuffer[i];
        }
        
        
        for (int i = sampleCount; i < maxSamples; i++)
        {
            audioData[i] = 0.0f;
        }
        
        
        
        float maxAmplitude = 0f;
        for (int i = 0; i < sampleCount; i++)
        {
            maxAmplitude = Mathf.Max(maxAmplitude, Mathf.Abs(audioData[i]));
        }
        
        if (maxAmplitude > 0.001f) 
        {
            float normalizationFactor = 0.95f / maxAmplitude; 
            for (int i = 0; i < sampleCount; i++)
            {
                audioData[i] *= normalizationFactor;
            }
            Debug.Log($"Audio normalized with factor: {normalizationFactor}");
        }
        else
        {
            Debug.LogWarning("Audio signal is too quiet or silent");
        }
        
        audioInput = new Tensor<float>(new TensorShape(1, maxSamples), audioData);
        yield return null;
    }    
    IEnumerator TranscribeAudio()
    {
        if (isTranscribing) yield break;
        
        Debug.Log($"=== Starting Transcription === Buffer size: {audioBuffer.Count} samples");
        isTranscribing = true;
        
        
        yield return StartCoroutine(PrepareAudioForTranscription());
        
        
        yield return StartCoroutine(EncodeAudio());
        
        
        SetupTranscriptionTokens();
        
        
        yield return StartCoroutine(RunTranscriptionLoop());
        
        
        CompleteTranscription();
        
        isTranscribing = false;
        
        if (continuousMode && !isRecording)
        {
            yield return new WaitForSeconds(0.5f); 
        }
    }
    
    IEnumerator EncodeAudio()
    {
        Debug.Log("=== Encoding Audio ===");
        
        
        spectrogram.Schedule(audioInput);
        
        bool spectrogramComplete = false;
        while (!spectrogramComplete)
        {
            yield return null;
            var spectrogramOutput = spectrogram.PeekOutput();
            if (spectrogramOutput != null)
            {
                spectrogramComplete = true;
            }
        }
        
        var logmel = spectrogram.PeekOutput() as Tensor<float>;
        
        
        encoder.Schedule(logmel);
        
        bool encodingComplete = false;
        while (!encodingComplete)
        {
            yield return null;
            var encoderOutput = encoder.PeekOutput();
            if (encoderOutput != null)
            {
                encodingComplete = true;
                encodedAudio = encoderOutput as Tensor<float>;
            }
        }
        
        Debug.Log("=== Audio Encoding Complete ===");
    }
    
    void SetupTranscriptionTokens()
    {
        
        outputTokens[0] = START_OF_TRANSCRIPT;
        outputTokens[1] = ENGLISH;
        outputTokens[2] = translateToEnglish ? TRANSLATE : TRANSCRIBE;
        
        tokenCount = 3;
        
        if (!includeTimestamps)
        {
            
        }
        
        
        tokensTensor = new Tensor<int>(new TensorShape(1, maxTokens));
        ComputeTensorData.Pin(tokensTensor);
        tokensTensor.Reshape(new TensorShape(1, tokenCount));
        tokensTensor.dataOnBackend.Upload<int>(outputTokens, tokenCount);
        
        lastToken[0] = includeTimestamps ? START_TIME : NO_TIME_STAMPS;
        lastTokenTensor = new Tensor<int>(new TensorShape(1, 1), new[] { lastToken[0] });
        
        transcribe = true;
        outputString = "";
    }
    
    IEnumerator RunTranscriptionLoop()
    {
        Debug.Log("=== Starting Transcription Loop ===");
        
        while (transcribe && tokenCount < (outputTokens.Length - 1))
        {
            yield return StartCoroutine(InferenceStep());
        }
        
        Debug.Log("=== Transcription Loop Complete ===");
    }

    IEnumerator InferenceStep()
    {
        
        decoder1.SetInput("input_ids", tokensTensor);
        decoder1.SetInput("encoder_hidden_states", encodedAudio);
        decoder1.Schedule();

        bool decoder1Complete = false;
        while (!decoder1Complete)
        {
            yield return null;
            if (decoder1.PeekOutput("present.0.decoder.key") != null)
            {
                decoder1Complete = true;
            }
        }

        
        var past_key_values_0_decoder_key = decoder1.PeekOutput("present.0.decoder.key") as Tensor<float>;
        var past_key_values_0_decoder_value = decoder1.PeekOutput("present.0.decoder.value") as Tensor<float>;
        var past_key_values_1_decoder_key = decoder1.PeekOutput("present.1.decoder.key") as Tensor<float>;
        var past_key_values_1_decoder_value = decoder1.PeekOutput("present.1.decoder.value") as Tensor<float>;
        var past_key_values_2_decoder_key = decoder1.PeekOutput("present.2.decoder.key") as Tensor<float>;
        var past_key_values_2_decoder_value = decoder1.PeekOutput("present.2.decoder.value") as Tensor<float>;
        var past_key_values_3_decoder_key = decoder1.PeekOutput("present.3.decoder.key") as Tensor<float>;
        var past_key_values_3_decoder_value = decoder1.PeekOutput("present.3.decoder.value") as Tensor<float>;

        var past_key_values_0_encoder_key = decoder1.PeekOutput("present.0.encoder.key") as Tensor<float>;
        var past_key_values_0_encoder_value = decoder1.PeekOutput("present.0.encoder.value") as Tensor<float>;
        var past_key_values_1_encoder_key = decoder1.PeekOutput("present.1.encoder.key") as Tensor<float>;
        var past_key_values_1_encoder_value = decoder1.PeekOutput("present.1.encoder.value") as Tensor<float>;
        var past_key_values_2_encoder_key = decoder1.PeekOutput("present.2.encoder.key") as Tensor<float>;
        var past_key_values_2_encoder_value = decoder1.PeekOutput("present.2.encoder.value") as Tensor<float>;
        var past_key_values_3_encoder_key = decoder1.PeekOutput("present.3.encoder.key") as Tensor<float>;
        var past_key_values_3_encoder_value = decoder1.PeekOutput("present.3.encoder.value") as Tensor<float>;

        
        decoder2.SetInput("input_ids", lastTokenTensor);
        decoder2.SetInput("past_key_values.0.decoder.key", past_key_values_0_decoder_key);
        decoder2.SetInput("past_key_values.0.decoder.value", past_key_values_0_decoder_value);
        decoder2.SetInput("past_key_values.1.decoder.key", past_key_values_1_decoder_key);
        decoder2.SetInput("past_key_values.1.decoder.value", past_key_values_1_decoder_value);
        decoder2.SetInput("past_key_values.2.decoder.key", past_key_values_2_decoder_key);
        decoder2.SetInput("past_key_values.2.decoder.value", past_key_values_2_decoder_value);
        decoder2.SetInput("past_key_values.3.decoder.key", past_key_values_3_decoder_key);
        decoder2.SetInput("past_key_values.3.decoder.value", past_key_values_3_decoder_value);

        decoder2.SetInput("past_key_values.0.encoder.key", past_key_values_0_encoder_key);
        decoder2.SetInput("past_key_values.0.encoder.value", past_key_values_0_encoder_value);
        decoder2.SetInput("past_key_values.1.encoder.key", past_key_values_1_encoder_key);
        decoder2.SetInput("past_key_values.1.encoder.value", past_key_values_1_encoder_value);
        decoder2.SetInput("past_key_values.2.encoder.key", past_key_values_2_encoder_key);
        decoder2.SetInput("past_key_values.2.encoder.value", past_key_values_2_encoder_value);
        decoder2.SetInput("past_key_values.3.encoder.key", past_key_values_3_encoder_key);
        decoder2.SetInput("past_key_values.3.encoder.value", past_key_values_3_encoder_value);

        decoder2.Schedule();

        bool decoder2Complete = false;
        while (!decoder2Complete)
        {
            yield return null;
            if (decoder2.PeekOutput("logits") != null)
            {
                decoder2Complete = true;
            }
        }

        var logits = decoder2.PeekOutput("logits") as Tensor<float>;
        argmax.Schedule(logits);

        bool argmaxComplete = false;
        while (!argmaxComplete)
        {
            yield return null;
            if (argmax.PeekOutput() != null)
            {
                argmaxComplete = true;
            }
        }

        
        var t_Token_GPU = argmax.PeekOutput() as Tensor<int>;
        var t_Token = t_Token_GPU.ReadbackAndClone();
        int index = t_Token[0];

        
        t_Token.Dispose();

        
        outputTokens[tokenCount] = lastToken[0];
        lastToken[0] = index;
        tokenCount++;
        tokensTensor.Reshape(new TensorShape(1, tokenCount));
        tokensTensor.dataOnBackend.Upload<int>(outputTokens, tokenCount);
        lastTokenTensor.dataOnBackend.Upload<int>(lastToken, 1);

        if (index == END_OF_TEXT)
        {
            transcribe = false;
        }
        else if (index < tokens.Length)
        {
            string tokenText = GetUnicodeText(tokens[index]);
            outputString += tokenText;
            Debug.Log($"Token: {tokenText} | Full: {outputString}");
        }
    }
    void CompleteTranscription()
    {
        Debug.Log($"=== TRANSCRIPTION COMPLETE === Result: '{outputString.Trim()}'");
        OnTranscriptionComplete?.Invoke(outputString.Trim());
        
        
        if (tokensTensor != null)
        {
            tokensTensor.Dispose();
            tokensTensor = null;
        }
        
        if (lastTokenTensor != null)
        {
            lastTokenTensor.Dispose();
            lastTokenTensor = null;
        }
        
        if (audioInput != null)
        {
            audioInput.Dispose();
            audioInput = null;
        }
    }
    
    
    void GetTokens()
    {
        var vocab = JsonConvert.DeserializeObject<Dictionary<string, int>>(vocabAsset.text);
        tokens = new string[vocab.Count];
        foreach (var item in vocab)
        {
            tokens[item.Value] = item.Key;
        }
    }
    
    string GetUnicodeText(string text)
    {
        var bytes = Encoding.GetEncoding("ISO-8859-1").GetBytes(ShiftCharacterDown(text));
        return Encoding.UTF8.GetString(bytes);
    }
    
    string ShiftCharacterDown(string text)
    {
        string outText = "";
        foreach (char letter in text)
        {
            outText += ((int)letter <= 256) ? letter : (char)whiteSpaceCharacters[(int)(letter - 256)];
        }
        return outText;
    }
    
    void SetupWhiteSpaceShifts()
    {
        for (int i = 0, n = 0; i < 256; i++)
        {
            if (IsWhiteSpace((char)i)) whiteSpaceCharacters[n++] = i;
        }
    }
    
    bool IsWhiteSpace(char c)
    {
        return !(('!' <= c && c <= '~') || ('¡' <= c && c <= '¬') || ('®' <= c && c <= 'ÿ'));
    }
    
    
    void OnDestroy()
    {
        
        decoder1?.Dispose();
        decoder2?.Dispose();
        encoder?.Dispose();
        spectrogram?.Dispose();
        argmax?.Dispose();
        
        
        audioInput?.Dispose();
        lastTokenTensor?.Dispose();
        tokensTensor?.Dispose();
        encodedAudio?.Dispose();
        
        
        if (outputTokens.IsCreated)
            outputTokens.Dispose();
        if (lastToken.IsCreated)
            lastToken.Dispose();
        
        
        Microphone.End(null);
    }
}