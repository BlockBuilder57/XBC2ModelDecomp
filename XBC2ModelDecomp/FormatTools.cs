using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media;
using SharpGLTF.Schema2;
using SharpGLTF.Geometry;
using SharpGLTF.Geometry.VertexTypes;
using zlib;
using SharpGLTF.Materials;

namespace XBC2ModelDecomp
{
    using GLTFVert = Vertex<VertexPositionNormal, VertexColor1, VertexJoints16x4>;

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

        public void SaveStreamToFile(Stream stream, string fileName, string filePath)
        {
            if (!string.IsNullOrWhiteSpace(fileName) && !string.IsNullOrWhiteSpace(filePath))
            {
                if (fileName[1] == ':')
                    fileName = fileName.Substring(3);

                filePath += $@"{string.Join("/", fileName.Split('/').Reverse().Skip(1).Reverse())}";
                fileName = fileName.Split('/').Last();

                if (!string.IsNullOrWhiteSpace(fileName) && !string.IsNullOrWhiteSpace(filePath))
                {
                    if (!Directory.Exists(filePath))
                        Directory.CreateDirectory(filePath);
                    FileStream outputter = new FileStream($@"{filePath}\{fileName}", FileMode.OpenOrCreate);
                    stream.CopyTo(outputter);
                    outputter.Flush();
                    outputter.Close();

                    stream.Seek(0, SeekOrigin.Begin);
                }
            }
            else
            {
                App.PushLog("No filename or file path given to SaveStreamToFile()!");
            }
        }

        public static void ReadXBC1Datas(Stream sXBC1s, ref Structs.XBC1[] XBC1s, bool forceReread = false)
        {
            for (int i = 0; i < XBC1s.Length; i++)
            {
                if (XBC1s[i].Data == null || forceReread)
                    XBC1s[i].Data = ReadZlib(sXBC1s, XBC1s[i].OffsetInFile, XBC1s[i].FileSize, XBC1s[i].CompressedSize);
            }
        }

        public static MemoryStream ReadZlib(Stream sZlib, int Offset, int FileSize, int CompressedSize)
        {
            sZlib.Seek(Offset, SeekOrigin.Begin);
            byte[] fileBuffer = new byte[FileSize >= CompressedSize ? FileSize : CompressedSize];

            MemoryStream msFile = new MemoryStream();
            sZlib.Read(fileBuffer, 0, CompressedSize);

            ZOutputStream ZOutFile = new ZOutputStream(msFile);
            ZOutFile.Write(fileBuffer, 0, CompressedSize);
            ZOutFile.Flush();

            msFile.Seek(0L, SeekOrigin.Begin);
            return msFile;
        }

        public List<int>[] VerifyMeshes(Structs.Mesh Mesh, Structs.MXMD MXMD)
        {
            if (MXMD.Version == Int32.MaxValue)
                return new List<int>[] { new List<int> { 0 } };

            List<int>[] VerifiedMeshes = new List<int>[MXMD.ModelStruct.MeshesCount];

            for (int i = 0; i < MXMD.ModelStruct.MeshesCount; i++)
            {
                if (MXMD.ModelStruct.Meshes[i].Descriptors.Count(x => x.LOD == App.LOD || App.LOD == -1) == 0)
                {
                    int prev = App.LOD;
                    App.PushLog($"An LOD value of {App.LOD} returns 0 meshes, checking for the highest available LOD...");
                    for (int j = 0; j <= 3; j++)
                    {
                        if (MXMD.ModelStruct.Meshes[i].Descriptors.Count(x => x.LOD == j) > 0)
                        {
                            App.LOD = j;
                            App.PushLog($"LOD set to {j}.");
                            break;
                        }
                    }
                    if (App.LOD == prev)
                        App.PushLog("...this file has no meshes in it? Really?");
                }

                List<int> ValidMeshes = new List<int>();
                for (int j = 0; j < MXMD.ModelStruct.Meshes[i].TableCount; j++)
                {
                    if (MXMD.ModelStruct.Meshes[i].Descriptors[j].LOD == App.LOD || App.LOD == -1)
                    {
                        if (App.ExportOutlines || (!App.ExportOutlines && !MXMD.Materials[MXMD.ModelStruct.Meshes[i].Descriptors[j].MaterialID].Name.Contains("outline")))
                        {
                            ValidMeshes.Add(j);
                            if (App.ExportFlexes && Mesh.MorphDataOffset > 0)
                            {
                                List<Structs.MeshMorphDescriptor> descs = Mesh.MorphData.MorphDescriptors.Where(x => x.BufferID == MXMD.ModelStruct.Meshes[i].Descriptors[j].VertTableIndex).ToList();
                                if (descs.Count > 0)
                                    for (int k = 1; k < descs[0].TargetCounts; k++)
                                        ValidMeshes.Add(j);
                            }
                        }
                    }
                }

                VerifiedMeshes[i] = ValidMeshes;
            }

            return VerifiedMeshes;
        }

        public Structs.XBC1 ReadXBC1(Stream sXBC1, BinaryReader brXBC1, int offset, bool saveStream = true)
        {
            if (sXBC1 == null || brXBC1 == null || offset > sXBC1.Length || offset < 0)
                return new Structs.XBC1 { Version = Int32.MaxValue };

            sXBC1.Seek(offset, SeekOrigin.Begin);
            int XBC1Magic = brXBC1.ReadInt32(); //nice meme
            if (XBC1Magic != 0x31636278)
            {
                App.PushLog("XBC1 header invalid!");
                return new Structs.XBC1 { Version = Int32.MaxValue };
            }

            Structs.XBC1 XBC1 = new Structs.XBC1
            {
                Version = brXBC1.ReadInt32(),
                FileSize = brXBC1.ReadInt32(),
                CompressedSize = brXBC1.ReadInt32(),
                Unknown1 = brXBC1.ReadInt32(),
                Name = ReadNullTerminatedString(brXBC1),
                OffsetInFile = offset
            };

            if (saveStream)
            {
                XBC1.Data = ReadZlib(sXBC1, offset + 0x30, XBC1.FileSize, XBC1.CompressedSize);
            }

            return XBC1;
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
            else if (SAR1.Path[0] == '/')
                safePath += SAR1.Path.Substring(1);
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
                    App.PushLog("BC is corrupt (or wrong endianness)!");
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
                Unknown2 = brSKEL.ReadInt32(),

                TOCItems = new Structs.SKELTOC[9],

                NodeNames = new Dictionary<string, int>()
            };

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

