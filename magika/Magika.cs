/*
 * This code is a modified version of the original code, which is licensed under the Apache 2.0 License.
 * The original code can be found at: https://github.com/google/magika/
 * And the original license is as follows:
 *
 * Copyright 2024 Google LLC
 * 
 * Licensed under the Apache License, Version 2.0 (the "License");
 * you may not use this file except in compliance with the License.
 * You may obtain a copy of the License at
 * 
 *     http://www.apache.org/licenses/LICENSE-2.0
 * 
 * Unless required by applicable law or agreed to in writing, software
 * distributed under the License is distributed on an "AS IS" BASIS,
 * WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
 * See the License for the specific language governing permissions and
 * limitations under the License.
 */

using Microsoft.ML.OnnxRuntime;
using Microsoft.ML.OnnxRuntime.Tensors;
using System.Text;
using System.Reflection;
using System.Diagnostics;

namespace magika;

public class Magika : IDisposable
{
    private readonly string _default_model_name;
    private readonly float _medium_confidence_threshold;
    private readonly int _min_file_size_for_dl;
    private readonly int _padding_token;
    private readonly int _block_size;

    private static readonly string _baseDir = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
    private const string modelLogicalName = "magika_model.onnx";

    ModelConfig _model_config;
    Dictionary<string, float> _thresholds;
    Dictionary<string, string> _model_output_overwrite_map;
    Dictionary<string, int> _input_sizes;

    private readonly int _input_column_size;
    private readonly int _output_column_size;

    string[] _target_labels_space_np;
    public PredictionMode prediction_mode { get; set; } = PredictionMode.HIGH_CONFIDENCE;
    public bool no_dereference { get; set; } = false;

    ContentTypesManager _ctm;

    InferenceSession _onnx_session;

    readonly SimpleLogger _logger;
    static readonly Stopwatch _stopwatch = new();

    public Magika(
        PredictionMode prediction_mode = PredictionMode.HIGH_CONFIDENCE,
        bool no_dereference = false,
        bool verbose = false,
        bool debug = false,
        bool use_colors = false
    )
    {
        InitAssemblyResolve();

        this._logger = new SimpleLogger(useColors: use_colors);
        if (verbose)
        {
            this._logger.LogLevel = LogLevel.Information;
        }
        if (debug)
        {
            this._logger.LogLevel = LogLevel.Debug;
        }

        // Default model, used in case not specified via the Magika constructor
        this._default_model_name = MagikaConfig.default_model_name;
        // Minimum threshold for "default" prediction mode
        this._medium_confidence_threshold = MagikaConfig.medium_confidence_threshold;
        // # Minimum file size for using the DL model
        this._min_file_size_for_dl = MagikaConfig.min_file_size_for_dl;
        // Which integer we use to indicate padding
        this._padding_token = MagikaConfig.padding_token;
        this._block_size = MagikaConfig.block_size;

        this._model_config = new ModelConfig();
        this._thresholds = this._model_config.thresholds;
        this._model_output_overwrite_map = this._model_config.model_output_overwrite_map;
        this._input_sizes = this._model_config.input_sizes;
        this._target_labels_space_np = this._model_config.target_labels_space;

        this._input_column_size = this._input_sizes["beg"] + this._input_sizes["mid"] + this._input_sizes["end"];
        this._output_column_size = this._target_labels_space_np.Length;

        this.no_dereference = no_dereference;
        this.prediction_mode = prediction_mode;
        this._ctm = new ContentTypesManager();
        this._onnx_session = InitOnnxSession();
    }

    public MagikaResult IdentifyPath(string path) => this.GetResultFromPath(path);
    public List<MagikaResult> IdentifyPaths(string[] paths) => this.GetResultsFromPaths(paths);
    public MagikaResult IdentifyBytes(byte[] bytes) => this.GetResultFromBytes(bytes);

    public static string GetDefaultModelName()
    {
        /* This returns the default model name.
           We make this method static so that it can be used by the client (to
           print help, etc.) without the need to instantiate a Magika object.
        */
        return MagikaConfig.default_model_name;
    }

    public string GetModelName() => _default_model_name;

