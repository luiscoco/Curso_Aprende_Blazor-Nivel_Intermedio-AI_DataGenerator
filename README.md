# How to create a DataGenerator with a C# Console application invoking OpenAI service

This sample is based on the github repo: https://github.com/dotnet/eShopSupport

We have analysed and developed the **DataGenerator** application, see the context in the **eShopSupport** general architecture picture: 

![image](https://github.com/user-attachments/assets/2992d5a7-3c8c-4e95-bc99-d1d50fcf793c)

## 1. Create a C# application with Visual Studio 2022

We run Visual Studio 2022 and create a new project

![image](https://github.com/user-attachments/assets/6306dce7-fbd5-4b67-9cde-b61b2a6e6085)

We select the C# console project template and press the Next button

![image](https://github.com/user-attachments/assets/8d248eb8-a9cb-4b0b-b3c9-995eeeb4fede)

We input the project name and location and press the Next button

![image](https://github.com/user-attachments/assets/8db8d777-4195-42c7-9cbf-8d3c05fd9bed)

We select the .NET 8 or 9 framework and press the Create button

![image](https://github.com/user-attachments/assets/a195ddf6-8989-41f4-9b4d-af22679300bb)

## 2. Get an OpenAPI Key

Navigate to this URL and Generate an API Key: 

https://platform.openai.com/api-keys

![image](https://github.com/user-attachments/assets/7ce8e9e5-01c0-426b-b392-e1d0b631d7e5)

## 3. Create the project folders

![image](https://github.com/user-attachments/assets/a24e00ec-1f6d-4d39-85d1-01bc47346f1e)

## 4. Load the Nuget Packages

![image](https://github.com/user-attachments/assets/4adf3fdb-142b-4d03-9357-004faff9cee4)

## 5. Configure the OpenAI service in appsettings.json file

```json
{
  "ConnectionStrings": {
    "chatcompletion": "Endpoint=https://api.openai.com/v1/chat/completion;Key=XXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXX;Deployment=gpt-4o"
  }
}
```

## 6. Middleware code

We first read the configuration files:

```csharp
builder.Configuration.AddJsonFile("appsettings.json");
builder.Configuration.AddJsonFile("appsettings.Local.json", optional: true);
```

We also register the application services:

```csharp
builder.AddOpenAIChatCompletion("chatcompletion");
builder.Services.AddSingleton<IEmbeddingGenerator<string, Embedding<float>>, LocalTextEmbeddingGenerator>();
```

This is the Program.cs whole code:

**Program.cs**

```csharp
using eShopSupport.DataGenerator;
using eShopSupport.DataGenerator.Generators;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

var builder = Host.CreateApplicationBuilder(args);
builder.Configuration.AddJsonFile("appsettings.json");
builder.Configuration.AddJsonFile("appsettings.Local.json", optional: true);
builder.AddOpenAIChatCompletion("chatcompletion");
builder.Services.AddSingleton<IEmbeddingGenerator<string, Embedding<float>>, LocalTextEmbeddingGenerator>();

var services = builder.Build().Services;

var categories = await new CategoryGenerator(services).GenerateAsync();
Console.WriteLine($"Got {categories.Count} categories");

var products = await new ProductGenerator(categories, services).GenerateAsync();
Console.WriteLine($"Got {products.Count} products");

var manualTocs = await new ManualTocGenerator(categories, products, services).GenerateAsync();
Console.WriteLine($"Got {manualTocs.Count} manual TOCs");

var manuals = await new ManualGenerator(categories, products, manualTocs, services).GenerateAsync();
Console.WriteLine($"Got {manuals.Count} manuals");

var manualPdfs = await new ManualPdfConverter(products, manuals).ConvertAsync();
Console.WriteLine($"Got {manualPdfs.Count} PDFs");

var tickets = await new TicketGenerator(products, categories, manuals, services).GenerateAsync();
Console.WriteLine($"Got {tickets.Count} tickets");

var ticketThreads = await new TicketThreadGenerator(tickets, products, manuals, services).GenerateAsync();
Console.WriteLine($"Got {ticketThreads.Count} threads");

var summarizedThreads = await new TicketSummaryGenerator(products, ticketThreads, services).GenerateAsync();
Console.WriteLine($"Got {summarizedThreads.Count} thread summaries");

var evalQuestions = await new EvalQuestionGenerator(products, categories, manuals, services).GenerateAsync();
Console.WriteLine($"Got {evalQuestions.Count} evaluation questions");
```

We also created an Extension file to define the OpenAI service:

**ChatCompletionServiceExtensions.cs**

```csharp
using Microsoft.Extensions.DependencyInjection;
using System.Data.Common;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.AI;
using Azure.AI.OpenAI;
using System.ClientModel;
using OpenAI;

namespace eShopSupport.DataGenerator;

public static class ChatCompletionServiceExtensions
{
    public static void AddOpenAIChatCompletion(this HostApplicationBuilder builder, string connectionStringName)
    {
        var connectionStringBuilder = new DbConnectionStringBuilder();
        var connectionString = builder.Configuration.GetConnectionString(connectionStringName);
        if (string.IsNullOrEmpty(connectionString))
        {
            throw new InvalidOperationException($"Missing connection string {connectionStringName}");
        }

        connectionStringBuilder.ConnectionString = connectionString;

        var deployment = connectionStringBuilder.TryGetValue("Deployment", out var deploymentValue) ? (string)deploymentValue : throw new InvalidOperationException($"Connection string {connectionStringName} is missing 'Deployment'");
        var endpoint = connectionStringBuilder.TryGetValue("Endpoint", out var endpointValue) ? (string)endpointValue : throw new InvalidOperationException($"Connection string {connectionStringName} is missing 'Endpoint'");
        var key = connectionStringBuilder.TryGetValue("Key", out var keyValue) ? (string)keyValue : throw new InvalidOperationException($"Connection string {connectionStringName} is missing 'Key'");

        builder.Services.AddSingleton<OpenAIClient>(_ => new OpenAIClient(key));

        builder.Services.AddChatClient(builder => builder
            .UseFunctionInvocation()
            .Use(builder.Services.GetRequiredService<OpenAIClient>().AsChatClient(deployment))); // TODO: Use simpler extension method
    }
}
```

## 7. Application flow explanation

As we can see in the **Program.cs** this application sequentially execute the following tasks:

### 7.1. We first invoke the **CategoryGenerator** class for generating product category names

In the **middleware** we invoke the **Categories** geneation function:

**Program.cs**

```csharp
var services = builder.Build().Services;

var categories = await new CategoryGenerator(services).GenerateAsync();
Console.WriteLine($"Got {categories.Count} categories");
```

Then the **Categories** are provided by the OpenAI service in a **JSON** format:

**CategoryGenerator.cs**

```csharp
 var numCategories = 1;
var batchSize = 1;
var categoryNames = new HashSet<string>();

while (categoryNames.Count < numCategories)
{
    Console.WriteLine($"Generating {batchSize} categories...");

    var prompt = @$"Generate {batchSize} product category names for an online retailer
    of high-tech outdoor adventure goods and related clothing/electronics/etc.
    Each category name is a single descriptive term, so it does not use the word 'and'.
    Category names should be interesting and novel, e.g., ""Mountain Unicycles"", ""AI Boots"",
    or ""High-volume Water Filtration Plants"", not simply ""Tents"".
    This retailer sells relatively technical products.

    Each category has a list of up to 8 brand names that make products in that category. All brand names are
    purely fictional. Brand names are usually multiple words with spaces and/or special characters, e.g.
    ""Orange Gear"", ""Aqua Tech US"", ""Livewell"", ""E & K"", ""JAXⓇ"".
    Many brand names are used in multiple categories. Some categories have only 2 brands.
    
    The response should be a JSON object of the form {{ ""categories"": [{{""name"":""Tents"", ""brands"":[""Rosewood"", ""Summit Kings""]}}, ...] }}.";

    var response = await GetAndParseJsonChatCompletion<Response>(prompt, maxTokens: 70 * batchSize);
    foreach (var c in response.Categories)
    {
        if (categoryNames.Add(c.Name))
        {
            c.CategoryId = categoryNames.Count;
            c.Brands = c.Brands.Select(ImproveBrandName).ToArray();
            yield return c;
        }
    }
}
```

This is an example of running the prompt in ChatGPT-4o:

![image](https://github.com/user-attachments/assets/49eb6a89-10ab-45f2-8084-827287210261)

![image](https://github.com/user-attachments/assets/e676f808-56fa-49c0-ba27-24a5f9bb4c6a)

We write the output in a JSON file in the **output** directory:

**...DataGenerator\output\categories**

![image](https://github.com/user-attachments/assets/a3d1ad7c-2ee9-4af8-af54-307d9938b7c3)

### 7.2. We also invoke **ProductGenerator** for generating Products names

In the **middleware** we invoke the **Products** geneation function:

**Program.cs**

```csharp
var services = builder.Build().Services;

var categories = await new CategoryGenerator(services).GenerateAsync();
Console.WriteLine($"Got {categories.Count} categories");

var products = await new ProductGenerator(categories, services).GenerateAsync();
Console.WriteLine($"Got {products.Count} products");
```

The **Products** are provided by the OpenAI service in a **JSON** format:

**ProductGenerator.cs**

We also **create the Products names** invoking the OpenAI service with the following code, similar as in section 7.1:

```csharp
 var numProducts = 1;
 var batchSize = 1;
 var productId = 0;

 var mappedBatches = MapParallel(Enumerable.Range(0, numProducts / batchSize), async batchIndex =>
 {
     var chosenCategories = Enumerable.Range(0, batchSize)
         .Select(_ => categories[(int)Math.Floor(categories.Count * Random.Shared.NextDouble())])
         .ToList();

     var prompt = @$"Write list of {batchSize} products for an online retailer
     of outdoor adventure goods and related electronics, clothing, and homeware. There is a focus on high-tech products. They match the following category/brand pairs:
     {string.Join(Environment.NewLine, chosenCategories.Select((c, index) => $"- product {(index + 1)}: category {c.Name}, brand: {c.Brands[Random.Shared.Next(c.Brands.Length)]}"))}

     Model names are up to 50 characters long, but usually shorter. Sometimes they include numbers, specs, or product codes.
     Example model names: ""iGPS 220c 64GB"", ""Nomad Camping Stove"", ""UX Polarized Sunglasses (Womens)"", ""40L Backpack, Green""
     Do not repeat the brand name in the model name.

     The description is up to 200 characters long and is the marketing text that will appear on the product page.
     Include the key features and selling points.

     The result should be JSON form {{ ""products"": [{{ ""id"": 1, ""brand"": ""string"", ""model"": ""string"", ""description"": ""string"", ""price"": 123.45 }}] }}.";

     var response = await GetAndParseJsonChatCompletion<Response>(prompt, maxTokens: 200 * batchSize);
     var batchEntryIndex = 0;
     foreach (var p in response.Products!)
     {
         var category = chosenCategories[batchEntryIndex++];
         p.CategoryId = category.CategoryId;
     }

     return response.Products;
 });

await foreach (var batch in mappedBatches)
{
    foreach (var p in batch)
    {
        p.ProductId = ++productId;
        yield return p;
    }
}
```

### 7.3. We create the Table of Contents for the Manuals 

In the **middleware** after creating the **Categories** and **Products**, we generate the **Toc Manual** for each product

A **TOC Manual** refers to a **Table of Contents Manual**, which is essentially a document that provides an organized listing of sections, chapters, and topics covered in a larger document or manual

It helps users quickly locate and access specific information within the manual

This is especially useful for complex documents such as technical manuals, instruction guides, or policy handbooks

**Program.cs**

```csharp
var services = builder.Build().Services;

var categories = await new CategoryGenerator(services).GenerateAsync();
Console.WriteLine($"Got {categories.Count} categories");

var products = await new ProductGenerator(categories, services).GenerateAsync();
Console.WriteLine($"Got {products.Count} products");

var manualTocs = await new ManualTocGenerator(categories, products, services).GenerateAsync();
Console.WriteLine($"Got {manualTocs.Count} manual TOCs");
```

We also generate the TOC invoking the OpenAI service with the **ManualTocGenerator** class

```csharp
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
        Please not include special characters like: \u0027, \u0026, etc
        
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
```

### 7.4. We create the Markdown file for each Product Manual

In the **middleware** after creating the Categories, Products and ToC we create the Manuals in Markdown format

```csharp
var services = builder.Build().Services;

var categories = await new CategoryGenerator(services).GenerateAsync();
Console.WriteLine($"Got {categories.Count} categories");

var products = await new ProductGenerator(categories, services).GenerateAsync();
Console.WriteLine($"Got {products.Count} products");

var manualTocs = await new ManualTocGenerator(categories, products, services).GenerateAsync();
Console.WriteLine($"Got {manualTocs.Count} manual TOCs");

var manuals = await new ManualGenerator(categories, products, manualTocs, services).GenerateAsync();
Console.WriteLine($"Got {manuals.Count} manuals");
```

This is code for generating the Manual:

```csharp
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
```

### 7.5. We convert the Manual from Markdown to PDF format

After creating the Categories, Products, ToCs and Manuals (in Markdown format), we proceed to convert the Manuals to PDF

```csharp
var services = builder.Build().Services;

var categories = await new CategoryGenerator(services).GenerateAsync();
Console.WriteLine($"Got {categories.Count} categories");

var products = await new ProductGenerator(categories, services).GenerateAsync();
Console.WriteLine($"Got {products.Count} products");

var manualTocs = await new ManualTocGenerator(categories, products, services).GenerateAsync();
Console.WriteLine($"Got {manualTocs.Count} manual TOCs");

var manuals = await new ManualGenerator(categories, products, manualTocs, services).GenerateAsync();
Console.WriteLine($"Got {manuals.Count} manuals");

var manualPdfs = await new ManualPdfConverter(products, manuals).ConvertAsync();
Console.WriteLine($"Got {manualPdfs.Count} PDFs");
```

For converting to PDF from Mardown we define the **ManualPdfConverter** class

We invoke the **Markdown2Pdf(version 2.2.1)** Nuget Package:

```csharp
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
```

### 7.6. **TicketGenerator**:

### 7.7.**TicketThreadGenerator**:

### 7.8. **TicketSummaryGenerator**:

### 7.9. **EvalQuestionGenerator**:

## 8. We run the application and see the outputs

After running the application we confirm the **output** folder was created

And also inside the  **output** folder other folder were also created: **products**, **categories**, **manuals**, **tickets** and **evalquestions**

![image](https://github.com/user-attachments/assets/76f157d2-35ea-4339-a3cc-782369093322)

We can see inside each folder the JSON files created for the **products**, **categories**, **tickets** and **evalquestions**

And also inside the **manuals** folder we find the JSON files for the TOCs, and the Markdown and PDF for the Manuals

