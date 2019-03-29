using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Threading.Tasks;
using zlib;

namespace XBC2ModelDecomp
{
    class ModelTools
    {
        FormatTools ft = new FormatTools();

        public ModelTools(string[] args)
        {
            FileStream fsWIMDO = new FileStream(Path.GetFileNameWithoutExtension(args[0]) + ".wimdo", FileMode.Open, FileAccess.Read);
            BinaryReader brWIMDO = new BinaryReader(fsWIMDO);

            ReadMXMD(fsWIMDO, brWIMDO);


            //wismt
            FileStream fsWISMT = new FileStream(Path.GetFileNameWithoutExtension(args[0]) + ".wismt", FileMode.Open, FileAccess.Read);
            BinaryReader brWISMT = new BinaryReader(fsWISMT);

            int MSRDMagic = brWISMT.ReadInt32();
            if (MSRDMagic != 0x4D535244)
            {
                Console.WriteLine("wismt is corrupt (or wrong endianness)!");
                return;
            }

            Structs.MSRD MSRD = new Structs.MSRD
            {
                Version = brWISMT.ReadInt32(),
                HeaderSize = brWISMT.ReadInt32(),
                MainOffset = brWISMT.ReadInt32(), //0xC 0x10, 16

                Tag = brWISMT.ReadInt32(),
                Revision = brWISMT.ReadInt32(),

                DataItemsCount = brWISMT.ReadInt32(), //0x18 0x10, 16
                DataItemsOffset = brWISMT.ReadInt32(), //0x1C 0x4C, 76
                FileCount = brWISMT.ReadInt32(), //0x20 0xE, 14
                TOCOffset = brWISMT.ReadInt32(), //0x24 0x18C, 396

                Unknown1 = brWISMT.ReadBytes(28),

                TextureIdsCount = brWISMT.ReadInt32(), //0x44
                TextureIdsOffset = brWISMT.ReadInt32(), //0x48 0x234, 564
                TextureCountOffset = brWISMT.ReadInt32() //0x4C 0x24E, 590
            };

            MSRD.DataItems = new Structs.MSRDDataItem[MSRD.DataItemsCount];
            fsWISMT.Seek(MSRD.MainOffset + MSRD.DataItemsOffset, SeekOrigin.Begin); //0x5C, 92
            for (int i = 0; i < MSRD.DataItemsCount; i++)
            {
                MSRD.DataItems[i] = new Structs.MSRDDataItem
                {
                    Offset = brWISMT.ReadInt32(),
                    Size = brWISMT.ReadInt32(),
                    id1 = brWISMT.ReadInt16(),
                    Type = (Structs.MSRDDataItemTypes)brWISMT.ReadInt16()
                };
                fsWISMT.Seek(0x8, SeekOrigin.Current);
            }

            MemoryStream msCurFile = new MemoryStream();

            if (MSRD.TextureIdsCount > 0 && MSRD.TextureCountOffset > 0)
            {
                fsWISMT.Seek(MSRD.MainOffset + MSRD.TextureIdsOffset, SeekOrigin.Begin); //0x244, 580
                MSRD.TextureIds = new short[MSRD.TextureIdsCount];
                for (int curTextureId = 0; curTextureId < MSRD.TextureIdsCount; curTextureId++)
                {
                    MSRD.TextureIds[curTextureId] = brWISMT.ReadInt16();
                }

                fsWISMT.Seek(MSRD.MainOffset + MSRD.TextureCountOffset, SeekOrigin.Begin); //0x25E, 606
                MSRD.TextureCount = brWISMT.ReadInt32(); //0x25E
                MSRD.TextureChunkSize = brWISMT.ReadInt32();
                MSRD.Unknown2 = brWISMT.ReadInt32();
                MSRD.TextureStringBufferOffset = brWISMT.ReadInt32();

                MSRD.TextureInfo = new Structs.MSRDTextureInfo[MSRD.TextureCount];
                for (int curTextureNameOffset = 0; curTextureNameOffset < MSRD.TextureCount; curTextureNameOffset++)
                {
                    MSRD.TextureInfo[curTextureNameOffset].Unknown1 = brWISMT.ReadInt32();
                    MSRD.TextureInfo[curTextureNameOffset].Size = brWISMT.ReadInt32();
                    MSRD.TextureInfo[curTextureNameOffset].Offset = brWISMT.ReadInt32();
                    MSRD.TextureInfo[curTextureNameOffset].StringOffset = brWISMT.ReadInt32();
                }

                MSRD.TextureNames = new string[MSRD.TextureCount];
                for (int curTextureName = 0; curTextureName < MSRD.TextureCount; curTextureName++)
                {
                    fsWISMT.Seek(MSRD.MainOffset + MSRD.TextureCountOffset + MSRD.TextureInfo[curTextureName].StringOffset, SeekOrigin.Begin);
                    MSRD.TextureNames[curTextureName] = FormatTools.ReadNullTerminatedString(brWISMT);
                }

                MSRD.TOC = new Structs.TOC[MSRD.FileCount];
                fsWISMT.Seek(MSRD.MainOffset + MSRD.TOCOffset, SeekOrigin.Begin);
                for (int curFileOffset = 0; curFileOffset < MSRD.FileCount; curFileOffset++)
                {
                    MSRD.TOC[curFileOffset].CompSize = brWISMT.ReadInt32();
                    MSRD.TOC[curFileOffset].FileSize = brWISMT.ReadInt32();
                    MSRD.TOC[curFileOffset].Offset = brWISMT.ReadInt32();
                }

                string texturesFolderPath = Path.GetFileNameWithoutExtension(args[0]) + "_textures";
                if (!Directory.Exists(texturesFolderPath))
                    Directory.CreateDirectory(texturesFolderPath);
                ft.ReadTextures(fsWISMT, brWISMT, MSRD, texturesFolderPath);

                msCurFile = ft.XBC1(fsWISMT, brWISMT, MSRD.TOC[0].Offset, "meshfile.bin", Path.GetFileNameWithoutExtension(args[0]) + "_files");
            }

            //start mesh file
            if (msCurFile != null)
            {
                BinaryReader brCurFile = new BinaryReader(msCurFile); //start new file
                ft.ModelToASCII(msCurFile, brCurFile, args);
            }
            else
                Console.WriteLine("mesh pointer wrong!");
        }

