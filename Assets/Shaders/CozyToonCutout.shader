// 森友会风卡通着色：镂空（网袋、羽毛）
Shader "CatMart/CozyToonCutout"
{
    Properties
    {
        _Color ("Color", Color) = (1,1,1,1)
        _MainTex ("Albedo", 2D) = "white" {}
        _Cutoff ("Alpha Cutoff", Range(0,1)) = 0.5
        _Detail ("Texture Strength", Range(0,1)) = 1
        _ShadowTint ("Shadow Tint", Color) = (0.74,0.70,0.86,1)
        _RampSoft ("Ramp Softness", Range(0.01,1)) = 0.28
        _RampOffset ("Ramp Offset", Range(-1,1)) = 0.05
        _Gloss ("Gloss", Range(0,1)) = 0.1
        _RimColor ("Rim Color", Color) = (1,0.96,0.88,1)
        _RimStrength ("Rim Strength", Range(0,1)) = 0.2
        _EmissionColor ("Emission", Color) = (0,0,0,1)
        _EmissionMap ("Emission Map", 2D) = "white" {}
    }
    SubShader
    {
        Tags { "RenderType"="TransparentCutout" "Queue"="AlphaTest" }
        LOD 200
        Cull Off

        CGPROGRAM
        #pragma surface surf Cozy alphatest:_Cutoff addshadow
        #pragma target 3.0
        #include "CozyToonCommon.cginc"
        ENDCG
    }
}
