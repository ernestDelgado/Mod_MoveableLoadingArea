using System;
using System.Reflection;
using HarmonyLib;

namespace DeliveryZoneMover
{
    [HarmonyPatch]
    public static class DeliveryManager_Delivery_Patch
    {
        private static MethodBase TargetMethod()
        {
            try
            {
                Type deliveryManagerType = AccessTools.TypeByName("DeliveryManager");
                Type cartDataType = AccessTools.TypeByName("CartData");

                if (deliveryManagerType == null)
                {
                    Plugin.LogError("Could not find type: DeliveryManager");
                    return null;
                }

                if (cartDataType == null)
                {
                    Plugin.LogError("Could not find type: CartData");
                    return null;
                }

                MethodInfo method = AccessTools.Method(deliveryManagerType, "Delivery", new Type[] { cartDataType });
                if (method == null)
                {
                    Plugin.LogError("Could not find method: DeliveryManager.Delivery(CartData)");
                    return null;
                }

                return method;
            }
            catch (Exception ex)
            {
                Plugin.LogError("DeliveryManager.Delivery TargetMethod exception: " + ex);
                return null;
            }
        }

        private static void Prefix()
        {
            DeliveryZoneController.ApplyCurrentPlacement();
        }
    }
}