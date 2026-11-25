// 森友会风后期：柔光（bloom）+ 暖色调色 + 轻微暗角
Shader "Hidden/CatMart/CozyPost"
{
    Properties
    {
        _MainTex ("", 2D) = "white" {}
        _BloomTex ("", 2D) = "black" {}
    }

    CGINCLUDE
    #include "UnityCG.cginc"
    sampler2D _MainTex;
    float4 _MainTex_TexelSize;
    sampler2D _BloomTex;
    half _Threshold;
    half _Knee;
    half _Intensity;
    half _Saturation;
    half _Vignette;
    half4 _Tint;
    half4 _ShadowLift;

    half3 Box(float2 uv, float d)
    {
        float4 o = _MainTex_TexelSize.xyxy * float4(-d, -d, d, d);
        half3 s = tex2D(_MainTex, uv + o.xy).rgb + tex2D(_MainTex, uv + o.zy).rgb
                + tex2D(_MainTex, uv + o.xw).rgb + tex2D(_MainTex, uv + o.zw).rgb;
        return s * 0.25;
    }

    half3 Prefilter(half3 c)
    {
        half br = max(c.r, max(c.g, c.b));
        half rq = clamp(br - _Threshold + _Knee, 0, 2 * _Knee);
        rq = rq * rq / (4 * _Knee + 1e-4);
        half w = max(rq, br - _Threshold) / max(br, 1e-4);
        return c * w;
    }

    half4 FragPrefilter(v2f_img i) : SV_Target { return half4(Prefilter(Box(i.uv, 1)), 1); }
    half4 FragDown(v2f_img i) : SV_Target { return half4(Box(i.uv, 1), 1); }
    half4 FragUp(v2f_img i) : SV_Target { return half4(Box(i.uv, 0.5), 1); }

    half4 FragComposite(v2f_img i) : SV_Target
    {
        half4 c = tex2D(_MainTex, i.uv);
        half3 col = c.rgb + tex2D(_BloomTex, i.uv).rgb * _Intensity;
        half l = dot(col, half3(0.299, 0.587, 0.114));
        col = lerp(l.xxx, col, _Saturation);
        col = col * _Tint.rgb + _ShadowLift.rgb * saturate(1 - l * 2) * 0.08;
        float2 d = i.uv - 0.5;
        col *= 1 - dot(d, d) * _Vignette;
        return half4(col, c.a);
    }
    ENDCG

    SubShader
    {
        Cull Off ZWrite Off ZTest Always

        Pass
        {
            CGPROGRAM
            #pragma vertex vert_img
            #pragma fragment FragPrefilter
            ENDCG
        }
        Pass
        {
            CGPROGRAM
            #pragma vertex vert_img
            #pragma fragment FragDown
            ENDCG
        }
        Pass
        {
            Blend One One
            CGPROGRAM
            #pragma vertex vert_img
            #pragma fragment FragUp
            ENDCG
        }
        Pass
        {
            CGPROGRAM
            #pragma vertex vert_img
            #pragma fragment FragComposite
            ENDCG
        }
    }
}
