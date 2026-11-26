using System;
using System.IO;

class Program
{
    static void Main()
    {
        var files = new[]
        {
            @"Presentation/Nop.Web/wwwroot/lib_npm/@fortawesome/fontawesome-free/css/all.min.css",
            @"Presentation/Nop.Web/wwwroot/lib_npm/@fortawesome/fontawesome-free/css/solid.min.css",
            @"Presentation/Nop.Web/wwwroot/lib_npm/@fortawesome/fontawesome-free/css/regular.min.css"
        };

        foreach (var path in files)
        {
            if (File.Exists(path))
            {
                var text = File.ReadAllText(path);
                var replaced = text.Replace("font-display:block", "font-display:swap");
                File.WriteAllText(path, replaced);
                Console.WriteLine($"Patched {Path.GetFileName(path)}: font-display:swap");
            }
        }
    }
}
