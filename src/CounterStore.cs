using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Globalization;

// The flushed, append-only journal is authoritative; total.txt is a compatible
// snapshot. A stale window or damaged snapshot must never overwrite progress.
sealed class CounterStore {
    public readonly string Path;
    readonly string journal;
    DateTime lastWrite;
    long lastLength;
    public long Count { get; private set; }
    public string LastWarning { get; private set; }
    struct Record { public long Sequence,Value; public bool DamagedAfterLatest; }
    public CounterStore(string path) {
        Path=path; journal=path+".history";
        Directory.CreateDirectory(System.IO.Path.GetDirectoryName(path));
        if(!File.Exists(journal)) {
            long imported=File.Exists(path)?ReadLegacy(path):File.Exists(path+".bak")?ReadLegacy(path+".bak"):0;
            try { using(var stream=new FileStream(journal,FileMode.CreateNew,FileAccess.ReadWrite,FileShare.Read)) Append(stream,1,imported,"IMPORT"); }
            catch(IOException) { if(!File.Exists(journal)) throw; }
        }
        using(var stream=new FileStream(journal,FileMode.Open,FileAccess.ReadWrite,FileShare.Read)) {
            var record=ReadJournal(stream); ValidateRecovery(record); Count=record.Value;
            long snapshot;
            if(!TryReadLegacy(path,out snapshot)||snapshot!=Count) Snapshot();
        }
        lastWrite=File.GetLastWriteTimeUtc(journal);
        lastLength=new FileInfo(journal).Length;
    }
    static long ReadLegacy(string path) {
        long value;
        if(!TryReadLegacy(path,out value)) throw new IOException("El progreso no es valido. Se conserva el archivo para recuperarlo: "+path);
        return value;
    }
    static bool TryReadLegacy(string path,out long value) {
        value=0;
        try { return File.Exists(path)&&Int64.TryParse(File.ReadAllText(path).Trim(),out value)&&value>=0; }
        catch(IOException) { return false; }
    }
    static string Digest(string payload) {
        using(var sha=SHA256.Create()) return BitConverter.ToString(sha.ComputeHash(Encoding.UTF8.GetBytes(payload))).Replace("-","");
    }
    static Record ReadJournal(Stream stream) {
        stream.Position=0; var latest=new Record();
        using(var reader=new StreamReader(stream,Encoding.UTF8,true,1024,true)) {
            string line;
            while((line=reader.ReadLine())!=null) {
                if(String.IsNullOrWhiteSpace(line)) continue;
                var parts=line.Split('|'); long sequence,value,ticks;
                if(parts.Length!=5||!Int64.TryParse(parts[0],out sequence)||!Int64.TryParse(parts[1],out ticks)||!Int64.TryParse(parts[3],out value)||value<0) { latest.DamagedAfterLatest=true; continue; }
                string payload=String.Join("|",parts,0,4);
                if(!String.Equals(Digest(payload),parts[4],StringComparison.Ordinal)||sequence!=latest.Sequence+1) { latest.DamagedAfterLatest=true; continue; }
                string kind=parts[2];
                bool valid=latest.Sequence==0 ? kind=="IMPORT" : kind=="RESET" ? value==0 : kind=="CHANGE" ? (value==latest.Value || value==latest.Value+1 || value==latest.Value-1) : kind=="SET" && value>=latest.Value-1;
                if(valid) { latest.Sequence=sequence; latest.Value=value; latest.DamagedAfterLatest=false; }
                else latest.DamagedAfterLatest=true;
            }
        }
        if(latest.Sequence==0) throw new IOException("El registro de progreso esta danado. No se reiniciara el contador: "+((FileStream)stream).Name);
        return latest;
    }
    void ValidateRecovery(Record record) {
        if(!record.DamagedAfterLatest) return;
        long snapshot,backup;
        if(record.Value==0||(TryReadLegacy(Path,out snapshot)&&snapshot>record.Value)||(TryReadLegacy(Path+".bak",out backup)&&backup>record.Value))
            throw new IOException("El registro esta incompleto y no permite confirmar el total. Se conservan todos los archivos; no se reiniciara ni reducira el progreso.");
    }
    static void Append(FileStream stream,long sequence,long value,string kind) {
        string payload=sequence.ToString(CultureInfo.InvariantCulture)+"|"+DateTime.UtcNow.Ticks.ToString(CultureInfo.InvariantCulture)+"|"+kind+"|"+value.ToString(CultureInfo.InvariantCulture);
        stream.Seek(0,SeekOrigin.End);
        byte[] data=Encoding.UTF8.GetBytes("\n"+payload+"|"+Digest(payload)+"\n");
        stream.Write(data,0,data.Length); stream.Flush(true);
    }
    public bool Refresh() {
        DateTime changed=File.GetLastWriteTimeUtc(journal);
        long length=new FileInfo(journal).Length;
        if(changed==lastWrite&&length==lastLength) return false;
        long previous=Count;
        using(var stream=new FileStream(journal,FileMode.Open,FileAccess.Read,FileShare.ReadWrite)) { var record=ReadJournal(stream); ValidateRecovery(record); Count=record.Value; }
        lastWrite=changed; lastLength=length; return Count!=previous;
    }
    public void Adjust(int delta) {
        if(delta!=1&&delta!=-1) throw new ArgumentException("Solo se permite sumar o restar un cofre.");
        Commit(delegate(long current) { return delta>0 ? checked(current+1) : Math.Max(0,current-1); },"CHANGE");
    }
    public void Reset() { Commit(delegate(long current) { return 0; },"RESET"); }
    // Retained for migration/tests. Production changes use Adjust or Reset.
    public void Set(long value) {
        if(value<0) throw new ArgumentOutOfRangeException("value");
        Commit(delegate(long current) {
            if(value<current-1) throw new InvalidOperationException("Solo el boton Reiniciar total puede borrar el progreso.");
            return value;
        },"SET");
    }
    void Commit(Func<long,long> next,string kind) {
        using(var stream=new FileStream(journal,FileMode.Open,FileAccess.ReadWrite,FileShare.Read)) {
            var previous=ReadJournal(stream); ValidateRecovery(previous); long value=next(previous.Value);
            if(value!=previous.Value || kind=="RESET") Append(stream,previous.Sequence+1,value,kind);
            Count=value; Snapshot();
        }
        lastWrite=File.GetLastWriteTimeUtc(journal);
        lastLength=new FileInfo(journal).Length;
    }
    void Snapshot() {
        string temp=Path+".tmp-"+Guid.NewGuid().ToString("N");
        try {
            using(var stream=new FileStream(temp,FileMode.CreateNew,FileAccess.Write,FileShare.None)) {
                var data=Encoding.UTF8.GetBytes(Count.ToString(CultureInfo.InvariantCulture)); stream.Write(data,0,data.Length); stream.Flush(true);
            }
            if(File.Exists(Path)) File.Replace(temp,Path,Path+".bak"); else File.Move(temp,Path);
            LastWarning=null;
        } catch(IOException ex) { LastWarning="El progreso esta guardado en el registro, pero no se pudo actualizar total.txt: "+ex.Message; }
        catch(UnauthorizedAccessException ex) { LastWarning="El progreso esta guardado en el registro, pero no se pudo actualizar total.txt: "+ex.Message; }
        finally { try { if(File.Exists(temp)) File.Delete(temp); } catch(IOException) { } catch(UnauthorizedAccessException) { } }
    }
}
