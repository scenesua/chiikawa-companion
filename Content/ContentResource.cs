using System;
using System.IO;
using System.Reflection;

namespace Momonga.Content;

public static class ContentResource
{
    public static Stream Open(string path) => Assembly.GetExecutingAssembly().GetManifestResourceStream(path)
        ?? throw new FileNotFoundException("Missing bundled content", path);
}
