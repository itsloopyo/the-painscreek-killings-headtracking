using System;
using System.IO;

public static class PatcherMain
{
    public static int Main(string[] args)
    {
        if (args.Length != 2)
        {
            Console.Error.WriteLine("usage: BootstrapPatcher.exe <input-assembly> <output-assembly>");
            return 2;
        }

        File.Copy(args[0], args[1], true);
        return BootstrapPatcher.PatchAssembly(args[1]) ? 0 : 1;
    }
}
