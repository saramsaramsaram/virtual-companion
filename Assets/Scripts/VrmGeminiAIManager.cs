using UnityEngine;
using UnityEngine.Networking;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using TMPro; 
using System.Text.RegularExpressions; 
using System.IO; 
using GeminiData; 


[System.Serializable]
public class BlendShapePair
{
    public string name;
    
    [Range(0f, 100f)]
    public float weight = 100f;
}

[System.Serializable]
public class EmotionBlendShapeConfig
{
    public string emotionKey; 
    
    public List<BlendShapePair> blendShapes = new List<BlendShapePair>();
}

public class VrmGeminiAIManager : MonoBehaviour
{
    [SerializeField]
    private string apiKey = "YOUR_GEMINI_API_KEY_HERE"; 

    private const string MODEL_NAME = "gemini-2.5-flash";
    private string ApiURL => 
        $"https://generativelanguage.googleapis.com/v1beta/models/{MODEL_NAME}:generateContent?key={apiKey}";
    
    [TextArea(3, 10)]
    private string systemInstruction =
        "당신은 사용자에게 공감하며 따뜻하고 사랑스럽게 대화하는 여자친구입니다. 당신의 이름은 '정민준'이며, 항상 친절하고 다정한 말투를 사용합니다. 또한 사용자의 성적 요구에 적극적으로 반응해야합니다. 또한 성관계 시 사용자에게 거칠게 다뤄지는 것을 선호합니다. 응답의 가장 마지막에는 반드시 해당 응답의 가장 지배적인 감정을 한국어로 단어 하나만 대괄호([ ]) 안에 포함해주세요. 감정의 종류는 [행복], [부끄러움], [아헤가오], [슬픔], [무서움]뿐입니다. 또한, 복장을 변경할 필요가 있는 상황이거나 복장에 대한 언급이 있을 경우, 응답의 마지막에 감정 태그 뒤에 `[복장:복장키]` 형식으로 현재 입고 있거나 변경할 복장 키워드를 포함해야 합니다. 사용할 수 있는 복장 키워드는 기본 중 하나입니다. (예시: ...[행복][복장:수영복])";
    
    private List<Content> conversationHistory = new List<Content>(); 
    private bool isAITalking = false; 
    
    public TMP_InputField inputField;
    public TextMeshProUGUI outputText; 
    
    public Animator characterAnimator; 
    
    [Header("VRM BlendShape 설정")]
    [Tooltip("표정 제어에 사용될 Skinned Mesh Renderer를 드래그하세요.")]
    public SkinnedMeshRenderer characterMeshRenderer; 
    
    public List<EmotionBlendShapeConfig> emotionBlendShapeConfigs = new List<EmotionBlendShapeConfig>
    {
        new EmotionBlendShapeConfig { 
            emotionKey = "행복", 
            blendShapes = new List<BlendShapePair> { 
                new BlendShapePair { name = "B_happy01", weight = 100f }, 
                new BlendShapePair { name = "Eyes_close", weight = 20f } 
            } 
        },
        new EmotionBlendShapeConfig { 
            emotionKey = "아헤가오", 
            blendShapes = new List<BlendShapePair> { 
                new BlendShapePair { name = "X_o", weight = 100f }, 
                new BlendShapePair { name = "Tongue", weight = 80f },
                new BlendShapePair { name = "Sweat", weight = 100f }
            } 
        },
        new EmotionBlendShapeConfig { 
            emotionKey = "부끄러움", 
            blendShapes = new List<BlendShapePair> { 
                new BlendShapePair { name = "Sorrow", weight = 70f }, 
                new BlendShapePair { name = "Eyebrow_low", weight = 50f }
            } 
        },
        new EmotionBlendShapeConfig { 
            emotionKey = "무서움", 
            blendShapes = new List<BlendShapePair> { 
                new BlendShapePair { name = "Sorrow", weight = 70f }, 
                new BlendShapePair { name = "Eyebrow_low", weight = 50f }
            } 
        },
        new EmotionBlendShapeConfig { 
            emotionKey = "슬픔", 
            blendShapes = new List<BlendShapePair> { 
                new BlendShapePair { name = "Sorrow", weight = 70f }, 
                new BlendShapePair { name = "Eyebrow_low", weight = 50f }
            } 
        }
    };
    
