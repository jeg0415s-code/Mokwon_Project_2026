// =====================================================================================
//  HappyBalloon / PBR Hex Tiling Lit  -  surface input
//  URP Lit 의 LitInput.hlsl 을 대체한다. 조명/그림자/GI 는 URP 기본 패스를 그대로 쓰고,
//  텍스처 샘플링만 "헥스 타일링(Hex-Tiling, Mikkelsen 2022)" 으로 바꿔 반복 패턴을 없앤다.
//
//  - UV 평면을 육각형 셀로 나누고, 셀마다 무작위 오프셋(+선택적 회전)으로 텍스처를 3번 샘플링
//  - 밝기 기반 가중치로 세 샘플을 이음매 없이 블렌드
//  - 노멀맵은 회전량만큼 되돌려서 조명 방향이 틀어지지 않게 보정
//  - 넓은 범위 밝기/거칠기 변화(Macro Variation)로 남은 균일함까지 깨뜨림
//
//  마스크 텍스처 채널: R = Metallic, G = Ambient Occlusion, A = Smoothness
// =====================================================================================
#ifndef UNIVERSAL_LIT_INPUT_INCLUDED
#define UNIVERSAL_LIT_INPUT_INCLUDED

// 이 셰이더는 항상 노멀맵/마스크맵을 사용한다 (텍스처가 비어 있으면 기본값이 평평한 노멀/흰색).
#ifndef _NORMALMAP
    #define _NORMALMAP 1
#endif

#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/CommonMaterial.hlsl"
#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/SurfaceInput.hlsl"
#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/ParallaxMapping.hlsl"
#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DBuffer.hlsl"
#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/DebugMipmapStreamingMacros.hlsl"

// NOTE: SRP Batcher 호환을 위해 모든 패스에서 동일한 레이아웃을 유지한다.
CBUFFER_START(UnityPerMaterial)
float4 _BaseMap_ST;
float4 _BaseMap_TexelSize;
float4 _DetailAlbedoMap_ST;
half4 _BaseColor;
half4 _SpecColor;
half4 _EmissionColor;
half _Cutoff;
half _Smoothness;
half _Metallic;
half _BumpScale;
half _Parallax;
half _OcclusionStrength;
half _ClearCoatMask;
half _ClearCoatSmoothness;
half _DetailAlbedoMapScale;
half _DetailNormalMapScale;
half _Surface;
// ---- hex tiling
half _HexTiling;
half _HexScale;
half _HexRotation;
half _HexBlendContrast;
half _HexLumaFalloff;
float4 _HexOffsetSnap;
// ---- macro variation
half _MacroStrength;
half _MacroScale;
UNITY_TEXTURE_STREAMING_DEBUG_VARS;
CBUFFER_END

TEXTURE2D(_ParallaxMap);        SAMPLER(sampler_ParallaxMap);
TEXTURE2D(_MetallicGlossMap);   SAMPLER(sampler_MetallicGlossMap);
TEXTURE2D(_DetailAlbedoMap);    SAMPLER(sampler_DetailAlbedoMap);
TEXTURE2D(_DetailNormalMap);    SAMPLER(sampler_DetailNormalMap);
TEXTURE2D(_DetailMask);         SAMPLER(sampler_DetailMask);

void ApplyPerPixelDisplacement(half3 viewDirTS, inout float2 uv)
{
#if defined(_PARALLAXMAP)
    uv += ParallaxMapping(TEXTURE2D_ARGS(_ParallaxMap, sampler_ParallaxMap), viewDirTS, _Parallax, uv);
#endif
}

// =====================================================================================
//  Hex tiling core
// =====================================================================================
#define HB_SQRT3 1.7320508

// st 를 삼각 격자로 나누고, 픽셀이 속한 삼각형의 세 꼭짓점(=육각 셀 중심) 과 무게를 구한다.
void HB_TriangleGrid(float2 st, out float3 w, out int2 v1, out int2 v2, out int2 v3)
{
    st *= 2.0 * HB_SQRT3;
    const float2x2 gridToSkewedGrid = float2x2(1.0, -0.57735027, 0.0, 1.15470054);
    float2 skewed = mul(gridToSkewedGrid, st);
    int2 baseId = int2(floor(skewed));
    float3 t = float3(frac(skewed), 0.0);
    t.z = 1.0 - t.x - t.y;
    float s = step(0.0, -t.z);
    float s2 = 2.0 * s - 1.0;
    w = float3(-t.z * s2, s - t.y * s2, s - t.x * s2);
    v1 = baseId + int2(s, s);
    v2 = baseId + int2(s, 1 - s);
    v3 = baseId + int2(1 - s, s);
}