    private InferenceSession InitOnnxSession()
    {
        _stopwatch.Restart();
        var assembly = Assembly.GetExecutingAssembly();
        var stream = assembly.GetManifestResourceStream(modelLogicalName);
        var bs = new BinaryReader(stream);
        var modelContent = bs.ReadBytes((int)stream.Length);
        var onnxSession = new InferenceSession(modelContent);
        _stopwatch.Stop();
        _logger.Debug($"ONNX DL model loaded in {_stopwatch.Elapsed.TotalSeconds:N3} seconds");
        return onnxSession;
    }

    private static MagikaConfig GetMagikaConfig()
    {
        return new MagikaConfig();
    }


    private List<MagikaResult> GetResultsFromPaths(IReadOnlyList<string> paths)
    {
        /* Given a list of paths, returns a list of predictions. Each prediction
        is a dict with the relevant information, such as the file path, the
        output of the DL model, the output of the tool, and the associated
        metadata. The order of the predictions matches the order of the input
        paths. */

        _stopwatch.Restart();

        // We do a first pass on all files: we collect features for the files
        // that need to be analyzed with the DL model, and we already determine
        // the output for the remaining ones.

        Dictionary<string, MagikaResult> all_outputs = [];

        // We use a list and not the dict because that's what we need later on
        // for inference.
        List<(string, ModelFeatures)> all_features = [];

        _logger.Debug($"Processing input files and extracting features for {paths.Count} samples");
        foreach (string path in paths)
        {
            if (string.IsNullOrWhiteSpace(path)) continue;
            (MagikaResult? output, ModelFeatures? features) = this.GetResultOrFeaturesFromPath(path);
            if (output != null)
            {
                all_outputs[path] = output;
            }
            else
            {
                Assert.IsTrue(features != null);
                all_features.Add((path, features));
            }
            _stopwatch.Stop();
            _logger.Debug($"First pass and features extracted in {_stopwatch.Elapsed.TotalSeconds:N3} seconds");
        }

        // Get the outputs via DL for the files that need it.
        Dictionary<string, MagikaResult> outputs_with_dl = this.GetResultsFromFeatures(all_features);
        foreach (var kv in outputs_with_dl)
        {
            all_outputs[kv.Key] = kv.Value;
        }

        // Finally, we collect the predictions in a final list, sorted by the
        // initial paths list (and not by insertion order).
        List<MagikaResult> sorted_outputs = [];
        foreach (string path in paths)
        {
            sorted_outputs.Add(all_outputs[path]);
        }
        return sorted_outputs;
    }

    private MagikaResult GetResultFromPath(string path)
    {
        return this.GetResultsFromPaths([path])[0];
    }

    private MagikaResult GetResultFromBytes(byte[] bytes)
    {
        (MagikaResult? result, ModelFeatures? features) = this.GetResultOrFeaturesFromBytes(bytes);
        if (result != null)
        {
            return result;
        }
        Assert.IsTrue(features != null);
        return this.GetResultFromFeatures(features);
    }

