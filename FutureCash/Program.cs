using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;

namespace FutureCash
{
    class Program
    {
        class UInt256 : IComparable<UInt256>
        {
            private ulong A;
            private ulong B;
            private ulong C;
            private ulong D;

            public UInt256(ulong a, ulong b, ulong c, ulong d)
            {
                A = a;
                B = b;
                C = c;
                D = d;
            }

            public static UInt256 MaxValue = new UInt256(ulong.MaxValue, ulong.MaxValue, ulong.MaxValue, ulong.MaxValue);

            public UInt256(byte[] value, int startIndex = 0)
            {
                A = BitConverter.ToUInt64(value, startIndex);
                B = BitConverter.ToUInt64(value, startIndex + 4);
                C = BitConverter.ToUInt64(value, startIndex + 8);
                D = BitConverter.ToUInt64(value, startIndex + 12);
            }

            public override string ToString()
            {
                return A.ToString() + B.ToString() + C.ToString() + D.ToString();
            }

            public int CompareTo(UInt256 other)
            {
                if (A < other.A)
                    return -1;
                if (A > other.A)
                    return 1;
                if (B < other.B)
                    return -1;
                if (B > other.B)
                    return 1;
                if (C < other.C)
                    return -1;
                if (C > other.C)
                    return 1;
                return D.CompareTo(other.D);
            }

            public static bool operator <(UInt256 a, UInt256 b)
            {
                return a.CompareTo(b) < 0;
            }

            public static bool operator >(UInt256 a, UInt256 b)
            {
                return a.CompareTo(b) > 0;
            }

            public string ToHex()
            {
                var format = "X16";
                return A.ToString(format) + B.ToString(format) + C.ToString(format) + D.ToString(format);
            }
        }

        class Hash
        {
            private static SHA256 sha256 = SHA256.Create();

            public static UInt256 Sha256d(byte[] buffer)
            {
                return new UInt256(sha256.ComputeHash(sha256.ComputeHash(buffer)));
            }
        }

        class Block
        {
            public long Nonce;

            private Object SerializableRepresentation
            {
                get
                {
                    return new { Nonce = Nonce };
                }
            }

            public string Serialize()
            {
                return JsonConvert.SerializeObject(SerializableRepresentation);
            }

            public override string ToString()
            {
                return Serialize();
            }

            public UInt256 BlockHash
            {
                get
                {
                    var buffer = Encoding.UTF8.GetBytes(Serialize());
                    return Hash.Sha256d(buffer);
                }
            }

            public void Mine(UInt256 maxHash, long startingNonce = 0)
            {
                Nonce = startingNonce;
                while (BlockHash > maxHash)
                {
                    Nonce++;
                }
            }
        }

        static void Main(string[] args)
        {
            var block = new Block();
            var maxHash = new UInt256(ulong.MaxValue / 32, ulong.MaxValue, ulong.MaxValue, ulong.MaxValue);
            Console.WriteLine("MaxHash: " + maxHash.ToHex());
            block.Mine(maxHash);
            Console.WriteLine(block.Nonce);
            Console.WriteLine(block.BlockHash.ToHex());
            Console.ReadKey();
        }
    }
}
