# QuickTranslate Agent Guidelines

This document outlines the coding standards, architecture, and patterns used in the QuickTranslate project. Agents should follow these guidelines to ensure consistency and maintainability.

## 1. Technology Stack
- **Framework**: .NET 8.0
- **UI Framework**: WPF (Windows Presentation Foundation)
- **Language**: C# 12 (latest supported by .NET 8)
- **External Libraries**: 
    - `GTranslate` (Translation)
    - `CsWin32` (Native Interop)

## 2. Architecture & Patterns
- **Pattern**: MVVM (Model-View-ViewModel)
- **Dependency Injection**: 
    - **Manual DI** in `App.xaml.cs`.
    - Services are instantiated in `Application_Startup` and injected into ViewModels via constructors.
    - No external IoC container is used.
- **Services**:
    - Defined by an interface (e.g., `ITranslationService`) and an implementation (e.g., `GTranslateService`).
    - Located in `Services/`.
- **ViewModels**:
    - Inherit from `ViewModelBase`.
    - Located in `ViewModels/`.
    - Use `SetProperty` for `INotifyPropertyChanged`.

## 3. Coding Conventions

### Formatting
- **Namespaces**: Use file-scoped namespaces (e.g., `namespace QuickTranslate.Services;`).
- **Braces**: **Allman Style** (braces on a new line).
    ```csharp
    public void MyMethod()
    {
        if (condition)
        {
            // Code
        }
    }
    ```
- **Indentation**: 4 spaces.

### Naming
- **Classes/Interfaces/Methods/Properties**: PascalCase.
- **Private Fields**: _camelCase (e.g., `_translationService`).
- **Parameters/Locals**: camelCase.
- **Async Methods**: Suffix with `Async` (e.g., `LoadPronunciationAsync`).

### Language Features
- **Nullability**: Enabled (`<Nullable>enable</Nullable>`). Explicitly handle `null` or use `?`.
- **Async/Await**: Use `async Task` for asynchronous operations. Avoid `async void` except for event handlers.
- **Typing**: Use `var` when the type is obvious from the right-hand side.

## 4. Common Tasks

### Adding a New Service
1. Create interface in `Services/<Category>/IMyService.cs`.
2. Create implementation in `Services/<Category>/MyService.cs`.
3. Register/Instantiate in `App.xaml.cs` inside `Application_Startup`.
4. Inject into ViewModels as needed.

### Adding a New ViewModel
1. Create class in `ViewModels/` inheriting from `ViewModelBase`.
2. Define properties using `SetProperty`.
3. Add constructor dependencies.
4. Instantiate in `App.xaml.cs` and pass strictly required dependencies.

### Error Handling
- Wrap service calls in `try-catch` blocks within ViewModels.
- Update a `StatusMessage` or similar property to notify the user of errors.
- Log errors using `System.Diagnostics.Debug.WriteLine` (or a logging service if available).

## 5. File Structure
- `App.xaml.cs`: Composition Root (DI setup).
- `Views/`: XAML windows and user controls.
- `ViewModels/`: Logic and state for views.
- `Services/`: Business logic and external integrations.
- `Models/`: Data structures (DTOs).

## 6. Testing
- Currently, no automated test project is linked in the solution (based on file listing).
- Verify changes by running the application and testing the specific feature manually.

## 7. Feature-Specific Guidelines

### Pronunciation Feature
- **Text Processing**: Always use `ISyllableService` for splitting text. Do not rely on simple string manipulation for syllables.
- **Audio Providers**:
    - Use `IPronunciationService` facade rather than accessing providers directly.
    - **Google**: Standard, usually faster, no API key needed.
    - **Gemini**: Premium, requires API key, supports "Slow Mode". Handles its own caching.
- **Concurrency**:
    - Pronunciation requests are frequent.
    - **Always** use the "Generation Counter" pattern (`_pronunciationGeneration`) in ViewModels to discard stale results.
- **Testing**:
    - When testing `GeminiProvider`, ensure the API key is set in Settings.
    - Verify "Slow Mode" actually changes prompt/speed (audible difference).