            for (int i = 0; i < SKEL.TOCItems[2].Count; i++)
            {
                Quaternion posQuat = SKEL.Transforms[i].Position;
                Vector3 posVector = new Vector3(posQuat.X, posQuat.Y, posQuat.Z);
                SKEL.Transforms[i].RealPosition = posVector;

                SKEL.NodeNames.Add(SKEL.Nodes[i].Name, i);

                if (SKEL.Parents[i] < 0) //is root
                {
                    SKEL.Transforms[i].RealPosition = posVector;
                    SKEL.Transforms[i].RealRotation = SKEL.Transforms[i].Rotation;
                }
                else
                {
                    int curParentIndex = SKEL.Parents[i];
                    //add rotation of parent
                    SKEL.Transforms[i].RealRotation = SKEL.Transforms[curParentIndex].RealRotation * SKEL.Transforms[i].Rotation;
                    //make position a quaternion again (for later operations)
                    Quaternion bonePosQuat = new Quaternion(posVector, 0f);
                    //multiply position and rotation (?)
                    Quaternion bonePosRotQuat = SKEL.Transforms[curParentIndex].RealRotation * bonePosQuat;
                    //do something or other i dunno
                    Quaternion newPosition = bonePosRotQuat * new Quaternion(-SKEL.Transforms[curParentIndex].RealRotation.X, -SKEL.Transforms[curParentIndex].RealRotation.Y, -SKEL.Transforms[curParentIndex].RealRotation.Z, SKEL.Transforms[curParentIndex].RealRotation.W);
                    //add position to parent's position
                    SKEL.Transforms[i].RealPosition = new Vector3(newPosition.X, newPosition.Y, newPosition.Z) + SKEL.Transforms[curParentIndex].RealPosition;
                }
            }

