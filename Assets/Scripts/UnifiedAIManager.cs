using UnityEngine;
using UnityEngine.Networking;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using UniVRM10;
using TMPro; 
using System.Text.RegularExpressions; 
using System.IO; 
using System.Linq; 

[System.Serializable] public class Part { public string text; }
[System.Serializable] public class Content { public string role; public List<Part> parts; }
[System.Serializable] public class Candidate { public Content content; }
[System.Serializable] public class GeminiResponse { public List<Candidate> candidates; }
[System.Serializable] public class GenerationConfig { public float temperature = 0.8f; public int maxOutputTokens = 512; }
[System.Serializable] public class GeminiRequest { public Content systemInstruction; public List<Content> contents; public GenerationConfig generationConfig; }

public class UnifiedAIManager : MonoBehaviour
{
    [SerializeField] private string apiKey = "YOUR_GEMINI_API_KEY_HERE"; 
    private const string MODEL_NAME = "gemini-2.5-flash";
    private string ApiURL => $"https://generativelanguage.googleapis.com/v1beta/models/{MODEL_NAME}:generateContent?key={apiKey}";
    
    private string systemInstruction; 
    private string aiName;
    private Vrm10Instance vrmInstance;

    private List<Content> conversationHistory = new List<Content>(); 
    private bool isAITalking = false; 
    
    public TMP_InputField inputField;
    public TextMeshProUGUI outputText; 
    
    private Animator characterAnimator; 
    private SkinnedMeshRenderer characterMeshRenderer; 

    private CharacterDataAsset loadedData;
    private Dictionary<string, EmotionBlendShapeConfig> blendShapeMap;
    private Dictionary<string, int> costumeMap = new Dictionary<string, int>{ {"기본", 0} };
    
    public float typingSpeed = 0.05f; 
    private string saveFilePath; 

    void Start()
    {
        GameObject[] oldModels = GameObject.FindGameObjectsWithTag("CharacterModel");
        foreach (GameObject model in oldModels)
        {
            Destroy(model);
        }
        
        string lastAiName = PlayerPrefs.GetString("LAST_CHARACTER_AI_NAME", string.Empty);
        
        Debug.Log(lastAiName);

        if (string.IsNullOrEmpty(lastAiName))
        {
            Debug.LogError("PlayerPrefs에서 LAST_CHARACTER_AI_NAME을 찾을 수 없습니다. Main Menu를 통해 씬을 로드하세요.");
            return;
        }

        loadedData = Resources.LoadAll<CharacterDataAsset>("CharacterData")
                           .FirstOrDefault(data => data.aiName == lastAiName);

        if (loadedData == null)
        {
            Debug.LogError($"'{lastAiName}'에 해당하는 CharacterDataAsset을 Resources에서 찾을 수 없습니다.");
            return;
        }
        
        if (loadedData.characterPrefab != null)
        {
            Vector3 spawnPosition = new Vector3(0f, 0f, -8.5f); 
            Quaternion spawnRotation = Quaternion.Euler(0, 180f, 0); 
            
            GameObject characterInstance = Instantiate(loadedData.characterPrefab, spawnPosition, spawnRotation);

            characterInstance.tag = "CharacterModel";

            characterAnimator = characterInstance.GetComponent<Animator>();
            
            if (loadedData.modelType == CharacterDataAsset.ModelType.VRM_BLENDSHAPE)
            {
                SkinnedMeshRenderer[] renderers = characterInstance.GetComponentsInChildren<SkinnedMeshRenderer>();
        
                foreach (SkinnedMeshRenderer renderer in renderers)
                {
                    if (renderer.gameObject.name.ToLower().Contains("face")) 
                    {
                        characterMeshRenderer = renderer;
                        Debug.Log($"Face SkinnedMeshRenderer found: {renderer.gameObject.name}");
                        break;
                    }
                }
                
                vrmInstance = characterInstance.GetComponent<Vrm10Instance>();
                
                if (vrmInstance == null)
                {
                    Debug.LogError("Vrm10Instance Not found");
                }
            }

            if (characterAnimator != null)
            {
                characterAnimator.Play("Idle", 0, 0f); 
            }
        }
        else
        {
            Debug.LogError($"{loadedData.aiName}의 characterPrefab이 CharacterDataAsset에 연결x");
            return; 
        }

        this.aiName = loadedData.aiName;
        this.systemInstruction = loadedData.systemInstruction;
        
        if (loadedData.modelType == CharacterDataAsset.ModelType.VRM_BLENDSHAPE)
        {
            InitializeBlendShapeMap();
        }

        string safeName = Regex.Replace(aiName, @"[^a-zA-Z0-9가-힣]", "_");
        string fileName = $"{safeName}_chat_log.txt";
        saveFilePath = Path.Combine(Application.persistentDataPath, fileName);
        
        LoadChatHistory();
        Debug.Log(saveFilePath);
        Debug.Log($"Unified AIManager initialized for: {aiName}. Model: {loadedData.modelType}");
    }

