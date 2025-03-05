Shader "Unlit/boidShader"
{
    Properties
    {
        _Colour ("Colour", Color) = (1, 1, 0, 1)
        _Scale ("Scale", Float) = 0.1
        _RatUp ("Rat Up", 2D) = "yellow"
        _RatUpRight ("Rat Up-Right", 2D) = "grey"
        _RatRight ("Rat Right", 2D) = "grey"
        _RatDownRight ("Rat Down-Right", 2D) = "grey"
        _RatDown ("Rat Down", 2D) = "grey"
        _RatDownLeft ("Rat Down-Left", 2D) = "grey"
        _RatLeft ("Rat Left", 2D) = "grey"
        _RatUpLeft ("Rat Up-Left", 2D) = "grey"
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
                float2 uv;
                int sprite;
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
                if (vertexID % 4 == 0)
                {
                    if (boid.dead)
                    {
                        o.otherData.uv = float2(0, 1);
                    }
                    else
                    {
                        o.otherData.uv = float2(0, 0);
                    }
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
                switch (i.otherData.sprite)
                {
                case 0:
                    return tex2D(_RatUp, i.otherData.uv);
                case 1:
                    return tex2D(_RatUpRight, i.otherData.uv);
                case 2:
                    return tex2D(_RatRight, i.otherData.uv);
                case 3:
                    return tex2D(_RatDownRight, i.otherData.uv);
                case 4:
                    return tex2D(_RatDown, i.otherData.uv);
                case 5:
                    return tex2D(_RatDownLeft, i.otherData.uv);
                case 6:
                    return tex2D(_RatLeft, i.otherData.uv);
                case 7:
                    return tex2D(_RatUpLeft, i.otherData.uv);
                default:
                    return _Colour;
                }
            }
            ENDCG
        }
    }
}