float2 HB_Hash2(float2 p)
{
    float2 r = mul(float2x2(127.1, 311.7, 269.5, 183.3), p);
    return frac(sin(r) * 43758.5453);
}

float HB_Hash1(float2 p)
{
    return frac(sin(dot(p, float2(127.1, 311.7))) * 43758.5453);
}

float2 HB_CellCenter(int2 v)
{
    const float2x2 invSkew = float2x2(1.0, 0.5, 0.0, 1.0 / 1.15470054);
    return mul(invSkew, float2(v)) / (2.0 * HB_SQRT3);
}

float2x2 HB_CellRotation(int2 id, float strength)
{
    float a = abs(id.x * id.y) + abs(id.x + id.y) + PI;
    a = fmod(a, 2.0 * PI);
    if (a < 0.0) a += 2.0 * PI;
    if (a > PI) a -= 2.0 * PI;
    a *= strength;
    float cs = cos(a), sn = sin(a);
    return float2x2(cs, -sn, sn, cs);
}

float3 HB_Gain3(float3 x, float r)
{
    float k = log(1.0 - r) / log(0.5);
    float3 s = 2.0 * step(0.5, x);
    float3 m = 2.0 * (1.0 - s);
    float3 res = 0.5 * s + 0.25 * m * pow(max(0.0, s + x * m), k);
    return res / (res.x + res.y + res.z);
}

struct HBHex
{
    float2 uv1, uv2, uv3;
    float2x2 r1, r2, r3;
    float2 dx, dy;
    float3 w;   // 기하 무게 (밝기 보정 전)
};

float2 HB_Offset(int2 v)
{
    float2 o = HB_Hash2(float2(v));
    // 타일/줄무늬처럼 격자가 있는 텍스처는 오프셋을 격자 단위로 맞춰 줄눈 위치를 유지한다.
    if (_HexOffsetSnap.x > 0.5) o.x = round(o.x * _HexOffsetSnap.x) / _HexOffsetSnap.x;
    if (_HexOffsetSnap.y > 0.5) o.y = round(o.y * _HexOffsetSnap.y) / _HexOffsetSnap.y;
    return o;
}

HBHex HB_BuildHex(float2 uv, float2 duvdx, float2 duvdy)
{
    HBHex h;
    float scale = max(_HexScale, 0.05);
    int2 v1, v2, v3;
    HB_TriangleGrid(uv / scale, h.w, v1, v2, v3);
    h.r1 = HB_CellRotation(v1, _HexRotation);
    h.r2 = HB_CellRotation(v2, _HexRotation);
    h.r3 = HB_CellRotation(v3, _HexRotation);
    float2 c1 = HB_CellCenter(v1) * scale, c2 = HB_CellCenter(v2) * scale, c3 = HB_CellCenter(v3) * scale;
    h.uv1 = mul(uv - c1, h.r1) + c1 + HB_Offset(v1);
    h.uv2 = mul(uv - c2, h.r2) + c2 + HB_Offset(v2);
    h.uv3 = mul(uv - c3, h.r3) + c3 + HB_Offset(v3);
    h.dx = duvdx;
    h.dy = duvdy;
    return h;
}

#define HB_SAMPLE(tex, samp, h, i) SAMPLE_TEXTURE2D_GRAD(tex, samp, h.uv##i, mul(h.dx, h.r##i), mul(h.dy, h.r##i))

// 세 샘플의 밝기를 반영한 최종 블렌드 무게
float3 HB_Weights(HBHex h, half3 c1, half3 c2, half3 c3)
{
    const float3 lumW = float3(0.299, 0.587, 0.114);
    float3 dw = float3(dot(c1, lumW), dot(c2, lumW), dot(c3, lumW));
    dw = lerp(1.0, dw, _HexLumaFalloff);
    float3 W = dw * pow(max(h.w, 1e-4), 7.0);
    W /= (W.x + W.y + W.z);
    return HB_Gain3(W, clamp(_HexBlendContrast, 0.01, 0.99));
}

half3 HB_RotateNormal(half3 n, float2x2 r)
{
    n.xy = mul(r, n.xy);
    return n;
}

// ---- macro variation (부드러운 값 노이즈)
float HB_ValueNoise(float2 p)
{
    float2 i = floor(p), f = frac(p);
    float2 u = f * f * (3.0 - 2.0 * f);
    float a = HB_Hash1(i), b = HB_Hash1(i + float2(1, 0)), c = HB_Hash1(i + float2(0, 1)), d = HB_Hash1(i + float2(1, 1));
    return lerp(lerp(a, b, u.x), lerp(c, d, u.x), u.y);
}

