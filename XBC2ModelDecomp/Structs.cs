using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Media;

namespace XBC2ModelDecomp
{
    public class Structs
    {
        public enum ExportFormat
        {
            None,
            XNALara,
            glTF
        }

        public struct XBC1
        {
            public int Version;
            public int FileSize;
            public int CompressedSize;
            public int Unknown1;

            public string Name;

            public MemoryStream Data;

            public override string ToString()
            {
                string output = "xbc1:";

                output += $"\n\tVersion: {Version}";
                output += $"\n\tFileSize: 0x{FileSize:X}";
                output += $"\n\tCompressedSize: 0x{CompressedSize:X}";
                output += $"\n\tUnknown1: 0x{Unknown1:X}";

                output += $"\n\tName: {Name}";

                return output;
            }
        }


        //wismt
        public struct MSRD
        {
            public int Version;
            public int HeaderSize;
            public int MainOffset;

            public int Tag;
            public int Revision;

            public int DataItemsCount;
            public int DataItemsOffset;
            public int FileCount;
            public int TOCOffset;

            public byte[] Unknown1; //0x1C long

            public int TextureIdsCount;
            public int TextureIdsOffset;
            public int TextureCountOffset;

            public MSRDDataItem[] DataItems;
            public MSRDTOC[] TOC;

            public short[] TextureIds;
            public int TextureCount;
            public int TextureChunkSize;
            public int Unknown2; //texture related
            public int TextureStringBufferOffset;
            public MSRDTextureInfo[] TextureInfo;
            public string[] TextureNames;

            public override string ToString()
            {
                string output = "MSRD:";

                output += $"\n\tVersion: {Version}";
                output += $"\n\tHeaderSize: 0x{HeaderSize:X}";
                output += $"\n\tMainOffset: 0x{MainOffset:X}";

                output += $"\n\n\tTag: {Tag}";
                output += $"\n\tRevision: {Revision}";

                output += $"\n\n\tDataItemsCount: {DataItemsCount}";
                output += $"\n\tDataItemsOffset: {DataItemsOffset}";
                output += $"\n\tFileCount: {FileCount}";
                output += $"\n\tTOCOffset: 0x{TOCOffset:X}";

                output += $"\n\n\tUnknown1: 0x{BitConverter.ToString(Unknown1).Replace("-", "")}";

                output += $"\n\n\tTextureIdsCount: {TextureIdsCount}";
                output += $"\n\tTextureIdsOffset: 0{TextureIdsOffset:X}";
                output += $"\n\tTextureCountOffset: 0x{TextureCountOffset:X}";

                output += $"\n\n\tDataItems[{DataItems.Length}]:";
                for (int i = 0; i < DataItems.Length; i++)
                {
                    output += $"\n\t\tItem {i}:";
                    output += $"\n\t\t\tOffset: 0x{DataItems[i].Offset:X}";
                    output += $"\n\t\t\tSize: 0x{DataItems[i].Size:X}";
                    output += $"\n\t\t\tTOCIndex: {DataItems[i].TOCIndex}";
                    output += $"\n\t\t\tType: {DataItems[i].Type} ({(int)DataItems[i].Type})";
                }

                output += $"\n\tTOC[{TOC.Length}]:";
                for (int i = 0; i < TOC.Length; i++)
                {
                    output += $"\n\t\tItem {i}:";
                    output += $"\n\t\t\tCompSize: 0x{TOC[i].CompSize:X}";
                    output += $"\n\t\t\tFileSize: 0x{TOC[i].FileSize:X}";
                    output += $"\n\t\t\tOffset: 0x{TOC[i].Offset:X}";
                }

                output += $"\n\n\tTextureIds[{TextureIds.Length}]:";
                for (int i = 0; i < TextureIds.Length; i++)
                    output += $"\n\t\tItem {i}: {TextureIds[i]}";

                output += $"\n\tTextureCount: {TextureCount}";
                output += $"\n\tTextureChunkSize: 0x{TextureChunkSize:X}";
                output += $"\n\tUnknown2: {Unknown2}";
                output += $"\n\tTextureStringBufferOffset: 0x{TextureStringBufferOffset:X}";
                output += $"\n\tTextureInfo[{TextureInfo.Length}]:";
                for (int i = 0; i < TextureInfo.Length; i++)
                {
                    output += $"\n\t\tItem {i}:";
                    output += $"\n\t\t\tUnknown1: 0x{TextureInfo[i].Unknown1:X}";
                    output += $"\n\t\t\tSize: 0x{TextureInfo[i].Size:X}";
                    output += $"\n\t\t\tOffset: 0x{TextureInfo[i].Offset:X}";
                    output += $"\n\t\t\tStringOffset: 0x{TextureInfo[i].StringOffset:X}";
                }
                output += $"\n\tTextureNames[{TextureNames.Length}]:";
                for (int i = 0; i < TextureNames.Length; i++)
                    output += $"\n\t\tItem {i}: {TextureNames[i]}";

                return output;
            }
        }

