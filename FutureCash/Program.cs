using BigMath;
using BigMath.Utils;
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
        class Hash
        {
            private static SHA256 sha256 = SHA256.Create();

            public static Int256 Sha256d(byte[] buffer)
            {
                return ExtendedBitConverter.ToInt256(sha256.ComputeHash(sha256.ComputeHash(buffer)));
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

            public Int256 BlockHash
            {
                get
                {
                    var buffer = Encoding.UTF8.GetBytes(Serialize());
                    return Hash.Sha256d(buffer);
                }
            }

            public void Mine(Int256 maxHash, long startingNonce = 0)
            {
                Console.WriteLine("MaxHash: " + maxHash);
                Nonce = startingNonce;
                while (BlockHash > maxHash)
                {
                    Console.WriteLine("Nonce: " + Nonce);
                    Console.WriteLine("BlockHash: " + BlockHash);
                    Nonce++;
                }
                Console.WriteLine("Nonce: " + Nonce);
                Console.WriteLine("BlockHash: " + BlockHash);
            }
        }

        static void Main(string[] args)
        {
            var block = new Block();
            block.Mine(Int64.MaxValue);
            Console.WriteLine(block.Nonce);
            Console.WriteLine(block.BlockHash);
            Console.ReadKey();
        }
    }
}
