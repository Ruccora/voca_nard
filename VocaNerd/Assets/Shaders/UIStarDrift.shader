// 星空背景を「星だけ」左斜め下へ流す UI シェーダ。
//
// 画像を Repeat でタイル状に繰り返し、UV を一方向にずらして無限スクロールさせる。
// ただし背景が不透明なベタ塗り（濃紺 #013555）なので、そのままだとタイルの継ぎ目が出る。
// そこで「ベタ塗りとの色差」をマスクにして地の色を毎フレーム塗り直し、
// 星（＝地色と違う色のところ）だけを動かす。地色が一様に塗られるので継ぎ目は見えない。
//
// 動きは 3 系統:
//   Drift   : タイルを一方向（既定は画像の流れ星と同じ左斜め下）へスクロール
//   Twinkle : なめらかなノイズで星ごとに位相と速さを散らして明滅させる
//   Flow    : 流れ星の進行方向に沿って明るさのパルスを流す。点線の尾が流れて見える
//
// 使う側の条件:
//   - テクスチャの Wrap Mode = Repeat / Mesh Type = Full Rect（タイル化の前提）
//   - Filter Mode = Point / Compression = None（ドット感の維持と、圧縮ノイズでマスクを汚さないため）
//   - Image は Type = Simple（Tiled にする必要はない。繰り返しは UV 側でやる）
//
// 注意: Screen Space - Overlay の Canvas は URP のポストプロセスより後に描画されるため、
//       Star Brightness を 1 より上げても Bloom は乗らない（白飛びするだけ）。
Shader "UI/StarDrift"
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

        [Header(Star Mask)]
        _Threshold ("Star Detect Threshold (地色との色差)", Range(0.01, 0.5)) = 0.08
        _MaskSoftness ("Mask Softness", Range(0.001, 0.3)) = 0.05
        _StarBoost ("Star Brightness", Range(0, 4)) = 1
        [Toggle(_MANUAL_BG)] _UseManualBg ("Use Manual Bg Color", Float) = 0
        _BgColor ("Bg Color (手動指定時のみ)", Color) = (0.004, 0.208, 0.333, 1)

        [Header(Tile Drift)]
        _Tiling ("Tiling (1=画面いっぱいに1枚)", Range(0.25, 4)) = 1
        _DriftDir ("Star Move Direction (x, y) (画面上の向き。左斜め下=-0.82 -0.57)", Vector) = (-0.82, -0.57, 0, 0)
        _DriftSpeed ("Star Move Speed (UV per sec / 0=止める)", Range(0, 0.2)) = 0.02

        [Header(Twinkle)]
        _TwinkleSpeed ("Speed", Range(0, 10)) = 2.5
        _TwinkleAmount ("Amount", Range(0, 1)) = 0.5
        _TwinkleScale ("Noise Scale (px)", Range(4, 100)) = 24

        [Header(Shooting Star Flow)]
        _FlowDir ("Direction (x, y)", Vector) = (-0.82, -0.57, 0, 0)
        _FlowSpeed ("Speed", Range(0, 30)) = 8
        _FlowWavelength ("Wavelength (px)", Range(4, 600)) = 90
        _FlowAmount ("Amount", Range(0, 1)) = 0.5

        [Header(Common)]
        _StepFps ("Step FPS (0=なめらか 小さいほどカクつく)", Range(0, 60)) = 0
    }

    SubShader
    {
        Tags
        {
            "Queue"="Transparent"
            "IgnoreProjector"="True"
            "RenderType"="Transparent"
            "PreviewType"="Plane"
            "CanUseSpriteAtlas"="False"
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
            #pragma shader_feature_local _MANUAL_BG

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
                float2 texcoord : TEXCOORD0;
                float4 worldPosition : TEXCOORD1;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            sampler2D _MainTex;
            float4 _MainTex_ST;
            float4 _MainTex_TexelSize;
            fixed4 _Color;
            fixed4 _TextureSampleAdd;
            float4 _ClipRect;

            float _Threshold;
            float _MaskSoftness;
            float _StarBoost;
            fixed4 _BgColor;

            float _Tiling;
            float4 _DriftDir;
            float _DriftSpeed;

            float _TwinkleSpeed;
            float _TwinkleAmount;
            float _TwinkleScale;

            float4 _FlowDir;
            float _FlowSpeed;
            float _FlowWavelength;
            float _FlowAmount;

            float _StepFps;

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

            float luma(float3 c)
            {
                return dot(c, float3(0.299, 0.587, 0.114));
            }

            float hash21(float2 p)
            {
                p = frac(p * float2(123.34, 456.21));
                p += dot(p, p + 45.32);
                return frac(p.x * p.y);
            }

            // なめらかなバリューノイズ。隣接ピクセルで値が飛ばないので、
            // 1 つの星が途中で割れて明滅しない。
            float vnoise(float2 p)
            {
                float2 i = floor(p);
                float2 f = frac(p);
                f = f * f * (3.0 - 2.0 * f);
                float a = hash21(i);
                float b = hash21(i + float2(1, 0));
                float c = hash21(i + float2(0, 1));
                float d = hash21(i + float2(1, 1));
                return lerp(lerp(a, b, f.x), lerp(c, d, f.x), f.y);
            }

            // 地色（ベタ塗り部分）を画像から推定する。星は必ず地より明るいので、
            // 散らした数点のうち一番暗いものを地色とみなす。
            // 手動指定に切り替えられるようにしてあるが、基本は自動で当たる。
            float3 SampleBgColor()
            {
                #ifdef _MANUAL_BG
                    return _BgColor.rgb;
                #else
                    float3 bg = tex2D(_MainTex, float2(0.06, 0.30)).rgb;
                    float bl = luma(bg);

                    float3 c1 = tex2D(_MainTex, float2(0.94, 0.78)).rgb;
                    float l1 = luma(c1);
                    bg = l1 < bl ? c1 : bg; bl = min(bl, l1);

                    float3 c2 = tex2D(_MainTex, float2(0.52, 0.24)).rgb;
                    float l2 = luma(c2);
                    bg = l2 < bl ? c2 : bg; bl = min(bl, l2);

                    float3 c3 = tex2D(_MainTex, float2(0.22, 0.88)).rgb;
                    float l3 = luma(c3);
                    bg = l3 < bl ? c3 : bg; bl = min(bl, l3);

                    float3 c4 = tex2D(_MainTex, float2(0.78, 0.44)).rgb;
                    float l4 = luma(c4);
                    bg = l4 < bl ? c4 : bg;

                    return bg;
                #endif
            }

            fixed4 frag(v2f IN) : SV_Target
            {
                // 実 FPS に関係なくコマ数を揃えたいときは _StepFps で時間を量子化する。
                float t = _StepFps > 0.0 ? floor(_Time.y * _StepFps) / _StepFps : _Time.y;

                // タイルを一方向にスクロールさせる。UV を +方向にずらすと絵は逆に動くので、
                // 指定した「星が動く向き」の符号を反転して足す。Repeat 前提なので端は反対側から出てくる。
                float2 driftDir = normalize(_DriftDir.xy + 1e-5);
                float2 uv = IN.texcoord * _Tiling - driftDir * (_DriftSpeed * t);

                float3 c = (tex2D(_MainTex, uv) + _TextureSampleAdd).rgb;
                float3 bg = SampleBgColor();

                // 地色との色差＝星の寄与ぶん。圧縮ノイズを拾わないよう threshold で足切りする。
                float3 diff = c - bg;
                float mask = smoothstep(_Threshold, _Threshold + _MaskSoftness, length(diff));

                // アスペクト比に依存しない距離計算のためピクセル座標に直す。
                float2 px = uv * _MainTex_TexelSize.zw;

                // Twinkle: 位置ノイズで位相と速さをばらつかせる → 全体が同期しない明滅
                float2 np = px / max(_TwinkleScale, 0.0001);
                float n = vnoise(np);
                float speed = _TwinkleSpeed * (0.6 + 0.8 * vnoise(np + 17.3));
                float tw = 0.5 + 0.5 * sin(n * UNITY_TWO_PI + t * speed);
                float twinkleMul = 1.0 + _TwinkleAmount * (tw - 0.5) * 1.4;

                // Flow: 進行方向に射影して明るさのパルスを流す → 尾が流れて見える
                float2 flowDir = normalize(_FlowDir.xy + 1e-5);
                float proj = dot(px, flowDir) * (UNITY_TWO_PI / max(_FlowWavelength, 0.0001));
                float wave = 0.5 + 0.5 * sin(proj - t * _FlowSpeed);
                wave = wave * wave; // パルスを尖らせる
                float flowMul = lerp(1.0, 0.5 + 1.5 * wave, _FlowAmount);

                // 明滅は「地色からの差分」に掛ける。サンプル色そのものに掛けると、
                // 淡いハロー（色差が小さく地色に近い領域）が地色より暗くなって
                // 星のまわりに黒い輪ができてしまう。
                // mask=0 の場所は元画像そのまま（bg + diff == c）になるので、
                // threshold は「どこまで動かすか」だけを決め、絵を壊さない。
                float anim = twinkleMul * flowMul * _StarBoost;
                float3 rgb = bg + diff * (1.0 + (anim - 1.0) * mask);

                fixed4 color = fixed4(rgb * IN.color.rgb, IN.color.a);

                #ifdef UNITY_UI_CLIP_RECT
                color.a *= UnityGet2DClipping(IN.worldPosition.xy, _ClipRect);
                #endif

                return color;
            }
            ENDCG
        }
    }

    Fallback "UI/Default"
}
