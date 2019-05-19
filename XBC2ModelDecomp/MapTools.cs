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
            App.PushLog($"Extracting large maps can take a lot of memory! If you have less than 2GB to spare the program, the program may slow down as it enters swap memory.");

            List<int> magicOccurences = new List<int>();

            FileStream fileStream = new FileStream(App.CurFilePathAndName + ".wismda", FileMode.Open, FileAccess.Read);
            BinaryReader binaryReader = new BinaryReader(fileStream);

            //this thing can be replaced with data in the wismhd, but I can't figure out a consistent way to get the data
            //for ma01a it's at 0x340
            //the table looks like [Int32 offset, Int32 FileSize]
            //it doesn't seem to have every xbc1 though?
            byte[] ByteBuffer = File.ReadAllBytes(App.CurFilePathAndName + ".wismda");
            byte[] SearchBytes = Encoding.ASCII.GetBytes("xbc1");
            for (int i = 0; i <= (ByteBuffer.Length - SearchBytes.Length); i++)
            {
                if (ByteBuffer[i] == SearchBytes[0])
                {
                    for (int j = 1; j < SearchBytes.Length && ByteBuffer[i + j] == SearchBytes[j]; j++)
                    {
                        if (j == SearchBytes.Length - 1)
                        {
                            //Console.WriteLine($"String was found at offset {i}");
                            magicOccurences.Add(i);
                            i += BitConverter.ToInt32(ByteBuffer, i + 12);
                        }
                    }
                }
            }
            ByteBuffer = new byte[0];

            Structs.WISMDA WISMDA = new Structs.WISMDA
            {
                Files = new Structs.XBC1[magicOccurences.Count]
            };

            List<string> filenames = new List<string>();

            App.PushLog($"Saving {magicOccurences.Count} file(s) to disk...");
            for (int i = 0; i < magicOccurences.Count; i++)
            {
                WISMDA.Files[i] = ft.ReadXBC1(fileStream, binaryReader, magicOccurences[i]);

                if (App.SaveRawFiles)
                {
                    string fileName = WISMDA.Files[i].Name.Split('/').Last();
                    int dupeCount = filenames.Where(x => x == WISMDA.Files[i].Name).Count();
                    string saveName = $"{WISMDA.Files[i].Name}{(string.IsNullOrWhiteSpace(fileName) ? "NOFILENAME" : "")}{(dupeCount > 0 ? $"-{dupeCount}" : "")}";

                    ft.SaveStreamToFile(WISMDA.Files[i].Data, saveName, App.CurOutputPath + @"\RawFiles\");
                    if (App.ShowInfo)
                        App.PushLog($"Saved {saveName} to disk...");
                    filenames.Add(WISMDA.Files[i].Name);
                }
            }
            App.PushLog("Done!");
            fileStream.Dispose();

            Structs.MapInfo bina = ft.ReadMapInfo(WISMDA.Files[2].Data, new BinaryReader(WISMDA.Files[2].Data));
            Structs.Mesh mesh = ft.ReadMesh(WISMDA.Files[7].Data, new BinaryReader(WISMDA.Files[7].Data));

            App.PushLog(Structs.ReflectToString(bina));
            App.PushLog(mesh.ToString());

            ft.ModelToASCII(mesh, new Structs.MXMD { Version = Int32.MaxValue }, new Structs.SKEL { Unknown1 = Int32.MaxValue });
        }
    }
}
