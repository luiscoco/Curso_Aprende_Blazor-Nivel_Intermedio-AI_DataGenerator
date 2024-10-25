# How to create a DataGenerator C# Console App for invoking OpenAI(ChatGPT) or Azure OpenAI with Microsoft.Extensions.AI

This sample is based on the github repo: https://github.com/dotnet/eShopSupport

We developed the **DataGenerator** application highlighted in the **eShopSupport** architecture:

![image](https://github.com/user-attachments/assets/2992d5a7-3c8c-4e95-bc99-d1d50fcf793c)

The **DataGenerator** request to **LLM** (Large Language Models), as **OpenAI gpt-4o** or **Azure OpenAI gpt-4o**, to generate, in an **output** folder, **JSON** files

This JSON files will contain a list of: **Categories**, **Products**, **Manuals Table of Contents** (ToCs), **Manuals** (with Markdown syntax), **Tickets** and **Evaluation Questions**

## 1. Summary

With this application you will learn how to invoke **OpenAI API (ChatGPT service)** or **Azure OpenAI** from a C# Console application

For invoking **Azure OpenAI** we use **Azure.AI.OpenAI** and for invoking **OpenAI API (ChatGPT service)** we use **Microsoft.Extensions.AI.OpenAI**

This sample is configured for **OpenAI API (ChatGPT service)**:

**appsettings.json**

```json
{
  "ConnectionStrings": {
    "chatcompletion": "Endpoint=https://api.openai.com/v1/chat/completion;Key=XXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXX;Deployment=gpt-4"
  }
}
```

**ChatCompletionServiceExtensions.cs**

```csharp
builder.Services.AddSingleton<OpenAIClient>(_ => new OpenAIClient(key));
```

We also can configure for using **Azure OpenAI**:

**appsettings.json**

```json
{
  "ConnectionStrings": {
    "chatcompletion": "Endpoint=https://<your-resource-name>.openai.azure.com/;Key=XXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXX;Deployment=<your-deployment-name>;ApiVersion=2023-05-15"
  }
}
```

**ChatCompletionServiceExtensions.cs**

```csharp
 builder.Services.AddSingleton<OpenAIClient>(_ => new AzureOpenAIClient(new Uri(endpoint), new ApiKeyCredential(key)));
```

For sending AI request we use the **Microsoft.Extensions.AI** library and function **CompleteAsync**. See the the file **GeneratorBase.cs**

We also can learn the **Embedding and Manual Search**: The class uses an **IEmbeddingGenerator (for text embedding)** to facilitate information retrieval from product manuals. This is used in the assistant's responses to help resolve customer queries

The **SearchUserManualAsync** function **searches product manuals** for relevant information using text embeddings and returns the most similar text snippets. The **SearchUserManualAsync** function is defined in the **TicketThreadGenerator** file

## 2. Create a C# application with Visual Studio 2022

We run Visual Studio 2022 and create a new project

![image](https://github.com/user-attachments/assets/6306dce7-fbd5-4b67-9cde-b61b2a6e6085)

We select the C# console project template and press the Next button

![image](https://github.com/user-attachments/assets/8d248eb8-a9cb-4b0b-b3c9-995eeeb4fede)

We input the project name and location and press the Next button

![image](https://github.com/user-attachments/assets/8db8d777-4195-42c7-9cbf-8d3c05fd9bed)

We select the .NET 8 or 9 framework and press the Create button

![image](https://github.com/user-attachments/assets/a195ddf6-8989-41f4-9b4d-af22679300bb)

## 3. Get an OpenAPI Key

Navigate to this URL and Generate an API Key: 

https://platform.openai.com/api-keys

![image](https://github.com/user-attachments/assets/7ce8e9e5-01c0-426b-b392-e1d0b631d7e5)

## 4. Create the project folders

![image](https://github.com/user-attachments/assets/a24e00ec-1f6d-4d39-85d1-01bc47346f1e)

## 5. Load the Nuget Packages

![image](https://github.com/user-attachments/assets/4adf3fdb-142b-4d03-9357-004faff9cee4)

## 6. Configure the OpenAI service in appsettings.json file

```json
{
  "ConnectionStrings": {
    "chatcompletion": "Endpoint=https://api.openai.com/v1/chat/completion;Key=XXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXX;Deployment=gpt-4o"
  }
}
```

## 7. Middleware code

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

## 8. Application flow explanation

As we can see in the **Program.cs** this application sequentially execute the following tasks:

### 8.1. We first invoke the **CategoryGenerator** class for generating product category names

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

### 8.2. We also invoke **ProductGenerator** for generating Products names

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

### 8.3. We create the Table of Contents for the Manuals 

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

### 8.4. We create the Markdown file for each Product Manual

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

### 8.5. We convert the Manual from Markdown to PDF format

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

### 8.6. We create the Enqueries Tickets

We invoke in the **middleware** the **TicketGenerator** for creating the Tickets content:

Please verify in the following code, we need to create previously the categories, products, and manuals before generating the tickets:

```csharp
var tickets = await new TicketGenerator(products, categories, manuals, services).GenerateAsync();
Console.WriteLine($"Got {tickets.Count} tickets");
```

We also invoke the OpenAI service with the following code giving also two main input variables **situations** and **styles**

**TicketGenerator.cs**

```csharp
  protected override async IAsyncEnumerable<Ticket> GenerateCoreAsync()
  {
      // If there are any tickets already, assume this covers everything we need
      if (Directory.GetFiles(OutputDirPath).Any())
      {
          yield break;
      }

      var numTickets = 1;
      var batchSize = 1;
      var ticketId = 0;

      string[] situations = [
          "asking about a particular usage scenario before purchase",
          "asking a specific technical question about the product's capabilities",
          "needs information on the product's suitability for its most obvious use case",
          "unable to make the product work in one particular way",
          "thinks the product doesn't work at all",
          "can't understand how to do something",
          "has broken the product",
          "needs reassurance that the product is behaving as expected",
          "wants to use the product for a wildly unexpected purpose, but without self-awareness and assumes it's reasonable",
          "incredibly fixated on one minor, obscure detail (before or after purchase), but without self-awareness that they are fixated on an obscure matter. Do not use the word 'fixated'.",
          "a business-to-business enquiry from another retailer who stocks the product and has their own customer enquiries to solve",
      ];

      string[] styles = [
          "polite",
          "extremely jovial, as if trying to be best friends",
          "formal",
          "embarassed and thinks they are the cause of their own problem",
          "not really interested in communicating clearly, only using a few words and assuming support can figure it out",
          "demanding and entitled",
          "frustrated and angry",
          "grumpy, and trying to claim there are logical flaws in whatever the support agent has said",
          "extremely brief and abbreviated, by a teenager typing on a phone while distracted by another task",
          "extremely technical, as if trying to prove the superiority of their own knowledge",
          "relies on extremely, obviously false assumptions, but is earnest and naive",
          "providing almost no information, so it's impossible to know what they want or why they are submitting the support message",
      ];

      while (ticketId < numTickets)
      {
          var numInBatch = Math.Min(batchSize, numTickets - ticketId);
          var ticketsInBatch = await Task.WhenAll(Enumerable.Range(0, numInBatch).Select(async _ =>
          {
              var product = products[Random.Shared.Next(products.Count)];
              var category = categories.Single(c => c.CategoryId == product.CategoryId);
              var situation = situations[Random.Shared.Next(situations.Length)];
              var style = styles[Random.Shared.Next(styles.Length)];
              var manual = manuals.Single(m => m.ProductId == product.ProductId);
              var manualExtract = ManualGenerator.ExtractFromManual(manual);

              var prompt = @$"You are creating test data for a customer support ticketing system.
                  Write a message by a customer who has purchased, or is considering purchasing, the following:

                  Product: {product.Model}
                  Brand: {product.Brand}
                  Category: {category.Name}
                  Description: {product.Description}
                  Random extract from manual: <extract>{manualExtract}</extract>

                  The situation is: {situation}
                  If applicable, they can ask for a refund/replacement/repair. However in most cases they
                  are asking for information or help with a problem.

                  The customer writes in the following style: {style}

                  Create a name for the author, writing the message as if you are that person. The customer name
                  should be fictional and random, and not based on the support enquiry itself. Do not use cliched
                  or stereotypical names.

                  Where possible, the message should refer to something specific about this product such as a feature
                  mentioned in its description or a fact mentioned in the manual (but the customer does not refer
                  to having read the manual).

                  The message length may be anything from very brief (around 10 words) to very long (around 200 words).
                  Use blank lines for paragraphs if needed.

                  The result should be JSON form {{ ""customerFullName"": ""string"", ""message"": ""string"" }}.";

              var ticket = await GetAndParseJsonChatCompletion<Ticket>(prompt);
              ticket.ProductId = product.ProductId;
              ticket.CustomerSituation = situation;
              ticket.CustomerStyle = style;
              return ticket;
          }));

          foreach (var t in ticketsInBatch)
          {
              t.TicketId = ++ticketId;
              yield return t;
          }
      }
  }
```

### 8.7. We create the Threads Tickets

We invoke in the **middleware** the **TicketThreadGenerator** for creating the Tickets content:

Please verify in the following code, we need to create previously the products, manuals and enqueries tickets before generating the thread tickets:

```csharp
var ticketThreads = await new TicketThreadGenerator(tickets, products, manuals, services).GenerateAsync();
Console.WriteLine($"Got {ticketThreads.Count} threads");
```

The **GenerateThreadAsync** method generates a **conversation (thread)** for each ticket, starting with an **initial customer message** and then alternating between customer and support agent messages

The **length of the conversation** is **randomized** using a geometric distribution, and the method determines whether the conversation should continue or if the ticket is resolved

**GenerateCustomerMessageAsync** and **GenerateAssistantMessageAsync** are responsible for generating the next message from the customer and the support agent (assistant), respectively

They use prompts to simulate realistic conversation flow

Each message is constructed based on the product, ticket, and previous conversation history. The customer’s message style is also taken into account.

This is the code for creating the **CustomerMessage** inside the thread tickets:

**TicketThreadGenerator.cs**

```csharp
    private async Task<Response> GenerateCustomerMessageAsync(Product product, Ticket ticket, IReadOnlyList<TicketThreadMessage> messages)
    {
        var prompt = $@"You are generating test data for a customer support ticketing system. There is an open ticket as follows:
        
        Product: {product.Model}
        Brand: {product.Brand}
        Customer name: {ticket.CustomerFullName}

        The message log so far is:

        {FormatMessagesForPrompt(messages)}

        Generate the next reply from the customer. You may do any of:

        - Supply more information as requested by the support agent
        - Say you did what the support agent suggested and whether or not it worked
        - Confirm that your enquiry is now resolved and you accept the resolution
        - Complain about the resolution
        - Say you need more information

        Write as if you are the customer. This customer ALWAYS writes in the following style: {ticket.CustomerStyle}.

        Respond in the following JSON format: {{ ""message"": ""string"", ""shouldClose"": bool }}.
        Indicate that the ticket should be closed if, as the customer, you feel the ticket is resolved (whether or not you are satisfied).
";

        return await GetAndParseJsonChatCompletion<Response>(prompt);
    }
```

Now we create the prompt for creating the **Assistant** message:

**TicketThreadGenerator.cs**

```csharp
private async Task<Response> GenerateAssistantMessageAsync(Product product, Ticket ticket, IReadOnlyList<TicketThreadMessage> messages, IReadOnlyList<Manual> manuals)
{
    var prompt = $@"You are a customer service agent working for AdventureWorks, an online retailer. You are responding to a customer
    enquiry about the following product:

    Product: {product.Model}
    Brand: {product.Brand}

    The message log so far is:

    {FormatMessagesForPrompt(messages)}

    Your job is to provide the next message to send to the customer, and ideally close the ticket. Your goal is to help resolve their enquiry, which might include:

    - Providing information or technical support
    - Recommending a return or repair, if compliant with policy below
    - Closing off-topic enquiries

    You must first decide if you have enough information, and if not, either ask the customer for more details or search for information
    in the product manual using the configured tool. Don't repeat information that was already given earlier in the message log.

    Our policy for returns/repairs is:
    - Returns are allowed within 30 days if the product is unused
    - Defective products may be returned within 1 year of purchase for a refund
    - There may be other warranty or repair options provided by the manufacturer, as detailed in the manual
    Returns may be initiated at https://northernmountains.example.com/support/returns

    You ONLY give information based on the product details and manual. If you cannot answer based on the provided context, say that you don't know.
    Whenever possible, give your answer as a quote from the manual, for example saying ""According to the manual, ..."".
    If needed, refer the customer to the manufacturer's support contact detail in the user manual, if any.

    You refer to yourself only as ""AdventureWorks Support"", or ""Support team"".

    Respond in the following JSON format: {{ ""message"": ""string"", ""shouldClose"": bool }}.
    Indicate that the ticket should be closed only if the customer has confirmed it is resolved.
    It's OK to give very short, 1-sentence replies if applicable.
    ";

    var manual = manuals.Single(m => m.ProductId == product.ProductId);
    var tools = new AssistantTools(embedder, manual);
    var searchManual = AIFunctionFactory.Create(tools.SearchUserManualAsync);

    return await GetAndParseJsonChatCompletion<Response>(prompt, tools: [searchManual]);
}
```

**Embedding and Manual Search**:

The class uses an **IEmbeddingGenerator (for text embedding)** to facilitate information retrieval from product manuals. This is used in the assistant's responses to help resolve customer queries

The **SearchUserManualAsync** function **searches product manuals** for relevant information using text embeddings and returns the most similar text snippets

```csharp
[Description("Searches for information in the product's user manual.")]
public async Task<string> SearchUserManualAsync([Description("text to look for in user manual")] string query)
{
    // Obviously it would be more performant to chunk and embed each manual only once, but this is simpler for now
    var chunks = SplitIntoChunks(manual.MarkdownText, 200).ToList();
    var embeddings = await embedder.GenerateAsync(chunks);
    var candidates = chunks.Zip(embeddings);
    var queryEmbedding = (await embedder.GenerateAsync(query)).Single();

    var closest = candidates
        .Select(c => new { Text = c.First, Similarity = TensorPrimitives.CosineSimilarity(c.Second.Vector.Span, queryEmbedding.Vector.Span) })
        .OrderByDescending(c => c.Similarity)
        .Take(3)
        .Where(c => c.Similarity > 0.6f)
        .ToList();

    if (closest.Any())
    {
        return string.Join(Environment.NewLine, closest.Select(c => $"<snippet_from_manual>{c.Text}</snippet_from_manual>"));
    }
    else
    {
        return "The manual contains no relevant information about this";
    }
}
```

### 8.8. **TicketSummaryGenerator**:

In the middleware, we have to send as parameters the products and ticketThreads to be summarized bythe **TicketSummaryGenerator** class

```
var summarizedThreads = await new TicketSummaryGenerator(products, ticketThreads, services).GenerateAsync();
Console.WriteLine($"Got {summarizedThreads.Count} thread summaries");
```

We also summarize each ticket with the following prompt:

**TicketSummaryGenerator.cs**

```csharp
 private async Task GenerateSummaryAsync(TicketThread thread)
 {
     // The reason for prompting to express satisfation in words rather than numerically, and forcing it to generate a summary
     // of the customer's words before doing so, are necessary prompt engineering techniques. If it's asked to generate sentiment
     // score without first summarizing the customer's words, then it scores the agent's response even when told not to. If it's
     // asked to score numerically, it produces wildly random scores - it's much better with words than numbers.
     string[] satisfactionScores = ["AbsolutelyFurious", "VeryUnhappy", "Unhappy", "Disappointed", "Indifferent", "Pleased", "Happy", "Delighted", "UnspeakablyThrilled"];

     var product = products.Single(p => p.ProductId == thread.ProductId);
     var prompt = $@"You are part of a customer support ticketing system.
         Your job is to write brief summaries of customer support interactions. This is to help support agents
         understand the context quickly so they can help the customer efficiently.

         Here are details of a support ticket.

         Product: {product.Model}
         Brand: {product.Brand}
         Customer name: {thread.CustomerFullName}

         The message log so far is:

         {TicketThreadGenerator.FormatMessagesForPrompt(thread.Messages)}

         Write these summaries:

         1. A longer summary that is up to 30 words long, condensing as much distinctive information
            as possible. Do NOT repeat the customer or product name, since this is known anyway.
            Try to include what SPECIFIC questions/info were given, not just stating in general that questions/info were given.
            Always cite specifics of the questions or answers. For example, if there is pending question, summarize it in a few words.
            FOCUS ON THE CURRENT STATUS AND WHAT KIND OF RESPONSE (IF ANY) WOULD BE MOST USEFUL FROM THE NEXT SUPPORT AGENT.

         2. A shorter summary that is up to 8 words long. This functions as a title for the ticket,
            so the goal is to distinguish what's unique about this ticket.

         3. A 10-word summary of the latest thing the CUSTOMER has said, ignoring any agent messages. Then, based
            ONLY on that, score the customer's satisfaction using one of the following phrases ranked from worst to best:
            {string.Join(", ", satisfactionScores)}.
            Pay particular attention to the TONE of the customer's messages, as we are most interested in their emotional state.

         Both summaries will only be seen by customer support agents.

         Respond as JSON in the following form: {{
           ""longSummary"": ""string"",
           ""shortSummary"": ""string"",
           ""tenWordsSummarizingOnlyWhatCustomerSaid"": ""string"",
           ""customerSatisfaction"": ""string"",
           ""ticketStatus"": ""Open""|""Closed"",
           ""ticketType"": ""Question""|""Idea""|""Complaint""|""Returns""
         }}

         ticketStatus should be Open if there is some remaining work for support agents to handle, otherwise Closed.
         ticketType must be one of the specified values best matching the ticket. Do not use any other value except the specified ones.";

     var response = await GetAndParseJsonChatCompletion<Response>(prompt);
     thread.ShortSummary = response.ShortSummary;
     thread.LongSummary = response.LongSummary;
     thread.CustomerSatisfaction = null;
     thread.TicketStatus = response.TicketStatus;
     thread.TicketType = response.TicketType;

     var satisfactionScore = Array.IndexOf(satisfactionScores, response.CustomerSatisfaction ?? string.Empty);
     if (satisfactionScore > 0)
     {
         var satisfactionPercent = (int)(10 * ((double)satisfactionScore / (satisfactionScores.Length - 1)));
         thread.CustomerSatisfaction = satisfactionPercent;
     }
 }
```

### 8.9. We create the Evaluation Questions

Finally, for generating, in the **middleware**, the questions we have to send as parameters the categories, products and manuals:

```csharp
var evalQuestions = await new EvalQuestionGenerator(products, categories, manuals, services).GenerateAsync();
Console.WriteLine($"Got {evalQuestions.Count} evaluation questions");
```

For this purpose we define the **EvalQuestionGenerator** class and send this prompt to the OpenAI API:

```csharp
private async Task<EvalQuestion> GenerateSingle()
{
    var product = products[Random.Shared.Next(products.Count)];
    var category = categories.Single(c => c.CategoryId == product.CategoryId);
    var manual = manuals.Single(m => m.ProductId == product.ProductId);
    var manualExtract = ManualGenerator.ExtractFromManual(manual);
    var isQuestionWrittenByAgent = Random.Shared.NextDouble() < 0.75;
    var questionPrompt = isQuestionWrittenByAgent
        ? """
                Questions are short, typically 3-6 words, and are not always full sentences. They may look
                like search queries or things typed in a hurry by a support agent. They are not polite or
                verbose, since they are addressed to a machine.
                Example questions might be "weight", "what are the dimensions", "how to shut down",
                "can use on pets?", "what accessories does it come with?"
                """
        : """
                The question is actually an entire email written by a customer. It usually starts with a
                greeting, some description of their situation, and then their question. The whole thing
                may be up to 100 words long. It may contain spelling and grammar errors, and may be angry
                or rude.
                """;

    var question = await GetAndParseJsonChatCompletion<EvalQuestion>($$"""
                There is an AI system used by customer support agents working for an online retailer.
                The AI system is used to help agents answer customer questions.

                Your task is to write question/answer pairs that will be used to evaluate the
                performance of that AI system. All the questions you write will be about actual products
                sold by that retailer, based on information from the product catalog and manuals. The
                questions should plausibly represent what customers and support agents will ask.

                In this case, you are to write a question/answer pair based on the following context:

                <product_name>{{product.Model}}</product_name>
                <brand>{{product.Brand}}</brand>
                <category>{{category.Name}}</category>
                <extract_from_manual>{{manualExtract}}</extract_from_manual>

                Questions are one of the following types:
                 - A pre-purchase question to help a customer who wants to know about the product
                   features, suitability for particular use cases, or other objective facts
                 - A post-purchase question to help a customer resolve an issue or understand how to
                   use the product

                You must select an OBJECTIVE FACT from the product manual and write a question to which
                that fact is the answer. Only select facts that are distinctive about this specific product,
                not generic information about any product or warranty terms.

                Always follow these style guidelines:
                 - {{questionPrompt}}
                 - Answers are short, typically a single brief sentence of 1-10 words. Never use more than
                   20 words for an answer.
                 - The "verbatim_quote_from_manual" is 3-6 words taken EXACTLY from the manual which are
                   the factual basis for the question and asnwer.
                 - If the provided context does not contain a suitable fact, set all the response properties
                   to null or empty strings.

                Respond as JSON in the following form: {
                    "question": "string",
                    "answer": "string",
                    "verbatimQuoteFromManual": "string"
                }
                """);
    question.ProductId = product.ProductId;
    return question;
}
```

## 9. We run the application and see the outputs

Before running the application for the first test we are going to **Set to One** the number of: **Categories**, **Products**, **Manuals**, **Tickets** and **Evaluation Questions**

See in this code we assing **1** value to the variable **numCategories**

**CategoryGenerator.cs**

```csharp
 protected override async IAsyncEnumerable<Category> GenerateCoreAsync()
 {
     // If there are any categories already, assume this covers everything we need
     if (Directory.GetFiles(OutputDirPath).Any())
     {
         yield break;
     }

     var numCategories = 1;
     var batchSize = 1;
     var categoryNames = new HashSet<string>();
```

See in this code we assing **1** value to the variable **numProducts**

**ProductGenerator.cs**

```csharp
  protected override async IAsyncEnumerable<Product> GenerateCoreAsync()
  {
      // If there are any products already, assume this covers everything we need
      if (Directory.GetFiles(OutputDirPath).Any())
      {
          yield break;
      }

      var numProducts = 1;
      var batchSize = 1;
      var productId = 0;

```


After running the application we confirm the **output** folder was created

And also inside the  **output** folder other folder were also created: **products**, **categories**, **manuals**, **tickets** and **evalquestions**

![image](https://github.com/user-attachments/assets/76f157d2-35ea-4339-a3cc-782369093322)

We can see inside each folder the JSON files created for the **products**, **categories**, **tickets** and **evalquestions**

And also inside the **manuals** folder we find the JSON files for the TOCs, and the Markdown and PDF for the Manuals

