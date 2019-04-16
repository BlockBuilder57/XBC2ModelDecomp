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

        public MemoryStream ReadXBC1(Stream sXBC1, BinaryReader brXBC1, int offset, string saveToFileName = "", string savetoFilePath = "")
        {
            if (sXBC1 == null || brXBC1 == null || offset > sXBC1.Length || offset < 0)
                return null;
            sXBC1.Seek(offset, SeekOrigin.Begin);
            int XBC1Magic = brXBC1.ReadInt32(); //nice meme
            if (XBC1Magic != 0x31636278)
            {
                App.PushLog("XBC1 header invalid!");
                return null;
            }
            brXBC1.ReadInt32();
            int outputFileSize = brXBC1.ReadInt32();
            int compressedLength = brXBC1.ReadInt32();
            brXBC1.ReadInt32();

            //string fileInfo = ReadNullTerminatedString(binaryReader);

            sXBC1.Seek(offset + 0x30, SeekOrigin.Begin);
            byte[] fileBuffer = new byte[outputFileSize >= compressedLength ? outputFileSize : compressedLength];

            MemoryStream msFile = new MemoryStream();
            sXBC1.Read(fileBuffer, 0, compressedLength);

            ZOutputStream ZOutFile = new ZOutputStream(msFile);
            ZOutFile.Write(fileBuffer, 0, compressedLength);
            ZOutFile.Flush();

            if (App.SaveRawFiles && !string.IsNullOrWhiteSpace(saveToFileName))
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

        public Structs.SAR1 ReadSAR1(Stream sSAR1, BinaryReader brSAR1, string folderPath, bool folderConditional)
        {
            App.PushLog("Parsing SAR1...");
            sSAR1.Seek(0, SeekOrigin.Begin);
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

            string safePath = App.CurOutputPath + folderPath;
            if (SAR1.Path[1] == ':')
                safePath += SAR1.Path.Substring(3);
            else
                safePath += SAR1.Path;

            if (folderConditional && !Directory.Exists(safePath))
                Directory.CreateDirectory(safePath);

            SAR1.TOCItems = new Structs.SARTOC[SAR1.NumFiles];
            for (int i = 0; i < SAR1.NumFiles; i++)
            {
                sSAR1.Seek(SAR1.TOCOffset + (i * 0x40), SeekOrigin.Begin);
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
                sSAR1.Seek(SAR1.TOCItems[i].Offset, SeekOrigin.Begin);
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

                sSAR1.Seek(SAR1.TOCItems[i].Offset + SAR1.BCItems[i].OffsetToData + 0x4, SeekOrigin.Begin);

                SAR1.BCItems[i].Data = new MemoryStream(SAR1.BCItems[i].FileSize - SAR1.BCItems[i].OffsetToData);
                sSAR1.CopyTo(SAR1.BCItems[i].Data);

                if (folderConditional)
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

        public Structs.SKEL ReadSKEL(Stream sSKEL, BinaryReader brSKEL)
        {
            App.PushLog("Parsing SKEL...");
            sSKEL.Seek(0, SeekOrigin.Begin);
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
            sSKEL.Seek(SKEL.TOCItems[2].Offset - 0x24, SeekOrigin.Begin);
            for (int i = 0; i < SKEL.Parents.Length; i++)
            {
                SKEL.Parents[i] = brSKEL.ReadInt16();
            }

            SKEL.Nodes = new Structs.SKELNodes[SKEL.TOCItems[3].Count];
            for (int i = 0; i < SKEL.Nodes.Length; i++)
            {
                sSKEL.Seek(SKEL.TOCItems[3].Offset - 0x24 + (i * 0x10), SeekOrigin.Begin);
                SKEL.Nodes[i] = new Structs.SKELNodes
                {
                    Offset = brSKEL.ReadInt32(),
                    Unknown1 = brSKEL.ReadBytes(0xC)
                };
                sSKEL.Seek(SKEL.Nodes[i].Offset - 0x24, SeekOrigin.Begin);
                SKEL.Nodes[i].Name = ReadNullTerminatedString(brSKEL);
            }

            SKEL.Transforms = new Structs.SKELTransforms[SKEL.TOCItems[4].Count];
            sSKEL.Seek(SKEL.TOCItems[4].Offset - 0x24, SeekOrigin.Begin);
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

        public Structs.MSRD ReadMSRD(Stream sMSRD, BinaryReader brMSRD)
        {
            App.PushLog("Parsing MSRD...");
            sMSRD.Seek(0, SeekOrigin.Begin);
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
                sMSRD.Seek(MSRD.MainOffset + MSRD.DataItemsOffset, SeekOrigin.Begin); //0x5C, 92
                for (int i = 0; i < MSRD.DataItemsCount; i++)
                {
                    MSRD.DataItems[i] = new Structs.MSRDDataItem
                    {
                        Offset = brMSRD.ReadInt32(),
                        Size = brMSRD.ReadInt32(),
                        id1 = brMSRD.ReadInt16(),
                        Type = (Structs.MSRDDataItemTypes)brMSRD.ReadInt16()
                    };
                    sMSRD.Seek(0x8, SeekOrigin.Current);
                }
            }
            
            if (MSRD.TextureIdsOffset != 0)
            {
                sMSRD.Seek(MSRD.MainOffset + MSRD.TextureIdsOffset, SeekOrigin.Begin); //0x244, 580
                MSRD.TextureIds = new short[MSRD.TextureIdsCount];
                for (int i = 0; i < MSRD.TextureIdsCount; i++)
                {
                    MSRD.TextureIds[i] = brMSRD.ReadInt16();
                }
            }
            
            if (MSRD.TextureCountOffset != 0)
            {
                sMSRD.Seek(MSRD.MainOffset + MSRD.TextureCountOffset, SeekOrigin.Begin); //0x25E, 606
                MSRD.TextureCount = brMSRD.ReadInt32(); //0x25E
                MSRD.TextureChunkSize = brMSRD.ReadInt32();
                MSRD.Unknown2 = brMSRD.ReadInt32();
                MSRD.TextureStringBufferOffset = brMSRD.ReadInt32();

                MSRD.TextureInfo = new Structs.MSRDTextureInfo[MSRD.TextureCount];
                for (int i = 0; i < MSRD.TextureCount; i++)
                {
                    MSRD.TextureInfo[i].Unknown1 = brMSRD.ReadInt32();
                    MSRD.TextureInfo[i].Size = brMSRD.ReadInt32();
                    MSRD.TextureInfo[i].Offset = brMSRD.ReadInt32();
                    MSRD.TextureInfo[i].StringOffset = brMSRD.ReadInt32();
                }

                MSRD.TextureNames = new string[MSRD.TextureCount];
                for (int i = 0; i < MSRD.TextureCount; i++)
                {
                    sMSRD.Seek(MSRD.MainOffset + MSRD.TextureCountOffset + MSRD.TextureInfo[i].StringOffset, SeekOrigin.Begin);
                    MSRD.TextureNames[i] = FormatTools.ReadNullTerminatedString(brMSRD);
                }
            }

            MSRD.TOC = new Structs.MSRDTOC[MSRD.FileCount];
            for (int curFileOffset = 0; curFileOffset < MSRD.FileCount; curFileOffset++)
            {
                sMSRD.Seek(MSRD.MainOffset + MSRD.TOCOffset + (curFileOffset * 12), SeekOrigin.Begin); //prevents errors I guess
                MSRD.TOC[curFileOffset].CompSize = brMSRD.ReadInt32();
                MSRD.TOC[curFileOffset].FileSize = brMSRD.ReadInt32();
                MSRD.TOC[curFileOffset].Offset = brMSRD.ReadInt32();

                App.PushLog($"Decompressing file{curFileOffset} in MSRD...");
                MSRD.TOC[curFileOffset].Data = ReadXBC1(sMSRD, brMSRD, MSRD.TOC[curFileOffset].Offset, $"file{curFileOffset}.bin", App.CurOutputPath + @"\RawFiles");
            }

            return MSRD;
        }

        public Structs.MXMD ReadMXMD(Stream sMXMD, BinaryReader brMXMD)
        {
            App.PushLog("Parsing MXMD...");
            sMXMD.Seek(0, SeekOrigin.Begin);
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
                sMXMD.Seek(MXMD.ModelStructOffset + MXMD.ModelStruct.MorphControllersOffset, SeekOrigin.Begin);
                MXMD.ModelStruct.MorphControls = new Structs.MXMDMorphControls
                {
                    TableOffset = brMXMD.ReadInt32(),
                    Count = brMXMD.ReadInt32(),

                    Unknown2 = brMXMD.ReadBytes(0x10)
                };

                MXMD.ModelStruct.MorphControls.Controls = new Structs.MXMDMorphControl[MXMD.ModelStruct.MorphControls.Count];
                long nextPosition = sMXMD.Position;
                for (int i = 0; i < MXMD.ModelStruct.MorphControls.Count; i++)
                {
                    sMXMD.Seek(nextPosition, SeekOrigin.Begin);
                    nextPosition += 0x1C;
                    MXMD.ModelStruct.MorphControls.Controls[i] = new Structs.MXMDMorphControl
                    {
                        NameOffset1 = brMXMD.ReadInt32(),
                        NameOffset2 = brMXMD.ReadInt32(), //the results of these should be identical
                        Unknown1 = brMXMD.ReadBytes(0x14)
                    };

                    sMXMD.Seek(MXMD.ModelStructOffset + MXMD.ModelStruct.MorphControllersOffset + MXMD.ModelStruct.MorphControls.Controls[i].NameOffset1, SeekOrigin.Begin);
                    MXMD.ModelStruct.MorphControls.Controls[i].Name = FormatTools.ReadNullTerminatedString(brMXMD);
                }
            }

            if (MXMD.ModelStruct.MorphNamesOffset != 0)
            {
                sMXMD.Seek(MXMD.ModelStructOffset + MXMD.ModelStruct.MorphNamesOffset, SeekOrigin.Begin);
                MXMD.ModelStruct.MorphNames = new Structs.MXMDMorphNames
                {
                    TableOffset = brMXMD.ReadInt32(),
                    Count = brMXMD.ReadInt32(),

                    Unknown2 = brMXMD.ReadBytes(0x20)
                };

                MXMD.ModelStruct.MorphNames.Names = new Structs.MXMDMorphName[MXMD.ModelStruct.MorphNames.Count];
                long nextPosition = sMXMD.Position;
                for (int i = 0; i < MXMD.ModelStruct.MorphNames.Count; i++)
                {
                    sMXMD.Seek(nextPosition, SeekOrigin.Begin);
                    nextPosition += 0x10;
                    MXMD.ModelStruct.MorphNames.Names[i] = new Structs.MXMDMorphName
                    {
                        NameOffset = brMXMD.ReadInt32(),
                        Unknown1 = brMXMD.ReadInt32(),
                        Unknown2 = brMXMD.ReadInt32(),
                        Unknown3 = brMXMD.ReadInt32(),
                    };

                    sMXMD.Seek(MXMD.ModelStructOffset + MXMD.ModelStruct.MorphControllersOffset + MXMD.ModelStruct.MorphNames.Names[i].NameOffset, SeekOrigin.Begin);
                    MXMD.ModelStruct.MorphNames.Names[i].Name = FormatTools.ReadNullTerminatedString(brMXMD);
                }
            }

            if (MXMD.ModelStruct.MeshesOffset != 0)
            {
                sMXMD.Seek(MXMD.ModelStructOffset + MXMD.ModelStruct.MeshesOffset, SeekOrigin.Begin);
                MXMD.ModelStruct.Meshes = new Structs.MXMDMeshes
                {
                    TableOffset = brMXMD.ReadInt32(),
                    TableCount = brMXMD.ReadInt32(),
                    Unknown1 = brMXMD.ReadInt32(),

                    BoundingBoxStart = new Vector3(brMXMD.ReadSingle(), brMXMD.ReadSingle(), brMXMD.ReadSingle()),
                    BoundingBoxEnd = new Vector3(brMXMD.ReadSingle(), brMXMD.ReadSingle(), brMXMD.ReadSingle()),
                    BoundingRadius = brMXMD.ReadSingle()
                };

                sMXMD.Seek(MXMD.ModelStructOffset + MXMD.ModelStruct.Meshes.TableOffset, SeekOrigin.Begin);
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
                sMXMD.Seek(MXMD.ModelStructOffset + MXMD.ModelStruct.NodesOffset, SeekOrigin.Begin);
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
                    sMXMD.Seek(nextPosition, SeekOrigin.Begin);
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

                    sMXMD.Seek(MXMD.ModelStructOffset + MXMD.ModelStruct.NodesOffset + MXMD.ModelStruct.Nodes.Nodes[i].NameOffset, SeekOrigin.Begin);
                    MXMD.ModelStruct.Nodes.Nodes[i].Name = FormatTools.ReadNullTerminatedString(brMXMD);
                }

                nextPosition = MXMD.ModelStructOffset + MXMD.ModelStruct.NodesOffset + MXMD.ModelStruct.Nodes.NodeTmsOffset;
                for (int i = 0; i < MXMD.ModelStruct.Nodes.BoneCount; i++)
                {
                    sMXMD.Seek(nextPosition, SeekOrigin.Begin);
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

        public Structs.Mesh ReadMesh(Stream sMesh, BinaryReader brMesh)
        {
            App.PushLog("Parsing mesh...");
            sMesh.Seek(0, SeekOrigin.Begin);

            Structs.Mesh Mesh = new Structs.Mesh
            {
                VertexTableOffset = brMesh.ReadInt32(),
                VertexTableCount = brMesh.ReadInt32(),
                FaceTableOffset = brMesh.ReadInt32(),
                FaceTableCount = brMesh.ReadInt32(),

                Reserved1 = brMesh.ReadBytes(0xC),

                UnknownOffset1 = brMesh.ReadInt32(),
                UnknownOffset2 = brMesh.ReadInt32(),
                UnknownOffset2Count = brMesh.ReadInt32(),

                KindaMorphDataOffset = brMesh.ReadInt32(),
                DataSize = brMesh.ReadInt32(),
                DataOffset = brMesh.ReadInt32(),
                ExtraDataVoxOffset = brMesh.ReadInt32(),
                ExtraDataOffset = brMesh.ReadInt32(),

                Reserved2 = brMesh.ReadBytes(0x14)
            };

            Mesh.VertexTables = new Structs.MeshVertexTable[Mesh.VertexTableCount];
            Mesh.VertexDescriptors = new List<Structs.MeshVertexDescriptor>();
            for (int i = 0; i < Mesh.VertexTableCount; i++)
            {
                sMesh.Seek(Mesh.VertexTableOffset + (i * 0x20), SeekOrigin.Begin);
                Mesh.VertexTables[i] = new Structs.MeshVertexTable
                {
                    DataOffset = brMesh.ReadInt32(),
                    DataCount = brMesh.ReadInt32(),
                    BlockSize = brMesh.ReadInt32(),

                    DescOffset = brMesh.ReadInt32(),
                    DescCount = brMesh.ReadInt32(),

                    Unknown1 = brMesh.ReadBytes(0xC)
                };
                Mesh.VertexTables[i].Descriptors = new Structs.MeshVertexDescriptor[Mesh.VertexTables[i].DescCount];
                sMesh.Seek(Mesh.VertexTables[i].DescOffset, SeekOrigin.Begin);
                for (int j = 0; j < Mesh.VertexTables[i].DescCount; j++)
                {
                    Structs.MeshVertexDescriptor desc = new Structs.MeshVertexDescriptor
                    {
                        Type = brMesh.ReadInt16(),
                        Size = brMesh.ReadInt16()
                    };
                    Mesh.VertexDescriptors.Add(desc);
                    Mesh.VertexTables[i].Descriptors[j] = desc;
                }
            }

            Mesh.FaceTables = new Structs.MeshFaceTable[Mesh.FaceTableCount];
            sMesh.Seek(Mesh.FaceTableOffset, SeekOrigin.Begin);
            for (int i = 0; i < Mesh.FaceTableCount; i++)
            {
                Mesh.FaceTables[i] = new Structs.MeshFaceTable
                {
                    Offset = brMesh.ReadInt32(),
                    Count = brMesh.ReadInt32(),

                    Unknown1 = brMesh.ReadBytes(0xC)
                };
            }

            sMesh.Seek(Mesh.ExtraDataOffset, SeekOrigin.Begin);
            Mesh.MorphData = new Structs.MeshMorphData
            {
                WeightManagerCount = brMesh.ReadInt32(),
                WeightManagerOffset = brMesh.ReadInt32(),

                Unknown1 = brMesh.ReadInt16(),
                Unknown2 = brMesh.ReadInt16(),

                Offset02 = brMesh.ReadInt32()
            };

            App.PushLog(Mesh.MorphData.WeightManagerOffset.ToString("X"));
            Mesh.MorphData.WeightManagers = new Structs.MeshWeightManager[Mesh.MorphData.WeightManagerCount];
            sMesh.Seek(Mesh.MorphData.WeightManagerOffset, SeekOrigin.Begin);
            for (int i = 0; i < Mesh.MorphData.WeightManagerCount; i++)
            {
                Mesh.MorphData.WeightManagers[i] = new Structs.MeshWeightManager
                {
                    Unknown1 = brMesh.ReadInt32(),
                    Offset = brMesh.ReadInt32(),
                    Count = brMesh.ReadInt32(),

                    Unknown2 = brMesh.ReadBytes(0x11),
                    LOD = brMesh.ReadByte(),
                    Unknown3 = brMesh.ReadBytes(0xA)
                };
            }

            return Mesh;
        }

        public void ModelToASCII(Stream memoryStream, BinaryReader binaryReader, string filepath)
        {
            Structs.Mesh Mesh = ReadMesh(memoryStream, binaryReader);

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


            FileStream fsARC = new FileStream(App.CurFilePath.Remove(App.CurFilePath.LastIndexOf('.')) + ".arc", FileMode.Open, FileAccess.Read);
            BinaryReader brARC = new BinaryReader(fsARC);

            Structs.SAR1 SAR1 = ReadSAR1(fsARC, brARC, @"\RawFiles\", App.SaveRawFiles);
            BinaryReader brSKEL = new BinaryReader(SAR1.ItemBySearch(".skl").Data);
            Structs.SKEL SKEL = ReadSKEL(brSKEL.BaseStream, brSKEL);

            int boneCount = SKEL.TOCItems[2].Count;

            Vector3[] bonePos = new Vector3[boneCount];
            Quaternion[] boneRot = new Quaternion[boneCount];

            string[] bone_names = new string[boneCount];
            Dictionary<string, int> SKELNodeNames = new Dictionary<string, int>();

            for (int i = 0; i < boneCount; i++)
            {
                Quaternion posQuat = SKEL.Transforms[i].Position;
                Vector3 posVector = new Vector3(posQuat.X, posQuat.Y, posQuat.Z);
                bonePos[i] = posVector;

                SKELNodeNames.Add(SKEL.Nodes[i].Name, i);
                bone_names[i] = SKEL.Nodes[i].Name;

                if (SKEL.Parents[i] < 0) //is root
                {
                    bonePos[i] = posVector;
                    boneRot[i] = SKEL.Transforms[i].Rotation;
                }
                else
                {
                    int curParentIndex = SKEL.Parents[i];
                    //add rotation of parent
                    boneRot[i] = boneRot[curParentIndex] * SKEL.Transforms[i].Rotation;
                    //make position a quaternion again (for math reasons)
                    Quaternion bonePosQuat = new Quaternion(posVector, 0f);
                    //multiply position and rotation (?)
                    Quaternion bonePosRotQuat = boneRot[curParentIndex] * bonePosQuat;
                    //do something or other i dunno
                    Quaternion newPosition = bonePosRotQuat * new Quaternion(-boneRot[curParentIndex].X, -boneRot[curParentIndex].Y, -boneRot[curParentIndex].Z, boneRot[curParentIndex].W);
                    //add position to parent's position
                    bonePos[i] = new Vector3(newPosition.X, newPosition.Y, newPosition.Z) + bonePos[curParentIndex];
                }
            }

            //begin ascii
            //bone time
            StreamWriter asciiWriter = new StreamWriter($@"{App.CurOutputPath}\{App.CurFileNameNoExt + ".ascii"}");
            asciiWriter.WriteLine(boneCount);
            for (int j = 0; j < boneCount; j++)
            {
                asciiWriter.WriteLine(bone_names[j]);
                asciiWriter.WriteLine(SKEL.Parents[j]);
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

            int[,] meshWeightIds = new int[Mesh.VertexTables[Mesh.MorphData.Unknown1].DataCount, 4];
            float[,] meshWeightValues = new float[Mesh.VertexTables[Mesh.MorphData.Unknown1].DataCount, 4];
            Vector3[][] meshVertices = new Vector3[Mesh.VertexTableCount][];
            Vector3[][] meshNormals = new Vector3[Mesh.VertexTableCount][];
            float[][,] meshUVLayerPosX = new float[Mesh.VertexTableCount][,];
            float[][,] meshUVLayerPosY = new float[Mesh.VertexTableCount][,];
            int[][] meshWeights = new int[Mesh.VertexTableCount][];
            int[] meshUVVertices = new int[Mesh.VertexTableCount];

            //begin meshes
            for (int i = 0; i < Mesh.VertexTableCount; i++)
            {
                meshVertices[i] = new Vector3[Mesh.VertexTables[i].DataCount];
                meshNormals[i] = new Vector3[Mesh.VertexTables[i].DataCount];
                meshUVLayerPosX[i] = new float[Mesh.VertexTables[i].DataCount, 4];
                meshUVLayerPosY[i] = new float[Mesh.VertexTables[i].DataCount, 4];
                meshWeights[i] = new int[Mesh.VertexTables[i].DataCount];
                int meshVerticesOffset = -1;
                int meshNormalsOffset = -1;
                int meshWeightsOffset = -1;
                int meshWeightValuesOffset = -1;
                int meshWeightIdsOffset = -1;
                int meshDescriptorOffset = 0;
                int[] meshUVLayers = new int[4];
                for (int j = 0; j < Mesh.VertexTables[i].DescCount; j++)
                {
                    switch (Mesh.VertexTables[i].Descriptors[j].Type)
                    {
                        case 0:
                            meshVerticesOffset = meshDescriptorOffset;
                            break;
                        case 3:
                            meshWeightsOffset = meshDescriptorOffset;
                            break;
                        case 5:
                        case 6:
                        case 7:
                            meshUVLayers[meshUVVertices[i]] = meshDescriptorOffset;
                            meshUVVertices[i]++;
                            break;
                        case 28:
                            meshNormalsOffset = meshDescriptorOffset;
                            break;
                        case 41:
                            meshWeightValuesOffset = meshDescriptorOffset;
                            break;
                        case 42:
                            meshWeightIdsOffset = meshDescriptorOffset;
                            break;
                    }
                    meshDescriptorOffset += Mesh.VertexTables[i].Descriptors[j].Size;
                }
                for (int j = 0; j < Mesh.VertexTables[i].DataCount; j++)
                {
                    if (meshVerticesOffset >= 0)
                    {
                        memoryStream.Seek(Mesh.DataOffset + Mesh.VertexTables[i].DataOffset + j * Mesh.VertexTables[i].BlockSize + meshVerticesOffset, SeekOrigin.Begin);
                        meshVertices[i][j] = new Vector3(binaryReader.ReadSingle(), binaryReader.ReadSingle(), binaryReader.ReadSingle());
                    }
                    if (meshWeightsOffset >= 0)
                    {
                        memoryStream.Seek(Mesh.DataOffset + Mesh.VertexTables[i].DataOffset + j * Mesh.VertexTables[i].BlockSize + meshWeightsOffset, SeekOrigin.Begin);
                        meshWeights[i][j] = binaryReader.ReadInt32();
                    }

                    for (int k = 0; k < meshUVVertices[i]; k++)
                    {
                        memoryStream.Seek(Mesh.DataOffset + Mesh.VertexTables[i].DataOffset + j * Mesh.VertexTables[i].BlockSize + meshUVLayers[k], SeekOrigin.Begin);
                        meshUVLayerPosX[i][j, k] = binaryReader.ReadSingle();
                        meshUVLayerPosY[i][j, k] = binaryReader.ReadSingle();
                    }

                    if (meshNormalsOffset >= 0)
                    {
                        memoryStream.Seek(Mesh.DataOffset + Mesh.VertexTables[i].DataOffset + j * Mesh.VertexTables[i].BlockSize + meshNormalsOffset, SeekOrigin.Begin);
                        float num40 = (float)binaryReader.ReadSByte() / 128f;
                        float num41 = (float)binaryReader.ReadSByte() / 128f;
                        float num42 = (float)binaryReader.ReadSByte() / 128f;
                        meshNormals[i][j] = new Vector3(num40, num41, num42);
                    }

                    if (meshWeightValuesOffset >= 0)
                    {
                        memoryStream.Seek(Mesh.DataOffset + Mesh.VertexTables[i].DataOffset + j * Mesh.VertexTables[i].BlockSize + meshWeightValuesOffset, SeekOrigin.Begin);
                        meshWeightValues[j, 0] = (float)binaryReader.ReadUInt16() / 65535f;
                        meshWeightValues[j, 1] = (float)binaryReader.ReadUInt16() / 65535f;
                        meshWeightValues[j, 2] = (float)binaryReader.ReadUInt16() / 65535f;
                        meshWeightValues[j, 3] = (float)binaryReader.ReadUInt16() / 65535f;
                    }
                    if (meshWeightIdsOffset >= 0)
                    {
                        memoryStream.Seek(Mesh.DataOffset + Mesh.VertexTables[i].DataOffset + j * Mesh.VertexTables[i].BlockSize + meshWeightIdsOffset, SeekOrigin.Begin);
                        try
                        {
                            meshWeightIds[j, 0] = SKELNodeNames[NodesIdsNames[binaryReader.ReadByte()]];
                            meshWeightIds[j, 1] = SKELNodeNames[NodesIdsNames[binaryReader.ReadByte()]];
                            meshWeightIds[j, 2] = SKELNodeNames[NodesIdsNames[binaryReader.ReadByte()]];
                            meshWeightIds[j, 3] = SKELNodeNames[NodesIdsNames[binaryReader.ReadByte()]];
                        }
                        catch
                        {
                            meshWeightIds[j, 0] = 0;
                            meshWeightIds[j, 1] = 0;
                            meshWeightIds[j, 2] = 0;
                            meshWeightIds[j, 3] = 0;
                        }
                    }
                }
            }

            int MorphDataCount = 0;
            int MorphDataOffset2 = 0;
            int[] MorphWeightUnknown = null;
            int[] MorphWeightOffset = null;
            int[] MorphWeightCount = null;
            if (Mesh.KindaMorphDataOffset > 0) //flex related?
            {
                memoryStream.Seek(Mesh.KindaMorphDataOffset, SeekOrigin.Begin);
                MorphDataCount = binaryReader.ReadInt32();
                int MorphDataOffset1 = binaryReader.ReadInt32();
                binaryReader.ReadInt32();
                MorphDataOffset2 = binaryReader.ReadInt32();
                memoryStream.Seek(MorphDataOffset1, SeekOrigin.Begin);
                MorphWeightUnknown = new int[MorphDataCount];
                MorphWeightOffset = new int[MorphDataCount];
                MorphWeightCount = new int[MorphDataCount];
                for (int i = 0; i < MorphDataCount; i++)
                {
                    MorphWeightUnknown[i] = binaryReader.ReadInt32();
                    MorphWeightOffset[i] = binaryReader.ReadInt32();
                    MorphWeightCount[i] = binaryReader.ReadInt32();
                    binaryReader.ReadInt32();
                    binaryReader.ReadInt32();
                }
            }

            int[] array10 = new int[Mesh.VertexTableCount];
            Vector3[][] array42 = new Vector3[MorphDataCount][];
            if (Mesh.KindaMorphDataOffset > 0)
            {
                for (int i = 0; i < MorphDataCount; i++)
                {
                    array10[MorphWeightUnknown[i]] = i + 1;
                    memoryStream.Seek((long)(MorphDataOffset2 + MorphWeightOffset[i] * 16), SeekOrigin.Begin);
                    int num52 = Mesh.DataOffset + binaryReader.ReadInt32();
                    int num53 = binaryReader.ReadInt32();
                    if (num53 != Mesh.VertexTables[MorphWeightUnknown[i]].DataCount)
                    {
                        App.PushLog("Flex vertices count is incorrect!");
                    }
                    array42[i] = new Vector3[num53];
                    for (int j = 0; j < num53; j++)
                    {
                        memoryStream.Seek((long)(num52 + j * 32), SeekOrigin.Begin);
                        meshVertices[MorphWeightUnknown[i]][j] = new Vector3(binaryReader.ReadSingle(), binaryReader.ReadSingle(), binaryReader.ReadSingle());
                        meshNormals[MorphWeightUnknown[i]][j] = new Vector3(0f, 0f, 0f);
                    }
                }
            }

            //total mesh count
            int flexAndMeshCount = 0;
            for (int i = 0; i < MeshesTableCount; i++)
            {
                if (MXMD.ModelStruct.Meshes.Meshes[i].LOD < 2)
                {
                    flexAndMeshCount++;
                    if (BitConverter.GetBytes(MXMD.ModelStruct.Meshes.Meshes[i].ID).Last() != 0 && App.ExportFlexes)
                    {
                        flexAndMeshCount += MorphWeightCount[0];
                    }
                }
            }
            asciiWriter.WriteLine(flexAndMeshCount);
            int num55 = 1;
            if (Mesh.KindaMorphDataOffset > 0 && App.ExportFlexes)
            {
                num55 += MorphWeightCount[0];
            }

            //write it
            for (int i = 0; i < num55; i++)
            {
                if (i > 0)
                {
                    for (int j = 0; j < MorphDataCount; j++)
                    {
                        for (int k = 0; k < array42[j].Length; k++)
                        {
                            array42[j][k] = new Vector3();
                        }
                        memoryStream.Seek((long)(MorphDataOffset2 + (MorphWeightOffset[j] + 1 + i) * 16), SeekOrigin.Begin);
                        int num56 = Mesh.DataOffset + binaryReader.ReadInt32();
                        int num57 = binaryReader.ReadInt32();
                        for (int k = 0; k < num57; k++)
                        {
                            memoryStream.Seek((long)(num56 + k * 32), SeekOrigin.Begin);
                            Vector3 Vector3 = new Vector3(binaryReader.ReadSingle(), binaryReader.ReadSingle(), binaryReader.ReadSingle());
                            memoryStream.Seek(16L, SeekOrigin.Current);
                            int num58 = binaryReader.ReadInt32();
                            array42[j][num58] = Vector3;
                        }
                    }
                }
                for (int j = 0; j < MeshesTableCount; j++)
                {
                    if (MXMD.ModelStruct.Meshes.Meshes[j].LOD <= 1)
                    {
                        int curMesh = MXMD.ModelStruct.Meshes.Meshes[j].UVFaces;
                        int curMeshIndex = MXMD.ModelStruct.Meshes.Meshes[j].VTBuffer;
                        if (BitConverter.GetBytes(MXMD.ModelStruct.Meshes.Meshes[j].ID).Last() != 0 || i <= 0)
                        {
                            memoryStream.Seek(Mesh.DataOffset + Mesh.FaceTables[curMesh].Offset, SeekOrigin.Begin);
                            int[] array43 = new int[Mesh.FaceTables[curMesh].Count];
                            int curMeshVertCount = 0;
                            int prvMeshVertCount = Mesh.VertexTables[curMeshIndex].DataCount;
                            for (int k = 0; k < Mesh.FaceTables[curMesh].Count; k++)
                            {
                                int num63 = (int)binaryReader.ReadInt16();
                                array43[k] = num63;
                                if (num63 > curMeshVertCount)
                                {
                                    curMeshVertCount = num63;
                                }
                                if (num63 < prvMeshVertCount)
                                {
                                    prvMeshVertCount = num63;
                                }
                            }
                            if (i > 0)
                            {
                                asciiWriter.WriteLine($"sm_{j}_{meshFlexNames[i - 1]}"); //mesh name (+ flex name)
                            }
                            else
                            {
                                asciiWriter.WriteLine("sm_" + j); //mesh name
                            }
                            asciiWriter.WriteLine(meshUVVertices[curMeshIndex]);
                            asciiWriter.WriteLine(0); //texture count, always 0 for us, though maybe I should change that?
                            asciiWriter.WriteLine(curMeshVertCount - prvMeshVertCount + 1); //vertex count
                            for (int vrtIndex = prvMeshVertCount; vrtIndex <= curMeshVertCount; vrtIndex++)
                            {
                                //vertex position
                                Vector3 vertexPos = meshVertices[curMeshIndex][vrtIndex];
                                if (i > 0)
                                    vertexPos += array42[array10[curMeshIndex] - 1][vrtIndex];
                                asciiWriter.Write(vertexPos.X.ToString("0.######"));
                                asciiWriter.Write(" " + vertexPos.Y.ToString("0.######"));
                                asciiWriter.Write(" " + vertexPos.Z.ToString("0.######"));
                                asciiWriter.WriteLine();

                                //vertex normal
                                asciiWriter.Write(meshNormals[curMeshIndex][vrtIndex].X.ToString("0.######"));
                                asciiWriter.Write(" " + meshNormals[curMeshIndex][vrtIndex].Y.ToString("0.######"));
                                asciiWriter.Write(" " + meshNormals[curMeshIndex][vrtIndex].Z.ToString("0.######"));
                                asciiWriter.WriteLine();

                                asciiWriter.WriteLine("0 0 0 0"); // vertex color (why)

                                //uv coords
                                for (int curUVLayer = 0; curUVLayer < meshUVVertices[curMeshIndex]; curUVLayer++)
                                    asciiWriter.WriteLine(meshUVLayerPosX[curMeshIndex][vrtIndex, curUVLayer].ToString("0.######") + " " + meshUVLayerPosY[curMeshIndex][vrtIndex, curUVLayer].ToString("0.######"));

                                //weight ids
                                asciiWriter.Write(meshWeightIds[meshWeights[curMeshIndex][vrtIndex], 0]);
                                asciiWriter.Write(" " + meshWeightIds[meshWeights[curMeshIndex][vrtIndex], 1]);
                                asciiWriter.Write(" " + meshWeightIds[meshWeights[curMeshIndex][vrtIndex], 2]);
                                asciiWriter.Write(" " + meshWeightIds[meshWeights[curMeshIndex][vrtIndex], 3]);
                                asciiWriter.WriteLine();

                                //weight values
                                asciiWriter.WriteLine(meshWeightValues[meshWeights[curMeshIndex][vrtIndex], 0].ToString("0.######"));
                            }

                            //face count
                            asciiWriter.WriteLine(Mesh.FaceTables[curMesh].Count / 3);
                            for (int k = 0; k < Mesh.FaceTables[curMesh].Count; k += 3)
                            {
                                int faceVertexZ = array43[k] - prvMeshVertCount;
                                int faceVertexY = array43[k + 1] - prvMeshVertCount;
                                int faceVertexX = array43[k + 2] - prvMeshVertCount;
                                //face vertex ids
                                asciiWriter.WriteLine($"{faceVertexX} {faceVertexY} {faceVertexZ}");
                            }
                        }
                    }
                }
            }

            App.PushLog("Writing .ascii file...");
            asciiWriter.Flush();
            asciiWriter.Dispose();
            GC.Collect();
        }

        public void ReadTextures(Stream sWISMT, BinaryReader brWISMT, Structs.MSRD MSRD, string texturesFolderPath)
        {
            if (sWISMT == null || brWISMT == null)
                return;
            App.PushLog("Reading textures...");

            BinaryReader brCurFile = new BinaryReader(MSRD.TOC[1].Data);

            int[] TextureHeightArray = new int[MSRD.TextureIdsCount];
            int[] TextureWidthArray = new int[MSRD.TextureIdsCount];
            int[] TextureTypeArray = new int[MSRD.TextureIdsCount];
            for (int i = 0; i < MSRD.TextureIdsCount; i++)
            {
                MSRD.TOC[1].Data.Seek(MSRD.DataItems[i + 3].Offset + MSRD.DataItems[i + 3].Size - 32, SeekOrigin.Begin);
                TextureHeightArray[i] = brCurFile.ReadInt32();
                TextureWidthArray[i] = brCurFile.ReadInt32();
                brCurFile.ReadInt32();
                brCurFile.ReadInt32();
                TextureTypeArray[i] = brCurFile.ReadInt32();
            }

            for (int i = 0; i < MSRD.TextureIdsCount - 2; i++)
            {
                brCurFile = new BinaryReader(MSRD.TOC[i + 2].Data);
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
                        App.PushLog("unknown texture type " + TextureTypeArray[i]);
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
                    BinaryWriter bwTexture = new BinaryWriter(fsTexture);
                    bwTexture.Write(0x20000); //type stuff
                    bwTexture.Write(0x0); //color map info
                    bwTexture.Write(0x0); //origin position
                    bwTexture.Write((short)ImageWidth);
                    bwTexture.Write((short)ImageHeight);
                    bwTexture.Write(0x820); //pixel size and descriptor
                    bwTexture.Seek(0x12, SeekOrigin.Begin);
                    TextureUnswizzleBufferSize = BitsPerPixel[TextureType] / 8;
                    SwizzleSize = 1;
                }
                else
                {
                    fsTexture = new FileStream($@"{texturesFolderPath}\{i.ToString("d2")}_{MSRD.TextureNames[MSRD.TextureIds[i]]}.dds", FileMode.Create);
                    BinaryWriter bwTexture = new BinaryWriter(fsTexture);
                    bwTexture.Write(0x7C20534444); //magic
                    bwTexture.Write(0x1007); //flags
                    bwTexture.Write(ImageHeight);
                    bwTexture.Write(ImageWidth);
                    bwTexture.Write(MSRD.TOC[i + 2].Data.Length);
                    bwTexture.Write(0x1);
                    fsTexture.Seek(0x2C, SeekOrigin.Current);
                    bwTexture.Write(0x20);
                    bwTexture.Write(0x4);
                    bwTexture.Write(DDSFourCC);
                    fsTexture.Seek(0x28, SeekOrigin.Current);
                    if (DDSFourCC == 0x30315844) //DXT10 header
                    {
                        bwTexture.Write(TextureType);
                        bwTexture.Write(3); //resourceDimension
                        bwTexture.Write(0); //miscFlag
                        bwTexture.Write(1); //arraySize
                        bwTexture.Write(0); //miscFlags2
                    }
                }

                byte[] TextureUnswizzleBuffer = new byte[16];
                byte[] TextureUnswizzled = new byte[MSRD.TOC[i + 2].Data.Length];

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
                                        TextureUnswizzleBuffer[2] = (byte)MSRD.TOC[i + 2].Data.ReadByte();
                                        TextureUnswizzleBuffer[1] = (byte)MSRD.TOC[i + 2].Data.ReadByte();
                                        TextureUnswizzleBuffer[0] = (byte)MSRD.TOC[i + 2].Data.ReadByte();
                                        TextureUnswizzleBuffer[3] = (byte)MSRD.TOC[i + 2].Data.ReadByte();
                                        somethingHeight = ImageHeight - somethingHeight - 1;
                                    }
                                    else
                                    {
                                        MSRD.TOC[i + 2].Data.Read(TextureUnswizzleBuffer, 0, TextureUnswizzleBufferSize);
                                    }

                                    int destinationIndex = TextureUnswizzleBufferSize * (somethingHeight * ImageWidthInTiles + somethingWidth);
                                    Array.Copy(TextureUnswizzleBuffer, 0, TextureUnswizzled, destinationIndex, TextureUnswizzleBufferSize);
                                }
                            }
                        }
                    }
                }

                fsTexture.Write(TextureUnswizzled, 0, (int)MSRD.TOC[i + 2].Data.Length);
                fsTexture.Dispose();
            }

            brCurFile.Dispose();
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
