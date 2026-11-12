// =====================================================================================
//  HappyBalloon / PBR Hex Tiling Lit
//  텍스처 반복(타일링 패턴)이 눈에 띄지 않게 해 주는 URP PBR 셰이더.
//  조명·그림자·안개·Forward+ 는 URP Lit 과 동일하게 처리되고, 텍스처 샘플링만 헥스 타일링을 쓴다.
//
//  사용법
//   1) 머티리얼의 Shader 를 "HappyBalloon/PBR Hex Tiling Lit" 으로 바꾼다.
//   2) Base Map / Normal Map / Metallic·AO·Smoothness Map 에 기존 PBR 텍스처를 그대로 넣는다.
//      (마스크 채널: R = Metallic, G = AO, A = Smoothness)
//   3) 텍스처 종류에 맞게 조절
//      - 콘크리트·회벽·녹·카펫 같은 무작위 재질 : Rotation 1, Offset Snap 0
//      - 타일·줄무늬 벽지·마루처럼 격자가 있는 재질 : Rotation 0,
//        Offset Snap = 텍스처 한 장 안에서 "패턴이 반복되는 주기" 의 개수
//        (예: 체크 타일(흑백 4x4, 주기 2칸) → (2,2) / 줄무늬 16줄(주기 2줄) → (8,0) / 마루판 13줄 → (13,0)
//             천장판 4x4 → (4,4))
//      - 놀이방 퍼즐 매트처럼 색 배열이 텍스처 한 장 단위인 재질 : Enable Hex Tiling 끄고 Macro Variation 만 사용
//   4) Surface Type (인스펙터 맨 위)
//      - Opaque      : 불투명 (기본값)
//      - Transparent : 반투명. 투명도 = Base Map 알파(A) x Base Color 알파.
//                      반사광(하이라이트)은 투명해도 유지된다 (풍선·유리 같은 재질용).
//      블렌드·깊이 쓰기·렌더 큐·키워드는 HB_PBR_HexTilingGUI.cs 가 자동으로 맞춘다.
// =====================================================================================
Shader "HappyBalloon/PBR Hex Tiling Lit"
{
    Properties
    {
        [Header(Surface Maps)]
        [MainTexture] _BaseMap ("Base Map (RGB) / Alpha (A)", 2D) = "white" {}
        [MainColor] _BaseColor ("Base Color Tint", Color) = (1, 1, 1, 1)
        [Normal] _BumpMap ("Normal Map", 2D) = "bump" {}
        _BumpScale ("Normal Strength", Range(0, 2)) = 1.0
        [NoScaleOffset] _MetallicGlossMap ("Metallic (R) AO (G) Smoothness (A)", 2D) = "white" {}
        _Metallic ("Metallic Multiplier", Range(0, 1)) = 1.0
        _Smoothness ("Smoothness Multiplier", Range(0, 1)) = 1.0
        _OcclusionStrength ("AO Strength", Range(0, 1)) = 1.0
        [NoScaleOffset] _EmissionMap ("Emission Map", 2D) = "white" {}
        [HDR] _EmissionColor ("Emission Color", Color) = (0, 0, 0, 1)

        [Header(Hex Tiling Anti Repetition)]
        [ToggleUI] _HexTiling ("Enable Hex Tiling", Float) = 1
        _HexScale ("Hex Cell Size (x texture size)", Range(0.25, 4)) = 1.0
        _HexRotation ("Random Rotation", Range(0, 1)) = 0.0
        _HexOffsetSnap ("Offset Snap Grid (X, Y, 0 = free)", Vector) = (0, 0, 0, 0)
        _HexBlendContrast ("Blend Contrast", Range(0.01, 0.99)) = 0.6
        _HexLumaFalloff ("Luminance Weighting", Range(0, 1)) = 0.6

        [Header(Macro Variation)]
        _MacroStrength ("Macro Variation Strength", Range(0, 1)) = 0.25
        _MacroScale ("Macro Variation Frequency (per texture)", Range(0.01, 2)) = 0.15

        [Header(Advanced)]
        [Toggle(_ALPHATEST_ON)] _AlphaClip ("Alpha Clipping", Float) = 0
        _Cutoff ("Alpha Cutoff", Range(0, 1)) = 0.5
        [Enum(UnityEngine.Rendering.CullMode)] _Cull ("Cull", Float) = 2

        // Surface Type (커스텀 인스펙터 HB_PBR_HexTilingGUI 가 그리고, 아래 값들을 자동으로 설정)
        [HideInInspector] _Surface ("__surface", Float) = 0
        [HideInInspector] _SrcBlend ("__src", Float) = 1
        [HideInInspector] _DstBlend ("__dst", Float) = 0
        [HideInInspector] _SrcBlendAlpha ("__srcA", Float) = 1
        [HideInInspector] _DstBlendAlpha ("__dstA", Float) = 0
        [HideInInspector] _ZWrite ("__zw", Float) = 1

        // URP 내부 패스가 참조하는 값 (인스펙터에는 숨김)
        [HideInInspector] _SpecColor ("Specular", Color) = (0.2, 0.2, 0.2)
        [HideInInspector] _Parallax ("Scale", Float) = 0.005
        [HideInInspector] _ClearCoatMask ("ClearCoat", Float) = 0
        [HideInInspector] _ClearCoatSmoothness ("ClearCoat Smoothness", Float) = 0
        [HideInInspector] _DetailAlbedoMapScale ("Detail Scale", Float) = 1
        [HideInInspector] _DetailNormalMapScale ("Detail Normal Scale", Float) = 1
        [HideInInspector] _DetailAlbedoMap ("Detail", 2D) = "linearGrey" {}
    }

    SubShader
    {
        Tags
        {
            "RenderType" = "Opaque"
            "RenderPipeline" = "UniversalPipeline"
            "UniversalMaterialType" = "Lit"
            "Queue" = "Geometry"
            "IgnoreProjector" = "True"
        }
        LOD 300

        // ------------------------------------------------------------------ Forward (Forward / Forward+)
        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode" = "UniversalForward" }

            Blend [_SrcBlend] [_DstBlend], [_SrcBlendAlpha] [_DstBlendAlpha]
            ZWrite [_ZWrite]
            Cull [_Cull]

            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex LitPassVertex
            #pragma fragment LitPassFragment

            #pragma shader_feature_local_fragment _ALPHATEST_ON
            #pragma shader_feature_local_fragment _SURFACE_TYPE_TRANSPARENT
            #pragma shader_feature_local_fragment _ALPHAPREMULTIPLY_ON

            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile _ _ADDITIONAL_LIGHTS_VERTEX _ADDITIONAL_LIGHTS
            #pragma multi_compile _ EVALUATE_SH_MIXED EVALUATE_SH_VERTEX
            #pragma multi_compile_fragment _ _ADDITIONAL_LIGHT_SHADOWS
            #pragma multi_compile_fragment _ _REFLECTION_PROBE_BLENDING
            #pragma multi_compile_fragment _ _REFLECTION_PROBE_BOX_PROJECTION
            #pragma multi_compile_fragment _ _REFLECTION_PROBE_ATLAS
            #pragma multi_compile_fragment _ _SHADOWS_SOFT _SHADOWS_SOFT_LOW _SHADOWS_SOFT_MEDIUM _SHADOWS_SOFT_HIGH
            #pragma multi_compile_fragment _ _SCREEN_SPACE_OCCLUSION
            #pragma multi_compile_fragment _ _SCREEN_SPACE_IRRADIANCE
            #pragma multi_compile_fragment _ _DBUFFER_MRT1 _DBUFFER_MRT2 _DBUFFER_MRT3
            #pragma multi_compile_fragment _ _LIGHT_COOKIES
            #pragma multi_compile _ _LIGHT_LAYERS
            #pragma multi_compile _ _CLUSTER_LIGHT_LOOP
            #include_with_pragmas "Packages/com.unity.render-pipelines.core/ShaderLibrary/FoveatedRenderingKeywords.hlsl"
            #include_with_pragmas "Packages/com.unity.render-pipelines.universal/ShaderLibrary/RenderingLayers.hlsl"

            #pragma multi_compile _ LIGHTMAP_SHADOW_MIXING
            #pragma multi_compile _ SHADOWS_SHADOWMASK
            #pragma multi_compile _ DIRLIGHTMAP_COMBINED
            #pragma multi_compile _ LIGHTMAP_ON
            #pragma multi_compile_fragment _ LIGHTMAP_BICUBIC_SAMPLING
            #pragma multi_compile_fragment _ REFLECTION_PROBE_ROTATION
            #pragma multi_compile _ DYNAMICLIGHTMAP_ON
            #pragma multi_compile _ USE_LEGACY_LIGHTMAPS
            #pragma multi_compile _ LOD_FADE_CROSSFADE
            #pragma multi_compile_fragment _ DEBUG_DISPLAY
            #include_with_pragmas "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Fog.hlsl"
            #include_with_pragmas "Packages/com.unity.render-pipelines.universal/ShaderLibrary/ProbeVolumeVariants.hlsl"

            #pragma multi_compile_instancing
            #pragma instancing_options renderinglayer

            #include "HB_HexTilingInput.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/Shaders/LitForwardPass.hlsl"
            ENDHLSL
        }

        // ------------------------------------------------------------------ Shadow caster
        Pass
        {
            Name "ShadowCaster"
            Tags { "LightMode" = "ShadowCaster" }

            ZWrite On
            ZTest LEqual
            ColorMask 0
            Cull [_Cull]

            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex ShadowPassVertex
            #pragma fragment ShadowPassFragment
            #pragma shader_feature_local _ALPHATEST_ON
            #pragma multi_compile_instancing
            #pragma multi_compile _ LOD_FADE_CROSSFADE
            #pragma multi_compile_vertex _ _CASTING_PUNCTUAL_LIGHT_SHADOW

            #include "HB_HexTilingInput.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/Shaders/ShadowCasterPass.hlsl"
            ENDHLSL
        }

        // ------------------------------------------------------------------ Depth only
        Pass
        {
            Name "DepthOnly"
            Tags { "LightMode" = "DepthOnly" }

            ZWrite On
            ColorMask R
            Cull [_Cull]

            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex DepthOnlyVertex
            #pragma fragment DepthOnlyFragment
            #pragma shader_feature_local _ALPHATEST_ON
            #pragma multi_compile _ LOD_FADE_CROSSFADE
            #pragma multi_compile_instancing

            #include "HB_HexTilingInput.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/Shaders/DepthOnlyPass.hlsl"
            ENDHLSL
        }

        // ------------------------------------------------------------------ Depth + normals (SSAO 등)
        Pass
        {
            Name "DepthNormals"
            Tags { "LightMode" = "DepthNormals" }

            ZWrite On
            Cull [_Cull]

            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex DepthNormalsVertex
            #pragma fragment DepthNormalsFragment
            #pragma shader_feature_local _ALPHATEST_ON
            #pragma multi_compile _ LOD_FADE_CROSSFADE
            #include_with_pragmas "Packages/com.unity.render-pipelines.universal/ShaderLibrary/RenderingLayers.hlsl"
            #pragma multi_compile_instancing

            #include "HB_HexTilingInput.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/Shaders/LitDepthNormalsPass.hlsl"
            ENDHLSL
        }
    }

    FallBack "Hidden/Universal Render Pipeline/FallbackError"
    CustomEditor "HB_PBR_HexTilingGUI"
}
