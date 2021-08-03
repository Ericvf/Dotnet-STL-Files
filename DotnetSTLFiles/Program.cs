using System;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;

namespace ConsoleApp1
{
    class Program
    {
        static async Task Main(string[] args)
        {
            var fileName = args[0];

            using var fileStream = File.Open(fileName, FileMode.Open, FileAccess.Read);
            var stlFile = new StlFile(fileStream, fileName);

            using (var outputFileStream = File.Open(fileName + "output.stl", FileMode.OpenOrCreate, FileAccess.Write))
            {
                stlFile.WriteBinary(outputFileStream);
            }

            var json3dFile = new Json3dFile
            {
                rootnode = new Json3dFile.Rootnode()
                {
                    name = fileName,
                    transformation = new int[] {}
                },
                meshes = new[] {
                    new Json3dFile.Mesh()
                    {
                        faces = stlFile.Facets
                            .Select((f, i) => new [] {
                                i * 3 + 0,
                                i * 3 + 1,
                                i * 3 + 2,
                            })
                            .ToArray(),

                        vertices = stlFile.Vertices
                            .Select(v => new [] {
                                v.X, v.Y, v.Z,
                            })
                            .SelectMany(_ => _)
                            .ToArray(),

                        normals = stlFile.Facets
                            .Select(f => new [] {
                                f.normal.X, f.normal.Y, f.normal.Z,
                                f.normal.X, f.normal.Y, f.normal.Z,
                                f.normal.X, f.normal.Y, f.normal.Z,
                            })
                            .SelectMany(_ => _)
                            .ToArray(),
                        }
                    },
            };

            var jsonString = JsonSerializer.Serialize(json3dFile);

            var outputFileName = fileName + ".json";

            await File.WriteAllTextAsync(outputFileName, jsonString);
        }
    }

    public class Json3dFile
    {
        public Rootnode rootnode { get; set; }

        public Mesh[] meshes { get; set; }

        public class Rootnode
        {
            public string name { get; set; }
            public int[] transformation { get; set; }
            public int[] meshes { get; set; }
        }

        public class Mesh
        {
            public string name { get; set; }
            public int materialindex { get; set; }
            public int primitivetypes { get; set; }
            public float[] vertices { get; set; }
            public float[] normals { get; set; }
            public int[][] faces { get; set; }
        }
    }
}
