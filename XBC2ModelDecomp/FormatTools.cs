using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Threading;
using zlib;

namespace XBC2ModelDecomp
{
    public class FormatTools
    {
        public static string ReadNullTerminatedString(BinaryReader br)
        {
            string text = "";
            byte b;
            while ((b = br.ReadByte()) > 0)
            {
                text += (char)b;
            }
            return text;
        }

        public MemoryStream XBC1(FileStream fileStream, BinaryReader binaryReader, int offset, string saveToFileName = "", string savetoFilePath = "")
        {
            if (fileStream == null || binaryReader == null || offset > fileStream.Length || offset < 0)
                return null;
            fileStream.Seek(offset, SeekOrigin.Begin);
            int XBC1Magic = binaryReader.ReadInt32(); //nice meme
            if (XBC1Magic != 0x31636278)
            {
                App.PushLog("XBC1 header invalid!");
                return null;
            }
            binaryReader.ReadInt32();
            int outputFileSize = binaryReader.ReadInt32();
            int compressedLength = binaryReader.ReadInt32();
            binaryReader.ReadInt32();

            //string fileInfo = ReadNullTerminatedString(binaryReader);

            fileStream.Seek(offset + 0x30, SeekOrigin.Begin);
            byte[] fileBuffer = new byte[outputFileSize >= compressedLength ? outputFileSize : compressedLength];

            MemoryStream msFile = new MemoryStream();
            fileStream.Read(fileBuffer, 0, compressedLength);

            ZOutputStream ZOutFile = new ZOutputStream(msFile);
            ZOutFile.Write(fileBuffer, 0, compressedLength);
            ZOutFile.Flush();

            if (App.SaveAllFiles && !string.IsNullOrWhiteSpace(saveToFileName))
            {
                if (!string.IsNullOrWhiteSpace(savetoFilePath) && !Directory.Exists(savetoFilePath))
                    Directory.CreateDirectory(savetoFilePath);
                FileStream outputter = new FileStream($@"{savetoFilePath}\{saveToFileName}", FileMode.OpenOrCreate);
                msFile.WriteTo(outputter);
                outputter.Flush();
                outputter.Close();
            }

            msFile.Seek(0L, SeekOrigin.Begin);
            return msFile;
        }

        public Structs.SAR1 ReadSAR1(FileStream fsSAR1, BinaryReader brSAR1)
        {
            App.PushLog("Parsing SAR1...");
            fsSAR1.Seek(0, SeekOrigin.Begin);
            int SAR1Magic = brSAR1.ReadInt32();
            if (SAR1Magic != 0x53415231)
            {
                App.PushLog("SAR1 is corrupt (or wrong endianness)!");
                return new Structs.SAR1 { Version = Int32.MaxValue };
            }

            Structs.SAR1 SAR1 = new Structs.SAR1
            {
                FileSize = brSAR1.ReadInt32(),
                Version = brSAR1.ReadInt32(),
                NumFiles = brSAR1.ReadInt32(),
                TOCOffset = brSAR1.ReadInt32(),
                DataOffset = brSAR1.ReadInt32(),
                Unknown1 = brSAR1.ReadInt32(),
                Unknown2 = brSAR1.ReadInt32(),
                Path = ReadNullTerminatedString(brSAR1)
            };

            string safePath = App.CurOutputPath + @"\RawFiles\";
            if (SAR1.Path[1] == ':')
                safePath += SAR1.Path.Substring(3);
            else
                safePath += SAR1.Path;

            if (App.SaveAllFiles && !Directory.Exists(safePath))
                Directory.CreateDirectory(safePath);

            SAR1.TOCItems = new Structs.SARTOC[SAR1.NumFiles];
            for (int i = 0; i < SAR1.NumFiles; i++)
            {
                fsSAR1.Seek(SAR1.TOCOffset + (i * 0x40), SeekOrigin.Begin);
                SAR1.TOCItems[i] = new Structs.SARTOC
                {
                    Offset = brSAR1.ReadInt32(),
                    Size = brSAR1.ReadInt32(),
                    Unknown1 = brSAR1.ReadInt32(),
                    Filename = ReadNullTerminatedString(brSAR1)
                };
            }

            SAR1.BCItems = new Structs.SARBC[SAR1.NumFiles];
            long nextPosition = SAR1.DataOffset;
            
            for (int i = 0; i < SAR1.NumFiles; i++)
            {
                fsSAR1.Seek(SAR1.TOCItems[i].Offset, SeekOrigin.Begin);
                int BCMagic = brSAR1.ReadInt32();
                if (BCMagic != 0x00004342)
                {
                    Console.WriteLine("BC is corrupt (or wrong endianness)!");
                    Console.ReadLine();
                    return new Structs.SAR1 { Version = Int32.MaxValue };
                }
                SAR1.BCItems[i] = new Structs.SARBC
                {
                    BlockCount = brSAR1.ReadInt32(),
                    FileSize = brSAR1.ReadInt32(),
                    PointerCount = brSAR1.ReadInt32(),
                    OffsetToData = brSAR1.ReadInt32(),
                };

                fsSAR1.Seek(SAR1.TOCItems[i].Offset + SAR1.BCItems[i].OffsetToData + 0x4, SeekOrigin.Begin);

                SAR1.BCItems[i].Data = new MemoryStream(SAR1.BCItems[i].FileSize - SAR1.BCItems[i].OffsetToData);
                fsSAR1.CopyTo(SAR1.BCItems[i].Data);

                if (App.SaveAllFiles)
                {
                    FileStream outputter = new FileStream($@"{safePath}\{SAR1.TOCItems[i].Filename}", FileMode.OpenOrCreate);
                    SAR1.BCItems[i].Data.WriteTo(outputter);
                    outputter.Flush();
                    outputter.Close();
                }

                nextPosition += SAR1.BCItems[i].FileSize;
            }

            return SAR1;
        }

        public Structs.SKEL ReadSKEL(Stream fsSKEL, BinaryReader brSKEL)
        {
            App.PushLog("Parsing SKEL...");
            fsSKEL.Seek(0, SeekOrigin.Begin);
            int SKELMagic = brSKEL.ReadInt32();
            if (SKELMagic != 0x4C454B53)
            {
                App.PushLog("SKEL is corrupt (or wrong endianness)!");
                return new Structs.SKEL { Parents = new short[0] };
            }

            Structs.SKEL SKEL = new Structs.SKEL
            {
                Unknown1 = brSKEL.ReadInt32(),
                Unknown2 = brSKEL.ReadInt32()
            };

            SKEL.TOCItems = new Structs.SKELTOC[9];
            for (int i = 0; i < SKEL.TOCItems.Length; i++)
            {
                SKEL.TOCItems[i] = new Structs.SKELTOC
                {
                    Offset = brSKEL.ReadInt32(),
                    Unknown1 = brSKEL.ReadInt32(),
                    Count = brSKEL.ReadInt32(),
                    Unknown2 = brSKEL.ReadInt32()
                };
            }

            SKEL.Parents = new short[SKEL.TOCItems[2].Count];
            fsSKEL.Seek(SKEL.TOCItems[2].Offset - 0x24, SeekOrigin.Begin);
            for (int i = 0; i < SKEL.Parents.Length; i++)
            {
                SKEL.Parents[i] = brSKEL.ReadInt16();
            }

            SKEL.Nodes = new Structs.SKELNodes[SKEL.TOCItems[3].Count];
            for (int i = 0; i < SKEL.Nodes.Length; i++)
            {
                fsSKEL.Seek(SKEL.TOCItems[3].Offset - 0x24 + (i * 0x10), SeekOrigin.Begin);
                SKEL.Nodes[i] = new Structs.SKELNodes
                {
                    Offset = brSKEL.ReadInt32(),
                    Unknown1 = brSKEL.ReadBytes(0xC)
                };
                fsSKEL.Seek(SKEL.Nodes[i].Offset - 0x24, SeekOrigin.Begin);
                SKEL.Nodes[i].Name = ReadNullTerminatedString(brSKEL);
            }

            SKEL.Transforms = new Structs.SKELTransforms[SKEL.TOCItems[4].Count];
            fsSKEL.Seek(SKEL.TOCItems[4].Offset - 0x24, SeekOrigin.Begin);
            for (int i = 0; i < SKEL.Transforms.Length; i++)
            {
                SKEL.Transforms[i] = new Structs.SKELTransforms
                {
                    Position = new Quaternion(brSKEL.ReadSingle(), brSKEL.ReadSingle(), brSKEL.ReadSingle(), brSKEL.ReadSingle()),
                    Rotation = new Quaternion(brSKEL.ReadSingle(), brSKEL.ReadSingle(), brSKEL.ReadSingle(), brSKEL.ReadSingle()),
                    Scale = new Quaternion(brSKEL.ReadSingle(), brSKEL.ReadSingle(), brSKEL.ReadSingle(), brSKEL.ReadSingle())
                };
            }

            return SKEL;
        }