    private ModelFeatures ExtractFeaturesFromPath
    (
        string file_path,
        int? beg_size = null,
        int? mid_size = null,
        int? end_size = null,
        int? padding_token = null,
        int? block_size = null
    )
    {
        /* Note: the way we extract features is somehow convoluted, the reason
        being that we need to reflect how we extracted features for training
        (during which we did not consider efficiency aspects).

        If it is the first time you look at this code, you may first want to
        read the code and comment of "_extract_features_from_bytes()", which is
        an alternative (but in essence equivalent) implementation. */

        beg_size ??= this._input_sizes["beg"];
        mid_size ??= this._input_sizes["mid"];
        end_size ??= this._input_sizes["end"];
        padding_token ??= this._padding_token;
        block_size ??= this._block_size;

        Assert.IsTrue(beg_size <= 512);
        Assert.IsTrue(mid_size <= 512);
        Assert.IsTrue(end_size <= 512);

        FileInfo fileInfo = new(file_path);
        long fileSize = fileInfo.Length;

        if (fileSize < 2 * block_size.Value + mid_size)
        {
            byte[] content = File.ReadAllBytes(file_path);
            return ExtractFeaturesFromBytes(content, beg_size.Value, mid_size.Value, end_size.Value, padding_token.Value);
        }
        else
        {
            // avoid reading the entire file
            using FileStream fs = File.OpenRead(file_path);
            byte[] begContent = [];
            int begTrimmedSize = 0;
            if (beg_size > 0)
            {
                begContent = new byte[block_size.Value];
                fs.Read(begContent, 0, block_size.Value);
                begContent = begContent.SkipWhile(IsSpace).ToArray();
                begTrimmedSize = block_size.Value - begContent.Length;
                if (begContent.Length < beg_size)
                {
                    // We need more bytes, let's read more
                    byte[] additionalBytes = new byte[block_size.Value];
                    fs.Read(additionalBytes, 0, block_size.Value);
                    begContent = begContent.Concat(additionalBytes).ToArray();
                }
            }

            byte[] endContent = [];
            int endTrimmedSize = 0;
            if (end_size > 0)
            {
                fs.Seek(-block_size.Value, SeekOrigin.End);
                endContent = new byte[block_size.Value];
                fs.Read(endContent, 0, block_size.Value);
                endContent = endContent.Reverse().SkipWhile(IsSpace).Reverse().ToArray();
                endTrimmedSize = block_size.Value - endContent.Length;
                if (endContent.Length < end_size)
                {
                    // Same as above
                    fs.Seek(-2 * block_size.Value, SeekOrigin.End);
                    byte[] additionalBytes = new byte[block_size.Value];
                    fs.Read(additionalBytes, 0, block_size.Value);
                    endContent = additionalBytes.Concat(endContent).ToArray();
                }
            }

            byte[] midFull = [];
            if (mid_size > 0)
            {
                var trimmed_file_size = fileSize - begTrimmedSize - endTrimmedSize;
                var midIdx = (begTrimmedSize + trimmed_file_size) / 2;
                var midLeftIdx = midIdx - mid_size.Value / 2;
                fs.Seek(midLeftIdx, SeekOrigin.Begin);
                midFull = new byte[mid_size.Value];
                fs.Read(midFull, 0, mid_size.Value);
            }

            List<int> begInts = GetBegIntsWithPadding(begContent, beg_size.Value, padding_token.Value);
            List<int> midInts = GetMidIntsWithPadding(midFull, mid_size.Value, padding_token.Value);
            List<int> endInts = GetEndIntsWithPadding(endContent, end_size.Value, padding_token.Value);

            return new ModelFeatures(begInts, midInts, endInts);
        }
    }

