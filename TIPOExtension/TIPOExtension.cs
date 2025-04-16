using Newtonsoft.Json.Linq;
using SwarmUI.Builtin_ComfyUIBackend; // Needed for ComfyUI integration specifics
using SwarmUI.Core;
using SwarmUI.Text2Image;
using SwarmUI.Utils;
using System.Collections.Generic; // Required for List
using System.Linq; // Required for Linq operations like Select, OrderBy
using System; // Required for InvalidOperationException
using Newtonsoft.Json; // Required for Formatting enum

namespace SwarmExtensions.TIPO
{
    public class TIPOExtension : Extension
    {
        public TIPOExtension()
        {
            ExtensionName = "TIPO Integration";
            // PopulateMetadata will fill others if git repo exists
        }

        // Parameter Definitions
        public static T2IRegisteredParam<string> BanTags, TipoModel, Format, TagLength, NlLength, PromptType, Device; // Added Device
        public static T2IRegisteredParam<bool> NoFormatting; // Added NoFormatting
        public static T2IRegisteredParam<double> Temperature, TopP, MinP;
        public static T2IRegisteredParam<int> TopK;
        public static T2IRegisteredParam<long> TipoSeed;

        // Parameter Group
        public static T2IParamGroup TIPOParamGroup;

        // Static list for dynamic model population
        public static List<string> DynamicTipoModelList = ["(Requires ComfyUI Backend Connection)"];

