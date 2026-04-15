using System.Windows.Input;
using QuickTranslate.Helpers;
using QuickTranslate.Models;
using QuickTranslate.Services;

namespace QuickTranslate.ViewModels;

public class PopupHeaderViewModel : ViewModelBase
{
    private readonly IClipboardService _clipboardService;
    private TranslationModel? _currentTranslation;

    public TranslationModel? CurrentTranslation
    {
        get => _currentTranslation;
        set => SetProperty(ref _currentTranslation, value);
    }

    public ICommand CopyCommand { get; }

    public PopupHeaderViewModel(IClipboardService clipboardService)
    {
        _clipboardService = clipboardService;
        CopyCommand = new RelayCommand(CopyTranslation, CanCopyTranslation);
    }

    private bool CanCopyTranslation()
    {
        return CurrentTranslation != null && !string.IsNullOrWhiteSpace(CurrentTranslation.MainTranslation);
    }

    private void CopyTranslation()
    {
        if (CurrentTranslation != null)
        {
            _clipboardService.SetText(CurrentTranslation.MainTranslation);
        }
    }
}
