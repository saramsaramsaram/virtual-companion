using UnityEngine;
using UniVRM10;
using System.Text.RegularExpressions;
using System.Collections.Generic;

public class AICharacterController : MonoBehaviour
{
    private Animator characterAnimator;
    private SkinnedMeshRenderer characterMeshRenderer;
    private CharacterDataAsset loadedData;
    private float animationTransitionDuration;
    private Dictionary<string, EmotionBlendShapeConfig> blendShapeMap;
    private Dictionary<string, int> costumeMap = new Dictionary<string, int>{ {"기본", 0} };

    public void Initialize(Animator animator, SkinnedMeshRenderer meshRenderer, CharacterDataAsset data, float transitionDuration)
    {
        this.characterAnimator = animator;
        this.characterMeshRenderer = meshRenderer;
        this.loadedData = data;
        this.animationTransitionDuration = transitionDuration;

        if (data.modelType == CharacterDataAsset.ModelType.VRM_BLENDSHAPE && meshRenderer != null)
        {
            InitializeBlendShapeMap();
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
    
    public string ProcessResponseVisuals(string responseText)
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
        return emotion;
    }

    public void PlayTalkAnimation(string emotion)
    {
        if (characterAnimator == null) return;

        string talkStateName = $"{emotion}_Talk";
        if (characterAnimator.HasState(0, Animator.StringToHash(talkStateName)))
        {
            characterAnimator.CrossFadeInFixedTime(talkStateName, animationTransitionDuration, 0, 0f);
        }
        else
        {
            characterAnimator.CrossFadeInFixedTime("Talk", animationTransitionDuration, 0, 0f);
        }
    }

    public void PlayIdleAnimation(string emotion)
    {
        if (characterAnimator == null) return;
        
        string idleStateName = $"{emotion}_Idle";
        
        if (characterAnimator.HasState(0, Animator.StringToHash(idleStateName)))
        {
            characterAnimator.CrossFadeInFixedTime(idleStateName, animationTransitionDuration, 0, 0f);
        }
        else
        {
            characterAnimator.CrossFadeInFixedTime("Idle", animationTransitionDuration, 0, 0f);
        }
    }
    
    private void ApplyEmotionToBlendShape(string emotionKey)
    {
        if (characterMeshRenderer == null || characterMeshRenderer.sharedMesh == null || blendShapeMap == null) return;
        int blendShapeCount = characterMeshRenderer.sharedMesh.blendShapeCount;

        for (int i = 0; i < blendShapeCount; i++)
        {
            characterMeshRenderer.SetBlendShapeWeight(i, 0f); 
        }
        
        if (blendShapeMap.TryGetValue(emotionKey.ToLower(), out EmotionBlendShapeConfig config))
        {
            foreach (var pair in config.blendShapes)
            {
                if (string.IsNullOrWhiteSpace(pair.name)) continue;
                int blendShapeIndex = characterMeshRenderer.sharedMesh.GetBlendShapeIndex(pair.name);
                float weight_0_100 = pair.weight;
                
                if (blendShapeIndex >= 0)
                {
                    characterMeshRenderer.SetBlendShapeWeight(blendShapeIndex, weight_0_100); 
                }
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
        Match match = Regex.Match(responseText, @"\[(.*?)\]");
        if (match.Success)
        {
            string value = match.Groups[1].Value.Trim();
            Match emotionMatch = Regex.Match(value, @"^(.*?)(?:\[|$)");
            string emotion = emotionMatch.Success ? emotionMatch.Groups[1].Value.Trim().ToLower() : value.ToLower();
            if (emotion.Contains("복장:")) return "행복";
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
}