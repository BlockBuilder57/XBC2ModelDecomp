using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Numerics;
using zlib;

namespace XBC2ModelDecomp
{   
    public class Program
    {
        public static void Main(string[] args)
        {
            FormatTools ft = new FormatTools();

            if (args.Length == 0)
            {
                Console.WriteLine("For models, a .wismt, .wimdo, and a .arc file must exist with the same name in the same directory in order for the file to be fully extracted.");
                Environment.Exit(0);
            }

            Console.WriteLine(Path.GetFileName(args[0]));

            string[] fileArr = Path.GetFileName(args[0]).Split('.');
            switch (fileArr[fileArr.Length-1].ToLower())
            {
                case "wismda":
                    new MapTools(args);
                    break;
                case "arc":
                case "wimdo":
                case "wismt":
                    new ModelTools(args);
                    break;
                case "bin": //assume it's a raw model file
                    FileStream fs = new FileStream(args[0], FileMode.Open, FileAccess.Read);
                    MemoryStream ms = new MemoryStream();
                    fs.CopyTo(ms);
                    BinaryReader br = new BinaryReader(ms);
                    ft.ModelToASCII(ms, br, args);
                    break;
            }
        }
    }
}
