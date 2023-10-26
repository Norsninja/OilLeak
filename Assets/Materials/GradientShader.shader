Shader "Unlit/GradientShader"
{
    Properties
    {
        _TopColor ("Top Color", Color) = (0, 0, 1, 0.9) // Default to blue at 50% transparency
        _BottomColor ("Bottom Color", Color) = (0, 1, 1, 1) // Default to light blue at 50% transparency
    }
    SubShader
    {
        Tags { "Queue" = "Transparent" } // This makes sure your shader is rendered after all opaque shaders
        Pass
        {
            Blend SrcAlpha OneMinusSrcAlpha // This line enables transparency based on the alpha value
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

            float4 _TopColor;
            float4 _BottomColor;

            v2f vert (appdata v)
            {
                v2f o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.uv = v.uv;
                return o;
            }

            half4 frag (v2f i) : SV_Target
            {
                // Linearly interpolate between top and bottom colors based on the y-coordinate of the UV
                half4 col = lerp(_BottomColor, _TopColor, i.uv.y);
                return col * col.a; // Multiply by alpha for proper transparency
            }
            ENDCG
        }
    }
}

