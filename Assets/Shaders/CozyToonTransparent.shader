// 森友会风卡通着色：半透明（玻璃瓶、塑料袋、透明盒）
Shader "CatMart/CozyToonTransparent"
{
    Properties
    {
        _Color ("Color", Color) = (1,1,1,0.3)
        _MainTex ("Albedo", 2D) = "white" {}
        _Detail ("Texture Strength", Range(0,1)) = 1
        _ShadowTint ("Shadow Tint", Color) = (0.80,0.78,0.90,1)
        _RampSoft ("Ramp Softness", Range(0.01,1)) = 0.35
        _RampOffset ("Ramp Offset", Range(-1,1)) = 0.0
        _Gloss ("Gloss", Range(0,1)) = 0.6
        _RimColor ("Rim Color", Color) = (1,1,1,1)
        _RimStrength ("Rim Strength", Range(0,1)) = 0.35
        _EmissionColor ("Emission", Color) = (0,0,0,1)
        _EmissionMap ("Emission Map", 2D) = "white" {}
    }
    SubShader
    {
        Tags { "RenderType"="Transparent" "Queue"="Transparent" "IgnoreProjector"="True" }
        LOD 200

        CGPROGRAM
        #pragma surface surf Cozy alpha:fade
        #pragma target 3.0
        #include "CozyToonCommon.cginc"
        ENDCG
    }
}
