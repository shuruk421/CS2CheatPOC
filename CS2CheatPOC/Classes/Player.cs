using System.Runtime.InteropServices;

[StructLayout(LayoutKind.Explicit)]
struct Player
{
    [FieldOffset(272)]
    public float XPos; //0x0110
	[FieldOffset(276)]
    public float YPos; //0x0114
	[FieldOffset(280)]
    public float ZPos; //0x0118
	[FieldOffset(284)]
    public float pitch; //0x011C
    [FieldOffset(288)]
    public float yaw; //0x0120
    [FieldOffset(338)]
    public byte m_bSpotted; //0x152
    [FieldOffset(352)]
    public int Health; //0x0160
    [FieldOffset(364)]
    public int TeamID; //0x16C
    [FieldOffset(380)]
    public int END; //0x0176
}