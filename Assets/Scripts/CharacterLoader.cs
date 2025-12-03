using UnityEngine;
using UniVRM10;
using System.Linq;
using System.Collections.Generic;

public class CharacterLoader : MonoBehaviour
{
    private Animator characterAnimator; 
    private SkinnedMeshRenderer characterMeshRenderer; 
    private Vrm10Instance vrmInstance;
    private CharacterDataAsset loadedData;
    private Dictionary<string, EmotionBlendShapeConfig> blendShapeMap;

    public Animator CharacterAnimator => characterAnimator;
    public SkinnedMeshRenderer CharacterMeshRenderer => characterMeshRenderer;
    public CharacterDataAsset LoadedData => loadedData;
    public Dictionary<string, EmotionBlendShapeConfig> BlendShapeMap => blendShapeMap;

    public void DestroyOldModel()
    {
        GameObject[] oldModels = GameObject.FindGameObjectsWithTag("CharacterModel");
        foreach (GameObject model in oldModels)
        {
            Destroy(model);
        }
    }

    public void LoadCharacter()
    {
        string lastAiName = PlayerPrefs.GetString("LAST_CHARACTER_AI_NAME", string.Empty);
        
        if (string.IsNullOrEmpty(lastAiName))
        {
            Debug.LogError("LAST_CHARACTER_AI_NAME을 찾을 수 없습니다.");
            return;
        }

        loadedData = Resources.LoadAll<CharacterDataAsset>("CharacterData")
                           .FirstOrDefault(data => data.aiName == lastAiName);

        if (loadedData == null)
        {
            Debug.LogError($"'{lastAiName}'에 해당하는 CharacterDataAsset을 찾을 수 없습니다.");
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
                        break;
                    }
                }
                
                vrmInstance = characterInstance.GetComponent<Vrm10Instance>();
                
                if (vrmInstance == null)
                {
                    Debug.LogError("Vrm10Instance Not found");
                }

                InitializeBlendShapeMap();
            }

            if (characterAnimator != null)
            {
                characterAnimator.Play("Idle", 0, 0f); 
            }
        }
        else
        {
            Debug.LogError($"{loadedData.aiName}의 characterPrefab이 CharacterDataAsset에 연결되지 않았습니다.");
            loadedData = null; 
        }
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
}