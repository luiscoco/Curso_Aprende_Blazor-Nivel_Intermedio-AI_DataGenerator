using eShopSupport.DataGenerator.Model;
using Markdown2Pdf;
using Markdown2Pdf.Options;
using System.Text.RegularExpressions;

namespace eShopSupport.DataGenerator.Generators;

public class ManualPdfConverter(IReadOnlyList<Product> products, IReadOnlyList<Manual> manuals)
{
    Markdown2PdfConverter CreateConverter(Product product) => new(new()
    {
        DocumentTitle = product.Model,
        MarginOptions = new()
        {
            Top = "80px",
            Bottom = "80px",
            Left = "75px",
            Right = "75px",
        },
        TableOfContents = new()
        {
            ListStyle = ListStyle.Decimal, // Ensures numbered lists in TOC
            MinDepthLevel = 1,
            MaxDepthLevel = 4,
            PageNumberOptions = new()
            {
                TabLeader = Leader.Dots,
            },
        },
        CustomHeadContent = @"<style>
            h1 { padding-top: 100px; font-size: 2.5rem !important; page-break-after: always; border-bottom: none !important; }
            h2 { page-break-before: always; }
        </style>",
        HeaderHtml = "",
        FooterHtml = $"<div style=\"width: 100%; padding: 30px 75px; display: flex; justify-content: space-between;\"><span style=\"color: gray\">(c) {product.Brand}</span><span class=\"pageNumber\"></span></div>",
    });

    public async Task<IReadOnlyList<ManualPdf>> ConvertAsync()
    {
        var results = new List<ManualPdf>();

        foreach (var manual in manuals)
        {
            var outputDir = Path.Combine(GeneratorBase<object>.OutputDirRoot, "manuals", "pdf");
            var outputPath = Path.Combine(outputDir, $"{manual.ProductId}.pdf");
            results.Add(new ManualPdf { ProductId = manual.ProductId, LocalPath = outputPath });

            if (File.Exists(outputPath))
            {
                continue;
            }

            Directory.CreateDirectory(outputDir);

            // Insert TOC marker after first level-1 heading
            var firstMatch = true;
            var markdown = Regex.Replace(manual.MarkdownText, "^(# .*\r?\n)", match =>
            {
                if (firstMatch)
                {
                    firstMatch = false;
                    return match.Value + "\n[TOC]\n\n";
                }
                else
                {
                    return match.Value;
                }
            }, RegexOptions.Multiline);

            using var inputFile = new TempFile(markdown);

            var product = products.Single(p => p.ProductId == manual.ProductId);
            var converter = CreateConverter(product);

            try
            {
                // Attempt conversion and capture errors if they occur
                await converter.Convert(inputFile.FilePath, outputPath);
                Console.WriteLine($"Successfully wrote {Path.GetFileName(outputPath)}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error converting file {manual.ProductId}: {ex.Message}");
            }
        }

        return results;
    }

    private class TempFile : IDisposable
    {
        public string FilePath { get; }

        public TempFile(string contents)
        {
            FilePath = Path.GetTempFileName();
            File.WriteAllText(FilePath, contents);
        }

        public void Dispose()
            => File.Delete(FilePath);
    }
}
