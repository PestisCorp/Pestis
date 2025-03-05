Shader "Unlit/deadBoidShader"
{
    Properties
    {
        _Colour ("Colour", Color) = (1, 1, 0, 1)
        _Scale ("Scale", Float) = 0.1
        _Texture ("Corpse Texture", 2D) = "yellow"
    }
    SubShader
    {
        Tags
        {
            "Queue"="Transparent" "IgnoreProjector"="True" "RenderType"="Transparent"
        }
        Blend SrcAlpha OneMinusSrcAlpha
        Pass
        {
            CGPROGRAM
            #include "UnityCG.cginc"
            #pragma vertex vert
            #pragma fragment frag

            struct Boid
            {
                float2 pos;
                float2 vel;
                int player;
                int horde;
                int dead;
            };

            void rotate2D(inout float2 v, float2 vel)
            {
                float2 dir = normalize(vel);
                v = float2(v.x * dir.y + v.y * dir.x, v.y * dir.y - v.x * dir.x);
            }

            float4 _Colour;
            float _Scale;
            sampler2D _Texture;

            StructuredBuffer<Boid> boids;
            StructuredBuffer<float2> _Positions;

            struct v2f
            {
                float4 pos : SV_POSITION;
                float2 uv : TEXCOORD0;
            };

            v2f vert(uint vertexID : SV_VertexID)
            {
                uint instanceID = vertexID / 4;
                Boid boid = boids[instanceID];
                float2 pos = _Positions[vertexID - instanceID * 4];
                v2f o;
                o.pos = UnityWorldToClipPos(float4(pos + boid.pos, 0, 0));
                if (vertexID % 4 == 0)
                {
                    o.uv = float2(0, 0);
                }
                else if (vertexID % 4 == 1)
                {
                    o.uv = float2(0, 1);
                }
                else if (vertexID % 4 == 2)
                {
                    o.uv = float2(1, 1);
                }
                else
                {
                    o.uv = float2(1, 0);
                }
                return o;
            }

            fixed4 frag(v2f i) : SV_Target
            {
                return tex2D(_Texture, i.uv);
            }
            ENDCG
        }
    }
}