    private void InitializeBlendShapeMap()
    {
        blendShapeMap = new Dictionary<string, EmotionBlendShapeConfig>();
        foreach (var config in loadedData.vrmEmotionConfigs)
        {
            if (!string.IsNullOrEmpty(config.emotionKey))
            {
                blendShapeMap.Add(config.emotionKey.ToLower(), config);
            }
        }
    }

    private void LoadChatHistory()
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
                if (conversationHistory.Count > 0)
                {
                    string lastResponseWithTags = conversationHistory[conversationHistory.Count - 1].parts[0].text;
                    string lastCleanMessage = Regex.Replace(lastResponseWithTags, @"\[.*?\]", "").Trim();
                    outputText.text = lastCleanMessage;
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
        if (outputText != null) { outputText.text = "새로운 대화를 시작합니다."; }
    }

    public void SendChatMessageFromUI()
    {
        if (isAITalking || string.IsNullOrWhiteSpace(inputField.text)) return;
        string userInput = inputField.text;
        outputText.text = $"{aiName} 생각 중...";
        inputField.text = ""; 
        SendChatMessage(userInput);
    }
    
    private void SendChatMessage(string userInput)
    {
        if (string.IsNullOrWhiteSpace(apiKey)) { HandleFinalAIResponse("API 키 미설정 오류."); return; }
        AddMessageToHistory("user", userInput);
        SaveChatHistory();
        StartCoroutine(SendRequestCoroutine());
    }

    private void AddMessageToHistory(string role, string message)
    {
        conversationHistory.Add(new Content { role = role, parts = new List<Part> { new Part { text = message } } });
    }
    
    private Content CreateSystemContent(string message)
    {
        return new Content { parts = new List<Part> { new Part { text = message } } };
    }

