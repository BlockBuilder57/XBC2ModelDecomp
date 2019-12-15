using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace XBC2ModelDecomp
{
    public class TextureTools
    {
        private FormatTools ft = MainFormTest.FormatTools;

        public void ExtractTextures()
        {
            App.PushLog($"Reading {App.CurFileNameNoExt}...");

            //wismt
            FileStream fsWIFNT = new FileStream(App.CurFilePathAndName + ".wifnt", FileMode.Open, FileAccess.Read);
            BinaryReader brWIFNT = new BinaryReader(fsWIFNT);

            List<Structs.LBIM> TextureLBIMs = new List<Structs.LBIM>();

            fsWIFNT.Seek(-0x4, SeekOrigin.End);
            if (brWIFNT.ReadInt32() == 0x4D49424C)
            {
                Structs.LBIM lbim = ft.ReadLBIM(fsWIFNT, brWIFNT, 0x1000, (int)fsWIFNT.Length - 0x1000);
                lbim.Filename = "thistestcool";
                lbim.Type = 75;
                if (lbim.Data != null)
                    TextureLBIMs.Add(lbim);
            }

            ft.ReadTextures(new Structs.MSRD { Version = Int32.MaxValue }, App.CurFilePath, TextureLBIMs);

            App.PushLog("Done!");
        }
    }
}
