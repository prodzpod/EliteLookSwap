using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using RoR2;
using System;
using System.Linq;
using System.Collections.Generic;
using MonoMod.Cil;
using Mono.Cecil.Cil;
using HarmonyLib;
using System.Reflection;
using R2API;

namespace EliteLookSwap
{
    [BepInPlugin(PluginGUID, PluginName, PluginVersion)]
    public class Main : BaseUnityPlugin
    {
        public const string PluginGUID = PluginAuthor + "." + PluginName;
        public const string PluginAuthor = "_prodzpod";
        public const string PluginName = "EliteLookSwap";
        public const string PluginVersion = "1.0.0";
        public static ManualLogSource Log;
        public static PluginInfo pluginInfo;
        public static ConfigFile Config;
        public static Harmony Harmony;
        public static ConfigEntry<string> OverridesRaw;
        public static Dictionary<string, string> Overrides = [];

        public void Awake()
        {
            pluginInfo = Info;
            Log = Logger;
            Config = new ConfigFile(System.IO.Path.Combine(Paths.ConfigPath, PluginGUID + ".cfg"), true);
            Harmony = new Harmony(PluginGUID);
            OverridesRaw = Config.Bind("General", "Overrides", "NEMESISRISINGTIDES_AFFIXBUFFERED=ELITEVARIETY_AFFIXARMORED, ELITE_EQUIPMENT_MOTIVATING=ELITEVARIETY_AFFIXBUFFING, ELITEVARIETY_AFFIXIMPPLANE=EQUIPMENT_AFFIXARAGONITE, EQUIPMENT_AFFIXARAGONITE=ELITEVARIETY_AFFIXIMPPLANE, ELITEAURELIONITEEQUIPMENT=ELITEVARIETY_AFFIXPILLAGING", "FROM=TO, elite names, separated by commas. see the log for list of valid input for your pack.");
            foreach (var _entry in OverridesRaw.Value.Split(","))
            {
                var entry = _entry.Split("=");
                if (entry.Length != 2) { Log.LogWarning("Entry is malformed, skipping: " + _entry); continue; }
                Overrides.Add(entry[0].Trim().ToUpper(), entry[1].Trim().ToUpper());
            }
            RoR2Application.onLoad += () =>
            {
                List<string> txt = [];
                foreach (var name in EquipmentCatalog.equipmentDefs.Where(x => x.passiveBuffDef?.eliteDef != null).Select(x => x.name))
                {
                    var _txt = name.ToUpper();
                    if (Overrides.ContainsKey(_txt.ToUpper())) _txt += "=" + Overrides[_txt.ToUpper()];
                    txt.Add(_txt);
                }
                Log.LogInfo("Elites: " + string.Join(", ", txt));
            };
            Harmony.PatchAll(typeof(PatchR2API));
            On.RoR2.CharacterModel.SetEquipmentDisplay += (orig, self, idx) =>
            {
                var def = EquipmentCatalog.GetEquipmentDef(idx);
                if (def != null && Overrides.ContainsKey(def.name.ToUpper())) orig(self, EquipmentCatalog.equipmentDefs.FirstOrDefault(x => x.name.ToUpper() == Overrides[def.name.ToUpper()])?.equipmentIndex ?? idx);
                else orig(self, idx);
            };
        }
    }

    [HarmonyPatch]
    public class PatchR2API
    {
        public static void ILManipulator(ILContext il, MethodBase original, ILLabel retLabel)
        {
            ILCursor c = new(il);
            while (c.TryGotoNext(MoveType.After, x => x.MatchLdfld<CharacterModel>(nameof(CharacterModel.myEliteIndex))))
                c.EmitDelegate<Func<EliteIndex, EliteIndex>>(idx =>
                {
                    var def = EliteCatalog.GetEliteDef(idx);
                    if (def == null) return idx;
                    if (Main.Overrides.ContainsKey(def.eliteEquipmentDef.name.ToUpper())) 
                        return (EquipmentCatalog.equipmentDefs.FirstOrDefault(x => x.name.ToUpper() == Main.Overrides[def.eliteEquipmentDef.name.ToUpper()]) ?? def.eliteEquipmentDef).passiveBuffDef.eliteDef.eliteIndex;
                    return idx;
                });
        }

        public static MethodBase TargetMethod()
        {
            return AccessTools.GetDeclaredMethods(typeof(EliteRamp)).Find(x => x.Name.StartsWith($"<{nameof(EliteRamp.UpdateRampProperly)}>g"));
        }
    }
}