    private ModelFeatures ExtractFeaturesFromBytes
    (
        byte[] content,
        int? beg_size = null,
        int? mid_size = null,
        int? end_size = null,
        int? padding_token = null,
        int? block_size = null
    )
    {
        /*This implements the features extraction. The "from bytes" (this one)
        and the "from paths" are alternative, but equivalent implementations.
        Both these implementations aim at having a bounded time for features
        extraction, regardless of the size of the input content.

        Intuitively, the algorithm works as follows:
        - for "beg bytes": consider "content", strip at most "block_size"
        from the beginning, and take beg_size bytes. If you don't have enough
        remaining bytes, then suffix the existing bytes with padding.
        - for "end bytes": consider "content", strip at most "block_size"
        from the end, and take end_size bytes. If you don't have enough remaining
        bytes, then prefix the existing bytes with padding.
        - for "mid bytes": we want to extract features from the middle part of the
        content, centered in a way that we consider how many bytes we trimmed from
        the beginning and from the end. Again, if we don't have enough bytes, we use
        padding, this time on the left and the right of the bytes we have.

        The implementation for beg and end is quite simple, but the one for middle
        could be made easier. At the moment we leave it as is because we need to
        live with "how we extracted the features during training"... which was very
        easy to code, but did not consider "can we implement this
        in an efficient way for inference?" So for now we stick to it.
        */

        beg_size ??= this._input_sizes["beg"];
        mid_size ??= this._input_sizes["mid"];
        end_size ??= this._input_sizes["end"];
        padding_token ??= this._padding_token;
        block_size ??= this._block_size;

        Assert.IsTrue(beg_size <= 512);
        Assert.IsTrue(mid_size <= 512);
        Assert.IsTrue(end_size <= 512);

        Assert.IsTrue(beg_size == mid_size);
        Assert.IsTrue(beg_size == end_size);

        // If the content is big enough, the implementation of the above becomes
        // much simpler. Here, we check that we can safely strip a full
        // "block_size" from the beginning AND the end, and still have enough
        // bytes to extract a middle portion without checking for too many corner
        // cases.

        List<int> begInts = [];
        List<int> midInts = [];
        List<int> endInts = [];

        ReadOnlySpan<byte> contentSpan = content.AsSpan();
        if (content.Length >= (2 * block_size + mid_size))
        {
            // extract beg features
            var trimmedFirstBlock = contentSpan.Slice(0, block_size.Value).ToArray()
                .SkipWhile(IsSpace)
                .ToArray().AsSpan();
            var secondBlock = contentSpan.Slice(block_size.Value, block_size.Value);
            var begContent = ArrayConcat2(trimmedFirstBlock, secondBlock);
            begInts = Magika.GetBegIntsWithPadding(begContent, beg_size.Value, padding_token.Value);

            // extract end features
            var trimmedLastBlock = contentSpan.Slice(content.Length - block_size.Value, block_size.Value)
                .ToArray().Reverse().SkipWhile(IsSpace).Reverse()
                .ToArray().AsSpan();
            var secondToLastBlock = contentSpan.Slice(content.Length - 2 * block_size.Value, block_size.Value);
            var endContent = ArrayConcat2(secondToLastBlock, trimmedLastBlock);
            endInts = Magika.GetEndIntsWithPadding(endContent, end_size.Value, padding_token.Value);

            // extract mid features
            // we calculate mid_idx as the middle of the content we have not trimmed
            var trimmedBegBytesNum = block_size.Value - trimmedFirstBlock.Length;
            var trimmedEndBytesNum = block_size.Value - trimmedLastBlock.Length;
            var midIdx = trimmedBegBytesNum + (content.Length - trimmedBegBytesNum - trimmedEndBytesNum) / 2;
            var midLeftIdx = midIdx - mid_size.Value / 2;
            var midRightIdx = midLeftIdx + mid_size.Value;
            Assert.IsTrue(midLeftIdx >= 0 && midRightIdx < content.Length);
            var midContent = contentSpan.Slice(midLeftIdx, mid_size.Value).ToArray();
            midInts = Magika.GetMidIntsWithPadding(midContent, mid_size.Value, padding_token.Value);
        }
        else
        {
            // If the content is very small, we take this shortcut to avoid
            // checking for too many corner cases.
            var contentStripped = content.SkipWhile(IsSpace).Reverse().SkipWhile(IsSpace).Reverse().ToArray();
            begInts = Magika.GetBegIntsWithPadding(contentStripped, beg_size.Value, padding_token.Value);
            midInts = Magika.GetMidIntsWithPadding(contentStripped, mid_size.Value, padding_token.Value);
            endInts = Magika.GetEndIntsWithPadding(contentStripped, end_size.Value, padding_token.Value);
        }

        return new ModelFeatures(begInts, midInts, endInts);
    }

    private static List<int> GetBegIntsWithPadding(byte[] beg_content, int beg_size, int padding_token)
    {
        // We make sure to skip leading whitespaces as "beg" features
        byte[] begBytes = beg_content.SkipWhile(IsSpace).ToArray();
        if (beg_size <= begBytes.Length)
        {
            // We don't need so many bytes
            begBytes = begBytes.Take(beg_size).ToArray();
        }

        List<int> begInts = begBytes.Select(b => (int)b).ToList();

        if (begInts.Count < beg_size)
        {
            // We don't have enough ints, add padding
            begInts.AddRange(Enumerable.Repeat(padding_token, beg_size - begInts.Count));
        }

        Assert.IsTrue(begInts.Count == beg_size);

        return begInts;
    }

    private static List<int> GetMidIntsWithPadding(byte[] mid_content, int mid_size, int padding_token)
    {
        byte[] midBytes;
        if (mid_size <= mid_content.Length)
        {
            int midIdx = mid_content.Length / 2;
            int midLeftIdx = midIdx - mid_size / 2;
            int midRightIdx = midLeftIdx + mid_size;
            midBytes = mid_content.Skip(midLeftIdx).Take(mid_size).ToArray();
        }
        else
        {
            midBytes = (byte[])mid_content.Clone();
        }

        List<int> midInts = midBytes.Select(b => (int)b).ToList();

        if (midInts.Count < mid_size)
        {
            // We don't have enough ints, add padding
            int paddingSize = mid_size - midInts.Count;
            int paddingSizeLeft = paddingSize / 2;
            int paddingSizeRight = paddingSize / 2;
            if (paddingSize % 2 != 0)
            {
                paddingSizeRight += 1;
            }
            midInts = Enumerable.Repeat(padding_token, paddingSizeLeft)
                .Concat(midInts)
                .Concat(Enumerable.Repeat(padding_token, paddingSizeRight))
                .ToList();
        }

        Assert.IsTrue(midInts.Count == mid_size);

        return midInts;
    }

