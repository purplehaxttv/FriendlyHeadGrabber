using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using BepInEx;
using BepInEx.Logging;
using HarmonyLib;
using UnityEngine;

namespace FriendlyHeadGrabberDev
{
    // Not shipped. Starts a solo run, spawns a head grabber next to the player and logs its state and the player's health.
    [BepInPlugin("purplehaxttv.FriendlyHeadGrabberDev", "Friendly Head Grabber Dev", "0.0.1")]
    [BepInDependency("com.purplehaxttv.friendlyheadgrabber")]
    [BepInDependency("REPOLib")]
    public class DevPlugin : BaseUnityPlugin
    {
        static ManualLogSource L;
        void Awake() { L = Logger; transform.parent = null; gameObject.hideFlags = HideFlags.HideAndDontSave; StartCoroutine(Run()); }
        static void Say(string s) => L.LogInfo("DEVTEST " + s);

        IEnumerator Run()
        {
            while (FindObjectOfType<MenuPageMain>() == null) yield return new WaitForSecondsRealtime(1f);
            yield return new WaitForSecondsRealtime(6f);
            try { SemiFunc.MenuActionSingleplayerGame(); } catch (Exception e) { Say("start failed " + e); }
            yield return new WaitForSecondsRealtime(5f);
            float deadline = Time.realtimeSinceStartup + 180f;
            while (!(SemiFunc.RunIsLevel() && LevelGenerator.Instance != null && LevelGenerator.Instance.Generated && FindObjectsOfType<PhysGrabCart>().Length > 0))
            {
                if (Time.realtimeSinceStartup > deadline) { Say("FAIL level never generated"); Application.Quit(); yield break; }
                yield return new WaitForSecondsRealtime(1f);
            }
            yield return new WaitForSecondsRealtime(12f);

            Transform me = PlayerAvatar.instance.transform;
            EnemySetup setup = REPOLib.Modules.Enemies.AllEnemies.FirstOrDefault(e => e && e.name == "Enemy - Head Grabber");
            List<EnemyParent> spawned = setup ? REPOLib.Modules.Enemies.SpawnEnemy(setup, me.position + me.forward * 3f, Quaternion.identity, false) : null;
            EnemyParent parent = spawned?.FirstOrDefault();
            Say("spawned " + (parent ? parent.name + " / " + parent.enemyName : "nothing"));
            if (!parent) { Application.Quit(); yield break; }

            var seen = new HashSet<string>();
            for (int i = 0; i < 26; i++)
            {
                yield return new WaitForSecondsRealtime(1f);
                EnemyHeadGrabber grabber = parent.GetComponentInChildren<EnemyHeadGrabber>(true);
                if (!grabber) { Say("no grabber component"); continue; }
                int hp = Traverse.Create(PlayerAvatar.instance.playerHealth).Field<int>("health").Value;
                float distance = Vector3.Distance(Traverse.Create(Traverse.Create(grabber).Field<Enemy>("enemy").Value).Field<EnemyRigidbody>("Rigidbody").Value.transform.position, me.position);
                seen.Add(grabber.currentState.ToString());
                if (i % 5 == 4) Say($"t={i + 1} state={grabber.currentState} distance={distance:F1}m playerHp={hp} states so far: {string.Join(",", seen)}");
                if (i == 21) ScreenCapture.CaptureScreenshot(System.IO.Path.Combine(Paths.BepInExRootPath, "grabber.png"));
                if (i == 19) { var rb = Traverse.Create(Traverse.Create(grabber).Field<Enemy>("enemy").Value).Field<EnemyRigidbody>("Rigidbody").Value; rb.GetComponent<PhysGrabObject>().Teleport(Camera.main.transform.position + Camera.main.transform.forward * 3f, Quaternion.identity); }
                if (i == 20)
                {
                    GameObject fx = FriendlyHeadGrabber.Plugin.Pet(grabber);
                    Say("pet: hearts " + (fx ? "spawned " + fx.name + " particles=" + (fx.GetComponent("ParticleSystem") != null) + " active=" + fx.activeInHierarchy + " centerActive=" + Traverse.Create(grabber).Field<Enemy>("enemy").Value.CenterTransform.gameObject.activeInHierarchy + " height above player feet=" + (fx.transform.position.y - me.position.y).ToString("F1") : "MISSING"));
                }
            }
            Say("DONE");
            yield return new WaitForSecondsRealtime(1f);
            Application.Quit();
        }
    }
}
