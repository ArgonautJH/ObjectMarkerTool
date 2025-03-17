using UnityEngine;

namespace ArgonautJH.ObjectMarkerTool.Editor
{
    public class URPLitConfigurator : IMaterialConfigurator
    {
        private static readonly int BaseColor = Shader.PropertyToID("_BaseColor");

        public void Configure(Material material, Color newColor, float alpha)
        {
            newColor.a = alpha;
            material.SetColor(BaseColor, newColor);
        }
    }
}