#ifndef CATMART_COZY_TOON
#define CATMART_COZY_TOON

sampler2D _MainTex;
sampler2D _EmissionMap;
fixed4 _Color;
half _Detail;
fixed4 _ShadowTint;
half _RampSoft;
half _RampOffset;
half _Gloss;
fixed4 _RimColor;
half _RimStrength;
fixed4 _EmissionColor;

struct Input
{
    float2 uv_MainTex;
    float3 viewDir;
};

struct SurfaceOutputCozy
{
    fixed3 Albedo;
    fixed3 Normal;
    fixed3 Emission;
    fixed Alpha;
};

half4 LightingCozy (SurfaceOutputCozy s, half3 lightDir, half3 viewDir, half atten)
{
    half ndl = dot(s.Normal, lightDir);
    // 柔和的两段式明暗
    half ramp = smoothstep(_RampOffset - _RampSoft, _RampOffset + _RampSoft, ndl);
    half lit = ramp * atten;

    // 柔光高光：一小块圆润的亮斑
    half3 h = normalize(lightDir + viewDir);
    half nh = saturate(dot(s.Normal, h));
    half specPow = 24 + _Gloss * 180;
    half spec = smoothstep(0.45, 0.6, pow(nh, specPow)) * _Gloss * lit;

    half4 c;
#ifdef UNITY_PASS_FORWARDADD
    // 附加灯光：只加亮，不再叠加暗部色调
    c.rgb = (s.Albedo * lit + spec * 0.5) * _LightColor0.rgb;
#else
    half3 shade = lerp(_ShadowTint.rgb, half3(1,1,1), lit);
    c.rgb = (s.Albedo * shade + spec * 0.55) * _LightColor0.rgb;
#endif
    c.a = s.Alpha;
    return c;
}

void surf (Input IN, inout SurfaceOutputCozy o)
{
    fixed4 tex = tex2D(_MainTex, IN.uv_MainTex);
    fixed3 albedo = _Color.rgb * lerp(fixed3(1,1,1), tex.rgb, _Detail);
    o.Albedo = albedo;
    o.Alpha = _Color.a * tex.a;
    // 暖色边缘光
    half rim = 1 - saturate(dot(normalize(IN.viewDir), o.Normal));
    rim = smoothstep(0.55, 1.0, rim);
    o.Emission = _RimColor.rgb * rim * _RimStrength * (0.5 + 0.5 * albedo)
               + _EmissionColor.rgb * tex2D(_EmissionMap, IN.uv_MainTex).rgb;
}

#endif
