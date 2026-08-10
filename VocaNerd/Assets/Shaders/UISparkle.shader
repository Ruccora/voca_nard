Shader "UI/Sparkle"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}
        _Color ("Tint", Color) = (1,1,1,1)

        _StencilComp ("Stencil Comparison", Float) = 8
        _Stencil ("Stencil ID", Float) = 0
        _StencilOp ("Stencil Operation", Float) = 0
        _StencilWriteMask ("Stencil Write Mask", Float) = 255
        _StencilReadMask ("Stencil Read Mask", Float) = 255
        _ColorMask ("Color Mask", Float) = 15
        [Toggle(UNITY_UI_ALPHACLIP)] _UseUIAlphaClip ("Use Alpha Clip", Float) = 0

        [Header(Bloom Glow)]
        _MaskTex ("Bloom Mask (R channel)", 2D) = "white" {}
        _GlowColor ("Glow Color (fallback)", Color) = (1, 1, 1, 1)
        _ColorFromImage ("Color From Image", Range(0, 1)) = 1.0
        _ColorBoost ("Image Color Boost", Range(0.5, 4)) = 1.8
        _GlowIntensity ("Glow Intensity", Range(0, 5)) = 1.5
        _PulseAmount ("Pulse Amount", Range(0, 1)) = 0.3
        _PulseSpeed ("Pulse Speed", Range(0, 10)) = 2.0
        _PhaseGridSize ("Phase Grid Size (1=sync, high=各領域独立)", Range(1, 60)) = 8
        _BlurSpread ("Bloom Spread", Range(0, 0.03)) = 0.006
        _MaskCutoff ("Mask Cutoff", Range(0, 1)) = 0.01
        _MaskContrast ("Mask Contrast", Range(0.5, 8)) = 1.5
    }

    SubShader
    {
        Tags
        {
            "Queue"="Transparent"
            "IgnoreProjector"="True"
            "RenderType"="Transparent"
            "PreviewType"="Plane"
            "CanUseSpriteAtlas"="True"
        }

        Stencil
        {
            Ref [_Stencil]
            Comp [_StencilComp]
            Pass [_StencilOp]
            ReadMask [_StencilReadMask]
            WriteMask [_StencilWriteMask]
        }

        Cull Off
        Lighting Off
        ZWrite Off
        ZTest [unity_GUIZTestMode]
        Blend SrcAlpha OneMinusSrcAlpha
        ColorMask [_ColorMask]

        Pass
        {
            Name "Default"
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 2.0

            #include "UnityCG.cginc"
            #include "UnityUI.cginc"

            #pragma multi_compile_local _ UNITY_UI_CLIP_RECT
            #pragma multi_compile_local _ UNITY_UI_ALPHACLIP

            struct appdata_t
            {
                float4 vertex   : POSITION;
                float4 color    : COLOR;
                float2 texcoord : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct v2f
            {
                float4 vertex   : SV_POSITION;
                fixed4 color    : COLOR;
                float2 texcoord  : TEXCOORD0;
                float4 worldPosition : TEXCOORD1;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            sampler2D _MainTex;
            sampler2D _MaskTex;
            fixed4 _Color;
            fixed4 _TextureSampleAdd;
            float4 _ClipRect;
            float4 _MainTex_ST;
            float4 _MaskTex_ST;

            fixed4 _GlowColor;
            float _ColorFromImage;
            float _ColorBoost;
            float _GlowIntensity;
            float _PulseAmount;
            float _PulseSpeed;
            float _PhaseGridSize;
            float _BlurSpread;
            float _MaskCutoff;
            float _MaskContrast;

            v2f vert(appdata_t v)
            {
                v2f OUT;
                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(OUT);
                OUT.worldPosition = v.vertex;
                OUT.vertex = UnityObjectToClipPos(OUT.worldPosition);
                OUT.texcoord = TRANSFORM_TEX(v.texcoord, _MainTex);
                OUT.color = v.color * _Color;
                return OUT;
            }

            float hash(float2 p)
            {
                return frac(sin(dot(p, float2(12.9898, 78.233))) * 43758.5453);
            }

            // 4 隅の位相をバイリニア補間して滑らかに変化させる
            float samplePhase(float2 uv)
            {
                float2 gpos = uv * _PhaseGridSize;
                float2 gcell = floor(gpos);
                float2 gfrac = smoothstep(0.0, 1.0, frac(gpos));
                float p00 = hash(gcell);
                float p10 = hash(gcell + float2(1, 0));
                float p01 = hash(gcell + float2(0, 1));
                float p11 = hash(gcell + float2(1, 1));
                float px0 = lerp(p00, p10, gfrac.x);
                float px1 = lerp(p01, p11, gfrac.x);
                return lerp(px0, px1, gfrac.y) * 6.2831853;
            }

            fixed4 frag(v2f IN) : SV_Target
            {
                half4 mainSample = tex2D(_MainTex, IN.texcoord);
                half4 color = (mainSample + _TextureSampleAdd) * IN.color;

                // 5-tap Blurred mask sample → bloom っぽく縁がぼける
                float bs = _BlurSpread;
                float mask = tex2D(_MaskTex, IN.texcoord).r * 0.4
                           + tex2D(_MaskTex, IN.texcoord + float2(bs, 0)).r * 0.15
                           + tex2D(_MaskTex, IN.texcoord + float2(-bs, 0)).r * 0.15
                           + tex2D(_MaskTex, IN.texcoord + float2(0, bs)).r * 0.15
                           + tex2D(_MaskTex, IN.texcoord + float2(0, -bs)).r * 0.15;
                mask = pow(saturate(mask), _MaskContrast);

                if (mask > _MaskCutoff)
                {
                    // 位置ごとにランダムな位相 → 全体が同期せず不規則に明滅
                    float phase = samplePhase(IN.texcoord);
                    float pulseSine = 0.5 + 0.5 * sin(_Time.y * _PulseSpeed + phase);
                    float pulse = lerp(1.0 - _PulseAmount, 1.0, pulseSine);

                    // 発光色: 画像色 or フォールバックカラーをブレンド
                    float3 imageColor = saturate(mainSample.rgb * _ColorBoost);
                    float3 glowColor = lerp(_GlowColor.rgb, imageColor, _ColorFromImage);

                    // 加算合成で発光
                    color.rgb += mask * pulse * _GlowIntensity * glowColor * color.a;
                }

                #ifdef UNITY_UI_CLIP_RECT
                color.a *= UnityGet2DClipping(IN.worldPosition.xy, _ClipRect);
                #endif

                #ifdef UNITY_UI_ALPHACLIP
                clip(color.a - 0.001);
                #endif

                return color;
            }
            ENDCG
        }
    }
}
