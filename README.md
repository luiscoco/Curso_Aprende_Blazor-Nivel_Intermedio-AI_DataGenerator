# How to create a DataGenerator with a C# Console application invoking OpenAI service

This sample is based on the github repo: https://github.com/dotnet/eShopSupport

## 1. Create a C# application with Visual Studio 2022

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
