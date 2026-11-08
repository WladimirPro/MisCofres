// Standard Windows side-button messages: upper/forward = XBUTTON2.
// Returns 0 for unrelated input, 1 to count, 2 to consume without counting.
sealed class SideButtonInput {
    bool pressed;
    public int SelectedButton = 2;
    public int Process(int message, uint data) {
        if ((message != 0x020B && message != 0x020C) || (data >> 16) != SelectedButton) return 0;
        if (message == 0x020C) { pressed = false; return 2; }
        if (pressed) return 2;
        pressed = true;
        return 1;
    }
    public bool ProcessRaw(ushort flags) {
        if(SelectedButton!=1 && SelectedButton!=2) return false;
        ushort down = SelectedButton == 2 ? (ushort)0x100 : (ushort)0x40;
        ushort up = SelectedButton == 2 ? (ushort)0x200 : (ushort)0x80;
        bool count = false;
        if ((flags & down) != 0) count = Process(0x020B, (uint)SelectedButton << 16) == 1;
        if ((flags & up) != 0) Process(0x020C, (uint)SelectedButton << 16);
        return count;
    }
}
