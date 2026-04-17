using System;
using System.Threading;
using System.Threading.Tasks;
using QuickTranslate.Models;
using System.Collections.Generic;
using System.Linq;

namespace QuickTranslate.ViewModels;

public partial class PronunciationViewModel
{
    public void PrepareForLoading(string text)
    {
        _pronunciationGeneration++;
        ResetData();
        OriginalText = text?.Trim() ?? string.Empty;
        IsLoading = true;
        StatusMessage = string.Empty;
    }

    public async Task LoadPronunciationAsync(string text)
    {
        bool alreadyPrepared = IsLoading && OriginalText == text?.Trim();

        if (!alreadyPrepared)
        {
            _pronunciationGeneration++;
            if (string.IsNullOrWhiteSpace(text))
            {
                ResetData();
                return;
            }
            ResetData();
            OriginalText = text.Trim();
            IsLoading = true;
            StatusMessage = string.Empty;
        }

        try
        {
            var words = OriginalText.Split(new[] { ' ', '\r', '\n', '\t' }, StringSplitOptions.RemoveEmptyEntries);
            IsSingleWord = words.Length == 1;

            if (!IsSingleWord) PopulateWords(words);

            var result = await _pronunciationService.GetPronunciationAsync(OriginalText);
            if (!result.IsSuccess)
            {
                StatusMessage = result.Message;
                Syllables.Add(new SyllableItem { Text = OriginalText });
                await UpdateAudioUriAsync();
                return;
            }

            var data = result.Data!;
            _detectedLanguageCode = data.DetectedLanguageCode;
            LanguageName = _languageService.GetLanguageName(_detectedLanguageCode);

            if (IsSingleWord)
            {
                if (!string.IsNullOrEmpty(data.Phonetics)) PhoneticsDisplay = $"/{data.Phonetics}/";
                foreach (var s in data.Syllables) Syllables.Add(s);
            }
            else
            {
                PhoneticsDisplay = string.Empty;
                Syllables.Clear();
            }

            IsSlowMode = false;
            OnPropertyChanged(nameof(IsSlowMode));

            if (data.AudioUri != null) AudioUri = data.AudioUri;
            else await UpdateAudioUriAsync();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Pronunciation Error: {ex.Message}");
            Syllables.Add(new SyllableItem { Text = OriginalText });
            StatusMessage = "An unexpected error occurred.";
        }
        finally
        {
            IsLoading = false;
        }
    }

    private void ResetData()
    {
        OriginalText = string.Empty;
        Syllables.Clear();
        Words.Clear();
        _chunkWordRanges.Clear();
        _wordAnimationCts?.Cancel();
        AudioUri = null;
        PhoneticsDisplay = string.Empty;
        IsPlaying = false;
    }

    public void HideWindow()
    {
        _pronunciationGeneration++;
        IsVisible = false;
        IsPlaying = false;
        StopStreaming();
    }

    private void PopulateWords(string[] words)
    {
        Words.Clear();
        foreach (var word in words)
            Words.Add(new WordItem { Text = word });
    }
}
