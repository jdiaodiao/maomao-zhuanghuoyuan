// 商品属性光边（屏幕空间描边）。
// Pass 0：把每件商品画进遮罩图，rgb = 光边颜色，a = 视线深度；被场景挡住的部分丢掉。
// Pass 1：在商品轮廓外侧（或被前面商品挡住的地方）画一圈细光边，颜色取前面那件商品的。
Shader "Hidden/CatMart/ItemOutline"
{
    Properties
    {
        _MainTex ("Scene", 2D) = "white" {}
    }
    SubShader
    {
        // ---------------------------------------------------------------- 0: 遮罩
        Pass
        {
            Cull Off ZWrite On ZTest LEqual
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            UNITY_DECLARE_DEPTH_TEXTURE(_CameraDepthTexture);
            float4 _OutlineColor;

            struct v2f { float4 pos : SV_POSITION; float4 sp : TEXCOORD0; float depth : TEXCOORD1; };

            v2f vert (float4 v : POSITION)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v);
                o.sp = ComputeScreenPos(o.pos);
                o.depth = -UnityObjectToViewPos(v.xyz).z;
                return o;
            }

            float4 frag (v2f i) : SV_Target
            {
                float scene = LinearEyeDepth(SAMPLE_DEPTH_TEXTURE_PROJ(_CameraDepthTexture, UNITY_PROJ_COORD(i.sp)));
                // 被别的东西挡住了（袋壁、台面…）→ 不算轮廓
                if (i.depth > scene + 0.006 + 0.004 * i.depth) discard;
                return float4(_OutlineColor.rgb, i.depth);
            }
            ENDCG
        }

        // ---------------------------------------------------------------- 1: 合成
        Pass
        {
            Cull Off ZWrite Off ZTest Always
            CGPROGRAM
            #pragma vertex vert_img
            #pragma fragment frag
            #include "UnityCG.cginc"

            sampler2D _MainTex;
            float4 _MainTex_TexelSize;
            sampler2D _MaskTex;
            float4 _MaskTex_TexelSize;
            float _Width;      // 像素
            float _Strength;
            float _Debug;
            float _Boost;      // >1 时被柔光放大，看起来像发光

            // 邻居 m 是否在 d0 前面（构成一条轮廓）
            float Front(float4 m, float d0)
            {
                return (m.a > 0 && m.a < d0 - (0.04 + 0.03 * m.a)) ? 1 : 0;
            }

            float4 frag (v2f_img i) : SV_Target
            {
                float4 c = tex2D(_MainTex, i.uv);
                float2 muv = i.uv;
                #if UNITY_UV_STARTS_AT_TOP
                if (_MainTex_TexelSize.y < 0) muv.y = 1 - muv.y;
                #endif
                float4 m0 = tex2D(_MaskTex, muv);
                float d0 = m0.a > 0 ? m0.a : 1e5;

                float best = 0;
                float3 col = 0;
                // 三圈采样：内圈实线，外两圈是淡淡的光晕
                const float radii[4] = { 1.0, 1.9, 2.9, 4.2 };
                const float weights[4] = { 1.0, 0.85, 0.38, 0.14 };
                [unroll] for (int r = 0; r < 4; r++)
                {
                    [unroll] for (int k = 0; k < 12; k++)
                    {
                        float a = k * 0.5235988 + r * 0.26;
                        float2 off = float2(cos(a), sin(a)) * radii[r] * _Width * _MaskTex_TexelSize.xy;
                        float4 m = tex2Dlod(_MaskTex, float4(muv + off, 0, 0));
                        float w = Front(m, d0) * weights[r];
                        if (w > best) { best = w; col = m.rgb; }
                    }
                }
                best *= _Strength;
                // 内圈直接替换颜色（清晰的细线），外圈叠加一点发光
                c.rgb = lerp(c.rgb, col * _Boost, saturate(best) * 0.9) + col * best * 0.3;
                if (_Debug > 0.5) c = float4(m0.rgb * (m0.a > 0 ? 1 : 0.1), 1) + float4(best, 0, 0, 0) * 0.5 + float4(0, 0, saturate(m0.a / 3), 0);
                return c;
            }
            ENDCG
        }
    }
    Fallback Off
}
