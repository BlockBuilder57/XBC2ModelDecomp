﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Threading.Tasks;
using zlib;

namespace XBC2ModelDecomp
{
    public class ModelTools
    {
        private FormatTools ft = MainFormTest.FormatTools;

        public void ExtractModels()
        {
            App.PushLog($"Reading {App.CurFileNameNoExt}...");

            //wismt
            FileStream fsWISMT = new FileStream(App.CurFilePathAndName + ".wismt", FileMode.Open, FileAccess.Read);
            BinaryReader brWISMT = new BinaryReader(fsWISMT);

            Structs.MSRD MSRD = ft.ReadMSRD(fsWISMT, brWISMT);

            if (App.ExportAnims)
            {
                if (File.Exists(App.CurFilePathAndName + ".mot"))
                {
                    foreach (string file in Directory.GetFiles(App.CurFilePath, $"{App.CurFileNameNoExt}*.mot"))
                    {
                        FileStream fsMOT = new FileStream(file, FileMode.Open, FileAccess.Read);
                        BinaryReader brMOT = new BinaryReader(fsMOT);

                        Structs.SAR1 SAR1 = ft.ReadSAR1(fsMOT, brMOT, @"\Animations\", App.ExportAnims);

                        brMOT.Dispose();
                        fsMOT.Dispose();
                    }
                }
                else
                    App.PushLog("No .mot file exists, continuing...");
            }

            //start mesh file
            if (MSRD.TOC.Length > 0)
            {
                if (App.ExportFormat != Structs.ExportFormat.None && !Directory.Exists(App.CurOutputPath))
                    Directory.CreateDirectory(App.CurOutputPath);

                if (App.ExportTextures)
                    ft.ReadTextures(MSRD, $@"{App.CurOutputPath}\Textures");

                BinaryReader brCurFile = new BinaryReader(MSRD.TOC[0].Data); //start new file

                Structs.Mesh Mesh = ft.ReadMesh(MSRD.TOC[0].Data, brCurFile);

                Structs.MXMD MXMD = new Structs.MXMD { Version = Int32.MaxValue };
                if (File.Exists(App.CurFilePathAndName + ".wimdo"))
                {
                    FileStream fsWIMDO = new FileStream(App.CurFilePathAndName + ".wimdo", FileMode.Open, FileAccess.Read);
                    BinaryReader brWIMDO = new BinaryReader(fsWIMDO);

                    MXMD = ft.ReadMXMD(fsWIMDO, brWIMDO);
                }

                Structs.SAR1 SAR1 = new Structs.SAR1 { Version = Int32.MaxValue };
                Structs.SKEL SKEL = new Structs.SKEL { Unknown1 = Int32.MaxValue };
                if (File.Exists(App.CurFilePathAndName + ".arc"))
                {
                    FileStream fsARC = new FileStream(App.CurFilePathAndName + ".arc", FileMode.Open, FileAccess.Read);
                    BinaryReader brARC = new BinaryReader(fsARC);

                    SAR1 = ft.ReadSAR1(fsARC, brARC, @"\RawFiles\", App.ExportFormat == Structs.ExportFormat.RawFiles);
                    BinaryReader brSKEL = new BinaryReader(SAR1.ItemBySearch(".skl").Data);
                    SKEL = ft.ReadSKEL(brSKEL.BaseStream, brSKEL);
                }

                if (App.ShowInfo)
                {
                    App.PushLog(MSRD.ToString());
                    App.PushLog(Mesh.ToString());
                    App.PushLog(MXMD.ToString());
                }

                switch (App.ExportFormat)
                {
                    case Structs.ExportFormat.XNALara:
                        ft.ModelToASCII(new Structs.Mesh[] { Mesh }, MXMD, SKEL, new Structs.MapInfo { Unknown1 = Int32.MaxValue });
                        break;
                    case Structs.ExportFormat.glTF:
                        ft.ModelToGLTF(new Structs.Mesh[] { Mesh }, MXMD, SKEL, new Structs.MapInfo { Unknown1 = Int32.MaxValue });
                        break;
                }

                App.PushLog($"Finished {App.CurFileNameNoExt}!");
            }
            else
            {
                App.PushLog($"No files found in {App.CurFilePathAndName}.wismt?");
            }

            brWISMT.Dispose();
            fsWISMT.Dispose();
        }
    }
}
