using System.Xml.Linq;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using PdfSharp.Pdf;

namespace PdfTool;

public partial class MainWindow : Window
{
    PdfDocument? _metaDoc;
    XDocument? _xmp;
    string? _metaPath;

    public MainWindow()
    {
        InitializeComponent();
    }

    void ShowEncryptPage(object? sender, RoutedEventArgs e)
    {
        EncryptPage.IsVisible = true;
        MetaPage.IsVisible = false;
        ShowEncrypt.Classes.Add("on");
        ShowMeta.Classes.Remove("on");
    }

    void ShowMetaPage(object? sender, RoutedEventArgs e)
    {
        EncryptPage.IsVisible = false;
        MetaPage.IsVisible = true;
        ShowMeta.Classes.Add("on");
        ShowEncrypt.Classes.Remove("on");
    }

    async void OnBrowseEncrypt(object? sender, RoutedEventArgs e)
    {
        var path = await PickPdf();
        if (path != null)
            EncryptPathBox.Text = path;
    }

    async void OnBrowseMeta(object? sender, RoutedEventArgs e)
    {
        var path = await PickPdf();
        if (path != null)
            MetaPathBox.Text = path;
    }

    void OnEncrypt(object? sender, RoutedEventArgs e)
    {
        string path = (EncryptPathBox.Text ?? "").Trim();
        if (!PdfFiles.TryOpen(path, EncryptOpenBox.Text, out var source, out var error))
        {
            SetStatus(error);
            return;
        }

        using (source)
        using (var doc = PdfFiles.EditableCopy(source!))
        {
            if (!PdfFiles.Protect(doc, EncryptUserBox.Text ?? "", EncryptOwnerBox.Text ?? "",
                    AllowPrintBox.IsChecked == true, AllowCopyBox.IsChecked == true, AllowEditBox.IsChecked == true, out error))
            {
                SetStatus(error);
                return;
            }

            SetStatus("Saved: " + PdfFiles.SaveAs(doc, path, "Encrypted"));
        }
    }

    void OnLoadMeta(object? sender, RoutedEventArgs e)
    {
        string path = (MetaPathBox.Text ?? "").Trim();
        if (!PdfFiles.TryOpen(path, MetaOpenBox.Text, out var source, out var error))
        {
            SetStatus(error);
            return;
        }

        using (source)
        {
            _metaDoc?.Dispose();
            _xmp = XmpTool.Read(source!);
            _metaDoc = PdfFiles.EditableCopy(source!);
            _metaPath = path;
        }

        TitleBox.Text = _metaDoc.Info.Title ?? "";
        AuthorBox.Text = _metaDoc.Info.Author ?? "";
        SubjectBox.Text = _metaDoc.Info.Subject ?? "";
        KeywordsBox.Text = _metaDoc.Info.Keywords ?? "";
        CreatorBox.Text = _metaDoc.Info.Creator ?? "";
        WriteXmpBox.IsEnabled = _xmp != null;

        if (_xmp == null)
        {
            XmpText.Text = "No XMP metadata in this file.";
        }
        else
        {
            XmpText.Text = "XMP  Title: " + XmpTool.Title(_xmp)
                + "\nAuthor: " + XmpTool.Author(_xmp)
                + "\nSubject: " + XmpTool.Subject(_xmp)
                + "\nKeywords: " + XmpTool.Keywords(_xmp)
                + "\nCreator: " + XmpTool.Creator(_xmp);
        }

        SetStatus("Loaded.");
    }

    void OnSaveMeta(object? sender, RoutedEventArgs e)
    {
        if (_metaDoc == null || _metaPath == null)
        {
            SetStatus("Load a PDF first.");
            return;
        }

        _metaDoc.Info.Title = TitleBox.Text ?? "";
        _metaDoc.Info.Author = AuthorBox.Text ?? "";
        _metaDoc.Info.Subject = SubjectBox.Text ?? "";
        _metaDoc.Info.Keywords = KeywordsBox.Text ?? "";
        _metaDoc.Info.Creator = CreatorBox.Text ?? "";

        if (_xmp != null && WriteXmpBox.IsChecked == true)
        {
            XmpTool.Apply(_xmp, _metaDoc.Info.Title, _metaDoc.Info.Author, _metaDoc.Info.Subject, _metaDoc.Info.Keywords, _metaDoc.Info.Creator);
            XmpTool.Write(_metaDoc, _xmp);
        }

        SetStatus("Saved: " + PdfFiles.SaveAs(_metaDoc, _metaPath, "MetaModified"));
    }

    async Task<string?> PickPdf()
    {
        var files = await StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = "Open PDF",
            AllowMultiple = false,
            FileTypeFilter = [new FilePickerFileType("PDF") { Patterns = ["*.pdf"] }]
        });

        if (files.Count == 0)
            return null;

        var path = files[0].TryGetLocalPath();
        if (path == null)
            SetStatus("That location isn't a normal file path.");
        return path;
    }

    void SetStatus(string? text)
    {
        StatusText.Text = text ?? "";
    }

    protected override void OnClosed(EventArgs e)
    {
        _metaDoc?.Dispose();
        base.OnClosed(e);
    }
}
