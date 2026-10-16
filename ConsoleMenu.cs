using System.Text;
using PdfSharp.Pdf;

namespace PdfTool;

static class ConsoleMenu
{
    public static void Run()
    {
        Console.WriteLine("=== PDF Tool ===");
        Console.WriteLine("1) Encrypt PDF");
        Console.WriteLine("2) Metadata Modifier");
        Console.Write("Choose: ");
        string? choice = Console.ReadLine();

        if (choice == "1")
            EncryptMode();
        else if (choice == "2")
            MetadataMode();
        else
            Console.WriteLine("Invalid choice.");
    }

    static void EncryptMode()
    {
        string path = AskPath();
        if (!TryOpen(path, out PdfDocument? source))
            return;

        using (source)
        {
            string userPw = ReadPassword("Password to OPEN the file (user): ");
            string ownerPw = ReadPassword("Password to CHANGE permissions (owner): ");
            bool allowPrint = AskYesNo("Allow printing?", true);
            bool allowCopy = AskYesNo("Allow copying text?", true);
            bool allowEdit = AskYesNo("Allow editing?", true);

            using var doc = PdfFiles.EditableCopy(source!);
            if (!PdfFiles.Protect(doc, userPw, ownerPw, allowPrint, allowCopy, allowEdit, out var error))
            {
                Console.WriteLine(error);
                return;
            }

            Console.WriteLine("Saved: " + PdfFiles.SaveAs(doc, path, "Encrypted"));
        }
    }

    static void MetadataMode()
    {
        string path = AskPath();
        if (!TryOpen(path, out PdfDocument? source))
            return;

        using (source)
        {
            var xmp = XmpTool.Read(source!);
            if (xmp != null)
            {
                Console.WriteLine();
                Console.WriteLine("--- XMP currently says ---");
                Console.WriteLine("Title:    " + XmpTool.Title(xmp));
                Console.WriteLine("Author:   " + XmpTool.Author(xmp));
                Console.WriteLine("Subject:  " + XmpTool.Subject(xmp));
                Console.WriteLine("Keywords: " + XmpTool.Keywords(xmp));
                Console.WriteLine("Creator:  " + XmpTool.Creator(xmp));
            }
            else
            {
                Console.WriteLine();
                Console.WriteLine("No XMP metadata found in this file.");
            }

            using var doc = PdfFiles.EditableCopy(source!);
            Console.WriteLine();
            Console.WriteLine("--- Info dictionary (press Enter to keep a value) ---");
            doc.Info.Title = Prompt("Title", doc.Info.Title);
            doc.Info.Author = Prompt("Author", doc.Info.Author);
            doc.Info.Subject = Prompt("Subject", doc.Info.Subject);
            doc.Info.Keywords = Prompt("Keywords", doc.Info.Keywords);
            doc.Info.Creator = Prompt("Creator", doc.Info.Creator);

            if (xmp != null && AskYesNo("Also write these values into the XMP so they match?", true))
            {
                XmpTool.Apply(xmp, doc.Info.Title, doc.Info.Author, doc.Info.Subject, doc.Info.Keywords, doc.Info.Creator);
                XmpTool.Write(doc, xmp);
            }

            Console.WriteLine("Saved: " + PdfFiles.SaveAs(doc, path, "MetaModified"));
        }
    }

    static bool TryOpen(string path, out PdfDocument? source)
    {
        source = null;
        if (PdfFiles.TryOpen(path, null, out source, out var error))
            return true;

        if (error != null && error.Contains("required", StringComparison.OrdinalIgnoreCase))
        {
            for (int i = 0; i < 3; i++)
            {
                string pw = ReadPassword("Password: ");
                if (PdfFiles.TryOpen(path, pw, out source, out error))
                    return true;
                Console.WriteLine(error);
            }
            return false;
        }

        Console.WriteLine(error);
        return false;
    }

    static string AskPath()
    {
        while (true)
        {
            Console.Write("PDF file path (you can drag the file into the terminal): ");
            string input = (Console.ReadLine() ?? "").Trim().Trim('\'', '"', ' ');
            if (File.Exists(input))
                return input;
            Console.WriteLine("File not found, try again.");
        }
    }

    static string Prompt(string label, string? current)
    {
        current ??= "";
        Console.Write($"{label} [{current}]: ");
        string? input = Console.ReadLine();
        return string.IsNullOrEmpty(input) ? current : input;
    }

    static bool AskYesNo(string question, bool defaultYes)
    {
        Console.Write($"{question} {(defaultYes ? "(Y/n)" : "(y/N)")}: ");
        string input = (Console.ReadLine() ?? "").Trim().ToLowerInvariant();
        if (input.Length == 0)
            return defaultYes;
        return input == "y" || input == "yes";
    }

    static string ReadPassword(string prompt)
    {
        Console.Write(prompt);
        var sb = new StringBuilder();
        while (true)
        {
            var key = Console.ReadKey(true);
            if (key.Key == ConsoleKey.Enter)
            {
                Console.WriteLine();
                break;
            }
            if (key.Key == ConsoleKey.Backspace)
            {
                if (sb.Length > 0)
                    sb.Length--;
                continue;
            }
            sb.Append(key.KeyChar);
        }
        return sb.ToString();
    }
}
