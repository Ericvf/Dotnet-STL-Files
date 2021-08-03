using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection.PortableExecutable;
using System.Runtime.InteropServices;
using System.Text;
using static System.Net.WebRequestMethods;

namespace ConsoleApp1
{
    /// <summary>
    /// https://www.loc.gov/preservation/digital/formats/fdd/fdd000504.shtml
    /// http://formats.kaitai.io/stl/index.html
    /// </summary>
    public class StlFile
    {
        const int HEADER_SIZE = 84;
        const int JUNK_SIZE = 80;
        const int SIZE_OF_FACET = 50;

        public struct Vertex
        {
            public float X, Y, Z;

            public override string ToString() => $"X:{X}, Y:{Y}, Z:{Z}";

            public override int GetHashCode() => HashCode.Combine(X, Y, Z);
        }

        public struct Facet
        {
            public Vertex normal;
            public Vertex v1, v2, v3;
        };

        private readonly FileStream fileStream;
        private string FileName { get; }

        public Vertex[] Vertices { get; set; }

        public Facet[] Facets { get; set; }

        public StlFile(FileStream fileStream, string fileName)
        {
            this.fileStream = fileStream;
            this.FileName = fileName;

            bool isLoaded = ReadBinary(fileStream);
            if (!isLoaded)
            {
                fileStream.Position = 0;
                isLoaded = ReadAscii(fileStream);
            }
        }

        private bool ReadBinary(FileStream fileStream)
        {
            var reader = new BinaryReader(fileStream);

            var fileContentSize = reader.BaseStream.Length - HEADER_SIZE;
            var fileSize = reader.BaseStream.Length;

            if (fileContentSize % SIZE_OF_FACET != 0)
            {
                return false;
            }

            for (var i = 0; i < JUNK_SIZE; i++)
            {
                fileStream.ReadByte();
            }

            var numFacets = fileContentSize / SIZE_OF_FACET;
            var headerNumFacets = reader.ReadUInt32();
            if (numFacets != headerNumFacets)
            { 
                return false;
            }

            var facets = new List<Facet>();
            var vertices = new List<Vertex>();

            while (numFacets-- > 0)
            {
                Facet facet = default;

                facet.normal = ReadBinaryVertex(reader);
                facet.v1 = ReadBinaryVertex(reader);
                facet.v2 = ReadBinaryVertex(reader);
                facet.v3 = ReadBinaryVertex(reader);

                vertices.Add(facet.v1);
                vertices.Add(facet.v2);
                vertices.Add(facet.v3);

                reader.ReadUInt16();

                facets.Add(facet);
            }

            this.Facets = facets.ToArray();
            this.Vertices = vertices.ToArray();

            return true;
        }

        private Vertex ReadBinaryVertex(BinaryReader reader)
        {
            Vertex vertex = default;
            vertex.X = ReadBinaryFloat(reader);
            vertex.Y = ReadBinaryFloat(reader);
            vertex.Z = ReadBinaryFloat(reader);
            return vertex;
        }

        private float ReadBinaryFloat(BinaryReader reader)
        {
            StlFloat value = default;
            value.intValue = reader.ReadByte();
            value.intValue |= reader.ReadByte() << 0x08;
            value.intValue |= reader.ReadByte() << 0x10;
            value.intValue |= reader.ReadByte() << 0x18;
            return value.floatValue;
        }

        private bool ReadAscii(FileStream fileStream)
        {
            var sr = new StreamReader(fileStream);
            sr.ReadLine(); // solid 

            var facets = new List<Facet>();
            var vertices = new List<Vertex>();

            while (!sr.EndOfStream)
            {
                Facet facet = default;

                var line = sr.ReadLine(); // facet normal

                if (line.StartsWith("endsolid"))
                    break;

                facet.normal = ReadAsciiVertex(line, 2);

                line = sr.ReadLine(); // outer loop
                line = sr.ReadLine(); // vertex
                facet.v1 = ReadAsciiVertex(line);

                line = sr.ReadLine(); // vertex
                facet.v2 = ReadAsciiVertex(line);

                line = sr.ReadLine(); // vertex
                facet.v3 = ReadAsciiVertex(line);

                line = sr.ReadLine(); // endloop
                line = sr.ReadLine(); // endfacet

                vertices.Add(facet.v1);
                vertices.Add(facet.v2);
                vertices.Add(facet.v3);
                facets.Add(facet);
            }

            this.Facets = facets.ToArray();
            this.Vertices = vertices.ToArray();

            return true;

        }

        private string ReadAsciiWord(StreamReader sr)
        {
            while (sr.Peek() >= 0)
            {
                var c = (char)sr.Peek();
                if (!isWhiteSpace(c))
                    break;
                sr.Read();
            }

            var word = string.Empty;

            while (!sr.EndOfStream)
            {
                var c = (char)sr.Read();
                if (isWhiteSpace(c))
                    break;
                word += c;
            }

            return word;

            bool isWhiteSpace(char c) => c.Equals(' ') || c.Equals('\t') || c.Equals('\n') || c.Equals('\r');
        }

        private Vertex ReadAsciiVertex(string line, int skip = 1)
        {
            using var lineReader = new StreamReader(new MemoryStream(Encoding.UTF8.GetBytes(line)));

            while (skip-- > 0)
            { 
                ReadAsciiWord(lineReader);
            }

            return new Vertex()
            {
                X = Convert.ToSingle(ReadAsciiWord(lineReader)),
                Y = Convert.ToSingle(ReadAsciiWord(lineReader)),
                Z = Convert.ToSingle(ReadAsciiWord(lineReader)),
            };
        }

        public void WriteBinary(FileStream outputFileStream)
        {
            var binaryWriter = new BinaryWriter(outputFileStream);

            var headerBytes = Encoding.UTF8.GetBytes("Appbyfex.DotnetSTLFiles");
            Array.Resize(ref headerBytes, JUNK_SIZE);
            binaryWriter.Write(headerBytes);

            binaryWriter.Write((uint)this.Facets.Length);

            var facetCount = Facets.Length;
            foreach (var facet in Facets)
            {
                WriteFacetBinary(binaryWriter, facet);

                    binaryWriter.Write((ushort)0);
            }
        }

        private void WriteFacetBinary(BinaryWriter binaryWriter, Facet facet)
        {
            WriteVertexBinary(binaryWriter, facet.normal);
            WriteVertexBinary(binaryWriter, facet.v1);
            WriteVertexBinary(binaryWriter, facet.v2);
            WriteVertexBinary(binaryWriter, facet.v3);
        }

        private static void WriteVertexBinary(BinaryWriter binaryWriter, Vertex vertex)
        {
            binaryWriter.Write(vertex.X);
            binaryWriter.Write(vertex.Y);
            binaryWriter.Write(vertex.Z);
        }

        [StructLayout(LayoutKind.Explicit)]
        public struct StlFloat
        {
            [FieldOffset(0)] public int intValue;
            [FieldOffset(0)] public float floatValue;
        }
    }
}