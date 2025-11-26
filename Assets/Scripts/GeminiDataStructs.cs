using System.Collections.Generic;

namespace GeminiData
{
    [System.Serializable]
    public class Part { public string text; }
    
    [System.Serializable]
    public class Content { public string role; public List<Part> parts; }

    [System.Serializable]
    public class Candidate 
    { 
        public Content content; 
        public string finishReason; 
    }

    [System.Serializable]
    public class GeminiResponse 
    { 
        public List<Candidate> candidates; 
    }

    [System.Serializable]
    public class GenerationConfig
    {
        public float temperature = 0.8f; 
        public int maxOutputTokens = 512; 
    }

    [System.Serializable]
    public class GeminiRequest
    {
        public Content systemInstruction; 
        public List<Content> contents; 
        public GenerationConfig generationConfig; 
    }
}