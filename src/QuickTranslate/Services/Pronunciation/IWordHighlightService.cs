using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using QuickTranslate.Models;

namespace QuickTranslate.Services;

public interface IWordHighlightService
{
    Task AnimateWordsAsync(
        IAudioPlaybackService playbackService,
        IList<WordItem> words,
        CancellationToken ct,
        Task? startSignal = null);
}
