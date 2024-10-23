using eShopSupport.DataGenerator.Model;

namespace eShopSupport.DataGenerator.Generators;

public class ManualTocGenerator : GeneratorBase<ManualToc>
{
    private readonly IReadOnlyList<Category> categories;
    private readonly IReadOnlyList<Product> products;

    public ManualTocGenerator(IReadOnlyList<Category> categories, IReadOnlyList<Product> products, IServiceProvider services)
        : base(services)
    {
        this.categories = categories;
        this.products = products;
    }

    protected override string DirectoryName => $"manuals{Path.DirectorySeparatorChar}toc";

    protected override object GetId(ManualToc item) => item.ProductId;

    protected override async IAsyncEnumerable<ManualToc> GenerateCoreAsync()
    {
        await foreach (var item in MapParallel(
            products.Where(p => !File.Exists(GetItemOutputPath(p.ProductId.ToString()))),
            GenerateTocForProductAsync))
        {
            yield return item;
        }
    }

    private async Task<ManualToc> GenerateTocForProductAsync(Product product)
    {
        var styles = new[] {
            "normal",
            "friendly",
            "trying to be cool and hip, with lots of emojis",
            "extremely formal and embarrassingly over-polite",
            "extremely technical, with many references to industrial specifications. Require the user to perform complex diagnostics using specialized industrial and scientific equipment before and after use.",
            "extremely badly translated from another language - most sentences are in broken English, grammatically incorrect, and misspelled",
            "confusing and often off-topic, with spelling mistakes",
            "incredibly negative and risk-averse, implying it would be unreasonable to use the product for any use case at all.",
        };
        var chosenStyle = styles[Random.Shared.Next(styles.Length)];
        var category = categories.SingleOrDefault(c => c.CategoryId == product.CategoryId);
        if (category == null)
        {
            throw new InvalidOperationException($"Category not found for product {product.Model}");
        }

        var prompt = @$"Write a suggested table of contents for the user manual for the following product:

            Category: {category.Name}
            Brand: {product.Brand}
            Product name: {product.Model}
            Overview: {product.Description}

            The manual MUST be written in the following style: {chosenStyle}
            The table of contents MUST follow that style, even if it makes the manual useless to users.
            
            The response should be a JSON object of the form
            {{
                ""sections"": [
                    {{
                        ""title"": ""..."",
                        ""subsections"": [
                            {{
                                ""title"": ""..."",
                                ""subsections"": [...]
                            }},
                            ...
                        ]
                    }},
                    ...
                ]
            }}

            Subsections can be nested up to 3 levels deep. Most sections have no subsections.";

        var toc = await GetAndParseJsonChatCompletion<ManualToc>(prompt, maxTokens: 4000);
        toc.ManualStyle = chosenStyle;
        toc.ProductId = product.ProductId;
        PopulateSiblingIndexes(toc.Sections);
        return toc;
    }

    void PopulateSiblingIndexes(List<ManualTocSection> sections)
    {
        for (var index = 0; index < sections.Count; index++)
        {
            var section = sections[index];
            section.SiblingIndex = index + 1;
            if (section.Subsections?.Count > 0)
            {
                PopulateSiblingIndexes(section.Subsections);
            }
        }
    }
}