float HB_Macro(float2 uv)
{
    float2 p = uv * max(_MacroScale, 0.001);
    return HB_ValueNoise(p) * 0.65 + HB_ValueNoise(p * 2.7 + 17.3) * 0.35;
}

// =====================================================================================
//  Surface data (URP LitForwardPass 가 호출)
// =====================================================================================
inline void InitializeStandardLitSurfaceData(float2 uv, out SurfaceData outSurfaceData)
{
    half4 albedo;
    half4 mask;
    half3 normalTS;
    half3 emission;

    // 미분값은 분기 밖에서 계산 (분기 안 gradient 연산 경고 방지)
    float2 duvdx = ddx(uv), duvdy = ddy(uv);

    UNITY_BRANCH
    if (_HexTiling > 0.5)
    {
        HBHex h = HB_BuildHex(uv, duvdx, duvdy);
        half4 a1 = HB_SAMPLE(_BaseMap, sampler_BaseMap, h, 1);
        half4 a2 = HB_SAMPLE(_BaseMap, sampler_BaseMap, h, 2);
        half4 a3 = HB_SAMPLE(_BaseMap, sampler_BaseMap, h, 3);
        float3 W = HB_Weights(h, a1.rgb, a2.rgb, a3.rgb);
        albedo = a1 * W.x + a2 * W.y + a3 * W.z;

        mask = HB_SAMPLE(_MetallicGlossMap, sampler_MetallicGlossMap, h, 1) * W.x
             + HB_SAMPLE(_MetallicGlossMap, sampler_MetallicGlossMap, h, 2) * W.y
             + HB_SAMPLE(_MetallicGlossMap, sampler_MetallicGlossMap, h, 3) * W.z;

        half3 n1 = HB_RotateNormal(UnpackNormalScale(HB_SAMPLE(_BumpMap, sampler_BumpMap, h, 1), _BumpScale), h.r1);
        half3 n2 = HB_RotateNormal(UnpackNormalScale(HB_SAMPLE(_BumpMap, sampler_BumpMap, h, 2), _BumpScale), h.r2);
        half3 n3 = HB_RotateNormal(UnpackNormalScale(HB_SAMPLE(_BumpMap, sampler_BumpMap, h, 3), _BumpScale), h.r3);
        normalTS = normalize(n1 * W.x + n2 * W.y + n3 * W.z);

        emission = HB_SAMPLE(_EmissionMap, sampler_EmissionMap, h, 1).rgb * W.x
                 + HB_SAMPLE(_EmissionMap, sampler_EmissionMap, h, 2).rgb * W.y
                 + HB_SAMPLE(_EmissionMap, sampler_EmissionMap, h, 3).rgb * W.z;
    }
    else
    {
        albedo = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, uv);
        mask = SAMPLE_TEXTURE2D(_MetallicGlossMap, sampler_MetallicGlossMap, uv);
        normalTS = UnpackNormalScale(SAMPLE_TEXTURE2D(_BumpMap, sampler_BumpMap, uv), _BumpScale);
        emission = SAMPLE_TEXTURE2D(_EmissionMap, sampler_EmissionMap, uv).rgb;
    }

    // 넓은 범위의 밝기/거칠기 변화
    float macro = HB_Macro(uv);
    half macroAlbedo = lerp(1.0, 0.72 + 0.56 * macro, _MacroStrength);
    half macroSmooth = lerp(1.0, 0.8 + 0.4 * (1.0 - macro), _MacroStrength);

    outSurfaceData.alpha = Alpha(albedo.a, _BaseColor, _Cutoff);
    outSurfaceData.albedo = albedo.rgb * _BaseColor.rgb * macroAlbedo;
    outSurfaceData.albedo = AlphaModulate(outSurfaceData.albedo, outSurfaceData.alpha);
    outSurfaceData.metallic = mask.r * _Metallic;
    outSurfaceData.specular = half3(0.0, 0.0, 0.0);
    outSurfaceData.smoothness = saturate(mask.a * _Smoothness * macroSmooth);
    outSurfaceData.occlusion = LerpWhiteTo(mask.g, _OcclusionStrength);
    outSurfaceData.normalTS = normalTS;
    outSurfaceData.emission = emission * _EmissionColor.rgb;
    outSurfaceData.clearCoatMask = half(0.0);
    outSurfaceData.clearCoatSmoothness = half(0.0);
}

#endif // UNIVERSAL_LIT_INPUT_INCLUDED
