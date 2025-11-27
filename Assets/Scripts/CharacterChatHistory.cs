using System.IO;
using System.Text.RegularExpressions;
using UnityEngine;
using System.Collections.Generic;
using TMPro;  
using UnityEngine.SceneManagement;
using File = System.IO.File;

public class MainMenuController : MonoBehaviour
{
    private const string DialogueSceneName = "ChatScene"; 
    
    public List<CharacterDataAsset> allCharacterDataAssets; 

    private string saveDirectory;
    public GameObject characterButtonPrefab;
    public Transform contentParent;

    private Dictionary<string, CharacterDataAsset> nameToDataMap;
    
    private void Start()
    {
        nameToDataMap = new Dictionary<string, CharacterDataAsset>();
        foreach (var data in allCharacterDataAssets)
        {
            nameToDataMap[data.aiName.Replace("_", " ")] = data;
        }

        saveDirectory = Application.persistentDataPath; 
        LoadCharacterSummaries();
    }

    public void LoadCharacterSummaries()
    {
        ClearExistingButtons(); 
        
        List<CharacterSummary> summaries = new List<CharacterSummary>();
        
        foreach (var dataAsset in allCharacterDataAssets) 
        {
            string characterNameKey = dataAsset.aiName.Replace("_", " ");
            string safeName = Regex.Replace(dataAsset.aiName, @"[^a-zA-Z0-9가-힣]", "_");
            string expectedFileName = $"{safeName}_chat_log.txt";
            string filePath = Path.Combine(saveDirectory, expectedFileName);

            string lastResponse = null;
            bool fileExists = File.Exists(filePath);
            
            if (fileExists)
            {
                lastResponse = GetLastAIResponse(filePath);
            }
            
            CharacterSummary summary = new CharacterSummary
            {
                name = characterNameKey,
                saveFileName = expectedFileName,
                lastResponse = fileExists && !string.IsNullOrEmpty(lastResponse) 
                               ? TruncateString(lastResponse, 10) 
                               : "새 대화 시작..."
            };
            summaries.Add(summary);
        }
        
        DisplaySummaries(summaries);
    }

    private void ClearExistingButtons()
    {
        int childCount = contentParent.childCount;
        for (int i = 0; i < childCount; i++)
        {
            Destroy(contentParent.GetChild(i).gameObject);
        }
    }

    private string GetLastAIResponse(string filePath)
    {
        string[] lines = File.ReadAllLines(filePath);
        for (int i = lines.Length - 1; i >= 0; i--)
        {
            if (lines[i].StartsWith("[model]"))
            {
                string response = lines[i].Substring("[model]".Length).Trim();
                response = Regex.Replace(response, @"\[.*?\]", "").Trim();
                return response;
            }
        }
        return null;
    }

    private string TruncateString(string s, int maxLength)
    {
        if (string.IsNullOrEmpty(s)) return "...";
        if (s.Length <= maxLength) return s;
        return s.Substring(0, maxLength) + "...";
    }

    private void DisplaySummaries(List<CharacterSummary> summaries)
    {
        foreach (var summary in summaries)
        {
            GameObject buttonObj = Instantiate(characterButtonPrefab, contentParent);
            
            TextMeshProUGUI nameText = buttonObj.transform.Find("NameText").GetComponent<TextMeshProUGUI>();
            TextMeshProUGUI lastResponseText = buttonObj.transform.Find("LastResponseText").GetComponent<TextMeshProUGUI>();
            UnityEngine.UI.Button button = buttonObj.GetComponent<UnityEngine.UI.Button>();

            nameText.text = summary.name;
            lastResponseText.text = summary.lastResponse;
            
            button.onClick.AddListener(() => LoadCharacterScene(summary.name));
        }
    }
    
    private void LoadCharacterScene(string _name)
    {
        if (nameToDataMap.TryGetValue(_name, out CharacterDataAsset selectedData))
        {
            PlayerPrefs.SetString("LAST_CHARACTER_AI_NAME", selectedData.aiName);
            PlayerPrefs.Save();

            SceneManager.LoadScene(DialogueSceneName);
            Debug.Log($"Loading single scene '{DialogueSceneName}' for character: {_name} ({selectedData.aiName})");
        }
        else
        {
            Debug.LogError($"Character data asset not found for: {_name}");
        }
    }
}