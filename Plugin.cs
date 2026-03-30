using BepInEx;
using BepInEx.Logging;
using BepInEx.Unity.IL2CPP;
using HarmonyLib;
using Il2CppInterop.Runtime.Injection;
using UnityEngine;

namespace DeliveryZoneMover
{
    [BepInPlugin("yaboie88.deliveryzonemover", "Delivery Zone Mover", "2.2.0")]
    public class Plugin : BasePlugin
    {
        internal static ManualLogSource Log;

        public override void Load()
        {
            Log = Log ?? base.Log;

            ClassInjector.RegisterTypeInIl2Cpp<DeliveryZoneController>();

            Harmony harmony = new Harmony("yaboie88.deliveryzonemover");
            harmony.PatchAll();

            GameObject controllerObject = new GameObject("DeliveryZoneMover_Controller");
            Object.DontDestroyOnLoad(controllerObject);
            controllerObject.hideFlags = HideFlags.HideAndDontSave;
            controllerObject.AddComponent<DeliveryZoneController>();

            LogInfo("Plugin loaded successfully.");
        }

        internal static void LogInfo(string message)
        {
            Log?.LogInfo("[DeliveryZoneMover] " + message);
        }

        internal static void LogError(string message)
        {
            Log?.LogError("[DeliveryZoneMover] " + message);
        }
    }
}