using SmartHome.Backend.NILM.Models;

namespace SmartHome.Backend.NILM.Services;

public interface ISeq2PointInferenceService : IAsyncDisposable
{
    /// <summary>
    /// Aggiunge un campione (un secondo). Restituisce il risultato solo quando la finestra è piena.
    /// </summary>
    /// <param name="timestampUtc">Timestamp UTC del campione (secondo corrente)</param>
    /// <param name="mainsWatts">Potenza totale (mains) in Watt</param>
    /// <returns>Risultato o null se la finestra non è ancora piena</returns>
    Seq2PointResult? AddSample(DateTime timestampUtc, double mainsWatts);

    /// <summary>
    /// Aggiunge una serie di campioni in batch, restituisce solo i risultati prodotti (uno per secondo dopo warm-up).
    /// </summary>
    IEnumerable<Seq2PointResult> AddSamplesBatch(IEnumerable<(DateTime ts, double mainsWatts)> samples);
}