        public Structs.MSRD ReadMSRD(FileStream fsMSRD, BinaryReader brMSRD)
        {
            App.PushLog("Parsing MSRD...");
            fsMSRD.Seek(0, SeekOrigin.Begin);
            int MSRDMagic = brMSRD.ReadInt32();
            if (MSRDMagic != 0x4D535244)
            {
                App.PushLog("MSRD is corrupt (or wrong endianness)!");
                return new Structs.MSRD { Version = Int32.MaxValue };
            }

            Structs.MSRD MSRD = new Structs.MSRD
            {
                Version = brMSRD.ReadInt32(),
                HeaderSize = brMSRD.ReadInt32(),
                MainOffset = brMSRD.ReadInt32(), //0xC 0x10, 16

                Tag = brMSRD.ReadInt32(),
                Revision = brMSRD.ReadInt32(),

                DataItemsCount = brMSRD.ReadInt32(), //0x18 0x10, 16
                DataItemsOffset = brMSRD.ReadInt32(), //0x1C 0x4C, 76
                FileCount = brMSRD.ReadInt32(), //0x20 0xE, 14
                TOCOffset = brMSRD.ReadInt32(), //0x24 0x18C, 396

                Unknown1 = brMSRD.ReadBytes(0x1C),

                TextureIdsCount = brMSRD.ReadInt32(), //0x44
                TextureIdsOffset = brMSRD.ReadInt32(), //0x48 0x234, 564
                TextureCountOffset = brMSRD.ReadInt32() //0x4C 0x24E, 590
            };
            
            if (MSRD.DataItemsOffset != 0)
            {
                MSRD.DataItems = new Structs.MSRDDataItem[MSRD.DataItemsCount];
                fsMSRD.Seek(MSRD.MainOffset + MSRD.DataItemsOffset, SeekOrigin.Begin); //0x5C, 92
                for (int i = 0; i < MSRD.DataItemsCount; i++)
                {
                    MSRD.DataItems[i] = new Structs.MSRDDataItem
                    {
                        Offset = brMSRD.ReadInt32(),
                        Size = brMSRD.ReadInt32(),
                        id1 = brMSRD.ReadInt16(),
                        Type = (Structs.MSRDDataItemTypes)brMSRD.ReadInt16()
                    };
                    fsMSRD.Seek(0x8, SeekOrigin.Current);
                }
            }
            
            if (MSRD.TextureIdsOffset != 0)
            {
                fsMSRD.Seek(MSRD.MainOffset + MSRD.TextureIdsOffset, SeekOrigin.Begin); //0x244, 580
                MSRD.TextureIds = new short[MSRD.TextureIdsCount];
                for (int curTextureId = 0; curTextureId < MSRD.TextureIdsCount; curTextureId++)
                {
                    MSRD.TextureIds[curTextureId] = brMSRD.ReadInt16();
                }
            }
            
            if (MSRD.TextureCountOffset != 0)
            {
                fsMSRD.Seek(MSRD.MainOffset + MSRD.TextureCountOffset, SeekOrigin.Begin); //0x25E, 606
                MSRD.TextureCount = brMSRD.ReadInt32(); //0x25E
                MSRD.TextureChunkSize = brMSRD.ReadInt32();
                MSRD.Unknown2 = brMSRD.ReadInt32();
                MSRD.TextureStringBufferOffset = brMSRD.ReadInt32();

                MSRD.TextureInfo = new Structs.MSRDTextureInfo[MSRD.TextureCount];
                for (int curTextureNameOffset = 0; curTextureNameOffset < MSRD.TextureCount; curTextureNameOffset++)
                {
                    MSRD.TextureInfo[curTextureNameOffset].Unknown1 = brMSRD.ReadInt32();
                    MSRD.TextureInfo[curTextureNameOffset].Size = brMSRD.ReadInt32();
                    MSRD.TextureInfo[curTextureNameOffset].Offset = brMSRD.ReadInt32();
                    MSRD.TextureInfo[curTextureNameOffset].StringOffset = brMSRD.ReadInt32();
                }

                MSRD.TextureNames = new string[MSRD.TextureCount];
                for (int curTextureName = 0; curTextureName < MSRD.TextureCount; curTextureName++)
                {
                    fsMSRD.Seek(MSRD.MainOffset + MSRD.TextureCountOffset + MSRD.TextureInfo[curTextureName].StringOffset, SeekOrigin.Begin);
                    MSRD.TextureNames[curTextureName] = FormatTools.ReadNullTerminatedString(brMSRD);
                }
            }

            MSRD.TOC = new Structs.TOC[MSRD.FileCount];
            for (int curFileOffset = 0; curFileOffset < MSRD.FileCount; curFileOffset++)
            {
                fsMSRD.Seek(MSRD.MainOffset + MSRD.TOCOffset + (curFileOffset * 12), SeekOrigin.Begin); //prevents errors I guess
                MSRD.TOC[curFileOffset].CompSize = brMSRD.ReadInt32();
                MSRD.TOC[curFileOffset].FileSize = brMSRD.ReadInt32();
                MSRD.TOC[curFileOffset].Offset = brMSRD.ReadInt32();

                App.PushLog($"Decompressing file{curFileOffset} in MSRD...");
                MSRD.TOC[curFileOffset].MemoryStream = XBC1(fsMSRD, brMSRD, MSRD.TOC[curFileOffset].Offset, $"file{curFileOffset}.bin", App.CurOutputPath + @"\RawFiles");
            }

            return MSRD;
        }

