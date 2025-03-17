using UnityEngine;

namespace ArgonautJH.ObjectMarkerTool.Editor
{
    /// <summary>
    /// HDRP Lit용 Material 설정 클래스
    /// </summary>
    public class HDRPLitConfigurator : IMaterialConfigurator
    {
        private static readonly int BlendMode = Shader.PropertyToID("_BlendMode");
        private static readonly int SurfaceType = Shader.PropertyToID("_SurfaceType");
        private static readonly int BaseColor = Shader.PropertyToID("_BaseColor");

        public void Configure(Material material, Color newColor, float alpha)
        {
            // Surface Type을 Transparent(1)로, Blend Mode를 Alpha(0)로 설정
            material.SetFloat(SurfaceType, 1.0f);
            material.SetFloat(BlendMode, 0.0f);

            newColor.a = alpha;
            material.SetColor(BaseColor, newColor);
        }
    }
}