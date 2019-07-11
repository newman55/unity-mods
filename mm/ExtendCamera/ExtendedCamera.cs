using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Xml.Serialization;
using Harmony;
using UnityEngine;
using UnityModManagerNet;

namespace ExtendedCamera
{
    public class Settings : UnityModManager.ModSettings, IDrawable
    {
        [Draw] public bool InstantTransitions;
        [Draw(Precision = 0)] public float MinZoom = 10f;
        [Draw(Precision = 0)] public float MaxZoom = 220f;
        [Draw(Precision = 0)] public float MinRotationY = 10f;
        [Draw(Precision = 0)] public float MaxRotationY = 80f;
        [Draw(DrawType.Slider, Min = 1f, Max = 10f)] public float MouseSpeed = 5f;
        [Draw(DrawType.Slider, Min = 1f, Max = 10f)] public float KeyboardSpeed = 5f;
        public float NearClip = 1f;
        public float GetMouseSpeed => MouseSpeed * 0.01f;
        public float GetKeyboardSpeed => KeyboardSpeed * 0.5f;

        public override void Save(UnityModManager.ModEntry modEntry)
        {
            Save(this, modEntry);
        }

        public void OnChange()
        {
            Main.ApplySettings();
        }
    }

#if DEBUG
    //[EnableReloading]
#endif
    static class Main
    {
        public static bool enabled;
        public static Settings settings;
        public static UnityModManager.ModEntry.ModLogger logger;
        public static bool freeCamActivated;
        public static UnityVehicle fixCamTarget;
        public static Vector3 fixCamRotation;

        static bool Load(UnityModManager.ModEntry modEntry)
        {
            settings = Settings.Load<Settings>(modEntry);

            var harmony = HarmonyInstance.Create(modEntry.Info.Id);
            harmony.PatchAll(Assembly.GetExecutingAssembly());

            logger = modEntry.Logger;
            modEntry.OnSaveGUI = OnSaveGUI;
            modEntry.OnToggle = OnToggle;
            modEntry.OnGUI = OnGUI;
            modEntry.OnUpdate = OnUpdate;
#if DEBUG
            modEntry.OnUnload = Unload;
#endif
            return true;
        }
#if DEBUG
        static bool Unload(UnityModManager.ModEntry modEntry)
        {
            return true;
        }
#endif
        static void OnSaveGUI(UnityModManager.ModEntry modEntry)
        {
            settings.Save(modEntry);
        }

        static bool OnToggle(UnityModManager.ModEntry modEntry, bool value)
        {
            if (value)
            {
                ApplySettings();
            }
            else
            {
                var camera = App.instance?.cameraManager?.gameCamera?.freeRoamCamera;
                if (camera != null && freeCamActivated)
                {
                    Traverse.Create(camera).Method("SetState", new Type[] { freeRoamCamera_State }).GetValue(state_FollowingTarget);
                }
            }
            
            enabled = value;

            return true;
        }

        public static Type freeRoamCamera_State = (Type)Traverse.Create(typeof(FreeRoamCamera)).Type("State").GetValue();
        public static object state_FreeRoam = Enum.Parse(freeRoamCamera_State, "FreeRoam");
        public static object state_FollowingTarget = Enum.Parse(freeRoamCamera_State, "FollowingTarget");

        static void OnUpdate(UnityModManager.ModEntry modEntry, float dt)
        {
            if (Game.instance?.sessionManager?.circuit == null)
                return;
            
            if (Input.GetKeyDown(KeyCode.X))
            {
                var camera = App.instance?.cameraManager?.gameCamera?.freeRoamCamera;
                if (camera != null && camera.gameObject.activeSelf && Game.instance.sessionManager.isCircuitActive /*&& Game.instance.sessionManager.isSessionActive*/)
                {
                    Traverse.Create(camera).Method("SetState", new Type[] {freeRoamCamera_State}).GetValue(freeCamActivated ? state_FollowingTarget : state_FreeRoam);
                }
            }

            if (Input.GetKeyDown(KeyCode.C))
            {
                var camera = App.instance?.cameraManager?.gameCamera?.freeRoamCamera;
                if (camera && camera.targetVehicle != null)
                {
                    var state = Traverse.Create(camera).Field("mState").GetValue();
                    if ((int)state == (int)state_FollowingTarget)
                    {
                        fixCamTarget = !fixCamTarget ? camera.targetVehicle.unityVehicle : null;
                        fixCamRotation = camera.transform.parent.localEulerAngles - camera.targetVehicle.unityVehicle.transform.localEulerAngles;
                    }
                }
                else
                {
                    fixCamTarget = null;
                }
            }

            if (Input.GetKeyDown(KeyCode.H))
            {
                if (UIManager.instance.currentScreen is SessionHUD sessionHUD)
                {
                    var mask = 1 << LayerMask.NameToLayer("UI");
                    sessionHUD.sessionHUDCamera.cullingMask ^= mask;
                }
            }
        }

