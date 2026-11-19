// 화면 반전 (흉신 마라 2스 참 연출). 화면 맨 위 UI 판(Screen Space - Overlay)에 쓰면, 그 아래에 이미 그려진 화면 색을 뒤집는다.
// 블렌드: 결과 = 판 색 × (1 - 화면 색) + 화면 색 × (1 - 판 색)
//  → 판 색 s(회색)일 때 결과 = 화면 + s × (1 - 2 × 화면) = Lerp(화면, 1 - 화면, s)
//  → s = 1이면 완전 반전, 0이면 그대로. Image 색(회색 밝기)으로 반전 세기를 조절한다.
// 텍스처 계산 없이 블렌드만 쓰므로, 후처리(Bloom)가 끝난 최종 화면에 적용된다.
Shader "Game/UI/ScreenInvert"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}
    }

    SubShader
    {
        Tags { "Queue" = "Overlay" "RenderType" = "Transparent" "IgnoreProjector" = "True" "PreviewType" = "Plane" }

        Cull Off
        ZWrite Off
        ZTest Always
        Blend OneMinusDstColor OneMinusSrcColor

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            struct appdata
            {
                float4 vertex : POSITION;
                float4 color : COLOR;
            };

            struct v2f
            {
                float4 position : SV_POSITION;
                float4 color : COLOR;
            };

            v2f vert(appdata input)
            {
                v2f output;
                output.position = UnityObjectToClipPos(input.vertex);
                output.color = input.color;
                return output;
            }

            // Image 색 × 투명도 = 반전 세기 s (회색). 알파는 블렌드에 쓰지 않는다.
            fixed4 frag(v2f input) : SV_Target
            {
                fixed strength = saturate(input.color.r * input.color.a);
                return fixed4(strength, strength, strength, 1);
            }
            ENDCG
        }
    }
}
