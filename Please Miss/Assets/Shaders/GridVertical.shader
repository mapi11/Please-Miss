Shader "Unlit/GridVertical"
{
    Properties
    {
        _GridColour ("Grid Colour", color) = (1, 1, 1, 1)
        _BaseColour ("Base Colour", color) = (1, 1, 1, 0)
        _GridSpacing ("Grid Spacing", float) = 1
        _LineThickness ("Line Thickness", float) = .1
        _ODistance ("Start Transparency Distance", float) = 5
        _TDistance ("Full Transparency Distance", float) = 10
    }
    SubShader
    {
        Tags { "RenderType"="Transparent" "Queue"="Transparent"}
        LOD 100
        Blend SrcAlpha OneMinusSrcAlpha
        ZWrite Off
        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"
            struct v2f
            {
                float4 vertex : SV_POSITION;
                float3 worldPos : TEXCOORD0;
                float3 worldNormal : TEXCOORD1;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            fixed4 _GridColour;
            fixed4 _BaseColour;
            float _GridSpacing;
            float _LineThickness;
            float _ODistance;
            float _TDistance;

            v2f vert (appdata_full v)
            {
                v2f o;
                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.worldPos = mul(unity_ObjectToWorld, v.vertex);
                o.worldNormal = UnityObjectToWorldNormal(v.normal);
                return o;
            }

            float2 GridUv(float2 uv, float spacing)
            {
                float2 wrapped = frac(uv) - 0.5f;
                float2 range = abs(wrapped);
                float2 speeds = fwidth(uv);
                float2 pixelRange = range / speeds;
                return saturate(min(pixelRange.x, pixelRange.y) - _LineThickness);
            }

            fixed4 frag (v2f i) : SV_Target
            {
                float3 n = abs(normalize(i.worldNormal));

                float3 weights = pow(n, float3(8.0, 8.0, 8.0));
                weights /= (weights.x + weights.y + weights.z);

                float2 uvXZ = i.worldPos.xz / _GridSpacing;
                float2 uvZY = i.worldPos.zy / _GridSpacing;
                float2 uvXY = i.worldPos.xy / _GridSpacing;

                float lineWeight = weights.y * GridUv(uvXZ, _GridSpacing)
                                 + weights.x * GridUv(uvZY, _GridSpacing)
                                 + weights.z * GridUv(uvXY, _GridSpacing);
                lineWeight = saturate(lineWeight);

                half4 param = lerp(_GridColour, _BaseColour, lineWeight);

                half3 viewDirW = _WorldSpaceCameraPos - i.worldPos;
                half viewDist = length(viewDirW);
                half falloff = saturate((viewDist - _ODistance) / (_TDistance - _ODistance) );
                param.a *= (1.0f - falloff);
                return param;
            }
            ENDCG
        }
    }
}
