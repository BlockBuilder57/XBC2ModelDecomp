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
            App.PushLog("Extracting large maps can take a decent amount of memory as the program decompresses them.");
            if (App.ExportTextures)
                App.PushLog("In addition, exporting textures can sometimes take even more memory (1-3GB), especially for larger maps.");

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
                            magicOccurences.Add(i);
                            i += BitConverter.ToInt32(ByteBuffer, i + 12);
                        }
                    }
                }
            }
            ByteBuffer = new byte[0];

            if (magicOccurences.Count > 0)
            {
                if (!Directory.Exists(App.CurOutputPath))
                    Directory.CreateDirectory(App.CurOutputPath);
            }

            Structs.WISMDA WISMDA = new Structs.WISMDA
            {
                Data = fileStream,
                Files = new Structs.XBC1[magicOccurences.Count]
            };

            List<string> filenames = new List<string>();

            if (App.ExportFormat == Structs.ExportFormat.RawFiles)
                App.PushLog($"Saving {magicOccurences.Count} file(s) to disk...");
            for (int i = 0; i < magicOccurences.Count; i++)
            {
                WISMDA.Files[i] = ft.ReadXBC1(fileStream, binaryReader, magicOccurences[i]);

                if (App.ExportFormat == Structs.ExportFormat.RawFiles)
                {
                    using (MemoryStream XBC1Stream = FormatTools.ReadZlib(fileStream, WISMDA.Files[i].OffsetInFile + 0x30, WISMDA.Files[i].FileSize, WISMDA.Files[i].CompressedSize))
                    {
                        string fileName = WISMDA.Files[i].Name.Split('/').Last();
                        int dupeCount = filenames.Where(x => x == WISMDA.Files[i].Name).Count();
                        string saveName = $"{WISMDA.Files[i].Name}{(string.IsNullOrWhiteSpace(fileName) ? "NOFILENAME" : "")}{(dupeCount > 0 ? $"-{dupeCount}" : "")}";

                        ft.SaveStreamToFile(XBC1Stream, saveName, App.CurOutputPath + @"\RawFiles\");
                        if (App.ShowInfo)
                            App.PushLog($"Saved {saveName} to disk...");
                        filenames.Add(WISMDA.Files[i].Name);
                    }

                    App.PushLog("Done!");
                }
            }

            if (App.ExportTextures)
                SaveMapTextures(WISMDA, $@"{App.CurOutputPath}\Textures");

            SaveMapMeshes(WISMDA);
            SaveMapProps(WISMDA);

            App.PushLog("Done!");
        }

        public void SaveMapMeshes(Structs.WISMDA WISMDA)
        {
            Structs.XBC1[] MapInfoDatas = WISMDA.FilesBySearch("bina_basefix.temp_wi");
            Structs.MXMD[] MapMXMDs = new Structs.MXMD[MapInfoDatas.Length];
            Structs.MapInfo[] MapInfos = new Structs.MapInfo[MapInfoDatas.Length];
            for (int i = 0; i < MapInfoDatas.Length; i++)
            {
                MapInfos[i] = ft.ReadMapInfo(MapInfoDatas[i].Data, new BinaryReader(MapInfoDatas[i].Data));
                for (int j = 0; j < MapInfos[i].MeshFileLookup.Length; j++)
                    if (i != 0)
                        MapInfos[i].MeshFileLookup[j] += (short)(MapInfos[i - 1].MeshFileLookup.Max() + 1);

                MapMXMDs[i] = new Structs.MXMD { Version = 0xFF };
                MapMXMDs[i].ModelStruct.Meshes = new Structs.MXMDMeshes[MapInfos[i].MeshTables.Length];
                for (int j = 0; j < MapInfos[i].MeshTables.Length; j++)
                {
                    MapMXMDs[i].Materials = MapInfos[i].Materials;
                    MapMXMDs[i].ModelStruct.MeshesCount = MapInfos[i].MeshTableDataCount;
                    MapMXMDs[i].ModelStruct.Meshes[j].TableCount = MapInfos[i].MeshTables[j].MeshCount;
                    MapMXMDs[i].ModelStruct.Meshes[j].Descriptors = MapInfos[i].MeshTables[j].Descriptors;
                }
            }

            if (App.ShowInfo)
                foreach (Structs.MapInfo map in MapInfos)
                    App.PushLog("MapInfo:\n" + Structs.ReflectToString(map, 1, 180));

            Structs.XBC1[] MapMeshDatas = WISMDA.FilesBySearch("basemap/poli//");
            Structs.Mesh[] MapMeshes = new Structs.Mesh[MapMeshDatas.Length];
            for (int i = 0; i < MapMeshes.Length; i++)
            {
                MemoryStream model = MapMeshDatas[i].Data;
                MapMeshes[i] = ft.ReadMesh(model, new BinaryReader(model));
            }

            for (int i = 0; i < MapInfos.Length; i++)
            {
                switch (App.ExportFormat)
                {
                    case Structs.ExportFormat.XNALara:
                        ft.ModelToASCII(MapMeshes, MapMXMDs[i], new Structs.SKEL { Unknown1 = Int32.MaxValue }, MapInfos[i]);
                        break;
                }
            }
        }

        public void SaveMapProps(Structs.WISMDA WISMDA)
        {
            Structs.XBC1[] MapInfoDatas = WISMDA.FilesBySearch("seamwork/inst/out");
            Structs.XBC1[] MapMeshDatas = WISMDA.FilesBySearch("seamwork/inst/mdl");
            Structs.Mesh[] MapMeshes = new Structs.Mesh[MapMeshDatas.Length];
            Structs.MXMD[] MapMXMDs = new Structs.MXMD[MapInfoDatas.Length];
            Structs.MapInfo[] MapInfos = new Structs.MapInfo[MapInfoDatas.Length];
            for (int i = 0; i < MapInfoDatas.Length; i++)
            {
                MapInfos[i] = ft.ReadMapInfo(MapInfoDatas[i].Data, new BinaryReader(MapInfoDatas[i].Data));

                MapMXMDs[i] = new Structs.MXMD { Version = 0xFF };
                MapMXMDs[i].ModelStruct.Meshes = new Structs.MXMDMeshes[MapInfos[i].MeshTables.Length];
                for (int j = 0; j < MapInfos[i].MeshTables.Length; j++)
                {
                    MapMXMDs[i].Materials = MapInfos[i].Materials;
                    MapMXMDs[i].ModelStruct.MeshesCount = MapInfos[i].MeshTableDataCount;
                    MapMXMDs[i].ModelStruct.Meshes[j].TableCount = MapInfos[i].MeshTables[j].MeshCount;
                    MapMXMDs[i].ModelStruct.Meshes[j].Descriptors = MapInfos[i].MeshTables[j].Descriptors;

                    for (int k = 0; k < MapInfos[i].MeshTables[j].Descriptors.Length; k++)
                        MapInfos[i].MeshTables[j].Descriptors[k].LOD = (short)MapInfos[i].PropLODs[j][1];

                    if (MapMeshes[MapInfos[i].PropMeshToFile[j]].VertexTableOffset == 0 && App.PropPositions)
                    {
                        MemoryStream model = MapMeshDatas[MapInfos[i].PropMeshToFile[j]].Data;
                        //there's multiple positions per prop id, so how do those relate?
                        Structs.MapInfoPropPosition PropPosition = MapInfos[i].PropPositions.Where(x => x.PropID == MapInfos[i].PropLODs[j][0]).First();
                        MapMeshes[MapInfos[i].PropMeshToFile[j]] = ft.ReadMesh(model, new BinaryReader(model), PropPosition.Position);
                    }

                }
            }

            if (App.ShowInfo)
                foreach (Structs.MapInfo map in MapInfos)
                    App.PushLog("PropInfo:\n" + Structs.ReflectToString(map, 1, 180));

            for (int i = 0; i < MapInfos.Length; i++)
            {
                switch (App.ExportFormat)
                {
                    case Structs.ExportFormat.XNALara:
                        ft.ModelToASCII(MapMeshes, MapMXMDs[i], new Structs.SKEL { Unknown1 = Int32.MaxValue }, MapInfos[i]);
                        break;
                }
            }
        }

        public void SaveMapTextures(Structs.WISMDA WISMDA, string texturesFolderPath)
        {
            //"cache/" contains all base colors and normals
            //"seamwork/tecpac//" contains PBR materials but in severely disjointed fashion
            //"seamwork/texture//" contains PBR materials and some base color things, seems to be for props exclusively?

            List<Structs.LBIM> TextureLBIMs = new List<Structs.LBIM>();
            List<Structs.XBC1> CacheAndTecPac = WISMDA.FilesBySearch("cache/").ToList();
            CacheAndTecPac.AddRange(WISMDA.FilesBySearch("seamwork/tecpac"));
            foreach (Structs.XBC1 xbc1 in CacheAndTecPac)
            {
                BinaryReader brTexture = new BinaryReader(xbc1.Data);
                xbc1.Data.Seek(-0x4, SeekOrigin.End);
                if (brTexture.ReadInt32() == 0x4D49424C)
                {
                    Structs.LBIM lbim = ft.ReadLBIM(xbc1.Data, brTexture, 0, (int)xbc1.Data.Length);
                    if (lbim.Data != null && lbim.Width > 15 && lbim.Height > 15) //get rid of the tinies
                        TextureLBIMs.Add(lbim);
                }
            }

            ft.ReadTextures(new Structs.MSRD { Version = Int32.MaxValue }, texturesFolderPath + @"\CacheAndTecPac", TextureLBIMs);
            foreach (Structs.LBIM lbim in TextureLBIMs)
                lbim.Data.Dispose();

            TextureLBIMs.Clear();

            foreach (Structs.XBC1 xbc1 in WISMDA.FilesBySearch("seamwork/texture"))
            {
                BinaryReader brTexture = new BinaryReader(xbc1.Data);
                Structs.SeamworkTexture smwrkTexture = new Structs.SeamworkTexture
                {
                    TableCount = brTexture.ReadInt32(),
                    TableOffset = brTexture.ReadInt32()
                };

                smwrkTexture.Table = new Structs.SeamworkTextureTable[smwrkTexture.TableCount];
                xbc1.Data.Seek(smwrkTexture.TableOffset, SeekOrigin.Begin);
                for (int i = 0; i < smwrkTexture.TableCount; i++)
                {
                    smwrkTexture.Table[i] = new Structs.SeamworkTextureTable
                    {
                        Unknown1 = brTexture.ReadInt32(),
                        Size = brTexture.ReadInt32(),
                        Offset = brTexture.ReadInt32(),
                        Unknown2 = brTexture.ReadInt32()
                    };
                }
                foreach (Structs.SeamworkTextureTable table in smwrkTexture.Table)
                {
                    Structs.LBIM lbim = ft.ReadLBIM(xbc1.Data, brTexture, table.Offset, table.Size);

                    if (lbim.Data != null && lbim.Width > 15 && lbim.Height > 15) //get rid of the tinies
                        TextureLBIMs.Add(lbim);
                }
            }

            ft.ReadTextures(new Structs.MSRD { Version = Int32.MaxValue }, texturesFolderPath + @"\SeamworkTexture", TextureLBIMs);
            foreach (Structs.LBIM lbim in TextureLBIMs)
                lbim.Data.Dispose();

            TextureLBIMs.Clear();
        }
    }
}