        public enum MSRDDataItemTypes : ushort
        {
            Model = 0,
            ShaderBundle,
            CachedTexture,
            Texture
        }

        public struct MSRDDataItem
        {
            public int Offset;
            public int Size;
            public short TOCIndex;
            public MSRDDataItemTypes Type;
        }

        public struct MSRDTOC
        {
            public int CompSize;
            public int FileSize;
            public int Offset;

            public MemoryStream Data;
        }

        public struct MSRDTexture
        {
            public int Size;
            public int Offset;
            public string Name;
        }

        public struct MSRDTextureInfo
        {
            public int Unknown1;
            public int Size;
            public int Offset;
            public int StringOffset;
        }


        //wismt file0
        public struct Mesh
        {
            public int VertexTableOffset;
            public int VertexTableCount;
            public int FaceTableOffset;
            public int FaceTableCount;

            public byte[] Reserved1; //0xC long

            public int UnknownOffset1;
            public int UnknownOffset2;
            public int UnknownOffset2Count;

            public int MorphDataOffset;
            public int DataSize;
            public int DataOffset;
            public int WeightDataSize;
            public int WeightDataOffset;

            public byte[] Reserved2; //0x14 long

            public MeshVertexTable[] VertexTables;
            public MeshFaceTable[] FaceTables;

            //public byte[] Reserved3; //0x30 long

            public List<MeshVertexDescriptor> VertexDescriptors;

            public MeshWeightData WeightData;
            public MeshMorphData MorphData;

