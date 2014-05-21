using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using MsgPack;

namespace TestMsgPackCreator
{
    class Program
    {
        static void Main(string[] args)
        {
            if (args.Length < 3)
            {
                Usage();
                return;
            }
            var filename = args[0];
            int nodeSize;
            if (!int.TryParse(args[1], out nodeSize))
            {
                Console.WriteLine("Error: Can't parse [NodeSize]");
                Usage();
                return;
            }
            int nodeCount;
            if (!int.TryParse(args[2], out nodeCount))
            {
                Console.WriteLine("Error: Can't parse [NodeCount]");
                Usage();
                return;
            }

            Console.WriteLine("FileName: " + filename);
            Console.WriteLine("NodeSize: " + nodeSize);
            Console.WriteLine("NodeCount: " + nodeCount);
            Console.WriteLine("Packing...");
            CreateMessagePack(filename, nodeSize, nodeCount);
            Console.WriteLine("Packed!");

            Console.ReadLine();
        }

        static void Usage()
        {
            Console.WriteLine(System.IO.Path.GetFileName(System.Reflection.Assembly.GetExecutingAssembly().Location
                + " [FileName] [NodeSize] [NodeCount]"));
            Console.WriteLine("FileName: Output file");
            Console.WriteLine("NodeSize: Size per node (bytes)");
            Console.WriteLine("NodeCount:Number of nodes");
        }

        static void CreateMessagePack(string filename, int nodeSize, int nodeCount)
        {
            using (var st = System.IO.File.Open(filename, System.IO.FileMode.Create))
            {
                var packer = Packer.Create(st);
                packer.PackArrayHeader(nodeCount);
                foreach (var b in GetRandomByteArrayEnumerable(nodeSize).Take(nodeCount))
                {
                    packer.PackRawHeader(nodeSize);
                    packer.PackRawBody(b);
                }
            }
        }

        static IEnumerable<byte[]> GetRandomByteArrayEnumerable(int length)
        {
            var rand = new Random();
            var arr = new byte[length];
            while (true)
            {
                rand.NextBytes(arr);
                yield return arr;
            }
        }
    }
}
