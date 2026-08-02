Shader "Unlit/GridDouble"
{
    Properties
    {
        _GridColour1 ("Grid 1 Colour", color) = (1, 1, 1, 1)
        _BaseColour1 ("Base 1 Colour", color) = (1, 1, 1, 0)
        _GridSpacing1 ("Grid 1 Spacing", float) = 1
        _LineThickness1 ("Line 1 Thickness", float) = .1
        _ODistance1 ("Grid 1 Start Transparency Distance", float) = 5
        _TDistance1 ("Grid 1 Full Transparency Distance", float) = 10

        _GridColour2 ("Grid 2 Colour", color) = (1, 0, 0, 1)
        _BaseColour2 ("Base 2 Colour", color) = (1, 1, 1, 0)
        _GridSpacing2 ("Grid 2 Spacing", float) = 4
        _LineThickness2 ("Line 2 Thickness", float) = .15
        _ODistance2 ("Grid 2 Start Transparency Distance", float) = 5
        _TDistance2 ("Grid 2 Full Transparency Distance", float) = 10
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

            fixed4 _GridColour1;
            fixed4 _BaseColour1;
            float _GridSpacing1;
            float _LineThickness1;
            float _ODistance1;
            float _TDistance1;

            fixed4 _GridColour2;
            fixed4 _BaseColour2;
            float _GridSpacing2;
            float _LineThickness2;
            float _ODistance2;
            float _TDistance2;

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

            float GridUv(float2 uv)
            {
                float2 wrapped = frac(uv) - 0.5f;
                float2 range = abs(wrapped);
                float2 speeds = fwidth(uv);
                float2 pixelRange = range / speeds;
                return saturate(min(pixelRange.x, pixelRange.y));
            }

            float2 TriplanarUv(float3 worldPos, float3 worldNormal, float spacing)
            {
                float3 n = abs(normalize(worldNormal));

                float3 weights = pow(n, float3(8.0, 8.0, 8.0));
                weights /= (weights.x + weights.y + weights.z);

                return (weights.y * (worldPos.xz / spacing)
                     +  weights.x * (worldPos.zy / spacing)
                     +  weights.z * (worldPos.xy / spacing));
            }

            fixed4 frag (v2f i) : SV_Target
            {
                float2 uv1 = TriplanarUv(i.worldPos, i.worldNormal, _GridSpacing1);
                float2 uv2 = TriplanarUv(i.worldPos, i.worldNormal, _GridSpacing2);

                float lw1 = saturate(GridUv(uv1) - _LineThickness1);
                float lw2 = saturate(GridUv(uv2) - _LineThickness2);

                //distance falloff (0 = near, 1 = far)
                half3 viewDirW = _WorldSpaceCameraPos - i.worldPos;
                half viewDist = length(viewDirW);
                half falloff1 = saturate((viewDist - _ODistance1) / (_TDistance1 - _ODistance1) );
                half falloff2 = saturate((viewDist - _ODistance2) / (_TDistance2 - _ODistance2) );

                //line weights fade with distance (lw -> 1 hides the line)
                lw1 = lerp(lw1, 1.0f, falloff1);
                lw2 = lerp(lw2, 1.0f, falloff2);

                //colour: grid 1 line wins over grid 2 line, background never shows
                fixed4 param = lerp(_GridColour2, _GridColour1, 1.0f - lw1);

                //alpha: only on lines (lw = 0 on line, 1 between lines)
                param.a *= saturate(1.0f - min(lw1, lw2));
                return param;
            }
            ENDCG
        }
    }
}