        public override void OnInit()
        {
            Logs.Init("Loading TIPO Integration Extension...");

            // Map ComfyUI Node name to SwarmUI Feature ID
            ComfyUIBackendExtension.NodeToFeatureMap["TIPO"] = "tipo_prompt_generation";
            ComfyUIBackendExtension.FeaturesSupported.Add("tipo_prompt_generation");
            ComfyUIBackendExtension.FeaturesDiscardIfNotFound.Add("tipo_prompt_generation");

            // Define the Parameter Group
            TIPOParamGroup = new("TIPO Prompt Generation", Toggles: true, Open: false, IsAdvanced: true, OrderPriority: 50, Description: "Uses the TIPO node (if installed on the ComfyUI backend) to generate or modify prompts based on the main prompt text (interpreted as tags or natural language). Requires the 'TIPO' custom node from Kahsolt.");

            // Register Parameters
            PromptType = T2IParamTypes.Register<string>(new(Name: "[TIPO] Prompt Type", Description: "Select whether the main prompt text should be treated as 'tags' or 'natural language' by TIPO.", Default: "tags", GetValues: (_) => ["tags", "natural language"], Group: TIPOParamGroup, FeatureFlag: "tipo_prompt_generation", OrderPriority: 2));
            BanTags = T2IParamTypes.Register<string>(new(Name: "[TIPO] Banned Tags", Description: "Comma-separated list of tags to ban during TIPO generation.", Default: "", Group: TIPOParamGroup, FeatureFlag: "tipo_prompt_generation", OrderPriority: 3, ViewType: ParamViewType.BIG));
            TipoModel = T2IParamTypes.Register<string>(new(Name: "[TIPO] TIPO Model", Description: "Select the TIPO model to use (requires backend connection).", Default: "", GetValues: (_) => DynamicTipoModelList, Group: TIPOParamGroup, FeatureFlag: "tipo_prompt_generation", OrderPriority: 4));
            Format = T2IParamTypes.Register<string>(new(Name: "[TIPO] Format", Description: "The format string for TIPO output.", Default: "<|special|>,\n<|characters|>, <|copyrights|>,\n<|artist|>,\n\n<|general|>,\n\n<|extended|>.\n\n<|quality|>, <|meta|>, <|rating|>", Group: TIPOParamGroup, FeatureFlag: "tipo_prompt_generation", OrderPriority: 5, ViewType: ParamViewType.BIG, Toggleable: true)); // Made format toggleable

            // Added NoFormatting Checkbox
            NoFormatting = T2IParamTypes.Register<bool>(new(Name: "[TIPO] No Formatting", Description: "If checked, use the 'unformatted_prompt' output from TIPO instead of the formatted 'prompt' output.", Default: "false", IgnoreIf: "false", Group: TIPOParamGroup, FeatureFlag: "tipo_prompt_generation", OrderPriority: 5.5)); // Position after Format

            Temperature = T2IParamTypes.Register<double>(new(Name: "[TIPO] Temperature", Description: "Sampling temperature for TIPO generation.", Default: "0.5", Min: 0, Max: 2, Step: 0.01, Group: TIPOParamGroup, FeatureFlag: "tipo_prompt_generation", OrderPriority: 6, ViewType: ParamViewType.SLIDER));
            TopP = T2IParamTypes.Register<double>(new(Name: "[TIPO] Top P", Description: "Sampling Top P for TIPO generation.", Default: "0.95", Min: 0, Max: 1, Step: 0.01, Group: TIPOParamGroup, FeatureFlag: "tipo_prompt_generation", OrderPriority: 7, ViewType: ParamViewType.SLIDER));
            MinP = T2IParamTypes.Register<double>(new(Name: "[TIPO] Min P", Description: "Sampling Min P for TIPO generation.", Default: "0.05", Min: 0, Max: 1, Step: 0.01, Group: TIPOParamGroup, FeatureFlag: "tipo_prompt_generation", OrderPriority: 8, ViewType: ParamViewType.SLIDER));
            TopK = T2IParamTypes.Register<int>(new(Name: "[TIPO] Top K", Description: "Sampling Top K for TIPO generation.", Default: "80", Min: 0, Max: 200, Step: 1, Group: TIPOParamGroup, FeatureFlag: "tipo_prompt_generation", OrderPriority: 9, ViewType: ParamViewType.SLIDER));
            TagLength = T2IParamTypes.Register<string>(new(Name: "[TIPO] Tag Length", Description: "Target tag length.", Default: "long", GetValues: (_) => ["very_short", "short", "long", "very_long"], Group: TIPOParamGroup, FeatureFlag: "tipo_prompt_generation", OrderPriority: 10));
            NlLength = T2IParamTypes.Register<string>(new(Name: "[TIPO] NL Length", Description: "Target natural language length.", Default: "long", GetValues: (_) => ["very_short", "short", "long", "very_long"], Group: TIPOParamGroup, FeatureFlag: "tipo_prompt_generation", OrderPriority: 11));
            TipoSeed = T2IParamTypes.Register<long>(new(Name: "[TIPO] Seed", Description: "Seed for the TIPO prompt generation. -1 means random.", Default: "-1", Min: -1, Max: long.MaxValue, Step: 1, Toggleable: true, Group: TIPOParamGroup, FeatureFlag: "tipo_prompt_generation", OrderPriority: 12, ViewType: ParamViewType.SEED, Clean: T2IParamTypes.Seed.Type.Clean));

            // Added Device Listbox
            Device = T2IParamTypes.Register<string>(new(Name: "[TIPO] Device", Description: "Device to run TIPO inference on (usually handled by ComfyUI, but can override).", Default: "cuda", GetValues: (_) => ["cuda", "cpu"], Group: TIPOParamGroup, FeatureFlag: "tipo_prompt_generation", OrderPriority: 13)); // Place last


            // Add Parser for Dynamic Model List
             ComfyUIBackendExtension.RawObjectInfoParsers.Add(rawObjectInfo => {
                try {
                    if (rawObjectInfo.TryGetValue("TIPO", out JToken tipoNodeData)
                        && tipoNodeData is JObject tipoObj
                        && tipoObj.TryGetValue("input", out JToken inputData)
                        && inputData is JObject inputObj
                        && inputObj.TryGetValue("required", out JToken requiredData)
                        && requiredData is JObject requiredObj
                        && requiredObj.TryGetValue("tipo_model", out JToken modelListToken)
                        && modelListToken is JArray modelListArray
                        && modelListArray.HasValues && modelListArray[0] is JArray actualList)
                    {
                        lock (DynamicTipoModelList)
                        {
                             var newModelList = actualList.Select(t => t.ToString()).Order().ToList();
                             if (!newModelList.SequenceEqual(DynamicTipoModelList))
                             {
                                Logs.Info($"Updating TIPO Model List from backend: {newModelList.Count} models found.");
                                DynamicTipoModelList = newModelList;
                                if (!DynamicTipoModelList.Any()) {
                                    DynamicTipoModelList.Add("(No TIPO models found on backend)");
                                }
                             }
                        }
                    }
                }
                catch (Exception ex) {
                    Logs.Error($"Error processing TIPO object_info for model list: {ex}");
                }
            });


            // Modify Workflow Generation
            WorkflowGenerator.AddStep(g =>
            {
                bool isTipoGroupActive = T2IParamTypes.Types.Values
                    .Where(p => p.Group == TIPOParamGroup)
                    .Any(p => g.UserInput.ValuesInput.ContainsKey(p.ID));

                if (isTipoGroupActive)
                {
                    // --- Feature Check ---
                    if (!g.Features.Contains("tipo_prompt_generation")) {
                        throw new SwarmUserErrorException("TIPO enabled but backend lacks feature.");
                    }
                    Logs.Debug($"TIPO Step running at priority -0.1.");


                    // --- Get Inputs ---
                    string mainPromptText = g.UserInput.Get(T2IParamTypes.Prompt) ?? "";
                    string promptTypeText = g.UserInput.Get(PromptType, "tags");
                    bool useUnformatted = g.UserInput.Get(NoFormatting); // Get NoFormatting value
                    string deviceSelection = g.UserInput.Get(Device); // Get Device value
                    long tipoSeedValue = g.UserInput.Get(TipoSeed);
                    int width = g.UserInput.Get(T2IParamTypes.Width);
                    int height = g.UserInput.Get(T2IParamTypes.Height);

                    // --- Create TIPO Node ---
                    JObject tipoInputs = new JObject {
                         ["tags"] = (promptTypeText == "tags") ? mainPromptText : "",
                         ["nl_prompt"] = (promptTypeText == "natural language") ? mainPromptText : "",
                         ["ban_tags"] = g.UserInput.Get(BanTags),
                         ["tipo_model"] = g.UserInput.Get(TipoModel),
                         ["format"] = g.UserInput.Get(Format), // Keep format input even if using unformatted output
                         ["width"] = width,
                         ["height"] = height,
                         ["temperature"] = g.UserInput.Get(Temperature),
                         ["top_p"] = g.UserInput.Get(TopP),
                         ["min_p"] = g.UserInput.Get(MinP),
                         ["top_k"] = g.UserInput.Get(TopK),
                         ["tag_length"] = g.UserInput.Get(TagLength),
                         ["nl_length"] = g.UserInput.Get(NlLength),
                         ["seed"] = tipoSeedValue,
                         ["device"] = deviceSelection // Add device input
                     };
                    string tipoNodeId = g.CreateNode("TIPO", tipoInputs, g.GetStableDynamicID(100, 0));

                    // --- Select Correct TIPO Output ---
                    // Output 0: prompt (formatted)
                    // Output 2: unformatted_prompt
                    int tipoOutputIndex = useUnformatted ? 2 : 0;
                    JArray tipoOutputLink = new JArray { tipoNodeId, tipoOutputIndex };
                    Logs.Debug($"TIPO: Using output index {tipoOutputIndex} (NoFormatting={useUnformatted}).");


                    // --- Find and Modify Standard Positive CLIPTextEncode ---
                    string targetEncoderId = null;
                    if (g.FinalPrompt != null && g.FinalPrompt.Count == 2) {
                        targetEncoderId = g.FinalPrompt[0].ToString();
                    }
                    if (targetEncoderId == null || !g.Workflow.ContainsKey(targetEncoderId)) {
                        targetEncoderId = "6"; // Fallback
                    }

                    if (g.Workflow.TryGetValue(targetEncoderId, out JToken targetEncoderToken)
                        && targetEncoderToken is JObject targetEncoderNode
                        && targetEncoderNode.TryGetValue("inputs", out JToken encoderInputsToken)
                        && encoderInputsToken is JObject encoderInputs)
                    {
                         if (targetEncoderNode.TryGetValue("class_type", out var classType) && classType.ToString().Contains("CLIPTextEncode"))
                         {
                            string inputName = encoderInputs.ContainsKey("text_g") ? "text_g" : "text";
                            if (encoderInputs.ContainsKey(inputName)) {
                                if (inputName == "text_g" && encoderInputs.ContainsKey("text_l")) {
                                    encoderInputs["text_l"] = tipoOutputLink;
                                }
                                encoderInputs[inputName] = tipoOutputLink;
                                Logs.Debug($"TIPO: Rerouted '{inputName}' input of node '{targetEncoderId}' ({classType}) to TIPO node '{tipoNodeId}' output {tipoOutputIndex}.");
                                g.FinalPrompt = new JArray { targetEncoderId, g.FinalPrompt?[1] ?? 0 };
                                Logs.Debug($"TIPO: Updated g.FinalPrompt to: {g.FinalPrompt.ToString(Formatting.None)}");
                            } else { Logs.Warning($"TIPO: Target encoder node '{targetEncoderId}' lacks '{inputName}' input."); }
                         } else { Logs.Warning($"TIPO: Target node '{targetEncoderId}' is not a text encoder ({classType})."); }
                    } else { Logs.Warning($"TIPO: Target positive encoder node '{targetEncoderId}' not found at priority -0.1."); }
                }
            }, -0.1); // Keep priority -0.1
        }
    }
}