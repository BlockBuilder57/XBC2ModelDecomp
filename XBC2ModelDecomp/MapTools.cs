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
            bool WasXNALara = false;
            App.PushLog("Extracting large maps can take a decent amount of memory as the program decompresses/handles data.");
            if (App.ExportTextures)
                App.PushLog("In addition, exporting textures can sometimes take even more memory (1-3GB), especially for larger maps.");
            if (App.ExportFormat == Structs.ExportFormat.XNALara)
            {
                WasXNALara = true;
                App.ExportFormat = Structs.ExportFormat.glTF;
                App.PushLog("While XNALara *works*, glTF retains object position, rotation, and scale; so the output type has been switched to glTF.");
            }

            FileStream fsWINVHE = new FileStream(App.CurFilePathAndName + ".winvhe", FileMode.Open, FileAccess.Read);
            BinaryReader brWINVHE = new BinaryReader(fsWINVHE);

            Structs.NVMS NVMS = ft.ReadNVMS(fsWINVHE, brWINVHE);

            FileStream fsWINVDA = new FileStream(App.CurFilePathAndName + ".winvda", FileMode.Open, FileAccess.Read);
            BinaryReader brWINVDA = new BinaryReader(fsWINVDA);

            Structs.NVDA NVDA = new Structs.NVDA { Version = brWINVHE.ReadInt32() };

            NVDA.XBC1s = new Structs.XBC1[NVMS.NVDATableCount];
            for (int i = 0; i < NVMS.NVDATableCount; i++)
                NVDA.XBC1s[i] = ft.ReadXBC1(fsWINVDA, brWINVDA, NVMS.NVDAPointers[i].XBC1Offset);
            if (App.ExportFormat == Structs.ExportFormat.RawFiles)
                DumpXBC1s(fsWINVDA, NVDA.XBC1s);

            List<int> magicOccurences = new List<int>();

            FileStream fsWISMDA = new FileStream(App.CurFilePathAndName + ".wismda", FileMode.Open, FileAccess.Read);
            BinaryReader brWISMDA = new BinaryReader(fsWISMDA);

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
                if (!Directory.Exists(App.CurOutputPath))
                    Directory.CreateDirectory(App.CurOutputPath);

            Structs.WISMDA WISMDA = new Structs.WISMDA
            {
                Data = fsWISMDA,
                Files = new Structs.XBC1[magicOccurences.Count]
            };
            
            for (int i = 0; i < magicOccurences.Count; i++)
                WISMDA.Files[i] = ft.ReadXBC1(fsWISMDA, brWISMDA, magicOccurences[i]);
            if (App.ExportFormat == Structs.ExportFormat.RawFiles)
                DumpXBC1s(WISMDA.Data, WISMDA.Files);

            App.PushLog("Finished reading .wismda...");

            if (App.ExportFormat != Structs.ExportFormat.RawFiles)
            {
                if (App.ExportTextures)
                    SaveMapTextures(WISMDA, $@"{App.CurOutputPath}\Textures");

                SaveMapMeshes(WISMDA);
                SaveMapProps(WISMDA);
            }

            App.PushLog("Done!");
            if (WasXNALara)
                App.PushLog("Remember, the output type was switched to glTF as it better supports maps.");
        }

        public void SaveMapMeshes(Structs.WISMDA WISMDA)
        {
            Structs.XBC1[] MapInfoDatas = WISMDA.FilesBySearch("bina_basefix.temp_wi");
            Structs.MXMD[] MapMXMDs = new Structs.MXMD[MapInfoDatas.Length];
            Structs.MapInfo[] MapInfos = new Structs.MapInfo[MapInfoDatas.Length];
            for (int i = 0; i < MapInfoDatas.Length; i++)
            {
                MapInfos[i] = ft.ReadMapInfo(MapInfoDatas[i].Data, new BinaryReader(MapInfoDatas[i].Data), false);
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
                    App.PushLog("MapInfo:" + Structs.ReflectToString(map, 1, 180));

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
                    case Structs.ExportFormat.glTF:
                        ft.ModelToGLTF(MapMeshes, MapMXMDs[i], new Structs.SKEL { Unknown1 = Int32.MaxValue }, MapInfos[i]);
                        break;
                }
            }
        }

        public void SaveMapProps(Structs.WISMDA WISMDA)
        {
            Structs.XBC1[] MapInfoDatas = WISMDA.FilesBySearch("seamwork/inst/out");
            Structs.XBC1[] MapMeshDatas = WISMDA.FilesBySearch("seamwork/inst/mdl");
            Structs.XBC1[] MapPosDatas = WISMDA.FilesBySearch("seamwork/inst/pos");
            Structs.Mesh[] MapMeshes = new Structs.Mesh[MapMeshDatas.Length];
            List<Structs.MXMD> MapMXMDs = new List<Structs.MXMD>();
            Structs.SeamworkPropPosition[] MapPositions = new Structs.SeamworkPropPosition[MapPosDatas.Length];
            Structs.MapInfo[] MapInfos = new Structs.MapInfo[MapInfoDatas.Length];
            Dictionary<Structs.MXMD, Structs.MapInfo> MXMDToMapInfo = new Dictionary<Structs.MXMD, Structs.MapInfo>();

            int MapPositionsIndex = 0;
            for (int i = 0; i < MapPosDatas.Length; i++)
                MapPositions[i] = ft.ReadPropPositions(MapPosDatas[i].Data, new BinaryReader(MapPosDatas[i].Data));

            for (int i = 0; i < MapInfoDatas.Length; i++)
            {
                MapInfos[i] = ft.ReadMapInfo(MapInfoDatas[i].Data, new BinaryReader(MapInfoDatas[i].Data), true);

                for (int j = 0; j < MapMeshDatas.Length; j++)
                    if (MapMeshes[j].VertexTableOffset == 0)
                        MapMeshes[j] = ft.ReadMesh(MapMeshDatas[j].Data, new BinaryReader(MapMeshDatas[j].Data));

                if (MapInfos[i].PropPosTableCount == 1 && MapInfos[i].PropPositions[0].PropID != 0)
                {
                    MapInfos[i].PropPositions.AddRange(MapPositions[MapPositionsIndex].Positions);
                    MapInfos[i].PropPosTableCount = MapInfos[i].PropPositions.Count;
                    MapPositionsIndex++;
                }

                //base things off prop position table
                //the table has the prop ids I need
                //duplicate ids mean duplicate meshes
                //get the highest LOD available
                //scrub through the propid table and only take unique values, dictionary this by index
                //take the dictionary and get the same indexes out of meshtables
                //loop through each prop position and build the MXMD based off those + artificial prop table
                //keep in mind structs are just memory values so I can duplicate things easily

                Dictionary<int, int> UniqueIDIndex = new Dictionary<int, int>();

                for (int j = 0; j < MapInfos[i].PropIDs.Count; j++)
                    if (!UniqueIDIndex.ContainsKey(MapInfos[i].PropIDs[j]))
                        UniqueIDIndex.Add(MapInfos[i].PropIDs[j], j);

                Structs.MapInfoMeshTable[] MeshTables = new Structs.MapInfoMeshTable[UniqueIDIndex.Count];
                int[] MeshLookup = new int[UniqueIDIndex.Count];
                for (int j = 0; j < UniqueIDIndex.Count; j++)
                {
                    MeshTables[j] = MapInfos[i].MeshTables[UniqueIDIndex.Values.ElementAt(j)];
                    MeshLookup[j] = MapInfos[i].PropFileLookup[UniqueIDIndex.Values.ElementAt(j)];
                }
                MapInfos[i].PropFileLookup = MeshLookup;

                for (int j = 0; j < (MapInfos[i].PropPosTableCount / App.PropSplitCount) + 1; j++)
                {
                    int MeshCount = j + 1 == (MapInfos[i].PropPosTableCount / App.PropSplitCount) + 1 ? MapInfos[i].PropPosTableCount % App.PropSplitCount : App.PropSplitCount;
                    Structs.MXMD FakeMXMD = new Structs.MXMD { Version = 0xFF };
                    FakeMXMD.Materials = MapInfos[i].Materials;
                    FakeMXMD.ModelStruct.MeshesCount = MeshCount;
                    FakeMXMD.ModelStruct.Meshes = new Structs.MXMDMeshes[MeshCount];

                    for (int k = 0; k < MeshCount; k++)
                    {
                        Structs.MapInfoPropPosition PropPosition = MapInfos[i].PropPositions[k + (j * App.PropSplitCount)];

                        FakeMXMD.ModelStruct.Meshes[k].Unknown1 = k + (j * App.PropSplitCount);
                        FakeMXMD.ModelStruct.Meshes[k].TableCount = MeshTables[PropPosition.PropID].MeshCount;
                        FakeMXMD.ModelStruct.Meshes[k].Descriptors = MeshTables[PropPosition.PropID].Descriptors;
                    }

                    MapMXMDs.Add(FakeMXMD);
                    MXMDToMapInfo.Add(FakeMXMD, MapInfos[i]);
                }
            }

            if (App.ShowInfo)
                foreach (Structs.MapInfo map in MapInfos)
                    App.PushLog("PropInfo:" + Structs.ReflectToString(map, 1, 180));

            for (int i = 0; i < MXMDToMapInfo.Count; i++)
            {
                switch (App.ExportFormat)
                {
                    case Structs.ExportFormat.XNALara:
                        ft.ModelToASCII(MapMeshes, MXMDToMapInfo.Keys.ElementAt(i), new Structs.SKEL { Unknown1 = Int32.MaxValue }, MXMDToMapInfo.Values.ElementAt(i), $"props{i}x{MXMDToMapInfo.Keys.ElementAt(i).ModelStruct.MeshesCount}");
                        break;
                    case Structs.ExportFormat.glTF:
                        ft.ModelToGLTF(MapMeshes, MXMDToMapInfo.Keys.ElementAt(i), new Structs.SKEL { Unknown1 = Int32.MaxValue }, MXMDToMapInfo.Values.ElementAt(i), $"props{i}x{MXMDToMapInfo.Keys.ElementAt(i).ModelStruct.MeshesCount}");
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
                    lbim.Filename = xbc1.Name.Split('/').LastOrDefault();
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

        public void DumpXBC1s(Stream sData, Structs.XBC1[] XBC1s)
        {
            List<string> filenames = new List<string>();

            App.PushLog($"Saving {XBC1s.Length} file(s) to disk...");
            for (int i = 0; i < XBC1s.Length; i++)
            {
                using (MemoryStream XBC1Stream = FormatTools.ReadZlib(sData, XBC1s[i].OffsetInFile + 0x30, XBC1s[i].FileSize, XBC1s[i].CompressedSize))
                {
                    string fileName = XBC1s[i].Name.Split('/').Last();
                    int dupeCount = filenames.Where(x => x == XBC1s[i].Name).Count();
                    string saveName = $"{XBC1s[i].Name}{(string.IsNullOrWhiteSpace(fileName) ? "NOFILENAME" : "")}{(dupeCount > 0 ? $"-{dupeCount}" : "")}";

                    ft.SaveStreamToFile(XBC1Stream, saveName, App.CurOutputPath + @"\RawFiles\");
                    if (App.ShowInfo)
                        App.PushLog($"Saved {saveName} to disk...");
                    filenames.Add(XBC1s[i].Name);
                }
            }
        }
    }
}
