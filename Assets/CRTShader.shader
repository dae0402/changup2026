Shader "Custom/CRTShader"
{
    Properties
    {
        _MainTex ("Texture", 2D) = "white" {}
        _Distortion ("Distortion (굴곡)", Range(-1, 1)) = -0.05
        _ScanlineCount ("Scanline Count (줄무늬 개수)", Range(0, 1080)) = 800
        _ScanlineIntensity ("Scanline Intensity (진하기)", Range(0, 1)) = 0.05
    }
    SubShader
    {
        Cull Off ZWrite Off ZTest Always

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            struct appdata
            {
                float4 vertex : POSITION;
                float2 uv : TEXCOORD0;
            };

            struct v2f
            {
                float2 uv : TEXCOORD0;
                float4 vertex : SV_POSITION;
            };

            v2f vert (appdata v)
            {
                v2f o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.uv = v.uv;
                return o;
            }

            sampler2D _MainTex;
            float _Distortion;
            float _ScanlineCount;
            float _ScanlineIntensity;

            float2 RadialDistortion(float2 coord)
            {
                float2 cc = coord - 0.5;
                float dist = dot(cc, cc);
                return coord + cc * (dist + dist * dist) * _Distortion;
            }

            fixed4 frag (v2f i) : SV_Target
            {
                float2 uv = RadialDistortion(i.uv);

                // 화면 밖 검은색 처리
                if (uv.x < 0 || uv.x > 1 || uv.y < 0 || uv.y > 1)
                    return float4(0,0,0,1);

                fixed4 col = tex2D(_MainTex, uv);

                // 스캔라인 (더 부드러운 사인파 사용)
                // 화면 해상도와 상관없이 고정된 개수(_ScanlineCount)만큼 그립니다.
                float scanline = sin(uv.y * _ScanlineCount * 3.14159);
                col.rgb -= scanline * _ScanlineIntensity;

                // 비네팅 (가장자리 어둡게) - 고정값으로 은은하게 적용
                float vignette = uv.x * (1 - uv.x) * uv.y * (1 - uv.y) * 50.0;
                col.rgb *= clamp(vignette, 0.0, 1.0);

                return col;
            }
            ENDCG
        }
    }
}