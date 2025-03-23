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
}