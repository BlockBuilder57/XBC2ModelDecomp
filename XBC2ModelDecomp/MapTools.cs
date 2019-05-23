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

            if (App.SaveRawFiles)
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

            Structs.XBC1[] MapInfoDatas = WISMDA.FilesBySearch("bina_basefix.temp_wi").OrderByDescending(x => x.FileSize).ToArray();
            Structs.MXMD[] MapMXMDs = new Structs.MXMD[MapInfoDatas.Length];
            Structs.MapInfo[] MapInfos = new Structs.MapInfo[MapInfoDatas.Length];
            for (int i = 0; i < MapInfoDatas.Length; i++)
            {
                MapInfos[i] = ft.ReadMapInfo(MapInfoDatas[i].Data, new BinaryReader(MapInfoDatas[i].Data));

                MapMXMDs[i] = new Structs.MXMD { Version = 0x69420 };
                MapMXMDs[i].ModelStruct.Meshes = new Structs.MXMDMeshes[MapInfos[i].MeshTables.Length];
                for (int j = 0; j < MapInfos[i].MeshTables.Length; j++)
                {

                    //ADD MATERIALS PLEASE
                    MapMXMDs[i].ModelStruct.MeshesCount = MapInfos[i].MeshTableDataCount;
                    MapMXMDs[i].ModelStruct.Meshes[j].TableCount = MapInfos[i].MeshTables[j].MeshCount;
                    MapMXMDs[i].ModelStruct.Meshes[j].Descriptors = MapInfos[i].MeshTables[j].Descriptors;
                }
            }

            //these values are used:
            //MXMD.ModelStruct.Nodes - all values
            //MXMD.ModelStruct.Meshes.Meshes - all values
            //MXMD.ModelStruct.Meshes.TableCount
            //MXMD.Materials - all values
            //MXMD.ModelStruct.MorphControls.Controls - for flexes on props

            App.PushLog(Structs.ReflectToString(MapInfos[0]));
            App.PushLog(Structs.ReflectToString(MapInfos[1]));

            Structs.XBC1[] MapMeshes = WISMDA.FilesBySearch("basemap/poli//");
            Structs.MXMD EmptyMXMD = new Structs.MXMD { Version = Int32.MaxValue };
            Structs.SKEL EmptySKEL = new Structs.SKEL { Unknown1 = Int32.MaxValue };
            for (int i = 0; i < MapMeshes.Length; i++)
            {
                MemoryStream model = MapMeshes[i].Data;
                Structs.Mesh mesh = ft.ReadMesh(model, new BinaryReader(model));

                

                ft.ModelToASCII(mesh, MapMXMDs[1], EmptySKEL, $"mesh{i}");
            }

            App.PushLog("Done!");
        }
    }
}
