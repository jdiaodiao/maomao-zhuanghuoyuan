// 森友会风卡通着色：柔和的明暗过渡（暗部偏淡紫），柔光高光，暖色边缘光。
Shader "CatMart/CozyToon"
{
    Properties
    {
        _Color ("Color", Color) = (1,1,1,1)
        _MainTex ("Albedo", 2D) = "white" {}
        _Detail ("Texture Strength", Range(0,1)) = 1
        _ShadowTint ("Shadow Tint", Color) = (0.74,0.70,0.86,1)
        _RampSoft ("Ramp Softness", Range(0.01,1)) = 0.28
        _RampOffset ("Ramp Offset", Range(-1,1)) = 0.05
        _Gloss ("Gloss", Range(0,1)) = 0.2
        _RimColor ("Rim Color", Color) = (1,0.96,0.88,1)
        _RimStrength ("Rim Strength", Range(0,1)) = 0.22
        _EmissionColor ("Emission", Color) = (0,0,0,1)
        _EmissionMap ("Emission Map", 2D) = "white" {}
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" "Queue"="Geometry" }
        LOD 200

        CGPROGRAM
        #pragma surface surf Cozy fullforwardshadows addshadow
        #pragma target 3.0
        #include "CozyToonCommon.cginc"
        ENDCG
    }
}
