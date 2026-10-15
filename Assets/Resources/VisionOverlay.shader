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
            float _UseWorld3D;
            float _Orthographic;
            float3 _Ground00, _Ground10, _Ground01, _Ground11;
            float3 _WorldCamera, _WorldPlayer, _Ray00, _Ray10, _Ray01, _Ray11;
            v2f vert(appdata v) { v2f o; o.vertex = UnityObjectToClipPos(v.vertex); o.uv = v.uv; return o; }
            fixed4 frag(v2f i) : SV_Target
            {
                float2 delta = i.uv - _VisionCenter;
                delta.x *= _Aspect;
                float distanceFromPlayer = length(delta);
                if (_UseWorld3D > .5)
                {
                    float3 ray = lerp(lerp(_Ray00,_Ray10,i.uv.x),lerp(_Ray01,_Ray11,i.uv.x),i.uv.y);
                    float3 ground = _WorldCamera + ray * (-_WorldCamera.y / min(-.0001,ray.y));
                    if (_Orthographic > .5)
                        ground = lerp(lerp(_Ground00,_Ground10,i.uv.x),lerp(_Ground01,_Ground11,i.uv.x),i.uv.y);
                    distanceFromPlayer = length(ground.xz - _WorldPlayer.xz);
                }
                float alpha = smoothstep(_ClearRadius, _DarkRadius, distanceFromPlayer) * _OuterDarkness;
                return fixed4(0, 0, 0, alpha);
            }
            ENDCG
        }
    }
}
