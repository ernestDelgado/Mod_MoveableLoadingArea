using System;
using System.Reflection;
using HarmonyLib;
using UnityEngine;

namespace DeliveryZoneMover
{
    [HarmonyPatch]
    public static class SaveManagerSavePatch
    {
        private static float lastPatchedSaveTime = -999f;

        private static bool ShouldWriteNow()
        {
            float now = Time.unscaledTime;

            if (now - lastPatchedSaveTime < 0.5f)
                return false;

            lastPatchedSaveTime = now;
            return true;
        }

        private static void TrySavePlacement()
        {
            if (!ShouldWriteNow())
                return;

            DeliveryZoneController.SavePlacementToFile();
        }

        [HarmonyPatch]
        private static class Save_NoArgs_Patch
        {
            private static MethodBase TargetMethod()
            {
                try
                {
                    Type saveManagerType = AccessTools.TypeByName("SaveManager");
                    if (saveManagerType == null)
                    {
                        Plugin.LogError("Could not find type: SaveManager");
                        return null;
                    }

                    return AccessTools.Method(saveManagerType, "Save", Type.EmptyTypes);
                }
                catch (Exception ex)
                {
                    Plugin.LogError("Save_NoArgs_Patch TargetMethod exception: " + ex);
                    return null;
                }
            }

            private static void Postfix()
            {
                TrySavePlacement();
            }
        }

        [HarmonyPatch]
        private static class Save_String_Patch
        {
            private static MethodBase TargetMethod()
            {
                try
                {
                    Type saveManagerType = AccessTools.TypeByName("SaveManager");
                    if (saveManagerType == null)
                    {
                        Plugin.LogError("Could not find type: SaveManager");
                        return null;
                    }

                    return AccessTools.Method(saveManagerType, "Save", new Type[] { typeof(string) });
                }
                catch (Exception ex)
                {
                    Plugin.LogError("Save_String_Patch TargetMethod exception: " + ex);
                    return null;
                }
            }

            private static void Postfix()
            {
                TrySavePlacement();
            }
        }

        [HarmonyPatch]
        private static class Save_SaveInfo_Patch
        {
            private static MethodBase TargetMethod()
            {
                try
                {
                    Type saveManagerType = AccessTools.TypeByName("SaveManager");
                    Type saveInfoType = AccessTools.TypeByName("SaveInfo");

                    if (saveManagerType == null)
                    {
                        Plugin.LogError("Could not find type: SaveManager");
                        return null;
                    }

                    if (saveInfoType == null)
                    {
                        Plugin.LogError("Could not find type: SaveInfo");
                        return null;
                    }

                    return AccessTools.Method(saveManagerType, "Save", new Type[] { saveInfoType });
                }
                catch (Exception ex)
                {
                    Plugin.LogError("Save_SaveInfo_Patch TargetMethod exception: " + ex);
                    return null;
                }
            }

            private static void Postfix()
            {
                TrySavePlacement();
            }
        }
    }
}