        public Structs.MXMD ReadMXMD(FileStream fsMXMD, BinaryReader brMXMD)
        {
            App.PushLog("Parsing MXMD...");
            fsMXMD.Seek(0, SeekOrigin.Begin);
            int MXMDMagic = brMXMD.ReadInt32();
            if (MXMDMagic != 0x4D584D44)
            {
                App.PushLog("MXMD is corrupt (or wrong endianness)!");
                return new Structs.MXMD { Version = Int32.MaxValue };
            }

            Structs.MXMD MXMD = new Structs.MXMD
            {
                Version = brMXMD.ReadInt32(),

                ModelStructOffset = brMXMD.ReadInt32(),
                MaterialsOffset = brMXMD.ReadInt32(),

                Unknown1 = brMXMD.ReadInt32(),

                VertexBufferOffset = brMXMD.ReadInt32(),
                ShadersOffset = brMXMD.ReadInt32(),
                CachedTexturesTableOffset = brMXMD.ReadInt32(),
                Unknown2 = brMXMD.ReadInt32(),
                UncachedTexturesTableOffset = brMXMD.ReadInt32(),

                Unknown3 = brMXMD.ReadBytes(0x28)
            };

            MXMD.ModelStruct = new Structs.MXMDModelStruct
            {
                Unknown1 = brMXMD.ReadInt32(),
                BoundingBoxStart = new Vector3(brMXMD.ReadSingle(), brMXMD.ReadSingle(), brMXMD.ReadSingle()),
                BoundingBoxEnd = new Vector3(brMXMD.ReadSingle(), brMXMD.ReadSingle(), brMXMD.ReadSingle()),
                MeshesOffset = brMXMD.ReadInt32(),
                Unknown2 = brMXMD.ReadInt32(),
                Unknown3 = brMXMD.ReadInt32(),
                NodesOffset = brMXMD.ReadInt32(),

                Unknown4 = brMXMD.ReadBytes(0x54),

                MorphControllersOffset = brMXMD.ReadInt32(),
                MorphNamesOffset = brMXMD.ReadInt32()
            };

            if (MXMD.ModelStruct.MorphControllersOffset != 0)
            {
                fsMXMD.Seek(MXMD.ModelStructOffset + MXMD.ModelStruct.MorphControllersOffset, SeekOrigin.Begin);
                MXMD.ModelStruct.MorphControls = new Structs.MXMDMorphControls
                {
                    TableOffset = brMXMD.ReadInt32(),
                    Count = brMXMD.ReadInt32(),

                    Unknown2 = brMXMD.ReadBytes(0x10)
                };

                MXMD.ModelStruct.MorphControls.Controls = new Structs.MXMDMorphControl[MXMD.ModelStruct.MorphControls.Count];
                long nextPosition = fsMXMD.Position;
                for (int i = 0; i < MXMD.ModelStruct.MorphControls.Count; i++)
                {
                    fsMXMD.Seek(nextPosition, SeekOrigin.Begin);
                    nextPosition += 0x1C;
                    MXMD.ModelStruct.MorphControls.Controls[i] = new Structs.MXMDMorphControl
                    {
                        NameOffset1 = brMXMD.ReadInt32(),
                        NameOffset2 = brMXMD.ReadInt32(), //the results of these should be identical
                        Unknown1 = brMXMD.ReadBytes(0x14)
                    };

                    fsMXMD.Seek(MXMD.ModelStructOffset + MXMD.ModelStruct.MorphControllersOffset + MXMD.ModelStruct.MorphControls.Controls[i].NameOffset1, SeekOrigin.Begin);
                    MXMD.ModelStruct.MorphControls.Controls[i].Name = FormatTools.ReadNullTerminatedString(brMXMD);
                }
            }

            if (MXMD.ModelStruct.MorphNamesOffset != 0)
            {
                fsMXMD.Seek(MXMD.ModelStructOffset + MXMD.ModelStruct.MorphNamesOffset, SeekOrigin.Begin);
                MXMD.ModelStruct.MorphNames = new Structs.MXMDMorphNames
                {
                    TableOffset = brMXMD.ReadInt32(),
                    Count = brMXMD.ReadInt32(),

                    Unknown2 = brMXMD.ReadBytes(0x20)
                };

                MXMD.ModelStruct.MorphNames.Names = new Structs.MXMDMorphName[MXMD.ModelStruct.MorphNames.Count];
                long nextPosition = fsMXMD.Position;
                for (int i = 0; i < MXMD.ModelStruct.MorphNames.Count; i++)
                {
                    fsMXMD.Seek(nextPosition, SeekOrigin.Begin);
                    nextPosition += 0x10;
                    MXMD.ModelStruct.MorphNames.Names[i] = new Structs.MXMDMorphName
                    {
                        NameOffset = brMXMD.ReadInt32(),
                        Unknown1 = brMXMD.ReadInt32(),
                        Unknown2 = brMXMD.ReadInt32(),
                        Unknown3 = brMXMD.ReadInt32(),
                    };

                    fsMXMD.Seek(MXMD.ModelStructOffset + MXMD.ModelStruct.MorphControllersOffset + MXMD.ModelStruct.MorphNames.Names[i].NameOffset, SeekOrigin.Begin);
                    MXMD.ModelStruct.MorphNames.Names[i].Name = FormatTools.ReadNullTerminatedString(brMXMD);
                }
            }

            if (MXMD.ModelStruct.MeshesOffset != 0)
            {
                fsMXMD.Seek(MXMD.ModelStructOffset + MXMD.ModelStruct.MeshesOffset, SeekOrigin.Begin);
                MXMD.ModelStruct.Meshes = new Structs.MXMDMeshes
                {
                    TableOffset = brMXMD.ReadInt32(),
                    TableCount = brMXMD.ReadInt32(),
                    Unknown1 = brMXMD.ReadInt32(),

                    BoundingBoxStart = new Vector3(brMXMD.ReadSingle(), brMXMD.ReadSingle(), brMXMD.ReadSingle()),
                    BoundingBoxEnd = new Vector3(brMXMD.ReadSingle(), brMXMD.ReadSingle(), brMXMD.ReadSingle()),
                    BoundingRadius = brMXMD.ReadSingle()
                };

                fsMXMD.Seek(MXMD.ModelStructOffset + MXMD.ModelStruct.Meshes.TableOffset, SeekOrigin.Begin);
                MXMD.ModelStruct.Meshes.Meshes = new Structs.MXMDMesh[MXMD.ModelStruct.Meshes.TableCount];
                for (int i = 0; i < MXMD.ModelStruct.Meshes.TableCount; i++)
                {
                    MXMD.ModelStruct.Meshes.Meshes[i] = new Structs.MXMDMesh
                    {
                        //ms says to add 1 to some of these, why?
                        ID = brMXMD.ReadInt32(), //0x134

                        Descriptor = brMXMD.ReadInt32(), //0x138

                        VTBuffer = brMXMD.ReadInt16(), //0x13C
                        UVFaces = brMXMD.ReadInt16(),

                        Unknown1 = brMXMD.ReadInt16(), //0x140
                        MaterialID = brMXMD.ReadInt16(),
                        Unknown2 = brMXMD.ReadBytes(0xC),
                        Unknown3 = brMXMD.ReadInt16(), //0x150

                        LOD = brMXMD.ReadInt16(), //0x152
                        Unknown4 = brMXMD.ReadInt32(), //0x154

                        Unknown5 = brMXMD.ReadBytes(0xC),
                    };
                }
            }

            if (MXMD.ModelStruct.NodesOffset != 0)
            {
                fsMXMD.Seek(MXMD.ModelStructOffset + MXMD.ModelStruct.NodesOffset, SeekOrigin.Begin);
                MXMD.ModelStruct.Nodes = new Structs.MXMDNodes
                {
                    BoneCount = brMXMD.ReadInt32(),
                    BoneCount2 = brMXMD.ReadInt32(),

                    NodeIdsOffset = brMXMD.ReadInt32(),
                    NodeTmsOffset = brMXMD.ReadInt32()
                };

                MXMD.ModelStruct.Nodes.Nodes = new Structs.MXMDNode[MXMD.ModelStruct.Nodes.BoneCount];

                long nextPosition = MXMD.ModelStructOffset + MXMD.ModelStruct.NodesOffset + MXMD.ModelStruct.Nodes.NodeIdsOffset;
                for (int i = 0; i < MXMD.ModelStruct.Nodes.BoneCount; i++)
                {
                    fsMXMD.Seek(nextPosition, SeekOrigin.Begin);
                    nextPosition += 0x18;
                    MXMD.ModelStruct.Nodes.Nodes[i] = new Structs.MXMDNode
                    {
                        NameOffset = brMXMD.ReadInt32(),
                        Unknown1 = brMXMD.ReadSingle(),
                        Unknown2 = brMXMD.ReadInt32(),

                        ID = brMXMD.ReadInt32(),
                        Unknown3 = brMXMD.ReadInt32(),
                        Unknown4 = brMXMD.ReadInt32()
                    };

                    fsMXMD.Seek(MXMD.ModelStructOffset + MXMD.ModelStruct.NodesOffset + MXMD.ModelStruct.Nodes.Nodes[i].NameOffset, SeekOrigin.Begin);
                    MXMD.ModelStruct.Nodes.Nodes[i].Name = FormatTools.ReadNullTerminatedString(brMXMD);
                }

                nextPosition = MXMD.ModelStructOffset + MXMD.ModelStruct.NodesOffset + MXMD.ModelStruct.Nodes.NodeTmsOffset;
                for (int i = 0; i < MXMD.ModelStruct.Nodes.BoneCount; i++)
                {
                    fsMXMD.Seek(nextPosition, SeekOrigin.Begin);
                    nextPosition += 0x10 * 4;

                    //this is probably very incorrect
                    MXMD.ModelStruct.Nodes.Nodes[i].Scale = new Quaternion(brMXMD.ReadSingle(), brMXMD.ReadSingle(), brMXMD.ReadSingle(), brMXMD.ReadSingle());
                    MXMD.ModelStruct.Nodes.Nodes[i].Rotation = new Quaternion(brMXMD.ReadSingle(), brMXMD.ReadSingle(), brMXMD.ReadSingle(), brMXMD.ReadSingle());
                    MXMD.ModelStruct.Nodes.Nodes[i].Position = new Quaternion(brMXMD.ReadSingle(), brMXMD.ReadSingle(), brMXMD.ReadSingle(), brMXMD.ReadSingle());

                    MXMD.ModelStruct.Nodes.Nodes[i].ParentTransform = new Quaternion(brMXMD.ReadSingle(), brMXMD.ReadSingle(), brMXMD.ReadSingle(), brMXMD.ReadSingle());
                }
            }

            /*string text = "MXMD Properties: ";
            text += $"\n\tBone Count: {MXMD.ModelStruct.Nodes.BoneCount}";
            text += $"\n\tBone Count 2: {MXMD.ModelStruct.Nodes.BoneCount2}";
            text += "\n\tNodes: ";
            foreach(var test in MXMD.ModelStruct.Nodes.Nodes)
            {
                text += $"\n\t\tName:             | {test.Name}";
                text += $"\n\t\tID:               | {test.ID}";
                text += $"\n\t\tParent Transform: | {test.ParentTransform}";
                text += $"\n\t\tPosition:         | {test.Position}";
                text += $"\n\t\tRotation:         | {test.Rotation}";
                text += $"\n\t\tScale:            | {test.Scale}";
                text += "\n";
            }

            Console.WriteLine(text);
            Console.ReadLine();*/

            return MXMD;
        }