    private static List<int> GetEndIntsWithPadding(byte[] end_content, int end_size, int padding_token)
    {
        // We make sure to skip trailing whitespaces as "end" features
        byte[] endBytes = end_content.Reverse().SkipWhile(IsSpace).Reverse().ToArray();

        if (end_size <= endBytes.Length)
        {
            // We don't need so many bytes
            endBytes = endBytes.Skip(end_content.Length - end_size).ToArray();
        }

        List<int> endInts = endBytes.Select(b => (int)b).ToList();

        if (endInts.Count < end_size)
        {
            // We don't have enough ints, add padding
            endInts = Enumerable.Repeat(padding_token, end_size - endInts.Count).Concat(endInts).ToList();
        }

        Assert.IsTrue(endInts.Count == end_size);

        return endInts;
    }


    private List<(string, ModelOutput)> GetModelOutputsFromFeatures(List<(string, ModelFeatures)> allFeatures)
    {
        List<float[]> rawPreds = GetRawPredictions(allFeatures);
        int[] topPredsIdxs = new int[rawPreds.Count];
        float[] scores = new float[rawPreds.Count];
        for (int i = 0; i < rawPreds.Count; i++)
        {
            scores[i] = rawPreds[i].Max();
            topPredsIdxs[i] = Array.IndexOf(rawPreds[i], scores[i]);
        }
        string[] predsContentTypesLabels = new string[rawPreds.Count];
        for (int i = 0; i < rawPreds.Count; i++)
        {
            predsContentTypesLabels[i] = _target_labels_space_np[topPredsIdxs[i]];
        }

        List<(string, ModelOutput)> result = [];
        for (int i = 0; i < allFeatures.Count; i++)
        {
            string path = allFeatures[i].Item1;
            string ctLabel = predsContentTypesLabels[i];
            float score = scores[i];
            result.Add((path, new ModelOutput(ctLabel, score)));
        }

        return result;
    }

    private Dictionary<string, MagikaResult> GetResultsFromFeatures(List<(string, ModelFeatures)> allFeatures)
    {
        // We now do inference for those files that need it.

        Dictionary<string, MagikaResult> outputs = [];
        if (allFeatures.Count == 0)
        {
            // nothing to be done
            return outputs;
        }

        foreach (var (path, modelOutput) in this.GetModelOutputsFromFeatures(allFeatures))
        {
            // In additional to the content type label from the DL model, we
            // also allow for other logic to overwrite such result. For
            // debugging and information purposes, the JSON output stores
            // both the raw DL model output and the final output we return to
            // the user.

            var output_ct_label = this.GetOutputCtLabelFromDlResult(modelOutput.ct_label, modelOutput.score);
            outputs[path] = this.GetResultFromLabelsAndScore(
                path,
                dl_ct_label: modelOutput.ct_label,
                output_ct_label: output_ct_label,
                score: modelOutput.score
            );
        }

        return outputs;
    }

    private MagikaResult GetResultFromFeatures(ModelFeatures features, string? path = "")
    {
        if (string.IsNullOrEmpty(path))
        {
            path = "-";
        }
        var allFeatures = new List<(string, ModelFeatures)> { ("-", features) };
        var result_with_dl = GetResultsFromFeatures(allFeatures)["-"];
        return result_with_dl;
    }

    private string GetOutputCtLabelFromDlResult(string dl_ct_label, float score)
    {
        // Overwrite ct_label if specified in the config
        dl_ct_label = _model_output_overwrite_map.ContainsKey(dl_ct_label) ? _model_output_overwrite_map[dl_ct_label] : dl_ct_label;

        string outputCtLabel;
        if (prediction_mode == PredictionMode.BEST_GUESS)
        {
            // We take the model predictions, no matter what the score is.
            outputCtLabel = dl_ct_label;
        }
        else if (prediction_mode == PredictionMode.HIGH_CONFIDENCE && score >= _thresholds[dl_ct_label])
        {
            // The model score is higher than the per-content-type high-confidence threshold.
            outputCtLabel = dl_ct_label;
        }
        else if (prediction_mode == PredictionMode.MEDIUM_CONFIDENCE && score >= _medium_confidence_threshold)
        {
            // We take the model prediction only if the score is above a given relatively loose threshold.
            outputCtLabel = dl_ct_label;
        }
        else
        {
            // We are not in a condition to trust the model, we opt to return generic labels.
            // Note that here we use an implicit assumption that the model has, at the very least,
            // got the binary vs. text category right. This allows us to pick between unknown and txt
            // without the need to read or scan the file bytes once again.
            if (_ctm.GetOrRaise(dl_ct_label).is_text)
            {
                outputCtLabel = ContentType.GENERIC_TEXT;
            }
            else
            {
                outputCtLabel = ContentType.UNKNOWN;
            }
        }

        return outputCtLabel;
    }

