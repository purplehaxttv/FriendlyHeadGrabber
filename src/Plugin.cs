using System;
using System.IO;
using System.Reflection;
using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using HarmonyLib;
using UnityEngine;

namespace FriendlyHeadGrabber
{
    [BepInPlugin(Guid, "Friendly Head Grabber", "1.0.0")]
    [BepInDependency("com.purplehaxttv.friendlyduck", BepInDependency.DependencyFlags.SoftDependency)]
    public class Plugin : BaseUnityPlugin
    {
        public const string Guid = "com.purplehaxttv.friendlyheadgrabber";

        internal static ManualLogSource Log;
        internal static ConfigEntry<bool> LeaveHeadsAlone;
        internal static ConfigEntry<KeyCode> PetKey;
        internal static ConfigEntry<float> PetRange;
        internal static GameObject Hearts;

        float nextPet;

        void Awake()
        {
            Log = Logger;
            transform.parent = null;
            gameObject.hideFlags = HideFlags.HideAndDontSave;

            LeaveHeadsAlone = Config.Bind("General", "Leave player heads alone", true,
                "The head grabber no longer picks up the heads of dead players.");
            PetKey = Config.Bind("Petting", "Pet key", KeyCode.E, "Press this while looking at a head grabber to show it some love.");
            PetRange = Config.Bind("Petting", "Pet range", 4f,
                new ConfigDescription("How close you have to be to pet it, in metres.", new AcceptableValueRange<float>(1f, 10f)));

            LoadHearts();
            new Harmony(Guid).PatchAll();
            Log.LogInfo("Friendly Head Grabber loaded.");
        }

        void LoadHearts()
        {
            // Friendly Duck ships the same hearts; a bundle can only be loaded once, so reuse its copy when it is installed.
            Type duckAssets = AccessTools.TypeByName("ModAssets");
            if (duckAssets != null)
            {
                Hearts = AccessTools.Field(duckAssets, "HeartParticlesPrefab")?.GetValue(null) as GameObject;
                if (Hearts) return;
            }

            string path = Path.Combine(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location), "friendlyheadgrabberassets");
            if (!File.Exists(path))
            {
                Log.LogWarning("Heart effect not found at " + path);
                return;
            }
            AssetBundle bundle = AssetBundle.LoadFromFile(path);
            if (bundle) Hearts = bundle.LoadAsset<GameObject>("HeartParticles");
            if (!Hearts) Log.LogWarning("Heart effect could not be loaded.");
        }

        void Update()
        {
            if (!Input.GetKeyDown(PetKey.Value) || Time.unscaledTime < nextPet) return;
            Camera camera = Camera.main;
            if (!camera) return;

            EnemyHeadGrabber best = null;
            float bestAngle = 30f;
            foreach (EnemyHeadGrabber grabber in FindObjectsByType<EnemyHeadGrabber>(FindObjectsSortMode.None))
            {
                if (!grabber.isActiveAndEnabled || !grabber.enemy || !grabber.enemy.CenterTransform) continue;
                Vector3 to = grabber.enemy.CenterTransform.position - camera.transform.position;
                if (to.magnitude > PetRange.Value) continue;
                float angle = Vector3.Angle(camera.transform.forward, to);
                if (angle < bestAngle)
                {
                    best = grabber;
                    bestAngle = angle;
                }
            }
            if (!best) return;
            nextPet = Time.unscaledTime + 1f;
            Pet(best);
        }

        public static GameObject Pet(EnemyHeadGrabber grabber)
        {
            if (!Hearts) return null;
            Transform body = grabber.enemy.Rigidbody ? grabber.enemy.Rigidbody.transform : grabber.transform;
            Vector3 offset = Vector3.up * 1.5f;
            GameObject fx = Instantiate(Hearts, body.position + offset, Quaternion.identity);
            fx.SetActive(true);
            HeartFollow follow = fx.AddComponent<HeartFollow>();
            follow.Target = body;
            follow.Offset = offset;
            ParticleSystem particles = fx.GetComponent<ParticleSystem>();
            if (particles) particles.Play();
            Destroy(fx, 6f);
            return fx;
        }
    }

    // Keeps the hearts above the enemy without parenting them to it, since parts of an enemy get switched off.
    public class HeartFollow : MonoBehaviour
    {
        public Transform Target;
        public Vector3 Offset;

        void LateUpdate()
        {
            if (Target) transform.position = Target.position + Offset;
        }
    }
    [HarmonyPatch(typeof(EnemyHeadGrabber), "UpdateState")]
    static class Patch_UpdateState
    {
        static void Prefix(ref EnemyHeadGrabber.State _state)
        {
            switch (_state)
            {
                case EnemyHeadGrabber.State.Notice:
                case EnemyHeadGrabber.State.Attack:
                case EnemyHeadGrabber.State.CantReach:
                case EnemyHeadGrabber.State.GotoPlayer:
                case EnemyHeadGrabber.State.GotoPlayerOver:
                    _state = EnemyHeadGrabber.State.Idle;
                    break;
                case EnemyHeadGrabber.State.GotoHead:
                case EnemyHeadGrabber.State.GotoHeadOver:
                case EnemyHeadGrabber.State.GrabHead:
                    if (Plugin.LeaveHeadsAlone.Value) _state = EnemyHeadGrabber.State.Idle;
                    break;
            }
        }
    }

    [HarmonyPatch(typeof(EnemyHeadGrabber), "OnVision")]
    static class Patch_OnVision
    {
        static bool Prefix() => false;
    }

    [HarmonyPatch(typeof(EnemyHeadGrabber), "NearbyHeadLogic")]
    static class Patch_NearbyHeadLogic
    {
        static bool Prefix(ref bool __result)
        {
            if (!Plugin.LeaveHeadsAlone.Value) return true;
            __result = false;
            return false;
        }
    }
}
