Shader "DungeonSweeper/VisionOverlay"
{
    Properties { _OuterDarkness("Outer Darkness", Range(0,1)) = 0.82 }
    SubShader
    {
        Tags { "Queue"="Overlay" "RenderType"="Transparent" "IgnoreProjector"="True" }
        Cull Off ZWrite Off ZTest Always
        Blend SrcAlpha OneMinusSrcAlpha
        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"
            struct appdata { float4 vertex : POSITION; float2 uv : TEXCOORD0; };
            struct v2f { float4 vertex : SV_POSITION; float2 uv : TEXCOORD0; };
            float2 _VisionCenter;
            float _ClearRadius;
            float _DarkRadius;
            float _OuterDarkness;
            float _Aspect;
            v2f vert(appdata v) { v2f o; o.vertex = UnityObjectToClipPos(v.vertex); o.uv = v.uv; return o; }
            fixed4 frag(v2f i) : SV_Target
            {
                float2 delta = i.uv - _VisionCenter;
                delta.x *= _Aspect;
                float distanceFromPlayer = length(delta);
                float alpha = smoothstep(_ClearRadius, _DarkRadius, distanceFromPlayer) * _OuterDarkness;
                return fixed4(0, 0, 0, alpha);
            }
            ENDCG
        }
    }
}
