using eShopSupport.DataGenerator.Model;
using System.Text;
using System.Text.RegularExpressions;

namespace eShopSupport.DataGenerator.Generators;

public class ManualGenerator(IReadOnlyList<Category> categories, IReadOnlyList<Product> products, IReadOnlyList<ManualToc> manualTocs, IServiceProvider services)
    : GeneratorBase<Manual>(services)
{
    protected override string DirectoryName => $"manuals{Path.DirectorySeparatorChar}full";

    protected override object GetId(Manual item) => item.ProductId;

    protected override IAsyncEnumerable<Manual> GenerateCoreAsync()
    {
        // Skip the ones we already have
        var manualsToGenerate = manualTocs.Where(toc => !File.Exists(GetItemOutputPath(toc.ProductId.ToString())));
        return MapParallel(manualsToGenerate, GenerateManualAsync);
    }

    private async Task<Manual> GenerateManualAsync(ManualToc toc)
    {
        var product = products.Single(p => p.ProductId == toc.ProductId);
        var category = categories.Single(c => c.CategoryId == product.CategoryId);

        var result = new StringBuilder();
        result.AppendLine($"# {product.Model}");
        result.AppendLine();

        var desiredSubsectionWordLength = 500;
        foreach (var section in toc.Sections)
        {
            Console.WriteLine($"[Product {product.ProductId}] Generating manual section {section.SiblingIndex}: {section.Title}");

            var prompt = $@"Write a section of the user manual for the following product:
            Category: {category.Name}
            Brand: {product.Brand}
            Product name: {product.Model}
            Product overview: {product.Description}

            Manual style description: {toc.ManualStyle} (note: the text MUST follow this style, even if it makes the manual less helpful to reader)

            The section you are writing is ""{section.SiblingIndex}.{section.Title}"". It has the following structure:

            {FormatTocForPrompt(section)}

            Use valid Markdown formatting for PDF conversion, including:
            - Headings (#, ##, ###)
            - Simple lists (-, *, 1.)
            - Paragraphs of text
            - Bold (**bold**) and italics (*italics*)
            - Simple tables:
            | Feature | Description |
            | ------- | ----------- |
            - Images using the syntax: ![Description](path_to_image.jpg)

            Start your response with the section title formatted as ""## {section.SiblingIndex}. {section.Title}"". The output should be around {desiredSubsectionWordLength * CountSubtreeLength(section)} words in total, or {desiredSubsectionWordLength} words per subsection. Avoid any commentary or remarks about the task.
            ";

            var response = await GetCompletion(prompt);
            var sanitizedResponse = SanitizeMarkdown(response);
            result.AppendLine(sanitizedResponse);
            result.AppendLine();
        }

        return new Manual
        {
            ProductId = product.ProductId,
            MarkdownText = result.ToString()
        };
    }

    // This function sanitizes Markdown by removing unsupported or problematic elements
    private static string SanitizeMarkdown(string markdown)
    {
        // Remove code blocks, diagrams, and other complex markdown elements that might break conversion
        markdown = Regex.Replace(markdown, @"```.*?```", "", RegexOptions.Singleline);  // Remove code blocks
        markdown = Regex.Replace(markdown, @"mermaid.*?```", "", RegexOptions.Singleline);  // Remove Mermaid diagrams
        markdown = Regex.Replace(markdown, @"\!\[.*?\]\(.*?\)", "");  // Optionally remove images if they cause issues
        // You can add more sanitization steps if necessary
        return markdown;
    }

    private static string FormatTocForPrompt(ManualTocSection section)
    {
        var sb = new StringBuilder();
        AppendSection(sb, section);
        return sb.ToString();

        static void AppendSection(StringBuilder sb, ManualTocSection section, string ancestorSectionPrefix = "")
        {
            var fullSectionNumber = string.IsNullOrEmpty(ancestorSectionPrefix)
                ? section.SiblingIndex.ToString()
                : $"{ancestorSectionPrefix}.{section.SiblingIndex}";
            sb.AppendLine($"{fullSectionNumber}. {section.Title}");
            if (section.Subsections?.Any() == true)
            {
                foreach (var s in section.Subsections)
                {
                    AppendSection(sb, s, fullSectionNumber);
                }
            }
        }
    }

    private static int CountSubtreeLength(ManualTocSection tocSection)
    {
        return 1 + tocSection.Subsections?.Sum(CountSubtreeLength) ?? 0;
    }

    protected override string FilenameExtension => ".md";  // Ensure that files are saved as .md (Markdown)

    protected override Task WriteAsync(string path, Manual item)
    {
        return File.WriteAllTextAsync(path, item.MarkdownText);
    }

    protected override Manual Read(string path)
        => new Manual
        {
            ProductId = int.Parse(Path.GetFileNameWithoutExtension(path)),
            MarkdownText = File.ReadAllText(path)
        };

    public static string ExtractFromManual(Manual manual)
    {
        // We don't want to push the entire manual text into the prompt as it may be arbitrarily long
        // Instead, pick a lengthy chunk at random.
        var approxExtractLengthInChars = 1500;
        var startChar = Random.Shared.Next(manual.MarkdownText.Length - approxExtractLengthInChars);

        // Find the line containing this char
        var lines = manual.MarkdownText.Split('\n', StringSplitOptions.RemoveEmptyEntries);
        var lineIndexContainingStartChar = 0;
        for (var numCharsSeen = 0; numCharsSeen < startChar; lineIndexContainingStartChar++)
        {
            numCharsSeen += lines[lineIndexContainingStartChar].Length;
        }

        // Add lines until we have enough text
        var extract = new StringBuilder();
        for (var i = lineIndexContainingStartChar; i < lines.Length; i++)
        {
            extract.AppendLine(lines[i]);
            if (extract.Length >= approxExtractLengthInChars)
            {
                break;
            }
        }

        return extract.ToString();
    }
}