            public override string ToString()
            {
                string output = "Mesh:";

                output += $"\n\tVertexTableOffset: 0x{VertexTableOffset:X}";
                output += $"\n\tVertexTableCount: {VertexTableCount}";
                output += $"\n\tFaceTableOffset: 0x{FaceTableOffset:X}";
                output += $"\n\tFaceTableCount: {FaceTableCount}";

                output += $"\n\n\tReserved1: 0x{BitConverter.ToString(Reserved1).Replace("-", "")}";

                output += $"\n\n\tUnknownOffset1: 0x{UnknownOffset1:X}";
                output += $"\n\tUnknownOffset2: 0x{UnknownOffset2:X}";
                output += $"\n\tUnknownOffset2Count: {UnknownOffset2Count}";

                output += $"\n\n\tMorphDataOffset: 0x{MorphDataOffset:X}";
                output += $"\n\tDataSize: 0x{DataSize:X}";
                output += $"\n\tDataOffset: 0x{DataOffset:X}";
                output += $"\n\tWeightDataSize: 0x{WeightDataSize:X}";
                output += $"\n\tWeightDataOffset: 0x{WeightDataOffset:X}";

                output += $"\n\n\tReserved2: 0x{BitConverter.ToString(Reserved2).Replace("-", "")}";

                output += $"\n\n\tVertexTables[{VertexTableCount}]:";
                for (int i = 0; i < VertexTableCount; i++)
                {
                    output += $"\n\t\tItem {i}:";
                    output += $"\n\t\t\tDataOffset 0x{VertexTables[i].DataOffset:X}";
                    output += $"\n\t\t\tDataCount {VertexTables[i].DataCount}";
                    output += $"\n\t\t\tBlockSize 0x{VertexTables[i].BlockSize:X}";

                    output += $"\n\n\t\t\tDescOffset 0x{VertexTables[i].DescOffset:X}";
                    output += $"\n\t\t\tDescCount {VertexTables[i].DescCount}";

                    output += $"\n\n\t\t\tUnknown1: 0x{BitConverter.ToString(VertexTables[i].Unknown1).Replace("-", "")}";
                }
                output += $"\n\tFaceTables[{FaceTableCount}]:";
                for (int i = 0; i < FaceTableCount; i++)
                {
                    output += $"\n\t\tItem {i}:";
                    output += $"\n\t\t\tOffset 0x{FaceTables[i].Offset:X}";
                    output += $"\n\t\t\tVertCount {FaceTables[i].VertCount}";

                    output += $"\n\n\t\t\tUnknown1: 0x{BitConverter.ToString(FaceTables[i].Unknown1).Replace("-", "")}";
                }

                //output += $"\n\n\tReserved3: 0x{BitConverter.ToString(Reserved3).Replace("-", "")}";

                output += $"\n\n\tVertexDescriptors[{VertexDescriptors.Count}]:";
                for (int i = 0; i < VertexDescriptors.Count; i++)
                {
                    output += $"\n\t\tItem {i}:";
                    output += $"\n\t\t\tType {VertexDescriptors[i].Type}";
                    output += $"\n\t\t\tSize 0x{VertexDescriptors[i].Size:X}";
                }

                output += $"\n\n\tWeightData:";
                output += $"\n\t\tWeightManagerCount: {WeightData.WeightManagerCount}";
                output += $"\n\t\tWeightManagerOffset: 0x{WeightData.WeightManagerOffset:X}";

                output += $"\n\n\t\tVertexTableIndex: {WeightData.VertexTableIndex}";
                output += $"\n\t\tUnknown2: 0x{WeightData.Unknown2:X}";

                output += $"\n\n\t\tOffset02: 0x{WeightData.Offset02:X}";

                output += $"\n\n\t\tWeightManagers[{WeightData.WeightManagerCount}]:";
                for (int i = 0; i < WeightData.WeightManagerCount; i++)
                {
                    output += $"\n\t\t\tItem {i}:";
                    output += $"\n\t\t\t\tUnknown1: 0x{WeightData.WeightManagers[i].Unknown1:X}";
                    output += $"\n\t\t\t\tOffset: 0x{WeightData.WeightManagers[i].Offset:X}";
                    output += $"\n\t\t\t\tCount: {WeightData.WeightManagers[i].Count}";

                    output += $"\n\n\t\t\t\tUnknown2: 0x{BitConverter.ToString(WeightData.WeightManagers[i].Unknown2).Replace("-", "")}";
                    output += $"\n\t\t\t\tLOD: 0x{WeightData.WeightManagers[i].LOD:X}";
                    output += $"\n\t\t\t\tUnknown3: 0x{BitConverter.ToString(WeightData.WeightManagers[i].Unknown3).Replace("-", "")}";
                }

                output += $"\n\n\tMorphData:";
                output += $"\n\t\tMorphDescriptorsCount: {MorphData.MorphDescriptorsCount}";
                output += $"\n\t\tMorphDescriptorsOffset: 0x{MorphData.MorphDescriptorsOffset:X}";

                output += $"\n\n\t\tMorphDescriptors[{MorphData.MorphDescriptorsCount}]:";
                for (int i = 0; i < MorphData.MorphDescriptorsCount; i++)
                {
                    output += $"\n\t\t\tItem {i}:";
                    output += $"\n\t\t\t\tBufferID: {MorphData.MorphDescriptors[i].BufferID}";

                    output += $"\n\n\t\t\t\tTargetIndex: {MorphData.MorphDescriptors[i].TargetIndex}";
                    output += $"\n\t\t\t\tTargetCounts: {MorphData.MorphDescriptors[i].TargetCounts}";
                    output += $"\n\t\t\t\tTargetIDOffsets: 0x{MorphData.MorphDescriptors[i].TargetIDOffsets:X}";

                    output += $"\n\n\t\t\t\tUnknown1: 0x{MorphData.MorphDescriptors[i].Unknown1:X}";
                }

                output += $"\n\n\t\tMorphTargetsCount: {MorphData.MorphTargetsCount}";
                output += $"\n\t\tMorphTargetsOffset: 0x{MorphData.MorphTargetsOffset:X}";

                output += $"\n\n\t\tMorphTargets[{MorphData.MorphTargetsCount}]:";
                for (int i = 0; i < MorphData.MorphTargetsCount; i++)
                {
                    output += $"\n\t\t\tItem {i}:";
                    output += $"\n\t\t\t\tBufferOffset: 0x{MorphData.MorphTargets[i].BufferOffset:X}";
                    output += $"\n\t\t\t\tVertCount: {MorphData.MorphTargets[i].VertCount}";
                    output += $"\n\t\t\t\tBlockSize: 0x{MorphData.MorphTargets[i].BlockSize:X}";

                    output += $"\n\n\t\t\t\tUnknown1: {MorphData.MorphTargets[i].Unknown1}";
                    output += $"\n\t\t\t\tType: {MorphData.MorphTargets[i].Type}";
                }

                return output;
            }
        }