            return SKEL;
        }

        public Structs.LBIM ReadLBIM(Stream sLBIM, BinaryReader brLBIM, int Offset, int Size, Structs.MSRDDataItem dataItem = default(Structs.MSRDDataItem))
        {
            sLBIM.Seek(Offset + Size - 0x4, SeekOrigin.Begin);
            if (brLBIM.ReadInt32() != 0x4D49424C)
            {
                App.PushLog($"Texture magic is incorrect! Offset {Offset:X}, size {Size:X}, cur position {sLBIM.Position:X}");
                return default(Structs.LBIM);
            }

            sLBIM.Seek(Offset + Size - 0x28, SeekOrigin.Begin);
            Structs.LBIM LBIM = new Structs.LBIM
            {
                Data = new MemoryStream(dataItem.Size),

                Unknown5 = brLBIM.ReadInt32(),
                Unknown4 = brLBIM.ReadInt32(),

                Width = brLBIM.ReadInt32(),
                Height = brLBIM.ReadInt32(),

                Unknown3 = brLBIM.ReadInt32(),
                Unknown2 = brLBIM.ReadInt32(),

                Type = brLBIM.ReadInt32(),
                Unknown1 = brLBIM.ReadInt32(),
                Version = brLBIM.ReadInt32()
            };

            if (dataItem.Size != default(Structs.MSRDDataItem).Size)
                LBIM.DataItem = dataItem;
            sLBIM.Seek(Offset, SeekOrigin.Begin);
            sLBIM.CopyTo(LBIM.Data, Size);

            return LBIM;
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
                MainOffset = brMSRD.ReadInt32(),

                Tag = brMSRD.ReadInt32(),
                Revision = brMSRD.ReadInt32(),

                DataItemsCount = brMSRD.ReadInt32(),
                DataItemsOffset = brMSRD.ReadInt32(),
                FileCount = brMSRD.ReadInt32(),
                TOCOffset = brMSRD.ReadInt32(),

                Unknown1 = brMSRD.ReadBytes(0x1C),

                TextureIdsCount = brMSRD.ReadInt32(),
                TextureIdsOffset = brMSRD.ReadInt32(),
                TextureCountOffset = brMSRD.ReadInt32()
            };
            
            if (MSRD.DataItemsOffset != 0)
            {
                MSRD.DataItems = new Structs.MSRDDataItem[MSRD.DataItemsCount];
                sMSRD.Seek(MSRD.MainOffset + MSRD.DataItemsOffset, SeekOrigin.Begin);
                for (int i = 0; i < MSRD.DataItemsCount; i++)
                {
                    MSRD.DataItems[i] = new Structs.MSRDDataItem
                    {
                        Offset = brMSRD.ReadInt32(),
                        Size = brMSRD.ReadInt32(),
                        TOCIndex = brMSRD.ReadInt16(),
                        Type = (Structs.MSRDDataItemTypes)brMSRD.ReadInt16()
                    };
                    sMSRD.Seek(0x8, SeekOrigin.Current);
                }
            }
            
            if (MSRD.TextureIdsOffset != 0)
            {
                sMSRD.Seek(MSRD.MainOffset + MSRD.TextureIdsOffset, SeekOrigin.Begin);
                MSRD.TextureIds = new short[MSRD.TextureIdsCount];
                for (int i = 0; i < MSRD.TextureIdsCount; i++)
                {
                    MSRD.TextureIds[i] = brMSRD.ReadInt16();
                }
            }
            
            if (MSRD.TextureCountOffset != 0)
            {
                sMSRD.Seek(MSRD.MainOffset + MSRD.TextureCountOffset, SeekOrigin.Begin);
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
            for (int i = 0; i < MSRD.FileCount; i++)
            {
                sMSRD.Seek(MSRD.MainOffset + MSRD.TOCOffset + (i * 12), SeekOrigin.Begin); //prevents errors I guess
                MSRD.TOC[i].CompSize = brMSRD.ReadInt32();
                MSRD.TOC[i].FileSize = brMSRD.ReadInt32();
                MSRD.TOC[i].Offset = brMSRD.ReadInt32();

                App.PushLog($"Decompressing file{i} in MSRD...");
                MSRD.TOC[i].Data = ReadXBC1(sMSRD, brMSRD, MSRD.TOC[i].Offset, true).Data;
                if (App.ExportFormat == Structs.ExportFormat.RawFiles)
                    SaveStreamToFile(MSRD.TOC[i].Data, $"file{i}.bin", App.CurOutputPath + @"\RawFiles\");
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

            if (MXMD.ModelStructOffset != 0)
            {
                sMXMD.Seek(MXMD.ModelStructOffset, SeekOrigin.Begin);
                MXMD.ModelStruct = new Structs.MXMDModelStruct
                {
                    Unknown1 = brMXMD.ReadInt32(),
                    BoundingBoxStart = new Vector3(brMXMD.ReadSingle(), brMXMD.ReadSingle(), brMXMD.ReadSingle()),
                    BoundingBoxEnd = new Vector3(brMXMD.ReadSingle(), brMXMD.ReadSingle(), brMXMD.ReadSingle()),
                    MeshesOffset = brMXMD.ReadInt32(),
                    MeshesCount = brMXMD.ReadInt32(),
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

                        sMXMD.Seek(MXMD.ModelStructOffset + MXMD.ModelStruct.MorphNamesOffset + MXMD.ModelStruct.MorphNames.Names[i].NameOffset, SeekOrigin.Begin);
                        MXMD.ModelStruct.MorphNames.Names[i].Name = FormatTools.ReadNullTerminatedString(brMXMD);
                    }
                }

                if (MXMD.ModelStruct.MeshesOffset != 0)
                {
                    MXMD.ModelStruct.Meshes = new Structs.MXMDMeshes[MXMD.ModelStruct.MeshesCount];

                    sMXMD.Seek(MXMD.ModelStructOffset + MXMD.ModelStruct.MeshesOffset, SeekOrigin.Begin);
                    for (int i = 0; i < MXMD.ModelStruct.MeshesCount; i++)
                    {
                        MXMD.ModelStruct.Meshes[i] = new Structs.MXMDMeshes
                        {
                            TableOffset = brMXMD.ReadInt32(),
                            TableCount = brMXMD.ReadInt32(),
                            Unknown1 = brMXMD.ReadInt32(),

                            BoundingBoxStart = new Vector3(brMXMD.ReadSingle(), brMXMD.ReadSingle(), brMXMD.ReadSingle()),
                            BoundingBoxEnd = new Vector3(brMXMD.ReadSingle(), brMXMD.ReadSingle(), brMXMD.ReadSingle()),
                            BoundingRadius = brMXMD.ReadSingle()
                        };

                        sMXMD.Seek(MXMD.ModelStructOffset + MXMD.ModelStruct.Meshes[i].TableOffset, SeekOrigin.Begin);
                        MXMD.ModelStruct.Meshes[i].Descriptors = new Structs.MXMDMeshDescriptor[MXMD.ModelStruct.Meshes[i].TableCount];
                        for (int j = 0; j < MXMD.ModelStruct.Meshes[i].TableCount; j++)
                        {
                            MXMD.ModelStruct.Meshes[i].Descriptors[j] = new Structs.MXMDMeshDescriptor
                            {
                                //ms says to add 1 to some of these, why?
                                ID = brMXMD.ReadInt32(),

                                Descriptor = brMXMD.ReadInt32(),

                                VertTableIndex = brMXMD.ReadInt16(),
                                FaceTableIndex = brMXMD.ReadInt16(),

                                Unknown1 = brMXMD.ReadInt16(),
                                MaterialID = brMXMD.ReadInt16(),
                                Unknown2 = brMXMD.ReadBytes(0xC),
                                Unknown3 = brMXMD.ReadInt16(),

                                LOD = brMXMD.ReadInt16(),
                                Unknown4 = brMXMD.ReadInt32(),

                                Unknown5 = brMXMD.ReadBytes(0xC),
                            };
                        }
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
            }

            if (MXMD.MaterialsOffset != 0)
            {
                sMXMD.Seek(MXMD.MaterialsOffset, SeekOrigin.Begin);
                MXMD.MaterialHeader = new Structs.MXMDMaterialHeader
                {
                    Offset = brMXMD.ReadInt32(),
                    Count = brMXMD.ReadInt32()
                };
                
                MXMD.Materials = new Structs.MXMDMaterial[MXMD.MaterialHeader.Count];
                for (int i = 0; i < MXMD.MaterialHeader.Count; i++)
                {
                    sMXMD.Seek(MXMD.MaterialsOffset + MXMD.MaterialHeader.Offset + (i * 0x74), SeekOrigin.Begin);
                    MXMD.Materials[i] = new Structs.MXMDMaterial
                    {
                        NameOffset = brMXMD.ReadInt32(),
                        Unknown1 = brMXMD.ReadBytes(0x70)
                    };

                    sMXMD.Seek(MXMD.MaterialsOffset + MXMD.Materials[i].NameOffset, SeekOrigin.Begin);
                    MXMD.Materials[i].Name = ReadNullTerminatedString(brMXMD);
                }
            }

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

                MorphDataOffset = brMesh.ReadInt32(),
                DataSize = brMesh.ReadInt32(),
                DataOffset = brMesh.ReadInt32(),
                WeightDataSize = brMesh.ReadInt32(),
                WeightDataOffset = brMesh.ReadInt32(),

                Reserved2 = brMesh.ReadBytes(0x14)
            };

            if (Mesh.VertexTableOffset != 0)
            {
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
            }

            if (Mesh.FaceTableOffset != 0)
            {
                Mesh.FaceTables = new Structs.MeshFaceTable[Mesh.FaceTableCount];
                for (int i = 0; i < Mesh.FaceTableCount; i++)
                {
                    sMesh.Seek(Mesh.FaceTableOffset + (i * 0x14), SeekOrigin.Begin);
                    Mesh.FaceTables[i] = new Structs.MeshFaceTable
                    {
                        Offset = brMesh.ReadInt32(),
                        VertCount = brMesh.ReadInt32(),

                        Unknown1 = brMesh.ReadBytes(0xC)
                    };

                    Mesh.FaceTables[i].Vertices = new ushort[Mesh.FaceTables[i].VertCount];
                    sMesh.Seek(Mesh.DataOffset + Mesh.FaceTables[i].Offset, SeekOrigin.Begin);
                    for (int j = 0; j < Mesh.FaceTables[i].VertCount; j++)
                        Mesh.FaceTables[i].Vertices[j] = brMesh.ReadUInt16();
                }
            }

            if (Mesh.WeightDataOffset != 0)
            {
                sMesh.Seek(Mesh.WeightDataOffset, SeekOrigin.Begin);
                Mesh.WeightData = new Structs.MeshWeightData
                {
                    WeightManagerCount = brMesh.ReadInt32(),
                    WeightManagerOffset = brMesh.ReadInt32(),

                    VertexTableIndex = brMesh.ReadInt16(),
                    Unknown2 = brMesh.ReadInt16(),

                    Offset02 = brMesh.ReadInt32()
                };

                Mesh.WeightData.WeightManagers = new Structs.MeshWeightManager[Mesh.WeightData.WeightManagerCount];
                sMesh.Seek(Mesh.WeightData.WeightManagerOffset, SeekOrigin.Begin);
                for (int i = 0; i < Mesh.WeightData.WeightManagerCount; i++)
                {
                    Mesh.WeightData.WeightManagers[i] = new Structs.MeshWeightManager
                    {
                        Unknown1 = brMesh.ReadInt32(),
                        Offset = brMesh.ReadInt32(),
                        Count = brMesh.ReadInt32(),

                        Unknown2 = brMesh.ReadBytes(0x11),
                        LOD = brMesh.ReadByte(),
                        Unknown3 = brMesh.ReadBytes(0xA)
                    };
                }
            }

            if (Mesh.MorphDataOffset > 0) //has flexes
            {
                sMesh.Seek(Mesh.MorphDataOffset, SeekOrigin.Begin);

                Mesh.MorphData.MorphDescriptorsCount = brMesh.ReadInt32();
                Mesh.MorphData.MorphDescriptorsOffset = brMesh.ReadInt32();
                Mesh.MorphData.MorphTargetsCount = brMesh.ReadInt32();
                Mesh.MorphData.MorphTargetsOffset = brMesh.ReadInt32();

                Mesh.MorphData.MorphDescriptors = new Structs.MeshMorphDescriptor[Mesh.MorphData.MorphDescriptorsCount];
                sMesh.Seek(Mesh.MorphData.MorphDescriptorsOffset, SeekOrigin.Begin);
                for (int i = 0; i < Mesh.MorphData.MorphDescriptorsCount; i++)
                {
                    Mesh.MorphData.MorphDescriptors[i] = new Structs.MeshMorphDescriptor
                    {
                        BufferID = brMesh.ReadInt32(),

                        TargetIndex = brMesh.ReadInt32(),
                        TargetCounts = brMesh.ReadInt32(),
                        TargetIDOffsets = brMesh.ReadInt32(),

                        Unknown1 = brMesh.ReadInt32()
                    };
                }

                Mesh.MorphData.MorphTargets = new Structs.MeshMorphTarget[Mesh.MorphData.MorphTargetsCount];
                sMesh.Seek(Mesh.MorphData.MorphTargetsOffset, SeekOrigin.Begin);
                for (int i = 0; i < Mesh.MorphData.MorphTargetsCount; i++)
                {
                    Mesh.MorphData.MorphTargets[i] = new Structs.MeshMorphTarget
                    {
                        BufferOffset = brMesh.ReadInt32(),
                        VertCount = brMesh.ReadInt32(),
                        BlockSize = brMesh.ReadInt32(),

                        Unknown1 = brMesh.ReadInt16(),
                        Type = brMesh.ReadInt16()
                    };
                    Mesh.MorphData.MorphTargets[i].Vertices = new Vector3[Mesh.MorphData.MorphTargets.Max(x => x.VertCount)];
                    Mesh.MorphData.MorphTargets[i].Normals = new Quaternion[Mesh.MorphData.MorphTargets.Max(x => x.VertCount)];
                }
            }

            for (int i = 0; i < Mesh.VertexTableCount; i++)
            {
                sMesh.Seek(Mesh.DataOffset + Mesh.VertexTables[i].DataOffset, SeekOrigin.Begin);

                Mesh.VertexTables[i].Vertices = new Vector3[Mesh.VertexTables[i].DataCount];
                Mesh.VertexTables[i].Weights = new int[Mesh.VertexTables[i].DataCount];
                Mesh.VertexTables[i].UVPosX = new float[Mesh.VertexTables[i].DataCount, 4];
                Mesh.VertexTables[i].UVPosY = new float[Mesh.VertexTables[i].DataCount, 4];
                Mesh.VertexTables[i].VertexColor = new Color[Mesh.VertexTables[i].DataCount];
                Mesh.VertexTables[i].Normals = new Quaternion[Mesh.VertexTables[i].DataCount];
                Mesh.VertexTables[i].WeightValues = new float[Mesh.VertexTables[i].DataCount, 4];
                Mesh.VertexTables[i].WeightIds = new byte[Mesh.VertexTables[i].DataCount, 4];

                for (int j = 0; j < Mesh.VertexTables[i].DataCount; j++)
                {
                    foreach (Structs.MeshVertexDescriptor desc in Mesh.VertexTables[i].Descriptors)
                    {
                        switch (desc.Type)
                        {
                            case 0:
                                Mesh.VertexTables[i].Vertices[j] = new Vector3(brMesh.ReadSingle(), brMesh.ReadSingle(), brMesh.ReadSingle());
                                break;
                            case 3:
                                Mesh.VertexTables[i].Weights[j] = brMesh.ReadInt32();
                                break;
                            case 5:
                            case 6:
                            case 7:
                                Mesh.VertexTables[i].UVPosX[j, desc.Type - 5] = brMesh.ReadSingle();
                                Mesh.VertexTables[i].UVPosY[j, desc.Type - 5] = brMesh.ReadSingle();
                                if (desc.Type - 4 > Mesh.VertexTables[i].UVLayerCount)
                                    Mesh.VertexTables[i].UVLayerCount = desc.Type - 4;
                                break;
                            case 17:
                                Mesh.VertexTables[i].VertexColor[j] = Color.FromArgb(brMesh.ReadByte(), brMesh.ReadByte(), brMesh.ReadByte(), brMesh.ReadByte());
                                break;
                            case 28:
                                Mesh.VertexTables[i].Normals[j] = new Quaternion(brMesh.ReadSByte() / 128f, brMesh.ReadSByte() / 128f, brMesh.ReadSByte() / 128f, brMesh.ReadSByte() /*dummy*/);
                                break;
                            case 41:
                                Mesh.VertexTables[i].WeightValues[j, 0] = brMesh.ReadUInt16() / 65535f;
                                Mesh.VertexTables[i].WeightValues[j, 1] = brMesh.ReadUInt16() / 65535f;
                                Mesh.VertexTables[i].WeightValues[j, 2] = brMesh.ReadUInt16() / 65535f;
                                Mesh.VertexTables[i].WeightValues[j, 3] = brMesh.ReadUInt16() / 65535f;
                                break;
                            case 42:
                                Mesh.VertexTables[i].WeightIds[j, 0] = brMesh.ReadByte();
                                Mesh.VertexTables[i].WeightIds[j, 1] = brMesh.ReadByte();
                                Mesh.VertexTables[i].WeightIds[j, 2] = brMesh.ReadByte();
                                Mesh.VertexTables[i].WeightIds[j, 3] = brMesh.ReadByte();
                                break;
                            default:
                                sMesh.Seek(desc.Size, SeekOrigin.Current);
                                break;
                        }
                    }
                }
            }

            for (int i = 0; i < Mesh.MorphData.MorphDescriptorsCount; i++)
            {
                Mesh.MorphData.MorphDescriptors[i].TargetIDs = new short[Mesh.MorphData.MorphDescriptors[i].TargetCounts];
                sMesh.Seek(Mesh.MorphData.MorphDescriptors[i].TargetIDOffsets, SeekOrigin.Begin);
                for (int j = 0; j < Mesh.MorphData.MorphDescriptors[i].TargetCounts; j++)
                    Mesh.MorphData.MorphDescriptors[i].TargetIDs[j] = brMesh.ReadInt16();

                Structs.MeshMorphDescriptor desc = Mesh.MorphData.MorphDescriptors[i];

                sMesh.Seek(Mesh.DataOffset + Mesh.MorphData.MorphTargets[desc.TargetIndex].BufferOffset, SeekOrigin.Begin);

                for (int j = 0; j < Mesh.MorphData.MorphTargets[desc.TargetIndex].VertCount; j++)
                {
                    Mesh.VertexTables[desc.BufferID].Vertices[j] = new Vector3(brMesh.ReadSingle(), brMesh.ReadSingle(), brMesh.ReadSingle());
                    Mesh.VertexTables[desc.BufferID].Normals[j] = new Quaternion(brMesh.ReadByte() / 128f, brMesh.ReadByte() / 128f, brMesh.ReadByte() / 128f, 1 /*dummy*/);
                    sMesh.Seek(Mesh.MorphData.MorphTargets[desc.TargetIndex].BlockSize - 15, SeekOrigin.Current);
                }

                for (int j = 1; j < Mesh.MorphData.MorphDescriptors[i].TargetCounts; j++)
                {
                    Mesh.MorphData.MorphTargets[desc.TargetIndex + j].Vertices = new Vector3[Mesh.MorphData.MorphTargets[desc.TargetIndex].VertCount];
                    Mesh.MorphData.MorphTargets[desc.TargetIndex + j].Normals = new Quaternion[Mesh.MorphData.MorphTargets[desc.TargetIndex].VertCount];

                    sMesh.Seek(Mesh.DataOffset + Mesh.MorphData.MorphTargets[desc.TargetIndex + j].BufferOffset, SeekOrigin.Begin);
                    for (int k = 0; k < Mesh.MorphData.MorphTargets[desc.TargetIndex + j].VertCount; k++)
                    {
                        Vector3 vert = new Vector3(brMesh.ReadSingle(), brMesh.ReadSingle(), brMesh.ReadSingle());
                        brMesh.ReadInt32();
                        Quaternion norm = new Quaternion(brMesh.ReadByte() / 128f, brMesh.ReadByte() / 128f, brMesh.ReadByte() / 128f, brMesh.ReadByte() /*dummy*/);
                        brMesh.ReadInt32();
                        brMesh.ReadInt32();
                        int index = brMesh.ReadInt32();
                        Mesh.MorphData.MorphTargets[desc.TargetIndex + j].Vertices[index] = vert;
                        Mesh.MorphData.MorphTargets[desc.TargetIndex + j].Normals[index] = norm;
                    }
                }
            }

            return Mesh;
        }

        public Structs.MapInfo ReadMapInfo(Stream sMap, BinaryReader brMap)
        {
            Structs.MapInfo map = new Structs.MapInfo
            {
                Unknown1 = brMap.ReadInt32(),
                Unknown2 = brMap.ReadInt32(),
                Unknown3 = brMap.ReadInt32(),

                MeshTableOffset = brMap.ReadInt32(),
                MaterialTableOffset = brMap.ReadInt32(),

                Unknown4 = brMap.ReadInt32(),
                LODOffset = brMap.ReadInt32(),
                Unknown5 = brMap.ReadInt32(),
                Unknown6 = brMap.ReadInt32(),
                Unknown7 = brMap.ReadInt32(),

                PopFileIndexOffset = brMap.ReadInt32(),
                PopFileIndexCount = brMap.ReadInt32(),
                Unknown8 = brMap.ReadInt32(),
                Unknown9 = brMap.ReadInt32(),

                TableIndexOffset = brMap.ReadInt32()
            };

            if (map.MaterialTableOffset != 0)
            {
                sMap.Seek(map.MaterialTableOffset, SeekOrigin.Begin);
                map.MaterialHeader = new Structs.MXMDMaterialHeader
                {
                    Offset = brMap.ReadInt32(),
                    Count = brMap.ReadInt32()
                };

                map.Materials = new Structs.MXMDMaterial[map.MaterialHeader.Count];
                for (int i = 0; i < map.MaterialHeader.Count; i++)
                {
                    sMap.Seek(map.MaterialTableOffset + map.MaterialHeader.Offset + (i * 0x74), SeekOrigin.Begin);
                    map.Materials[i] = new Structs.MXMDMaterial
                    {
                        NameOffset = brMap.ReadInt32(),
                        Unknown1 = brMap.ReadBytes(0x70)
                    };

                    sMap.Seek(map.MaterialTableOffset + map.Materials[i].NameOffset, SeekOrigin.Begin);
                    map.Materials[i].Name = ReadNullTerminatedString(brMap);
                }
            }

            if (map.MeshTableOffset != 0)
            {
                sMap.Seek(map.MeshTableOffset + 0x1C, SeekOrigin.Begin);
                map.MeshTableDataOffset = brMap.ReadInt32();
                map.MeshTableDataCount = brMap.ReadInt32();

                map.MeshTables = new Structs.MapInfoMeshTable[map.MeshTableDataCount];
                for (int i = 0; i < map.MeshTableDataCount; i++)
                {
                    sMap.Seek(map.MeshTableOffset + map.MeshTableDataOffset + (i * 0x44), SeekOrigin.Begin);
                    map.MeshTables[i].MeshOffset = brMap.ReadInt32();
                    map.MeshTables[i].MeshCount = brMap.ReadInt32();

                    map.MeshTables[i].Descriptors = new Structs.MXMDMeshDescriptor[map.MeshTables[i].MeshCount];
                    sMap.Seek(map.MeshTableOffset + map.MeshTables[i].MeshOffset, SeekOrigin.Begin);
                    for (int j = 0; j < map.MeshTables[i].MeshCount; j++)
                    {
                        map.MeshTables[i].Descriptors[j] = new Structs.MXMDMeshDescriptor
                        {
                            ID = brMap.ReadInt32(),

                            Descriptor = brMap.ReadInt32(),

                            VertTableIndex = brMap.ReadInt16(),
                            FaceTableIndex = brMap.ReadInt16(),

                            Unknown1 = brMap.ReadInt16(),
                            MaterialID = brMap.ReadInt16(),
                            Unknown2 = brMap.ReadBytes(0xC),
                            Unknown3 = brMap.ReadInt16(),

                            LOD = brMap.ReadInt16(),
                            Unknown4 = brMap.ReadInt32(),

                            Unknown5 = brMap.ReadBytes(0xC)
                        };
                    }
                }

                if (map.TableIndexOffset != 0 && map.PopFileIndexOffset == 0)
                {
                    sMap.Seek(map.TableIndexOffset + 0x8, SeekOrigin.Begin);
                    map.MeshFileLookupOffset = brMap.ReadInt32();
                    map.MeshFileLookupCount = brMap.ReadInt32();

                    sMap.Seek(map.TableIndexOffset + map.MeshFileLookupOffset, SeekOrigin.Begin);
                    map.MeshFileLookup = new short[map.MeshFileLookupCount];
                    for (int i = 0; i < map.MeshFileLookupCount; i++)
                    {
                        map.MeshFileLookup[i] = brMap.ReadInt16();
                    }
                }
            }

            if (map.LODOffset != 0)
            {
                sMap.Seek(map.LODOffset + 0x4, SeekOrigin.Begin);
                map.LODDataCount = brMap.ReadInt32();
                map.LODDataOffset = brMap.ReadInt32();

                map.LODSomething = new List<int>();
                for (int i = 0; i < map.LODDataCount; i++)
                {
                    sMap.Seek(map.LODOffset + map.LODDataOffset + (i * 0x8), SeekOrigin.Begin);
                    brMap.ReadInt32();
                    for (int j = 0; j < brMap.ReadInt32(); j++)
                        map.LODSomething.Add(i);
                }
            }

            if (map.PopFileIndexOffset != 0)
            {
                map.PopFileSomething = new int[map.PopFileIndexCount];
                sMap.Seek(map.PopFileIndexOffset, SeekOrigin.Begin);
                for (int i = 0; i < map.PopFileIndexCount; i++)
                    map.PopFileSomething[i] = brMap.ReadInt32();
            }

            return map;
        }

        public void ModelToASCII(Structs.Mesh[] Meshes, Structs.MXMD MXMD, Structs.SKEL SKEL, Structs.MapInfo MapInfo)
        {
            Dictionary<int, string> NodesIdsNames = new Dictionary<int, string>();
            for (int r = 0; r < MXMD.ModelStruct.Nodes.BoneCount; r++)
                NodesIdsNames.Add(r, MXMD.ModelStruct.Nodes.Nodes[r].Name);

            //begin ascii
            //bone time
            string filename = $@"{App.CurOutputPath}\{App.CurFileNameNoExt}";
            if (MapInfo.Unknown1 != Int32.MaxValue && MapInfo.MeshFileLookupOffset != 0)
                filename += $"_mesh{MapInfo.MeshFileLookup.Min()}-{MapInfo.MeshFileLookup.Max()}";
            else if (MapInfo.Unknown1 != Int32.MaxValue && MapInfo.PopFileIndexOffset != 0)
                filename += $"_props_mesh{MapInfo.PopFileSomething.Min()}-{MapInfo.PopFileSomething.Max()}";
            StreamWriter asciiWriter = new StreamWriter(filename + ".ascii");
            if (SKEL.Unknown1 != Int32.MaxValue)
            {
                asciiWriter.WriteLine(SKEL.TOCItems[2].Count);
                for (int i = 0; i < SKEL.TOCItems[2].Count; i++)
                {
                    asciiWriter.WriteLine(SKEL.Nodes[i].Name);
                    asciiWriter.WriteLine(SKEL.Parents[i]);
                    asciiWriter.Write(SKEL.Transforms[i].RealPosition.X.ToString("F6") + " ");
                    asciiWriter.Write(SKEL.Transforms[i].RealPosition.Y.ToString("F6") + " ");
                    asciiWriter.Write(SKEL.Transforms[i].RealPosition.Z.ToString("F6"));
                    asciiWriter.WriteLine();
                }
            }
            else
                asciiWriter.WriteLine(0);

            List<int>[] ValidMeshes = VerifyMeshes(Meshes[0], MXMD);

            Structs.Mesh Mesh = Meshes[0];
            asciiWriter.WriteLine(ValidMeshes.Sum(x => x.Count));
            for (int i = 0; i < MXMD.ModelStruct.MeshesCount; i++)
            {
                if (MapInfo.Unknown1 != Int32.MaxValue && MapInfo.MeshFileLookupOffset != 0)
                {
                    App.PushLog($"I think this is one of them map thingers - {i} gets {MapInfo.MeshFileLookup[i]}");
                    Mesh = Meshes[MapInfo.MeshFileLookup[i]];
                    ValidMeshes = VerifyMeshes(Mesh, MXMD);
                }
                else if (MapInfo.Unknown1 != Int32.MaxValue && MapInfo.PopFileIndexOffset != 0)
                {
                    App.PushLog($"I think this is one of them prop thingers");
                    Mesh = Meshes[MapInfo.PopFileSomething[i]];
                    ValidMeshes = VerifyMeshes(Mesh, MXMD);
                }

                int lastMeshIdIdenticalCount = 0;
                bool lastMeshIdIdentical = false;
                for (int j = 0; j < ValidMeshes[i].Count; j++)
                {
                    lastMeshIdIdentical = j == 0 ? false : ValidMeshes[i][j - 1] == ValidMeshes[i][j];

                    if (lastMeshIdIdentical)
                        lastMeshIdIdenticalCount++;
                    else
                        lastMeshIdIdenticalCount = 0;

                    int descId = ValidMeshes[i][j];
                    Structs.MXMDMeshDescriptor desc = MXMD.Version == Int32.MaxValue ? default(Structs.MXMDMeshDescriptor) : MXMD.ModelStruct.Meshes[i].Descriptors[descId];
                    if (desc.LOD == App.LOD || App.LOD == -1)
                    {
                        Structs.MeshVertexTable vertTbl = Mesh.VertexTables[desc.VertTableIndex];
                        Structs.MeshFaceTable faceTbl = Mesh.FaceTables[desc.FaceTableIndex];
                        Structs.MeshVertexTable weightTbl = Mesh.VertexTables.Last();
                        Structs.MeshMorphDescriptor morphDesc = new Structs.MeshMorphDescriptor();
                        if (App.ExportFlexes && Mesh.MorphDataOffset > 0)
                            morphDesc = Mesh.MorphData.MorphDescriptors.Where(x => x.BufferID == desc.VertTableIndex).FirstOrDefault();

                        int highestVertId = 0;
                        int lowestVertId = vertTbl.DataCount;
                        for (int k = 0; k < faceTbl.VertCount; k++)
                        {
                            if (faceTbl.Vertices[k] > highestVertId)
                                highestVertId = faceTbl.Vertices[k];
                            if (faceTbl.Vertices[k] < lowestVertId)
                                lowestVertId = faceTbl.Vertices[k];
                        }

                        string meshName = $"file{i}mesh{descId}_{(App.LOD == -1 ? $"LOD{desc.LOD}_" : "")}";
                        if (lastMeshIdIdentical)
                            meshName += $"flex_{MXMD.ModelStruct.MorphControls.Controls[lastMeshIdIdenticalCount - 1].Name}";
                        else
                        {
                            if (MXMD.Version != Int32.MaxValue)
                                meshName += MXMD.Materials[desc.MaterialID].Name;
                            else
                                meshName += "NO_MATERIALS";
                        }

                        asciiWriter.WriteLine(meshName); //mesh name
                        asciiWriter.WriteLine(vertTbl.UVLayerCount);
                        asciiWriter.WriteLine(0); //texture count, always 0 for us, though maybe I should change that?
                        asciiWriter.WriteLine(highestVertId - lowestVertId + 1); //vertex count
                        for (int vrtIndex = lowestVertId; vrtIndex <= highestVertId; vrtIndex++)
                        {
                            //vertex position
                            Vector3 vertexPos = vertTbl.Vertices[vrtIndex];
                            if (lastMeshIdIdentical)
                                vertexPos += Mesh.MorphData.MorphTargets[morphDesc.TargetIndex + lastMeshIdIdenticalCount + 1].Vertices[vrtIndex];
                            asciiWriter.Write($"{vertexPos.X:F6} ");
                            asciiWriter.Write($"{vertexPos.Y:F6} ");
                            asciiWriter.Write($"{vertexPos.Z:F6}");
                            asciiWriter.WriteLine();

                            //vertex normal
                            Quaternion normalPos = vertTbl.Normals[vrtIndex];
                            if (lastMeshIdIdentical)
                                normalPos += Mesh.MorphData.MorphTargets[morphDesc.TargetIndex + lastMeshIdIdenticalCount + 1].Normals[vrtIndex];
                            asciiWriter.Write($"{normalPos.X:F6} ");
                            asciiWriter.Write($"{normalPos.Y:F6} ");
                            asciiWriter.Write($"{normalPos.Z:F6}");
                            asciiWriter.WriteLine();

                            //vertex color
                            asciiWriter.Write(vertTbl.VertexColor[vrtIndex].R + " ");
                            asciiWriter.Write(vertTbl.VertexColor[vrtIndex].G + " ");
                            asciiWriter.Write(vertTbl.VertexColor[vrtIndex].B + " ");
                            asciiWriter.Write(vertTbl.VertexColor[vrtIndex].A);
                            asciiWriter.WriteLine();

                            //uv coords
                            for (int curUVLayer = 0; curUVLayer < vertTbl.UVLayerCount; curUVLayer++)
                                asciiWriter.WriteLine(vertTbl.UVPosX[vrtIndex, curUVLayer].ToString("F6") + " " + vertTbl.UVPosY[vrtIndex, curUVLayer].ToString("F6"));

                            if (SKEL.Unknown1 != Int32.MaxValue)
                            {
                                //weight ids
                                asciiWriter.Write(SKEL.NodeNames[NodesIdsNames[weightTbl.WeightIds[vertTbl.Weights[vrtIndex], 0]]] + " ");
                                asciiWriter.Write(SKEL.NodeNames[NodesIdsNames[weightTbl.WeightIds[vertTbl.Weights[vrtIndex], 1]]] + " ");
                                asciiWriter.Write(SKEL.NodeNames[NodesIdsNames[weightTbl.WeightIds[vertTbl.Weights[vrtIndex], 2]]] + " ");
                                asciiWriter.Write(SKEL.NodeNames[NodesIdsNames[weightTbl.WeightIds[vertTbl.Weights[vrtIndex], 3]]]);
                                asciiWriter.WriteLine();

                                //weight values
                                asciiWriter.Write(weightTbl.WeightValues[vertTbl.Weights[vrtIndex], 0].ToString("F6") + " ");
                                asciiWriter.Write(weightTbl.WeightValues[vertTbl.Weights[vrtIndex], 1].ToString("F6") + " ");
                                asciiWriter.Write(weightTbl.WeightValues[vertTbl.Weights[vrtIndex], 2].ToString("F6") + " ");
                                asciiWriter.Write(weightTbl.WeightValues[vertTbl.Weights[vrtIndex], 3].ToString("F6"));
                                asciiWriter.WriteLine();
                            }
                        }

                        //face count
                        asciiWriter.WriteLine(faceTbl.VertCount / 3);
                        for (int k = 0; k < faceTbl.VertCount; k += 3)
                        {
                            int faceVertex2 = faceTbl.Vertices[k] - lowestVertId;
                            int faceVertex1 = faceTbl.Vertices[k + 1] - lowestVertId;
                            int faceVertex0 = faceTbl.Vertices[k + 2] - lowestVertId;
                            //face vertex ids
                            asciiWriter.WriteLine($"{faceVertex0} {faceVertex1} {faceVertex2}");
                        }
                    }
                }
            }

            App.PushLog("Writing .ascii file...");
            asciiWriter.Flush();
            asciiWriter.Dispose();
        }

        public void ModelToGLTF(Structs.Mesh Mesh, Structs.MXMD MXMD, Structs.SKEL SKEL)
        {
            List<int>[] ValidMeshes = VerifyMeshes(Mesh, MXMD);

            MeshBuilder<VertexPositionNormal, VertexEmpty, VertexJoints16x4>[] meshBuilders = new MeshBuilder<VertexPositionNormal, VertexEmpty, VertexJoints16x4>[MXMD.Materials.Length];
            Dictionary<string, MaterialBuilder> nameToMat = new Dictionary<string, MaterialBuilder>();

            for (int i = 0; i < MXMD.MaterialHeader.Count; i++)
            {
                MaterialBuilder material = new MaterialBuilder(MXMD.Materials[i].Name)
                    .WithMetallicRoughnessShader()
                    .WithChannelParam(KnownChannels.BaseColor, new Vector4(1, 1, 1, 1));
                nameToMat.Add(MXMD.Materials[i].Name, material);
            }

            int lastMeshIdIdenticalCount = 0;
            bool lastMeshIdIdentical = false;
            Structs.MeshVertexTable weightTbl = Mesh.VertexTables.Last();
            for (int i = 0; i < ValidMeshes.Length; i++)
            {
                for (int j = 0; j < ValidMeshes[i].Count; j++)
                {
                    lastMeshIdIdentical = j == 0 ? false : ValidMeshes[j - 1] == ValidMeshes[j];

                    if (lastMeshIdIdentical)
                        lastMeshIdIdenticalCount++;
                    else
                        lastMeshIdIdenticalCount = 0;

                    int meshId = ValidMeshes[i][j];

                    Structs.MXMDMeshDescriptor MXMDMesh = MXMD.Version == Int32.MaxValue ? default(Structs.MXMDMeshDescriptor) : MXMD.ModelStruct.Meshes[j].Descriptors[meshId];
                    Structs.MeshVertexTable vertTbl = Mesh.VertexTables[MXMDMesh.VertTableIndex];
                    Structs.MeshFaceTable faceTbl = Mesh.FaceTables[MXMDMesh.FaceTableIndex];

                    MeshBuilder<VertexPositionNormal, VertexEmpty, VertexJoints16x4> meshBuilder = new MeshBuilder<VertexPositionNormal, VertexEmpty, VertexJoints16x4>($"mesh{meshId}{(App.LOD == -1 ? $"_LOD{MXMDMesh.LOD}_" : "")}{(lastMeshIdIdentical ? $"flex_{MXMD.ModelStruct.MorphControls.Controls[lastMeshIdIdenticalCount - 1].Name}" : "")}");
                    PrimitiveBuilder<MaterialBuilder, VertexPositionNormal, VertexEmpty, VertexJoints16x4> meshPrim = meshBuilder.UsePrimitive((MXMD.Version == Int32.MaxValue ? new MaterialBuilder("NO_MATERIALS") : nameToMat[MXMD.Materials[MXMDMesh.MaterialID].Name]));

                    for (int k = 0; k < faceTbl.VertCount; k += 3)
                    {
                        Vector3 vert0 = vertTbl.Vertices[faceTbl.Vertices[k]];
                        Vector3 vert1 = vertTbl.Vertices[faceTbl.Vertices[k + 1]];
                        Vector3 vert2 = vertTbl.Vertices[faceTbl.Vertices[k + 2]];
                        Vector3 norm0 = new Vector3(vertTbl.Normals[faceTbl.Vertices[k]].X, vertTbl.Normals[faceTbl.Vertices[k]].Y, vertTbl.Normals[faceTbl.Vertices[k]].Z);
                        Vector3 norm1 = new Vector3(vertTbl.Normals[faceTbl.Vertices[k + 1]].X, vertTbl.Normals[faceTbl.Vertices[k + 1]].Y, vertTbl.Normals[faceTbl.Vertices[k + 1]].Z);
                        Vector3 norm2 = new Vector3(vertTbl.Normals[faceTbl.Vertices[k + 2]].X, vertTbl.Normals[faceTbl.Vertices[k + 2]].Y, vertTbl.Normals[faceTbl.Vertices[k + 2]].Z);

                        meshPrim.AddTriangle(new GLTFVert(new VertexPositionNormal(vert0, norm0)), new GLTFVert(new VertexPositionNormal(vert1, norm1)), new GLTFVert(new VertexPositionNormal(vert2, norm2)));
                    }

                    meshBuilders[j] = meshBuilder;
                }
            }

            ModelRoot model = ModelRoot.CreateModel();
            IReadOnlyList<Mesh> meshes = model.CreateMeshes(meshBuilders);
            Scene scene = model.UseScene("default");
            Node node = scene.CreateNode(App.CurFileNameNoExt);

            foreach (Mesh m in meshes)
                node.CreateNode().WithMesh(m);

            App.PushLog("Writing .glb file...");
            model.SaveGLB($@"{App.CurOutputPath}\{App.CurFileNameNoExt + ".glb"}");
        }

        public void ReadTextures(Structs.MSRD MSRD, string texturesFolderPath, List<Structs.LBIM> LBIMs = null)
        {
            App.PushLog("Reading textures...");

            if (!Directory.Exists(texturesFolderPath))
                Directory.CreateDirectory(texturesFolderPath);

            if (LBIMs == null)
                LBIMs = new List<Structs.LBIM>();
            
            for (int i = 0; i < MSRD.DataItemsCount; i++)
            {
                Structs.MSRDDataItem dataItem = MSRD.DataItems[i];
                Stream sStream = MSRD.TOC[1].Data;
                BinaryReader brTexture = new BinaryReader(sStream);
                int Offset = dataItem.Offset;
                int Size = dataItem.Size;

                switch (dataItem.Type)
                {
                    case Structs.MSRDDataItemTypes.Texture:
                        Structs.LBIM TextureLBIM = ReadLBIM(sStream, brTexture, Offset, Size, dataItem);
                        LBIMs.Add(TextureLBIM);
                        break;
                    case Structs.MSRDDataItemTypes.CachedTextures:
                        BinaryReader meshData = new BinaryReader(MSRD.TOC[0].Data);
                        for (int j = 0; j < MSRD.TextureInfo.Length; j++)
                        {
                            Structs.LBIM CacheLBIM = ReadLBIM(MSRD.TOC[0].Data, meshData, dataItem.Offset + MSRD.TextureInfo[j].Offset, MSRD.TextureInfo[j].Size);
                            LBIMs.Add(CacheLBIM);
                        }
                        break;
                }
            }

            for (int i = 0; i < LBIMs.Count; i++)
            {
                Structs.LBIM LBIM = LBIMs[i];
                MemoryStream TextureData = LBIM.Data;

                if (LBIM.DataItem.TOCIndex != 0 && LBIM.DataItem.Type == Structs.MSRDDataItemTypes.Texture)
                    TextureData = MSRD.TOC[LBIM.DataItem.TOCIndex - 1].Data;

                TextureData.Seek(0x0, SeekOrigin.Begin);

                int TextureType = 0;
                switch (LBIMs[i].Type)
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
                        App.PushLog($"Unknown texture type {LBIMs[i].Type}! Skipping texture...");
                        continue;
                }

                int ImageWidth = LBIM.DataItem.Size == 0 ? LBIM.Width : LBIM.Width * 2;
                int ImageHeight = LBIM.DataItem.Size == 0 ? LBIM.Height : LBIM.Height * 2;
                int TextureUnswizzleBufferSize = BitsPerPixel[TextureType] * 2;
                int SwizzleSize = 4;

                int DDSFourCC = 0x30315844; //DX10
                switch (TextureType)
                {
                    case 71:
                        DDSFourCC = 0x31545844; //DXT1
                        break;
                    case 74:
                        DDSFourCC = 0x33545844; //DXT3
                        break;
                    case 77:
                        DDSFourCC = 0x35545844; //DXT5
                        break;
                    case 80:
                        DDSFourCC = 0x31495441; //ATI1
                        break;
                    case 83:
                        DDSFourCC = 0x32495441; //ATI2
                        break;
                }

                //this will rewrite the small versions of the textures, but that's not a big deal considering they're so tiny
                string filename = $@"{texturesFolderPath}\";
                if (MSRD.DataItemsCount > 0)
                {
                    int NameIndex = i < MSRD.TextureInfo.Length ? i : MSRD.TextureIds[i % MSRD.TextureInfo.Length];
                    filename = $"{NameIndex:d2}.{MSRD.TextureNames[NameIndex]}";
                }
                else
                    filename += $"file{i}";
                

                FileStream fsTexture;
                if (LBIMs[i].Type == 37)
                {
                    fsTexture = new FileStream(filename + ".tga", FileMode.Create);
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
                    fsTexture = new FileStream(filename + ".dds", FileMode.Create);
                    BinaryWriter bwTexture = new BinaryWriter(fsTexture);
                    bwTexture.Write(0x7C20534444); //magic
                    bwTexture.Write(0x1007); //flags
                    bwTexture.Write(ImageHeight);
                    bwTexture.Write(ImageWidth);
                    bwTexture.Write(TextureData.Length);
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
                byte[] TextureUnswizzled = new byte[TextureData.Length];

                int ImageHeightInTiles = ImageHeight / SwizzleSize;
                int ImageWidthInTiles = ImageWidth / SwizzleSize;

                int ImageRowCount = ImageHeightInTiles / 8;
                if (ImageRowCount > 16)
                    ImageRowCount = 16;
                else if (ImageRowCount == 0)
                    ImageRowCount = 1;

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

                                    if (SwizzleSize == 1 || ImageWidth == SwizzleSize)
                                    {
                                        TextureUnswizzleBuffer[2] = (byte)TextureData.ReadByte();
                                        TextureUnswizzleBuffer[1] = (byte)TextureData.ReadByte();
                                        TextureUnswizzleBuffer[0] = (byte)TextureData.ReadByte();
                                        TextureUnswizzleBuffer[3] = (byte)TextureData.ReadByte();
                                        somethingHeight = ImageHeight - somethingHeight - 1;
                                    }
                                    else
                                    {
                                        TextureData.Read(TextureUnswizzleBuffer, 0, TextureUnswizzleBufferSize);
                                    }

                                    int destinationIndex = TextureUnswizzleBufferSize * (somethingHeight * ImageWidthInTiles + somethingWidth);
                                    Array.Copy(TextureUnswizzleBuffer, 0, TextureUnswizzled, destinationIndex, TextureUnswizzleBufferSize);
                                }
                            }
                        }
                    }
                }

                fsTexture.Write(TextureUnswizzled, 0, (int)TextureData.Length);
                fsTexture.Dispose();
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
