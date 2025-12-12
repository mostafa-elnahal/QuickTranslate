# GTranslate API Documentation

A comprehensive guide to the **GTranslate** NuGet package (v2.3.1) – a collection of free translation APIs for .NET applications.

---

## Table of Contents

1. [Overview](#overview)
2. [Installation](#installation)
3. [Available Translators](#available-translators)
4. [Core Concepts](#core-concepts)
5. [API Reference](#api-reference)
   - [Translation](#translation)
   - [Transliteration](#transliteration)
   - [Language Detection](#language-detection)
   - [Text-to-Speech](#text-to-speech)
6. [Language Class](#language-class)
7. [Result Objects](#result-objects)
8. [Usage in QuickTranslate](#usage-in-quicktranslate)
9. [Best Practices](#best-practices)
10. [Error Handling](#error-handling)
11. [Supported Languages](#supported-languages)

---

## Overview

**GTranslate** is a free, open-source .NET library that provides unified access to multiple translation services:

| Service | Namespace Class | Features |
|---------|-----------------|----------|
| Google Translate | `GoogleTranslator` | Translation, TTS, Language Detection |
| Bing Translator | `BingTranslator` | Translation, TTS, Language Detection |
| Microsoft Azure | `MicrosoftTranslator` | Translation, Transliteration (best), Language Detection |
| Yandex.Translate | `YandexTranslator` | Translation, Transliteration, Language Detection |

> [!NOTE]
> All translators use **free endpoints** that don't require API keys. However, they may have rate limits.

---

## Installation

### NuGet Package Manager

```bash
dotnet add package GTranslate
```

### Package Reference (csproj)

```xml
<PackageReference Include="GTranslate" Version="2.3.1" />
```

### Supported Frameworks

- .NET 8.0
- .NET Standard 2.0
- .NET Standard 2.1

---

## Available Translators

All translators are located in the `GTranslate.Translators` namespace:

```csharp
using GTranslate.Translators;
```

### Translator Classes

| Class | Description |
|-------|-------------|
| `GoogleTranslator` | Uses Google Translate's web API |
| `BingTranslator` | Uses Bing Translator's web API |
| `MicrosoftTranslator` | Uses Microsoft Azure Translator's free tier |
| `YandexTranslator` | Uses Yandex.Translate's web API |
| `AggregateTranslator` | Groups multiple translators for fallback support |

### ITranslator Interface

All translators implement the `ITranslator` interface:

```csharp
public interface ITranslator
{
    string Name { get; }
    
    Task<ITranslationResult> TranslateAsync(
        string text, 
        string toLanguage, 
        string? fromLanguage = null);
    
    Task<ITranslationResult> TranslateAsync(
        string text, 
        ILanguage toLanguage, 
        ILanguage? fromLanguage = null);
}
```

---

## Core Concepts

### Creating a Translator

```csharp
// Create individual translators
var google = new GoogleTranslator();
var bing = new BingTranslator();
var microsoft = new MicrosoftTranslator();
var yandex = new YandexTranslator();

// Or use AggregateTranslator for automatic fallback
var aggregate = new AggregateTranslator();
```

### Language Codes

Languages can be specified using:
- **ISO 639-1 codes**: `"en"`, `"ar"`, `"es"`, `"fr"`
- **Language names**: `"English"`, `"Arabic"`, `"Spanish"`
- **Language objects**: `Language.GetLanguage("en")`

---

## API Reference

### Translation

The primary method for translating text.

#### Method Signature

```csharp
Task<ITranslationResult> TranslateAsync(
    string text,           // Text to translate
    string toLanguage,     // Target language (code or name)
    string? fromLanguage = null  // Source language (null = auto-detect)
);
```

#### Basic Example

```csharp
using GTranslate.Translators;

var translator = new GoogleTranslator();

// Translate "Hello world" to Spanish
var result = await translator.TranslateAsync("Hello world", "es");

Console.WriteLine(result.Translation);       // "Hola Mundo"
Console.WriteLine(result.SourceLanguage);    // Language object (English)
Console.WriteLine(result.TargetLanguage);    // Language object (Spanish)
Console.WriteLine(result.Service);           // "GoogleTranslator"
```

#### Auto-Detect Source Language

```csharp
// Source language is automatically detected when not specified
var result = await translator.TranslateAsync("Bonjour le monde", "en");
Console.WriteLine(result.SourceLanguage.Name); // "French"
Console.WriteLine(result.Translation);         // "Hello World"
```

#### Translate with Explicit Languages

```csharp
using GTranslate;

var sourceLang = Language.GetLanguage("en");
var targetLang = Language.GetLanguage("ar");

var result = await translator.TranslateAsync(
    "Hello world", 
    targetLang, 
    sourceLang);
```

---

### Transliteration

Converts text from one script to another (e.g., Cyrillic to Latin).

> [!TIP]
> `MicrosoftTranslator` is recommended for transliteration due to its explicit script control.

#### Method Signature

```csharp
Task<ITransliterationResult> TransliterateAsync(
    string text,
    string toLanguage,
    string? fromLanguage = null
);
```

#### Example

```csharp
var yandex = new YandexTranslator();

// Transliterate Russian to English script
var result = await yandex.TransliterateAsync("Привет, мир", "en");

Console.WriteLine(result.Transliteration); // "privet, mir"
```

#### Microsoft Transliteration (with script control)

```csharp
var microsoft = new MicrosoftTranslator();

// More control over source and target scripts
var result = await microsoft.TransliterateAsync(
    "こんにちは",  // Japanese Hiragana
    "ja",          // Japanese language
    "Hrkt",        // From: Hiragana/Katakana
    "Latn"         // To: Latin script
);
```

---

### Language Detection

Detects the language of a given text.

#### Method Signature

```csharp
Task<ILanguage> DetectLanguageAsync(string text);
```

#### Example

```csharp
var translator = new GoogleTranslator();

var detected = await translator.DetectLanguageAsync("Bonjour tout le monde");

Console.WriteLine(detected.Name);    // "French"
Console.WriteLine(detected.ISO6391); // "fr"
```

---

### Text-to-Speech

Generates audio pronunciation for text.

> [!IMPORTANT]
> Not all translators support TTS. Check availability before use.

#### Method Signature

```csharp
Task<Stream> TextToSpeechAsync(
    string text,
    string language,
    bool slow = false  // Some services support slow pronunciation
);
```

#### Example

```csharp
var google = new GoogleTranslator();

using var audioStream = await google.TextToSpeechAsync("Hello world", "en");

// Save to file
using var fileStream = File.Create("output.mp3");
await audioStream.CopyToAsync(fileStream);
```

---

## Language Class

The `Language` class provides access to language metadata and supported services.

### Namespace

```csharp
using GTranslate;
```

### Getting a Language

```csharp
// By ISO 639-1 code
var english = Language.GetLanguage("en");

// By name
var arabic = Language.GetLanguage("Arabic");

// Safe version (returns bool)
if (Language.TryGetLanguage("fr", out var french))
{
    Console.WriteLine(french.Name); // "French"
}
```

### Language Properties

```csharp
var lang = Language.GetLanguage("ar");

lang.Name;           // "Arabic" (English name)
lang.NativeName;     // "العربية"
lang.ISO6391;        // "ar" (2-letter code)
lang.ISO6393;        // "ara" (3-letter code)
lang.SupportedServices; // Flags indicating which translators support this language
```

### Language Dictionary

Access all supported languages:

```csharp
var allLanguages = Language.LanguageDictionary;

foreach (var kvp in allLanguages)
{
    Console.WriteLine($"{kvp.Key}: {kvp.Value.Name}");
}
// Output:
// en: English
// ar: Arabic
// es: Spanish
// ...
```

### Check Service Support

```csharp
var lang = Language.GetLanguage("ar");

// Check if a specific translator supports this language
bool googleSupports = lang.IsGoogleTranslateSupported();
bool bingSupports = lang.IsBingTranslatorSupported();
bool microsoftSupports = lang.IsMicrosoftTranslatorSupported();
bool yandexSupports = lang.IsYandexTranslateSupported();
```

---

## Response Types (Comprehensive)

This section provides detailed documentation of all response types returned by GTranslate operations.

---

### Core Interfaces

#### ITranslationResult

The base interface for all translation results:

```csharp
public interface ITranslationResult
{
    string Translation { get; }       // The translated text
    string Source { get; }            // Original input text
    ILanguage SourceLanguage { get; } // Detected or specified source language
    ILanguage TargetLanguage { get; } // Target language
    string Service { get; }           // Name of the translator service
}
```

#### ITransliterationResult

The base interface for transliteration results:

```csharp
public interface ITransliterationResult
{
    string Transliteration { get; }   // Text converted to target script
    string Source { get; }            // Original input text
    ILanguage SourceLanguage { get; } // Detected or specified source language
    ILanguage TargetLanguage { get; } // Target language/script
    string Service { get; }           // Name of the translator service
}
```

#### ILanguage

Language information returned in results:

```csharp
public interface ILanguage
{
    string Name { get; }              // English name (e.g., "Spanish")
    string NativeName { get; }        // Native name (e.g., "Español")
    string ISO6391 { get; }           // 2-letter code (e.g., "es")
    string ISO6393 { get; }           // 3-letter code (e.g., "spa")
    TranslationServices SupportedServices { get; } // Flags for supported translators
}
```

---

### Google Translator Responses

#### GoogleTranslationResult

The most feature-rich response with additional metadata:

```csharp
var google = new GoogleTranslator();
var result = await google.TranslateAsync("Hello, how are you?", "ar");

// Cast to access Google-specific properties
var googleResult = result as GoogleTranslationResult;
```

**Properties:**

| Property | Type | Description |
|----------|------|-------------|
| `Translation` | `string` | Translated text |
| `Source` | `string` | Original text |
| `SourceLanguage` | `ILanguage` | Detected/specified source language |
| `TargetLanguage` | `ILanguage` | Target language |
| `Service` | `string` | `"GoogleTranslator"` |
| `Transliteration` | `string?` | Latin script pronunciation (when available) |
| `Confidence` | `float?` | Detection confidence (0.0 - 1.0) |

**Example Response:**

```csharp
// Input: "Hello, how are you?" → Arabic
GoogleTranslationResult
{
    Translation = "مرحبا، كيف حالك؟",
    Source = "Hello, how are you?",
    SourceLanguage = Language { Name = "English", ISO6391 = "en" },
    TargetLanguage = Language { Name = "Arabic", ISO6391 = "ar" },
    Service = "GoogleTranslator",
    Transliteration = "marhabaan, kayf haluka?",
    Confidence = 0.98f
}
```

**When Transliteration is Available:**

```csharp
// Japanese to English - includes romanization
var result = await google.TranslateAsync("こんにちは", "en") as GoogleTranslationResult;

Console.WriteLine(result.Translation);      // "Hello"
Console.WriteLine(result.Transliteration);  // "Kon'nichiwa" (romanized Japanese)
```

---

### Bing Translator Responses

#### BingTranslationResult

```csharp
var bing = new BingTranslator();
var result = await bing.TranslateAsync("Good morning", "ja");

var bingResult = result as BingTranslationResult;
```

**Properties:**

| Property | Type | Description |
|----------|------|-------------|
| `Translation` | `string` | Translated text |
| `Source` | `string` | Original text |
| `SourceLanguage` | `ILanguage` | Detected source language |
| `TargetLanguage` | `ILanguage` | Target language |
| `Service` | `string` | `"BingTranslator"` |
| `Transliteration` | `string?` | Romanized pronunciation |

**Example Response:**

```csharp
// Input: "Good morning" → Japanese
BingTranslationResult
{
    Translation = "おはようございます",
    Source = "Good morning",
    SourceLanguage = Language { Name = "English", ISO6391 = "en" },
    TargetLanguage = Language { Name = "Japanese", ISO6391 = "ja" },
    Service = "BingTranslator",
    Transliteration = "Ohayōgozaimasu"
}
```

---

### Microsoft Translator Responses

#### MicrosoftTranslationResult

```csharp
var microsoft = new MicrosoftTranslator();
var result = await microsoft.TranslateAsync("Thank you", "ko");

var msResult = result as MicrosoftTranslationResult;
```

**Properties:**

| Property | Type | Description |
|----------|------|-------------|
| `Translation` | `string` | Translated text |
| `Source` | `string` | Original text |
| `SourceLanguage` | `ILanguage` | Detected source language |
| `TargetLanguage` | `ILanguage` | Target language |
| `Service` | `string` | `"MicrosoftTranslator"` |
| `Transliteration` | `string?` | Romanized form (when applicable) |
| `Score` | `float?` | Translation quality score |

**Example Response:**

```csharp
// Input: "Thank you" → Korean
MicrosoftTranslationResult
{
    Translation = "감사합니다",
    Source = "Thank you",
    SourceLanguage = Language { Name = "English", ISO6391 = "en" },
    TargetLanguage = Language { Name = "Korean", ISO6391 = "ko" },
    Service = "MicrosoftTranslator",
    Transliteration = "gamsahabnida",
    Score = 0.95f
}
```

#### MicrosoftTransliterationResult

Microsoft has the most powerful transliteration with explicit script control:

```csharp
var microsoft = new MicrosoftTranslator();
var result = await microsoft.TransliterateAsync(
    "おはよう",     // Japanese Hiragana
    "ja",           // Language code
    "Hrkt",         // From script: Hiragana/Katakana
    "Latn"          // To script: Latin
);
```

**Example Response:**

```csharp
MicrosoftTransliterationResult
{
    Transliteration = "ohayou",
    Source = "おはよう",
    SourceLanguage = Language { Name = "Japanese", ISO6391 = "ja" },
    TargetLanguage = Language { Name = "Japanese", ISO6391 = "ja" },
    Service = "MicrosoftTranslator",
    SourceScript = "Hrkt",
    TargetScript = "Latn"
}
```

---

### Yandex Translator Responses

#### YandexTranslationResult

```csharp
var yandex = new YandexTranslator();
var result = await yandex.TranslateAsync("Hello world", "ru");

var yandexResult = result as YandexTranslationResult;
```

**Properties:**

| Property | Type | Description |
|----------|------|-------------|
| `Translation` | `string` | Translated text |
| `Source` | `string` | Original text |
| `SourceLanguage` | `ILanguage` | Detected source language |
| `TargetLanguage` | `ILanguage` | Target language |
| `Service` | `string` | `"YandexTranslator"` |

**Example Response:**

```csharp
// Input: "Hello world" → Russian
YandexTranslationResult
{
    Translation = "Привет мир",
    Source = "Hello world",
    SourceLanguage = Language { Name = "English", ISO6391 = "en" },
    TargetLanguage = Language { Name = "Russian", ISO6391 = "ru" },
    Service = "YandexTranslator"
}
```

#### YandexTransliterationResult

Yandex has dedicated transliteration support:

```csharp
var yandex = new YandexTranslator();
var result = await yandex.TransliterateAsync("Привет, мир", "en");
```

**Example Response:**

```csharp
YandexTransliterationResult
{
    Transliteration = "privet, mir",
    Source = "Привет, мир",
    SourceLanguage = Language { Name = "Russian", ISO6391 = "ru" },
    TargetLanguage = Language { Name = "English", ISO6391 = "en" },
    Service = "YandexTranslator"
}
```

---

### Language Detection Responses

All translators return an `ILanguage` object when detecting language:

```csharp
var google = new GoogleTranslator();
var detected = await google.DetectLanguageAsync("Bonjour tout le monde");
```

**Response Structure:**

```csharp
ILanguage
{
    Name = "French",
    NativeName = "Français",
    ISO6391 = "fr",
    ISO6393 = "fra",
    SupportedServices = TranslationServices.Google | 
                        TranslationServices.Bing | 
                        TranslationServices.Microsoft | 
                        TranslationServices.Yandex
}
```

---

### Text-to-Speech Response

TTS methods return a `Stream` containing audio data:

```csharp
var google = new GoogleTranslator();
Stream audioStream = await google.TextToSpeechAsync("Hello", "en");

// Stream properties:
// - Format: MP3 (typically)
// - Can be saved to file or played directly
```

**Complete TTS Example:**

```csharp
var google = new GoogleTranslator();

// Get audio stream
using var audioStream = await google.TextToSpeechAsync(
    text: "Hello, how are you?",
    language: "en",
    slow: false  // Normal speed
);

// Save to file
using var fileStream = File.Create("speech.mp3");
await audioStream.CopyToAsync(fileStream);

// Or play directly using audio library
// NAudio, MediaPlayer, etc.
```

---

### Response Comparison Table

| Translator | Translation | Transliteration | Confidence | Script Control | TTS |
|------------|-------------|-----------------|------------|----------------|-----|
| Google | ✅ `GoogleTranslationResult` | ✅ (in result) | ✅ `float?` | ❌ | ✅ |
| Bing | ✅ `BingTranslationResult` | ✅ (in result) | ❌ | ❌ | ✅ |
| Microsoft | ✅ `MicrosoftTranslationResult` | ✅ Dedicated | ✅ Score | ✅ | ❌ |
| Yandex | ✅ `YandexTranslationResult` | ✅ Dedicated | ❌ | ❌ | ❌ |

---

### Accessing Extended Properties

To access translator-specific properties, cast the result:

```csharp
// Generic interface
ITranslationResult result = await translator.TranslateAsync("Hello", "es");

// Access common properties
Console.WriteLine(result.Translation);
Console.WriteLine(result.SourceLanguage.Name);

// Access Google-specific properties
if (result is GoogleTranslationResult googleResult)
{
    Console.WriteLine($"Confidence: {googleResult.Confidence}");
    Console.WriteLine($"Transliteration: {googleResult.Transliteration}");
}

// Access Microsoft-specific properties
if (result is MicrosoftTranslationResult msResult)
{
    Console.WriteLine($"Score: {msResult.Score}");
}
```

---

### ToString() Output

All result types override `ToString()` for easy debugging:

```csharp
var result = await google.TranslateAsync("Hello", "es");
Console.WriteLine(result);

// Output:
// Translation: 'Hola', TargetLanguage: 'Spanish (es)', SourceLanguage: 'English (en)', Service: GoogleTranslator
```

---

## Usage in QuickTranslate

The QuickTranslate project wraps GTranslate in a service abstraction:

### ITranslationService Interface

```csharp
public interface ITranslationService
{
    Task<TranslationModel> TranslateAsync(
        string text, 
        string targetLanguage, 
        string? sourceLanguage = null);
    
    string ProviderName { get; }
    void SetProvider(string providerName);
    string[] GetAvailableProviders();
}
```

### GTranslateService Implementation

```csharp
public class GTranslateService : ITranslationService, IDisposable
{
    // Available providers
    private static readonly string[] AvailableProviderNames = 
        ["Google", "Bing", "Microsoft", "Yandex"];
    
    private ITranslator? _currentTranslator;
    private string _currentProviderName = "Google";

    public string ProviderName => _currentProviderName;

    // Switch between providers
    public void SetProvider(string providerName)
    {
        // Validates provider name
        // Disposes old translator
        // Lazy-loads new translator on next translate call
    }

    public async Task<TranslationModel> TranslateAsync(
        string text, 
        string targetLanguage, 
        string? sourceLanguage = null)
    {
        // Lazy-load translator
        _currentTranslator ??= CreateTranslator(_currentProviderName);
        
        var result = await _currentTranslator.TranslateAsync(
            text, 
            targetLanguage, 
            sourceLanguage);
        
        return new TranslationModel
        {
            OriginalText = text,
            MainTranslation = result.Translation,
            SourceLanguage = result.SourceLanguage.Name,
            TargetLanguage = result.TargetLanguage.Name,
            ProviderName = _currentProviderName
        };
    }
}
```

### Usage Example

```csharp
// Create service
var service = new GTranslateService();

// Get available providers
string[] providers = service.GetAvailableProviders();
// ["Google", "Bing", "Microsoft", "Yandex"]

// Switch provider
service.SetProvider("Bing");

// Translate
var result = await service.TranslateAsync("Hello world", "ar");

Console.WriteLine(result.MainTranslation);  // "مرحبا بالعالم"
Console.WriteLine(result.ProviderName);     // "Bing"
```

---

## Best Practices

### 1. Lazy Loading

```csharp
// ✅ Good: Create translator once, reuse
private ITranslator? _translator;

public async Task<string> Translate(string text, string target)
{
    _translator ??= new GoogleTranslator();
    var result = await _translator.TranslateAsync(text, target);
    return result.Translation;
}

// ❌ Bad: Creating new translator for each request
public async Task<string> Translate(string text, string target)
{
    var translator = new GoogleTranslator(); // Creates HTTP client each time
    var result = await translator.TranslateAsync(text, target);
    return result.Translation;
}
```

### 2. Proper Disposal

```csharp
public class MyService : IDisposable
{
    private readonly ITranslator _translator = new GoogleTranslator();

    public void Dispose()
    {
        if (_translator is IDisposable disposable)
        {
            disposable.Dispose();
        }
    }
}
```

### 3. Use Language Objects for Type Safety

```csharp
// ✅ Compile-time validated
var result = await translator.TranslateAsync(
    text, 
    Language.GetLanguage("es"),
    Language.GetLanguage("en"));

// ⚠️ String-based (runtime errors possible)
var result = await translator.TranslateAsync(text, "es", "en");
```

### 4. Fallback with AggregateTranslator

```csharp
// Automatically tries next translator if one fails
var aggregate = new AggregateTranslator();
var result = await aggregate.TranslateAsync("Hello", "es");
```

---

## Error Handling

### Common Exceptions

| Exception | Cause |
|-----------|-------|
| `TranslatorException` | Translation service error |
| `ArgumentException` | Invalid language code |
| `HttpRequestException` | Network/connectivity issues |

### Recommended Pattern

```csharp
try
{
    var result = await translator.TranslateAsync(text, targetLanguage);
    return result.Translation;
}
catch (TranslatorException ex)
{
    // Service-specific error (rate limit, service down, etc.)
    logger.LogError(ex, "Translation service error");
    return $"[Translation Error: {ex.Message}]";
}
catch (ArgumentException ex)
{
    // Invalid language code
    logger.LogWarning(ex, "Invalid language specified");
    throw;
}
catch (HttpRequestException ex)
{
    // Network error
    logger.LogError(ex, "Network error during translation");
    return "[Network Error - Please check connection]";
}
```

---

## Supported Languages

### Commonly Used Language Codes

| Language | Code | Native Name |
|----------|------|-------------|
| English | `en` | English |
| Arabic | `ar` | العربية |
| Spanish | `es` | Español |
| French | `fr` | Français |
| German | `de` | Deutsch |
| Chinese (Simplified) | `zh-Hans` | 简体中文 |
| Chinese (Traditional) | `zh-Hant` | 繁體中文 |
| Japanese | `ja` | 日本語 |
| Korean | `ko` | 한국어 |
| Russian | `ru` | Русский |
| Portuguese | `pt` | Português |
| Italian | `it` | Italiano |
| Turkish | `tr` | Türkçe |
| Hindi | `hi` | हिन्दी |

### Getting All Languages

```csharp
foreach (var lang in Language.LanguageDictionary.Values)
{
    Console.WriteLine($"{lang.ISO6391}: {lang.Name} ({lang.NativeName})");
}
```

---

## Resources

- **NuGet Package**: [nuget.org/packages/GTranslate](https://www.nuget.org/packages/GTranslate)
- **GitHub Repository**: [github.com/d4n3436/GTranslate](https://github.com/d4n3436/GTranslate)
- **License**: MIT

---

*Documentation generated for GTranslate v2.3.1*
