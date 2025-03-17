using UnityEngine;

namespace ArgonautJH.ObjectMarkerTool.Editor
{
    /// <summary>
    /// Standard용 Material 설정 클래스
    /// </summary>
    public class StandardConfigurator : IMaterialConfigurator
    {
        private static readonly int Color1 = Shader.PropertyToID("_Color");
        private static readonly int Mode = Shader.PropertyToID("_Mode");

        public void Configure(Material material, Color newColor, float alpha)
        {
            newColor.a = alpha;
            material.SetColor(Color1, newColor);
            material.SetFloat(Mode, 3); // Transparent
        }
    }
}