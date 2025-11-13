Shader "Unlit/boidShader"
{
    Properties
    {
        _Colour ("Colour", Color) = (1, 1, 0, 0)
        _Scale ("Scale", Float) = 0.1
        // [Up, UpRight, Right, DownRight, Down]
        _MainTex ("Texture Array", 2DArray) = "" {}
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
            #pragma require 2darray

            struct Boid
            {
                float2 pos;
                float2 vel;
                int player;
                int horde;
                bool dead;
            };

            void rotate2D(inout float2 v, float2 vel)
            {
                float2 dir = normalize(vel);
                v = float2(v.x * dir.y + v.y * dir.x, v.y * dir.y - v.x * dir.x);
            }

            float4 _Colour;
            float _Scale;
            sampler2D _RatUp;
            sampler2D _RatUpRight;
            sampler2D _RatRight;
            sampler2D _RatDownRight;
            sampler2D _RatDown;
            sampler2D _RatDownLeft;
            sampler2D _RatLeft;
            sampler2D _RatUpLeft;

            StructuredBuffer<Boid> boids;
            StructuredBuffer<float2> _Positions;

            struct other
            {
                float3 uv;
                bool dead;
            };

            struct v2f
            {
                float4 pos : SV_POSITION;
                //float2 uv : TEXCOORD0;
                other otherData: TEXCOORD0;
            };

            v2f vert(uint vertexID : SV_VertexID)
            {
                uint instanceID = vertexID / 4;
                Boid boid = boids[instanceID];
                float2 pos = _Positions[vertexID - instanceID * 4];
                v2f o;
                o.pos = UnityWorldToClipPos(float4(pos + boid.pos, 0, 0));
                o.otherData.dead = boid.dead;
                if (vertexID % 4 == 0)
                {
                    o.otherData.uv = float3(0, 0, 0);
                }
                else if (vertexID % 4 == 1)
                {
                    o.otherData.uv = float3(0, 1, 0);
                }
                else if (vertexID % 4 == 2)
                {
                    o.otherData.uv = float3(1, 1, 0);
                }
                else
                {
                    o.otherData.uv = float3(1, 0, 0);
                }

                // Up
                if (boid.vel.x > 0 && boid.vel.y > 0 && boid.vel.y > boid.vel.x)
                {
                    o.otherData.uv.z = 0;
                }
                // UpRight
                else if (boid.vel.x > 0 && boid.vel.y > 0)
                {
                    o.otherData.uv.z = 1;
                }
                // Right
                else if (boid.vel.x > 0 && boid.vel.y < 0 && boid.vel.x > -boid.vel.y)
                {
                    o.otherData.uv.z = 2;
                }
                // DownRight
                else if (boid.vel.x > 0 && boid.vel.y < 0)
                {
                    o.otherData.uv.z = 3;
                }
                // Down
                else if (boid.vel.x < 0 && boid.vel.y < 0 && boid.vel.y < boid.vel.x)
                {
                    o.otherData.uv.z = 4;
                }
                // DownLeft
                else if (boid.vel.x < 0 && boid.vel.y < 0)
                {
                    o.otherData.uv.z = 3;
                    o.otherData.uv.x = 1.0 - o.otherData.uv.x;
                }
                // Left
                else if (boid.vel.x < 0 && boid.vel.y > 0 && -boid.vel.x > boid.vel.y)
                {
                    o.otherData.uv.z = 2;
                    o.otherData.uv.x = 1.0 - o.otherData.uv.x;
                }
                // UpLeft
                else if (boid.vel.x < 0 && boid.vel.y > 0)
                {
                    o.otherData.uv.z = 1;
                    o.otherData.uv.x = 1.0 - o.otherData.uv.x;
                }
                return o;
            }

            UNITY_DECLARE_TEX2DARRAY(_MainTex);

            fixed4 frag(v2f i) : SV_Target
            {
                if (i.otherData.dead)
                {
                    return _Colour;
                }

                return UNITY_SAMPLE_TEX2DARRAY(_MainTex, i.otherData.uv);
            }
            ENDCG
        }
    }
}