        static void OnGUI(UnityModManager.ModEntry modEntry)
        {
            GUILayout.Label("Free camera: <b>X</b>");
            GUILayout.Label("Sticky camera: <b>C</b>");
            GUILayout.Label("Hide UI: <b>H</b>");
            GUILayout.Space(5);

            settings.Draw(modEntry);
            
#if DEBUG
            if (GUILayout.Button("Debug", GUILayout.ExpandWidth(false)))
            {
            }
#endif
        }

        public static void ApplySettings()
        {
            if (Game.IsActive())
            {
                App.instance.preferencesManager.videoPreferences.SetExpandedCamera(true);
                App.instance.preferencesManager.SetSetting(Preference.pName.Video_ExpandedCamera, true, false);
            }

            var camera = App.instance?.cameraManager?.gameCamera?.GetCamera();
            if (camera)
            {
                camera.nearClipPlane = Main.settings.NearClip;
            }

            var freeRoamCamera = App.instance?.cameraManager?.gameCamera?.freeRoamCamera;
            if (freeRoamCamera)
            {
                Traverse.Create(freeRoamCamera).Field("expandedMinZoom").SetValue(Main.settings.MinZoom);
                Traverse.Create(freeRoamCamera).Field("expandedMaxZoom").SetValue(Main.settings.MaxZoom);
                Traverse.Create(freeRoamCamera).Field("expandedMinRotationX").SetValue(Main.settings.MinRotationY);
                Traverse.Create(freeRoamCamera).Field("expandedMaxRotationX").SetValue(Main.settings.MaxRotationY);
            }
        }

        public static float AngleSigned(Vector3 v1, Vector3 v2, Vector3 n)
        {
            return Mathf.Atan2(Vector3.Dot(n, Vector3.Cross(v1, v2)), Vector3.Dot(v1, v2)) * Mathf.Rad2Deg;
        }
    }

    [HarmonyPatch(typeof(FreeRoamCamera), "SetState")]
    static class FreeRoamCamera_SetState_Patch
    {
        static void Postfix(FreeRoamCamera __instance)
        {
            if (!Main.enabled)
                return;
            
            try
            {
                Main.fixCamTarget = null;

                var state = Traverse.Create(__instance).Field("mState").GetValue();
                if ((int)state == (int)Main.state_FreeRoam)
                {
                    Main.freeCamActivated = true;
                }
                else
                {
                    Main.freeCamActivated = false;
                }

                Traverse.Create(__instance).Field("mDisablePanControls").SetValue(Main.freeCamActivated);
            }
            catch (Exception e)
            {
                Main.logger.LogException(e);
            }
        }
    }

    [HarmonyPatch(typeof(FreeRoamCamera), "OnEnable")]
    static class FreeRoamCamera_OnEnable_Patch
    {
        static void Postfix(FreeRoamCamera __instance)
        {
            if (!Main.enabled)
                return;

            try
            {
                Main.ApplySettings();
            }
            catch (Exception e)
            {
                Main.logger.LogException(e);
            }
        }
    }

    [HarmonyPatch(typeof(FreeRoamCamera), "OnDragging")]
    static class FreeRoamCamera_OnDragging_Patch
    {
        static void Postfix(FreeRoamCamera __instance, DragInfo dragInfo)
        {
            if (!Main.enabled || !Main.freeCamActivated)
                return;

            try
            {
                var mDragDelayTimer = Traverse.Create(__instance).Field("mDragDelayTimer").GetValue<float>();
                var dragInitialDelay = Traverse.Create(__instance).Field("dragInitialDelay").GetValue<float>();
                var dragModifier = Traverse.Create(__instance).Field("dragModifier").GetValue<float>();

                if (Input.GetMouseButton(1) && mDragDelayTimer >= dragInitialDelay && !(UIManager.instance.UIObjectsAtMousePosition.Count > 0))
                {
                    var zoom = Mathf.Lerp(__instance.cameraMaxZoom, __instance.cameraMinZoom, __instance.zoomNormalized);
                    var pos = Vector3.zero;
                    var transform = __instance.transform;
                    var position = transform.position;
                    var forward = transform.forward;
                    forward.y = 0;
                    pos += forward.normalized * (-dragInfo.delta.y * Time.unscaledDeltaTime) * dragModifier * Main.settings.GetMouseSpeed * zoom;
                    var right = transform.right;
                    right.y = 0;
                    pos += right.normalized * (-dragInfo.delta.x * Time.unscaledDeltaTime) * dragModifier * Main.settings.GetMouseSpeed * zoom;
                    transform.parent.position += pos;
                }
            }
            catch (Exception e)
            {
                Main.logger.LogException(e);
            }
        }
    }

