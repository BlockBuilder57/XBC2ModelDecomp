using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using zlib;

namespace XBC2ModelDecomp
{
    public class MapTools
    {
        FormatTools ft = new FormatTools();

        public MapTools()
        {
            List<int> magicOccurences = new List<int>();

            FileStream fileStream = new FileStream(App.CurFileName.Remove(App.CurFileName.LastIndexOf('.')) + ".wismda", FileMode.Open, FileAccess.Read);
            BinaryReader binaryReader = new BinaryReader(fileStream);

            byte[] ByteBuffer = File.ReadAllBytes(App.CurFileName.Remove(App.CurFileName.LastIndexOf('.')) + ".wismda");
            byte[] SearchBytes = Encoding.ASCII.GetBytes("xbc1");
            for (int i = 0; i <= (ByteBuffer.Length - SearchBytes.Length); i++)
            {
                if (ByteBuffer[i] == SearchBytes[0])
                {
                    for (int j = 1; j < SearchBytes.Length && ByteBuffer[i + j] == SearchBytes[j]; j++)
                    {
                        if (j == SearchBytes.Length-1)
                        {
                            Console.WriteLine($"String was found at offset {i}");
                            magicOccurences.Add(i);
                            i += BitConverter.ToInt32(ByteBuffer, i + 12);
                        }
                    }
                }
            }

            for (int i = 0; i < magicOccurences.Count; i++)
            {
                MemoryStream ms = ft.XBC1(fileStream, binaryReader, magicOccurences[i], $"{Path.GetFileNameWithoutExtension(App.CurFileName)}_file{i}.bin", App.CurOutputPath + $"{Path.GetFileNameWithoutExtension(App.CurFileName)}_xbc1files");
                ms.Dispose();
            }
        }
    }
}
