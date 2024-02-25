/*
 * This code is a modified version of the original code, which is licensed under the Apache 2.0 License.
 * The original code can be found at: https://github.com/google/magika/tree/v0.5.0
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

using System.Reflection;

namespace magika;

public class ContentType
{
    // the tool returned unknown, '',  null, or similar
    public static readonly string UNKNOWN = "unknown";
    public static readonly string UNKNOWN_MIME_TYPE = "application/unknown";
    public static readonly string UNKNOWN_CONTENT_TYPE_GROUP = "unknown";
    public static readonly string UNKNOWN_MAGIC = "Unknown";
    public static readonly string UNKNOWN_DESCRIPTION = "Unknown type";

    // the tool returned an output that we currently do not map to our content types
    public static readonly string UNSUPPORTED = "unsupported";

    // the tool exited with returncode != 0
    public static readonly string ERROR = "error";

    // there is no result for this tool
    public static readonly string MISSING = "missing";

    // the file is empty (or just \x00, spaces, etc.)
    public static readonly string EMPTY = "empty";

    // the output of the tool is gibberish / meaningless type
    public static readonly string CORRUPTED = "corrupted";

    // the tool did not return in time
    public static readonly string TIMEOUT = "timeout";

    // the mapping functions returned a type we don't recognized, and we flag it
    // as NOT VALID
    public static readonly string NOT_VALID = "not_valid";

    // Used when a file path does not exist
    public static readonly string FILE_DOES_NOT_EXIST = "file_does_not_exist";

    // Used when a file path exists, but there are permission issues, e.g., can't
    // read file
    public static readonly string PERMISSION_ERROR = "permission_error";

    // more special labels
    public static readonly string DIRECTORY = "directory";
    public static readonly string SYMLINK = "symlink";

    public static readonly string GENERIC_TEXT = "txt";

    public string name { get; init; }
    public List<string> extensions { get; init; }
    public string? mime_type { get; init; }
    public string? group { get; init; }
    public string? magic { get; init; }
    public string? description { get; init; }
    public string? vt_type { get; init; }
    public List<string> datasets { get; init; }
    public string? parent { get; init; }
    public List<string> tags { get; init; }
    public string? model_target_label { get; init; }
    public string? target_label { get; init; }
    public List<string> correct_labels { get; init; }
    public bool in_scope_for_output_content_type { get; init; }

    public ContentType(
        string name,
        List<string>? extensions = null,
        string? mime_type = null,
        string? group = null,
        string? magic = null,
        string? description = null,
        string? vt_type = null,
        List<string>? datasets = null,
        string? parent = null,
        List<string>? tags = null,
        string? model_target_label = null,
        string? target_label = null,
        List<string>? correct_labels = null,
        bool in_scope_for_output_content_type = false
    )
    {
        this.name = name;
        this.extensions = extensions ?? [];
        this.mime_type = mime_type;
        this.group = group;
        this.magic = magic;
        this.description = description;
        this.vt_type = vt_type;
        this.datasets = datasets ?? [];
        this.parent = parent;
        this.tags = tags ?? [];
        this.model_target_label = model_target_label;
        this.target_label = target_label;
        this.correct_labels = correct_labels ?? [];
        this.in_scope_for_output_content_type = in_scope_for_output_content_type;

        // add automatic tags based on dataset
        if (datasets != null)
        {
            foreach (var dataset in datasets)
            {
                this.tags.Add($"dataset:{dataset}");
            }
        }
        if (!string.IsNullOrEmpty(model_target_label))
        {
            this.tags.Add($"model_target_label:{model_target_label}");
        }
        if (!string.IsNullOrEmpty(target_label))
        {
            this.tags.Add($"target_label:{target_label}");
        }
        if (correct_labels != null)
        {
            foreach (var cl in correct_labels)
            {
                this.tags.Add($"correct_label:{cl}");
            }
        }
    }

    public bool is_text => tags.Contains("text");

    public bool in_scope_for_training
    {
        get
        {
            if (datasets == null || datasets.Count == 0)
            {
                return false;
            }
            if (model_target_label == null)
            {
                return false;
            }
            if (target_label == null)
            {
                return false;
            }
            if (correct_labels == null || correct_labels.Count == 0)
            {
                return false;
            }
            return true;
        }
    }

    public Dictionary<string, object?> ToDictionary()
    {
        return new Dictionary<string, object?>
        {
            { "name", name },
            { "extensions", extensions },
            { "mime_type", mime_type },
            { "group", group },
            { "magic", magic },
            { "description", description },
            { "vt_type", vt_type },
            { "datasets", datasets },
            { "parent", parent },
            { "tags", tags },
            { "model_target_label", model_target_label },
            { "target_label", target_label },
            { "correct_labels", correct_labels },
            { "in_scope_for_output_content_type", in_scope_for_output_content_type },
            { "in_scope_for_training", in_scope_for_training }
        };
    }

    public static ContentType FromDictionary(Dictionary<string, object> info_d, bool add_automatic_tags = true)
    {
        var infoDCopy = new Dictionary<string, object>(info_d);
        infoDCopy.Remove("in_scope_for_training");

        var ct = new ContentType(
            (string)infoDCopy["name"],
            (List<string>)infoDCopy["extensions"],
            (string)infoDCopy["mime_type"],
            (string)infoDCopy["group"],
            (string)infoDCopy["magic"],
            (string)infoDCopy["description"],
            (string)infoDCopy["vt_type"],
            (List<string>)infoDCopy["datasets"],
            (string)infoDCopy["parent"],
            (List<string>)infoDCopy["tags"],
            (string)infoDCopy["model_target_label"],
            (string)infoDCopy["target_label"],
            (List<string>)infoDCopy["correct_labels"],
            (bool)infoDCopy["in_scope_for_output_content_type"]
        );

        return ct;
    }

    public override string ToString()
    {
        return $"<{name}>";
    }
}

public class ContentTypesManager
{
    // private static readonly string _baseDir = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);

    internal static List<string> SPECIAL_CONTENT_TYPES =
    [
        ContentType.UNKNOWN,
        ContentType.UNSUPPORTED,
        ContentType.ERROR,
        ContentType.MISSING,
        ContentType.EMPTY,
        ContentType.CORRUPTED,
        ContentType.NOT_VALID,
        ContentType.PERMISSION_ERROR,
        ContentType.GENERIC_TEXT,
    ];

    internal static List<string> SUPPORTED_TARGET_LABELS_SPEC =
    [
        "content-type",
        "model-target-label",
        "target-label",
    ];

    private Dictionary<string, ContentType?> cts;

    // tag to content type map
    private Dictionary<string, List<ContentType>> tag2cts;

    // map from extension to content types
    private Dictionary<string, List<ContentType>> ext2cts;

    public ContentTypesManager(bool add_automatic_tags = true)
    {
        this.cts = [];
        this.tag2cts = [];
        this.ext2cts = [];
        this.LoadContentTypesInfo();
    }

    private void LoadContentTypesInfo()
    {
        // var config = new ConfigurationBuilder()
        //    .AddJsonFile(Path.Combine(_baseDir, "config\\content_types_config.json"))
        //    .Build();

        this.cts = new ContentTypesConfig().allContentTypes;
        foreach (var ct in this.cts.Values)
        {
            foreach (var tag in ct.tags)
            {
                if (!this.tag2cts.ContainsKey(tag))
                {
                    this.tag2cts[tag] = [];
                }
                this.tag2cts[tag].Add(ct);
            }
            foreach (var ext in ct.extensions)
            {
                if (!this.ext2cts.ContainsKey(ext))
                {
                    this.ext2cts[ext] = [];
                }
                this.ext2cts[ext].Add(ct);
            }
        }
    }

    internal ContentType? Get(string content_type_name)
    {
        if (this.cts.ContainsKey(content_type_name))
        {
            return this.cts[content_type_name];
        }
        else
        {
            return null;
        }
    }

    internal ContentType GetOrRaise(string? content_type_name)
    {
        if (string.IsNullOrEmpty(content_type_name))
        {
            throw new Exception("Input content_type_name is None");
        }
        var ct = this.Get(content_type_name);
        if (ct == null)
        {
            throw new Exception($"Could not get a ContentType for \"{content_type_name}\"");
        }
        return ct;
    }

    internal string GetMimeType(string content_type_name, string? Default = null)
    {
        var ct = this.Get(content_type_name);
        if (ct == null)
        {
            return Default ?? ContentType.UNKNOWN_MIME_TYPE;
        }
        return ct.mime_type ?? Default ?? ContentType.UNKNOWN_MIME_TYPE;
    }

    internal string GetGroup(string content_type_name, string? Default = null)
    {
        var ct = this.Get(content_type_name);
        if (ct == null)
        {
            return Default ?? ContentType.UNKNOWN_CONTENT_TYPE_GROUP;
        }
        return ct.group ?? Default ?? ContentType.UNKNOWN_CONTENT_TYPE_GROUP;
    }

    internal string GetMagic(string content_type_name, string? Default = null, bool fallback_to_label = true)
    {
        var ct = this.Get(content_type_name);
        if (ct == null || ct.magic == null)
        {
            if (fallback_to_label)
            {
                return content_type_name;
            }
            else
            {
                return Default ?? ContentType.UNKNOWN_MAGIC;
            }
        }
        return ct.magic;
    }

    internal string GetDescription(string content_type_name, string? Default = null, bool fallback_to_label = true)
    {
        var ct = this.Get(content_type_name);
        if (ct == null || ct.description == null)
        {
            if (fallback_to_label)
            {
                return content_type_name;
            }
            else
            {
                return Default ?? ContentType.UNKNOWN_DESCRIPTION;
            }
        }
        return ct.description;
    }

    private List<ContentType> GetCtsByExt(string ext)
    {
        return this.ext2cts.ContainsKey(ext) ? this.ext2cts[ext] : [];
    }

    private List<ContentType> GetCtsByExtOrRaise(string ext)
    {
        var cts = this.GetCtsByExt(ext);
        if (cts.Count == 0)
        {
            throw new Exception($"Could not find ContentType for extension \"{ext}\"");
        }
        return cts;
    }

    private List<string> GetValidTags(bool only_explicit = true)
    {
        List<string> allTags;
        if (only_explicit)
        {
            allTags = tag2cts.Keys
                .Where(x => !x.Split(':')[0].EndsWith("_label") && !x.StartsWith("dataset"))
                .OrderBy(x => x)
                .ToList();
        }
        else
        {
            allTags = tag2cts.Keys.OrderBy(x => x).ToList();
        }
        return allTags;
    }

    private bool IsValidCtLabel(string label)
    {
        if (this.Get(label) != null)
        {
            return true;
        }
        if (SPECIAL_CONTENT_TYPES.Contains(label))
        {
            return true;
        }
        return false;
    }

    private bool IsValidTag(string tag)
    {
        return this.tag2cts.ContainsKey(tag);
    }

    private List<ContentType> Select(string? query = null, bool must_be_in_scope_for_training = true)
    {
        List<string> ctNames = this.SelectNames(query, must_be_in_scope_for_training);
        // we know these are valid content types
        return ctNames.Select(this.GetOrRaise).ToList();
    }

    private List<string> SelectNames(string? query = null, bool must_be_in_scope_for_training = true)
    {
        HashSet<string> ctNamesSet = [];
        if (string.IsNullOrEmpty(query))
        {
            // select them all, honoring must_be_in_scope_for_training
            foreach (var ct in this.cts.Values)
            {
                if (must_be_in_scope_for_training && !ct.in_scope_for_training)
                {
                    continue;
                }
                ctNamesSet.Add(ct.name);
            }
        }
        else
        {
            // consider each element of the query in sequence and add/remove
            // content types as appropriate (also honoring
            // must_be_in_scope_for_training)
            string[] entries = query.Split(',');
            foreach (var entry in entries)
            {
                if (entry is "*" or "all")
                {
                    // we know we get list of strings because we set only_names=True
                    ctNamesSet.UnionWith(this.SelectNames(null, must_be_in_scope_for_training));
                }
                else if (entry.StartsWith("tag:"))
                {
                    string tag = entry.Substring(4);
                    if (!this.IsValidTag(tag))
                    {
                        Console.WriteLine($"ERROR: \"{tag}\" is not a valid tag. Valid tags: {string.Join(", ", this.tag2cts.Keys.OrderBy(x => x))}.");
                        Environment.Exit(1);
                    }
                    foreach (var ct in this.tag2cts[tag])
                    {
                        if (must_be_in_scope_for_training && !ct.in_scope_for_training)
                        {
                            continue;
                        }
                        ctNamesSet.Add(ct.name);
                    }
                }
                else if (entry.StartsWith("-tag:"))
                {
                    string tag = entry.Substring(5);
                    Assert.IsTrue(this.IsValidTag(tag));
                    foreach (var ct in this.tag2cts[tag])
                    {
                        // no need to check for must_be_in_scope_for_training when removing
                        ctNamesSet.Remove(ct.name);
                    }
                }
                else if (entry.StartsWith("-"))
                {
                    string label = entry.Substring(1);
                    Assert.IsTrue(this.IsValidCtLabel(label));
                    // no need to check for must_be_in_scope_for_training when removing
                    ctNamesSet.Remove(label);
                }
                else
                {
                    Assert.IsTrue(this.IsValidCtLabel(entry));
                    // this ct was manually specified, if it does not honor
                    // must_be_in_scope_for_training, that's a problem.
                    if (must_be_in_scope_for_training)
                    {
                        ContentType? candidateCt = this.Get(entry);
                        Assert.IsTrue(candidateCt != null);
                        Assert.IsTrue(candidateCt.in_scope_for_training);
                    }
                    ctNamesSet.Add(entry);
                }
            }
        }
        List<string> ctNames = ctNamesSet.OrderBy(x => x).ToList();
        return ctNames;
    }

    public List<string> GetContentTypesSpace()
    {
        /* Returns the full list of possible content types, including out of
        scope and special types. Returns only the names. */

        // We know that we get content type names (str), and not a list of
        // ContentType
        HashSet<string> output = new(this.SelectNames(must_be_in_scope_for_training: false));
        output.UnionWith(SPECIAL_CONTENT_TYPES);
        return output.OrderBy(x => x).ToList();
    }

    public List<ContentType> GetOutputContentTypes()
    {
        /* Return a sorted list of ContentType objects representing valid output
        content types. */
        return this.Select(must_be_in_scope_for_training: false)
            .Where(ct => ct.in_scope_for_output_content_type && !string.IsNullOrEmpty(ct.target_label))
            .Select(ct => GetOrRaise(ct.target_label))
            .Distinct()
            .OrderBy(ct => ct.name)
            .ToList();
    }

    public List<string> GetOutputContentTypesNames()
    {
        /* Return a sorted list of ContentType names representing valid output
        content types. */
        return this.GetOutputContentTypes().Select(ct => ct.name).ToList();
    }

    public List<string> GetInvalidLabels(IEnumerable<string> labels)
    {
        HashSet<string> notValidLabels = [];
        foreach (string label in labels.Distinct())
        {
            if (!IsValidCtLabel(label))
            {
                notValidLabels.Add(label);
            }
        }
        return notValidLabels.OrderBy(label => label).ToList();
    }
}
