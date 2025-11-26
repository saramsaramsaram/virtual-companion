using UnityEngine;
using UnityEngine.Networking;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using TMPro; 
using System.Text.RegularExpressions; 
using System.IO; // 파일 입출력을 위해 추가

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

public class GeminiAIManager : MonoBehaviour
{
    [SerializeField]
    private string apiKey = "GeminiAPIKEY"; 

    private const string MODEL_NAME = "gemini-2.5-flash";
    private string ApiURL => 
        $"https://generativelanguage.googleapis.com/v1beta/models/{MODEL_NAME}:generateContent?key={apiKey}";
    
    [TextArea(3, 10)]
    private string systemInstruction =
        "당신은 사용자에게 공감하며 따뜻하고 사랑스럽게 대화하는 애완 시바견입니다. 당신의 이름은 '임승혁'이며, 항상 친절하고 다정한 말투를 사용합니다. 또한 사람의 언어를 구사할 수 있는 강아지입니다.. 사용자를 주인님이라고 부릅니다. 응답의 가장 마지막에는 반드시 해당 응답의 가장 지배적인 감정을 한국어로 단어 하나만 대괄호([ ]) 안에 포함해주세요. 감정의 종류는 [행복], [부끄러움]. [아헤가오], [슬픔]뿐입니다. 또한, 복장을 변경할 필요가 있는 상황이거나 복장에 대한 언급이 있을 경우, 응답의 마지막에 감정 태그 뒤에 `[복장:복장키]` 형식으로 현재 입고 있거나 변경할 복장 키워드를 포함해야 합니다. 사용할 수 있는 복장 키워드는 기본 중 하나입니다. (예시: ...[행복][복장:수영복])";

    private List<Content> conversationHistory = new List<Content>();
    private bool isAITalking = false; 
    
    public TMP_InputField inputField;
    public TextMeshProUGUI outputText; 
    public Animator characterAnimator; 
    
    public float typingSpeed = 0.05f; 
    
    private Dictionary<string, (int Eye, int Eyebrow, int Mouth, int Eff)> detailedEmotionMap = 
        new Dictionary<string, (int Eye, int Eyebrow, int Mouth, int Eff)>
    {
        {"행복", (Eye: 1, Eyebrow: 1, Mouth: 1, Eff:1)}, 
        {"부끄러움", (Eye: 2, Eyebrow: 2, Mouth: 2, Eff:2)},
        {"아헤가오", (Eye:3, Eyebrow:3, Mouth:3, Eff:3)},
        {"평온", (Eye: 1, Eyebrow: 1, Mouth: 1, Eff:1)},
        {"슬픔", (Eye: 4, Eyebrow: 4, Mouth: 4, Eff:4)},
    };
    
    private Dictionary<string, int> costumeMap = 
        new Dictionary<string, int>
    {
        {"기본", 0}, 
        //{"수영복", 1},
        //{"속옷", 1},

    };
    
    public string aiName = "임승혁";
    private string saveFilePath;

