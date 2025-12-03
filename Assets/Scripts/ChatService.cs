using UnityEngine;
using UnityEngine.Networking;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using System.IO; 
using System.Linq; 
using System.Text.RegularExpressions;

[System.Serializable] public class Part { public string text; }
[System.Serializable] public class Content { public string role; public List<Part> parts; }
[System.Serializable] public class Candidate { public Content content; }
[System.Serializable] public class GeminiResponse { public List<Candidate> candidates; }
[System.Serializable] public class GenerationConfig { public float temperature = 0.8f; public int maxOutputTokens = 256; }
[System.Serializable] public class GeminiRequest { public Content systemInstruction; public List<Content> contents; public GenerationConfig generationConfig; }

public class ChatService : MonoBehaviour
{
    private string apiKey;
    private const string API_KEY_RESOURCE_PATH = "gemini_api_key";
    private const string MODEL_NAME = "gemini-2.5-flash";
    private string ApiURL => $"https://generativelanguage.googleapis.com/v1beta/models/{MODEL_NAME}:generateContent?key={apiKey}";
    
    private string systemInstruction; 
    private string aiName;

    private List<Content> conversationHistory = new List<Content>(); 
    private string saveFilePath; 
    
    public List<Content> ConversationHistory => conversationHistory;

    public bool CheckAndLoadApiKey()
    {
        TextAsset keyFile = Resources.Load<TextAsset>(API_KEY_RESOURCE_PATH);

        if (keyFile == null || string.IsNullOrWhiteSpace(keyFile.text))
        {
            Debug.LogError($"'{API_KEY_RESOURCE_PATH}.txt' 파일을 Resources 폴더에서 찾을 수 없거나 키가 비어있습니다. 파일을 생성하고 키를 입력해주세요.");
            return false;
        }
        
        apiKey = keyFile.text.Trim();
        
        if (string.IsNullOrEmpty(apiKey))
        {
            Debug.LogError("API 키가 파일에 존재하지만 비어있습니다. 키를 입력해주세요.");
            return false;
        }
        return true;
    }

    public void Initialize(string aiName, string systemInstruction)
    {
        this.aiName = aiName;
        this.systemInstruction = systemInstruction;
        
        string safeName = Regex.Replace(aiName, @"[^a-zA-Z0-9가-힣]", "_");
        string fileName = $"{safeName}_chat_log.txt";
        saveFilePath = Path.Combine(Application.persistentDataPath, fileName);
    }
    
    public void LoadChatHistory()
    {
        if (File.Exists(saveFilePath))
        {
            try
            {
                string[] lines = File.ReadAllLines(saveFilePath);
                conversationHistory.Clear();
                foreach (string line in lines)
                {
                    if (line.StartsWith("[user]"))
                    {
                        AddMessageToHistory("user", line.Substring("[user]".Length));
                    }
                    else if (line.StartsWith("[model]"))
                    {
                        AddMessageToHistory("model", line.Substring("[model]".Length));
                    }
                }
            }
            catch (System.Exception e)
            {
                Debug.LogError($"Cant Load Chat History{e.Message}");
            }
        }
    }

    private void SaveChatHistory()
    {
        List<string> lines = new List<string>();
        foreach (var content in conversationHistory)
        {
            if (content.parts != null && content.parts.Count > 0 && content.role != null)
            {
                lines.Add($"[{content.role}]{content.parts[0].text}");
            }
        }
        try { File.WriteAllLines(saveFilePath, lines); }
        catch (System.Exception e) { Debug.LogError($"대화 기록 저장 실패: {e.Message}"); }
    }
    
    public void ResetChatHistory()
    {
        conversationHistory.Clear();
        if (File.Exists(saveFilePath)) { try { File.Delete(saveFilePath); } catch (System.Exception e) { Debug.LogError($"Failed to delete chat log file: {e.Message}"); } }
    }

    public void SendChatMessage(string userInput, System.Action<string> onResponseReceived)
    {
        if (string.IsNullOrWhiteSpace(apiKey)) 
        {
             onResponseReceived?.Invoke("API 키가 로드되지 않았습니다. 파일을 확인해주세요.");
             return;
        }
        
        AddMessageToHistory("user", userInput);
        SaveChatHistory();
        StartCoroutine(SendRequestCoroutine(onResponseReceived));
    }

    private void AddMessageToHistory(string role, string message)
    {
        conversationHistory.Add(new Content { role = role, parts = new List<Part> { new Part { text = message } } });
    }
    
    private Content CreateSystemContent(string message)
    {
        return new Content { parts = new List<Part> { new Part { text = message } } };
    }

    IEnumerator SendRequestCoroutine(System.Action<string> onResponseReceived)
    {
        GeminiRequest requestPayload = new GeminiRequest { systemInstruction = CreateSystemContent(this.systemInstruction), contents = conversationHistory, generationConfig = new GenerationConfig() };
        string jsonPayload = JsonUtility.ToJson(requestPayload);
        
        int maxRetries = 3; 
        int currentRetry = 0;

        while (currentRetry < maxRetries)
        {
            using (UnityWebRequest www = new UnityWebRequest(ApiURL, "POST"))
            {
                byte[] bodyRaw = Encoding.UTF8.GetBytes(jsonPayload);
                www.uploadHandler = new UploadHandlerRaw(bodyRaw);
                www.downloadHandler = new DownloadHandlerBuffer();
                www.timeout = 15;
                www.SetRequestHeader("Content-Type", "application/json");

                yield return www.SendWebRequest();
                
                if (www.result == UnityWebRequest.Result.Success)
                {
                    GeminiResponse geminiResponse = JsonUtility.FromJson<GeminiResponse>(www.downloadHandler.text);
                    if (geminiResponse.candidates != null && geminiResponse.candidates.Count > 0)
                    {
                        string aiResponseText = geminiResponse.candidates[0].content.parts[0].text;
                        AddMessageToHistory("model", aiResponseText); 
                        SaveChatHistory();
                        onResponseReceived?.Invoke(aiResponseText);
                        yield break; 
                    }
                    onResponseReceived?.Invoke("응답이 비어있습니다.");
                    yield break;
                }
                
                if (www.responseCode == 503 || www.responseCode == 429) 
                {
                    currentRetry++;
                    float waitTime = Mathf.Pow(2f, currentRetry); 
                    yield return new WaitForSeconds(waitTime);
                }
                else
                {
                    onResponseReceived?.Invoke($"치명적 오류 발생 ({www.responseCode}).");
                    yield break;
                }
            }
        }
        onResponseReceived?.Invoke("죄송해요, 서버가 너무 바빠서 대화가 불가능해요.");
    }
}