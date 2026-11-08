using System;

sealed class StableNotice {
    int hits;
    DateTime last=DateTime.MinValue;
    public bool Observe(bool matched,DateTime now) {
        if(!matched) { hits=0; last=now; return false; }
        if(now-last>TimeSpan.FromMilliseconds(700)) hits=0;
        last=now; hits++;
        return hits>=2;
    }
    public void Clear() { hits=0; last=DateTime.MinValue; }
}
