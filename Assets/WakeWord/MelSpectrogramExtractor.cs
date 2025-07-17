using UnityEngine;
using Unity.InferenceEngine;
using System;
using System.Collections.Generic;

public class WakeWordDetector : MonoBehaviour
{
    [Header("Audio Settings")]
    public int sampleRate = 16000;
    public int frameSize = 1280;      // 80ms frames (1280 samples at 16kHz)
    public int hopSize = 160;         // 10ms hop
    
    [Header("Model Assets")]
    public ModelAsset melModelAsset;        
    public ModelAsset embeddingModelAsset;  
    public ModelAsset classifierModelAsset; 
    
    [Header("Detection Settings")]
    public float threshold = 0.5f;
    public int patienceFrames = 3;          // consecutive frames above threshold
    public float debounceTime = 1.0f;       // Seconds to wait before next detection
    
    [Header("Pipeline Settings")]
    public int requiredMelFrames = 76;      
    public int requiredEmbedFrames = 16;    
    private Queue<float[]> melFrameBuffer;  
    private Queue<float[]> embeddingFrameBuffer; 
    
    private Model melModel, embeddingModel, classifierModel;
    private Worker melWorker, embeddingWorker, classifierWorker;
    private AudioClip micClip;
    private int lastSamplePosition = 0;
    private float[] audioBuffer;
    private Queue<float[]> audioQueue;
    private List<float> recentPredictions;
    private float lastDetectionTime;
    private bool isProcessing = false;
    
    void Start()
    {
        Debug.Log("=== WakeWord Detector Starting ===");
        
        InitializeAudio();
        LoadModels();
        InitializeDetectionState();
        
        Debug.Log("=== WakeWord Detector Started Successfully ===");
    }
    
    void InitializeAudio()
    {
        micClip = Microphone.Start(null, true, 2, sampleRate);
        audioBuffer = new float[frameSize];
        audioQueue = new Queue<float[]>();
        
        if (micClip == null)
        {
            Debug.LogError("Failed to start microphone");
            return;
        }
        
        Debug.Log($"Microphone started. Frame size: {frameSize} samples ({frameSize / (float)sampleRate * 1000:F1}ms)");
    }
    
    void LoadModels()
    {
        if (melModelAsset == null)
        {
            Debug.LogError("Mel model asset is null!");
            return;
        }
        melModel = ModelLoader.Load(melModelAsset);
        melWorker = new Worker(melModel, BackendType.CPU);
        Debug.Log($"Mel model loaded - Inputs: {string.Join(", ", melModel.inputs)}, Outputs: {string.Join(", ", melModel.outputs)}");
        
        if (embeddingModelAsset == null)
        {
            Debug.LogError("Embedding model asset is null!");
            return;
        }
        embeddingModel = ModelLoader.Load(embeddingModelAsset);
        embeddingWorker = new Worker(embeddingModel, BackendType.CPU);
        Debug.Log($"Embedding model loaded - Inputs: {string.Join(", ", embeddingModel.inputs)}, Outputs: {string.Join(", ", embeddingModel.outputs)}");
        
        if (classifierModelAsset == null)
        {
            Debug.LogError("Classifier model asset is null!");
            return;
        }
        classifierModel = ModelLoader.Load(classifierModelAsset);
        classifierWorker = new Worker(classifierModel, BackendType.CPU);
        Debug.Log($"Classifier model loaded - Inputs: {string.Join(", ", classifierModel.inputs)}, Outputs: {string.Join(", ", classifierModel.outputs)}");
    }
    
    void InitializeDetectionState()
    {
        recentPredictions = new List<float>();
        lastDetectionTime = -debounceTime; 
        melFrameBuffer = new Queue<float[]>();
        embeddingFrameBuffer = new Queue<float[]>();
    }
    
    void Update()
    {
        if (micClip == null || !Microphone.IsRecording(null) || isProcessing)
            return;
            
        ProcessAudioStream();
        
        // periodic status updates
        if (Time.time % 2.0f < 0.1f) // Every 2 seconds
        {
            Debug.Log($"=== STATUS === Mel: {melFrameBuffer?.Count ?? 0}/{requiredMelFrames}, Embedding: {embeddingFrameBuffer?.Count ?? 0}/{requiredEmbedFrames}, Processing: {isProcessing}");
        }
    }
    
    void ProcessAudioStream()
    {
        int micPosition = Microphone.GetPosition(null);
        int samplesAvailable = micPosition - lastSamplePosition;
        
        // wrap-around
        if (samplesAvailable < 0)
            samplesAvailable += micClip.samples;
        
        if (samplesAvailable >= frameSize)
        {
            micClip.GetData(audioBuffer, lastSamplePosition);
            ProcessAudioFrame(audioBuffer);
            lastSamplePosition = (lastSamplePosition + frameSize) % micClip.samples;
        }
    }
    
