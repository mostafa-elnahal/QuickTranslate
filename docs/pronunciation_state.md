# Pronunciation Functionality State

This document captures the current state of the pronunciation and syllable functionality in QuickTranslate.

## Current Status (As of Jan 2026)
- **Core Implementation**: Complete.
- **Providers**: Google (Fallback) and Gemini (Premium/Slow Mode) are functional.
- **UI**: 
    - Dedicated popup (`PronunciationPopup.xaml`) defaults to a loading state with spinner.
    - "Karaoke" animation is synchronized with audio duration.
- **Recent Improvements**:
    - Fixed loading spinner logic.
    - Improved error message display (user-friendly text over raw exceptions).
    - Thread-safety for audio generation (using generation counters).

## Overview
The pronunciation feature allows users to get high-quality audio pronunciation for text (primarily single words), broken down into syllables with visual feedback during playback. It supports multiple providers (Google, Gemini) and includes a "karaoke-style" animation where current syllables highlight as audio plays.

## Architecture

### Services
The core logic is distributed across three main services:

1.  **`PronunciationService`** (`Services/PronunciationService.cs`)
    *   **Role**: Facade/Orchestrator.
    *   **Responsibility**: routes requests to the active provider (Settings-defined).
    *   **Key Interface**: `IPronunciationService`.

2.  **`SyllableService`** (`Services/SyllableService.cs`)
    *   **Role**: Phonetic logic engine.
    *   **Responsibility**:
        *   Breaks words into syllables using rule-based algorithms (English-focused).
        *   Maps IPA (International Phonetic Alphabet) to simple "sounds like" respellings.
        *   Identifying stress patterns from IPA (e.g., `'` to highlight primary stress).
    *   **Key Interface**: `ISyllableService`.

3.  **Providers** (`Services/PronunciationProviders/`)
    *   **`GooglePronunciationProvider`**: Fallback/Standard provider. Uses `TranslationService` for phonetics and likely standard TTS (implied).
    *   **`GeminiPronunciationProvider`**: Advanced provider.
        *   Uses Gemini API (`gemini-2.5-flash-preview-tts`) for high-quality AI speech.
        *   Supports "Slow Mode" via specific prompt engineering.
        *   Caches generated audio to temp files to reduce API calls.
        *   **File**: `Services/PronunciationProviders/GeminiPronunciationProvider.cs`.

### Data Models (`Models/`)

*   **`PronunciationData`**
    *   Holds the complete state for a pronunciation request.
    *   Properties: `OriginalText`, `Phonetics` (IPA), `AudioUri`, `Syllables` (List<SyllableItem>), `DetectedLanguageCode`.
*   **`SyllableItem`**
    *   Represents a single syllable chunk.
    *   Properties:
        *   `Text`: The display text (e.g., "he" of "hello").
        *   `IsStressed` (bool): If true, bolded/highlighted as primary stress.
        *   `IsActive` (bool): Used for playback animation (karaoke effect).

## User Interface

### 1. Pronunciation Popup (`Views/PronunciationPopup.xaml`)
A dedicated, transient window separate from the main translation popup.
*   **Trigger**: Activated via Global Hotkey (Default: `Ctrl+Shift+P`).
*   **Visual Elements**:
    *   **Large Source Text**: Centered.
    *   **"Sounds Like"**: Horizontal list of syllables separated by dots (` · `).
    *   **Visual Feedback**: Syllables light up (`IsActive`) during visual playback simulation.
    *   **IPA Display**: Shows technical phonetics below the simple syllables.
    *   **Controls**: Play/Pause Toggle, Restart Button, Slow Mode Toggle.
*   **Logic**: `PronunciationViewModel` handles state, audio playback requests, and animation timing.

### 2. Translation Popup Integration (`Views/TranslationPopup.xaml`)
The main translation window also embeds pronunciation features for quick access.
*   **Trigger**: Automatically appears when translating a **single word** if enabled in settings.
*   **Visual Elements**:
    *   **Speaker Button**: Allows playing the audio directly from the main popup.
    *   **IPA Phonetics**: Displays the phonetic transcription (e.g., `/həˈloʊ/`) next to the source word.
    *   **Karaoke Animation**: highlighting the source text during playback (implied by `OriginalText` binding in similar context).