        public void ModelToASCII(Stream memoryStream, BinaryReader binaryReader, string filepath)
        {
            memoryStream.Seek(0, SeekOrigin.Begin);

            int i = 0;
            int meshPointersPointer = binaryReader.ReadInt32(); //0x0
            int meshCount = binaryReader.ReadInt32(); //0x4
            int meshDataPointer = binaryReader.ReadInt32(); //0x8
            int meshCountWithFlexes = binaryReader.ReadInt32(); //0xC

            memoryStream.Seek(0x18, SeekOrigin.Current);
            int num16 = binaryReader.ReadInt32(); //0x28
            binaryReader.ReadInt32(); //0x2C
            int meshDataStart = binaryReader.ReadInt32(); //0x30 4096
            binaryReader.ReadInt32(); //0x34
            int meshExtraDataPointer = binaryReader.ReadInt32(); //0x38 496

            //no clue
            int num19 = 0;
            int num20 = 0;
            int[] array6 = null;
            int[] array7 = null;
            int[] array8 = null;
            if (num16 > 0) //flex related?
            {
                memoryStream.Seek((long)num16, SeekOrigin.Begin);
                num19 = binaryReader.ReadInt32();
                int num21 = binaryReader.ReadInt32();
                binaryReader.ReadInt32();
                num20 = binaryReader.ReadInt32();
                memoryStream.Seek((long)num21, SeekOrigin.Begin);
                array6 = new int[num19];
                array7 = new int[num19];
                array8 = new int[num19];
                for (i = 0; i < num19; i++)
                {
                    array6[i] = binaryReader.ReadInt32();
                    array7[i] = binaryReader.ReadInt32();
                    array8[i] = binaryReader.ReadInt32();
                    binaryReader.ReadInt32();
                    binaryReader.ReadInt32();
                }
            }

            int[] meshesPointers = new int[meshCount];
            int[] array10 = new int[meshCount];
            int[] meshesDataCount = new int[meshCount];
            int[] array12 = new int[meshCount];
            int[] array13 = new int[meshCount];
            int[] array14 = new int[meshCount];
            memoryStream.Seek(meshPointersPointer, SeekOrigin.Begin); //0x50
            for (i = 0; i < meshCount; i++)
            {
                meshesPointers[i] = meshDataStart + binaryReader.ReadInt32();
                meshesDataCount[i] = binaryReader.ReadInt32(); //608???
                array12[i] = binaryReader.ReadInt32(); //36
                array13[i] = binaryReader.ReadInt32(); //388
                array14[i] = binaryReader.ReadInt32(); //6
                memoryStream.Seek(12L, SeekOrigin.Current);
            }

            int[] meshesDataStart = new int[meshCountWithFlexes];
            int[] meshesVertexCount = new int[meshCountWithFlexes]; //5
            memoryStream.Seek(meshDataPointer, SeekOrigin.Begin); //0xF0 80
            for (i = 0; i < meshCountWithFlexes; i++)
            {
                meshesDataStart[i] = meshDataStart + binaryReader.ReadInt32();
                meshesVertexCount[i] = binaryReader.ReadInt32();
                binaryReader.ReadInt32();
                binaryReader.ReadInt32();
                binaryReader.ReadInt32();
            }

            memoryStream.Seek(meshExtraDataPointer + 0x8, SeekOrigin.Begin);
            int meshWeightBoneCount = binaryReader.ReadInt16(); //4
            int[,] meshWeightIds = new int[meshesDataCount[meshWeightBoneCount], 4];
            float[,] meshWeightValues = new float[meshesDataCount[meshWeightBoneCount], 4];
            Vector3[][] meshVertices = new Vector3[meshCount][];
            Vector3[][] meshNormals = new Vector3[meshCount][];
            float[][,] meshFlexesX = new float[meshCount][,];
            float[][,] meshFlexesY = new float[meshCount][,];
            int[][] meshWeights = new int[meshCount][];
            int[] meshUVLayers = new int[meshCount];

            if (!File.Exists(App.CurFilePath.Remove(App.CurFilePath.LastIndexOf('.')) + ".wimdo"))
                return;
            if (!File.Exists(App.CurFilePath.Remove(App.CurFilePath.LastIndexOf('.')) + ".arc"))
                return;

            FileStream fsWIMDO = new FileStream(App.CurFilePath.Remove(App.CurFilePath.LastIndexOf('.')) + ".wimdo", FileMode.Open, FileAccess.Read);
            BinaryReader brWIMDO = new BinaryReader(fsWIMDO);

            Structs.MXMD MXMD = ReadMXMD(fsWIMDO, brWIMDO);

            //these will be deleted eventually once I get skeleton and mesh reading, considering they're redundant

            string[] meshFlexNames = new string[128];
            if (MXMD.ModelStruct.MorphControllersOffset != 0)
            {
                meshFlexNames = new string[MXMD.ModelStruct.MorphControls.Controls.Length];
                for (int r = 0; r < MXMD.ModelStruct.MorphControls.Controls.Length; r++)
                {
                    meshFlexNames[r] = MXMD.ModelStruct.MorphControls.Controls[r].Name;
                }
            }

            int MeshesTableCount = MXMD.ModelStruct.Meshes.TableCount;

            Dictionary<int, string> NodesIdsNames = new Dictionary<int, string>();
            for (int r = 0; r < MXMD.ModelStruct.Nodes.BoneCount; r++)
            {
                NodesIdsNames.Add(r, MXMD.ModelStruct.Nodes.Nodes[r].Name);
            }

            int[] MeshesUVFaces = new int[MXMD.ModelStruct.Meshes.TableCount];
            int[] MeshesVertexBuffer = new int[MXMD.ModelStruct.Meshes.TableCount];
            int[] array29 = new int[MXMD.ModelStruct.Meshes.TableCount];
            int[] meshVertexCount = new int[MXMD.ModelStruct.Meshes.TableCount];
            for (int r = 0; r < MXMD.ModelStruct.Meshes.TableCount; r++)
            {
                MeshesUVFaces[r] = MXMD.ModelStruct.Meshes.Meshes[r].UVFaces;
                MeshesVertexBuffer[r] = MXMD.ModelStruct.Meshes.Meshes[r].VTBuffer;
                array29[r] = BitConverter.GetBytes(MXMD.ModelStruct.Meshes.Meshes[r].ID).Last(); //still not sure what this is for
                meshVertexCount[r] = MXMD.ModelStruct.Meshes.Meshes[r].LOD;
            }


            FileStream fsARC = new FileStream(App.CurFilePath.Remove(App.CurFilePath.LastIndexOf('.')) + ".arc", FileMode.Open, FileAccess.Read);
            BinaryReader brARC = new BinaryReader(fsARC);

            Structs.SAR1 SAR1 = ReadSAR1(fsARC, brARC);
            BinaryReader brSKEL = new BinaryReader(SAR1.ItemBySearch(".skl").Data);
            Structs.SKEL SKEL = ReadSKEL(brSKEL.BaseStream, brSKEL);

            int boneCount = SKEL.TOCItems[2].Count;

            Vector3[] bonePosOrig = new Vector3[boneCount];
            Vector3[] bonePos = new Vector3[boneCount];
            Quaternion[] boneRotOrig = new Quaternion[boneCount];
            Quaternion[] boneRot = new Quaternion[boneCount];

            int[] bone_parents = new int[boneCount];
            string[] bone_names = new string[boneCount];
            Dictionary<string, int> SKELNodeNames = new Dictionary<string, int>();

            for (i = 0; i < boneCount; i++)
            {
                Quaternion posQuat = SKEL.Transforms[i].Position;
                bonePosOrig[i] = new Vector3(posQuat.X, posQuat.Y, posQuat.Z);
                boneRotOrig[i] = SKEL.Transforms[i].Rotation;

                bone_parents[i] = SKEL.Parents[i];
                SKELNodeNames.Add(SKEL.Nodes[i].Name, i);
                bone_names[i] = SKEL.Nodes[i].Name;

                if (bone_parents[i] < 0) //is root
                {
                    bonePos[i] = bonePosOrig[i];
                    boneRot[i] = boneRotOrig[i];
                }
                else
                {
                    int curParentIndex = bone_parents[i];
                    //add rotation of parent
                    boneRot[i] = boneRot[curParentIndex] * boneRotOrig[i];
                    //make position a quaternion again (for math reasons)
                    Quaternion bonePosQuat = new Quaternion(bonePosOrig[i], 0f);
                    //multiply position and rotation (?)
                    Quaternion bonePosRotQuat = boneRot[curParentIndex] * bonePosQuat;
                    //do something or other i dunno
                    Quaternion newPosition = bonePosRotQuat * new Quaternion(-boneRot[curParentIndex].X, -boneRot[curParentIndex].Y, -boneRot[curParentIndex].Z, boneRot[curParentIndex].W);
                    bonePos[i] = new Vector3(newPosition.X, newPosition.Y, newPosition.Z);
                }
            }


            //begin ascii
            //bone time
            StreamWriter asciiWriter = new StreamWriter($@"{App.CurOutputPath}\{Path.GetFileNameWithoutExtension(filepath) + ".ascii"}");
            App.PushLog("Writing .ascii file...");
            asciiWriter.WriteLine(boneCount);
            for (int j = 0; j < boneCount; j++)
            {
                asciiWriter.WriteLine(bone_names[j]);
                asciiWriter.WriteLine(bone_parents[j]);
                asciiWriter.Write(bonePos[j].X.ToString("0.######"));
                asciiWriter.Write(" " + bonePos[j].Y.ToString("0.######"));
                asciiWriter.Write(" " + bonePos[j].Z.ToString("0.######"));
                asciiWriter.Write(" " + boneRot[j].X.ToString("0.######"));
                asciiWriter.Write(" " + boneRot[j].Y.ToString("0.######"));
                asciiWriter.Write(" " + boneRot[j].Z.ToString("0.######"));
                asciiWriter.Write(" " + boneRot[j].W.ToString("0.######"));
                //bone name
                //bone parent index
                //x y z i j w real
                asciiWriter.WriteLine();
            }

            //begin meshes
            for (int currentMesh = 0; currentMesh < meshCount; currentMesh++)
            {
                meshVertices[currentMesh] = new Vector3[meshesDataCount[currentMesh]];
                meshNormals[currentMesh] = new Vector3[meshesDataCount[currentMesh]];
                meshFlexesX[currentMesh] = new float[meshesDataCount[currentMesh], 4];
                meshFlexesY[currentMesh] = new float[meshesDataCount[currentMesh], 4];
                meshWeights[currentMesh] = new int[meshesDataCount[currentMesh]];
                int num44 = -1;
                int num45 = -1;
                int num46 = -1;
                int num47 = -1;
                int num48 = -1;
                int num49 = 0;
                int[] meshUVSomething = new int[4];
                memoryStream.Seek((long)array13[currentMesh], SeekOrigin.Begin);
                for (int j = 0; j < array14[currentMesh]; j++)
                {
                    int num50 = (int)binaryReader.ReadInt16();
                    int num51 = (int)binaryReader.ReadInt16();
                    if (num50 == 0)
                    {
                        num44 = num49;
                    }
                    else if (num50 == 3)
                    {
                        num46 = num49;
                    }
                    else if (num50 == 5)
                    {
                        meshUVSomething[meshUVLayers[currentMesh]] = num49;
                        meshUVLayers[currentMesh]++;
                    }
                    else if (num50 == 6)
                    {
                        meshUVSomething[meshUVLayers[currentMesh]] = num49;
                        meshUVLayers[currentMesh]++;
                    }
                    else if (num50 == 7)
                    {
                        meshUVSomething[meshUVLayers[currentMesh]] = num49;
                        meshUVLayers[currentMesh]++;
                    }
                    else if (num50 == 28)
                    {
                        num45 = num49;
                    }
                    else if (num50 == 41)
                    {
                        num47 = num49;
                    }
                    else if (num50 == 42)
                    {
                        num48 = num49;
                    }
                    num49 += num51;
                }
                for (int j = 0; j < meshesDataCount[currentMesh]; j++)
                {
                    if (num44 >= 0)
                    {
                        //if (j == 0)
                        //    Console.WriteLine($"POINTER: {meshesPointers[currentMesh]}\nJ: {j}\nARRAY12: {array12[currentMesh]}\nNUM44: {num44}");
                        memoryStream.Seek((long)(meshesPointers[currentMesh] + j * array12[currentMesh] + num44), SeekOrigin.Begin);
                        //if (currentMesh == 0)
                        //    Console.WriteLine($"Setting mesh's vertices! At offset 0x{memoryStream.Position.ToString("X")}");
                        meshVertices[currentMesh][j] = new Vector3(binaryReader.ReadSingle(), binaryReader.ReadSingle(), binaryReader.ReadSingle());
                        meshNormals[currentMesh][j] = new Vector3(0f, 0f, 0f);
                    }
                    if (num46 >= 0)
                    {
                        memoryStream.Seek((long)(meshesPointers[currentMesh] + j * array12[currentMesh] + num46), SeekOrigin.Begin);
                        //if (currentMesh == 0)
                        //    Console.WriteLine($"Setting mesh's weights! At offset 0x{memoryStream.Position.ToString("X")}");
                        meshWeights[currentMesh][j] = binaryReader.ReadInt32();
                    }

                    for (int k = 0; k < meshUVLayers[currentMesh]; k++)
                    {
                        memoryStream.Seek((long)(meshesPointers[currentMesh] + j * array12[currentMesh] + meshUVSomething[k]), SeekOrigin.Begin);
                        //if (currentMesh == 0 && k == 0)
                        //    Console.WriteLine($"Setting mesh's flexes! At offset 0x{memoryStream.Position.ToString("X")}");
                        meshFlexesX[currentMesh][j, k] = binaryReader.ReadSingle();
                        meshFlexesY[currentMesh][j, k] = binaryReader.ReadSingle();
                    }

                    if (num45 >= 0)
                    {
                        memoryStream.Seek((long)(meshesPointers[currentMesh] + j * array12[currentMesh] + num45), SeekOrigin.Begin);
                        //if (currentMesh == 0)
                        //    Console.WriteLine($"Setting mesh's normals! At offset 0x{memoryStream.Position.ToString("X")}");
                        float num40 = (float)binaryReader.ReadSByte() / 128f;
                        float num41 = (float)binaryReader.ReadSByte() / 128f;
                        float num42 = (float)binaryReader.ReadSByte() / 128f;
                        meshNormals[currentMesh][j] = new Vector3(num40, num41, num42);
                    }
                    if (num48 >= 0)
                    {
                        memoryStream.Seek((long)(meshesPointers[currentMesh] + j * array12[currentMesh] + num48), SeekOrigin.Begin);
                        //if (currentMesh == 0)
                        //    Console.WriteLine($"Setting mesh's weight ids! At offset 0x{memoryStream.Position.ToString("X")}");
                        try
                        {
                            int key = (int)binaryReader.ReadByte();
                            meshWeightIds[j, 0] = SKELNodeNames[NodesIdsNames[key]];
                            key = (int)binaryReader.ReadByte();
                            meshWeightIds[j, 1] = SKELNodeNames[NodesIdsNames[key]];
                            key = (int)binaryReader.ReadByte();
                            meshWeightIds[j, 2] = SKELNodeNames[NodesIdsNames[key]];
                            key = (int)binaryReader.ReadByte();
                            meshWeightIds[j, 3] = SKELNodeNames[NodesIdsNames[key]];
                        }
                        catch
                        {
                            meshWeightIds[j, 0] = 0;
                            meshWeightIds[j, 1] = 0;
                            meshWeightIds[j, 2] = 0;
                            meshWeightIds[j, 3] = 0;
                        }
                    }
                    if (num47 >= 0)
                    {
                        memoryStream.Seek((long)(meshesPointers[currentMesh] + j * array12[currentMesh] + num47), SeekOrigin.Begin);
                        //if (currentMesh == 0)
                        //    Console.WriteLine($"Setting mesh's weight values! At offset 0x{memoryStream.Position.ToString("X")}");
                        meshWeightValues[j, 0] = (float)binaryReader.ReadUInt16() / 65535f;
                        meshWeightValues[j, 1] = (float)binaryReader.ReadUInt16() / 65535f;
                        meshWeightValues[j, 2] = (float)binaryReader.ReadUInt16() / 65535f;
                        meshWeightValues[j, 3] = (float)binaryReader.ReadUInt16() / 65535f;
                    }
                }
            }

            Vector3[][] array42 = new Vector3[num19][];
            if (num16 > 0)
            {
                for (i = 0; i < num19; i++)
                {
                    array10[array6[i]] = i + 1;
                    memoryStream.Seek((long)(num20 + array7[i] * 16), SeekOrigin.Begin);
                    int num52 = meshDataStart + binaryReader.ReadInt32();
                    int num53 = binaryReader.ReadInt32();
                    if (num53 != meshesDataCount[array6[i]])
                    {
                        Console.WriteLine("Flex vertices count is incorrect!");
                    }
                    array42[i] = new Vector3[num53];
                    for (int j = 0; j < num53; j++)
                    {
                        memoryStream.Seek((long)(num52 + j * 32), SeekOrigin.Begin);
                        meshVertices[array6[i]][j] = new Vector3(binaryReader.ReadSingle(), binaryReader.ReadSingle(), binaryReader.ReadSingle());
                        meshNormals[array6[i]][j] = new Vector3(0f, 0f, 0f);
                    }
                }
            }

            //total mesh count
            int flexAndMeshCount = 0;
            for (i = 0; i < MeshesTableCount; i++)
            {
                if (meshVertexCount[i] < 2)
                {
                    flexAndMeshCount++;
                    if (array29[i] != 0 && App.ExportFlexes)
                    {
                        flexAndMeshCount += array8[0];
                    }
                }
            }
            asciiWriter.WriteLine(flexAndMeshCount);
            int num55 = 1;
            if (num16 > 0 && App.ExportFlexes)
            {
                num55 += array8[0];
            }

            //write it
            for (int flexIndex = 0; flexIndex < num55; flexIndex++)
            {
                if (flexIndex > 0)
                {
                    for (i = 0; i < num19; i++)
                    {
                        for (int j = 0; j < array42[i].Length; j++)
                        {
                            array42[i][j] = new Vector3();
                        }
                        memoryStream.Seek((long)(num20 + (array7[i] + 1 + flexIndex) * 16), SeekOrigin.Begin);
                        int num56 = meshDataStart + binaryReader.ReadInt32();
                        int num57 = binaryReader.ReadInt32();
                        for (int j = 0; j < num57; j++)
                        {
                            memoryStream.Seek((long)(num56 + j * 32), SeekOrigin.Begin);
                            Vector3 Vector3 = new Vector3(binaryReader.ReadSingle(), binaryReader.ReadSingle(), binaryReader.ReadSingle());
                            memoryStream.Seek(16L, SeekOrigin.Current);
                            int num58 = binaryReader.ReadInt32();
                            array42[i][num58] = Vector3;
                        }
                    }
                }
                for (i = 0; i < MeshesTableCount; i++)
                {
                    if (meshVertexCount[i] <= 1)
                    {
                        int curMesh = MeshesUVFaces[i];
                        int curMeshIndex = MeshesVertexBuffer[i];
                        if (array29[i] != 0 || flexIndex <= 0)
                        {
                            memoryStream.Seek((long)meshesDataStart[curMesh], SeekOrigin.Begin);
                            int[] array43 = new int[meshesVertexCount[curMesh]];
                            int curMeshVertCount = 0;
                            int prvMeshVertCount = meshesDataCount[curMeshIndex];
                            for (int j = 0; j < meshesVertexCount[curMesh]; j++)
                            {
                                int num63 = (int)binaryReader.ReadInt16();
                                array43[j] = num63;
                                if (num63 > curMeshVertCount)
                                {
                                    curMeshVertCount = num63;
                                }
                                if (num63 < prvMeshVertCount)
                                {
                                    prvMeshVertCount = num63;
                                }
                            }
                            if (flexIndex > 0)
                            {
                                asciiWriter.WriteLine($"sm_{i}_{meshFlexNames[flexIndex - 1]}"); //mesh name (+ flex name)
                            }
                            else
                            {
                                asciiWriter.WriteLine("sm_" + i); //mesh name
                            }
                            asciiWriter.WriteLine(meshUVLayers[curMeshIndex]);
                            asciiWriter.WriteLine(0); //texture count, always 0 for us, though maybe I should change that?
                            asciiWriter.WriteLine(curMeshVertCount - prvMeshVertCount + 1); //vertex count
                            for (int vrtIndex = prvMeshVertCount; vrtIndex <= curMeshVertCount; vrtIndex++)
                            {
                                if (flexIndex > 0)
                                {
                                    //vertex position
                                    Vector3 Vector3 = meshVertices[curMeshIndex][vrtIndex] + array42[array10[curMeshIndex] - 1][vrtIndex];
                                    asciiWriter.Write(Vector3.X.ToString("0.######"));
                                    asciiWriter.Write(" " + Vector3.Y.ToString("0.######"));
                                    asciiWriter.Write(" " + Vector3.Z.ToString("0.######"));
                                    asciiWriter.WriteLine();
                                }
                                else
                                {
                                    //vertex position
                                    asciiWriter.Write(meshVertices[curMeshIndex][vrtIndex].X.ToString("0.######"));
                                    asciiWriter.Write(" " + meshVertices[curMeshIndex][vrtIndex].Y.ToString("0.######"));
                                    asciiWriter.Write(" " + meshVertices[curMeshIndex][vrtIndex].Z.ToString("0.######"));
                                    asciiWriter.WriteLine();
                                }

                                //vertex normal
                                asciiWriter.Write(meshNormals[curMeshIndex][vrtIndex].X.ToString("0.######"));
                                asciiWriter.Write(" " + meshNormals[curMeshIndex][vrtIndex].Y.ToString("0.######"));
                                asciiWriter.Write(" " + meshNormals[curMeshIndex][vrtIndex].Z.ToString("0.######"));
                                asciiWriter.WriteLine();
                                asciiWriter.WriteLine("0 0 0 0"); // vertex color (why)

                                //uv coords
                                for (int curUVLayer = 0; curUVLayer < meshUVLayers[curMeshIndex]; curUVLayer++)
                                    asciiWriter.WriteLine(meshFlexesX[curMeshIndex][vrtIndex, curUVLayer].ToString("0.######") + " " + meshFlexesY[curMeshIndex][vrtIndex, curUVLayer].ToString("0.######"));

                                //weight ids
                                asciiWriter.Write(meshWeightIds[meshWeights[curMeshIndex][vrtIndex], 0]);
                                asciiWriter.Write(" " + meshWeightIds[meshWeights[curMeshIndex][vrtIndex], 1]);
                                asciiWriter.Write(" " + meshWeightIds[meshWeights[curMeshIndex][vrtIndex], 2]);
                                asciiWriter.Write(" " + meshWeightIds[meshWeights[curMeshIndex][vrtIndex], 3]);
                                asciiWriter.WriteLine();

                                //weight values
                                asciiWriter.Write(meshWeightValues[meshWeights[curMeshIndex][vrtIndex], 0].ToString("0.######"));
                                asciiWriter.Write(" " + meshWeightValues[meshWeights[curMeshIndex][vrtIndex], 1].ToString("0.######"));
                                asciiWriter.Write(" " + meshWeightValues[meshWeights[curMeshIndex][vrtIndex], 2].ToString("0.######"));
                                asciiWriter.Write(" " + meshWeightValues[meshWeights[curMeshIndex][vrtIndex], 3].ToString("0.######"));
                                asciiWriter.WriteLine();
                            }

                            //face count
                            asciiWriter.WriteLine(meshesVertexCount[curMesh] / 3);
                            for (int j = 0; j < meshesVertexCount[curMesh]; j += 3)
                            {
                                int faceVertexZ = array43[j] - prvMeshVertCount;
                                int faceVertexY = array43[j + 1] - prvMeshVertCount;
                                int faceVertexX = array43[j + 2] - prvMeshVertCount;
                                //face vertex ids
                                asciiWriter.WriteLine($"{faceVertexX} {faceVertexY} {faceVertexZ}");
                            }
                        }
                    }
                }
            }

            asciiWriter.Flush();
            asciiWriter.Close();

            memoryStream.Close();
            binaryReader.Close();
            GC.Collect();
        }

