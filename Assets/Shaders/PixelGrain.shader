// 화면 전체에 덮는 그레인.
//
// 알갱이 하나가 "게임 픽셀" 하나와 정확히 같아야 한다. uv 를 그대로 노이즈에 넣으면
// 알갱이가 화면 픽셀 크기가 되어, 3배로 띄운 화면에서 알갱이만 1x1 로 자글거린다.
// 주변 아트는 3x3 인데 그레인만 1x1 이면 화면에 모래를 뿌린 것처럼 겉돈다.
//
// 그래서 uv 를 먼저 640x360 격자의 "칸 번호"로 끊고, 그 번호로 노이즈를 읽는다.
// 노이즈를 Point + Repeat 로 넣어 두면 칸 하나당 텍셀 하나가 되어 1:1 로 맞는다.
//
// 캔버스가 Screen Space - Overlay 라 뒤에 그려진 색을 읽을 수 없다(그랩패스가 없다).
// 그래서 뒤 색을 "읽어서" 섞는 대신 블렌드로 곱한다.
//
//   Blend DstColor SrcColor  ->  결과 = 2 x Src x Dst
//
// Src 가 0.5 면 결과가 Dst 그대로다. 0.5 에서 벗어난 만큼 뒤 색이 밝아지거나 어두워진다.
// 알파로 회색을 덮는 방식은 어두운 데서 티가 난다 — 검정에 회색을 6% 덮으면 6% 밝아지는데,
// 그게 어두운 나무 카운터에서는 뿌옇게 보였다. 곱하기는 변동이 뒤 밝기에 비례해서
// 어두운 곳은 어둡게 남는다.
//
// 그래서 _Strength 는 알파가 아니라 "밝기가 몇 % 흔들리는가"다. 0.06 이면 ±6% 다.
Shader "Ramen/PixelGrain"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}

        _NoiseTex ("Noise", 2D) = "gray" {}
        _NoiseSize ("Noise Size", Float) = 64
        _Grid ("Game Pixel Grid", Vector) = (640, 360, 0, 0)
        _Offset ("Noise Offset", Vector) = (0, 0, 0, 0)
        _Strength ("Strength", Range(0, 1)) = 0.06

        _Color ("Tint", Color) = (1, 1, 1, 1)
        _StencilComp ("Stencil Comparison", Float) = 8
        _Stencil ("Stencil ID", Float) = 0
        _StencilOp ("Stencil Operation", Float) = 0
        _StencilWriteMask ("Stencil Write Mask", Float) = 255
        _StencilReadMask ("Stencil Read Mask", Float) = 255
        _ColorMask ("Color Mask", Float) = 15
    }

    SubShader
    {
        Tags
        {
            "Queue" = "Transparent"
            "IgnoreProjector" = "True"
            "RenderType" = "Transparent"
            "PreviewType" = "Plane"
            "CanUseSpriteAtlas" = "True"
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
        Blend DstColor SrcColor
        // 알파는 건드리지 않는다. 곱하기 블렌드가 알파에도 걸려 화면 알파가 0 으로 내려간다.
        ColorMask RGB

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            struct appdata_t
            {
                float4 vertex : POSITION;
                float2 texcoord : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct v2f
            {
                float4 vertex : SV_POSITION;
                float2 texcoord : TEXCOORD0;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            sampler2D _NoiseTex;
            float _NoiseSize;
            float4 _Grid;
            float4 _Offset;
            float _Strength;

            v2f vert(appdata_t v)
            {
                v2f o;
                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.texcoord = v.texcoord;
                return o;
            }

            fixed4 frag(v2f i) : SV_Target
            {
                // 게임 픽셀 칸 번호. 이 한 줄이 알갱이 크기를 결정한다.
                float2 cell = floor(i.texcoord * _Grid.xy);

                // 칸 번호를 그대로 텍셀 번호로 쓴다. 오프셋은 프레임마다 바뀌어 알갱이가 지글거린다.
                float2 noiseUV = (cell + _Offset.xy) / _NoiseSize;

                float n = tex2D(_NoiseTex, noiseUV).r;

                // 0.5 가 "그대로 두기"다. 결과가 Dst x (1 + 2 x (g - 0.5)) 가 되므로
                // 밝기 변동 폭이 곧 _Strength 다.
                float g = 0.5 + (n - 0.5) * _Strength;
                return fixed4(g, g, g, 1);
            }
            ENDCG
        }
    }

    Fallback Off
}
