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
        class Block
        {
            public long Nonce;
            public UInt256 ParentBlockHash;
            public DateTime Time = DateTime.UtcNow;

            private Object SerializableRepresentation
            {
                get
                {
                    return new
                    {
                        Nonce = Nonce,
                        ParentBlockHash = ParentBlockHash.ToHex(),
                        Time = Time.ToString("o")
                    };
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
            public static UInt256 MinValue = new UInt256(0, 0, 0, 0);

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
                var format = "x16";
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

        static void Main(string[] args)
        {
            var maxHash = new UInt256(ulong.MaxValue / 65536, ulong.MaxValue, ulong.MaxValue, ulong.MaxValue);
            Console.WriteLine("MaxHash: " + maxHash.ToHex());

            var block = new Block();
            block.ParentBlockHash = UInt256.MinValue;

            for (int i = 0; i < 10; i++)
            {
                Console.WriteLine();

                block.Mine(maxHash);
                Console.WriteLine("Height: " + i);
                Console.WriteLine("Nonce: " + block.Nonce);
                Console.WriteLine("Hash: " + block.BlockHash.ToHex());
                Console.WriteLine("Block: " + block.ToString());

                var oldBlock = block;
                block = new Block();
                block.ParentBlockHash = oldBlock.BlockHash;
            }
            Console.ReadKey();
        }
    }
}