    void ProcessAudioFrame(float[] audioFrame)
    {
        if (isProcessing) return;
        
        StartCoroutine(ProcessAudioFrameAsync(audioFrame));
    }
    System.Collections.IEnumerator ProcessAudioFrameAsync(float[] audioFrame)
    {
        isProcessing = true;
        
        Tensor<float> melOutput = null;
        var melCoroutine = GenerateMelSpectrogramAsync(audioFrame, result => melOutput = result);
        yield return StartCoroutine(melCoroutine);
        
        if (melOutput == null)
        {
            Debug.LogError("Failed to generate mel spectrogram");
            isProcessing = false;
            yield break;
        }
        
        float[] melFeatures = ExtractMelFeatures(melOutput);
        melOutput.Dispose();
        
        if (melFeatures == null)
        {
            Debug.LogError("Failed to extract mel features");
            isProcessing = false;
            yield break;
        }
        
        melFrameBuffer.Enqueue(melFeatures);
        
        while (melFrameBuffer.Count > requiredMelFrames)
        {
            melFrameBuffer.Dequeue();
        }
        
        if (melFrameBuffer.Count < requiredMelFrames)
        {
            Debug.Log($"Accumulating mel frames: {melFrameBuffer.Count}/{requiredMelFrames}");
            isProcessing = false;
            yield break;
        }

        Tensor<float> embeddingOutput = null;
        var embeddingCoroutine = GenerateEmbeddingsAsync(result => embeddingOutput = result);
        yield return StartCoroutine(embeddingCoroutine);
        
        if (embeddingOutput == null)
        {
            Debug.LogError("Failed to generate embeddings");
            isProcessing = false;
            yield break;
        }

        float[] embeddingFeatures = ExtractEmbeddingFeatures(embeddingOutput);
        embeddingOutput.Dispose();
        
        if (embeddingFeatures == null)
        {
            Debug.LogError("Failed to extract embedding features");
            isProcessing = false;
            yield break;
        }
        
        embeddingFrameBuffer.Enqueue(embeddingFeatures);
        
        while (embeddingFrameBuffer.Count > requiredEmbedFrames)
        {
            embeddingFrameBuffer.Dequeue();
        }
        
        if (embeddingFrameBuffer.Count < requiredEmbedFrames)
        {
            Debug.Log($"Accumulating embedding frames: {embeddingFrameBuffer.Count}/{requiredEmbedFrames}");
            isProcessing = false;
            yield break;
        }
        
        float prediction = 0f;
        var classifyCoroutine = ClassifyEmbeddingsAsync(result => prediction = result);
        yield return StartCoroutine(classifyCoroutine);
        
        ProcessDetectionResult(prediction);
        
        isProcessing = false;
    }

    System.Collections.IEnumerator GenerateMelSpectrogramAsync(float[] audioFrame, System.Action<Tensor<float>> callback)
    {
        Tensor<float> audioInput = null;
        Tensor<float> melOutput = null;
        bool hasError = false;
        
        audioInput = new Tensor<float>(new TensorShape(1, frameSize), audioFrame);
        
        if (audioInput == null)
        {
            Debug.LogError("Failed to create audio input tensor");
            callback(null);
            yield break;
        }
        
        melWorker.Schedule(audioInput);
        
        bool isComplete = false;
        while (!isComplete)
        {
            yield return null; 
            
            var output = melWorker.PeekOutput();
            if (output != null)
            {
                isComplete = true;
            }
        }
        
        melOutput = melWorker.PeekOutput() as Tensor<float>;
        
        if (melOutput != null)
        {
            Debug.Log($"Mel spectrogram generated - Shape: {melOutput.shape} (time={melOutput.shape[0]}, dim2={melOutput.shape[2]})");
            callback(melOutput);
        }
        else
        {
            Debug.LogError("Mel spectrogram output is null");
            callback(null);
        }
        
        if (audioInput != null)
            audioInput.Dispose();
    }

