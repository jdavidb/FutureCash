using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Dynamic;
using System.Linq;
using System.Numerics;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using System.Timers;

namespace FutureCash
{
    class Program
    {
        static Block newBlock;

        class Block
        {
            // XXX switch this to an index of Blocks by BlockHash, plus an index of BlockHashes by BlockHeight
            // XXX and persist it all to disk
            public static IDictionary<long, Block> BlocksByHeight = new Dictionary<long, Block>();

            // target hash for minimum difficulty - maximum target allowed
            public static UInt256 MaxTarget = UInt256.MaxValue / 65536;

            // desired block interval in seconds
            public static int BlockInterval = 1;

            public long BlockHeight { get; private set; }
            public long Nonce { get; private set; }
            public UInt256 ParentBlockHash { get; private set; }
            public DateTime Time { get; set; } = DateTime.UtcNow;
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
                    //if ((Nonce % 100000) == 0)
                    //{
                    //    Console.WriteLine("Invalid nonce: " + Nonce + " / Hash: " + BlockHash.ToHex());
                    //}
                    Nonce++;
                }
            }

            private static UInt256 ComputeNewTarget(Block b1, Block b2)
            {
                var work = b2.ChainWork - b1.ChainWork;
                var heightDifference = b2.BlockHeight - b1.BlockHeight;
                var expectedTime = heightDifference * BlockInterval;
                var actualTime = b2.Time - b1.Time;
                var actualTimeSeconds = Convert.ToInt64(actualTime.TotalSeconds);
                if (actualTimeSeconds > 2 * expectedTime) actualTimeSeconds = 2 * expectedTime;
                if (actualTimeSeconds < expectedTime / 2) actualTimeSeconds = expectedTime / 2;

                var workNew = work * BlockInterval / actualTimeSeconds;
                var newTarget = UInt256.MaxValue / workNew;
                Console.WriteLine("Expected time: " + expectedTime);
                Console.WriteLine("Actual time: " + actualTimeSeconds);
                Console.WriteLine("Expected/actual ratio: " + (decimal)expectedTime / (decimal)actualTimeSeconds);
                if (actualTimeSeconds > expectedTime)
                {
                    Console.WriteLine("Actual time > expected time: target should go up to make it easier");
                }
                else if (actualTimeSeconds < expectedTime)
                {
                    Console.WriteLine("Actual time < expected time: target should go down to make it harder");
                }
                //var newTarget = b2.Target * actualTimeSeconds / expectedTime;
                if (newTarget > b2.Target)
                {
                    Console.WriteLine("Target went up: easier");
                }
                else if (newTarget < b2.Target)
                {
                    Console.WriteLine("Target went down: harder");
                }
                Console.WriteLine("Oldtarget/newtarget ratio as %: " + b2.Target.DivPercent(newTarget));
                Console.WriteLine("New target: " + newTarget.ToHex());
                return newTarget;
            }

            private UInt256 ComputeNewTarget()
            {
                // XXX
                // look up by hash instead
                // among many other changes here
                var parent = BlocksByHeight[BlockHeight - 1];
                if (BlockHeight <= 6)
                    return parent.Target;
                var genesisBlock = BlocksByHeight[0];
                return ComputeNewTarget(genesisBlock, parent);
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
                ParentBlockHash = parent.BlockHash;
                BlockHeight = parent.BlockHeight + 1L;
                Target = ComputeNewTarget();
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

            public static UInt256 operator /(UInt256 a, long b)
            {
                // XXX range checking?
                return new UInt256(a.value / b);
            }

            private static UInt256 RestrainRange(BigInteger value)
            {
                if (value > UInt256.MaxValue.value)
                    return UInt256.MaxValue;
                if (value < UInt256.MinValue.value)
                    return UInt256.MinValue;
                return new UInt256(value);
            }

            internal decimal DivPercent(UInt256 other)
            {
                var v = value;
                var vo = other.value;
                return (decimal)(v * 100 / vo);
            }

            public static UInt256 operator +(UInt256 a, UInt256 b)
            {
                var result = a.value + b.value;
                // XXX range checking - should I throw an exception instead?
                return RestrainRange(result);
            }

            public static UInt256 operator -(UInt256 a, UInt256 b)
            {
                var result = a.value - b.value;
                // XXX range checking - should I throw an exception instead?
                return RestrainRange(result);
            }

            public static UInt256 operator *(UInt256 a, long b)
            {
                var result = a.value * b;
                // XXX range checking - should I throw an exception instead?
                return RestrainRange(result);
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
            newBlock = new Block();
            Console.WriteLine("MaxHash: " + newBlock.Target.ToHex());

            var timer = new Timer(3000);
            timer.Elapsed += (Object source, ElapsedEventArgs e) => { newBlock.Time = DateTime.UtcNow; };
            timer.AutoReset = true;
            timer.Enabled = true;

            for (int i = 0; i < 100000; i++)
            {
                Console.WriteLine();

                newBlock.Mine();
                Console.WriteLine("Height: " + i);
                //Console.WriteLine("Nonce: " + newBlock.Nonce);
                Console.WriteLine("Hash: " + newBlock.BlockHash.ToHex());
                //Console.WriteLine("Block: " + newBlock.ToString());
                Console.WriteLine("Time: " + DateTime.UtcNow.ToString("o"));

                var oldBlock = newBlock;
                newBlock = new Block(oldBlock);
            }
            Console.ReadKey();
        }
    }
}
