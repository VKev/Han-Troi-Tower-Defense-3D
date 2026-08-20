using UnityEditor;
using UnityEngine;

namespace TheVayuputra.ToonShader
{
    /// <summary>
    /// Keeps the outline pass's draw call from being issued at all when Stroke Width is 0,
    /// instead of letting the GPU render a zero-width (invisible) outline every frame.
    /// </summary>
    public sealed class ToonShaderGUI : ShaderGUI
    {
        private const string OutlinePassName = "OutlinePass";
        private const string StrokeWidthProperty = "_StrokeWidth";

        public override void OnGUI(MaterialEditor materialEditor, MaterialProperty[] properties)
        {
            materialEditor.PropertiesDefaultGUI(properties);
            ApplyOutlinePassState(materialEditor, properties);
        }

        public override void AssignNewShaderToMaterial(
            Material material,
            Shader oldShader,
            Shader newShader)
        {
            base.AssignNewShaderToMaterial(material, oldShader, newShader);
            SetOutlinePassEnabled(material, material.GetFloat(StrokeWidthProperty) > 0f);
        }

        private static void ApplyOutlinePassState(
            MaterialEditor materialEditor,
            MaterialProperty[] properties)
        {
            MaterialProperty strokeWidth = FindProperty(StrokeWidthProperty, properties, false);
            if (strokeWidth == null)
            {
                return;
            }

            bool enabled = strokeWidth.floatValue > 0f;
            foreach (Object target in materialEditor.targets)
            {
                SetOutlinePassEnabled((Material)target, enabled);
            }
        }

        private static void SetOutlinePassEnabled(Material material, bool enabled)
        {
            if (material.GetShaderPassEnabled(OutlinePassName) != enabled)
            {
                material.SetShaderPassEnabled(OutlinePassName, enabled);
                EditorUtility.SetDirty(material);
            }
        }
    }
}