    IEnumerator SendRequestCoroutine()
    {
        isAITalking = true;
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
                        HandleFinalAIResponse(aiResponseText);
                        isAITalking = false;
                        yield break; 
                    }
                    HandleFinalAIResponse("응답이 비어있습니다.");
                    isAITalking = false;
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
                    HandleFinalAIResponse($"치명적 오류 발생 ({www.responseCode}).");
                    isAITalking = false;
                    yield break;
                }
            }
        }
        isAITalking = false;
        HandleFinalAIResponse("죄송해요, 서버가 너무 바빠서 대화가 불가능해요.");
    }

    private void HandleFinalAIResponse(string responseText)
    {
        string emotion = ExtractEmotion(responseText);
        string costume = ExtractCostume(responseText);
        ApplyCostumeToAnimator(costume);
        
        if (loadedData.modelType == CharacterDataAsset.ModelType.VRM_BLENDSHAPE)
        {
            ApplyEmotionToBlendShape(emotion);
        }
        else if (loadedData.modelType == CharacterDataAsset.ModelType.STANDARD_ANIMATOR)
        {
            ApplyEmotionToAnimator(emotion);
        }

        string cleanResponse = Regex.Replace(responseText, @"\[.*?\]", "").Trim();
        if (outputText != null)
        {
            StartCoroutine(TypeTextCoroutine(cleanResponse));
        }
        float talkDuration = Mathf.Clamp(cleanResponse.Length * 0.05f, 1f, 5f);
        StartCoroutine(StopTalkingAfterDelay(talkDuration)); 
    }

    IEnumerator TypeTextCoroutine(string textToType)
    {
        outputText.text = ""; 
        foreach (char letter in textToType.ToCharArray())
        {
            outputText.text += letter; 
            yield return new WaitForSeconds(typingSpeed); 
        }
    }
    
    private void ApplyEmotionToBlendShape(string emotionKey)
    {
        if (vrmInstance == null || characterMeshRenderer == null || characterMeshRenderer.sharedMesh == null || blendShapeMap == null) return;
        int blendShapeCount = characterMeshRenderer.sharedMesh.blendShapeCount;

        for (int i = 0; i < blendShapeCount; i++)
        {
            string blendShapeName = characterMeshRenderer.sharedMesh.GetBlendShapeName(i);
            ExpressionKey expressionKey = ExpressionKey.CreateCustom(blendShapeName);
            vrmInstance.Runtime.Expression.SetWeight(expressionKey, 0f);
            characterMeshRenderer.SetBlendShapeWeight(i, 0f); 
        }
        
        if (blendShapeMap.TryGetValue(emotionKey.ToLower(), out EmotionBlendShapeConfig config))
        {
            Debug.Log($"Loaded Blend Shapes Count for '{emotionKey}': {config.blendShapes.Count}");
            foreach (var pair in config.blendShapes)
            {
                if (string.IsNullOrWhiteSpace(pair.name))
                {
                    Debug.LogError($"BlendShapePair에 이름이 없습니다. 이 항목을 건너뜁니다.");
                    continue;
                }
                int blendShapeIndex = characterMeshRenderer.sharedMesh.GetBlendShapeIndex(pair.name);
                float targetWeight = pair.weight / 100.0f;
                ExpressionKey expressionKey = ExpressionKey.CreateCustom(pair.name); 
                vrmInstance.Runtime.Expression.SetWeight(expressionKey, targetWeight);
                
                characterMeshRenderer.SetBlendShapeWeight(blendShapeIndex, pair.weight); 
                    
                //Debug.Log($"VRM Setting: {pair.name} to {targetWeight}");
                
            }
        }
    }
    
    private void ApplyEmotionToAnimator(string emotionKey)
    {
        if (characterAnimator == null || loadedData.animatorEmotionMap == null) return;
        
        if (!loadedData.animatorEmotionMap.ContainsKey(emotionKey))
        {
            emotionKey = "행복"; 
        }

        var emotionStates = loadedData.animatorEmotionMap[emotionKey];
        
        characterAnimator.SetInteger("Eye", emotionStates.Eye);
        characterAnimator.SetInteger("Eyebrow", emotionStates.Eyebrow);
        characterAnimator.SetInteger("Mouth", emotionStates.Mouth);
        characterAnimator.SetInteger("Eff", emotionStates.Eff); 
    }
    
    private void ApplyCostumeToAnimator(string costumeKey)
    {
        if (characterAnimator == null) return;
        if (!costumeMap.ContainsKey(costumeKey)) { costumeKey = "기본"; }
        characterAnimator.SetInteger("CostumeID", costumeMap[costumeKey]); 
    }

    private string ExtractEmotion(string responseText)
    {
        Debug.Log(responseText);
        Match match = Regex.Match(responseText, @"\[(.*?)\]");
        if (match.Success)
        {
            string value = match.Groups[1].Value.Trim();
            Match emotionMatch = Regex.Match(value, @"^(.*?)(?:\[|$)");
            string emotion = emotionMatch.Success ? emotionMatch.Groups[1].Value.Trim().ToLower() : value.ToLower();
            if (emotion.Contains("복장:")) return "행복";
            //Debug.Log(emotion);
            return emotion;
        }
        return "행복"; 
    }

    private string ExtractCostume(string responseText)
    {
        Match match = Regex.Match(responseText, @"\[복장:(.*?)\]");
        if (match.Success)
        {
            return match.Groups[1].Value.Trim().ToLower();
        }
        return "기본"; 
    }

    IEnumerator StopTalkingAfterDelay(float delay)
    {
        yield return new WaitForSeconds(delay);
    }
}