using System;
using System.Numerics;
using System.Runtime.InteropServices;

[StructLayout(LayoutKind.Explicit)]
public struct Player
{
    [FieldOffset(0x318)]
    public int m_iMaxHealth;
    [FieldOffset(0x86C)]
    public float XPos;
    [FieldOffset(0x86C + 4)]
    public float YPos;
    [FieldOffset(0x86C + 8)]
    public float ZPos;
    [FieldOffset(0x3B8)]
    public byte m_fFlags;
    [FieldOffset(0x31C)]
    public int m_iHealth;
    [FieldOffset(0x3AF)]
    public int m_iTeamNum;
    [FieldOffset(0x300)]
    public long m_pGameSceneNode; // CGameSceneNode* (CSkeletonInstance*)
}

[StructLayout(LayoutKind.Explicit)]
public struct CSkeletonInstance
{
    [FieldOffset(0x160)]
    public CModelState m_modelState; // 0x160 - 0x390
}

[StructLayout(LayoutKind.Explicit)]
public struct CModelState
{
    [FieldOffset(0x80)]
    public long m_boneArray; // CBoneData*
}

public struct CBoneData
{
    public Vector3 Location;
    public float Scale;
    public Quaternion Rotation;
}