    float[] ExtractMelFeatures(Tensor<float> melOutput)
    {
        if (melOutput == null)
        {
            Debug.LogError("Mel output tensor is null");
            return null;
        }
        
        try
        {
            // Mel output shape: [time, 1, Clipoutput_dim_2, 32]
            // We need to extract all the features from this frame
            int timeFrames = melOutput.shape[0];
            int dim2 = melOutput.shape[2];
            int melBands = 32;
            
            // For each time frame, we get dim2 * 32 features
            // But we need to flatten them into a single feature vector per time frame
            float[] features = new float[dim2 * melBands];
            
            // Take the first time frame and flatten the features
            for (int d = 0; d < dim2; d++)
            {
                for (int b = 0; b < melBands; b++)
                {
                    int melIndex = 0 * (1 * dim2 * melBands) + 0 * (dim2 * melBands) + d * melBands + b;
                    features[d * melBands + b] = melOutput[melIndex];
                }
            }
            
            Debug.Log($"Extracted {features.Length} mel features from shape {melOutput.shape}");
            return features;
        }
        catch (System.Exception e)
        {
            Debug.LogError($"Error extracting mel features: {e.Message}");
            return null;
        }
    }

    System.Collections.IEnumerator GenerateEmbeddingsAsync(System.Action<Tensor<float>> callback)
    {
        Tensor<float> embeddingInput = null;
        Tensor<float> embeddingOutput = null;
        
        // Convert mel frame buffer to tensor for embedding model
        // Target shape: [1, 76, 32, 1] (batch_size=1, frames=76, features=32, channels=1)
        
        float[][] melFrames = melFrameBuffer.ToArray();
        int frameCount = melFrames.Length;
        int featuresPerFrame = melFrames.Length > 0 ? melFrames[0].Length : 0;
        
        Debug.Log($"Building embedding input from {frameCount} mel frames with {featuresPerFrame} features each");
        
        // We need to reshape the mel features to [1, 76, 32, 1]
        // Each mel frame has dim2*32 features, we need to split them back to 32 features per mel band
        float[] embeddingInputData = new float[1 * 76 * 32 * 1];
        
        for (int f = 0; f < 76 && f < frameCount; f++)
        {
            float[] frameFeatures = melFrames[f];
            for (int feat = 0; feat < 32 && feat < frameFeatures.Length; feat++)
            {
                // Target index: [0, f, feat, 0]
                int embIndex = 0 * (76 * 32 * 1) + f * (32 * 1) + feat * 1 + 0;
                embeddingInputData[embIndex] = frameFeatures[feat];
            }
        }
        
        embeddingInput = new Tensor<float>(new TensorShape(1, 76, 32, 1), embeddingInputData);
        
        if (embeddingInput == null)
        {
            Debug.LogError("Failed to create embedding input tensor");
            callback(null);
            yield break;
        }
        
        embeddingWorker.Schedule(embeddingInput);
        
        bool isComplete = false;
        while (!isComplete)
        {
            yield return null;
            
            var output = embeddingWorker.PeekOutput();
            if (output != null)
            {
                isComplete = true;
            }
        }
        
        embeddingOutput = embeddingWorker.PeekOutput() as Tensor<float>;
        
        if (embeddingOutput != null)
        {
            Debug.Log($"Embeddings generated - Shape: {embeddingOutput.shape}");
            callback(embeddingOutput);
        }
        else
        {
            Debug.LogError("Embedding output is null");
            callback(null);
        }
        
        if (embeddingInput != null)
            embeddingInput.Dispose();
    }

    float[] ExtractEmbeddingFeatures(Tensor<float> embeddingOutput)
    {
        if (embeddingOutput == null)
        {
            Debug.LogError("Embedding output tensor is null");
            return null;
        }
        
        try
        {
            // Embedding output shape: [unk__315, 1, 1, 96]
            // Extract the 96-dimensional feature vector from the first output
            int timeFrames = embeddingOutput.shape[0];
            int featureDim = 96;
            
            float[] features = new float[featureDim];
            
            // Take the first time frame: [0, 0, 0, :]
            for (int f = 0; f < featureDim; f++)
            {
                int embIndex = 0 * (1 * 1 * featureDim) + 0 * (1 * featureDim) + 0 * featureDim + f;
                features[f] = embeddingOutput[embIndex];
            }
            
            Debug.Log($"Extracted {features.Length} embedding features from {timeFrames} time frames");
            return features;
        }
        catch (System.Exception e)
        {
            Debug.LogError($"Error extracting embedding features: {e.Message}");
            return null;
        }
    }

