using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Dynamic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Numerics;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Timers;

namespace FutureCash
{
    class Program
    {
        class Block
        {
            // XXX persist this to disk
            public static IDictionary<long, string> BlocksByHeight = new Dictionary<long, string>();
            public static IDictionary<string, Block> Blocks = new Dictionary<string, Block>();

            public static long BlockCount = -1;

            // target hash for minimum difficulty - maximum target allowed
            public static UInt256 MaxTarget = UInt256.MaxValue / 65536;

            // desired block interval in seconds
            public static int BlockInterval = 60;  // target block interval in seconds
            public static int DifficultyAdjustmentComputationInterval = 86400;  // one day in seconds
            private static int BlocksPerAdjustmentComputationInterval = DifficultyAdjustmentComputationInterval / BlockInterval;
            public static int TestScalingFactor = 1;

            public class BlockHeader
            {
                public long Nonce { get; internal set; }
                public UInt256 ParentBlockHash { get; internal set; }
                public DateTime Time { get; set; } = DateTime.UtcNow;
                public UInt256 Target { get; internal set; }
            }

            public BlockHeader Header { get; } = new BlockHeader();

            [JsonIgnore]
            public long Nonce { get { return Header.Nonce; } private set { Header.Nonce = value; } }

            [JsonIgnore]
            public UInt256 ParentBlockHash { get { return Header.ParentBlockHash; } private set { Header.ParentBlockHash = value; } }

            [JsonIgnore]
            public DateTime Time { get { return Header.Time; } private set { Header.Time = value; } }

            [JsonIgnore]
            public UInt256 Target { get { return Header.Target; } private set { Header.Target = value; } }

            public long BlockHeight { get; private set; }

            [JsonIgnore]
            public UInt256 Work
            {
                get
                {
                    return UInt256.MaxValue / Target;
                }
            }

            public UInt256 ChainWork { get; private set; }

            private static bool NewBlockTimeDirty = true;
            private static object NewBlockTimeLock = new object();
            private static DateTime _NewBlockTime = DateTime.UtcNow;
            public static DateTime NewBlockTime
            {
                get
                {
                    lock (NewBlockTimeLock)
                    {
                        NewBlockTimeDirty = false;
                        return _NewBlockTime;
                    }
                }
                set
                {
                    lock (NewBlockTimeLock)
                    {
                        NewBlockTimeDirty = true;
                        _NewBlockTime = value;
                    }
                }
            }

            public Block(Block parent = null)
            {
                SetParent(parent);
            }

            public override string ToString()
            {
                return JsonConvert.SerializeObject(this);
            }

            public UInt256 BlockHash
            {
                get
                {
                    var headerSerialization = JsonConvert.SerializeObject(Header);
                    var buffer = Encoding.UTF8.GetBytes(headerSerialization);
                    var hash = Hash.Sha256d(buffer);
                    return hash;
                }
            }

            public void Mine(long startingNonce = 0)
            {
                Nonce = startingNonce;
                while (BlockHash > Target)
                {
                    Nonce++;
                    if (NewBlockTimeDirty)
                    {
                        Time = NewBlockTime;
                    }
                }
                BlockCount = BlockHeight;
                BlocksByHeight[BlockHeight] = BlockHash.ToString();
                Blocks[BlockHash.ToString()] = this;
            }

            private Block ParentBlock
            {
                get
                {
                    if (BlockHeight < 1)
                        return null;
                    return Blocks[ParentBlockHash.ToString()];
                }
            }

            // Get the block with median time from the last N blocks
            private Block GetMedianBlock(long n = 3)
            {
                IList<Block> blocks = new List<Block>();
                if (n > BlockHeight) n = BlockHeight;
                var b = this;
                for (var i = n; i > 0; i--)
                {
                    blocks.Add(b);
                    b = b.ParentBlock;
                }
                var orderedBlocks = blocks.OrderBy(p => p.Time);
                var medianBlock = orderedBlocks.ElementAt(Convert.ToInt32(n) / 2);
                return medianBlock;
            }

            private static UInt256 ComputeNewTarget(Block b1, Block b2)
            {
                var effectiveBlockInterval = BlockInterval / TestScalingFactor;

                var work = b2.ChainWork - b1.ChainWork;
                var heightDifference = b2.BlockHeight - b1.BlockHeight;
                var expectedTime = heightDifference * effectiveBlockInterval;
                var actualTime = b2.Time - b1.Time;
                var actualTimeSeconds = Convert.ToInt64(actualTime.TotalSeconds);
                if (actualTimeSeconds > 2 * expectedTime) actualTimeSeconds = 2 * expectedTime;
                if (actualTimeSeconds < expectedTime / 2) actualTimeSeconds = expectedTime / 2;
                if (actualTimeSeconds == 0) actualTimeSeconds = 1;

                var workNew = work * effectiveBlockInterval / actualTimeSeconds;
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
                Console.WriteLine("New target: " + newTarget.ToString());
                return newTarget;
            }

