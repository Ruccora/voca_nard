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

        [Header(Bloom Glow R channel)]
        _MaskTex ("Glow Mask (R=bloom, B=streak)", 2D) = "white" {}
        _GlowColor ("Glow Color (fallback)", Color) = (1, 1, 1, 1)
        _ColorFromImage ("Color From Image", Range(0, 1)) = 1.0
        _ColorBoost ("Image Color Boost", Range(0.5, 4)) = 1.8
        _GlowIntensity ("Glow Intensity", Range(0, 5)) = 1.5
        _PulseAmount ("Pulse Amount", Range(0, 1)) = 0.3
        _PulseSpeed ("Pulse Speed", Range(0, 10)) = 2.0
        _PhaseGridSize ("Phase Grid Size (1=sync, high=各領域独立)", Range(1, 60)) = 8
        _MaskCutoff ("Mask Cutoff", Range(0, 1)) = 0.01
        _MaskContrast ("Mask Contrast", Range(0.5, 8)) = 1.5

        [Header(Directional Streak B channel)]
        _StreakColor ("Streak Color (fallback)", Color) = (1, 0.95, 0.8, 1)
        _StreakColorFromImage ("Streak Color From Image", Range(0, 1)) = 0.0
        [IntRange] _StreakSamples ("Streak Samples (片側)", Range(1, 24)) = 4
        _StreakAngle ("Streak Angle (degrees)", Range(0, 360)) = 45
        _StreakLength ("Streak Length (UV)", Range(0, 0.5)) = 0.15
        _StreakIntensity ("Streak Intensity", Range(0, 8)) = 2.5
        _StreakContrast ("Streak Contrast", Range(0.5, 8)) = 1.5
        _ShimmerSpeed ("Shimmer Speed", Range(0, 10)) = 2.5
        _ShimmerSharpness ("Shimmer Sharpness", Range(0.5, 16)) = 4.0
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
            #pragma target 3.0

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
            float _MaskCutoff;
            float _MaskContrast;

            fixed4 _StreakColor;
            float _StreakColorFromImage;
            int _StreakSamples;
            float _StreakAngle;
            float _StreakLength;
            float _StreakIntensity;
            float _StreakContrast;
            float _ShimmerSpeed;
            float _ShimmerSharpness;

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

            // グリッドセルごとに 1 hash → セル単位で位相が変わり不規則に明滅
            // (ドット絵向けに 4 隅補間を廃止し sin 呼び出しを 1/4 に削減)
            float samplePhase(float2 uv)
            {
                float2 gcell = floor(uv * _PhaseGridSize);
                return hash(gcell) * 6.2831853;
            }

            fixed4 frag(v2f IN) : SV_Target
            {
                half4 mainSample = tex2D(_MainTex, IN.texcoord);
                half4 color = (mainSample + _TextureSampleAdd) * IN.color;

                // マスク R を 1 tap で取得 (ドット絵はブラー不要 → クッキリ維持 & 帯域節約)
                float mask = pow(saturate(tex2D(_MaskTex, IN.texcoord).r), _MaskContrast);

                float3 imageColor = saturate(mainSample.rgb * _ColorBoost);

                // --- R チャンネル: 等方ブルーム (濃さ = 強度に比例) ---
                if (mask > _MaskCutoff)
                {
                    // 位置ごとにランダムな位相 → 全体が同期せず不規則に明滅
                    float phase = samplePhase(IN.texcoord);
                    float pulseSine = 0.5 + 0.5 * sin(_Time.y * _PulseSpeed + phase);
                    float pulse = lerp(1.0 - _PulseAmount, 1.0, pulseSine);

                    // 発光色: 画像色 or フォールバックカラーをブレンド
                    float3 glowColor = lerp(_GlowColor.rgb, imageColor, _ColorFromImage);

                    // 加算合成で発光 (mask=Rの強さがそのまま発光量に比例)
                    color.rgb += mask * pulse * _GlowIntensity * glowColor * color.a;
                }

                // --- B チャンネル: 指向性ストリーク (光条) ---
                // 青マスクを指定角度に沿って異方ブラー → 一方向へ伸びる光の筋
                float ang = _StreakAngle * 0.01745329; // deg → rad
                float2 dir = float2(cos(ang), sin(ang));
                float streak = 0.0;
                float wsum = 0.0;
                int samples = max(_StreakSamples, 1);
                float invSamples = 1.0 / samples;
                // ループ回数は全ピクセル共通 (uniform) なので divergence なし
                for (int i = -samples; i <= samples; i++)
                {
                    float t = i * invSamples;            // -1..1
                    float w = 1.0 - abs(t);              // 三角窓 (中心が濃く両端で減衰)
                    float2 off = dir * (t * _StreakLength);
                    streak += tex2D(_MaskTex, IN.texcoord + off).b * w;
                    wsum += w;
                }
                streak = pow(saturate(streak / wsum), _StreakContrast);

                if (streak > _MaskCutoff)
                {
                    // 筋に沿って流れるハイライト (指向性を強調するシマー)
                    float along = dot(IN.texcoord, dir);
                    float sweep = frac(along * (1.0 / max(_StreakLength, 1e-4)) - _Time.y * _ShimmerSpeed);
                    float shimmer = pow(sin(sweep * 3.14159265), _ShimmerSharpness);
                    float streakLevel = streak * (0.6 + 0.6 * shimmer);

                    float3 streakColor = lerp(_StreakColor.rgb, imageColor, _StreakColorFromImage);
                    color.rgb += streakLevel * _StreakIntensity * streakColor * color.a;
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