*   **Logic**:
    *   `PopupViewModel.cs` determines `IsSingleWord`.
    *   If `ShowPronunciation` setting is true, the `PronunciationSection` border becomes visible.
    *   Uses a separate `MediaElement` (`PronunciationAudioPlayer`) for playback within this window.

### 3. Settings Integration (`Views/SettingsWindow.xaml`)
*   **Section**: "Pronunciation" category.
*   **Configurable Options**:
    *   **Provider**: Select between "Google" or "Gemini".
    *   **Gemini API Key**: Password field, enabled only when Gemini is selected.
    *   **Single Word Lookup**: Toggle to show the pronunciation controls in the main **Translation Popup**.
    *   **Hotkey**: Configurable via the "Hotkeys" tab (Default: `Ctrl+Shift+P`) for the dedicated **Pronunciation Popup**.

## Key Workflows

### Audio Generation (Gemini Provider)
- **Streaming Support**: Uses `streamGenerateContent` API for low-latency playback.
- **Protocol**:
    1.  **Handshake**: Sends configuration (Voice: "Zephyr", Speed: 1.0 or 0.85).
    2.  **Chunking**: Receives audio in ~1KB chunks.
    3.  **Buffering**: Pushes PCM data to `NAudioStreamingPlayer`.
- **Latency Optimization**:
    - **Gradual Ramp-Up**: First chunks have smaller buffer thresholds (200ms) to start playback instantly.
    - **Smart Chunking**: Splits long text into sentences to stream sequentially.

### Audio Playback Architecture
1.  **`IStreamingAudioPlayer` Service**:
    *   Interface for streaming playback capabilities.
    *   **Methods**: `Initialize`, `EnqueueSamples`, `Play`, `Pause`, `Resume`, `Restart`, `Stop`.
    *   **Properties**: `Volume` (0.0-1.0), `IsPlaying`, `IsPaused`.

2.  **`NAudioStreamingPlayer` Implementation**:
    *   **Core**: Uses `WaveOutEvent` and `BufferedWaveProvider` from NAudio.
    *   **PCM Caching**: Automatically calls `_pcmHistory.Add()` for every enqueued chunk. This allows `Restart()` to replay audio instantly from memory without re-fetching from the API.
    *   **State Management**: Tracks playback state accurately to toggle UI controls.

### Playback & Animation
*   **Audio**: Uses WPF `MediaElement` (hidden in `PronunciationPopup.xaml`).
*   **Animation**:
    *   Triggered on `MediaOpened`.
    *   Calculates `duration / syllableCount`.
    *   Iterates through `Syllables` list, setting `IsActive = true` sequentially to simulate karaoke tracking.

## File Inventory

| Component | File Path |
| :--- | :--- |
| **Contracts** | `Services/IPronunciationService.cs`<br>`Services/ISyllableService.cs`<br>`Services/Audio/IStreamingAudioPlayer.cs` |
| **Logic** | `Services/PronunciationService.cs`<br>`Services/SyllableService.cs`<br>`Services/Audio/NAudioStreamingPlayer.cs` |
| **AI Provider** | `Services/PronunciationProviders/GeminiPronunciationProvider.cs` |
| **Models** | `Models/PronunciationData.cs`<br>`Models/SyllableItem.cs` |
| **Pronunciation UI** | `Views/PronunciationPopup.xaml`<br>`Views/PronunciationPopup.xaml.cs`<br>`ViewModels/PronunciationViewModel.cs` |
| **Translation UI** | `Views/TranslationPopup.xaml`<br>`ViewModels/PopupViewModel.cs` |
| **Settings** | `Views/SettingsWindow.xaml`<br>`ViewModels/SettingsViewModel.cs` |
| **Entry Point** | `App.xaml.cs` (DI Registration) |