            private UInt256 ComputeNewTarget()
            {
                if (BlockHeight < 1)
                    return MaxTarget;

                var parent = ParentBlock;
                if (BlockHeight <= 6)
                    return parent.Target;

                var ancestorIndex = parent.BlockHeight - BlocksPerAdjustmentComputationInterval;
                if (ancestorIndex < 0) ancestorIndex = 0;
                var firstBlock = Blocks[BlocksByHeight[ancestorIndex]];

                parent = parent.GetMedianBlock();
                if (firstBlock.BlockHeight >= 2)
                    firstBlock = firstBlock.GetMedianBlock();

                return ComputeNewTarget(firstBlock, parent);
            }

            public void SetParent(Block parent)
            {
                if (parent == null)
                {
                    Console.WriteLine("No parent specified - checking for existing blocks");
                    if (Blocks.Count > 0)
                    {
                        Console.WriteLine("I have blocks - should use block " + (Blocks.Count - 1) + " as parent");
                        var hash = BlocksByHeight[Blocks.Count - 1];
                        Console.WriteLine("Head of the chain is hash " + hash);
                        parent = Blocks[hash];
                    }
                    else
                    {
                        Console.WriteLine("I didn't see any blocks to start from, so starting a new chain");
                    }
                }
                if (parent == null)
                {
                    Console.WriteLine("parent is null; starting a new chain");
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

            public static bool ValidateAndStoreBlock(string receivedBlockData)
            {
                dynamic receivedBlock = JsonConvert.DeserializeObject(receivedBlockData);
                return ValidateAndStoreBlock(receivedBlock);
            }

            private static bool ValidateAndStoreBlock(dynamic receivedBlock)
            {
                Console.WriteLine("Validating a block");
                var header = receivedBlock["Header"];
                string parentHash = header["ParentBlockHash"];
                var isGenesis = parentHash == UInt256.MinValue.ToString();
                if (!Blocks.ContainsKey(parentHash) && !isGenesis)
                {
                    Console.WriteLine("Invalid parent hash - not found");
                    return false;
                }
                var parent = isGenesis ? null : Blocks[parentHash];
                var block = new Block(parent);
                long blockHeight = Convert.ToInt64(receivedBlock["BlockHeight"]);
                if (block.BlockHeight != blockHeight)
                {
                    Console.WriteLine("Invalid block height");
                    return false;
                }
                var time = Convert.ToDateTime(header["Time"]);
                // XXX validate time
                block.Time = time;
                string target = header["Target"];
                if (target != block.Target.ToString())
                {
                    Console.WriteLine("Invalid target");
                    return false;
                }
                var chainWork = receivedBlock["ChainWork"];
                if (chainWork != block.ChainWork.ToString())
                {
                    Console.WriteLine("Invalid ChainWork");
                    return false;
                }
                long nonce = Convert.ToInt64(header["Nonce"]);
                block.Nonce = nonce;
                string blockHash = receivedBlock["BlockHash"];
                Console.WriteLine("Received BlockHash is " + blockHash);
                Console.WriteLine("Calculated BlockHash is " + block.BlockHash.ToString());
                if (blockHash != block.BlockHash.ToString())
                {
                    Console.WriteLine("Invalid BlockHash");
                    Console.WriteLine("Here is the block I received " + receivedBlock.ToString());
                    Console.WriteLine("Here is the block I have constructed during validation " + block.ToString());
                    return false;
                }
                if (block.BlockHash > block.Target)
                {
                    Console.WriteLine("BlockHash does not meet target");
                }
                BlocksByHeight[blockHeight] = blockHash;
                Blocks[blockHash] = block;
                return true;
            }
        }

        public class ToStringJsonConverter : JsonConverter
        {
            public override bool CanConvert(Type objectType)
            {
                return true;
            }

            public override object ReadJson(JsonReader reader, Type objectType, object existingValue, JsonSerializer serializer)
            {
                throw new NotImplementedException();
            }

            public override void WriteJson(JsonWriter writer, object value, JsonSerializer serializer)
            {
                writer.WriteValue(value.ToString());
            }
        }

        [JsonConverter(typeof(ToStringJsonConverter))]
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
            }

            private UInt256(BigInteger value)
            {
                this.value = value;
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

            public override string ToString()
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
            public static UInt256 Sha256d(byte[] buffer)
            {
                using (var sha256 = SHA256.Create())
                {
                    return new UInt256(sha256.ComputeHash(sha256.ComputeHash(buffer)));
                }
            }
        }

