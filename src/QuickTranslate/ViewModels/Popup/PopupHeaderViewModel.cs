using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using QuickTranslate.Models;
using QuickTranslate.Services;

namespace QuickTranslate.ViewModels;

public partial class PopupHeaderViewModel : ObservableObject
{
    private readonly IClipboardService _clipboardService;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(CopyTranslationCommand))]
    private TranslationModel? _currentTranslation;

    public PopupHeaderViewModel(IClipboardService clipboardService)
    {
        _clipboardService = clipboardService;
    }

    [RelayCommand(CanExecute = nameof(CanCopyTranslation))]
    private void CopyTranslation()
    {
        if (CurrentTranslation != null)
        {
            _clipboardService.SetText(CurrentTranslation.MainTranslation);
        }
    }

    private bool CanCopyTranslation()
    {
        return CurrentTranslation != null && !string.IsNullOrWhiteSpace(CurrentTranslation.MainTranslation);
    }
}