    private MagikaResult GetResultFromLabelsAndScore(string path, string? dl_ct_label, float score, string output_ct_label)
    {
        float? dlScore = dl_ct_label == null ? null : score;
        float outputScore = score;

        // add group info
        string? dlGroup = dl_ct_label == null ? null : _ctm.GetGroup(dl_ct_label);
        string outputGroup = _ctm.GetGroup(output_ct_label);

        // add mime type info
        string? dlMimeType = dl_ct_label == null ? null : _ctm.GetMimeType(dl_ct_label);
        string outputMimeType = _ctm.GetMimeType(output_ct_label);

        // add magic
        string? dlMagic = dl_ct_label == null ? null : _ctm.GetMagic(dl_ct_label);
        string outputMagic = _ctm.GetMagic(output_ct_label);

        // add description
        string? dlDescription = dl_ct_label == null ? null : _ctm.GetDescription(dl_ct_label);
        string outputDescription = _ctm.GetDescription(output_ct_label);

        MagikaResult magikaResult = new(
            path: path,
            dl: new ModelOutputFields(
                ct_label: dl_ct_label,
                score: dlScore,
                group: dlGroup,
                mime_type: dlMimeType,
                magic: dlMagic,
                description: dlDescription
            ),
            output: new MagikaOutputFields(
                ct_label: output_ct_label,
                score: outputScore,
                group: outputGroup,
                mime_type: outputMimeType,
                magic: outputMagic,
                description: outputDescription
            )
        );

        return magikaResult;
    }

    private (MagikaResult?, ModelFeatures?) GetResultOrFeaturesFromPath(string path)
    {
        /*
        * Given a path, we return either a MagikaOutput or a MagikaFeatures.
        * 
        * There are some files and corner cases for which we do not need to use
        * deep learning to get the output; in these cases, we already return a
        * MagikaOutput object.
        * 
        * For some other files, we do need to use deep learning, in which case we
        * return a MagikaFeatures object. Note that for now we just collect the
        * features instead of already performing inference because we want to use
        * batching.
        */

        if (string.IsNullOrWhiteSpace(path))
        {
            throw new ArgumentException(nameof(path));
        }

        FileInfo file = new(path);
        DirectoryInfo directory = new(path);

        if (no_dereference
        && (file.Exists || directory.Exists)
        && File.GetAttributes(path).HasFlag(FileAttributes.ReparsePoint))
        {
            var result = GetResultFromLabelsAndScore(path, null, output_ct_label: ContentType.SYMLINK, score: 1.0f);
            var target = file.ResolveLinkTarget(true);
            if (target != null)
            {
                result.output.magic = result.output.magic.Replace("<path>", target.FullName);
                result.output.description = result.output.description.Replace("<path>", target.FullName);
            }
            return (result, null);
        }

        if (!file.Exists && !directory.Exists)
        {
            var result = GetResultFromLabelsAndScore(
                path,
                dl_ct_label: null,
                output_ct_label: ContentType.FILE_DOES_NOT_EXIST,
                score: 1.0f
            );
            return (result, null);
        }

        if (file.Exists)
        {
            if (!no_dereference && file.Attributes.HasFlag(FileAttributes.ReparsePoint))
            {
                file = (FileInfo)file.ResolveLinkTarget(true);
                path = file.FullName;
            }
            if (file.Length == 0)
            {
                var result = GetResultFromLabelsAndScore(
                    path,
                    dl_ct_label: null,
                    output_ct_label: ContentType.EMPTY,
                    score: 1.0f
                );
                return (result, null);
            }
            else if (!IsReadableFile(path))
            {
                var cts = GetCtsOfInAccessibleFile(path);
                var result = GetResultFromLabelsAndScore(
                    path,
                    dl_ct_label: null,
                    output_ct_label: cts,
                    score: 1.0f
                );
                return (result, null);
            }
            else if (file.Length <= this._min_file_size_for_dl)
            {
                var result = GetResultFromFirstBlockOfFile(path);
                return (result, null);
            }
            else
            {
                var fileFeatures = ExtractFeaturesFromPath(path);
                // Check whether we have enough bytes for a meaningful
                // detection, and not just padding.
                if (fileFeatures.beg[this._min_file_size_for_dl - 1] == this._padding_token)
                {
                    // If the n-th token is padding, then it means that,
                    // post-stripping, we do not have enough meaningful
                    // bytes.
                    var result = GetResultFromFirstBlockOfFile(path);
                    return (result, null);
                }
                else
                {
                    // We have enough bytes, scheduling this file for model
                    // prediction.
                    // features.append((path, file_features))
                    return (null, fileFeatures);
                }
            }
        }
        else if (directory.Exists)
        {
            var result = GetResultFromLabelsAndScore(
                path,
                dl_ct_label: null,
                output_ct_label: ContentType.DIRECTORY,
                score: 1.0f
            );
            return (result, null);
        }
        else
        {
            var unknownResult = GetResultFromLabelsAndScore(
                path,
                dl_ct_label: null,
                output_ct_label: ContentType.UNKNOWN,
                score: 1.0f
            );
            return (unknownResult, null);
        }

        throw new Exception("unreachable");
    }

