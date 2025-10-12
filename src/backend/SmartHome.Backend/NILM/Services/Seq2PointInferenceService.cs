using Microsoft.ML.OnnxRuntime;
using Microsoft.ML.OnnxRuntime.Tensors;
using SmartHome.Backend.NILM.Models;
using SessionOptions = Microsoft.ML.OnnxRuntime.SessionOptions;

namespace SmartHome.Backend.NILM.Services;

/// <summary>
/// Servizio di inferenza Seq2Point (finestra scorrevole). Thread-safe.
/// </summary>
public sealed class Seq2PointInferenceService : ISeq2PointInferenceService
{
    private readonly InferenceSession _session;
    private readonly string _inputName;
    private readonly string _outputName;
    private readonly NormalizationStats _stats;
    private readonly int _window;
    private readonly int _half;
    private readonly double _threshold;
    private readonly double[] _ring;
    private int _count;
    private int _write;
    private readonly object _lock = new();

    /// <summary>Costruisce la sessione ONNX con fallback CPU se GPU non disponibile.</summary>
    private static SessionOptions CreateSessionOptions(bool tryGpu)
    {
        var so = new SessionOptions();
       
        return so;
    }

    public Seq2PointInferenceService(
        string modelPath,
        NormalizationStats stats,
        int window = 99,
        double thresholdW = 20.0,
        bool tryGpu = true,
        SessionOptions? sessionOptions = null)
    {
        if (window % 2 == 0) throw new ArgumentException("window deve essere dispari", nameof(window));
        if (!File.Exists(modelPath)) throw new FileNotFoundException("Modello ONNX non trovato", modelPath);

        _stats = stats with
        {
            MainsStd = stats.MainsStd == 0 ? 1.0 : stats.MainsStd,
            TargetStd = stats.TargetStd == 0 ? 1.0 : stats.TargetStd
        };
        _window = window;
        _half = window / 2;
        _threshold = thresholdW;
        _ring = new double[window];

        sessionOptions ??= CreateSessionOptions(tryGpu);
        _session = new InferenceSession(modelPath, sessionOptions);
        _inputName = _session.InputMetadata.Keys.First();
        _outputName = _session.OutputMetadata.Keys.First();
    }

    /// <summary>
    /// Aggiunge un singolo campione (1 secondo). Restituisce un risultato solo dopo il warm-up (finestra piena).
    /// CenterTimestamp = timestampUtc - halfWindow.
    /// </summary>
    public Seq2PointResult? AddSample(DateTime timestampUtc, double mainsWatts)
    {
        lock (_lock)
        {
            _ring[_write] = mainsWatts;
            _write = (_write + 1) % _window;
            if (_count < _window) _count++;
            if (_count < _window) return null; // warm-up

            // Estrazione finestra ordinata + normalizzazione
            var norm = new float[_window];
            int start = (_write - _window + _window) % _window;
            for (int i = 0; i < _window; i++)
            {
                double v = _ring[(start + i) % _window];
                norm[i] = (float)((v - _stats.MainsMean) / _stats.MainsStd);
            }

            // Tensor [1, window, 1]
            var input = new DenseTensor<float>(new[] { 1, _window, 1 });
            for (int i = 0; i < _window; i++) input[0, i, 0] = norm[i];

            var inputs = new List<NamedOnnxValue>
            {
                NamedOnnxValue.CreateFromTensor(_inputName, input)
            };
            using var outputs = _session.Run(inputs);
            float yNorm = outputs.First(o => o.Name == _outputName).AsEnumerable<float>().First();

            double predWatts = yNorm * _stats.TargetStd + _stats.TargetMean;
            bool on = predWatts >= _threshold;
            var center = timestampUtc - TimeSpan.FromSeconds(_half);
            return new Seq2PointResult(center, predWatts, on, _threshold);
        }
    }

    public IEnumerable<Seq2PointResult> AddSamplesBatch(IEnumerable<(DateTime ts, double mainsWatts)> samples)
    {
        foreach (var (ts, w) in samples)
        {
            var r = AddSample(ts, w);
            if (r is not null) yield return r;
        }
    }

    public ValueTask DisposeAsync()
    {
        _session.Dispose();
        return ValueTask.CompletedTask;
    }
}

