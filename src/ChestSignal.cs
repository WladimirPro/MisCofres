using System;
using System.Text.RegularExpressions;

sealed class ChestSignal {
    bool visible;
    DateTime lastSeen=DateTime.MinValue, lastCount=DateTime.MinValue;
    public static bool IsOpening(string text) {
        string t=(text??"").ToUpperInvariant();
        if(!Regex.IsMatch(t,@"\bCOFRE(?:S)?\b")) return false;
        if(Regex.IsMatch(t,@"\b(BUSCAR|REGISTRA|ABRE|MISION|MISIONES|DESAFIO)\b")) return false;
        bool completed=Regex.IsMatch(t,@"\b(REGISTRADO|ABIERTO|SEARCHED|OPENED)\b");
        bool xp=Regex.IsMatch(t,@"\+\s*\d[\d., ]*\s*(PE|XP)\b");
        return completed||xp;
    }
    public bool Observe(bool opening,DateTime now) {
        if(!opening) {
            if(now-lastSeen>TimeSpan.FromSeconds(1.0)) visible=false;
            return false;
        }
        lastSeen=now;
        if(visible) return false;
        visible=true;
        if(now-lastCount<TimeSpan.FromSeconds(2.5)) return false;
        lastCount=now; return true;
    }
}