    System.Collections.IEnumerator ClassifyEmbeddingsAsync(System.Action<float> callback)
    {
        Tensor<float> classifierInput = null;
        Tensor<float> classifierOutput = null;
        
        // Convert embedding frame buffer to tensor for classifier
        // Target shape: [1, 16, 96]
        
        float[][] embeddingFrames = embeddingFrameBuffer.ToArray();
        int frameCount = embeddingFrames.Length;
        int featuresPerFrame = 96;
        
        Debug.Log($"=== CLASSIFIER STARTING === Building classifier input from {frameCount} embedding frames");
        
        float[] classifierInputData = new float[1 * 16 * 96];
        
        for (int f = 0; f < 16 && f < frameCount; f++)
        {
            for (int feat = 0; feat < featuresPerFrame; feat++)
            {
                // Target index: [0, f, feat]
                int classIndex = 0 * (16 * 96) + f * 96 + feat;
                classifierInputData[classIndex] = embeddingFrames[f][feat];
            }
        }
        
        classifierInput = new Tensor<float>(new TensorShape(1, 16, 96), classifierInputData);
        
        if (classifierInput == null)
        {
            Debug.LogError("Failed to create classifier input tensor");
            callback(0.0f);
            yield break;
        }
        
        Debug.Log("=== CLASSIFIER SCHEDULING ===");
        classifierWorker.Schedule(classifierInput);
        
        bool isComplete = false;
        while (!isComplete)
        {
            yield return null; 
            
            var output = classifierWorker.PeekOutput();
            if (output != null)
            {
                isComplete = true;
            }
        }
        
        classifierOutput = classifierWorker.PeekOutput() as Tensor<float>;
        
        if (classifierOutput != null)
        {
            float prediction = classifierOutput[0];
            Debug.Log($"=== CLASSIFIER RESULT === {prediction:F4}");
            classifierOutput.Dispose();
            callback(prediction);
        }
        else
        {
            Debug.LogError("Classifier output is null");
            callback(0.0f);
        }
        
        if (classifierInput != null)
            classifierInput.Dispose();
    }

    void ProcessDetectionResult(float prediction)
    {
        recentPredictions.Add(prediction);
        
        if (recentPredictions.Count > patienceFrames)
        {
            recentPredictions.RemoveAt(0);
        }
        
        bool shouldDetect = false;
        
        if (recentPredictions.Count >= patienceFrames)
        {
            int aboveThreshold = 0;
            foreach (float pred in recentPredictions)
            {
                if (pred >= threshold)
                    aboveThreshold++;
            }
            
            shouldDetect = aboveThreshold >= patienceFrames;
        }
        
        Debug.Log($"=== DETECTION RESULT === Prediction: {prediction:F4}, Recent avg: {GetAveragePrediction():F4}, Above threshold: {CountAboveThreshold()}/{patienceFrames}");
        
        // debouncing
        if (shouldDetect && (Time.time - lastDetectionTime) >= debounceTime)
        {
            OnWakeWordDetected(prediction);
            lastDetectionTime = Time.time;
        }
    }
    
    void OnWakeWordDetected(float confidence)
    {
        Debug.Log($"=== WAKE WORD DETECTED! Confidence: {confidence:F4} ===");
        
        // !!! wake word detection logic here
        // rn just change background color to notify
        if (Camera.main != null)
        {
            StartCoroutine(FlashDetection());
        }
    }
    
    System.Collections.IEnumerator FlashDetection()
    {
        Color originalColor = Camera.main.backgroundColor;
        Camera.main.backgroundColor = Color.green;
        yield return new WaitForSeconds(0.2f);
        Camera.main.backgroundColor = originalColor;
    }
    
    float GetAveragePrediction()
    {
        if (recentPredictions.Count == 0) return 0.0f;
        
        float sum = 0.0f;
        foreach (float pred in recentPredictions)
        {
            sum += pred;
        }
        return sum / recentPredictions.Count;
    }
    
    int CountAboveThreshold()
    {
        int count = 0;
        foreach (float pred in recentPredictions)
        {
            if (pred >= threshold)
                count++;
        }
        return count;
    }
    
    void OnDestroy()
    {
        melWorker?.Dispose();
        embeddingWorker?.Dispose();
        classifierWorker?.Dispose();
        Microphone.End(null); // might need to dispose other stuff
    }
    
    // temporary GUI 
    void OnGUI()
    {
        GUI.Label(new Rect(10, 10, 400, 20), $"Mel frames: {melFrameBuffer?.Count ?? 0}/{requiredMelFrames}");
        GUI.Label(new Rect(10, 30, 400, 20), $"Embedding frames: {embeddingFrameBuffer?.Count ?? 0}/{requiredEmbedFrames}");
        
        if (recentPredictions.Count > 0)
        {
            GUI.Label(new Rect(10, 50, 300, 20), $"Current Prediction: {recentPredictions[recentPredictions.Count - 1]:F4}");
            GUI.Label(new Rect(10, 70, 300, 20), $"Average: {GetAveragePrediction():F4}");
            GUI.Label(new Rect(10, 90, 300, 20), $"Above Threshold: {CountAboveThreshold()}/{patienceFrames}");
        }
        GUI.Label(new Rect(10, 110, 300, 20), $"Processing: {isProcessing}");
    }
}