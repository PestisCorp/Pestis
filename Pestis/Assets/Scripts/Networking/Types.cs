using System;
using Fusion;
using UnityEngine;

namespace Networking
{
    [Serializable]
    public struct IntPositive : INetworkStruct
    {
        [SerializeField] public int compressed;

        public IntPositive(uint value)
        {
            if ((value & 1) == 0)
                compressed = (int)(value >> 1); // Even
            else
                compressed = -((int)(value + 1) >> 1); // Odd
        }

        public static implicit operator uint(IntPositive value)
        {
            return value.compressed >= 0 ? (uint)(value.compressed << 1) : (uint)-((value.compressed << 1) + 1);
        }

        public static implicit operator int(IntPositive value)
        {
            return value.compressed >= 0 ? value.compressed << 1 : -((value.compressed << 1) + 1);
        }

        public override string ToString()
        {
            return ((uint)this).ToString();
        }
    }

    [Serializable]
    public struct Bounds : INetworkStruct
    {
        public Vector2CompRes10 center;
        public Vector2CompRes10 extents;

        public Bounds(Vector2CompRes10 c, Vector2CompRes10 e)
        {
            center = c;
            extents = e;
        }

        public static implicit operator UnityEngine.Bounds(Bounds b)
        {
            return new UnityEngine.Bounds(b.center, b.extents);
        }

        public static implicit operator Bounds(UnityEngine.Bounds b)
        {
            return new Bounds(b.center, b.extents);
        }
    }

    /// <summary>
    ///     Variant of the Fusion struct of the same name, but without the zigzagging weirdness.
    /// </summary>
    [Serializable]
    public struct Vector2CompRes10 : INetworkStruct, IEquatable<Vector2CompRes10>
    {
        private const float ENC = 10;
        private const float DEC = 1f / ENC;
        private const float RND = DEC * .5f;
        private const float RANGE = 256.0f;

        [SerializeField] private IntPositive compressed;

        // Constructors
        private Vector2CompRes10((uint x, uint y) v)
        {
            compressed = new IntPositive(v.x + (v.y << 16));
        }

        public static implicit operator Vector2(Vector2CompRes10 c)
        {
            var x = Convert.ToSingle(c.compressed % 0x00010000) * DEC - RANGE;
            var y = Convert.ToSingle(c.compressed >> 16) * DEC - RANGE;
            return new Vector2(x, y);
        }

        public static implicit operator Vector3(Vector2CompRes10 c)
        {
            return (Vector2)c;
        }

        public static implicit operator Vector2CompRes10(Vector2 v2)
        {
            if (v2.x < -RANGE || v2.x > RANGE || v2.y < -RANGE || v2.y > RANGE)
                throw new OverflowException("Compressed Vector arguments were outside range.");

            var x = Convert.ToUInt32((v2.x + RANGE) * ENC);
            var y = Convert.ToUInt32((v2.y + RANGE) * ENC);
            return new Vector2CompRes10((x, y));
        }

        public static implicit operator Vector2CompRes10(Vector3 v3)
        {
            return (Vector2)v3;
        }

        public bool Equals(Vector2CompRes10 other)
        {
            return compressed == other.compressed;
        }

        public override bool Equals(object obj)
        {
            return obj is Vector2CompRes10 other && Equals(other);
        }

        public override int GetHashCode()
        {
            return HashCode.Combine(compressed.compressed);
        }

        public static bool operator ==(Vector2CompRes10 left, Vector2CompRes10 right)
        {
            return left.Equals(right);
        }

        public static bool operator !=(Vector2CompRes10 left, Vector2CompRes10 right)
        {
            return !left.Equals(right);
        }
    }

    [Serializable]
    public struct PositiveFloatRes100 : INetworkStruct, IEquatable<PositiveFloatRes100>
    {
        private const float ENC = 100;
        private const float DEC = 1f / ENC;
        private const float RND = DEC * .5f;

        [SerializeField] private IntPositive compressed;

        // Constructors
        private PositiveFloatRes100(float f)
        {
            compressed = new IntPositive((uint)Mathf.FloorToInt(f * ENC));
        }

        public static implicit operator float(PositiveFloatRes100 c)
        {
            return (int)c.compressed * DEC;
        }

        public static implicit operator PositiveFloatRes100(float f)
        {
            return new PositiveFloatRes100(f);
        }

        public bool Equals(PositiveFloatRes100 other)
        {
            return compressed == other.compressed;
        }

        public override bool Equals(object obj)
        {
            return obj is PositiveFloatRes100 other && Equals(other);
        }

        public override int GetHashCode()
        {
            return HashCode.Combine(compressed.compressed);
        }

        public static bool operator ==(PositiveFloatRes100 left, PositiveFloatRes100 right)
        {
            return left.Equals(right);
        }

        public static bool operator !=(PositiveFloatRes100 left, PositiveFloatRes100 right)
        {
            return !left.Equals(right);
        }
    }