        public struct MeshVertexTable
        {
            public int DataOffset;
            public int DataCount;
            public int BlockSize;

            public int DescOffset;
            public int DescCount;

            public byte[] Unknown1; //0xC long

            public MeshVertexDescriptor[] Descriptors; //not in struct

            //these are right after the actual vertex struct, but not in this order
            public Vector3[] Vertices;
            public int[] Weights;
            public float[,] UVPosX;
            public float[,] UVPosY;
            public int UVLayerCount;
            public Color[] VertexColor;
            public Quaternion[] Normals;
            public float[,] WeightValues;
            public byte[,] WeightIds;
        }

        public struct MeshFaceTable
        {
            public int Offset;
            public int VertCount;

            public byte[] Unknown1; //0xC long

            public ushort[] Vertices; //not in struct
        }

        public struct MeshVertexDescriptor
        {
            public short Type;
            public short Size;
        }

        public struct MeshWeightData
        {
            public int WeightManagerCount;
            public int WeightManagerOffset;

            public short VertexTableIndex;
            public short Unknown2;

            public int Offset02;

            public MeshWeightManager[] WeightManagers;
        }

        public struct MeshWeightManager
        {
            public int Unknown1;
            public int Offset;
            public int Count;

            public byte[] Unknown2; //0x11 long
            public byte LOD;
            public byte[] Unknown3; //0xA long
        }

        public struct MeshMorphData
        {
            public int MorphDescriptorsCount;
            public int MorphDescriptorsOffset;

            public MeshMorphDescriptor[] MorphDescriptors;

            public int MorphTargetsCount;
            public int MorphTargetsOffset;

            public MeshMorphTarget[] MorphTargets;
        }

        public struct MeshMorphDescriptor
        {
            public int BufferID;

            public int TargetIndex;
            public int TargetCounts;
            public int TargetIDOffsets;

            public int Unknown1;

            public short[] TargetIDs; //not in struct
        }

        public struct MeshMorphTarget
        {
            public int BufferOffset;
            public int VertCount;
            public int BlockSize;

            public short Unknown1;
            public short Type;

            public Vector3[] Vertices; //not in struct
            public Quaternion[] Normals; //not in struct
        }


        //wimdo
        public struct MXMD
        {
            public int Version;

            public int ModelStructOffset;
            public int MaterialsOffset;

            public int Unknown1;

            public int VertexBufferOffset;
            public int ShadersOffset;
            public int CachedTexturesTableOffset;
            public int Unknown2;
            public int UncachedTexturesTableOffset;

            public byte[] Unknown3; //0x28 long

            public MXMDModelStruct ModelStruct;

            public MXMDMaterialHeader MaterialHeader;
            public MXMDMaterial[] Materials;

