using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Input = MNN.Unity.MNNGenerationGraph.Input;
using Output = MNN.Unity.MNNGenerationGraph.Output;

namespace MNN.Unity
{
    public sealed partial class MNNPocketTts
    {
        private void EnsureGraphs()
        {
            if (_main != null)
                return;
            try
            {
                string path = Path.Combine(_root, "mnn", "flow_lm_main_w8.mnn");
                _priming = new MNNExpressGraph(path, _threads, MNNBackendType.CPU, MNNPocketGraph.Main(File.ReadAllBytes(path), _cacheNames, true));
                _main = new MNNExpressGraph(path, _threads, MNNBackendType.CPU, MNNPocketGraph.Main(File.ReadAllBytes(path), new[]{"conditioning", "eos_logit"}.Concat(_cacheNames).ToArray(), false), _mainPrecision);
                path = Path.Combine(_root, "mnn", "mimi_encoder_w8.mnn");
                _encoder = new MNNExpressGraph(path, _threads, MNNBackendType.CPU, MNNPocketGraph.Encoder(File.ReadAllBytes(path)));
                _decoder = new MNNGenerationGraph(Path.Combine(_root, "mnn", "mimi_decoder_w8.mnn"), _threads, backendType: MNNBackendType.CPU);
                _conditioner = new MNNGenerationGraph(Path.Combine(_root, "mnn", "text_conditioner_fp32.mnn"), _threads, backendType: MNNBackendType.CPU);
                _flow = new MNNGenerationGraph(Path.Combine(_root, "mnn", "flow_lm_flow_w8.mnn"), _threads, backendType: MNNBackendType.CPU);
            }
            catch
            {
                ReleaseGraphs();
                throw;
            }
        }

        private static Output[] Run(MNNExpressGraph graph, string[] outputs, List<Input> state, Input input)
        {
            return graph.RunOutputs(outputs, state.Concat(new[]{input}).Where(item => graph.ContainsVariable(item.Name)).ToArray());
        }

        private void ReleaseGraphs()
        {
            _encoder?.Dispose();
            _priming?.Dispose();
            _main?.Dispose();
            _decoder?.Dispose();
            _conditioner?.Dispose();
            _flow?.Dispose();
            _encoder = _priming = _main = null;
            _conditioner = _flow = _decoder = null;
        }
    }
}
