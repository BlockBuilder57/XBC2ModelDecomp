using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace XBC2ModelDecomp
{
    public class Structs
    {
        public struct DRSM
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

            public byte[] Unknown1;

            public int TextureIdsCount;
            public int TextureIdsOffset;
            public int TextureCountOffset;

            public DRSMDataItem[] DataItems;
            public TOC[] TOC;

            public short[] TextureIds;
            public int TextureCount;
            public int TextureChunkSize;
            public int Unknown2; //texture related
            public int TextureStringBufferOffset;
            public DRSMTextureInfo[] TextureInfo;
            public string[] TextureNames;
        }

        public enum DRSMDataItemTypes : ushort
        {
            Model = 0,
            ShaderBundle,
            CachedTexture,
            Texture
        }

        public struct DRSMDataItem
        {
            public int Offset;
            public int Size;
            public short id1;
            public DRSMDataItemTypes Type;
        }

        public struct TOC
        {
            public int CompSize;
            public int FileSize;
            public int Offset;
        }

        public struct DRSMTexture
        {
            public int Size;
            public int Offset;
            public string Name;
        }

        public struct DRSMTextureInfo
        {
            public int Unknown1;
            public int Size;
            public int Offset;
            public int StringOffset;
        }
    }
}
