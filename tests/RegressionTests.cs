using System;
using System.IO;
static class RegressionTests {
    static void Assert(bool condition,string message) { if(!condition) throw new Exception(message); }
    static void Main() {
        string folder=Path.Combine(Path.GetTempPath(),"MisCofres-tests-"+Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(folder);
        try {
            string path=Path.Combine(folder,"total.txt");
            var counter=new CounterStore(path);
            Assert(counter.Count==0,"New user must start at zero");
            counter.Set(41); counter.Set(42);
            for(int i=0;i<5;i++) Assert(new CounterStore(path).Count==42,"Reopening reset progress");
            Assert(File.ReadAllText(path+".bak")=="41","Previous total backup missing");
            string prefsPath=Path.Combine(folder,"ajustes.xml");
            var prefs=new AppSettings { SizePercent=125,AddBinding=116,Automatic=false };
            prefs.Save(prefsPath);
            Assert(AppSettings.Load(prefsPath).SizePercent==125,"Settings did not persist");
            Assert(!AppSettings.Load(prefsPath).Automatic,"Automatic preference changed");
            Assert(new CounterStore(path).Count==42,"Saving settings reset progress");
            counter.Set(0);
            Assert(new CounterStore(path).Count==0,"Explicit reset did not persist");
            counter.Set(50); counter.Set(51); File.Delete(path);
            Assert(new CounterStore(path).Count==50,"Backup recovery failed");
            File.WriteAllText(path,"corrupt");
            bool rejected=false; try { new CounterStore(path); } catch(IOException) { rejected=true; }
            Assert(rejected,"Corrupt data must not silently reset to zero");
            var input=new SideButtonInput();
            Assert(!input.ProcessRaw(0x40),"Lower side button must remain free");
            Assert(input.ProcessRaw(0x100),"Upper side button must count");
            Assert(!input.ProcessRaw(0x100),"Held side button repeated");
            input.ProcessRaw(0x200);
            Assert(input.ProcessRaw(0x100),"Next side-button press was missed");
            Assert(!new SideButtonInput { SelectedButton=0 }.ProcessRaw(0x100),"Disabled binding counted");
            var gate=new ChestSignal(); var now=DateTime.UtcNow;
            Assert(gate.Observe(true,now),"First notification missed");
            Assert(!gate.Observe(true,now.AddSeconds(4)),"Sustained notification counted twice");
            gate.Observe(false,now.AddSeconds(6));
            Assert(gate.Observe(true,now.AddSeconds(7)),"New notification missed");
            Console.WriteLine("PASS: progress survives reopening/settings, explicit reset, backup, corruption protection, mouse buttons and duplicate suppression.");
        } finally {
            foreach(var file in Directory.GetFiles(folder)) File.Delete(file);
            Directory.Delete(folder);
        }
    }
}
