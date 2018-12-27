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
            var payload = "XXXXX";
            var buffer = Encoding.UTF8.GetBytes(payload);
            using (var hasher = new Hasher())
            {
                var hash = hasher.Sha256d(buffer);
                Console.WriteLine(BitConverter.ToString(hash));
            }
            Console.ReadKey();
        }
    }
}
