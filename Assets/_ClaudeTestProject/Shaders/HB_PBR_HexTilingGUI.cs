// =====================================================================================
//  HappyBalloon / PBR Hex Tiling Lit - 머티리얼 인스펙터
//  인스펙터 맨 위에 Surface Type (Opaque / Transparent) 선택을 추가하고,
//  선택에 맞춰 블렌드·깊이 쓰기·렌더 큐·셰이더 키워드를 자동으로 설정한다.
//  에디터 전용 코드라서 빌드에는 포함되지 않는다.
// =====================================================================================
#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

public class HB_PBR_HexTilingGUI : ShaderGUI
{
    enum SurfaceType { Opaque = 0, Transparent = 1 }

    static readonly GUIContent SurfaceLabel = new GUIContent(
        "Surface Type",
        "Opaque: 불투명\nTransparent: 반투명 (Base Map 알파 x Base Color 알파, 반사광은 유지)");
    static readonly string[] SurfaceNames = { "Opaque", "Transparent" };

    public override void OnGUI(MaterialEditor materialEditor, MaterialProperty[] properties)
    {
        MaterialProperty surface = FindProperty("_Surface", properties, false);
        if (surface != null)
        {
            EditorGUILayout.LabelField("Surface Options", EditorStyles.boldLabel);

            EditorGUI.showMixedValue = surface.hasMixedValue;
            EditorGUI.BeginChangeCheck();
            int value = EditorGUILayout.Popup(SurfaceLabel, (int)surface.floatValue, SurfaceNames);
            if (EditorGUI.EndChangeCheck())
            {
                materialEditor.RegisterPropertyChangeUndo("Surface Type");
                surface.floatValue = value;
            }
            EditorGUI.showMixedValue = false;

            if (!surface.hasMixedValue && (SurfaceType)(int)surface.floatValue == SurfaceType.Transparent)
            {
                EditorGUILayout.HelpBox(
                    "투명도는 Base Map 의 알파(A) 채널 x Base Color 의 알파 값으로 정해집니다.",
                    MessageType.None);
            }

            EditorGUILayout.Space();
        }

        EditorGUI.BeginChangeCheck();
        base.OnGUI(materialEditor, properties);
        bool changed = EditorGUI.EndChangeCheck();

        // Surface Type 이나 Alpha Clipping 등이 바뀌면 모든 선택된 머티리얼에 다시 적용
        if (changed || GUI.changed)
        {
            foreach (Object target in materialEditor.targets)
            {
                if (target is Material mat)
                    SetupMaterial(mat);
            }
        }
    }

    // 셰이더를 처음 지정했을 때, 머티리얼을 불러왔을 때, Undo 했을 때 등 Unity 가 호출
    public override void ValidateMaterial(Material material)
    {
        SetupMaterial(material);
    }

    public override void AssignNewShaderToMaterial(Material material, Shader oldShader, Shader newShader)
    {
        base.AssignNewShaderToMaterial(material, oldShader, newShader);
        SetupMaterial(material);
    }

    static void SetupMaterial(Material material)
    {
        bool transparent = material.HasProperty("_Surface")
                           && (SurfaceType)(int)material.GetFloat("_Surface") == SurfaceType.Transparent;
        bool alphaClip = material.HasProperty("_AlphaClip") && material.GetFloat("_AlphaClip") > 0.5f;

        // Alpha Clipping 키워드 (Toggle 드로어와 동일하게 유지)
        SetKeyword(material, "_ALPHATEST_ON", alphaClip);

        if (transparent)
        {
            // 반사광 유지(Preserve Specular) 알파 블렌드:
            // 디퓨즈만 알파로 미리 곱하고(_ALPHAPREMULTIPLY_ON) One / OneMinusSrcAlpha 로 섞는다.
            material.SetOverrideTag("RenderType", "Transparent");
            material.renderQueue = (int)RenderQueue.Transparent;
            SetFloat(material, "_SrcBlend", (float)BlendMode.One);
            SetFloat(material, "_DstBlend", (float)BlendMode.OneMinusSrcAlpha);
            SetFloat(material, "_SrcBlendAlpha", (float)BlendMode.One);
            SetFloat(material, "_DstBlendAlpha", (float)BlendMode.OneMinusSrcAlpha);
            SetFloat(material, "_ZWrite", 0f);
            SetKeyword(material, "_SURFACE_TYPE_TRANSPARENT", true);
            SetKeyword(material, "_ALPHAPREMULTIPLY_ON", true);

            // 반투명 물체는 깊이 프리패스에 쓰지 않는다 (그림자는 계속 드리움)
            material.SetShaderPassEnabled("DepthOnly", false);
            material.SetShaderPassEnabled("DepthNormals", false);
        }
        else
        {
            material.SetOverrideTag("RenderType", alphaClip ? "TransparentCutout" : "Opaque");
            material.renderQueue = alphaClip ? (int)RenderQueue.AlphaTest : (int)RenderQueue.Geometry;
            SetFloat(material, "_SrcBlend", (float)BlendMode.One);
            SetFloat(material, "_DstBlend", (float)BlendMode.Zero);
            SetFloat(material, "_SrcBlendAlpha", (float)BlendMode.One);
            SetFloat(material, "_DstBlendAlpha", (float)BlendMode.Zero);
            SetFloat(material, "_ZWrite", 1f);
            SetKeyword(material, "_SURFACE_TYPE_TRANSPARENT", false);
            SetKeyword(material, "_ALPHAPREMULTIPLY_ON", false);

            material.SetShaderPassEnabled("DepthOnly", true);
            material.SetShaderPassEnabled("DepthNormals", true);
        }
    }

    static void SetFloat(Material material, string name, float value)
    {
        if (material.HasProperty(name))
            material.SetFloat(name, value);
    }

    static void SetKeyword(Material material, string keyword, bool on)
    {
        if (on) material.EnableKeyword(keyword);
        else material.DisableKeyword(keyword);
    }
}
#endif
