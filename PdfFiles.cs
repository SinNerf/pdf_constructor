using System.Text;
using System.Xml.Linq;
using PdfSharp.Pdf;
using PdfSharp.Pdf.IO;

namespace PdfTool;

static class PdfFiles
{
    public static bool TryOpen(string path, string? password, out PdfDocument? source, out string? error)
    {
        source = null;
        error = null;

        if (!File.Exists(path))
        {
            error = "File not found.";
            return false;
        }

        try
        {
            string? pw = string.IsNullOrEmpty(password) ? null : password;
            source = PdfReader.Open(path, pw, PdfDocumentOpenMode.Import, null, null);
            return true;
        }
        catch (PdfReaderException ex)
        {
            error = ex.Message;
            return false;
        }
    }

    public static PdfDocument EditableCopy(PdfDocument source)
    {
        var copy = new PdfDocument();
        foreach (PdfPage page in source.Pages)
            copy.AddPage(page);

        copy.Info.Title = source.Info.Title;
        copy.Info.Author = source.Info.Author;
        copy.Info.Subject = source.Info.Subject;
        copy.Info.Keywords = source.Info.Keywords;
        copy.Info.Creator = source.Info.Creator;
        return copy;
    }

    public static bool Protect(PdfDocument doc, string userPw, string ownerPw, bool allowPrint, bool allowCopy, bool allowEdit, out string? error)
    {
        if (userPw.Length == 0 || ownerPw.Length == 0 || userPw == ownerPw)
        {
            error = "Both passwords are required and must be different.";
            return false;
        }

        var s = doc.SecuritySettings;
        s.UserPassword = userPw;
        s.OwnerPassword = ownerPw;
        s.PermitPrint = allowPrint;
        s.PermitExtractContent = allowCopy;
        s.PermitModifyDocument = allowEdit;
        error = null;
        return true;
    }

    public static string SaveAs(PdfDocument doc, string inPath, string tag)
    {
        string dir = Path.GetDirectoryName(inPath) ?? ".";
        string name = Path.GetFileNameWithoutExtension(inPath);
        string outPath = Path.Combine(dir, $"{name}_Constructed_{tag}.pdf");
        doc.Save(outPath);
        return outPath;
    }
}

static class XmpTool
{
    static readonly XNamespace Rdf = "http://www.w3.org/1999/02/22-rdf-syntax-ns#";
    static readonly XNamespace Dc = "http://purl.org/dc/elements/1.1/";
    static readonly XNamespace Pdf = "http://ns.adobe.com/pdf/1.3/";
    static readonly XNamespace Xmp = "http://ns.adobe.com/xap/1.0/";

    public static XDocument? Read(PdfDocument doc)
    {
        try
        {
            var meta = doc.Internals.Catalog.Elements.GetDictionary("/Metadata");
            if (meta?.Stream == null)
                return null;
            return XDocument.Parse(Encoding.UTF8.GetString(meta.Stream.UnfilteredValue));
        }
        catch
        {
            return null;
        }
    }

    public static void Write(PdfDocument doc, XDocument xmp)
    {
        byte[] bytes = Encoding.UTF8.GetBytes(xmp.ToString(SaveOptions.DisableFormatting));
        var meta = doc.Internals.Catalog.Elements.GetDictionary("/Metadata");
        if (meta == null)
        {
            meta = new PdfDictionary(doc);
            meta.Elements.SetName("/Type", "/Metadata");
            meta.Elements.SetName("/Subtype", "/XML");
            meta.CreateStream(bytes);
            doc.Internals.Catalog.Elements.SetObject("/Metadata", meta);
            return;
        }

        meta.Elements.Remove("/Filter");
        meta.Elements.Remove("/DecodeParms");
        if (meta.Stream == null)
            meta.CreateStream(bytes);
        else
            meta.Stream.Value = bytes;
    }

    public static string Title(XDocument x) => Get(x, Dc, "title");
    public static string Author(XDocument x) => Get(x, Dc, "creator");
    public static string Subject(XDocument x) => Get(x, Dc, "description");
    public static string Keywords(XDocument x) => Get(x, Pdf, "Keywords");
    public static string Creator(XDocument x) => Get(x, Xmp, "CreatorTool");

    public static void Apply(XDocument xmp, string title, string author, string subject, string keywords, string creator)
    {
        var target = xmp.Descendants(Rdf + "Description").FirstOrDefault();
        if (target == null)
            return;

        Remove(xmp, Dc, "title");
        Remove(xmp, Dc, "creator");
        Remove(xmp, Dc, "description");
        Remove(xmp, Pdf, "Keywords");
        Remove(xmp, Xmp, "CreatorTool");

        if (title != "")
            target.Add(new XElement(Dc + "title", new XElement(Rdf + "Alt", new XElement(Rdf + "li", Lang(), title))));
        if (author != "")
            target.Add(new XElement(Dc + "creator", new XElement(Rdf + "Seq", new XElement(Rdf + "li", author))));
        if (subject != "")
            target.Add(new XElement(Dc + "description", new XElement(Rdf + "Alt", new XElement(Rdf + "li", Lang(), subject))));
        if (keywords != "")
            target.Add(new XElement(Pdf + "Keywords", keywords));
        if (creator != "")
            target.Add(new XElement(Xmp + "CreatorTool", creator));
    }

    static XAttribute Lang() => new(XNamespace.Xml + "lang", "x-default");

    static string Get(XDocument xmp, XNamespace ns, string name)
    {
        foreach (var desc in xmp.Descendants(Rdf + "Description"))
        {
            var attr = desc.Attribute(ns + name);
            if (attr != null)
                return attr.Value;
            var el = desc.Element(ns + name);
            if (el != null)
                return el.Descendants(Rdf + "li").FirstOrDefault()?.Value ?? el.Value;
        }
        return "";
    }

    static void Remove(XDocument xmp, XNamespace ns, string name)
    {
        foreach (var desc in xmp.Descendants(Rdf + "Description").ToList())
        {
            desc.Attribute(ns + name)?.Remove();
            desc.Element(ns + name)?.Remove();
        }
    }
}