        public void ReadTextures(FileStream fsWISMT, BinaryReader brWISMT, Structs.MSRD MSRD, string texturesFolderPath)
        {
            if (fsWISMT == null || brWISMT == null)
                return;
            App.PushLog("Reading textures...");

            MemoryStream msCurFile = XBC1(fsWISMT, brWISMT, MSRD.TOC[1].Offset);
            BinaryReader brCurFile = new BinaryReader(msCurFile);

            int[] TextureHeightArray = new int[MSRD.TextureIdsCount];
            int[] TextureWidthArray = new int[MSRD.TextureIdsCount];
            int[] TextureTypeArray = new int[MSRD.TextureIdsCount];
            for (int i = 0; i < MSRD.TextureIdsCount; i++)
            {
                msCurFile.Seek(MSRD.DataItems[i + 3].Offset + MSRD.DataItems[i + 3].Size - 32, SeekOrigin.Begin);
                TextureHeightArray[i] = brCurFile.ReadInt32();
                TextureWidthArray[i] = brCurFile.ReadInt32();
                brCurFile.ReadInt32();
                brCurFile.ReadInt32();
                TextureTypeArray[i] = brCurFile.ReadInt32();
            }

            for (int i = 0; i < MSRD.TextureCount - 2; i++)
            {
                msCurFile = XBC1(fsWISMT, brWISMT, MSRD.TOC[i + 2].Offset);
                brCurFile = new BinaryReader(msCurFile);
                int TextureType = 0;
                switch(TextureTypeArray[i])
                {
                    case 37:
                        TextureType = 28;
                        break;
                    case 66:
                        TextureType = 71;
                        break;
                    case 68:
                        TextureType = 77;
                        break;
                    case 73:
                        TextureType = 80;
                        break;
                    case 75:
                        TextureType = 83;
                        break;
                    case 0:
                    default:
                        Console.WriteLine("unknown texture type " + TextureTypeArray[i]);
                        return;
                }

                int ImageWidth = TextureHeightArray[i] * 2;
                int ImageHeight = TextureWidthArray[i] * 2;
                int TextureUnswizzleBufferSize = BitsPerPixel[TextureType] * 2;
                int SwizzleSize = 4;

                int DDSFourCC = 0x30315844; //DX10
                switch (TextureType)
                {
                    case 71:
                        DDSFourCC = 0x31545844;
                        break;
                    case 74:
                        DDSFourCC = 0x33545844;
                        break;
                    case 77:
                        DDSFourCC = 0x35545844;
                        break;
                    case 80:
                        DDSFourCC = 0x31495441;
                        break;
                    case 83:
                        DDSFourCC = 0x32495441;
                        break;
                }

                FileStream fsTexture;
                if (TextureTypeArray[i] == 37)
                {
                    fsTexture = new FileStream($@"{texturesFolderPath}\{i.ToString("d2")}_{MSRD.TextureNames[MSRD.TextureIds[i]]}.tga", FileMode.Create);
                    BinaryWriter brTexture = new BinaryWriter(fsTexture);
                    brTexture.Write(0x20000); //type stuff
                    brTexture.Write(0x0); //color map info
                    brTexture.Write(0x0); //origin position
                    brTexture.Write((short)ImageWidth);
                    brTexture.Write((short)ImageHeight);
                    brTexture.Write(0x820); //pixel size and descriptor
                    brTexture.Seek(0x12, SeekOrigin.Begin);
                    TextureUnswizzleBufferSize = BitsPerPixel[TextureType] / 8;
                    SwizzleSize = 1;
                }
                else
                {
                    fsTexture = new FileStream($@"{texturesFolderPath}\{i.ToString("d2")}_{MSRD.TextureNames[MSRD.TextureIds[i]]}.dds", FileMode.Create);
                    BinaryWriter binaryWriter = new BinaryWriter(fsTexture);
                    binaryWriter.Write(0x7C20534444); //magic
                    binaryWriter.Write(0x1007); //flags
                    binaryWriter.Write(ImageHeight);
                    binaryWriter.Write(ImageWidth);
                    binaryWriter.Write(msCurFile.Length);
                    binaryWriter.Write(0x1);
                    fsTexture.Seek(0x2C, SeekOrigin.Current);
                    binaryWriter.Write(0x20);
                    binaryWriter.Write(0x4);
                    binaryWriter.Write(DDSFourCC);
                    fsTexture.Seek(0x28, SeekOrigin.Current);
                    if (DDSFourCC == 0x30315844) //DXT10 header
                    {
                        binaryWriter.Write(TextureType);
                        binaryWriter.Write(3); //resourceDimension
                        binaryWriter.Write(0); //miscFlag
                        binaryWriter.Write(1); //arraySize
                        binaryWriter.Write(0); //miscFlags2
                    }
                }

                byte[] TextureUnswizzleBuffer = new byte[16];
                byte[] TextureUnswizzled = new byte[msCurFile.Length];

                int ImageHeightInTiles = ImageHeight / SwizzleSize;
                int ImageWidthInTiles = ImageWidth / SwizzleSize;

                int ImageRowCount = ImageHeightInTiles / 8;
                if (ImageRowCount > 16)
                    ImageRowCount = 16;

                int ImageColumnCount = 1;
                switch (TextureUnswizzleBufferSize)
                {
                    case 16:
                        ImageColumnCount = 1;
                        break;
                    case 8:
                        ImageColumnCount = 2;
                        break;
                    case 4:
                        ImageColumnCount = 4;
                        break;
                }

                for (int HeightSection = 0; HeightSection < (ImageHeightInTiles / 8) / ImageRowCount; HeightSection++)
                {
                    for (int WidthSection = 0; WidthSection < (ImageWidthInTiles / 4) / ImageColumnCount; WidthSection++)
                    {
                        for (int CurRow = 0; CurRow < ImageRowCount; CurRow++)
                        {
                            for (int SwizzleIndex = 0; SwizzleIndex < 32; SwizzleIndex++)
                            {
                                for (int CurColumn = 0; CurColumn < ImageColumnCount; CurColumn++)
                                {
                                    int CurSwizzle = SwizzleLookup[SwizzleIndex];
                                    int somethingHeight = (HeightSection * ImageRowCount + CurRow) * 8 + (CurSwizzle / 4);
                                    int somethingWidth = (WidthSection * 4 + (CurSwizzle % 4)) * ImageColumnCount + CurColumn;

                                    if (SwizzleSize == 1)
                                    {
                                        TextureUnswizzleBuffer[2] = (byte)msCurFile.ReadByte();
                                        TextureUnswizzleBuffer[1] = (byte)msCurFile.ReadByte();
                                        TextureUnswizzleBuffer[0] = (byte)msCurFile.ReadByte();
                                        TextureUnswizzleBuffer[3] = (byte)msCurFile.ReadByte();
                                        somethingHeight = ImageHeight - somethingHeight - 1;
                                    }
                                    else
                                    {
                                        msCurFile.Read(TextureUnswizzleBuffer, 0, TextureUnswizzleBufferSize);
                                    }

                                    int destinationIndex = TextureUnswizzleBufferSize * (somethingHeight * ImageWidthInTiles + somethingWidth);
                                    Array.Copy(TextureUnswizzleBuffer, 0, TextureUnswizzled, destinationIndex, TextureUnswizzleBufferSize);
                                }
                            }
                        }
                    }
                }

                fsTexture.Write(TextureUnswizzled, 0, (int)msCurFile.Length);
                fsTexture.Close();
            }
        }

        public static int[] BitsPerPixel = new int[] {
             0, 128, 128, 128, 128,  96,  96,  96,  96,  64,  64,  64,  64,  64,  64,  64,
            64,  64,  64,  64,  64,  64,  64,  32,  32,  32,  32,  32,  32,  32,  32,  32,
            32,  32,  32,  32,  32,  32,  32,  32,  32,  32,  32,  32,  32,  32,  32,  32,
            16,  16,  16,  16,  16,  16,  16,  16,  16,  16,  16,  16,   8,   8,   8,   8,
             8,   8,   1,  32,  32,  32,   4,   4,   4,   8,   8,   8,   8,   8,   8,   4,
             4,   4,   8,   8,   8,  16,  16,  32,  32,  32,  32,  32,  32,  32,   8,   8,
             8,   8,   8,   8,   0,   0,   0,   0,   0,   0,   0,   0,   0,   0,   0,   0,
             0,   0,   0,  16
        };

        public static int[] SwizzleLookup = new int[]
        {
             0,  4,  1,  5,  8, 12,  9, 13,
            16, 20, 17, 21, 24, 28, 25, 29,
             2,  6,  3,  7, 10, 14, 11, 15,
            18, 22, 19, 23, 26, 30, 27, 31
        };
    }
}