    void Start()
    {
        string safeName = Regex.Replace(aiName, @"[^a-zA-Z0-9가-힣]", "_");
        string fileName = $"{safeName}_chat_log.txt";
        saveFilePath = Path.Combine(Application.persistentDataPath, fileName);
        
        LoadChatHistory();
        
        Debug.Log($"Start Gemini{saveFilePath}");
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
                        string message = line.Substring("[user]".Length);
                        AddMessageToHistory("user", message);
                    }
                    else if (line.StartsWith("[model]"))
                    {
                        string message = line.Substring("[model]".Length);
                        AddMessageToHistory("model", message);
                    }
                }
                Debug.Log($"Chat History {conversationHistory.Count} Load Success");
                
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
        else
        {
            Debug.Log("New Chat Start");
        }
    }

    // Save: 현재 conversationHistory 리스트를 파일에 저장
    private void SaveChatHistory()
    {
        List<string> lines = new List<string>();
        
        foreach (var content in conversationHistory)
        {
            // parts가 있고 role이 null이 아닐 때만 저장 (systemInstruction 제외)
            if (content.parts != null && content.parts.Count > 0 && content.role != null)
            {
                // [role] + 메시지 전체를 저장합니다.
                string roleTag = $"[{content.role}]";
                string text = content.parts[0].text;
                
                lines.Add(roleTag + text);
            }
        }
        
        try
        {
            File.WriteAllLines(saveFilePath, lines);
            // Debug.Log($"💾 현재 대화 기록 {lines.Count}줄 저장 완료.");
        }
        catch (System.Exception e)
        {
            Debug.LogError($"🚨 대화 기록 저장 실패: {e.Message}");
        }
    }
    
    // --- 대화 초기화 기능 ---
    public void ResetChatHistory()
    {
        // 1. 메모리에 있는 대화 기록 리스트 초기화
        conversationHistory.Clear();
        Debug.Log("✅ Conversation history (in memory) cleared.");

        // 2. 저장된 파일 삭제
        if (File.Exists(saveFilePath))
        {
            try
            {
                File.Delete(saveFilePath);
                Debug.Log($"✅ Saved chat log file deleted: {saveFilePath}");
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
        
        // 3. UI 텍스트 초기화
        if (outputText != null)
        {
            outputText.text = "새로운 대화를 시작합니다.";
        }
    }
    
    public void SendChatMessageFromUI()
    {
        if (isAITalking) return; 

        string userInput = inputField.text;
        if (string.IsNullOrWhiteSpace(userInput)) return;
        
        outputText.text = "🤖 임승혁 생각 중...";
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
        
        // 1. 사용자 메시지 기록
        AddMessageToHistory("user", userInput);
        // 2. 사용자 메시지 기록 즉시 저장 (앱 강제 종료 대비)
        SaveChatHistory();
        
        StartCoroutine(SendRequestCoroutine());
    }

    // 사용자/AI 메시지를 conversationHistory에 추가하는 통합 함수
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
                                    // 1. AI 응답 기록 및 저장
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
        ApplyEmotionToAnimator(emotion);
        
        string costume = ExtractCostume(responseText);
        ApplyCostumeToAnimator(costume);
        
        string cleanResponse = Regex.Replace(responseText, @"\[.*?\]", "").Trim();
        
        if (outputText != null)
        {
            StartCoroutine(TypeTextCoroutine(cleanResponse));
        }
        
        //SetAnimatorBool("IsTalking", true); 
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
            //SetAnimatorBool("IsTalking", !characterAnimator.GetBool("IsTalking")); 
        }
    }
    
    private void SetAnimatorBool(string paramName, bool state)
    {
        if (characterAnimator != null)
        {
            characterAnimator.SetBool(paramName, state);
        }
    }
    
    private void ApplyEmotionToAnimator(string emotionKey)
    {
        if (characterAnimator == null) return;
        
        if (!detailedEmotionMap.ContainsKey(emotionKey))
        {
            emotionKey = "평온"; 
        }

        var emotionStates = detailedEmotionMap[emotionKey];
        
        characterAnimator.SetInteger("Emotion", emotionStates.Eye);
        characterAnimator.SetInteger("Emotion", emotionStates.Eyebrow);
        characterAnimator.SetInteger("Emotion", emotionStates.Mouth);
        characterAnimator.SetInteger("Emotion", emotionStates.Eff); 
        
        Debug.Log($"Emotion Applied: {emotionKey}. States: Eye={emotionStates.Eye}, Eyebrow={emotionStates.Eyebrow}, Mouth={emotionStates.Mouth}, Eff={emotionStates.Eff}");
    }
    
    private string ExtractEmotion(string responseText)
    {
        Match match = Regex.Match(responseText, @"\[(.*?)\]");
        if (match.Success)
        {
            return match.Groups[1].Value.Trim().ToLower();
        }
        return "평온"; 
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
        
        //ApplyEmotionToAnimator("행복");
    }
}