            public override string ToString()
            {
                string output = "MXMD:";

                output += $"\n\tVersion: {Version}";

                output += $"\n\n\tModelStructOffset: 0x{ModelStructOffset:X}";
                output += $"\n\tMaterialsOffset: 0x{MaterialsOffset:X}";

                output += $"\n\n\tUnknown1: 0x{Unknown1:X}";

                output += $"\n\n\tVertexBufferOffset: 0x{VertexBufferOffset:X}";
                output += $"\n\tShadersOffset: 0x{ShadersOffset:X}";
                output += $"\n\tCachedTexturesTableOffset: 0x{CachedTexturesTableOffset:X}";
                output += $"\n\tUnknown2: 0x{Unknown2:X}";
                output += $"\n\tUncachedTexturesTableOffset: 0x{UncachedTexturesTableOffset:X}";

                output += $"\n\n\tUnknown3: 0x{BitConverter.ToString(Unknown3).Replace("-", "")}";

                output += $"\n\n\tModelStruct:";
                output += $"\n\t\tUnknown1: 0x{ModelStruct.Unknown1:X}";
                output += $"\n\t\tBoundingBoxStart: {ModelStruct.BoundingBoxStart:F6}";
                output += $"\n\t\tBoundingBoxEnd: {ModelStruct.BoundingBoxEnd:F6}";
                output += $"\n\t\tMeshesOffset: 0x{ModelStruct.MeshesOffset:X}";
                output += $"\n\t\tUnknown2: 0x{ModelStruct.Unknown2:X}";
                output += $"\n\t\tUnknown3: 0x{ModelStruct.Unknown3:X}";
                output += $"\n\t\tNodesOffset: 0x{ModelStruct.NodesOffset:X}";

                output += $"\n\n\t\tUnknown4: 0x{BitConverter.ToString(ModelStruct.Unknown4).Replace("-", "")}";

                output += $"\n\n\t\tMorphControllersOffset: 0x{ModelStruct.MorphControllersOffset:X}";
                output += $"\n\t\tMorphNamesOffset: 0x{ModelStruct.MorphNamesOffset:X}";


                output += $"\n\n\t\tMorphControls:";
                output += $"\n\t\t\tTableOffset: 0x{ModelStruct.MorphControls.TableOffset:X}";
                output += $"\n\t\t\tCount: {ModelStruct.MorphControls.Count}";

                if (ModelStruct.MorphControls.Count == 0)
                    output += $"\n\n\t\t\tUnknown2: 0x";
                else
                    output += $"\n\n\t\t\tUnknown2: 0x{BitConverter.ToString(ModelStruct.MorphControls.Unknown2).Replace("-", "")}";

                output += $"\n\n\t\t\tControls[{ModelStruct.MorphControls.Count}]:";
                for (int i = 0; i < ModelStruct.MorphControls.Count; i++)
                {
                    output += $"\n\t\t\t\tItem {i}:";
                    output += $"\n\t\t\t\t\tNameOffset1: 0x{ModelStruct.MorphControls.Controls[i].NameOffset1:X}";
                    output += $"\n\t\t\t\t\tNameOffset2: 0x{ModelStruct.MorphControls.Controls[i].NameOffset2:X}";
                }


                output += $"\n\n\t\tMorphNames:";
                output += $"\n\t\t\tTableOffset: 0x{ModelStruct.MorphNames.TableOffset:X}";
                output += $"\n\t\t\tCount: {ModelStruct.MorphNames.Count}";

                output += $"\n\n\t\t\tUnknown2: 0x{BitConverter.ToString(ModelStruct.MorphNames.Unknown2).Replace("-", "")}";

                output += $"\n\n\t\t\tNames[{ModelStruct.MorphNames.Count}]:";
                for (int i = 0; i < ModelStruct.MorphNames.Count; i++)
                {
                    output += $"\n\t\t\t\tItem {i}:";
                    output += $"\n\t\t\t\t\tNameOffset: 0x{ModelStruct.MorphNames.Names[i].NameOffset:X}";
                    output += $"\n\t\t\t\t\tUnknown1: 0x{ModelStruct.MorphNames.Names[i].Unknown1:X}";
                    output += $"\n\t\t\t\t\tUnknown2: 0x{ModelStruct.MorphNames.Names[i].Unknown2:X}";
                    output += $"\n\t\t\t\t\tUnknown3: 0x{ModelStruct.MorphNames.Names[i].Unknown3:X}";

                    output += $"\n\n\t\t\t\t\tName: {ModelStruct.MorphNames.Names[i].Name}";
                }


                output += $"\n\n\t\tMeshes:";
                output += $"\n\t\t\tTableOffset: 0x{ModelStruct.Meshes.TableOffset:X}";
                output += $"\n\t\t\tTableCount: {ModelStruct.Meshes.TableCount}";
                output += $"\n\t\t\tUnknown1: 0x{ModelStruct.Meshes.Unknown1:X}";

                output += $"\n\n\t\t\tBoundingBoxStart: {ModelStruct.Meshes.BoundingBoxStart:F6}";
                output += $"\n\t\t\tBoundingBoxEnd: {ModelStruct.Meshes.BoundingBoxEnd:F6}";
                output += $"\n\t\t\tBoundingRadius: {ModelStruct.Meshes.BoundingRadius}";

                output += $"\n\n\t\t\tMeshes[{ModelStruct.Meshes.TableCount}]:";
                for (int i = 0; i < ModelStruct.Meshes.TableCount; i++)
                {
                    output += $"\n\t\t\t\tItem {i}:";
                    output += $"\n\t\t\t\t\tID: {ModelStruct.Meshes.Meshes[i].ID}";
                    output += $"\n\t\t\t\t\tDescriptor: {ModelStruct.Meshes.Meshes[i].Descriptor}";

                    output += $"\n\n\t\t\t\t\tVertTableIndex: {ModelStruct.Meshes.Meshes[i].VertTableIndex}";
                    output += $"\n\t\t\t\t\tFaceTableIndex: {ModelStruct.Meshes.Meshes[i].FaceTableIndex}";

                    output += $"\n\n\t\t\t\t\tUnknown1: {ModelStruct.Meshes.Meshes[i].Unknown1}";
                    output += $"\n\t\t\t\t\tMaterialID: {ModelStruct.Meshes.Meshes[i].MaterialID}";
                    output += $"\n\t\t\t\t\tUnknown2: 0x{BitConverter.ToString(ModelStruct.Meshes.Meshes[i].Unknown2).Replace("-", "")}";
                    output += $"\n\t\t\t\t\tUnknown3: {ModelStruct.Meshes.Meshes[i].Unknown3}";

                    output += $"\n\n\t\t\t\t\tLOD: {ModelStruct.Meshes.Meshes[i].LOD}";
                    output += $"\n\t\t\t\t\tUnknown4: 0x{ModelStruct.Meshes.Meshes[i].Unknown4:X}";

                    output += $"\n\n\t\t\t\t\tUnknown5: 0x{BitConverter.ToString(ModelStruct.Meshes.Meshes[i].Unknown5).Replace("-", "")}";
                }


                output += $"\n\n\t\tNodes:";
                output += $"\n\t\t\tBoneCount: {ModelStruct.Nodes.BoneCount}";
                output += $"\n\t\t\tBoneCount2: {ModelStruct.Nodes.BoneCount2}";

                output += $"\n\n\t\t\tNodeIdsOffset: 0x{ModelStruct.Nodes.NodeIdsOffset:X}";
                output += $"\n\t\t\tNodeTmsOffset: 0x{ModelStruct.Nodes.NodeTmsOffset:X}";

                output += $"\n\n\t\t\tNodes[{ModelStruct.Nodes.BoneCount}]:";
                for (int i = 0; i < ModelStruct.Nodes.BoneCount; i++)
                {
                    output += $"\n\t\t\t\tItem {i}:";
                    output += $"\n\t\t\t\t\tNameOffset: 0x{ModelStruct.Nodes.Nodes[i].NameOffset:X}";
                    output += $"\n\t\t\t\t\tUnknown1: {ModelStruct.Nodes.Nodes[i].Unknown1}f";
                    output += $"\n\t\t\t\t\tUnknown2: {ModelStruct.Nodes.Nodes[i].Unknown2}";

                    output += $"\n\n\t\t\t\t\tID: {ModelStruct.Nodes.Nodes[i].ID}";
                    output += $"\n\t\t\t\t\tUnknown3: {ModelStruct.Nodes.Nodes[i].Unknown3}";
                    output += $"\n\t\t\t\t\tUnknown4: {ModelStruct.Nodes.Nodes[i].Unknown4}";

                    output += $"\n\n\t\t\t\t\tName: {ModelStruct.Nodes.Nodes[i].Name}";

                    output += $"\n\n\t\t\t\t\tScale: {ModelStruct.Nodes.Nodes[i].Scale:F6}";
                    output += $"\n\t\t\t\t\tRotation: {ModelStruct.Nodes.Nodes[i].Rotation:F6}";
                    output += $"\n\t\t\t\t\tPosition: {ModelStruct.Nodes.Nodes[i].Position:F6}";

                    output += $"\n\n\t\t\t\t\tParentTransform: {ModelStruct.Nodes.Nodes[i].ParentTransform:F6}";
                }

                output += $"\n\n\tMaterialHeader:";
                output += $"\n\t\tOffset: 0x{MaterialHeader.Offset:X}";
                output += $"\n\t\tCount: {MaterialHeader.Count}";

                output += $"\n\n\tMaterials[{MaterialHeader.Count}]:";
                for (int i = 0; i < MaterialHeader.Count; i++)
                {
                    output += $"\n\t\tItem {i}:";
                    output += $"\n\t\t\tNameOffset: 0x{Materials[i].NameOffset:X}";

                    output += $"\n\n\t\t\tUnknown1: 0x{BitConverter.ToString(Materials[i].Unknown1).Replace("-", "")}";

                    output += $"\n\n\t\t\tName: {Materials[i].Name}";
                }

                return output;
            }
        }

