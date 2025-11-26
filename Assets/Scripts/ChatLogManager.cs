using UnityEngine;
using System.IO;
using System.Collections.Generic;

public class ChatLogManager : MonoBehaviour
{
    private Dictionary<string, List<string>> allChatHistories = new Dictionary<string, List<string>>();
    
    public void AddMessage(string aiName, string sender, string message)
    {
        if (!allChatHistories.ContainsKey(aiName)) allChatHistories.Add(aiName, new List<string>());
        string timestamp = System.DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
        string logEntry = $"[{timestamp}] {sender}: {message}";
        
        allChatHistories[aiName].Add(logEntry);
        Debug.Log($"[{aiName}] Added Chat History {logEntry}");
    }
    
    public void SaveAllChats()
    {
        foreach (var entry in allChatHistories)
        {
            string aiName = entry.Key;
            List<string> chatHistory = entry.Value;
            
            string safeName = aiName.Replace(" ", "_");
            string fileName = $"{safeName}_chat_log.txt";
            string saveFilePath = Path.Combine(Application.persistentDataPath, fileName);
            
            try
            {
                string content = string.Join("\n", chatHistory);
                File.WriteAllText(saveFilePath, content);
                
                Debug.Log($"'{aiName}' Chat History Saved {saveFilePath}");
            }
            catch (System.Exception e)
            {
                Debug.LogError($"'{aiName}' Chat History Save Failed " + e.Message);
            }
        }
    }
}