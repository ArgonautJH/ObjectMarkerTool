using UnityEngine;

namespace ArgonautJH.ObjectMarkerTool.Editor
{
    public interface IMaterialConfigurator
    {
        void Configure(Material material, Color newColor, float alpha);
    }
}