        public Structs.MXMD ReadMXMD(FileStream fsWIMDO, BinaryReader brWIMDO)
        {
            int MXMDMagic = brWIMDO.ReadInt32();
            if (MXMDMagic != 0x4D584D44)
            {
                Console.WriteLine("wimdo is corrupt (or wrong endianness)!");
                return new Structs.MXMD { Version = Int32.MaxValue };
            }

            Structs.MXMD MXMD = new Structs.MXMD
            {
                Version = brWIMDO.ReadInt32(),

                ModelStructOffset = brWIMDO.ReadInt32(),
                MaterialsOffset = brWIMDO.ReadInt32(),

                Unknown1 = brWIMDO.ReadInt32(),

                VertexBufferOffset = brWIMDO.ReadInt32(),
                ShadersOffset = brWIMDO.ReadInt32(),
                CachedTexturesTableOffset = brWIMDO.ReadInt32(),
                Unknown2 = brWIMDO.ReadInt32(),
                UncachedTexturesTableOffset = brWIMDO.ReadInt32(),

                Unknown3 = brWIMDO.ReadBytes(0x28)
            };

            MXMD.ModelStruct = new Structs.MXMDModelStruct
            {
                Unknown1 = brWIMDO.ReadInt32(),
                BoundingBoxStart = new Vector3(brWIMDO.ReadSingle(), brWIMDO.ReadSingle(), brWIMDO.ReadSingle()),
                BoundingBoxEnd = new Vector3(brWIMDO.ReadSingle(), brWIMDO.ReadSingle(), brWIMDO.ReadSingle()),
                MeshesOffset = brWIMDO.ReadInt32(),
                Unknown2 = brWIMDO.ReadInt32(),
                Unknown3 = brWIMDO.ReadInt32(),
                NodesOffset = brWIMDO.ReadInt32(),

                Unknown4 = brWIMDO.ReadBytes(0x54),

                MorphControllersOffset = brWIMDO.ReadInt32(),
                MorphNamesOffset = brWIMDO.ReadInt32()
            };

            if (MXMD.ModelStruct.MorphControllersOffset != 0)
            {
                fsWIMDO.Seek(MXMD.ModelStructOffset + MXMD.ModelStruct.MorphControllersOffset, SeekOrigin.Begin);
                MXMD.ModelStruct.MorphControls = new Structs.MXMDMorphControls
                {
                    Unknown1 = brWIMDO.ReadInt32(),
                    Count = brWIMDO.ReadInt32(),

                    Unknown2 = brWIMDO.ReadBytes(0x10)
                };

                MXMD.ModelStruct.MorphControls.Controls = new Structs.MXMDMorphControl[MXMD.ModelStruct.MorphControls.Count];
                long nextPosition = fsWIMDO.Position;
                for (int i = 0; i < MXMD.ModelStruct.MorphControls.Count; i++)
                {
                    fsWIMDO.Seek(nextPosition, SeekOrigin.Begin);
                    nextPosition += 0x1C;
                    MXMD.ModelStruct.MorphControls.Controls[i] = new Structs.MXMDMorphControl
                    {
                        NameOffset1 = brWIMDO.ReadInt32(),
                        NameOffset2 = brWIMDO.ReadInt32(), //the results of these should be identical
                        Unknown1 = brWIMDO.ReadBytes(0x14)
                    };

                    fsWIMDO.Seek(MXMD.ModelStructOffset + MXMD.ModelStruct.MorphControllersOffset + MXMD.ModelStruct.MorphControls.Controls[i].NameOffset1, SeekOrigin.Begin);
                    MXMD.ModelStruct.MorphControls.Controls[i].Name = FormatTools.ReadNullTerminatedString(brWIMDO);
                }
            }

            if (MXMD.ModelStruct.MorphNamesOffset != 0)
            {
                fsWIMDO.Seek(MXMD.ModelStructOffset + MXMD.ModelStruct.MorphNamesOffset, SeekOrigin.Begin);
                MXMD.ModelStruct.MorphNames = new Structs.MXMDMorphNames
                {
                    Unknown1 = brWIMDO.ReadInt32(),
                    Count = brWIMDO.ReadInt32(),

                    Unknown2 = brWIMDO.ReadBytes(0x20)
                };

                MXMD.ModelStruct.MorphNames.Names = new Structs.MXMDMorphName[MXMD.ModelStruct.MorphNames.Count];
                long nextPosition = fsWIMDO.Position;
                for (int i = 0; i < MXMD.ModelStruct.MorphNames.Count; i++)
                {
                    fsWIMDO.Seek(nextPosition, SeekOrigin.Begin);
                    nextPosition += 0x10;
                    MXMD.ModelStruct.MorphNames.Names[i] = new Structs.MXMDMorphName
                    {
                        NameOffset = brWIMDO.ReadInt32(),
                        Unknown1 = brWIMDO.ReadInt32(),
                        Unknown2 = brWIMDO.ReadInt32(),
                        Unknown3 = brWIMDO.ReadInt32(),
                    };

                    fsWIMDO.Seek(MXMD.ModelStructOffset + MXMD.ModelStruct.MorphControllersOffset + MXMD.ModelStruct.MorphNames.Names[i].NameOffset, SeekOrigin.Begin);
                    MXMD.ModelStruct.MorphNames.Names[i].Name = FormatTools.ReadNullTerminatedString(brWIMDO);
                }
            }

            if (MXMD.ModelStruct.MeshesOffset != 0)
            {
                fsWIMDO.Seek(MXMD.ModelStructOffset + MXMD.ModelStruct.MeshesOffset, SeekOrigin.Begin);
                MXMD.ModelStruct.Meshes = new Structs.MXMDMeshes
                {
                    TableOffset = brWIMDO.ReadInt32(),
                    TableCount = brWIMDO.ReadInt32(),
                    Unknown1 = brWIMDO.ReadInt32(),

                    BoundingBoxStart = new Vector3(brWIMDO.ReadSingle(), brWIMDO.ReadSingle(), brWIMDO.ReadSingle()),
                    BoundingBoxEnd = new Vector3(brWIMDO.ReadSingle(), brWIMDO.ReadSingle(), brWIMDO.ReadSingle()),
                    BoundingRadius = brWIMDO.ReadSingle()
                };

                fsWIMDO.Seek(MXMD.ModelStructOffset + MXMD.ModelStruct.Meshes.TableOffset, SeekOrigin.Begin);
                MXMD.ModelStruct.Meshes.Meshes = new Structs.MXMDMesh[MXMD.ModelStruct.Meshes.TableCount];
                for (int i = 0; i < MXMD.ModelStruct.Meshes.TableCount; i++)
                {
                    MXMD.ModelStruct.Meshes.Meshes[i] = new Structs.MXMDMesh
                    {
                        //ms says to add 1 to some of these, why?
                        ID = brWIMDO.ReadInt32(),

                        Descriptor = brWIMDO.ReadInt32(),

                        VTBuffer = brWIMDO.ReadInt16(),
                        UVFaces = brWIMDO.ReadInt16(),

                        Unknown1 = brWIMDO.ReadInt16(),
                        MaterialID = brWIMDO.ReadInt16(),
                        Unknown2 = brWIMDO.ReadBytes(0xC),
                        Unknown3 = brWIMDO.ReadInt16(),

                        LOD = brWIMDO.ReadInt16(),
                        Unknown4 = brWIMDO.ReadInt32(),

                        Unknown5 = brWIMDO.ReadBytes(0xC),
                    };
                }
            }

            if (MXMD.ModelStruct.NodesOffset != 0)
            {
                fsWIMDO.Seek(MXMD.ModelStructOffset + MXMD.ModelStruct.NodesOffset, SeekOrigin.Begin);
                MXMD.ModelStruct.Nodes = new Structs.MXMDNodes
                {
                    BoneCount = brWIMDO.ReadInt32(),
                    BoneCount2 = brWIMDO.ReadInt32(),

                    NodeIdsOffset = brWIMDO.ReadInt32(),
                    NodeTmsOffset = brWIMDO.ReadInt32()
                };
            }

            return MXMD;
        }
    }
}
