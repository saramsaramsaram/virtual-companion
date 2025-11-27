using UnityEngine;
using System.Collections.Generic;

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

[CreateAssetMenu(fileName = "NewCharacterData", menuName = "Chat/Character Data Asset")]
public class CharacterDataAsset : ScriptableObject
{
    public string aiName; 
    
    [TextArea(3, 10)]
    public string systemInstruction;

    public ModelType modelType;
    public enum ModelType { VRM_BLENDSHAPE, STANDARD_ANIMATOR };

    public GameObject characterPrefab;

    public List<EmotionBlendShapeConfig> vrmEmotionConfigs;
    
    public Dictionary<string, (int Eye, int Eyebrow, int Mouth, int Eff)> animatorEmotionMap = 
        new Dictionary<string, (int Eye, int Eyebrow, int Mouth, int Eff)>(); 
}