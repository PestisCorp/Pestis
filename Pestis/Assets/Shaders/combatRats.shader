Shader "Unlit/combatBoidShader"
{
    Properties
    {
        _Colour ("Colour", Color) = (1, 1, 0, 0)
        _Scale ("Scale", Float) = 0.1
        _RatUpArr ("Rat Up", 2DArray) = ""{}
        _RatUpRightArr ("Rat Up-Right", 2DArray) = ""{}
        _RatRightArr ("Rat Right", 2DArray) = ""{}
        _RatDownRightArr ("Rat Down-Right", 2DArray) = ""{}
        _RatDownArr ("Rat Down", 2DArray) = ""{}
        _RatDownLeftArr ("Rat Down-Left", 2DArray) = ""{}
        _RatLeftArr ("Rat Left", 2DArray) = ""{}
        _RatUpLeftArr ("Rat Up-Left", 2DArray) = ""{}
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
                bool dead;
            };

            void rotate2D(inout float2 v, float2 vel)
            {
                float2 dir = normalize(vel);
                v = float2(v.x * dir.y + v.y * dir.x, v.y * dir.y - v.x * dir.x);
            }

            float4 _Colour;
            float _Scale;
            UNITY_DECLARE_TEX2DARRAY(_RatUpArr);
            UNITY_DECLARE_TEX2DARRAY(_RatUpRightArr);
            UNITY_DECLARE_TEX2DARRAY(_RatRightArr);
            UNITY_DECLARE_TEX2DARRAY(_RatDownRightArr);
            UNITY_DECLARE_TEX2DARRAY(_RatDownArr);
            UNITY_DECLARE_TEX2DARRAY(_RatDownLeftArr);
            UNITY_DECLARE_TEX2DARRAY(_RatLeftArr);
            UNITY_DECLARE_TEX2DARRAY(_RatUpLeftArr);

            StructuredBuffer<Boid> boids;
            StructuredBuffer<float2> _Positions;

            struct other
            {
                float2 uv;
                int sprite;
                bool dead;
                int horde;
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
                //rotate2D(pos, boid.vel);
                v2f o;
                o.pos = UnityWorldToClipPos(float4(pos + boid.pos, 0, 0));
                o.otherData.horde = boid.horde;
                o.otherData.dead = boid.dead;
                if (vertexID % 4 == 0)
                {
                    o.otherData.uv = float2(0, 0);
                }
                else if (vertexID % 4 == 1)
                {
                    o.otherData.uv = float2(0, 1);
                }
                else if (vertexID % 4 == 2)
                {
                    o.otherData.uv = float2(1, 1);
                }
                else
                {
                    o.otherData.uv = float2(1, 0);
                }

                if (boid.vel.x > 0 && boid.vel.y > 0 && boid.vel.y > boid.vel.x)
                {
                    o.otherData.sprite = 0;
                }
                else if (boid.vel.x > 0 && boid.vel.y > 0)
                {
                    o.otherData.sprite = 1;
                }
                else if (boid.vel.x > 0 && boid.vel.y < 0 && boid.vel.x > -boid.vel.y)
                {
                    o.otherData.sprite = 2;
                }
                else if (boid.vel.x > 0 && boid.vel.y < 0)
                {
                    o.otherData.sprite = 3;
                }
                else if (boid.vel.x < 0 && boid.vel.y < 0 && boid.vel.y < boid.vel.x)
                {
                    o.otherData.sprite = 4;
                }
                else if (boid.vel.x < 0 && boid.vel.y < 0)
                {
                    o.otherData.sprite = 5;
                }
                else if (boid.vel.x < 0 && boid.vel.y > 0 && -boid.vel.x > boid.vel.y)
                {
                    o.otherData.sprite = 6;
                }
                else if (boid.vel.x < 0 && boid.vel.y > 0)
                {
                    o.otherData.sprite = 7;
                }
                return o;
            }

            fixed4 frag(v2f i) : SV_Target
            {
                if (i.otherData.dead)
                {
                    return _Colour;
                }
                switch (i.otherData.sprite)
                {
                case 0:
                    return UNITY_SAMPLE_TEX2DARRAY(_RatUpArr, float3(i.otherData.uv, i.otherData.horde));
                case 1:
                    return UNITY_SAMPLE_TEX2DARRAY(_RatUpRightArr, float3(i.otherData.uv, i.otherData.horde));
                case 2:
                    return UNITY_SAMPLE_TEX2DARRAY(_RatRightArr, float3(i.otherData.uv, i.otherData.horde));
                case 3:
                    return UNITY_SAMPLE_TEX2DARRAY(_RatDownRightArr, float3(i.otherData.uv, i.otherData.horde));
                case 4:
                    return UNITY_SAMPLE_TEX2DARRAY(_RatDownArr, float3(i.otherData.uv, i.otherData.horde));
                case 5:
                    return UNITY_SAMPLE_TEX2DARRAY(_RatDownLeftArr, float3(i.otherData.uv, i.otherData.horde));
                case 6:
                    return UNITY_SAMPLE_TEX2DARRAY(_RatLeftArr, float3(i.otherData.uv, i.otherData.horde));
                case 7:
                    return UNITY_SAMPLE_TEX2DARRAY(_RatUpLeftArr, float3(i.otherData.uv, i.otherData.horde));
                default:
                    return _Colour;
                }
            }
            ENDCG
        }
    }
}