// Minimal collaborators for isolated tests of the real Tutorial and SaveData sources.
// These do not validate Unity rendering, scene simulation or Unity's JSON implementation.
using System;
using System.Collections.Generic;
using System.Web.Script.Serialization;
namespace UnityEngine
{
    public class MonoBehaviour { }
    public static class Time { public static float unscaledTime; }
    public static class Application { public static string persistentDataPath; }
    public static class Debug { public static void LogWarning(object message) { } }
    public static class PlayerPrefs
    {
        static Dictionary<string,int> values = new Dictionary<string,int>();
        public static int GetInt(string key, int fallback) => values.ContainsKey(key) ? values[key] : fallback;
        public static void SetInt(string key, int value) { values[key] = value; }
        public static void Save() { }
    }
    public static class Mathf
    {
        public static int Max(int a,int b) => Math.Max(a,b);
        public static int Clamp(int value,int min,int max) => Math.Min(max,Math.Max(min,value));
    }
    public struct Vector2
    {
        public float x, y;
        public Vector2(float x, float y) { this.x=x; this.y=y; }
        public static float Distance(Vector2 a, Vector2 b) => (float)Math.Sqrt((a.x-b.x)*(a.x-b.x)+(a.y-b.y)*(a.y-b.y));
        public static Vector2 Lerp(Vector2 a, Vector2 b, float t) => new Vector2(a.x+(b.x-a.x)*t,a.y+(b.y-a.y)*t);
    }
    public static class JsonUtility
    {
        public static string ToJson(object value) => new JavaScriptSerializer().Serialize(value);
        public static T FromJson<T>(string value) => new JavaScriptSerializer().Deserialize<T>(value);
    }
}
namespace AntColony
{
    public enum GameState { Playing, Won, Lost }
    public enum CameraView { Surface, Nest }
    public enum Sound { UiConfirm }
    public class AudioManager { public void Play(Sound sound) { } }
    public class CameraController
    {
        public CameraView View;
        public void ToggleView() { View=View==CameraView.Surface ? CameraView.Nest : CameraView.Surface; }
        public void FocusSurface(UnityEngine.Vector2 p) { }
        public UnityEngine.Vector2 NestFocus;
        public void FocusNest(UnityEngine.Vector2 p) { NestFocus=p; }
    }
    public class FoodSource { public UnityEngine.Vector2 Position; public bool HasUnclaimedUnits=true; }
    public class FoodSpawner
    {
        public List<FoodSource> Sources=new List<FoodSource>();
        public FoodSource SpawnNear(UnityEngine.Vector2 p,float min,float max) { var f=new FoodSource {Position=p};Sources.Add(f);return f; }
    }
    public enum RoomType { Queen, Cradle }
    public class Room { public RoomType Type; public bool IsBuilt=true, IsRemoved, AwaitingEgg; public object Brood; public UnityEngine.Vector2 StandPoint; }
    public class Nest
    {
        public Room QueenRoom=new Room {StandPoint=new UnityEngine.Vector2(2,-10)};
        public List<Room> Rooms=new List<Room> {new Room {Type=RoomType.Cradle,StandPoint=new UnityEngine.Vector2(4,-10)}};
    }
    public class GatherOrders { public int MarkedCount; }
    public class Surface { public UnityEngine.Vector2 EntrancePoint; }
    public class GameHud { public bool IsMissionsOpen; }
    public class SoldierSelection { public List<int> Selected=new List<int>(); public void Clear() { Selected.Clear(); } }
    public enum AntRole { Worker, Soldier, Builder, Spitter }
    public class Enemy { public int WaveNumber; }
    public partial class WaveSpawner
    {
        readonly List<Enemy> enemies=new List<Enemy>();
        public Enemy Boss;
        public int CurrentWave;
        public int TotalWaves => 10;
        public float PreparationTime;
        public void EnsurePreparationTime(float seconds) { PreparationTime=seconds; }
        public void AddEnemyForTest(Enemy e) { enemies.Add(e); }
        public WaveReportRecord ReportForTest(int wave) => Report(wave);
        public void AddPendingForTest(int wave,int count) { AddPending(wave,count); }
        public void FinishFrameForTest() { LateUpdate(); }
        public void PartialHistoryForTest(int through) { HistoryPartial=true; historyUnknownThrough=through; }
    }
    public partial class ColonyGame
    {
        public bool InMainMenu, IsPaused, TutorialPaused;
        public Tutorial Tutorial;
        public int Saves;
        public void Restart() { Tutorial.Stop(); IsPaused=false; State=GameState.Playing; }
        public bool SaveGame() { Saves++; return true; }
        public GameState State=GameState.Playing;
        public AudioManager Audio=new AudioManager();
        public CameraController CameraRig=new CameraController();
        public FoodSpawner Food=new FoodSpawner();
        public GatherOrders Gather=new GatherOrders();
        public Surface Surface=new Surface();
        public Nest Nest=new Nest();
        public GameHud Hud=new GameHud();
        public SoldierSelection Selection=new SoldierSelection();
        public WaveSpawner Waves=new WaveSpawner();
        public void SetTutorialPause(bool value) { TutorialPaused=value; }
        public void ShowMessage(string message) { }
        public IntroStub Intro=new IntroStub();
        public IntroSettings settings=new IntroSettings();
    }
    public class IntroStub { public int Plays; public void Play(System.Action finished) { Plays++; } }
    public class IntroSettings { public bool playIntro=true; }
}