        public struct MXMDModelStruct
        {
            public int Unknown1;
            public Vector3 BoundingBoxStart;
            public Vector3 BoundingBoxEnd;
            public int MeshesOffset;
            public int Unknown2;
            public int Unknown3;
            public int NodesOffset;

            public byte[] Unknown4; //0x54 long

            public int MorphControllersOffset;
            public int MorphNamesOffset;

            public MXMDMorphControls MorphControls;

            public MXMDMorphNames MorphNames;

            public MXMDMeshes Meshes;

            public MXMDNodes Nodes;
        }

        public struct MXMDMorphControls
        {
            public int TableOffset;
            public int Count;

            public byte[] Unknown2; //0x10 long

            public MXMDMorphControl[] Controls;
        }

        public struct MXMDMorphControl
        {
            public int NameOffset1;
            public int NameOffset2;

            public byte[] Unknown1; //0x14 long

            public string Name; //not in real struct
        }

        public struct MXMDMorphNames
        {
            public int TableOffset;
            public int Count;

            public byte[] Unknown2; //0x20 long

            public MXMDMorphName[] Names;
        }

        public struct MXMDMorphName
        {
            public int NameOffset;
            public int Unknown1;
            public int Unknown2;
            public int Unknown3;

            public string Name; //not in real struct
        }

