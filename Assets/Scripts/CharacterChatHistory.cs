using System.IO;
using System.Text.RegularExpressions;
using UnityEngine;
using System.Collections.Generic;
using TMPro;  
using UnityEngine.SceneManagement;
using File = System.IO.File;

public class MainMenuController : MonoBehaviour
{
    private string saveDirectory;
    public GameObject characterButtonPrefab;
    public Transform contentParent;

    private void Start()
    {
        saveDirectory = Application.persistentDataPath; 
        LoadCharacterSummaries();
    }

    public void LoadCharacterSummaries()
    {
        ClearExistingButtons(); 
        
        List<CharacterSummary> summaries = new List<CharacterSummary>();
        
        string[] allFiles = Directory.GetFiles(saveDirectory, "*.txt");
        
        foreach (string filePath in allFiles)
        {
            string fileName = Path.GetFileName(filePath);
            
            Match nameMatch = Regex.Match(fileName, @"(.*?)_chat_log\.txt"); 
            
            if (nameMatch.Success)
            {
                string lastResponse = GetLastAIResponse(filePath);
                
                if (!string.IsNullOrEmpty(lastResponse))
                {
                    CharacterSummary summary = new CharacterSummary
                    {
                        name = nameMatch.Groups[1].Value.Replace("_", " "),
                        saveFileName = fileName,

                        lastResponse = TruncateString(lastResponse, 10)
                    };
                    summaries.Add(summary);
                }
            }
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
            
            button.onClick.AddListener(() => LoadCharacterScene(summary.saveFileName, summary.name));
        }
    }
    
    private void LoadCharacterScene(string fileName, string name)
    {
        Debug.Log(name);
        SceneManager.LoadScene(name);
        Debug.Log($"Loading scene for character file: {fileName}");
    }
}