    [Serializable]
    public struct FloatRes100 : INetworkStruct, IEquatable<FloatRes100>
    {
        private const float ENC = 100;
        private const float DEC = 1f / ENC;
        private const float RND = DEC * .5f;

        [SerializeField] private int compressed;

        // Constructors
        private FloatRes100(float f)
        {
            compressed = Mathf.FloorToInt(f * ENC);
        }

        public static implicit operator float(FloatRes100 c)
        {
            return c.compressed * DEC;
        }

        public static implicit operator FloatRes100(float f)
        {
            return new FloatRes100(f);
        }

        public bool Equals(FloatRes100 other)
        {
            return compressed == other.compressed;
        }

        public override bool Equals(object obj)
        {
            return obj is FloatRes100 other && Equals(other);
        }

        public override int GetHashCode()
        {
            return HashCode.Combine(compressed);
        }

        public static bool operator ==(FloatRes100 left, FloatRes100 right)
        {
            return left.Equals(right);
        }

        public static bool operator !=(FloatRes100 left, FloatRes100 right)
        {
            return !left.Equals(right);
        }
    }

    public struct PopulationGroup1 : INetworkStruct
    {
        [SerializeField] private int compressed;

        //   BIRTH     DEATH      HealthPer
        // 00AAAAAAAAAABBBBBBBBBBCCCCCCCCCC

        public double BirthRate
        {
            get
                =>
                    (compressed >> 20) / 1000.0;
            set
            {
                compressed &= 0b00000000000011111111111111111111;
                compressed |= (int)(value * 1000.0) << 20;
            }
        }

        public double DeathRate
        {
            get
                =>
                    ((compressed & 0b0000000000000011111111110000000000) >> 10) / 1000.0;
            set
            {
                compressed &= 0b00111111111100000000001111111111;
                compressed |= (int)(value * 1000.0) << 10;
            }
        }

        public float HealthPerRat
        {
            get
                =>
                    (compressed & 0b00000000000000000000001111111111) / 100.0f;

            set
            {
                compressed &= 0b00111111111111111111110000000000;
                compressed |= (int)(value * 100.0);
            }
        }
    }

    public struct PopulationGroup2 : INetworkStruct
    {
        [SerializeField] private int compressed;

        // Tundra  Desert  Grass   Stone
        // AAAAAAAABBBBBBBBCCCCCCCCDDDDDDDD

        public float Tundra
        {
            get
                =>
                    (compressed >> 24) / 100.0f;
            set
            {
                compressed &= 0x00FFFFFF;
                compressed |= (int)(value * 100.0f) << 24;
            }
        }

        public float Desert
        {
            get
                =>
                    ((compressed & 0x00FF0000) >> 16) / 100.0f;
            set
            {
                compressed &= unchecked((int)0xFF00FFFF);
                compressed |= (int)(value * 100.0f) << 16;
            }
        }

        public float Grass
        {
            get
                =>
                    ((compressed & 0x0000FF00) >> 8) / 100.0f;

            set
            {
                compressed &= unchecked((int)0xFFFF00FF);
                compressed |= (int)(value * 100.0f) << 8;
            }
        }

        public float Stone
        {
            get
                =>
                    (compressed & 0x000000FF) / 100.0f;

            set
            {
                compressed &= unchecked((int)0xFFFFFF00);
                compressed |= (int)(value * 100.0f);
            }
        }
    }

    public struct PopulationGroup3 : INetworkStruct
    {
        [SerializeField] private int compressed;

        // Damage          Damage Reduction
        // AAAAAAAAAAAAAAAABBBBBBBBBBBBBBBB

        public float Damage
        {
            get
                =>
                    (compressed >> 16) / 100.0f;
            set
            {
                compressed &= 0x0000FFFF;
                compressed |= (int)(value * 100.0f) << 16;
            }
        }

        public float DamageReduction
        {
            get
                =>
                    (compressed & 0x0000FFFF) / 100.0f;
            set
            {
                compressed &= unchecked((int)0xFFFF0000);
                compressed |= (int)(value * 100.0f);
            }
        }
    }

    public struct PopulationGroup4 : INetworkStruct
    {
        [SerializeField] private int compressed;

        // 00AABBCC

        public float DamageMult
        {
            get
                =>
                    (compressed >> 16) / 100.0f;
            set
            {
                compressed &= unchecked((int)0xFF00FFFF);
                compressed |= (int)(value * 100.0f) << 16;
            }
        }

        public float DamageReductionMult
        {
            get
                =>
                    ((compressed & 0x0000FF00) >> 8) / 100.0f;
            set
            {
                compressed &= unchecked((int)0xFFFF00FF);
                compressed |= (int)(value * 100.0f) << 8;
            }
        }

        public float SepticMult
        {
            get
                =>
                    (compressed & 0x000000FF) / 100.0f;
            set
            {
                compressed &= unchecked((int)0xFFFFFF00);
                compressed |= (int)(value * 100.0f);
            }
        }
    }
}