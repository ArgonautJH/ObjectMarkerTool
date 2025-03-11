using System;
using UnityEngine;

namespace ArgonautJH.ObjectMarkerTool.Editor
{
    [CreateAssetMenu(fileName = "MaterialPresetDatabase", menuName = "Settings/Material Preset Database")]
    public class MaterialPresetDatabase : ScriptableObject {
        public MaterialPreset[] Presets;
    }
    
    [Serializable]
    public class MaterialPreset
    {
        public string PresetName;                   // 프리셋 이름
        public Color Color = Color.white;         // 색상
        [Range(0, 1)] public float Alpha = 1.0f;    // 투명도 
    }

}