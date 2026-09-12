// なめらかなグラデーションの上に、固定グリッドのドット（ordered dithering）を乗せる UI 背景シェーダ。
//
// 2 層構成:
//   1. 下地 : fbm ノイズでドメインワープした多色グラデーション。色の塊がゆっくり流れる
//   2. ドット: Bayer 行列の閾値と「白の混ざり具合」を比べて、白 / アクセント色 / 下地 の 3 値に振り分ける
//
// ドットのグリッドは画面ピクセルに固定し、流すのは色（＝白の塊）だけ。
// こうするとドットが同じ場所でチカチカせず、白い領域の「境界」が流れて見える。
// ピクセル感を保ったまま動かすための肝なので、グリッドは UV ではなく画面座標で切っている。
//
// 使う側の条件:
//   - テクスチャは不要（Image の Source Image は空でよい）。入れた場合はその α をマスクとして使う
//   - Canvas Scaler で解像度が変わると 1 ドットの見た目サイズも変わる（Cell は実ピクセル指定）
//
// 注意: Screen Space - Overlay の Canvas は URP のポストプロセスより後に描画されるため Bloom は乗らない。
Shader "UI/DitherGradient"
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

        [Header(Gradient)]
        _ColorA ("Color A (左)", Color) = (0.55, 0.87, 0.93, 1)
        _ColorB ("Color B (右)", Color) = (0.62, 0.62, 1.0, 1)
        _ColorC ("Color C (上に重ねる色)", Color) = (0.10, 0.90, 0.75, 1)
        _NoiseScale ("Noise Scale (大きいほど色の塊が細かい)", Range(0.2, 8)) = 1.5
        _WarpAmount ("Warp Amount (グラデの歪み)", Range(0, 1.5)) = 0.4
        _FlowSpeed ("Flow Speed (0.03-0.08 くらいが自然)", Range(0, 0.5)) = 0.05

        [Header(Dither Dots)]
        [KeywordEnum(Bayer4, Bayer8)] _Matrix ("Bayer Matrix", Float) = 0
        _Cell ("Cell Size (ドット1個のピクセル数)", Range(1, 32)) = 6
        _DotSize ("Dot Size (1=隙間なし)", Range(0.2, 1)) = 1
        _WhiteColor ("Dot Color", Color) = (1,1,1,1)
        _WhiteCoverage ("White Coverage (白の量)", Range(0, 1)) = 0.5
        _EdgeSoftness ("Edge Softness (小さいほど輪郭が締まる)", Range(0.01, 0.6)) = 0.2
        _WhiteSpeed ("White Flow Speed (下地に対する倍率)", Range(0, 5)) = 1.5

        [Header(Accent Dots)]
        _AccentColor ("Accent Color", Color) = (0.65, 0.45, 1.0, 1)
        _AccentCoverage ("Accent Coverage (0=出さない)", Range(0, 1)) = 0

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
            #pragma shader_feature_local _MATRIX_BAYER4 _MATRIX_BAYER8

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
                float4 screenPos : TEXCOORD2;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            sampler2D _MainTex;
            float4 _MainTex_ST;
            fixed4 _Color;
            fixed4 _TextureSampleAdd;
            float4 _ClipRect;

            fixed4 _ColorA;
            fixed4 _ColorB;
            fixed4 _ColorC;
            float _NoiseScale;
            float _WarpAmount;
            float _FlowSpeed;

            float _Cell;
            float _DotSize;
            fixed4 _WhiteColor;
            float _WhiteCoverage;
            float _EdgeSoftness;
            float _WhiteSpeed;

            fixed4 _AccentColor;
            float _AccentCoverage;

            float _StepFps;

            v2f vert(appdata_t v)
            {
                v2f OUT;
                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(OUT);
                OUT.worldPosition = v.vertex;
                OUT.vertex = UnityObjectToClipPos(OUT.worldPosition);
                // VPOS はプラットフォーム差があるので、画面座標は自前で持ち回る。
                OUT.screenPos = ComputeScreenPos(OUT.vertex);
                OUT.texcoord = TRANSFORM_TEX(v.texcoord, _MainTex);
                OUT.color = v.color * _Color;
                return OUT;
            }

            float hash21(float2 p)
            {
                p = frac(p * float2(123.34, 456.21));
                p += dot(p, p + 45.32);
                return frac(p.x * p.y);
            }

            // なめらかなバリューノイズ。隣り合うドットで値が飛ばないので色の塊が崩れない。
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

            // 4 オクターブ fbm。振幅の合計が 0.9375 なので、その値で割って 0..1 に正規化して返す。
            float fbm(float2 p)
            {
                float v = 0.0;
                float a = 0.5;
                for (int k = 0; k < 4; k++)
                {
                    v += a * vnoise(p);
                    p *= 2.0;
                    a *= 0.5;
                }
                return v / 0.9375;
            }

            // Bayer 行列の閾値。配列の動的インデックスを避けて漸化式で出す。
            // bayer2 は [[0,2],[3,1]]/4 と等価で、そこから 4x4 / 8x8 を組み上げる。
            float bayer2(float2 a)
            {
                a = floor(a);
                return frac(a.x * 0.5 + a.y * a.y * 0.75);
            }
            float bayer4(float2 a) { return bayer2(a * 0.5) * 0.25 + bayer2(a); }
            float bayer8(float2 a) { return bayer4(a * 0.5) * 0.25 + bayer2(a); }

            // 返る値は 0, 1/N, ... と下端が 0 になるので、半ステップ足して中央に寄せる。
            // こうしないと coverage が 0 でもドットが 1 マス残る。
            float ditherThreshold(float2 grid)
            {
                #ifdef _MATRIX_BAYER8
                    return bayer8(grid) + 1.0 / 128.0;
                #else
                    return bayer4(grid) + 1.0 / 32.0;
                #endif
            }

            // ノイズを「どれくらい塗るか」に変換する。soft を小さくすると輪郭が締まってベタ塗りに近づく。
            // 閾値の中心は coverage=0 で 1+soft、coverage=1 で -soft。
            // こうしておくと 0 で完全に消え、1 で全面になる（1-coverage だと 0 でも半分残る）。
            float coverageMask(float n, float coverage, float soft)
            {
                float center = lerp(1.0 + soft, -soft, coverage);
                return smoothstep(center - soft, center + soft, n);
            }

            fixed4 frag(v2f IN) : SV_Target
            {
                // 実 FPS に関係なくコマ数を揃えたいときは _StepFps で時間を量子化する。
                float t = _StepFps > 0.0 ? floor(_Time.y * _StepFps) / _StepFps : _Time.y;
                t *= _FlowSpeed;

                // 画面ピクセル座標 → グリッドに量子化。ここが固定なのでドットの升目は動かない。
                float2 sp = IN.screenPos.xy / max(IN.screenPos.w, 1e-5) * _ScreenParams.xy;
                float cell = max(_Cell, 1.0);
                float2 grid = floor(sp / cell);
                float2 qp = grid * cell;

                // 下地の評価はマス単位（qp）で行う。ピクセル単位でやるとマスの中で色が割れる。
                float2 suv = qp / _ScreenParams.xy;  // 0..1 / グラデーションのランプ用
                float2 nuv = qp / _ScreenParams.y;   // アスペクト比を保った座標 / ノイズ用
                float2 np = nuv * _NoiseScale;

                // 下地: ドメインワープさせた 3 色グラデーション。
                // warp を -0.5 して中心を合わせないと、全体が一方向に押し出される。
                float2 warp = float2(fbm(np * 1.5 + t), fbm(np * 1.5 - t)) - 0.5;
                float2 g = suv + _WarpAmount * warp;
                float3 baseCol = lerp(_ColorA.rgb, _ColorB.rgb, smoothstep(0.0, 1.0, g.x));
                baseCol = lerp(baseCol, _ColorC.rgb, smoothstep(0.3, 1.0, g.y + g.x * 0.5));

                // 白の量。下地より少し速く流して、ドットの塊だけが動いて見えるようにする。
                float white = fbm(np * 1.2 - t * _WhiteSpeed + 3.0);
                white = coverageMask(white, _WhiteCoverage, _EdgeSoftness);

                float thr = ditherThreshold(grid);
                float dot1 = step(thr, white);

                // アクセント: 別位相のノイズ＋別位相の閾値で、白とは違う塊として出す。
                // 白が乗ったマスには出さないので、白 / アクセント / 下地 の 3 値になる。
                float accent = fbm(np * 1.7 + t * 1.1 + 11.0);
                accent = coverageMask(accent, _AccentCoverage, _EdgeSoftness);
                float dot2 = step(ditherThreshold(grid + float2(2.0, 1.0)), accent) * (1.0 - dot1);

                // マスの内側だけ塗って隙間を作る（_DotSize = 1 なら常に内側＝隙間なし）。
                float2 f = frac(sp / cell) - 0.5;
                float inside = step(max(abs(f.x), abs(f.y)), _DotSize * 0.5);
                dot1 *= inside;
                dot2 *= inside;

                float3 rgb = lerp(baseCol, _AccentColor.rgb, dot2);
                rgb = lerp(rgb, _WhiteColor.rgb, dot1);

                // Source Image を入れた場合はその α で形を抜けるようにしておく（未設定なら白＝α 1）。
                float texA = (tex2D(_MainTex, IN.texcoord) + _TextureSampleAdd).a;

                fixed4 color = fixed4(rgb * IN.color.rgb, IN.color.a * texA);

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