        public struct MXMDMeshes
        {
            public int TableOffset;
            public int TableCount;
            public int Unknown1;

            public Vector3 BoundingBoxStart;
            public Vector3 BoundingBoxEnd;
            public float BoundingRadius;

            public MXMDMesh[] Meshes;
        }

        public struct MXMDMesh
        {
            public int ID;

            public int Descriptor;
            public int WeightBind; //not in struct

            public short VertTableIndex;
            public short FaceTableIndex;

            public short Unknown1;
            public short MaterialID;
            public byte[] Unknown2; //0xC long
            public short Unknown3;

            public short LOD;
            public int Unknown4;

            public byte[] Unknown5; //0xC long
        }

        public struct MXMDNodes
        {
            public int BoneCount;
            public int BoneCount2;

            public int NodeIdsOffset;
            public int NodeTmsOffset;

            public MXMDNode[] Nodes;
        }

        public struct MXMDNode
        {
            public int NameOffset;
            public float Unknown1;
            public int Unknown2;

            public int ID;
            public int Unknown3;
            public int Unknown4;

            public string Name; //not in struct

            public Quaternion Scale;
            public Quaternion Rotation;
            public Quaternion Position;

            public Quaternion ParentTransform;
        }

        public struct MXMDMaterialHeader
        {
            public int Offset;
            public int Count;
        }

        public struct MXMDMaterial
        {
            public int NameOffset;

            public byte[] Unknown1; //0x70 long oh no

            public string Name; //not in struct
        }


        //arc, mot
        public struct SAR1
        {
            public int FileSize;
            public int Version;
            public int NumFiles;
            public int TOCOffset;
            public int DataOffset;
            public int Unknown1;
            public int Unknown2;
            public string Path; //0x80 chars

            public SARTOC[] TOCItems;
            public SARBC[] BCItems;

            public SARBC ItemBySearch(string search)
            {
                return BCItems[Array.FindIndex(TOCItems, x => x.Filename.Contains(search))];
            }
        }

        public struct SARTOC
        {
            public int Offset;
            public int Size;
            public int Unknown1;
            public string Filename; //0x34 chars
        }

        public struct SARBC
        {
            public int BlockCount;
            public int FileSize;
            public int PointerCount;
            public int OffsetToData; //starts from blockcount, not magic

            public MemoryStream Data;
        }


        //arc
        public struct SKEL
        {
            public int Unknown1;
            public int Unknown2;

            public SKELTOC[] TOCItems;
            public short[] Parents;
            public SKELNodes[] Nodes;
            public SKELTransforms[] Transforms;

            public Dictionary<string, int> NodeNames; //not in struct
        }

        public struct SKELTOC
        {
            public int Offset;
            public int Unknown1;
            public int Count;
            public int Unknown2;
        }

        public struct SKELNodes
        {
            public int Offset;
            public byte[] Unknown1; //0xC long
            public string Name; //not in struct
        }

        public struct SKELTransforms
        {
            public Quaternion Position;
            public Quaternion Rotation;
            public Quaternion Scale;

            public Vector3 RealPosition; //not in struct
            public Quaternion RealRotation; //not in struct
        }


        //wismda
        public struct WISMDA
        {
            public XBC1[] Files;
        }
    }
}
