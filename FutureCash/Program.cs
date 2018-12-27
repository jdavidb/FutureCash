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
        class Block
        {
            public long nonce;

            public string Serialize()
            {
                return JsonConvert.SerializeObject(this);
            }

            public override string ToString()
            {
                return Serialize();
            }

            public byte[] Mine(Hasher hasher, Int256 difficulty)
            {
                nonce = 0;
                var buffer = Encoding.UTF8.GetBytes(Serialize());
                var hash = hasher.Sha256d(buffer);
                var hash256 = ExtendedBitConverter.ToInt256(hash);
                while (hash256 > difficulty)
                {
                    nonce++;
                    buffer = Encoding.UTF8.GetBytes(Serialize());
                    hash = hasher.Sha256d(buffer);
                    hash256 = ExtendedBitConverter.ToInt256(hash);
                }

                return hash;
            }
        }

        class Hasher : IDisposable
        {
            private SHA256 sha256 = SHA256.Create();

            protected virtual void Dispose(bool disposing)
            {
                if (disposing)
                {
                    sha256.Dispose();
                }
            }

            public void Dispose()
            {
                Dispose(true);
            }

            public byte[] Sha256d(byte[] buffer)
            {
                return sha256.ComputeHash(sha256.ComputeHash(buffer));
            }
        }

        static void Main(string[] args)
        {
            var block = new Block();
            using (var hasher = new Hasher())
            {
                var hash = block.Mine(hasher, new Int256(Int128.MaxValue));
                Console.WriteLine(block.nonce);
                Console.WriteLine(BitConverter.ToString(hash));
            }
            Console.ReadKey();
        }
    }
}