    [HarmonyPatch(typeof(FreeRoamCamera), "LateUpdate")]
    static class FreeRoamCamera_LateUpdate_Patch
    {
        static void Postfix(FreeRoamCamera __instance)
        {
            if (!Main.enabled)
                return;

            try
            {
                if (Main.freeCamActivated)
                {
                    var transform = __instance.transform;
                    var pos = Vector3.zero;
                    var zoom = Mathf.Lerp(__instance.cameraMaxZoom, __instance.cameraMinZoom, __instance.zoomNormalized);
                    var speed = Main.settings.GetKeyboardSpeed * zoom * Time.unscaledDeltaTime;
                    if (Input.GetKey(KeyCode.W))
                    {
                        var dir = transform.forward;
                        dir.y = 0;
                        pos += dir.normalized * speed;
                    }
                    if (Input.GetKey(KeyCode.S))
                    {
                        var dir = transform.forward;
                        dir.y = 0;
                        pos -= dir.normalized * speed;
                    }
                    if (Input.GetKey(KeyCode.A))
                    {
                        var dir = transform.right;
                        dir.y = 0;
                        pos -= dir.normalized * speed;
                    }
                    if (Input.GetKey(KeyCode.D))
                    {
                        var dir = transform.right;
                        dir.y = 0;
                        pos += dir.normalized * speed;
                    }

                    var position = transform.parent.position;
                    position += pos;
                    transform.parent.position = position;

                    //            RaycastHit raycastHit;
                    //            pos = position;
                    //            pos.y = 50f;
                    //            if (Physics.Raycast(pos, -transform.up, out raycastHit, 0))
                    //            {
                    //                transform.localPosition = new Vector3(0, raycastHit.point.y, 0);
                    //                Console.WriteLine($"{raycastHit.transform.name} {raycastHit.collider.gameObject.layer} {raycastHit.point}");
                    //            }

                    var scrollWheel = Input.GetAxis("Mouse ScrollWheel");
                    if (scrollWheel != 0)
                    {
                        var mZoomSpeed = Traverse.Create(__instance).Field("mZoomSpeed").GetValue<float>();
                        Traverse.Create(__instance).Field("mZoomSpeed").SetValue(mZoomSpeed + scrollWheel * Time.unscaledDeltaTime * Traverse.Create(__instance).Field("zoomSpeedModifier").GetValue<float>());
                    }
                }
                else
                {
                    if (Main.fixCamTarget)
                    {
                        var x = __instance.transform.parent.localEulerAngles.x;
                        var y1 = __instance.transform.parent.localEulerAngles.y;
                        var y2 = (__instance.targetVehicle.unityVehicle.transform.localEulerAngles + Main.fixCamRotation).y;
                        
                        if (y1 < 0)
                        {
                            y1 += 360f;
                        }
                        if (y2 < 0)
                        {
                            y2 += 360f;
                        }
                        y1 = y1 % 360f;
                        y2 = y2 % 360f;
                        if (y2 - y1 > 180)
                        {
                            y1 += 360f;
                        }
                        else if (y1 - y2 > 180)
                        {
                            y2 += 360f;
                        }

                        __instance.transform.parent.localEulerAngles = Vector3.Lerp(new Vector3(x, y1, 0), new Vector3(x, y2, 0), Time.unscaledDeltaTime * 3f * Game.instance.time.GetSimulationTimeScale());
                    }
                }
            }
            catch (Exception e)
            {
                Main.logger.LogException(e);
            }
        }
    }

    [HarmonyPatch(typeof(FreeRoamCamera), "ApplyRotation")]
    static class FreeRoamCamera_ApplyRotation_Patch
    {
        static Vector3 rotation;

        static void Prefix(FreeRoamCamera __instance)
        {
            if (!Main.enabled)
                return;

            try
            {
                if (Main.fixCamTarget)
                {
                    rotation = __instance.transform.parent.localEulerAngles;
                }
            }
            catch (Exception e)
            {
                Main.logger.LogException(e);
            }
        }

        static void Postfix(FreeRoamCamera __instance)
        {
            if (!Main.enabled)
                return;

            try
            {
                if (Main.fixCamTarget)
                {
                    Main.fixCamRotation += __instance.transform.parent.localEulerAngles - rotation;
                }
            }
            catch (Exception e)
            {
                Main.logger.LogException(e);
            }
        }
    }

    [HarmonyPatch(typeof(FreeRoamCamera), "SetTarget")]
    static class FreeRoamCamera_SetTarget_Patch
    {
        static void Prefix(FreeRoamCamera __instance, GameObject inTarget, ref CameraManager.Transition inTransition, float inCameraRightVectorOffset, Vehicle inVehicle)
        {
            if (!Main.enabled || !Main.settings.InstantTransitions)
                return;

            try
            {
                inTransition = CameraManager.Transition.Instant;
            }
            catch (Exception e)
            {
                Main.logger.LogException(e);
            }
        }
    }
}