    private (MagikaResult?, ModelFeatures?) GetResultOrFeaturesFromBytes(byte[] content)
    {
        if (content.Length == 0)
        {
            var result = GetResultFromLabelsAndScore(
                path: "-",
                dl_ct_label: null,
                output_ct_label: ContentType.EMPTY,
                score: 1.0f
            );
            return (result, null);
        }
        else if (content.Length <= this._min_file_size_for_dl)
        {
            var output = GetResultOfFewBytes(content);
            return (output, null);
        }
        else
        {
            var fileFeatures = ExtractFeaturesFromBytes(content);
            // Check whether we have enough bytes for a meaningful
            // detection, and not just padding.
            if (fileFeatures.beg[this._min_file_size_for_dl - 1] == this._padding_token)
            {
                // If the n-th token is padding, then it means that,
                // post-stripping, we do not have enough meaningful
                // bytes.
                var output = GetResultOfFewBytes(content);
                return (output, null);
            }
            else
            {
                // We have enough bytes, scheduling this file for model
                // prediction.
                // features.append((path, file_features))
                return (null, fileFeatures);
            }
        }

        throw new Exception("unreachable");
    }

    private MagikaResult GetResultFromFirstBlockOfFile(string path)
    {
        // We read at most "block_size" bytes
        byte[] content = new byte[this._block_size];
        using var fs = File.Open(path, FileMode.Open, FileAccess.Read);
        fs.Read(content, 0, this._block_size);
        return GetResultOfFewBytes(content, path);
    }

    private MagikaResult GetResultOfFewBytes(byte[] content, string path = "-")
    {
        Assert.IsTrue(content.Length <= 4 * this._block_size);
        var ctLabel = this.GetCtLabelOfFewBytes(content);
        return this.GetResultFromLabelsAndScore(path, dl_ct_label: null, output_ct_label: ctLabel, score: 1.0f);
    }

    private string GetCtLabelOfFewBytes(byte[] content)
    {
        var ctLabel = ContentType.GENERIC_TEXT;
        var enc = Encoding.GetEncoding("UTF-8", new EncoderExceptionFallback(), new DecoderExceptionFallback());
        try
        {
            string _ = enc.GetString(content);
        }
        catch (DecoderFallbackException)
        {
            ctLabel = ContentType.UNKNOWN;
        }
        return ctLabel;
    }

