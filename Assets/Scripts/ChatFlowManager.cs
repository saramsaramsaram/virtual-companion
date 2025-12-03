using UnityEngine;
using System.Collections;
using System.Text.RegularExpressions; 
using System.Linq; 
using TMPro; 

public class ChatFlowManager : MonoBehaviour
{
    [SerializeField] private ChatService chatService; 
    [SerializeField] private CharacterLoader characterLoader;
    [SerializeField] private AICharacterController characterController;
    
    public TMP_InputField inputField;
    public TextMeshProUGUI outputText; 
    
    private bool isAITalking = false;
    public float typingSpeed = 0.05f; 
    public float animationTransitionDuration = 0.25f;

    void Start()
    {
        characterLoader.DestroyOldModel();
        
        characterLoader.LoadCharacter(); 
        
        if (characterLoader.LoadedData == null) 
        {
            Debug.LogError("캐릭터 로딩 실패. 씬을 종료합니다.");
            return;
        }
        
        if (!chatService.CheckAndLoadApiKey())
        {
            Debug.LogError("API 키 로드 실패. 환경 변수를 확인하세요.");
            return;
        }
        
        chatService.Initialize(
            characterLoader.LoadedData.aiName, 
            characterLoader.LoadedData.systemInstruction
        );
        
        if (chatService.ConversationHistory.Count > 0)
        {
            string lastResponseWithTags = chatService.ConversationHistory.Last().parts[0].text;
            string lastCleanMessage = Regex.Replace(lastResponseWithTags, @"\[.*?\]", "").Trim();
            outputText.text = lastCleanMessage;
        }

        characterController.Initialize(
            characterLoader.CharacterAnimator,
            characterLoader.CharacterMeshRenderer,
            characterLoader.LoadedData,
            animationTransitionDuration
        );

        chatService.LoadChatHistory();
    }
    
    public void ResetChatHistory()
    {
        chatService.ResetChatHistory();
        if (outputText != null) outputText.text = "새로운 대화를 시작합니다.";
    }

    public void SendChatMessageFromUI()
    {
        if (isAITalking || string.IsNullOrWhiteSpace(inputField.text)) return;
        string userInput = inputField.text;
        outputText.text = $"{characterLoader.LoadedData.aiName} 생각 중...";
        inputField.text = ""; 
        chatService.SendChatMessage(userInput, HandleFinalAIResponse);
    }

    private void HandleFinalAIResponse(string responseText)
    {
        if (string.IsNullOrEmpty(responseText))
        {
            outputText.text = "응답이 비어있습니다.";
            return;
        }

        string emotion = characterController.ProcessResponseVisuals(responseText);
        
        string cleanResponse = Regex.Replace(responseText, @"\[.*?\]", "").Trim();
        
        if (characterLoader.CharacterAnimator != null)
        {
            characterController.PlayTalkAnimation(emotion);
        }

        if (outputText != null)
        {
            StartCoroutine(TypeTextCoroutine(cleanResponse));
        }
        
        float talkDuration = Mathf.Clamp(cleanResponse.Length * 0.07f, 1f, 1000f);
        StartCoroutine(StopTalkingAfterDelay(talkDuration, emotion)); 
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
    
    IEnumerator StopTalkingAfterDelay(float delay, string emotion)
    {
        yield return new WaitForSeconds(delay);
        
        characterController.PlayIdleAnimation(emotion);

        if (inputField != null)
        {
            inputField.interactable = true;
            inputField.ActivateInputField();
        }
    }
}