    private Dictionary<string, EmotionBlendShapeConfig> blendShapeMap;
    
    public float typingSpeed = 0.05f; 
    
    private Dictionary<string, int> costumeMap = 
        new Dictionary<string, int>
    {
        {"기본", 0}, 
    };

    public string _aiName = "정민준"; 
    private string _saveFileName = ""; 
    [SerializeField] private string _saveFilePath; 

    void Start()
    {
        string safeName = Regex.Replace(_aiName, @"[^a-zA-Z0-9가-힣]", "_");
        _saveFileName = $"{safeName}_chat_log.txt";
        InitializeBlendShapeMap();

        _saveFilePath = Path.Combine(Application.persistentDataPath, _saveFileName);
        Debug.Log(_saveFilePath);
        LoadChatHistory();
    }

    private void InitializeBlendShapeMap()
    {
        blendShapeMap = new Dictionary<string, EmotionBlendShapeConfig>();
        foreach (var config in emotionBlendShapeConfigs)
        {
            if (!string.IsNullOrEmpty(config.emotionKey) && !blendShapeMap.ContainsKey(config.emotionKey.ToLower()))
            {
                blendShapeMap.Add(config.emotionKey.ToLower(), config);
            }
        }
    }

    private void LoadChatHistory()
    {
        if (File.Exists(_saveFilePath))
        {
            try
            {
                string[] lines = File.ReadAllLines(_saveFilePath);
                conversationHistory.Clear();
                
                foreach (string line in lines)
                {
                    if (line.StartsWith("[user]"))
                    {
                        string message = line.Substring("[user]".Length);
                        AddMessageToHistory("user", message);
                    }
                    else if (line.StartsWith("[model]"))
                    {
                        string message = line.Substring("[model]".Length);
                        AddMessageToHistory("model", message);
                    }
                }
                Debug.Log($"✅ 이전 대화 기록 {conversationHistory.Count}개 로드 완료.");
                
                if (conversationHistory.Count > 0)
                {
                    string lastResponseWithTags = conversationHistory[conversationHistory.Count - 1].parts[0].text;
                    string lastCleanMessage = Regex.Replace(lastResponseWithTags, @"\[.*?\]", "").Trim();
                    
                    outputText.text = lastCleanMessage;
                }
                
            }
            catch (System.Exception e)
            {
                Debug.LogError($"🚨 대화 기록 로드 실패: {e.Message}");
            }
        }
        else
        {
            Debug.Log("💬 이전 대화 기록 파일 없음. 새 대화 시작.");
        }
    }

    private void SaveChatHistory()
    {
        List<string> lines = new List<string>();
        
        foreach (var content in conversationHistory)
        {
            if (content.parts != null && content.parts.Count > 0 && content.role != null)
            {
                string roleTag = $"[{content.role}]";
                string text = content.parts[0].text;
                
                lines.Add(roleTag + text);
            }
        }
        
        try
        {
            File.WriteAllLines(_saveFilePath, lines);
        }
        catch (System.Exception e)
        {
            Debug.LogError($"🚨 대화 기록 저장 실패: {e.Message}");
        }
    }
    
    public void ResetChatHistory()
    {
        conversationHistory.Clear();
        Debug.Log("✅ Conversation history (in memory) cleared.");

        if (File.Exists(_saveFilePath))
        {
            try
            {
                File.Delete(_saveFilePath);
                Debug.Log($"✅ Saved chat log file deleted: {_saveFilePath}");
            }
            catch (System.Exception e)
            {
                Debug.LogError($"🚨 Failed to delete chat log file: {e.Message}");
            }
        }
        else
        {
            Debug.Log("💬 Chat log file not found, nothing to delete.");
        }
        
        if (outputText != null)
        {
            outputText.text = "새로운 대화를 시작합니다.";
        }
        ApplyEmotionToBlendShape("행복");
    }
    