    private List<float[]> GetRawPredictions(List<(string, ModelFeatures)> features)
    {
        /*
        * Given a list of (path, features), return a (files_num, features_size)
        * matrix encoding the predictions.
        */

        var datasetFormat = this._model_config.dataset_format;
        // Assert.IsTrue(datasetFormat == "int-concat/one-hot");
        _stopwatch.Restart();

        List<int> XBytes = [];
        foreach (var (_, fs) in features)
        {
            List<int> sampleBytes = [];
            if (this._input_sizes["beg"] > 0)
            {
                sampleBytes.AddRange(fs.beg.Take(this._input_sizes["beg"]));
            }
            if (this._input_sizes["mid"] > 0)
            {
                sampleBytes.AddRange(fs.mid.Take(this._input_sizes["mid"]));
            }
            if (this._input_sizes["end"] > 0)
            {
                sampleBytes.AddRange(fs.end.Skip(Math.Max(0, fs.end.Count - this._input_sizes["end"])));
            }
            XBytes.AddRange(sampleBytes);
        }

        Memory<float> X = XBytes.Select(b => (float)b).ToArray().AsMemory();
        _logger.Debug($"DL input prepared in {_stopwatch.Elapsed.TotalSeconds:N3} seconds");

        _stopwatch.Restart();
        List<float[]> rawPredictionsList = [];
        int samplesNum = features.Count;

        int maxInternalBatchSize = 1000;
        int batchesNum = samplesNum / maxInternalBatchSize;
        if (samplesNum % maxInternalBatchSize != 0)
        {
            batchesNum += 1;
        }

        for (int batchIdx = 0; batchIdx < batchesNum; batchIdx++)
        {
            int startIdx = _input_column_size * batchIdx * maxInternalBatchSize;
            int endIdx = Math.Min((batchIdx + 1) * maxInternalBatchSize, samplesNum);
            int count = endIdx - startIdx;

            List<NamedOnnxValue> input = [];
            Tensor<float> dt = new DenseTensor<float>(X.Slice(startIdx, _input_column_size * count), [count, _input_column_size], false);
            input.Add(NamedOnnxValue.CreateFromTensor("bytes", dt));

            using var batchRawPredictions = this._onnx_session.Run(
                    outputNames: ["target_label"],
                    inputs: input
                );

            foreach (var rawPreds in batchRawPredictions[0].AsEnumerable<float>().Chunk(_output_column_size))
            {
                rawPredictionsList.Add(rawPreds);
            }
        }
        _stopwatch.Stop();
        _logger.Debug($"DL raw prediction in {_stopwatch.Elapsed.TotalSeconds:N3} seconds");
        return rawPredictionsList;
    }


    public void Dispose()
    {
        _onnx_session?.Dispose();
    }


    private static Type[] ArrayConcat2<Type>(ReadOnlySpan<Type> s1, ReadOnlySpan<Type> s2)
    {
        var array = new Type[s1.Length + s2.Length];
        s1.CopyTo(array);
        s2.CopyTo(array.AsSpan(s1.Length));
        return array;
    }

    private static bool IsSpace(byte b)
    {
        // We consider space, tab, newline, vertical tab, form feed, and carriage return as space.
        return b is 32 or 9 or 10 or 11 or 12 or 13;
    }

    private static bool IsReadableFile(string path)
    {
        var cts = GetCtsOfInAccessibleFile(path);
        return cts == ContentType.UNKNOWN;
    }

    private static string GetCtsOfInAccessibleFile(string path)
    {
        if (!File.Exists(path))
        {
            return ContentType.FILE_DOES_NOT_EXIST;
        }
        try
        {
            using var fs = File.Open(path, FileMode.Open, FileAccess.Read);
            return ContentType.UNKNOWN;
        }
        catch (UnauthorizedAccessException)
        {
            return ContentType.PERMISSION_ERROR;
        }
        catch
        {
            return ContentType.ERROR;
        }
    }

    private static void InitAssemblyResolve()
    {
#if !CoreCLR
        AppDomain.CurrentDomain.AssemblyResolve += new ResolveEventHandler((object sender, ResolveEventArgs args) =>
        {
            var requiredAsmName = new AssemblyName(args.Name);
            string possibleAsmPath = Path.Combine(_baseDir, $"{requiredAsmName.Name}.dll");

            AssemblyName bundledAsmName = null;
            try
            {
                bundledAsmName = AssemblyName.GetAssemblyName(possibleAsmPath);
            }
            catch
            {
                // If we don't bundle the assembly we're looking for, we don't have it so return nothing
                return null;
            }

            // Now make sure our version is greater
            if (bundledAsmName.Version < requiredAsmName.Version)
            {
                return null;
            }

            return Assembly.LoadFrom(possibleAsmPath);
        });
#endif
    }
}