        private static void Mine()
        {
            var newBlock = new Block();
            Console.WriteLine("MaxHash: " + Block.MaxTarget.ToString());

            var timer = new System.Timers.Timer(3000);
            timer.Elapsed += (Object source, ElapsedEventArgs e) => { Block.NewBlockTime = DateTime.UtcNow; };
            timer.AutoReset = true;
            timer.Enabled = true;

            for (int i = 0; i < 10000; i++)
            {
                Console.WriteLine();

                newBlock.Mine();
                Console.WriteLine("Height: " + newBlock.BlockHeight);
                Console.WriteLine("Nonce: " + newBlock.Nonce);
                Console.WriteLine("Hash: " + newBlock.BlockHash.ToString());
                Console.WriteLine("Block: " + newBlock.ToString());
                Console.WriteLine("Time: " + DateTime.UtcNow.ToString("o"));

                var oldBlock = newBlock;
                newBlock = new Block(oldBlock);
            }
        }

        private static bool serverRunning = true;

        static void Main(string[] args)
        {
            try
            {
                StartServerThread();
            }
            catch (Exception ex)
            {
            }
            if (args.Length > 0)
            {
                var endpoint = ParseIPEndPoint(args[0]);
                Console.WriteLine("Host: " + endpoint.Address);
                Console.WriteLine("Port: " + endpoint.Port);
                ConnectAndSync(endpoint);
            }
            Mine();
            Console.ReadKey();
            serverRunning = false;
        }

        private static void StartServerThread(int port = 9999)
        {
            var listener = new TcpListener(IPAddress.IPv6Any, port);
            listener.Server.SetSocketOption(SocketOptionLevel.IPv6, SocketOptionName.IPv6Only, false);
            listener.Start();
            new Thread(
            () =>
            {
                while (serverRunning)
                {
                    var client = listener.AcceptTcpClient();
                    new Thread(
                        () =>
                        {
                            using (var ns = client.GetStream())
                            using (var sr = new StreamReader(ns))
                            using (var sw = new StreamWriter(ns))
                            {
                                dynamic cmd = JsonConvert.DeserializeObject(sr.ReadLine());
                                object result;
                                var command = cmd["command"];
                                if (command == "getblockcount")
                                {
                                    result = Block.BlockCount;
                                }
                                else if (command == "getblockhash")
                                {
                                    var blockheight = Convert.ToInt64(cmd["blockheight"]);
                                    result = Block.BlocksByHeight[blockheight];
                                }
                                else if (command == "getblock")
                                {
                                    var block = Block.Blocks[(string)cmd["blockhash"]];
                                    result = block;
                                }
                                else
                                {
                                    result = new { errorMessage = "Unrecognized command " + cmd["command"] };
                                }
                                sw.WriteLine(JsonConvert.SerializeObject(result));
                            }
                        }).Start();
                }
            }).Start();
        }

        private static string SendCommand(IPEndPoint server, object cmd)
        {
            using (var tcpClient = new TcpClient(server.Address.ToString(), server.Port))
            using (var ns = tcpClient.GetStream())
            using (var sr = new StreamReader(ns))
            using (var sw = new StreamWriter(ns))
            {
                sw.WriteLine(JsonConvert.SerializeObject(cmd));
                sw.Flush();
                return sr.ReadToEnd();
            }
        }

        private static void ConnectAndSync(IPEndPoint server)
        {
            var cmd = new { command = "getblockcount" };
            long blockcount = Convert.ToInt64(SendCommand(server, cmd));
            Console.WriteLine("Need to sync up to " + blockcount + " blocks");

            for (long c = 0; c <= blockcount; c++)
            {
                var cmd2 = new { command = "getblockhash", blockheight = c };
                var results = SendCommand(server, cmd2);
                var hash = JsonConvert.DeserializeObject(results);
                Console.WriteLine(hash);

                var cmd3 = new { command = "getblock", blockhash = hash };
                var receivedBlockData = SendCommand(server, cmd3);
                Console.WriteLine(receivedBlockData);

                var valid = Block.ValidateAndStoreBlock(receivedBlockData);
                Console.WriteLine(valid ? "Valid" : "Not valid");
            }
        }

        private static IPEndPoint ParseIPEndPoint(string endpoint, int defaultPort = 0)
        {
            Uri uri;
            var success = Uri.TryCreate(endpoint, UriKind.Absolute, out uri);
            if (success)
                if (uri.Host.Length < 1)
                    success = false;
            if (!success)
                success = Uri.TryCreate(String.Concat("tcp://", endpoint), UriKind.Absolute, out uri);
            if (!success)
                success = Uri.TryCreate(String.Concat("tcp://", String.Concat("[", endpoint, "]")), UriKind.Absolute, out uri);
            var host = uri.Host;
            var address = Dns.GetHostAddresses(host)[0];
            var port = uri.Port;
            if (port <= IPEndPoint.MinPort)
                port = defaultPort;
            if (success)
                return new IPEndPoint(address, port);
            throw new FormatException("Unable to obtain host and port from [" + endpoint + "]");
        }
    }
}