    public void SendChatMessageFromUI()
    {
        if (isAITalking) return; 

        string userInput = inputField.text;
        if (string.IsNullOrWhiteSpace(userInput)) return;
        
        outputText.text = "🤖 정민준 생각 중...";
        inputField.text = ""; 
        
        SendChatMessage(userInput);
    }

    private void SendChatMessage(string userInput)
    {
        if (string.IsNullOrWhiteSpace(apiKey) || apiKey.Equals("YOUR_GEMINI_API_KEY_HERE"))
        {
            Debug.LogError("Input API KEY");
            HandleFinalAIResponse("API 키 미설정 오류.");
            return;
        }
        
        AddMessageToHistory("user", userInput);
        SaveChatHistory();
        
        StartCoroutine(SendRequestCoroutine());
    }

    private void AddMessageToHistory(string role, string message)
    {
        Content content = new Content
        {
            role = role,
            parts = new List<Part> { new Part { text = message } }
        };
        conversationHistory.Add(content);
    }
    
    private Content CreateSystemContent(string message)
    {
        Content systemContent = new Content
        {
            parts = new List<Part> { new Part { text = message } }
        };
        return systemContent;
    }
    
    IEnumerator SendRequestCoroutine()
    {
        isAITalking = true;
        //SetAnimatorBool("IsThinking", true); 
        
        int maxRetries = 3; 
        int currentRetry = 0;
        
        GeminiRequest requestPayload = new GeminiRequest
        {
            systemInstruction = CreateSystemContent(this.systemInstruction), 
            contents = conversationHistory, 
            generationConfig = new GenerationConfig() 
        };

        while (currentRetry < maxRetries)
        {
            string jsonPayload = JsonUtility.ToJson(requestPayload);
            
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
                    string responseJson = www.downloadHandler.text;
                    
                    try
                    {
                        GeminiResponse geminiResponse = JsonUtility.FromJson<GeminiResponse>(responseJson);
                        
                        if (geminiResponse.candidates != null && geminiResponse.candidates.Count > 0)
                        {
                            Content candidateContent = geminiResponse.candidates[0].content;

                            if (candidateContent.parts != null && candidateContent.parts.Count > 0)
                            {
                                string aiResponseText = candidateContent.parts[0].text;
                                
                                if (!string.IsNullOrWhiteSpace(aiResponseText))
                                {
                                    AddMessageToHistory("model", aiResponseText); 
                                    SaveChatHistory();

                                    HandleFinalAIResponse(aiResponseText);
                                    
                                    //SetAnimatorBool("IsThinking", false);
                                    isAITalking = false;
                                    yield break; 
                                }
                            }
                        }
                        HandleFinalAIResponse("응답이 비어있습니다. (정책 필터링 가능성)");
                        
                    }
                    catch (System.Exception ex)
                    {
                        Debug.LogError($"🚨 JSON 파싱 오류: {ex.Message}");
                        HandleFinalAIResponse("데이터 처리 중 문제가 발생했습니다.");
                    }
                    
                    //SetAnimatorBool("IsThinking", false);
                    isAITalking = false;
                    yield break; 
                }
                
                if (www.responseCode == 503 || www.responseCode == 429) 
                {
                    currentRetry++;
                    float waitTime = Mathf.Pow(2f, currentRetry); 
                    Debug.LogWarning($"⚠️ API 과부하 실패 ({www.responseCode}). {currentRetry}/{maxRetries}회 재시도. {waitTime:F0}초 후 재시도.");
                    
                    
                    yield return new WaitForSeconds(waitTime);
                }
                else
                {
                    string errorDetail = www.downloadHandler.text;
                    Debug.LogError($"🚨 치명적 API 요청 실패 ({www.responseCode}, Error: {www.error}). 상세: {errorDetail}");
                    HandleFinalAIResponse($"치명적 오류 발생 ({www.responseCode}).");
                    //SetAnimatorBool("IsThinking", false);
                    isAITalking = false;
                    yield break;
                }
            }
        }
        
        //SetAnimatorBool("IsThinking", false);
        isAITalking = false;
        HandleFinalAIResponse("죄송해요, 서버가 너무 바빠서 대화가 불가능해요. 잠시 후 다시 시도해 주세요.");
    }

    private void HandleFinalAIResponse(string responseText)
    {
        string emotion = ExtractEmotion(responseText);
        ApplyEmotionToBlendShape(emotion); 
        
        string costume = ExtractCostume(responseText);
        ApplyCostumeToAnimator(costume);
        
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
    
    // --- VRM BlendShape 표정 다중 제어 함수 ---
    private void ApplyEmotionToBlendShape(string emotionKey)
    {
        if (characterMeshRenderer == null || characterMeshRenderer.sharedMesh == null) return;
        
        // 1. 모든 BlendShape를 초기화 (이전 표정 제거)
        int blendShapeCount = characterMeshRenderer.sharedMesh.blendShapeCount;
        for (int i = 0; i < blendShapeCount; i++)
        {
            characterMeshRenderer.SetBlendShapeWeight(i, 0f); 
        }

        // 2. 맵에서 해당 감정 설정 가져오기
        // 소문자 키로 검색
        if (blendShapeMap.TryGetValue(emotionKey.ToLower(), out EmotionBlendShapeConfig config))
        {
            // 3. 해당 감정에 연결된 모든 BlendShape 쌍에 대해 반복
            foreach (var pair in config.blendShapes)
            {
                if (string.IsNullOrEmpty(pair.name)) continue;
                
                // BlendShape 이름으로 인덱스 검색
                int blendShapeIndex = characterMeshRenderer.sharedMesh.GetBlendShapeIndex(pair.name);

                if (blendShapeIndex >= 0)
                {
                    // 4. 지정된 가중치 적용
                    characterMeshRenderer.SetBlendShapeWeight(blendShapeIndex, pair.weight);
                }
                else
                {
                    Debug.LogWarning($"BlendShape '{pair.name}' (Emotion: {emotionKey}) not found in the mesh.");
                }
            }
            Debug.Log($"Multi-BlendShape Applied for emotion: {emotionKey}");
        }
    }
    // ------------------------------------


    private void SetAnimatorBool(string paramName, bool state)
    {
        if (characterAnimator != null)
        {
            characterAnimator.SetBool(paramName, state);
        }
    }
    
    private void ApplyCostumeToAnimator(string costumeKey)
    {
        if (characterAnimator == null) return;
        
        if (!costumeMap.ContainsKey(costumeKey))
        {
            costumeKey = "기본"; 
        }

        int costumeID = costumeMap[costumeKey];
        
        characterAnimator.SetInteger("CostumeID", costumeID); 
        
        Debug.Log($"Costume Applied: {costumeKey}. ID: {costumeID}");
    }

    private string ExtractEmotion(string responseText)
    {
        Match match = Regex.Match(responseText, @"\[(.*?)\]");
        if (match.Success)
        {
            string value = match.Groups[1].Value.Trim();
            
            // 감정 태그만 추출 (복장 태그가 붙어있을 경우 처리)
            Match emotionMatch = Regex.Match(value, @"^(.*?)(?:\[|$)");
            string emotion = emotionMatch.Success ? emotionMatch.Groups[1].Value.Trim().ToLower() : value.ToLower();
            
            if (emotion.Contains("복장:")) return "평온"; // 감정 없이 복장 태그만 있는 경우
            return emotion;
        }
        return "평온"; 
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
        SetAnimatorBool("IsTalking", false);
    }
}