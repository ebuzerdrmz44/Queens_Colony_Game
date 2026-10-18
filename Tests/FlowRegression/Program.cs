using System;
using System.IO;
using System.Reflection;
using AntColony;
using UnityEngine;
class Program
{
    static int checks;
    static void Check(bool pass,string label) { if(!pass) throw new Exception(label); checks++; }
    static void Tick(Tutorial t) { typeof(Tutorial).GetMethod("Update",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(t,null); }
    static Tutorial At(ColonyGame game,int step,bool started=false)
    {
        var t=new Tutorial();t.Init(game);
        t.Read(new SaveData {tutorialActive=true,tutorialStep=step,tutorialGoalStarted=started});
        Time.unscaledTime+=1;t.Continue();return t;
    }
    static void Main()
    {
        string folder=Path.Combine(Path.GetTempPath(),"colony-flow-tests-"+Guid.NewGuid());
        Application.persistentDataPath=folder;
        try
        {
            var g=new ColonyGame(); var t=new Tutorial(); t.Init(g); t.Begin(); t.Continue();
            Check(t.StepNumber==1 && t.ShowingPopup,"Opening input cannot skip the welcome");
            Time.unscaledTime+=1;t.Continue();
            Check(t.StepNumber==2 && t.ShowingPopup,"Welcome advances only to gather explanation");
            Time.unscaledTime+=1;t.Continue();Tick(t);
            Check(!t.GoalCompleted,"Gather waits for a real delivery");
            t.Notify(TutorialSignal.FoodMarked);Tick(t);
            Check(!t.GoalCompleted,"Marking food alone does not finish gathering");
            t.Notify(TutorialSignal.FoodDelivered);Tick(t);
            Check(t.GoalCompleted,"Stored food completes gathering");
            Time.unscaledTime+=60;Tick(t);
            Check(t.StepNumber==2 && !t.ShowingPopup,"Completed goal never auto-advances");
            t.Review();Time.unscaledTime+=1;t.Continue();
            Check(t.GoalCompleted && !t.ShowingPopup,"Read again preserves completed goal");
            g.IsPaused=true;t.Advance();Check(t.StepNumber==2,"Pause prevents progression");
            g.IsPaused=false;t.Advance();Check(t.StepNumber==3 && t.ShowingPopup,"Next explicitly advances");

            var cases=new [] {
                new []{TutorialSignal.AntQueued,TutorialSignal.WorkerHatched},
                new []{TutorialSignal.DigMarked,TutorialSignal.TileDug},
                new []{TutorialSignal.RoomPlaced,TutorialSignal.RoomBuilt},
                new []{TutorialSignal.WallPlaced,TutorialSignal.StructureBuilt}
            };
            int[] steps={3,5,6,8};
            for(int i=0;i<steps.Length;i++)
            {
                g=new ColonyGame();t=At(g,steps[i]);t.Notify(cases[i][0]);Tick(t);
                Check(!t.GoalCompleted,"Order alone does not complete step "+steps[i]);
                var save=new SaveData();t.Write(save);
                var restored=AtSaved(g,save);restored.Notify(cases[i][1]);Tick(restored);
                Check(restored.GoalCompleted,"Pending goal survives save/load at step "+steps[i]);
                var completed=new SaveData();restored.Write(completed);
                Check(AtSaved(g,completed).GoalCompleted,"Completed goal survives load at step "+steps[i]);
            }
            g=new ColonyGame();t=At(g,1);t.Notify(TutorialSignal.FoodMarked);t.Notify(TutorialSignal.FoodDelivered);
            var boundary=new SaveData();t.Write(boundary);
            Check(boundary.tutorialGoalCompleted,"Saving before tutorial Update still captures completion");
            var old=new Tutorial();old.Init(g);old.Read(new SaveData {tutorialStep=0});
            Check(!old.IsActive,"Legacy normal save does not enable tutorial");
            t=At(g,10);Check(!t.IsActive && g.Waves.PreparationTime==45f && !g.TutorialPaused,"Final explanation releases pause and gives preparation time");
            t=At(g,5);t.Skip();Check(!t.IsActive && !t.HoldsWaves && !g.TutorialPaused,"Skip releases tutorial and wave hold");

            g=new ColonyGame();t=At(g,3);
            Check(g.CameraRig.View==CameraView.Nest && g.CameraRig.NestFocus.x==3f && g.CameraRig.NestFocus.y==-10f,
                "Raise ants opens underground with queen and cradle in focus");
            t.SkipStep();
            Check(t.StepNumber==5 && t.ShowingPopup,"Larva wait can be skipped without waiting for a hatch");
            var skipped=new SaveData();t.Write(skipped);
            Check(skipped.tutorialStep==4,"Saving after skip stores the following step");
            t=At(g,1);t.SkipStep();Check(t.StepNumber==2,"Essential gathering cannot be skipped independently");
            t=At(g,5);t.SkipStep();Check(t.StepNumber==6,"Essential digging cannot be skipped independently");
            t=At(g,4);g.CameraRig.View=CameraView.Surface;t.SkipStep();
            Check(t.StepNumber==6 && g.CameraRig.View==CameraView.Nest,"Skipping view instruction still prepares nest tasks");
            t=At(g,7);g.CameraRig.View=CameraView.Nest;t.SkipStep();
            Check(t.StepNumber==9 && g.CameraRig.View==CameraView.Surface,"Skipping surface instruction prepares defence tasks");
            t=At(g,6);g.IsPaused=true;t.SkipStep();Check(t.StepNumber==7,"Pause blocks step skipping");g.IsPaused=false;
            var firstPopup=new Tutorial();firstPopup.Init(g);firstPopup.Begin();firstPopup.SkipStep();
            Check(firstPopup.StepNumber==1,"Opening input cannot immediately skip a step");
            PlayerPrefs.SetInt("tutorial.done",0);
            g=new ColonyGame();g.Tutorial=new Tutorial();g.Tutorial.Init(g);g.StartNewGame();
            Check(!g.Tutorial.IsActive && g.Saves==1,"New game never auto-starts an unfinished tutorial");
            g.StartNewGame(true);Check(g.Tutorial.IsActive,"Explicit Tutorial still starts the guide");
            int introPlays=g.Intro.Plays;g.StartNewGame(playIntro:false);Check(g.Intro.Plays==introPlays,"A new game started without the intro skips it");
            g.StartNewGame();Check(g.Intro.Plays==introPlays+1,"A new game plays the intro story");
            g.StartNewGame();Check(!g.Tutorial.IsActive && !g.TutorialPaused,"New game clears a previous active tutorial");

            new SaveData {food=11}.WriteToDisk();
            Check(SaveData.ReadFromDisk(out var problem).food==11 && problem==null,"First save creates directory and file");
            new SaveData {food=22}.WriteToDisk();new SaveData {food=33}.WriteToDisk();
            Check(SaveData.ReadFromDisk(out problem).food==33,"Repeated atomic save replaces slot");
            Check(UnityEngine.JsonUtility.FromJson<SaveData>(File.ReadAllText(SaveData.FilePath+".bak")).food==22,"Backup holds previous complete save");
            Directory.CreateDirectory(SaveData.FilePath+".tmp");bool failed=false;
            try {new SaveData {food=44}.WriteToDisk();}catch{failed=true;}
            Check(failed && SaveData.ReadFromDisk(out problem).food==33,"Failed write preserves old save");
            Directory.Delete(SaveData.FilePath+".tmp");
            File.WriteAllText(SaveData.FilePath,"broken JSON");
            Check(SaveData.ReadFromDisk(out problem)==null && problem!=null,"Corrupt save reports failure");
            File.WriteAllText(SaveData.FilePath,"{\"version\":1,\"food\":9}");
            Check(!SaveData.ReadFromDisk(out problem).tutorialActive,"Legacy JSON stays outside tutorial");
            var waves=new WaveSpawner();
            var first=new Enemy {WaveNumber=1};var second=new Enemy {WaveNumber=2};
            waves.ReportForTest(1).started=true;waves.ReportForTest(2).started=true;
            waves.AddEnemyForTest(first);waves.AddEnemyForTest(second);
            waves.AddPendingForTest(1,1);
            waves.RemoveEnemy(first);waves.RemoveEnemy(first);
            Check(waves.FindReport(1).kills==1 && waves.FindReport(2).kills==0,"Overlapping-wave kills stay with their original wave and cannot double count");
            waves.FinishFrameForTest();Check(!waves.FindReport(1).completed,"Pending arrivals prevent premature wave completion");
            waves.AddPendingForTest(1,-1);waves.FinishFrameForTest();
            Check(waves.FindReport(1).completed && !waves.FindReport(2).completed,"An earlier wave can clear while a later overlapping wave is active");
            Check(waves.RemainingInWave(2)==1,"Remaining count includes live enemies");
            waves.RecordAntLoss(AntRole.Soldier,1,false);
            waves.RecordAntLoss(AntRole.Spitter,2,false);
            waves.RecordAntLoss(AntRole.Worker,2,true);
            Check(waves.FindReport(1).soldiersLost==1 && waves.FindReport(2).spittersLost==1,"Casualties follow the attacking enemy's wave");
            Check(waves.FindReport(0).workersLost==1 && waves.FindReport(0).starved==1,"Starvation is upkeep rather than an enemy kill");
            waves.FindReport(1).RecordLoss(99,false);
            Check(waves.FindReport(1).TotalLosses==1,"Unknown roles cannot inflate casualties");
            var legacyWaves=new WaveSpawner();legacyWaves.PartialHistoryForTest(3);
            Check(legacyWaves.ReportForTest(3).partial,"Legacy current wave is marked as a partial record");
            Check(!legacyWaves.ReportForTest(4).partial,"New waves after a legacy load have complete records");
            var ledger=new SaveData {hasWaveReports=true,waveReports=new System.Collections.Generic.List<WaveReportRecord>(waves.Reports)};
            ledger.WriteToDisk();var loadedLedger=SaveData.ReadFromDisk(out problem);
            Check(loadedLedger.hasWaveReports && loadedLedger.waveReports.Find(r=>r.wave==1).kills==1 && loadedLedger.waveReports.Find(r=>r.wave==0).starved==1,
                "Wave history and upkeep counts survive a disk roundtrip");
            Console.WriteLine("PASS: "+checks+" tutorial/save regression checks (isolated runtime collaborators).");
        }
        finally { if(Directory.Exists(folder)) Directory.Delete(folder,true); }
    }
    static Tutorial AtSaved(ColonyGame g,SaveData save)
    {
        var t=new Tutorial();t.Init(g);t.Read(save);Time.unscaledTime+=1;t.Continue();return t;
    }
}
