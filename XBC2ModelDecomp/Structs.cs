using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Reflection;
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

        public static string ReflectToString(object parent, int tabCount = 1, int arrayLimit = 100)
        {
            string output = "";

            Type parentType = parent.GetType();
            
            foreach (FieldInfo field in parentType.GetFields())
            {
                object value = field.GetValue(parent);
                if (value == null)
                    continue;

                if (value is int || value is uint ||
                    value is short || value is ushort ||
                    value is byte || value is sbyte)
                {
                    output += "\n";
                    for (int i = 0; i < tabCount; i++)
                        output += "\t";

                    output += $"{field.Name}: 0x{value:X} ({value})";
                }
                else if (value is string)
                {
                    output += "\n";
                    for (int i = 0; i < tabCount; i++)
                        output += "\t";

                    output += $"{field.Name}: {value}";
                }
                else if (field.FieldType.IsEnum)
                {
                    output += "\n";
                    for (int i = 0; i < tabCount; i++)
                        output += "\t";

                    object enumValue = Enum.Parse(field.FieldType, value.ToString());

                    output += $"{field.Name}: {enumValue} ({Convert.ToInt32(enumValue)})";
                }
                else if (field.FieldType.IsArray)
                {
                    Array array = field.GetValue(parent) as Array;

                    output += "\n";
                    for (int i = 0; i < tabCount; i++)
                        output += "\t";

                    output += $"{field.Name}[{array.Length}]: ";

                    if (array.Rank > 1 || array.Length > arrayLimit || array is Vector3[] || array is Quaternion[] || array is Color[])
                        continue;

                    if (array is byte[])
                    {
                        output += $"0x{BitConverter.ToString(array as byte[]).Replace("-", "")}";
                    }
                    else
                    {
                        for (int i = 0; i < array.Length; i++)
                        {
                            output += "\n";
                            for (int j = 0; j < tabCount + 1; j++)
                                output += "\t";

                            if (field.FieldType.Namespace == "XBC2ModelDecomp")
                            {
                                output += $"Item {i}:";
                                output += ReflectToString(array.GetValue(i), tabCount + 2);
                            }
                            else
                            {
                                switch (array.GetValue(i).GetType().Name)
                                {
                                    case nameof(Int32):
                                    case nameof(UInt32):
                                    case nameof(Int16):
                                    case nameof(UInt16):
                                        output += $"Item {i}: 0x{array.GetValue(i):X} ({array.GetValue(i)})";
                                        break;
                                    case nameof(String):
                                        output += $"Item {i}: {array.GetValue(i)}";
                                        break;
                                    default:
                                        //output += $"({field.FieldType.Name}) {field.Name}: {field.GetValue(parent)}";
                                        break;
                                }

                            }
                        }
                    }
                }
                else if (field.FieldType.Namespace == "XBC2ModelDecomp")
                {
                    output += "\n";
                    for (int i = 0; i < tabCount; i++)
                        output += "\t";

                    output += $"{field.Name}:";
                    output += ReflectToString(value, tabCount + 1);
                }
            }

            return output;
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

        public struct LBIM //its reverse?
        {
            public MSRDDataItem DataItem; //not in struct

            public MemoryStream Data;

            public int Unknown5;
            public int Unknown4;

            public int Width;
            public int Height;

            public int Unknown3;
            public int Unknown2;

            public int Type;
            public int Unknown1;
            public int Version;
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
                output += ReflectToString(this);
                return output;
            }
        }

        public enum MSRDDataItemTypes : ushort
        {
            Model = 0,
            ShaderBundle,
            CachedTextures,
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
                output += ReflectToString(this);
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
                output += ReflectToString(this);
                return output;
            }
        }

        public struct MXMDModelStruct
        {
            public int Unknown1;
            public Vector3 BoundingBoxStart;
            public Vector3 BoundingBoxEnd;
            public int MeshesOffset;
            public int MeshesCount;
            public int Unknown3;
            public int NodesOffset;

            public byte[] Unknown4; //0x54 long

            public int MorphControllersOffset;
            public int MorphNamesOffset;

            public MXMDMorphControls MorphControls;

            public MXMDMorphNames MorphNames;

            public MXMDMeshes[] Meshes;

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

            public MXMDMeshDescriptor[] Descriptors;
        }

        public struct MXMDMeshDescriptor
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

            public override string ToString()
            {
                string output = "SAR1:";
                output += ReflectToString(this);
                return output;
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

            public override string ToString()
            {
                string output = "SAR1:";
                output += ReflectToString(this);
                return output;
            }
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

            public XBC1 FileBySearch(string search)
            {
                return Files[Array.FindIndex(Files, x => x.Name.Contains(search))];
            }

            public XBC1[] FilesBySearch(string search)
            {
                return Files.Where(x => x.Name.Contains(search)).ToArray();
            }
        }

        //winvhe
        public struct NVMS
        {
            public int Unknown1; //version?

            public int NVDATableOffset;
            public int NVDATableCount;

            public int Table2Offset;
            public int Table2Count;
            public int Table3Offset;
            public int Table3Count;

            public byte[] Reserved1; //0x20 long

            public NVMSNVDA[] NVDAs;
        }

        public struct NVMSNVDA
        {
            public int Unknown1;
            public int NVDAOffset;
            public int Unknown2;
            public int SecondFileOffset;
        }

        public struct MapInfo
        {
            public int Unknown1;
            public int Unknown2;
            public int Unknown3;

            public int MeshTableOffset;
            public int MaterialTableOffset;

            public byte[] Unknown4; //0x24 long

            public int TableIndexOffset;

            //the rest of these will be all over the place

            public MXMDMaterialHeader MaterialHeader;
            public MXMDMaterial[] Materials;

            public int MeshTableDataOffset;
            public int MeshTableDataCount;

            public int MeshFileLookupOffset;
            public int MeshFileLookupCount;

            public short[] MeshFileLookup;

            public MapInfoMeshTable[] MeshTables;

            public override string ToString()
            {
                string output = "MapInfo:";
                output += ReflectToString(this);
                return output;
            }
        }

        public struct MapInfoMeshTable
        {
            public int MeshOffset;
            public int MeshCount;

            public MXMDMeshDescriptor[] Descriptors;
        }
    }
}
