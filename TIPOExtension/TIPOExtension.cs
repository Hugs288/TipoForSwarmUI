using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using SwarmUI.Builtin_ComfyUIBackend;
using SwarmUI.Core;
using SwarmUI.Text2Image;
using SwarmUI.Utils;
using System;
using System.Collections.Generic;
using System.Linq;

namespace SwarmExtensions.TIPO
{
    public class TIPOExtension : Extension
    {
        public TIPOExtension()
        {
            ExtensionName = "TIPO Integration";
        }

        // Parameter Definitions
        public static T2IRegisteredParam<string> BanTags, TipoModel, Format, TagLength, NlLength, PromptType, Device;
        public static T2IRegisteredParam<bool> NoFormatting;
        public static T2IRegisteredParam<double> Temperature, TopP, MinP;
        public static T2IRegisteredParam<int> TopK;
        public static T2IRegisteredParam<long> TipoSeed;

        // Parameter Group
        public static T2IParamGroup TIPOParamGroup;

        // Static list for dynamic model population by the backend parser
        public static List<string> DynamicTipoModelList = ["(Requires ComfyUI Backend Connection)"];
        private static readonly object ModelListLock = new(); // Lock for thread safety

        public override void OnInit()
        {
            // Install
            InstallableFeatures.RegisterInstallableFeature(new("TIPO", "tipo_prompt_generation", "https://github.com/KohakuBlueleaf/z-tipo-extension", "KohakuBlueleaf", "This will install TIPO developed by KohakuBlueleaf.\nDo you wish to install?"));
            ScriptFiles.Add("assets/TIPO.js");
            // Map ComfyUI Node name to SwarmUI Feature ID for backend capability detection
            ComfyUIBackendExtension.NodeToFeatureMap["TIPO"] = "tipo_prompt_generation";

            // Define the Parameter Group
            TIPOParamGroup = new("TIPO Prompt Generation", Toggles: true, Open: false, IsAdvanced: false, OrderPriority: 50, Description: "Uses the TIPO node (if installed on the ComfyUI backend) to generate or modify prompts based on the main prompt text. Requires the 'TIPO' custom node from Kahsolt.");

            // Register Parameters
            PromptType = T2IParamTypes.Register<string>(new(Name: "[TIPO] Prompt Type", Description: "Treat main prompt as 'tags' or 'natural language'.", Default: "tags", GetValues: (_) => ["tags", "natural language"], Group: TIPOParamGroup, FeatureFlag: "tipo_prompt_generation", OrderPriority: 2));
            BanTags = T2IParamTypes.Register<string>(new(Name: "[TIPO] Banned Tags", Description: "Comma-separated list of tags to ban.", Default: "", Group: TIPOParamGroup, FeatureFlag: "tipo_prompt_generation", OrderPriority: 3, ViewType: ParamViewType.BIG));
            TipoModel = T2IParamTypes.Register<string>(new(Name: "[TIPO] TIPO Model", Description: "Select TIPO model (requires backend connection).", Default: "", GetValues: (_) => { lock (ModelListLock) { return DynamicTipoModelList.AsEnumerable().Reverse().ToList(); } }, Group: TIPOParamGroup, FeatureFlag: "tipo_prompt_generation", OrderPriority: 4));
            Format = T2IParamTypes.Register<string>(new(Name: "[TIPO] Format", Description: "TIPO output format string.", Default: "<|special|>,\n<|characters|>, <|copyrights|>,\n<|artist|>,\n\n<|general|>,\n\n<|extended|>.\n\n<|quality|>, <|meta|>, <|rating|>", Group: TIPOParamGroup, FeatureFlag: "tipo_prompt_generation", OrderPriority: 5, ViewType: ParamViewType.BIG, Toggleable: true));
            NoFormatting = T2IParamTypes.Register<bool>(new(Name: "[TIPO] No Formatting", Description: "Use unformatted TIPO output.", Default: "false", IgnoreIf: "false", Group: TIPOParamGroup, FeatureFlag: "tipo_prompt_generation", OrderPriority: 5.5));
            Temperature = T2IParamTypes.Register<double>(new(Name: "[TIPO] Temperature", Description: "TIPO sampling temperature.", Default: "0.5", Min: 0, Max: 2, Step: 0.01, Group: TIPOParamGroup, FeatureFlag: "tipo_prompt_generation", OrderPriority: 6, ViewType: ParamViewType.SLIDER));
            TopP = T2IParamTypes.Register<double>(new(Name: "[TIPO] Top P", Description: "TIPO sampling Top P.", Default: "0.95", Min: 0, Max: 1, Step: 0.01, Group: TIPOParamGroup, FeatureFlag: "tipo_prompt_generation", OrderPriority: 7, ViewType: ParamViewType.SLIDER));
            MinP = T2IParamTypes.Register<double>(new(Name: "[TIPO] Min P", Description: "TIPO sampling Min P.", Default: "0.05", Min: 0, Max: 1, Step: 0.01, Group: TIPOParamGroup, FeatureFlag: "tipo_prompt_generation", OrderPriority: 8, ViewType: ParamViewType.SLIDER));
            TopK = T2IParamTypes.Register<int>(new(Name: "[TIPO] Top K", Description: "TIPO sampling Top K.", Default: "80", Min: 0, Max: 200, Step: 1, Group: TIPOParamGroup, FeatureFlag: "tipo_prompt_generation", OrderPriority: 9, ViewType: ParamViewType.SLIDER));
            TagLength = T2IParamTypes.Register<string>(new(Name: "[TIPO] Tag Length", Description: "Target tag length.", Default: "long", GetValues: (_) => ["very_short", "short", "long", "very_long"], Group: TIPOParamGroup, FeatureFlag: "tipo_prompt_generation", OrderPriority: 10));
            NlLength = T2IParamTypes.Register<string>(new(Name: "[TIPO] NL Length", Description: "Target natural language length.", Default: "long", GetValues: (_) => ["very_short", "short", "long", "very_long"], Group: TIPOParamGroup, FeatureFlag: "tipo_prompt_generation", OrderPriority: 11));
            TipoSeed = T2IParamTypes.Register<long>(new(Name: "[TIPO] Seed", Description: "TIPO generation seed (-1 random).", Default: "-1", Min: -1, Max: long.MaxValue, Step: 1, Toggleable: true, Group: TIPOParamGroup, FeatureFlag: "tipo_prompt_generation", OrderPriority: 12, ViewType: ParamViewType.SEED, Clean: T2IParamTypes.Seed.Type.Clean));
            Device = T2IParamTypes.Register<string>(new(Name: "[TIPO] Device", Description: "Device override for TIPO.", Default: "cuda", GetValues: (_) => ["cuda", "cpu"], Group: TIPOParamGroup, FeatureFlag: "tipo_prompt_generation", OrderPriority: 13));

            // Add Parser for Dynamic Model List from ComfyUI Backend Info
            ComfyUIBackendExtension.RawObjectInfoParsers.Add(rawObjectInfo =>
            {
                if (rawObjectInfo?["TIPO"]?["input"]?["required"]?["tipo_model"] is JArray modelListToken
                    && modelListToken.Count > 0 && modelListToken[0] is JArray actualList)
                {
                    lock (ModelListLock)
                    {
                        var newModelList = actualList.Select(t => t.ToString()).OrderBy(m => m).ToList();
                        // Only update if the list content has actually changed
                        if (!newModelList.SequenceEqual(DynamicTipoModelList))
                        {
                            DynamicTipoModelList = newModelList.Any() ? newModelList : ["(No TIPO models found on backend)"];
                        }
                    }
                }
            });

            WorkflowGenerator.AddStep(g =>
            {
                bool isTipoGroupActive = T2IParamTypes.Types.Values.Any(p => p.Group == TIPOParamGroup && g.UserInput.ValuesInput.ContainsKey(p.ID));

                if (isTipoGroupActive)
                {
                    if (!g.Features.Contains("tipo_prompt_generation"))
                    {
                        throw new SwarmUserErrorException("TIPO parameters were provided, but the backend does not have the TIPO custom node installed or recognized.");
                    }

                    string mainPromptText = g.UserInput.Get(T2IParamTypes.Prompt) ?? "";
                    string promptTypeText = g.UserInput.Get(PromptType, "tags");
                    bool useUnformatted = g.UserInput.Get(NoFormatting);
                    string deviceSelection = g.UserInput.Get(Device);
                    long tipoSeedValue = g.UserInput.Get(TipoSeed);

                    JObject tipoInputs = new()
                    {
                        ["tags"] = (promptTypeText == "tags") ? mainPromptText : "",
                        ["nl_prompt"] = (promptTypeText == "natural language") ? mainPromptText : "",
                        ["ban_tags"] = g.UserInput.Get(BanTags),
                        ["tipo_model"] = g.UserInput.Get(TipoModel),
                        ["format"] = g.UserInput.Get(Format),
                        ["width"] = g.UserInput.Get(T2IParamTypes.Width),
                        ["height"] = g.UserInput.Get(T2IParamTypes.Height),
                        ["temperature"] = g.UserInput.Get(Temperature),
                        ["top_p"] = g.UserInput.Get(TopP),
                        ["min_p"] = g.UserInput.Get(MinP),
                        ["top_k"] = g.UserInput.Get(TopK),
                        ["tag_length"] = g.UserInput.Get(TagLength),
                        ["nl_length"] = g.UserInput.Get(NlLength),
                        ["seed"] = tipoSeedValue,
                        ["device"] = deviceSelection
                    };
                    string tipoNodeId = g.CreateNode("TIPO", tipoInputs, g.GetStableDynamicID(100, 0));
                    int tipoOutputIndex = useUnformatted ? 2 : 0; // 0 = formatted, 2 = unformatted
                    JArray tipoOutputLink = new JArray { tipoNodeId, tipoOutputIndex };

                    string targetEncoderId = g.FinalPrompt?[0]?.ToString() ?? "6"; // Try state, fallback default

                    if (g.Workflow.TryGetValue(targetEncoderId, out JToken targetEncoderToken)
                        && targetEncoderToken is JObject targetEncoderNode
                        && targetEncoderNode["inputs"] is JObject encoderInputs)
                    {
                        if (targetEncoderNode["class_type"]?.ToString().Contains("CLIPTextEncode") ?? false)
                        {
                            string inputName = encoderInputs.ContainsKey("text_g") ? "text_g" : "text";
                            if (encoderInputs.ContainsKey(inputName))
                            {
                                if (inputName == "text_g" && encoderInputs.ContainsKey("text_l")) { encoderInputs["text_l"] = tipoOutputLink; }
                                encoderInputs[inputName] = tipoOutputLink;
                                g.FinalPrompt = new JArray { targetEncoderId, g.FinalPrompt?[1] ?? 0 };
                            }
                        }
                    }
                }
            }, -0.1); // Run after core nodes are likely created
        }
    }
}