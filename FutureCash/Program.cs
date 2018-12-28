using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Dynamic;
using System.Linq;
using System.Numerics;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;

namespace FutureCash
{
    class Program
    {
        class Block
        {
            // XXX switch this to an index of Blocks by BlockHash, plus an index of BlockHashes by BlockHeight
            // XXX and persist it all to disk
            public static IDictionary<long, Block> BlocksByHeight = new Dictionary<long, Block>();

            // target hash for minimum difficulty - maximum target allowed
            public static UInt256 MaxTarget = UInt256.MaxValue / 65536;

            public long BlockHeight { get; private set; }
            public long Nonce { get; private set; }
            public UInt256 ParentBlockHash { get; private set; }
            public DateTime Time { get; } = DateTime.UtcNow;
            public UInt256 Target { get; private set; }
            public UInt256 Work
            {
                get
                {
                    return UInt256.MaxValue / Target;
                }
            }
            public UInt256 ChainWork { get; private set; }

            public Block(Block parent = null)
            {
                SetParent(parent);
                BlocksByHeight[BlockHeight] = this;
            }

            private dynamic Header
            {
                get
                {
                    dynamic h = new ExpandoObject();
                    h.Nonce = Nonce;
                    h.ParentBlockHash = ParentBlockHash.ToHex();
                    h.Time = Time.ToString("o");
                    h.Target = Target.ToHex();
                    return h;
                }
            }

            public string Serialize()
            {
                return JsonConvert.SerializeObject(Header);
            }

            public override string ToString()
            {
                dynamic data = Header;
                data.BlockHeight = BlockHeight;
                data.BlockHash = BlockHash.ToHex();
                data.ChainWork = ChainWork.ToHex();
                return JsonConvert.SerializeObject(data);
            }

            public UInt256 BlockHash
            {
                get
                {
                    var buffer = Encoding.UTF8.GetBytes(Serialize());
                    return Hash.Sha256d(buffer);
                }
            }

            public void Mine(long startingNonce = 0)
            {
                Nonce = startingNonce;
                while (BlockHash > Target)
                {
                    Nonce++;
                }
            }

            public void SetParent(Block parent)
            {
                if (parent == null)
                {
                    Target = MaxTarget;
                    ParentBlockHash = UInt256.MinValue;
                    BlockHeight = 0L;
                    ChainWork = Work;
                    return;
                }
                Target = parent.Target;  // XXX
                ParentBlockHash = parent.BlockHash;
                BlockHeight = parent.BlockHeight + 1L;
                ChainWork = parent.ChainWork + Work;
            }
        }

        class UInt256 : IComparable<UInt256>
        {
            private BigInteger value;

            public static UInt256 MaxValue = new UInt256(Enumerable.Repeat(byte.MaxValue, 32).ToArray());
            public static UInt256 MinValue = new UInt256(new byte[] { (byte)0 });

            public UInt256(byte[] value, int startIndex = 0)
            {
                if (value.Length > 32)
                    throw new ArgumentOutOfRangeException("value", value, "byte array is too long for unsigned 256 bit integer");
                var byteList = new List<byte>();
                byteList.Add((byte)0);  // this ensures we get an unsigned integer instead of interpreting a leading 1 as a sign bit
                byteList.AddRange(value);
                byteList.Reverse();
                var littleEndianValue = byteList.ToArray();
                this.value = new BigInteger(littleEndianValue);
                var roundTrip = this.value.ToByteArray();
                // XXX how do I verify that this is not creating a negative value?
                // XXX I have to include a 0 byte to make sure I don't get a negative value
                // XXX byteorder - sounds like this is the opposite of what I was getting from the hashes
                // XXX after I create the value I should be able to round trip it and get my original input
            }

            private UInt256(BigInteger value)
            {
                this.value = value;
            }

            public override string ToString()
            {
                return value.ToString();
            }

            public int CompareTo(UInt256 other)
            {
                return value.CompareTo(other.value);
            }

            public static bool operator <(UInt256 a, UInt256 b)
            {
                return a.value < b.value;
            }

            public static bool operator >(UInt256 a, UInt256 b)
            {
                return a.value > b.value;
            }

            public string ToHex()
            {
                var format = "x64";
                return value.ToString(format);
            }

            public static UInt256 operator /(UInt256 a, UInt256 b)
            {
                // XXX range checking?
                return new UInt256(a.value / b.value);
            }

            public static UInt256 operator /(UInt256 a, int b)
            {
                // XXX range checking?
                return new UInt256(a.value / b);
            }

            public static UInt256 operator +(UInt256 a, UInt256 b)
            {
                var result = a.value + b.value;
                // XXX range checking - should I throw an exception instead?
                if (result > UInt256.MaxValue.value)
                    return UInt256.MaxValue;
                if (result < UInt256.MinValue.value)
                    return UInt256.MinValue;
                return new UInt256(result);
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
            var block = new Block();
            Console.WriteLine("MaxHash: " + block.Target.ToHex());

            for (int i = 0; i < 100; i++)
            {
                Console.WriteLine();

                block.Mine();
                Console.WriteLine("Height: " + i);
                Console.WriteLine("Nonce: " + block.Nonce);
                Console.WriteLine("Hash: " + block.BlockHash.ToHex());
                Console.WriteLine("Block: " + block.ToString());

                var oldBlock = block;
                block = new Block(oldBlock);
            }
            Console.ReadKey();
        }
    }
}
